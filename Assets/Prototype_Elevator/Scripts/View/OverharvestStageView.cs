using System.Text;
using UnityEngine;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Events;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 과수확 5단계를 화면에 낸다. 핵심은 **5단계** 다 —
    /// `MASTER_PRD.md` §7.3: "레버를 끝까지 당기면 경고등·진동·통관 회전이 **동시에** 재개된다."
    ///
    /// 왜 <see cref="OverharvestUnlockEffect"/> 를 고쳐 쓰지 않는가: 그쪽은 **해금**
    /// (요구 전력 100% 달성) 연출이다. 해금과 당김은 서로 다른 순간이고, 한 층에서
    /// 해금은 한 번인데 당김은 여러 번이다. 한 코루틴에 둘을 얹으면 두 번째 당김이
    /// 해금 연출을 되감는다 — 지금 CONNECTED 인 연출을 깨뜨리는 가장 빠른 길이다.
    /// 대신 경고등 채널은 그쪽에서 **빌린다**(<see cref="OverharvestUnlockEffect.SetWarningScale"/>) —
    /// 같은 렌더러에 두 컴포넌트가 `MaterialPropertyBlock` 을 쓰면 실행 순서가 이긴다.
    ///
    /// **「동시에」를 어떻게 지키는가**: 세 채널이 각각 타이머를 갖지 않는다.
    /// <see cref="OverharvestStageTimeline"/> 하나가 당김 시각을 세 칸짜리 배열에 한 번에 적고,
    /// 이 컴포넌트는 매 프레임 그 계산기에게 세 값을 물어 그대로 바른다. 그래서
    /// "셋이 같은 순간에 살아났는가"가 취향이 아니라 <see cref="ResumeIsSimultaneous"/> 로
    /// 대답되는 질문이 된다.
    ///
    /// **통관 상시 회전 기본값은 0 이다.** 이 저장소에는 통관을 돌리는 코드가 한 줄도 없었고
    /// (전수 grep 결과), 여기서 상시 회전을 켜면 모든 고정 캡처의 각도가 시간에 따라 달라져
    /// 베이스라인이 통째로 흔들린다. 그래서 상시 회전은 씬 오너가 켜는 값으로 두고,
    /// 재개는 **정수 바퀴 버스트**로 낸다 — 끝나면 각도가 시작 각도로 정확히 돌아오므로
    /// 「과수확을 당긴 런」과 「안 당긴 런」의 정지 화면이 달라지지 않는다.
    ///
    /// 강도·회전수·진폭은 전부 인스펙터 값이다. 최종 연출은 승인 대기 항목이라 코드에 잠그지 않는다.
    /// </summary>
    public sealed class OverharvestStageView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;

        [Header("수치 (PRD §7.4 / UP-POWER-07)")]
        [Tooltip("비어 있으면 코드 기본값으로 진행하고 그 사실이 OverharvestSource 에 남는다.")]
        [SerializeField] private OverharvestProfile _overharvestProfile;

        [Tooltip("기계가 1 에서 감쇠 배율까지 내려가는 시간. 프로파일에는 없는 값이라 " +
                 "AudioDirector 의 같은 이름 필드와 맞춘다.")]
        [SerializeField, Min(0.01f)] private float _duckSeconds = OverharvestStageTimeline.DefaultDuckSeconds;

        [Tooltip("재개 순간의 과증폭. 0 이면 「돌아왔다」만 있고 「튀었다」가 없어 사건으로 안 읽힌다.")]
        [SerializeField, Min(0f)] private float _burstOvershoot = OverharvestStageTimeline.DefaultBurstOvershoot;

        [Header("① 경고등 — 해금 연출에서 빌린다")]
        [Tooltip("경고 띠와 전용 스팟의 소유자. 비어 있으면 씬에서 찾는다.")]
        [SerializeField] private OverharvestUnlockEffect _warningChannel;

        [Header("② 진동 — 환경 오브젝트를 흔든다 (VISUAL_SPEC §8.3)")]
        [Tooltip("카메라가 아니라 물체다. 전체 화면 흔들림은 §6 이 금지한다. " +
                 "RiskStateView 의 sway/camera 대상과 겹치지 않는 오브젝트를 넣는다.")]
        [SerializeField] private Transform[] _shakeTargets = new Transform[0];

        [SerializeField, Min(0f)] private float _shakeAmplitude = 0.018f;
        [SerializeField, Min(0f)] private float _shakeFrequency = 34f;

        [Header("③ 통관 회전")]
        [Tooltip("돌릴 통관(또는 그 회전용 자식). 비어 있으면 이 채널은 화면에 없다.")]
        [SerializeField] private Transform[] _tubeSpinners = new Transform[0];

        [SerializeField] private Vector3 _tubeSpinAxis = Vector3.up;

        [Tooltip("평소 회전 속도(도/초). **기본 0** — 상시 회전을 켜면 고정 캡처의 각도가 " +
                 "시간에 따라 달라져 베이스라인이 흔들린다. 켜는 것은 씬 오너의 결정이다.")]
        [SerializeField, Min(0f)] private float _idleSpinDegreesPerSecond;

        [Tooltip("재개 순간에 도는 바퀴 수. 정수라야 버스트가 끝난 각도가 시작 각도와 같다.")]
        [SerializeField, Min(0)] private int _resumeBurstTurns = 1;

        private readonly OverharvestStageTimeline _timeline = new OverharvestStageTimeline();

        private GameEventBus _bus;
        private System.Action<GameEvent> _handler;

        private Vector3[] _shakeHomes = new Vector3[0];
        private Quaternion[] _spinHomes = new Quaternion[0];

        private float _idlePhase;
        private float _tubeDegrees;
        private bool _shakeApplied;

        /// <summary>
        /// 오디오와 같은 시계를 쓴다. 타임스케일을 따라가면 정지·슬로우 중에 §7.3 의
        /// 0.3~0.7초 약속이 같이 늘어나고, 그러면 소리와 그림이 서로 다른 순간에 재개된다.
        /// </summary>
        private static float Now => Time.unscaledTime;

        // ── 관측 가능성 ──────────────────────────────────────────────────────
        // 단계별이다. 총합만 있으면 「5단계가 다 돌았다」를 반증할 수 없다.

        /// <summary>1단계 — 레버 조준이 dwell 을 넘긴 횟수.</summary>
        public int ApproachCount => _timeline.ApproachCount;

        /// <summary>2단계 — 기계 감쇠가 시작된 횟수.</summary>
        public int MachineDuckCount => _timeline.MachineDuckCount;

        /// <summary>3단계 — 승객 응시 시점이 지난 횟수.</summary>
        public int PassengerGazeCount => _timeline.PassengerGazeCount;

        /// <summary>4단계 — 정적 구간이 열린 횟수.</summary>
        public int SilenceCount => _timeline.SilenceCount;

        /// <summary>5단계 — 당김 뒤 세 채널이 재개한 횟수.</summary>
        public int ResumeCount => _timeline.ResumeCount;

        /// <summary>조준을 거둔 횟수. 5단계로 세지 않는다.</summary>
        public int ReleaseCount => _timeline.ReleaseCount;

        /// <summary>다섯 단계가 전부 한 번 이상 났는가.</summary>
        public bool AllStagesFired => _timeline.AllStagesFired;

        /// <summary>수치가 어디서 왔는가. 폴백이면 「코드 프리셋」이다.</summary>
        public string OverharvestSource => _timeline.ProfileSource;

        /// <summary>계산기 본체. 하네스가 눈금을 직접 잴 수 있어야 한다.</summary>
        public OverharvestStageTimeline Timeline => _timeline;

        /// <summary>기계가 지금 얼마나 돌고 있는가(0~1).</summary>
        public float MachineLevel => _timeline.ChannelLevel(OverharvestChannel.TubeSpin, Now);

        public float WarningScale => _timeline.ChannelScale(OverharvestChannel.Warning, Now);
        public float ShakeAmount => _timeline.ChannelScale(OverharvestChannel.Shake, Now);
        public float TubeSpinScale => _timeline.ChannelScale(OverharvestChannel.TubeSpin, Now);

        /// <summary>지금까지 통관이 돈 각도. 재개 버스트가 끝나면 시작 각도로 돌아온다.</summary>
        public float TubeSpinDegrees => _tubeDegrees;

        public bool IsResuming => _timeline.IsResuming;

        /// <summary>세 채널이 같은 순간에 재개했는가. <see cref="ResumeCount"/> 와 함께 읽는다.</summary>
        public bool ResumeIsSimultaneous => _timeline.ResumeIsSimultaneous;

        /// <summary>경고등 채널이 실제로 화면에 있는가.</summary>
        public bool WarningChannelBound => _warningChannel != null && _warningChannel.HasWarningChannel;

        /// <summary>진동 대상 수. 0 이면 이 채널은 화면에 없다.</summary>
        public int ShakeTargetCount => CountNonNull(_shakeTargets);

        /// <summary>통관 회전 대상 수. 0 이면 이 채널은 화면에 없다.</summary>
        public int TubeSpinnerCount => CountNonNull(_tubeSpinners);

        /// <summary>세 채널이 전부 배선됐는가. 하나라도 비면 §7.3 5단계는 부분적으로만 보인다.</summary>
        public bool AllChannelsBound =>
            WarningChannelBound && ShakeTargetCount > 0 && TubeSpinnerCount > 0;

        /// <summary>
        /// 비어 있는 채널의 이름. 「연출을 만들었다」와 「화면에 보인다」를 가르는 한 줄이다 —
        /// 배선이 비면 카운터는 올라가는데 아무것도 안 보인다.
        /// </summary>
        public string MissingChannels
        {
            get
            {
                var sb = new StringBuilder(48);
                if (!WarningChannelBound) sb.Append("경고등 ");
                if (ShakeTargetCount == 0) sb.Append("진동 ");
                if (TubeSpinnerCount == 0) sb.Append("통관회전 ");
                return sb.Length == 0 ? "없음" : sb.ToString().TrimEnd();
            }
        }

        /// <summary>한 줄 보고. 하네스 로그에 그대로 넣는다.</summary>
        public string Describe()
        {
            return _timeline.Describe()
                 + $" | 채널(경고등={WarningChannelBound} 진동={ShakeTargetCount} 통관={TubeSpinnerCount})"
                 + $" 미배선={MissingChannels}";
        }

        // ── 수명 ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _handler = OnEvent;

            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_warningChannel == null) _warningChannel = FindAnyObjectByType<OverharvestUnlockEffect>();

            ApplyProfile();
            CacheHomes();

            if (_run == null) return;
            _run.RunStarted += OnRunStarted;

            // RunSessionBehaviour.Awake 가 이미 ResetRun 을 끝냈을 수 있다.
            // 실행 순서에 기대면 어떤 날은 붙고 어떤 날은 안 붙는다.
            if (_run.Session != null) Attach(_run.Session.Events);
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Detach();
        }

        /// <summary>
        /// 꺼질 때 빌린 것을 전부 돌려준다. 재개 버스트 도중에 비활성화되면
        /// 경고 배수가 1.6 인 채, 진동이 오프셋인 채로 굳는다 — 원인을 찾기 가장 어려운 종류다.
        /// </summary>
        private void OnDisable()
        {
            if (_warningChannel != null) _warningChannel.SetWarningScale(1f);
            RestoreShake();
            RestoreSpin();
            _shakeApplied = false;
        }

        private void OnRunStarted(RunSession session)
        {
            Attach(session != null ? session.Events : null);
        }

        /// <summary>사건 버스에 붙는다. 런이 다시 시작되면 버스도 새로 만들어진다.</summary>
        public void Attach(GameEventBus bus)
        {
            if (ReferenceEquals(_bus, bus)) return;
            Detach();

            _bus = bus;
            _timeline.ResetRun();
            _idlePhase = 0f;

            if (_bus != null) _bus.Published += _handler;
        }

        private void Detach()
        {
            if (_bus != null) _bus.Published -= _handler;
            _bus = null;
        }

        /// <summary>테스트·에디터 배선용. 프로파일을 갈아 끼우고 출처를 다시 찍는다.</summary>
        public void SetOverharvestProfile(OverharvestProfile profile)
        {
            _overharvestProfile = profile;
            ApplyProfile();
        }

        private void ApplyProfile()
        {
            _timeline.Configure(_overharvestProfile, nameof(OverharvestStageView),
                                _duckSeconds, _burstOvershoot);
        }

        // ── 사건 ─────────────────────────────────────────────────────────────

        private void OnEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.OverharvestApproached:
                    _timeline.Approach(Now, SeedFor(in e));
                    break;

                case GameEventKind.OverharvestReleased:
                    _timeline.Release(Now);
                    break;

                case GameEventKind.OverharvestPulled:
                    // §7.3 5단계. 폴링하지 않는다 — 레버는 자기가 당겨진 것을 알지만
                    // 게임 상태를 모르고, 판돈이 실제로 빠져나간 순간은 `FloorSession` 만 안다.
                    _timeline.Pull(Now);
                    break;
            }
        }

        /// <summary>
        /// 정적 길이를 뽑을 시드. 런 시드·층·추가 스핀 횟수에서 파생하므로
        /// 같은 런의 같은 지점은 같은 길이를 낸다(`TECH_SPEC.md` §14).
        /// </summary>
        private int SeedFor(in GameEvent e)
        {
            int runSeed = _run != null ? _run.Seed : 0;
            unchecked { return runSeed * 397 ^ (e.Floor * 31 + e.IntValue); }
        }

        // ── 적용 ─────────────────────────────────────────────────────────────

        private void Update()
        {
            float now = Now;
            _timeline.Tick(now);

            // 경고등 — 소유자에게 배수만 넘긴다.
            if (_warningChannel != null)
                _warningChannel.SetWarningScale(_timeline.ChannelScale(OverharvestChannel.Warning, now));

            ApplyShake(_timeline.ChannelScale(OverharvestChannel.Shake, now), now);

            // 통관 — 상시 회전은 기계 수준을 따라가고(감쇠하면 느려진다), 재개는 정수 바퀴 버스트다.
            _idlePhase += Time.unscaledDeltaTime * _timeline.ChannelLevel(OverharvestChannel.TubeSpin, now);
            _tubeDegrees = _idleSpinDegreesPerSecond * _idlePhase
                         + 360f * _resumeBurstTurns * _timeline.BurstTurns01(now);
            ApplySpin(_tubeDegrees);
        }

        private void CacheHomes()
        {
            _shakeHomes = new Vector3[_shakeTargets.Length];
            for (int i = 0; i < _shakeTargets.Length; i++)
                _shakeHomes[i] = _shakeTargets[i] != null ? _shakeTargets[i].localPosition : Vector3.zero;

            _spinHomes = new Quaternion[_tubeSpinners.Length];
            for (int i = 0; i < _tubeSpinners.Length; i++)
                _spinHomes[i] = _tubeSpinners[i] != null ? _tubeSpinners[i].localRotation : Quaternion.identity;
        }

        /// <summary>
        /// 무작위가 아니라 결정된 파형이다. 위상을 **재개 시작 시각 기준**으로 잡는 이유는
        /// 절대 시각을 쓰면 같은 시드의 두 런이 다른 위상에서 캡처되기 때문이다.
        /// </summary>
        private void ApplyShake(float amount, float now)
        {
            if (amount <= 0.001f)
            {
                // 매 프레임 홈으로 되쓰지 않는다. 평소가 압도적으로 길고, 그동안 남의
                // localPosition 을 계속 덮어쓰면 다른 연출이 같은 오브젝트를 못 쓴다.
                if (_shakeApplied) { RestoreShake(); _shakeApplied = false; }
                return;
            }

            _shakeApplied = true;
            float phase = (now - _timeline.ResumeStartedAt(OverharvestChannel.Shake)) * _shakeFrequency;

            for (int i = 0; i < _shakeTargets.Length && i < _shakeHomes.Length; i++)
            {
                Transform t = _shakeTargets[i];
                if (t == null) continue;

                float offset = i * 1.7f;
                t.localPosition = _shakeHomes[i] + new Vector3(
                    Mathf.Sin(phase + offset) * _shakeAmplitude * amount,
                    Mathf.Sin(phase * 1.7f + offset) * _shakeAmplitude * 0.6f * amount,
                    Mathf.Sin(phase * 0.9f + offset) * _shakeAmplitude * 0.4f * amount);
            }
        }

        private void RestoreShake()
        {
            for (int i = 0; i < _shakeTargets.Length && i < _shakeHomes.Length; i++)
                if (_shakeTargets[i] != null) _shakeTargets[i].localPosition = _shakeHomes[i];
        }

        private void ApplySpin(float degrees)
        {
            if (_tubeSpinners.Length == 0) return;

            Vector3 axis = _tubeSpinAxis.sqrMagnitude > 0.0001f ? _tubeSpinAxis : Vector3.up;
            Quaternion delta = Quaternion.AngleAxis(degrees, axis.normalized);

            for (int i = 0; i < _tubeSpinners.Length && i < _spinHomes.Length; i++)
                if (_tubeSpinners[i] != null) _tubeSpinners[i].localRotation = _spinHomes[i] * delta;
        }

        private void RestoreSpin()
        {
            for (int i = 0; i < _tubeSpinners.Length && i < _spinHomes.Length; i++)
                if (_tubeSpinners[i] != null) _tubeSpinners[i].localRotation = _spinHomes[i];
        }

        private static int CountNonNull(Transform[] items)
        {
            int count = 0;
            for (int i = 0; i < items.Length; i++) if (items[i] != null) count++;
            return count;
        }
    }
}
