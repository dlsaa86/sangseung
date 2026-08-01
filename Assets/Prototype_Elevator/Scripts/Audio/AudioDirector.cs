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
        private string _mixSource = "(미초기화)";
        private string _overharvestSource = "(미초기화)";
        private bool _overflowWarned;

        private GameEventBus _bus;
        private System.Action<GameEvent> _handler;

        private bool _listenerDucked;
        private float _listenerBase = 1f;

        /// <summary>지금까지 실제로 재생한 큐 수. 무영상 검증이 "정말 울렸는가"를 물을 때 쓴다.</summary>
        /// <summary>지금 쓰고 있는 믹스의 출처. 검증 하네스가 「에셋이 실제로 읽혔는가」를 묻는다.</summary>
        public string MixSource => _mixSource;

        /// <summary>정적 구간 값의 출처. 같은 이유로 노출한다.</summary>
        public string OverharvestSource => _overharvestSource;

        /// <summary>실제로 쓰이는 정적 구간 길이(초). PRD §7 의 0.3~0.7 범위를 지켜야 한다.</summary>
        public float SilenceSeconds => _silenceSeconds;

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

            _silence.DuckSeconds = _duckSeconds;
            _silence.ResumeSeconds = _resumeSeconds;

            BuildSources();

            // 첫 사용 순간에 굽지 않는다. 하필 그 순간이 레버를 당긴 프레임이라
            // 성능 캡처에 스파이크로 찍힌다.
            if (_prewarmClips) ProceduralClipFactory.Prewarm();

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
            DrainQueue(now, gain);
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
            }

            AudioCueRequest req;
            if (!AudioCueTable.TryMap(in e, out req))
            {
                SilentEventCount++;
                return;
            }

            Submit(in req);
        }

        /// <summary>
        /// 승객 반응 시스템(UP-NPC-*)이 부르는 통로. 사건 표를 거치지 않는 이유는
        /// "어느 승객이 왜 소리를 내는가"를 이 컴포넌트가 알지 못하기 때문이다.
        /// </summary>
        public void PlayPassengerVoice(int passengerIndex, float intensity)
        {
            AudioCueRequest req = AudioCueTable.PassengerVoice(passengerIndex, intensity);
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
//     지금은 `_duckGlobalListener` 로 AudioListener 전체를 줄여 우회한다. 제대로 하려면
//     RiskStateView 에 `HumVolumeMultiplier` 를 두고 AudioDirector.SilenceGain 을 곱해야 한다.
