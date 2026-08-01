using System;
using UnityEngine;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Telemetry
{
    /// <summary>
    /// 인게임 런에 <see cref="TelemetryRecorder"/>를 붙이는 얇은 Unity 어댑터.
    ///
    /// 왜 별도 컴포넌트인가: 기록기 자체는 순수 C#이라야 씬 없이 검증된다
    /// (`TECH_SPEC.md` §2). 그렇다고 아무도 붙여 주지 않으면 `MASTER_PRD.md` §4.1이
    /// 필수로 요구하는 텔레메트리가 코드에는 있고 플레이에는 없는 상태가 된다 —
    /// 백로그 `UP-TEST-05`가 잡고 있던 것이 정확히 그 간극이다.
    ///
    /// 런마다 새로 만든다. `RunSessionBehaviour.ResetRun`이 새 <see cref="RunSession"/>을
    /// 세우면 버스도 새것이므로, 옛 기록기를 끊지 않으면 죽은 버스를 붙들고 있게 된다.
    ///
    /// <see cref="RunSessionTelemetryContext"/>를 함께 넘기는 이유: `RunSession`은
    /// **생성자 안에서** 1층 `FloorStarted`를 발행한다. `RunStarted`는 그보다 뒤에
    /// 울리므로 어떤 구독자도 1층의 요구 전력을 사건으로 받을 수 없다.
    ///
    /// 위험 단계와 성능 표본을 이 컴포넌트가 직접 구현하는 이유: 둘 다 `UnityEngine`이
    /// 필요하고(`RiskStateView`, `Time.unscaledDeltaTime`, `GC`), 기록기 본체는 씬 없이
    /// 도는 순수 C#이어야 한다(`TECH_SPEC.md` §2). 어댑터를 더 만들지 않고 이미 존재하는
    /// 이 어댑터가 맡는다 — "얇은 Unity 어댑터"가 이 클래스의 정의 그대로다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TelemetryRecorderBehaviour : MonoBehaviour,
        ITelemetryRiskSource, ITelemetryPerformanceSampler
    {
        [Tooltip("비우면 같은 오브젝트 → 씬 전체 순으로 찾는다.")]
        [SerializeField] private RunSessionBehaviour _run;

        [Tooltip("위험 단계의 출처. 비우면 씬에서 찾고, 없으면 RiskLevelChanged 사건만 본다.")]
        [SerializeField] private RiskStateView _riskView;

        [Tooltip("끄면 메모리에만 쌓는다. 디스크 접근 없이 프로파일링할 때 쓴다.")]
        [SerializeField] private bool _writeFiles = true;

        [Tooltip("비우면 Logs/telemetry. 상대 경로는 작업 디렉터리 기준이다.")]
        [SerializeField] private string _directoryOverride = string.Empty;

        private TelemetryRecorder _recorder;
        private ITelemetrySink _sink;

        /// <summary>지금 붙어 있는 기록기. 런이 없으면 null.</summary>
        public TelemetryRecorder Recorder => _recorder;

        /// <summary>지금 쓰고 있는 배출구. 디버그 패널이 <see cref="InMemoryTelemetrySink"/>로 캐스팅해 읽는다.</summary>
        public ITelemetrySink Sink => _sink;

        /// <summary>파일로 쓰는 중이라면 jsonl 경로. 아니면 null.</summary>
        public string CurrentFilePath => _sink is TelemetryFileSink file ? file.JsonlPath : null;

        private void Awake()
        {
            // 없어도 기록은 계속된다 — 위험 단계만 사건 폴백으로 내려앉는다.
            // 텔레메트리 하나 때문에 씬 배선이 강제되면 아무도 켜지 않게 된다.
            if (_riskView == null) _riskView = FindAnyObjectByType<RiskStateView>();

            if (_run == null) _run = GetComponent<RunSessionBehaviour>();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_run == null)
            {
                // 조용히 꺼지면 "기록이 켜져 있다"고 믿은 채 런을 돌리게 된다.
                Debug.LogWarning("[상승] 텔레메트리: RunSessionBehaviour 를 찾지 못해 기록하지 않는다.");
                return;
            }

            _run.RunStarted += OnRunStarted;
            // 이 컴포넌트보다 런이 먼저 시작됐을 수 있다(스크립트 실행 순서).
            if (_run.Session != null) OnRunStarted(_run.Session);
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Teardown();
        }

        /// <summary>
        /// 플레이 모드를 끄는 것은 정상 종료가 아니다 — `RunEnded`가 오지 않으므로
        /// 버퍼가 그대로 사라진다. 조사하고 싶은 런이 대개 끝까지 가지 못한 런이라
        /// 이 한 줄이 없으면 가장 필요한 기록만 없다.
        /// </summary>
        private void OnApplicationQuit() => Teardown();

        private void OnRunStarted(RunSession session)
        {
            Teardown();
            if (session == null) return;

            if (_writeFiles)
            {
                var file = new TelemetryFileSink(session.Seed,
                    string.IsNullOrEmpty(_directoryOverride) ? null : _directoryOverride)
                {
                    ErrorReported = Debug.LogWarning,
                };
                _sink = file;
            }
            else _sink = new InMemoryTelemetrySink();

            _spinStartHeapBytes = SpinTelemetryRecord.UnknownBytes;

            _recorder = new TelemetryRecorder(session.Events, _sink,
                new RunSessionTelemetryContext(session))
            {
                WarningReported = Debug.LogWarning,
                RiskSource = this,
                PerformanceSampler = this,
            };
        }

        private void Teardown()
        {
            _recorder?.Detach();
            _recorder = null;
            _sink?.Flush();
            _sink = null;
        }

        // ── ITelemetryRiskSource ────────────────────────────────────────────

        /// <summary>
        /// `RiskStateView`가 위험 단계의 단일 출처다(그 클래스의 `Level` 프로퍼티).
        /// 없으면 false를 돌려주고, 기록기는 사건 폴백으로 내려간다 — Stable로 채우지 않는다.
        /// </summary>
        bool ITelemetryRiskSource.TryGetRiskLevel(out RiskLevel level)
        {
            level = RiskLevel.Stable;
            if (_riskView == null) return false;
            level = _riskView.Level;
            return true;
        }

        // ── ITelemetryPerformanceSampler ────────────────────────────────────

        /// <summary>스핀 시작 시점의 관리 힙. −1이면 구간이 열려 있지 않다.</summary>
        private long _spinStartHeapBytes = SpinTelemetryRecord.UnknownBytes;

        void ITelemetryPerformanceSampler.BeginSpin()
        {
            // `GC.Collect()`를 부르지 않는다. 강제 수집은 그 자체로 수십 ms 스파이크라
            // 같은 스핀의 프레임 타임을 망친다(`MemoryTrendProbe` 주석과 같은 판단).
            _spinStartHeapBytes = GC.GetTotalMemory(false);
        }

        bool ITelemetryPerformanceSampler.TryEndSpin(out float frameTimeMs, out long gcAllocBytes)
        {
            // 스핀 판정은 `FloorSession.Spin()` 안에서 동기로 끝나므로 시작과 끝이 같은
            // 프레임이다. 따라서 이 프레임 시간이 곧 "스핀이 든 프레임"의 시간이다.
            // timeScale 에 영향받지 않는 값을 쓴다 — 연출이 시간을 늦춰도 성능은 그대로다.
            frameTimeMs = Time.unscaledDeltaTime * 1000f;

            if (_spinStartHeapBytes < 0L)
            {
                // SpinStarted 를 못 봤다. 프레임 타임은 유효하지만 할당 차분은 없다.
                gcAllocBytes = SpinTelemetryRecord.UnknownBytes;
                return true;
            }

            gcAllocBytes = GC.GetTotalMemory(false) - _spinStartHeapBytes;
            _spinStartHeapBytes = SpinTelemetryRecord.UnknownBytes;
            return true;
        }
    }
}

// 씬 배선 필요: Prototype_Elevator.unity 에서 RunSessionBehaviour 가 붙어 있는
// 오브젝트에 TelemetryRecorderBehaviour 를 추가한다. 인스펙터 값은 기본값 그대로 둔다
// (_run 은 비워 두면 같은 오브젝트에서 찾는다). 씬 소유자가 처리한다 — 이 에이전트는
// .unity 를 건드리지 않는다.
