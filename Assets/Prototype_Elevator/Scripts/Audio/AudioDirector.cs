using UnityEngine;
using Ascend.Prototype.Events;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Audio
{
    /// <summary>
    /// 큐가 실제로 나가는 통로. 채널 넷을 나눠 두는 이유는 한 채널에 전부 넣으면
    /// 캐스케이드 연타가 승객 목소리를 잘라먹기 때문이다 — <c>PlayOneShot</c>은 겹치지만
    /// <see cref="AudioSource.pitch"/>는 소스 단위라서 뒤에 온 큐가 앞의 피치를 바꿔 버린다.
    /// </summary>
    public enum AudioCueChannel
    {
        /// <summary>기계 동작 — 레버, 확정, 과수확, 붕괴.</summary>
        Machine = 0,

        /// <summary>룰렛 판정 — 열 공개, 수확, 정화, 캐스케이드, 임계점.</summary>
        Event = 1,

        /// <summary>승객 비언어 음성.</summary>
        Passenger = 2,

        /// <summary>경고 — 잔류 피해, 위험 단계 전이.</summary>
        Warning = 3,
    }

    /// <summary>
    /// 사건 버스를 듣고 소리를 낸다. UP-AUD-02·03·04의 씬 쪽 끝이다.
    ///
    /// 판정에는 관여하지 않는다. 이 컴포넌트를 통째로 꺼도 게임과 테스트는 그대로 돈다
    /// (`TECH_SPEC.md` §5). 반대로 이것이 없으면 `MASTER_PRD.md` §4.1 #18의 감각 채널 넷 중
    /// 소리 하나가 비고, §18 Phase 4의 "오디오만 듣는 테스트"를 통과할 수 없다.
    ///
    /// **왜 큐를 시간축에 펼치는가.** <c>FloorSession.PublishSpinDetail</c>은 한 스핀의 사건을
    /// 전부 **같은 프레임에 동기적으로** 발행한다. 화면 쪽은 <c>SpinPresenter</c>가
    /// <c>SpinResolution</c>을 직접 읽어 코루틴으로 펼치지만, 사건 버스는 그 펼침을
    /// 모른다. 받은 순서대로 즉시 재생하면 열 공개·수확·정화·캐스케이드 20단계가 한 순간에
    /// 뭉쳐 한 번의 잡음이 되고, "캐스케이드 깊이를 귀로 셀 수 있어야 한다"는 요구가
    /// 물리적으로 불가능해진다. 그래서 판정 사건은 최소 간격을 두고 줄을 세운다.
    ///
    /// 플레이어의 직접 동작(레버·확정·과수확·붕괴)은 줄을 서지 않는다. 입력에 대한 응답이
    /// 0.5초 늦게 오면 그건 연출이 아니라 지연이다.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        /// <summary>
        /// 대기열 상한. 20연쇄 스핀 하나가 만드는 사건 수보다 넉넉하다.
        /// 넘치면 버리되 조용히 버리지 않는다 — <see cref="DroppedCueCount"/>가 센다.
        /// </summary>
        private const int QueueCapacity = 96;

        [Header("배선 — 없어도 동작한다. Attach(bus)로 직접 붙여도 된다")]
        [Tooltip("설정하면 RunStarted 마다 그 런의 사건 버스에 자동으로 붙는다.")]
        [SerializeField] private RunSessionBehaviour _run;

        [Header("볼륨")]
        [Tooltip("채널 균형과 덕킹 배율. 비면 아래 슬라이더를 그대로 쓴다 — 소리 없이 죽지 않는다.")]
        [SerializeField] private Data.Profiles.AudioMixProfile _mixProfile;

        [Tooltip("과수확 정적 구간의 길이와 복귀 속도. 비면 아래 슬라이더를 쓴다.")]
        [SerializeField] private Data.Profiles.OverharvestProfile _overharvestProfile;

        [Tooltip("위험 단계별 험 값의 원본. 비면 코드 프리셋 Standard 를 쓴다. 소리를 내지는 않고 목표값만 계산한다 — 험을 실제로 울리는 것은 RiskStateView 다.")]
        [SerializeField] private Data.Profiles.DangerFeedbackProfile _dangerProfile;

        [Tooltip("사이렌 허용 여부와 음량. 비면 전부 허용으로 본다(PRD §12 「사이렌 피로도」).")]
        [SerializeField] private Data.Profiles.AccessibilityProfile _accessibilityProfile;

        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _machineVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _eventVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _passengerVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _warningVolume = 0.95f;

        [Header("과수확 정적 (UP-AUD-03 · MASTER_PRD §7.3)")]
        [Tooltip("완전 정적 구간의 길이. 0.3~0.7 밖의 값은 코드가 조인다.")]
        [SerializeField, Range(0.3f, 0.7f)] private float _silenceSeconds = 0.5f;
        [Tooltip("소리가 줄어드는 시간(§7.3 2단계).")]
        [SerializeField, Min(0.01f)] private float _duckSeconds = 0.12f;
        [Tooltip("소리가 돌아오는 시간(§7.3 5단계).")]
        [SerializeField, Min(0.01f)] private float _resumeSeconds = 0.25f;
        [Tooltip("정적 동안 AudioListener 전체를 함께 줄인다. 끄면 이 컴포넌트가 내는 소리만 조용해진다.")]
        [SerializeField] private bool _duckGlobalListener = true;

        [Header("승객 비언어 음성 (UP-AUD-04 · MASTER_PRD §9.3)")]
        [Tooltip("승객 반응 시스템이 소리를 요청하기 전까지, 사건 버스만으로 승객 음성을 낸다. " +
                 "누군가 PlayPassengerVoice 를 부르기 시작하면 자동으로 물러난다 — 두 소유자가 겹치면 한 사건에 목소리가 두 번 난다.")]
        [SerializeField] private bool _voiceFromEvents = true;

        [Tooltip("승객 음성 사이의 최소 간격(초). 쉬지 않고 내는 소리는 반응이 아니라 소음이다(PRD §9.4).")]
        [SerializeField, Min(0f)] private float _voiceCooldownSeconds = 0.9f;

        [Header("사이렌 (UP-RISK-05 · Notion PRD §8.3)")]
        [Tooltip("끄면 사이렌만 사라진다. 험·응력음·충격음은 그대로 남는다.")]
        [SerializeField] private bool _sirenEnabled = true;

        [Tooltip("사이렌 사이의 최소 간격(초). 이것이 없으면 단계가 빠르게 오갈 때 " +
                 "원샷이 겹쳐 사실상 지속 사이렌이 되고, 그게 §8.3 이 금지하는 그 상태다.")]
        [SerializeField, Min(0f)] private float _sirenCooldownSeconds = 1.2f;

        [Header("지속 위험 레이어 (UP-RISK-05 · Notion PRD §8.3)")]
        [Tooltip("사이렌이 사건에만 울리는 대신, 그 사이 시간을 채우는 저역 베드와 금속 응력음. " +
                 "끄면 위험 단계의 지속 표현은 RiskStateView 의 험 하나로 돌아간다.")]
        [SerializeField] private bool _dangerBedEnabled = true;

        [Tooltip("저역 베드 배율. 험보다 아래 음역이라 험과 겹치지 않는다. 0 이면 저역층만 사라진다.")]
        [SerializeField, Range(0f, 2f)] private float _subBedScale = 0.9f;

        [Tooltip("금속 응력음 배율. 안정 단계에서는 이 값과 무관하게 0 이다(DangerBed).")]
        [SerializeField, Range(0f, 2f)] private float _stressBedScale = 0.7f;

        [Tooltip("단계가 바뀔 때 지속 레이어를 갈아 끼우는 시간(초). 도는 파형을 도중에 " +
                 "바꾸면 딱 소리가 나므로 한 번 내렸다 올린다.")]
        [SerializeField, Min(0.02f)] private float _bedSwapSeconds = 0.18f;

        [Header("판정 사건 줄 세우기")]
        [Tooltip("소리가 화면보다 이만큼 넘게 뒤처지면 버린다. 밀린 소리는 정보가 아니라 소음이다.")]
        [SerializeField, Min(0.2f)] private float _maxLeadSeconds = 1.5f;

        [Header("검증")]
        [Tooltip("큐마다 콘솔에 한 줄 남긴다. UP-AUD-02의 검증 기준이 '사운드 이벤트 발동 로그'다. 문자열을 만들므로 평소에는 끈다.")]
        [SerializeField] private bool _logCues;
        [SerializeField] private bool _prewarmClips = true;

        private readonly SilenceWindow _silence = new SilenceWindow();
        private AudioSource[] _sources;
        private Scheduled[] _queue;
        private int _queueHead;
        private int _queueCount;
        private float _nextFree;
        private bool[] _bakeWarned;
        private Data.Profiles.AudioMixSnapshot _mix;
        private Data.Profiles.DangerFeedbackSnapshot _danger;
        private Data.Profiles.AccessibilitySnapshot _accessibility;
        private string _mixSource = "(미초기화)";
        private string _overharvestSource = "(미초기화)";
        private string _dangerSource = "(미초기화)";
        private string _accessibilitySource = "(미초기화)";
        private bool _overflowWarned;

        // ── 승객 음성 ────────────────────────────────────────────────────────
        private bool _externalVoiceDriver;
        private float _voiceReadyAt;

        // ── 사이렌 ───────────────────────────────────────────────────────────
        private float _sirenReadyAt;

        // ── 지속 위험 레이어 ─────────────────────────────────────────────────
        // 험이 아니다. 험(고른 50/100/150Hz 톤)의 소유자는 `RiskStateView` 이고 그대로 둔다.
        // 여기 둘은 그 아래(저역)와 그 위(응력 삐걱임)이며 음역이 겹치지 않는다
        // (`SustainedBedFactory` 클래스 주석).
        private AudioSource _subBed;
        private AudioSource _stressBed;
        private DangerBedTargets _bed;
        private int _bedLevel = -1;
        private float _bedFade;

        // ── 험 목표값 ────────────────────────────────────────────────────────
        // 험 자체는 `RiskStateView` 가 소유한다(같은 험이 두 겹으로 깔리면 안 된다).
        // 여기서 계산하는 것은 "그 험이 지금 얼마여야 하는가"뿐이다 — 값의 주인과
        // 소리의 주인을 나눈다. 배율이 `AudioMixProfile` 에 있고 형태가
        // `DangerFeedbackProfile` 에 있으므로, 둘을 곱하는 자리가 어딘가 하나는 있어야 한다.
        private Risk.RiskLevel _riskLevel = Risk.RiskLevel.Stable;
        private bool _riskRising;
        private float _humVolume;
        private float _humPitch;
        private float _humVolumeScale = 1f;
        private float _humPitchScale = 1f;
        private bool _humValid;
        private Risk.RiskLevel _humLevel = Risk.RiskLevel.Stable;
        private float _humGain = 1f;

        private GameEventBus _bus;
        private System.Action<GameEvent> _handler;

        private bool _listenerDucked;
        private float _listenerBase = 1f;

        /// <summary>지금까지 실제로 재생한 큐 수. 무영상 검증이 "정말 울렸는가"를 물을 때 쓴다.</summary>
        /// <summary>지금 쓰고 있는 믹스의 출처. 검증 하네스가 「에셋이 실제로 읽혔는가」를 묻는다.</summary>
        public string MixSource => _mixSource;

        /// <summary>정적 구간 값의 출처. 같은 이유로 노출한다.</summary>
        public string OverharvestSource => _overharvestSource;

        /// <summary>위험 연출 값의 출처. 험 목표값이 에셋에서 왔는지 코드 프리셋에서 왔는지 가른다.</summary>
        public string DangerSource => _dangerSource;

        /// <summary>접근성 설정의 출처. 사이렌을 껐는데 계속 울린다면 여기부터 본다.</summary>
        public string AccessibilitySource => _accessibilitySource;

        /// <summary>실제로 쓰이는 정적 구간 길이(초). PRD §7 의 0.3~0.7 범위를 지켜야 한다.</summary>
        public float SilenceSeconds => _silenceSeconds;

        /// <summary>사건 버스에서 읽은 현재 위험 단계. 험 목표값이 여기서 갈린다.</summary>
        public Risk.RiskLevel CurrentRiskLevel => _riskLevel;

        /// <summary>
        /// 지금 위험 단계에서 험이 내야 할 볼륨. 정적 게인이 이미 곱해져 있다 —
        /// 과수확 정적에 험만 남아 있으면 그 연출은 「음소거됐나」로 읽힌다(파일 끝 배선 메모 4번).
        ///
        /// **이 컴포넌트는 험을 재생하지 않는다.** 험의 소유자(`RiskStateView`)가 이 값을
        /// 읽어 가야 `AudioMixProfile` 의 단계별 배율이 실제로 소리에 닿는다.
        /// </summary>
        public float HumVolume => _humVolume;

        /// <summary>같은 이유로 계산해 두는 험 피치. 곱하기 전의 형태는 `DangerFeedbackProfile` 이 정한다.</summary>
        public float HumPitch => _humPitch;

        /// <summary>지금 단계에 적용 중인 험 볼륨 배율(`AudioMixProfile._humVolumeScale`). 1이면 위험 프로파일 값 그대로다.</summary>
        public float HumVolumeScale => _humVolumeScale;

        /// <summary>지금 단계에 적용 중인 험 피치 배율(`AudioMixProfile._humPitchScale`).</summary>
        public float HumPitchScale => _humPitchScale;

        // ── 지속 위험 레이어 관측값 (UP-RISK-05) ─────────────────────────────
        //
        // **스피커에 실제로 나간 값을 노출한다.** 목표값(`DangerBedTargets`)이 아니라
        // `AudioSource.volume` 을 그대로 읽는 이유는 `RiskStateView.EffectiveHumVolume` 과
        // 같다 — 목표값만 노출하면 「계산은 됐지만 소스에 닿지 않았다」를 반증할 수단이 없다.
        // 확인법: `_dangerBedEnabled` 를 끄면 둘 다 0 이 되고, 접근성 프로파일의
        // `_lowFrequencyScale` 을 0 으로 내리면 저역만 0 이 된다.

        /// <summary>지금 나가고 있는 저역 베드 볼륨. 접근성 저주파 배율과 정적 게인이 이미 곱해져 있다.</summary>
        public float SubBedVolume => _subBed != null ? _subBed.volume : 0f;

        /// <summary>지금 나가고 있는 금속 응력음 볼륨. 안정 단계에서는 0 이어야 한다.</summary>
        public float StressBedVolume => _stressBed != null ? _stressBed.volume : 0f;

        /// <summary>응력음 재생 피치. 단계가 오를수록 커진다(조여진다).</summary>
        public float StressBedPitch => _stressBed != null ? _stressBed.pitch : 0f;

        /// <summary>지금 깔려 있는 지속 레이어의 단계. −1 이면 아직 한 번도 세우지 않았다.</summary>
        public int DangerBedLevel => _bedLevel;

        /// <summary>지속 레이어가 켜져 있는가. 씬에서 끄면 험 하나로 돌아간다.</summary>
        public bool DangerBedEnabled => _dangerBedEnabled;

        /// <summary>지금까지 울린 사이렌 수. §8.3 이 금지하는 「지속 재생」은 이 수가 폭주하는 것으로 나타난다.</summary>
        public int SirenCueCount { get; private set; }

        /// <summary>사건 버스에서 만들어 낸 승객 음성 수. 승객 반응 시스템이 붙으면 여기가 멈춘다.</summary>
        public int EventVoiceCount { get; private set; }

        /// <summary>
        /// 승객 반응 시스템이 직접 요청해 나간 음성 수. <see cref="EventVoiceCount"/>가 멈추고
        /// 이쪽이 늘기 시작하면 배선이 실제로 넘어간 것이다 — 둘 다 0 이면 승객 채널이 비었다.
        /// </summary>
        public int ExternalVoiceCount { get; private set; }

        /// <summary>
        /// 지금까지 요청된 음성 종류의 비트 집합. 비트 n 은 <c>(PassengerVoiceKind)n</c>.
        /// `MASTER_PRD.md` §9.3 의 다섯 표현이 **각각** 났는지는 총합으로 셀 수 없다 —
        /// 「음성 40건」은 호흡만 40번 난 것과 구분되지 않는다.
        /// </summary>
        public int VoiceKindsMask { get; private set; }

        /// <summary>실제로 난 음성 종류 수. 다섯이 목표다.</summary>
        public int VoiceKindCount
        {
            get
            {
                int n = 0;
                for (int mask = VoiceKindsMask; mask != 0; mask &= mask - 1) n++;
                return n;
            }
        }

        /// <summary>승객 반응 시스템이 <see cref="PlayPassengerVoice"/>로 소리를 요청하고 있는가.</summary>
        public bool HasExternalVoiceDriver => _externalVoiceDriver;

        public int PlayedCueCount { get; private set; }

        /// <summary>대기열이 넘치거나 너무 밀려 버린 큐 수. 0이 아니면 간격 설정이 틀린 것이다.</summary>
        public int DroppedCueCount { get; private set; }

        /// <summary>
        /// 실제로 재생된 큐 **종류**의 비트 집합. 비트 n 은 `(AudioCueKind)n`.
        /// 총합(<see cref="PlayedCueCount"/>)으로는 「10종이 각각 났는가」를 셀 수 없다.
        /// </summary>
        public int PlayedKindsMask { get; private set; }

        /// <summary>재생된 큐 종류 수.</summary>
        public int PlayedKindCount
        {
            get
            {
                int n = 0;
                for (int mask = PlayedKindsMask; mask != 0; mask &= mask - 1) n++;
                return n;
            }
        }

        /// <summary>
        /// 소리 없이 지나간 사건 수. 층 흐름 사건처럼 **의도적으로** 조용한 것도 포함한다 —
        /// 0이어야 하는 값이 아니라, 매핑 표를 고쳤을 때 이 수가 크게 움직였는지 보는 값이다.
        /// </summary>
        public int SilentEventCount { get; private set; }

        /// <summary>현재 정적 게인(0~1). 승객 응시·기계 정지 같은 다른 채널이 이걸 따라갈 수 있다.</summary>
        public float SilenceGain { get { return _silence.GainAt(Now); } }

        /// <summary>정적 구간 타임라인. 검증 하네스가 눈금을 잴 때 읽는다.</summary>
        public SilenceWindow Silence { get { return _silence; } }

        private static float Now
        {
            // 오디오는 타임스케일을 따르지 않는다. 정지·슬로우 중에 정적 구간이 같이 늘어나면
            // 0.3~0.7초라는 약속(§7.3)이 깨진다.
            get { return Time.unscaledTime; }
        }

        // ── 수명 ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _handler = OnEvent;
            _queue = new Scheduled[QueueCapacity];
            _bakeWarned = new bool[32];

            // 에셋을 한 번만 스냅샷으로 뜬다. 매 프레임 `ScriptableObject` 를 타고 들어가면
            // 채널 넷 × 60fps 만큼의 간접 참조가 생기고, 런 중에 값이 바뀌면 오히려
            // 「무엇을 들었는지」가 재현되지 않는다.
            _mix = Data.Profiles.AudioMixProfile.SnapshotOrDefault(_mixProfile, nameof(AudioDirector));
            _mixSource = _mixProfile != null ? _mixProfile.name : "인스펙터 슬라이더";

            // 정적 구간의 길이는 **PRD §7 이 0.3~0.7초로 못박은 값**이고, 그것을 데이터로
            // 빼 둔 것이 `OverharvestProfile` 이다. 여기서 읽지 않으면 그 에셋은
            // 「만들어졌고 아무도 부르지 않는」 상태로 남는다.
            var over = Data.Profiles.OverharvestProfile.SnapshotOrDefault(
                _overharvestProfile, nameof(AudioDirector));
            _overharvestSource = _overharvestProfile != null
                ? _overharvestProfile.name : "인스펙터 슬라이더";

            if (_overharvestProfile != null)
            {
                // 구간 길이는 프로파일의 최소·최대 중앙값을 쓴다. 인스펙터 값은
                // 프로파일이 없을 때의 폴백으로 남긴다 — 둘 중 하나를 지우면
                // 씬에서 만질 손잡이가 사라지거나 데이터가 무의미해진다.
                _silenceSeconds = Mathf.Clamp(
                    (over.MinSilenceSeconds + over.MaxSilenceSeconds) * 0.5f,
                    over.MinSilenceSeconds, over.MaxSilenceSeconds);
                _resumeSeconds = Mathf.Max(0.01f, over.ResumeFadeSeconds);
            }

            // 위험 프로파일은 **경고 없이** 코드 프리셋으로 폴백한다.
            // `SnapshotOrDefault` 는 경고를 남기는데, 험의 소유자는 `RiskStateView` 이고
            // 이쪽은 목표값 계산기일 뿐이라 같은 경고가 두 번 나면 진짜 배선 누락이 묻힌다.
            _danger = _dangerProfile != null
                ? _dangerProfile.Snapshot()
                : Data.Profiles.DangerFeedbackProfile.DefaultSnapshot;
            _dangerSource = _dangerProfile != null ? _dangerProfile.name : "코드 프리셋 Standard";

            // 접근성은 없는 것이 정상이다(아직 옵션 메뉴가 없다). 그래서 이쪽도 조용하다.
            _accessibility = Data.Profiles.AccessibilityProfile.SnapshotOrDefault(_accessibilityProfile);
            _accessibilitySource = _accessibilityProfile != null
                ? _accessibilityProfile.name : "코드 기본값(전부 허용)";

            _silence.DuckSeconds = _duckSeconds;
            _silence.ResumeSeconds = _resumeSeconds;

            // 첫 Update 전에 누가 읽어도 값이 있어야 한다. 0으로 시작하면 「험이 꺼졌다」와
            // 「아직 계산 전이다」가 구분되지 않는다.
            RefreshHumTargets(1f);

            BuildSources();

            // 첫 사용 순간에 굽지 않는다. 하필 그 순간이 레버를 당긴 프레임이라
            // 성능 캡처에 스파이크로 찍힌다. 지속 레이어는 더하다 — 그 순간이 곧
            // 위험 단계가 오른 프레임이다.
            if (_prewarmClips)
            {
                ProceduralClipFactory.Prewarm();
                if (_dangerBedEnabled) SustainedBedFactory.Prewarm();
            }

            if (_run == null) return;

            _run.RunStarted += OnRunStarted;

            // RunSessionBehaviour.Awake 가 이미 ResetRun 을 끝냈을 수 있다. 스크립트 실행 순서에
            // 기대면 어떤 날은 붙고 어떤 날은 안 붙는다.
            if (_run.Session != null) Attach(_run.Session.Events);
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Detach();
            RestoreListener();
        }

        private void OnDisable()
        {
            // 정적 도중에 꺼지면 AudioListener 가 0인 채로 남는다. 그 상태는 게임 전체가
            // 무음이 된 것처럼 보이고, 원인이 이 컴포넌트라는 단서가 아무 데도 없다.
            RestoreListener();

            // 지속 레이어는 **자식 오브젝트의** AudioSource 라 이 컴포넌트를 꺼도 계속 돈다.
            // 「오디오를 껐는데 웅웅거리는 소리가 남는다」는 원인을 찾기 가장 어려운 종류의
            // 버그다 — 끄는 스위치가 소리의 주인이 아닌 것처럼 보이기 때문이다.
            StopDangerBed();
        }

        private void OnRunStarted(RunSession session)
        {
            if (session == null) return;
            Attach(session.Events);
        }

        /// <summary>
        /// 사건 버스에 붙는다. 런이 다시 시작되면 버스도 새로 만들어지므로 매번 다시 붙어야 한다
        /// (<c>RunSession.Events</c>는 세션 인스턴스 소유다).
        /// </summary>
        public void Attach(GameEventBus bus)
        {
            if (ReferenceEquals(_bus, bus)) return;
            Detach();

            _bus = bus;
            if (_bus == null)
            {
                Debug.LogWarning("[상승] AudioDirector.Attach 에 null 버스가 왔다 — 이 런은 소리가 나지 않는다");
                return;
            }

            _bus.Published += _handler;
            ResetPlayback();
        }

        /// <summary>구독을 끊는다. 끊지 않으면 죽은 런의 사건이 계속 소리를 낸다.</summary>
        public void Detach()
        {
            if (_bus != null) _bus.Published -= _handler;
            _bus = null;
        }

        private void ResetPlayback()
        {
            _queueHead = 0;
            _queueCount = 0;
            _nextFree = Now;
            _silence.Reset();
            _overflowWarned = false;

            // 쿨다운은 런 경계에서 풀어 준다. 지난 런의 마지막 사이렌 때문에 새 런의
            // 첫 경보가 조용해지면, 원인이 이 타이머라는 단서가 아무 데도 없다.
            _sirenReadyAt = Now;
            _voiceReadyAt = Now;

            // 위험 단계도 런과 함께 처음으로 돌아간다. 남겨 두면 새 런의 첫
            // `RiskLevelChanged` 가 "하강"으로 읽혀 사이렌이 통째로 빠진다.
            _riskLevel = Risk.RiskLevel.Stable;
            _riskRising = false;
            _humValid = false;

            // 지속 레이어도 처음으로 돌아간다. −1 로 두면 다음 프레임이 곧바로 안정 단계
            // 클립을 세운다(페이드 0 에서 시작하므로 갈아 끼우는 지연이 없다).
            _bedLevel = -1;
            _bedFade = 0f;

            RestoreListener();
        }

        // ── 프레임 ───────────────────────────────────────────────────────────

        private void Update()
        {
            // 이 메서드는 문자열·람다·배열을 만들지 않는다. `MASTER_PRD.md` §13.2의
            // "워밍업 후 매 프레임 0 B"가 오디오 때문에 깨지면 안 된다.
            float now = Now;
            float gain = _silence.GainAt(now);

            ApplyChannelVolumes(gain);
            ApplyListenerDuck(now, gain);
            RefreshHumTargets(gain);
            UpdateDangerBed(Time.unscaledDeltaTime);
            DrainQueue(now, gain);
        }

        /// <summary>
        /// 험 목표값을 다시 계산한다. 단계와 게인이 그대로면 아무 일도 하지 않는다 —
        /// <c>DangerFeedbackSnapshot.For</c> 는 구조체 사본을 만들므로 매 프레임 무조건
        /// 돌릴 이유가 없다. (힙 할당은 없지만 공짜도 아니다.)
        ///
        /// **여기가 `AudioMixProfile` 의 험 배율 네 함수가 실제로 소비되는 유일한 자리다.**
        /// 그전까지 그 8개 필드는 에셋에만 있고 런타임에는 아무도 읽지 않았다.
        /// </summary>
        private void RefreshHumTargets(float gain)
        {
            if (_humValid && _riskLevel == _humLevel && gain == _humGain) return;

            Risk.RiskProfile profile = _danger.For(_riskLevel);

            _humVolumeScale = _mix.HumVolumeScaleFor(_riskLevel);
            _humPitchScale = _mix.HumPitchScaleFor(_riskLevel);

            // 형태는 위험 프로파일이, 크기는 믹스가 정한다(`AudioMixProfile` 클래스 주석).
            // 정적 게인을 여기서 곱해 두는 이유: 험을 우회로(AudioListener)로 줄이면
            // 플레이어의 발소리까지 같이 사라져 정적이 고장으로 읽힌다.
            float humRaw = _mix.HumVolumeFor(_riskLevel, in profile);
            _humVolume = humRaw * gain;
            _humPitch = _mix.HumPitchFor(_riskLevel, in profile);

            // 지속 위험 레이어도 같은 두 출처에서 나온다(UP-RISK-05).
            // **게인이 곱해지지 않은** 험 볼륨을 넘긴다 — `DangerBed` 가 게인을 따로 받는다.
            // 이미 곱한 값을 넘기면 정적 구간에 지속층만 제곱으로 사라져,
            // §7.3 이 요구한 정적이 「오디오가 죽었다」로 읽힌다.
            _bed = DangerBed.Evaluate(_riskLevel, humRaw, _humPitch,
                                      _subBedScale, _stressBedScale, gain);

            _humValid = true;
            _humLevel = _riskLevel;
            _humGain = gain;
        }

        /// <summary>
        /// 지속 위험 레이어를 세우고 유지한다(UP-RISK-05 · Notion PRD §8.3).
        ///
        /// **사이렌과 정반대의 물건이다.** 사이렌은 네 순간에만 한 발씩 나가고
        /// (<c>AudioCueTable.TryMapSiren</c>), 이 둘은 늘 깔린 채 두께만 변한다.
        /// 그 분업이 §8.3 의 「사이렌은 지속 재생하지 않는다」를 성립시킨다 —
        /// 사이렌을 끄고 남는 것이 침묵이면 결국 사이렌을 다시 켜게 된다.
        ///
        /// 문자열·람다·할당을 만들지 않는다. `MASTER_PRD.md` §13.2 의 "워밍업 후 매 프레임 0 B".
        /// </summary>
        private void UpdateDangerBed(float dt)
        {
            if (_subBed == null || _stressBed == null) return;

            if (!_dangerBedEnabled)
            {
                _subBed.volume = 0f;
                _stressBed.volume = 0f;
                return;
            }

            int want = (int)_riskLevel;
            if (want < 0) want = 0;
            if (want >= DangerBed.LevelCount) want = DangerBed.LevelCount - 1;

            if (dt <= 0f) dt = 0f;
            float step = _bedSwapSeconds > 0.0001f ? dt / _bedSwapSeconds : 1f;

            if (want != _bedLevel)
            {
                // 도는 파형을 도중에 갈아 끼우면 그 순간이 딱 소리로 들리고, 그 딱 소리는
                // 단계 전이의 일회성 응력음(`AudioCueKind.MetalStress`)과 겹쳐
                // **무엇이 신호였는지** 알 수 없게 된다. 그래서 한 번 내렸다 올린다.
                _bedFade -= step;
                if (_bedFade <= 0f)
                {
                    _bedFade = 0f;
                    SwapBedClips(want);
                }
            }
            else if (_bedFade < 1f)
            {
                _bedFade += step;
                if (_bedFade > 1f) _bedFade = 1f;
            }

            // 저역만 접근성 배율을 통과한다. `AccessibilityProfile._lowFrequencyScale` 의
            // 툴팁이 "낮은 험은 스피커·헤드폰에 따라 두통을 만든다"고 적는데, 이 베드가
            // 30~50Hz 대라 그 문장이 가리키는 것이 정확히 여기다. 응력음(150~2600Hz)은
            // 저주파가 아니므로 통과시키지 않는다 — 통과시키면 저주파를 끈 사람에게서
            // 위험 단계의 청각 채널이 통째로 사라진다.
            _subBed.volume = _accessibility.ScaleLowFrequency(_bed.SubVolume) * _bedFade;
            _subBed.pitch = _bed.SubPitch;

            _stressBed.volume = _bed.StressVolume * _bedFade;
            _stressBed.pitch = _bed.StressPitch;
        }

        private void SwapBedClips(int level)
        {
            _bedLevel = level;

            AudioClip sub = SustainedBedFactory.Sub(level);
            AudioClip stress = SustainedBedFactory.Stress(level);

            // 이 시점의 볼륨은 0 이다(호출 직전에 페이드가 0 에 닿았다). 그래서
            // Stop→Play 가 들리지 않는다.
            if (sub != null)
            {
                _subBed.Stop();
                _subBed.clip = sub;
                _subBed.Play();
            }

            if (stress != null)
            {
                _stressBed.Stop();
                _stressBed.clip = stress;
                _stressBed.Play();
            }
        }

        private void StopDangerBed()
        {
            if (_subBed != null) { _subBed.volume = 0f; _subBed.Stop(); }
            if (_stressBed != null) { _stressBed.volume = 0f; _stressBed.Stop(); }

            // 다시 켜졌을 때 처음부터 세우게 한다. 그대로 두면 클립은 멈춰 있는데
            // 단계는 같아서 `UpdateDangerBed` 가 아무것도 하지 않는다 — 영구 무음이다.
            _bedLevel = -1;
            _bedFade = 0f;
        }

        private void ApplyChannelVolumes(float gain)
        {
            if (_sources == null) return;
            float master = _masterVolume * gain;
            _sources[(int)AudioCueChannel.Machine].volume = ChannelVolume(AudioCueChannel.Machine) * master;
            _sources[(int)AudioCueChannel.Event].volume = ChannelVolume(AudioCueChannel.Event) * master;
            _sources[(int)AudioCueChannel.Passenger].volume = ChannelVolume(AudioCueChannel.Passenger) * master;
            _sources[(int)AudioCueChannel.Warning].volume = ChannelVolume(AudioCueChannel.Warning) * master;
        }

        private void ApplyListenerDuck(float now, float gain)
        {
            if (!_duckGlobalListener) { RestoreListener(); return; }

            if (_silence.IsActive(now))
            {
                if (!_listenerDucked)
                {
                    // 남의 값을 기억해 두고 돌려준다. 이 컴포넌트가 전역 볼륨의 소유자는 아니다.
                    _listenerBase = AudioListener.volume;
                    _listenerDucked = true;
                }
                AudioListener.volume = _listenerBase * gain;
            }
            else
            {
                RestoreListener();
            }
        }

        private void RestoreListener()
        {
            if (!_listenerDucked) return;
            AudioListener.volume = _listenerBase;
            _listenerDucked = false;
        }

        // ── 사건 → 소리 ──────────────────────────────────────────────────────

        private void OnEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.OverharvestApproached:
                    // §7.3 1~4단계. 손을 올리는 순간부터 방이 조용해진다.
                    _silence.Begin(Now, _silenceSeconds);
                    return;

                case GameEventKind.OverharvestReleased:
                    _silence.Cancel(Now);
                    return;

                case GameEventKind.OverharvestPulled:
                    // §7.3 5단계 — "끝까지 당기면 경고등·진동·통관 회전이 동시에 재개된다."
                    // 정적을 먼저 걷어야 그 재개음이 실제로 들린다.
                    _silence.Cancel(Now);
                    break;

                case GameEventKind.FloorStarted:
                    // 층이 바뀌면 지난 층의 밀린 소리를 버린다. 새 층에서 들리는 옛 소리는 거짓이다.
                    _queueHead = 0;
                    _queueCount = 0;
                    _nextFree = Now;
                    break;

                case GameEventKind.RiskLevelChanged:
                    TrackRiskLevel(in e);
                    break;
            }

            // 겹쳐 우는 두 채널을 먼저 처리한다. 대표음보다 **앞에** 두는 이유는
            // 사이렌과 비명이 사건의 반응이 아니라 사건 자체와 같은 순간이기 때문이다 —
            // 붕괴 충격음이 끝난 뒤에 오는 비명은 놀란 소리가 아니라 회상이다.
            TryPlaySiren(in e);
            TryPlayEventVoice(in e);

            AudioCueRequest req;
            if (!AudioCueTable.TryMap(in e, out req))
            {
                SilentEventCount++;
                return;
            }

            Submit(in req);
        }

        /// <summary>
        /// 위험 단계를 따라간다. **방향까지 기억한다** — 사이렌은 단계가 *오를* 때만
        /// 울린다(Notion PRD §8.3). 사건의 <c>Payload</c> 에 이전 단계가 박싱돼 있지만
        /// 그것을 꺼내는 대신 순서대로 세어 둔다. 언박싱은 매핑 표를 Risk 네임스페이스에
        /// 묶고, 박싱된 값이 없는 경로(테스트·시뮬레이터)에서 조용히 틀린다.
        /// </summary>
        private void TrackRiskLevel(in GameEvent e)
        {
            int next = e.IntValue;
            if (next < 0) next = 0;
            if (next > (int)Risk.RiskLevel.Collapse) next = (int)Risk.RiskLevel.Collapse;

            _riskRising = next > (int)_riskLevel;
            _riskLevel = (Risk.RiskLevel)next;
        }

        /// <summary>
        /// 사이렌은 네 순간에만 울린다 — 단계 상승 · 과수확 해금 · 레버 결정 · 사고.
        /// 목록은 <see cref="AudioCueTable.TryMapSiren"/> 이 갖고, 여기서 더하는 것은
        /// 셋뿐이다: 단계가 올랐는가, 접근성이 허용하는가, 너무 자주 울리지 않는가.
        /// </summary>
        private void TryPlaySiren(in GameEvent e)
        {
            if (!_sirenEnabled) return;

            // 단계가 내려갈 때는 울리지 않는다. Critical → Strain 은 나아진 것이고,
            // 거기서도 경보가 울리면 사이렌은 "상태"가 아니라 "변화 알림"이 되어
            // 단계 상승의 무게가 사라진다.
            if (e.Kind == GameEventKind.RiskLevelChanged && !_riskRising) return;

            AudioCueRequest req;
            if (!AudioCueTable.TryMapSiren(in e, out req)) return;

            // 접근성 — 끄면 0이 되어 여기서 멈춘다. 경고는 시각과 험 피치로 남는다.
            float volume = _accessibility.SirenVolume(req.Volume);
            if (volume <= 0f) return;

            float now = Now;
            if (now < _sirenReadyAt) return;
            _sirenReadyAt = now + _sirenCooldownSeconds;

            var scaled = new AudioCueRequest(req.Kind, volume, req.Pitch, req.Variant);
            SirenCueCount++;
            Submit(in scaled);
        }

        /// <summary>
        /// 사건 버스만으로 승객 음성을 낸다. 승객 반응 시스템이 아직 소리를 요청하지
        /// 않을 때의 폴백이다 — <see cref="PlayPassengerVoice"/>가 한 번이라도 불리면
        /// 그쪽이 소유자가 되고 이 경로는 영구히 물러난다.
        ///
        /// 쿨다운을 두는 이유는 PRD §9.4 와 같다: 한 사건에 모두가 동시에 말하지
        /// 않는다. 여기서는 승객이 누구인지 모르므로 "한 번에 한 목소리"로 대신한다.
        /// </summary>
        private void TryPlayEventVoice(in GameEvent e)
        {
            if (!_voiceFromEvents || _externalVoiceDriver) return;

            AudioCueRequest req;
            if (!AudioCueTable.TryMapPassengerVoice(in e, out req)) return;

            float now = Now;
            if (now < _voiceReadyAt) return;
            _voiceReadyAt = now + _voiceCooldownSeconds;

            EventVoiceCount++;
            Submit(in req);
        }

        /// <summary>
        /// **승객 반응 시스템(UP-NPC-*)이 부르는 주 통로다.** 사건 표를 거치지 않는 이유는
        /// "어느 승객이 왜 소리를 내는가"를 이 컴포넌트가 알지 못하기 때문이다 —
        /// 그것은 중재기(`PassengerReactionDirector`)만 안다.
        ///
        /// 한 번이라도 불리면 사건 버스 폴백(<see cref="TryPlayEventVoice"/>)이 꺼진다.
        /// 둘 다 살아 있으면 같은 반응에 목소리가 두 번 나는데, 그건 승객이 늘어난 것처럼
        /// 들려서 동시 반응 상한(UP-NPC-05)이 지켜지고 있는지 귀로 확인할 수 없게 된다.
        /// **소리가 나지 않는 반응(강도 0)이어도 소유권은 넘어온다** — 그러지 않으면
        /// 과수확 접근(§7.3 정적)에서만 폴백이 되살아나 그 침묵에 숨소리가 하나 낀다.
        ///
        /// <paramref name="voiceCue"/>가 우선한다. `PassengerReactionSet` 의 큐 ID 를
        /// 편집자가 바꿨을 때 소리도 바뀌어야 §9.4 의 「데이터로 이벤트별 교체 가능」이
        /// 소리 채널에서도 성립한다. 비었거나 모르는 ID 면 반응 사건으로 정한다.
        /// </summary>
        /// <param name="passengerIndex">누가 냈는가. 재생 피치를 가른다(0~7 로 조인다).</param>
        /// <param name="reaction">무슨 반응인가. 큐 ID 가 없을 때 음성 종류를 정한다.</param>
        /// <param name="voiceCue">`PassengerReaction.VoiceCue`. 비어도 된다.</param>
        /// <param name="intensity">반응 강도 0~1. 0 이하면 소리를 내지 않는다.</param>
        /// <returns>실제로 소리를 냈는가. false 는 고장이 아니라 「이 반응은 조용하다」다.</returns>
        public bool PlayPassengerVoice(int passengerIndex, Npc.PassengerReactionEvent reaction,
                                       string voiceCue, float intensity)
        {
            _externalVoiceDriver = true;
            if (intensity <= 0f) return false;

            PassengerVoiceKind voice;
            if (!PassengerVoices.TryFromCueId(voiceCue, out voice))
                voice = PassengerVoices.FromReaction(reaction);

            PlayVoice(passengerIndex, voice, intensity);
            return true;
        }

        /// <summary>
        /// 종류를 이미 정한 쪽이 부르는 통로. 반응 사건과 무관한 소리(대기 중 숨소리 등)를
        /// 낼 때 쓴다.
        /// </summary>
        public bool PlayPassengerVoice(int passengerIndex, PassengerVoiceKind voice, float intensity)
        {
            _externalVoiceDriver = true;
            if (intensity <= 0f) return false;

            PlayVoice(passengerIndex, voice, intensity);
            return true;
        }

        /// <summary>
        /// 종류를 모를 때. 기본은 호흡이다 — 옛 호출이 내던 소리와 같다.
        /// </summary>
        public void PlayPassengerVoice(int passengerIndex, float intensity)
        {
            PlayPassengerVoice(passengerIndex, PassengerVoiceKind.Breath, intensity);
        }

        private void PlayVoice(int passengerIndex, PassengerVoiceKind voice, float intensity)
        {
            AudioCueRequest req = AudioCueTable.PassengerVoice(passengerIndex, voice, intensity);
            ExternalVoiceCount++;
            Submit(in req);
        }

        private void Submit(in AudioCueRequest req)
        {
            float now = Now;

            if (IsImmediate(req.Kind))
            {
                if (_nextFree < now) _nextFree = now;
                Play(in req, _silence.GainAt(now));
                return;
            }

            float at = _nextFree < now ? now : _nextFree;
            if (at - now > _maxLeadSeconds)
            {
                // 화면은 이미 다음 단계로 갔는데 소리만 밀려 있다. 이 시점의 소리는
                // 무슨 일이 일어났는지 알려 주는 대신 방해만 한다.
                // 문자열은 경고를 실제로 낼 때만 만든다 — 버리는 큐마다 만들면 버리는 쪽이 더 비싸진다.
                DroppedCueCount++;
                if (!_overflowWarned) WarnOverflow("판정 사건이 화면보다 " + _maxLeadSeconds + "초 넘게 밀렸다");
                return;
            }

            if (_queueCount >= QueueCapacity)
            {
                DroppedCueCount++;
                if (!_overflowWarned) WarnOverflow("오디오 대기열이 " + QueueCapacity + "개를 넘겼다");
                return;
            }

            int tail = (_queueHead + _queueCount) % QueueCapacity;
            _queue[tail].Kind = req.Kind;
            _queue[tail].Volume = req.Volume;
            _queue[tail].Pitch = req.Pitch;
            _queue[tail].Variant = req.Variant;
            _queue[tail].At = at;
            _queueCount++;

            _nextFree = at + SpacingFor(req.Kind);
        }

        private void DrainQueue(float now, float gain)
        {
            while (_queueCount > 0)
            {
                int head = _queueHead;
                if (_queue[head].At > now) break;

                var req = new AudioCueRequest(_queue[head].Kind, _queue[head].Volume,
                                              _queue[head].Pitch, _queue[head].Variant);
                _queueHead = (head + 1) % QueueCapacity;
                _queueCount--;
                Play(in req, gain);
            }
        }

        private void Play(in AudioCueRequest req, float gain)
        {
            if (_sources == null) return;

            AudioClip clip = ProceduralClipFactory.Bake(req.Kind, req.Variant);
            if (clip == null)
            {
                WarnBake(req.Kind);
                return;
            }

            AudioCueChannel channel = ChannelFor(req.Kind);
            AudioSource source = _sources[(int)channel];
            source.pitch = req.Pitch;

            // PlayOneShot 은 발동 시점의 source.volume 을 고정한다. Update 를 기다리면
            // 같은 프레임에 시작된 정적이 이 한 발에만 적용되지 않는다.
            source.volume = ChannelVolume(channel) * _masterVolume * gain;
            source.PlayOneShot(clip, req.Volume);

            PlayedCueCount++;

            // **총합이 아니라 종류를 센다.** `UP-AUD-02` 는 「룰렛 사운드 10종」을 요구하는데
            // 「재생 3건」으로는 한 종류가 세 번 난 것과 세 종류가 한 번씩 난 것이 구분되지 않는다.
            // 종류가 32 미만이라 int 하나로 충분하다.
            int bit = (int)req.Kind;
            if (bit > 0 && bit < 32) PlayedKindsMask |= 1 << bit;

            // 승객 음성은 한 큐 종류 안에 다섯 표현이 들어 있다(`MASTER_PRD.md` §9.3).
            // `PlayedKindsMask` 로는 그 다섯이 각각 났는지 셀 수 없다 — 비트가 하나뿐이다.
            if (req.Kind == AudioCueKind.PassengerVoice)
                VoiceKindsMask |= 1 << (int)PassengerVoices.KindOf(req.Variant);

            if (_logCues) Debug.Log("[상승] 오디오 큐 " + req + " ch=" + channel);
        }

        // ── 표 ───────────────────────────────────────────────────────────────

        /// <summary>줄을 서지 않는 큐 — 플레이어의 직접 동작과 층의 결말.</summary>
        private static bool IsImmediate(AudioCueKind kind)
        {
            switch (kind)
            {
                case AudioCueKind.LeverPull:
                case AudioCueKind.PowerBanked:
                case AudioCueKind.OverharvestUnlock:
                case AudioCueKind.OverharvestPull:
                case AudioCueKind.CollapseImpact:
                case AudioCueKind.MetalStress:
                case AudioCueKind.PassengerVoice:
                // 사이렌이 사건보다 늦게 오면 그건 경보가 아니라 사후 보고다.
                case AudioCueKind.Siren:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 큐 하나가 끝난 뒤 다음 큐까지의 최소 간격.
        /// 열 공개는 <c>SpinPresenter._columnRevealInterval</c> 기본값 0.32초에 맞춰 둔다 —
        /// 소리와 화면이 다른 박자로 가면 둘 다 못 읽는다.
        /// </summary>
        private static float SpacingFor(AudioCueKind kind)
        {
            switch (kind)
            {
                case AudioCueKind.ColumnReveal:     return 0.30f;
                case AudioCueKind.SoulHarvest:      return 0.18f;
                case AudioCueKind.PurifyScattered:  return 0.26f;
                case AudioCueKind.PurifyLine:       return 0.30f;
                case AudioCueKind.PurifyCluster:    return 0.34f;
                case AudioCueKind.CascadeStep:      return 0.16f;
                case AudioCueKind.ThresholdCrossed: return 0.30f;
                case AudioCueKind.ResidualDamage:   return 0.30f;
                default:                            return 0.10f;
            }
        }

        private static AudioCueChannel ChannelFor(AudioCueKind kind)
        {
            switch (kind)
            {
                case AudioCueKind.LeverPull:
                case AudioCueKind.PowerBanked:
                case AudioCueKind.OverharvestUnlock:
                case AudioCueKind.OverharvestPull:
                case AudioCueKind.CollapseImpact:
                    return AudioCueChannel.Machine;

                case AudioCueKind.PassengerVoice:
                    return AudioCueChannel.Passenger;

                case AudioCueKind.ResidualDamage:
                case AudioCueKind.MetalStress:
                // 사이렌이 경고 채널인 것은 취향이 아니다 — `AudioChannel.Warning` 의
                // 주석이 "경고음·사이렌. 접근성 옵션이 따로 끌 수 있어야 한다"고 정의한다.
                case AudioCueKind.Siren:
                    return AudioCueChannel.Warning;

                default:
                    return AudioCueChannel.Event;
            }
        }

        private float ChannelVolume(AudioCueChannel channel)
        {
            if (_mixProfile != null) return _mix.VolumeFor(ToMixChannel(channel)) * MixMaster;

            switch (channel)
            {
                case AudioCueChannel.Machine:   return _machineVolume;
                case AudioCueChannel.Passenger: return _passengerVolume;
                case AudioCueChannel.Warning:   return _warningVolume;
                default:                        return _eventVolume;
            }
        }

        /// <summary>
        /// 두 채널 열거를 잇는다. **캐스트로 넘기면 안 된다** — 이름이 같아서 컴파일되지만
        /// 값이 한 칸씩 밀려 있다.
        ///
        /// <code>
        ///   AudioCueChannel   Machine=0 Event=1 Passenger=2 Warning=3
        ///   AudioChannel      Machine=1 Event=2 Passenger=3 Warning=4
        /// </code>
        ///
        /// `(AudioChannel)cueChannel` 은 전 채널이 옆 채널의 볼륨을 받게 만든다.
        /// 컴파일도 되고 소리도 나므로 **아무도 눈치채지 못한다.**
        /// </summary>
        private static Data.Profiles.AudioChannel ToMixChannel(AudioCueChannel channel)
        {
            switch (channel)
            {
                case AudioCueChannel.Machine:   return Data.Profiles.AudioChannel.Machine;
                case AudioCueChannel.Event:     return Data.Profiles.AudioChannel.Event;
                case AudioCueChannel.Passenger: return Data.Profiles.AudioChannel.Passenger;
                case AudioCueChannel.Warning:   return Data.Profiles.AudioChannel.Warning;
                default:                        return Data.Profiles.AudioChannel.Master;
            }
        }

        /// <summary>
        /// 에셋의 마스터를 인스펙터 마스터에 곱한다. 에셋이 채널 균형을 정하고
        /// 인스펙터 슬라이더는 그 위의 전체 크기로 남는다 — 둘 중 하나를 지우면
        /// 씬에서 소리를 끄는 손잡이가 사라지거나 데이터가 무의미해진다.
        /// </summary>
        private float MixMaster => _mix.MasterVolume;

        // ── 부품 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 채널 넷을 자식 오브젝트로 만든다. 씬에 미리 배치하지 않는 이유는
        /// 씬 파일이 단일 소유 자원이라(`CLAUDE.md` 에이전트 소유권 규칙) 오디오 하나 붙이려고
        /// 씬 편집을 직렬화시킬 이유가 없기 때문이다.
        /// </summary>
        private void BuildSources()
        {
            _sources = new AudioSource[4];
            for (int i = 0; i < _sources.Length; i++)
            {
                var go = new GameObject("AudioChannel_" + (AudioCueChannel)i);
                go.transform.SetParent(transform, false);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                // 2D 로 둔다. 이 큐들은 "방 안에서 일어난 일"이지 특정 좌표에서 나는
                // 소리가 아니다. 3D 로 두면 플레이어가 돌아설 때마다 판정음이 사라진다.
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.volume = 0f;
                _sources[i] = source;
            }

            // 지속 레이어는 큐 채널과 **섞지 않는다.** 큐 채널은 `PlayOneShot` 을 받고
            // 매 프레임 볼륨이 덮어써지는 자리라, 루프 클립을 같은 소스에 얹으면
            // 캐스케이드 한 번에 지속층의 볼륨과 피치가 통째로 바뀐다.
            _subBed = BuildLoopSource("AudioBed_Sub");
            _stressBed = BuildLoopSource("AudioBed_Stress");
        }

        private AudioSource BuildLoopSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }

        private void WarnBake(AudioCueKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= _bakeWarned.Length) return;
            if (_bakeWarned[index]) return;
            _bakeWarned[index] = true;
            Debug.LogWarning("[상승] 오디오 큐 " + kind + " 를 굽지 못했다 — 이 사건은 소리가 나지 않는다");
        }

        private void WarnOverflow(string reason)
        {
            if (_overflowWarned) return;
            _overflowWarned = true;
            Debug.LogWarning("[상승] 오디오 큐를 버렸다 — " + reason
                             + ". 간격(SpacingFor)이 연출 템포보다 느리다는 뜻이다.");
        }

        private struct Scheduled
        {
            public AudioCueKind Kind;
            public float Volume;
            public float Pitch;
            public int Variant;
            public float At;
        }
    }
}

// 씬 배선 필요:
//  1. `Prototype_Elevator.unity` 의 런 루트(RunSessionBehaviour 가 붙은 오브젝트가 좋다)에
//     `AudioDirector` 를 하나 추가하고 `_run` 에 그 `RunSessionBehaviour` 를 꽂는다.
//     AudioSource 네 개는 Awake 에서 자식으로 자동 생성되므로 씬에 만들 것이 없다.
//  2. `OverharvestApproached` / `OverharvestReleased` 를 아무도 발행하지 않는다.
//     `InteractableOverharvestLever` 를 보는 `RouletteInteractionBridge` 가 조준 상태 변화에서
//     그 둘을 `Session.Current.Events` 로 발행해야 UP-AUD-03 의 정적이 실제로 걸린다.
//     (그 파일들은 이 작업의 소유 범위 밖이라 손대지 않았다.)
//  3. `RiskLevelChanged` 도 아무도 발행하지 않는다. `RiskStateView` 가 단계 전이를 이미
//     알고 있으므로 거기서 발행하는 것이 자연스럽다.
//  4. 위험 단계 험(`RiskStateView._hum`)은 정적 구간에 함께 줄어들지 않는다.
//     RiskStateView 가 매 LateUpdate 마다 `_hum.volume` 을 절대값으로 덮어쓰기 때문이다
//     (그 파일의 CabinLightMultiplier 주석이 같은 함정을 이미 적어 두었다).
//     지금은 `_duckGlobalListener` 로 AudioListener 전체를 줄여 우회한다.
//
//     **여기서 절반은 끝냈다.** `AudioDirector.HumVolume` / `HumPitch` 가 이미
//     `AudioMixProfile` 의 단계별 배율 × `DangerFeedbackProfile` 의 형태 × 정적 게인을
//     곱한 최종값을 매 프레임 들고 있다(`RefreshHumTargets`). 남은 한 줄은 RiskStateView
//     쪽이다 — `ApplyHum` 에서 `_hum.volume = _blended.HumVolume` 대신 배선된
//     AudioDirector 의 `HumVolume` 을 쓰면 된다. 그 파일은 이 작업의 소유 범위 밖이라
//     손대지 않았다. 그때까지 이 값들은 계산되고 노출되지만 험에 닿지는 않는다.
//
//  5. 사이렌(UP-RISK-05)은 `_accessibilityProfile` 이 비어 있으면 "전부 허용"으로 돈다.
//     `AccessibilityProfile.asset` 을 만들어 꽂으면 `AllowSiren` 이 실제로 사이렌만
//     끈다 — 험·응력음·충격음은 남는다. `AccessibilitySource` 가 어느 쪽인지 알려 준다.
//
//  6. 승객 음성(UP-AUD-04)은 지금 **사건 버스 폴백**으로 난다. 승객 반응 중재기가
//     누구를 고를지 이 컴포넌트는 모르기 때문이다. 그 파일들은 이 작업의 소유 범위 밖이라
//     손대지 않았고, 대신 **부를 수 있는 진입점**을 여기 만들어 두었다.
//
//     `Scripts/Npc/PassengerReactionView.OnReacted` 에서 한 줄이면 된다:
//
//         _audio.PlayPassengerVoice(passengers[i], reactionEvent,
//                                   reaction.VoiceCue, reaction.Intensity);
//
//     시그니처:
//         bool PlayPassengerVoice(int passengerIndex,
//                                 Ascend.Prototype.Npc.PassengerReactionEvent reaction,
//                                 string voiceCue, float intensity)
//         bool PlayPassengerVoice(int passengerIndex, PassengerVoiceKind voice, float intensity)
//         void PlayPassengerVoice(int passengerIndex, float intensity)   // 호흡 고정
//
//     첫 호출에 폴백이 물러나고(`HasExternalVoiceDriver`) 목소리와 몸짓이 같은 승객에게
//     붙는다. 반환값 false 는 고장이 아니라 「이 반응은 조용하다」다 —
//     과수확 접근이 그 경우이며 §7.3 이 그 순간을 정적으로 규정한다.
//     쿨다운을 여기서 걸지 않는 이유: 누가 언제 반응하는지는 중재기의 규칙이고
//     (UP-NPC-05), 두 곳에서 조이면 상한이 실제로 몇인지 아무도 모르게 된다.
//
//     `AudioDirector.VoiceKindCount` 가 다섯 표현이 실제로 났는지 센다. 5 가 목표다.
//
//  7. 지속 위험 레이어(UP-RISK-05)는 **씬에서 할 일이 없다.** AudioSource 두 개를
//     `AudioBed_Sub` / `AudioBed_Stress` 로 Awake 에서 자식 생성한다.
//     확인은 `SubBedVolume` / `StressBedVolume` / `DangerBedLevel` 로 한다 —
//     안정 단계에서 `StressBedVolume` 이 0 이 아니면 배분이 틀린 것이다.
//     `RiskStateView` 의 험과 겹쳐 들리면 `_dangerBedEnabled` 를 끄고 비교한다.
