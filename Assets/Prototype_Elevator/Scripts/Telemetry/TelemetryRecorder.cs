using System;
using System.Collections.Generic;
using System.Globalization;
using Ascend.Prototype.Events;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Telemetry
{
    /// <summary>
    /// 층이 스핀 밖에서 들고 있는 문맥. 사건만으로는 값을 알 수 없는 순간이 있어서 둔다.
    ///
    /// 구체적으로: `RunSession`은 **생성자 안에서** 1층의 `FloorStarted`를 발행한다.
    /// 그 시점에는 아직 아무도 버스를 구독하지 않았으므로 1층의 요구 전력은 사건으로
    /// 전달되지 않는다. 적재 층에서 무엇인가를 실으면 요구 전력이 다시 바뀌는데
    /// `BoardingFinished`는 무게만 싣고 요구 전력을 싣지 않는다.
    ///
    /// 이 인터페이스를 붙이면 스핀 직전에 층에게 직접 물어 정확한 값을 쓴다.
    /// 붙이지 않아도 기록은 계속된다 — 텔레메트리 하나 때문에 씬 배선이 강제되면
    /// 아무도 켜지 않게 되고, 그러면 기록이 없는 것과 같다.
    /// </summary>
    public interface ITelemetryFloorContext
    {
        /// <summary>지금 진행 중인 층의 값. 층이 없으면 false를 돌려주고 아무것도 채우지 않는다.</summary>
        bool TryGetFloorContext(out float requiredPower, out float carriedWeight, out bool overloaded);

        /// <summary>
        /// 지금 규칙을 바꾸고 있는 승객·부품 요약(`SpinTelemetryRecord.Loadout` 형식).
        /// 적재를 읽을 수 없으면 false. 비어 있는 적재는 false가 아니라
        /// <c>SpinTelemetryRecord.NoneMarker</c>를 담은 true다 — "안 실었다"와 "못 읽었다"는 다르다.
        ///
        /// 왜 사건이 아니라 문맥인가: `ItemBoarded`는 **싣는 순간**만 알리고, 승객은
        /// 목적지에서 조용히 내린다(`BuildLoadout.TakeDeparting`). 사건만 누적하면
        /// 이미 내린 승객이 영원히 발동 중인 것처럼 기록된다.
        /// </summary>
        bool TryGetLoadoutSummary(out string summary);
    }

    /// <summary>
    /// 위험 단계를 알려 줄 수 있는 곳.
    ///
    /// 왜 사건만으로 부족한가: <see cref="GameEventKind.RiskLevelChanged"/>는
    /// `RiskEventBridge`(MonoBehaviour)가 폴링으로 낸다. 그 다리는 **바뀐 순간만** 알리고
    /// 첫 프레임의 Stable은 일부러 알리지 않는다. 그래서 사건만 보면 런의 앞부분이
    /// 통째로 비고, 씬 없는 헤드리스에서는 한 건도 오지 않는다.
    ///
    /// 텔레메트리가 <see cref="RiskEvaluator"/>를 직접 돌리는 선택지는 버렸다 —
    /// 히스테리시스 상태를 따로 들게 되어 "위험 단계"의 정의가 두 벌 생긴다
    /// (`GameEventKind` 주석이 금지하는 바로 그 상태다).
    /// </summary>
    public interface ITelemetryRiskSource
    {
        /// <summary>지금 단계. 알 수 없으면 false를 돌려준다 — Stable로 채우지 않는다.</summary>
        bool TryGetRiskLevel(out RiskLevel level);
    }

    /// <summary>
    /// 스핀 판정 구간의 프레임 타임과 GC 할당을 재는 곳.
    ///
    /// 기록기 본체가 `UnityEngine.Time`을 부르지 않는 이유는 이 클래스가 순수 C#이어야
    /// 씬 없이 검증되기 때문이다(`TECH_SPEC.md` §2). 구간의 시작과 끝을 나눠 받는 이유는
    /// GC 할당이 **차분**으로만 잴 수 있어서다.
    /// </summary>
    public interface ITelemetryPerformanceSampler
    {
        /// <summary>스핀이 시작됐다. 구간 기준점을 잡는다.</summary>
        void BeginSpin();

        /// <summary>구간을 닫고 값을 돌려준다. 잴 수 없으면 false.</summary>
        bool TryEndSpin(out float frameTimeMs, out long gcAllocBytes);
    }

    /// <summary>
    /// <see cref="GameEventBus"/>를 구독해 스핀 하나당 <see cref="SpinTelemetryRecord"/> 하나를 만든다.
    ///
    /// 왜 사건을 보는가: `SpinResolution`을 직접 훑으면 텔레메트리가 판정 코드의 두 번째
    /// 독자가 된다. `GameEventKind` 주석이 금지하는 상태 — "정화가 일어났다"의 정의가
    /// 여러 벌 생기는 것 — 와 같은 문제다. 사건 흐름 하나만 보면 사운드·승객 반응과
    /// 같은 것을 본다는 사실이 구조로 보장된다.
    ///
    /// 순수 C#이라 씬 없이 돈다. 런이 버스를 소유하므로(`RunSession.Events` 주석)
    /// 런이 죽으면 이 구독도 함께 죽는다 — 두 런의 기록이 한 파일에 섞이지 않는다.
    /// </summary>
    public sealed class TelemetryRecorder
    {
        private readonly GameEventBus _bus;
        private readonly ITelemetrySink _sink;
        private readonly ITelemetryFloorContext _context;

        // ── 층 문맥 ──
        private int _floor;

        /// <summary>
        /// <see cref="_requiredPower"/>가 **어느 층의** 값인가. 0 은 "요구 전력이 0"과
        /// "모른다"를 구분하지 못하고, bool 하나로는 앞 층 값이 다음 층에 새는 것을
        /// 막지 못한다. 모르는 것을 아는 것처럼 적는 로그가 가장 나쁘다.
        /// </summary>
        private int _requiredPowerFloor = -1;
        private float _requiredPower;
        private float _carriedWeight;
        private bool _overloaded;
        private string _contractLabel;

        // ── 위험 단계 ──
        //
        // `_riskObserved`가 따로 있는 이유는 `_requiredPowerFloor`와 같다. Stable(=0)은
        // "안정이다"와 "아무도 알려 주지 않았다"를 구분하지 못하고, 후자를 전자로 적으면
        // 사고 기록이 조용히 거짓이 된다.
        private RiskLevel _riskLevel = Risk.RiskLevel.Stable;
        private bool _riskObserved;

        // ── 스핀 문맥 ──
        private bool _spinStartSeen;
        private bool _spinIsExtra;
        private float _powerBeforeSpin;

        private bool _attached;

        /// <summary>빈 목록의 단일 인스턴스. 스핀마다 빈 배열을 새로 만들면 그 자체가 GC 할당이다.</summary>
        private static readonly string[] EmptyList = new string[0];

        public TelemetryRecorder(GameEventBus bus, ITelemetrySink sink)
            : this(bus, sink, null)
        {
        }

        public TelemetryRecorder(GameEventBus bus, ITelemetrySink sink, ITelemetryFloorContext context)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus), "버스가 없으면 구독할 사건이 없다.");
            if (sink == null) throw new ArgumentNullException(nameof(sink), "기록을 흘려보낼 곳이 없다.");

            _bus = bus;
            _sink = sink;
            _context = context;
            _bus.Published += OnEvent;
            _attached = true;
        }

        /// <summary>만들어진 레코드 수.</summary>
        public int RecordCount { get; private set; }

        /// <summary>
        /// 기록하지 못하고 버린 <see cref="GameEventKind.SpinResolved"/> 수.
        /// 0이 아니면 사건 계약이 깨진 것이므로 조용히 넘기지 않는다.
        /// </summary>
        public int DroppedCount { get; private set; }

        /// <summary>가장 최근 경고 문장. 없으면 null.</summary>
        public string LastWarning { get; private set; }

        /// <summary>경고를 받을 곳. 씬 어댑터가 `Debug.LogWarning`을 물린다.</summary>
        public Action<string> WarningReported { get; set; }

        /// <summary>
        /// 위험 단계를 물어볼 곳. 붙이지 않으면 <see cref="GameEventKind.RiskLevelChanged"/>
        /// 사건만 보고, 그마저 없으면 <c>(unknown)</c>으로 적는다.
        ///
        /// 생성자가 아니라 프로퍼티인 이유: 이미 두 벌인 생성자에 선택 협력자를 더 얹으면
        /// 조합이 넷이 된다. `WarningReported`와 같은 방식으로 객체 초기자에서 붙인다.
        /// </summary>
        public ITelemetryRiskSource RiskSource { get; set; }

        /// <summary>프레임 타임·GC를 재 줄 곳. 없으면 NaN·−1로 적는다.</summary>
        public ITelemetryPerformanceSampler PerformanceSampler { get; set; }

        /// <summary>구독을 끊는다. 런을 버리거나 기록을 멈출 때 호출한다.</summary>
        public void Detach()
        {
            if (!_attached) return;
            _bus.Published -= OnEvent;
            _attached = false;
        }

        private void OnEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.FloorStarted:
                    BeginFloor(e.Floor, e.FloatValue);
                    break;

                case GameEventKind.BoardingFinished:
                    // 적재를 끝낸 시점의 무게와 과적 여부. `FloorSession.FinishBoarding`이
                    // Text에 "과적"을 싣는다 — 그 문자열이 유일한 신호다.
                    _carriedWeight = e.FloatValue;
                    _overloaded = e.Text == "과적";
                    break;

                case GameEventKind.ContractSelected:
                    _contractLabel = e.Text;
                    break;

                case GameEventKind.RiskLevelChanged:
                    // IntValue = 새 RiskLevel (`GameEventKind` 주석). 값은 고정돼 있으므로
                    // 정의되지 않은 숫자가 오면 조용히 받아들이지 않는다.
                    if (Enum.IsDefined(typeof(RiskLevel), e.IntValue))
                    {
                        _riskLevel = (RiskLevel)e.IntValue;
                        _riskObserved = true;
                    }
                    else
                    {
                        Warn($"RiskLevelChanged 의 IntValue 가 {e.IntValue} — RiskLevel 에 없는 값이다. 무시한다.");
                    }
                    break;

                case GameEventKind.SpinStarted:
                    _spinStartSeen = true;
                    _spinIsExtra = e.Text == "추가 스핀";
                    _powerBeforeSpin = e.FloatValue;
                    PullContext(e.Floor);
                    PerformanceSampler?.BeginSpin();
                    break;

                case GameEventKind.SpinResolved:
                    RecordSpin(in e);
                    break;

                case GameEventKind.RunEnded:
                    _sink.Flush();
                    break;
            }
        }

        private void BeginFloor(int floor, float requiredPower)
        {
            _floor = floor;
            _requiredPower = requiredPower;
            _requiredPowerFloor = floor;
            // 계약은 층마다 다시 고른다. 이월하면 계약 없는 층이 앞 층의 계약을 쓴 것처럼 보인다.
            _contractLabel = null;
            // 무게와 과적은 이월한다 — 적재는 층을 건너 살아남는다(`RunSession.Loadout`).
        }

        private void PullContext(int floor)
        {
            if (_context == null) return;
            if (!_context.TryGetFloorContext(out float required, out float weight, out bool overloaded))
                return;
            _requiredPower = required;
            _requiredPowerFloor = floor;
            _carriedWeight = weight;
            _overloaded = overloaded;
        }

        private void RecordSpin(in GameEvent e)
        {
            if (!(e.Payload is SpinResolution resolution))
            {
                DroppedCount++;
                Warn($"SpinResolved 의 Payload 가 SpinResolution 이 아니다 (floor={e.Floor}, spin={e.SpinIndex}). " +
                     "이 스핀은 기록되지 않는다.");
                return;
            }

            // 층 시작을 못 본 층. `RunSession` 생성자가 발행한 1층이 대표적이다 —
            // 그때는 아직 아무도 버스를 구독하지 않았다.
            if (_floor != resolution.Floor)
            {
                _floor = resolution.Floor;
                _contractLabel = null;
            }
            if (_requiredPowerFloor != resolution.Floor)
            {
                _requiredPower = 0f;
                // 층당 한 번만 알린다. 같은 문장을 스핀마다 반복하면 진짜 경고가 묻힌다.
                _requiredPowerFloor = resolution.Floor;
                Warn($"{resolution.Floor}층의 FloorStarted 를 보지 못했다 — 요구 전력을 알 수 없다(0으로 적는다). " +
                     "ITelemetryFloorContext 를 붙이면 채워진다.");
            }

            if (!_spinStartSeen)
            {
                // 시작을 못 봤으면 스핀 전 전력을 모른다. 0으로 두면 powerAfter 가 거짓이 되므로
                // 순 전력만 적고 누적은 포기한다 — 모른다는 사실이 드러나야 한다.
                Warn($"{resolution.Floor}층 {resolution.SpinIndex}번 스핀의 SpinStarted 를 보지 못했다 — " +
                     "powerAfter 가 이 스핀의 순 전력만 담는다.");
                _powerBeforeSpin = 0f;
                _spinIsExtra = false;
            }

            SummarizeSteps(in resolution, out int normalSouls, out int purifyCount, out PatternKind best,
                out string[] cascadeBoards, out string[] activationOrder);

            // 재지 못한 것은 0이 아니라 "모른다"로 남긴다. 0 ms·0 B는 존재할 법한 값이라
            // 나중에 "성능이 좋았다"로 읽히고, 그 오독은 되돌릴 방법이 없다.
            float frameTimeMs = float.NaN;
            long gcAllocBytes = SpinTelemetryRecord.UnknownBytes;
            if (PerformanceSampler != null &&
                PerformanceSampler.TryEndSpin(out float sampledFrameMs, out long sampledBytes))
            {
                frameTimeMs = sampledFrameMs;
                gcAllocBytes = sampledBytes;
            }

            var record = new SpinTelemetryRecord
            {
                RunSeed = resolution.RunSeed,
                SpinSeed = resolution.Seed,
                Floor = resolution.Floor,
                SpinIndex = resolution.SpinIndex,
                IsExtraSpin = _spinIsExtra,
                Contract = ResolveContractLabel(in resolution),
                InitialBoard = resolution.InitialBoard.ToString(),
                FinalBoard = resolution.FinalBoard.ToString(),
                CascadeDepth = resolution.ChainDepth,
                CascadeCapped = resolution.CascadeCapReached,
                NormalSouls = normalSouls,
                PurifyCount = purifyCount,
                BestPattern = best.ToString(),
                GrossPower = resolution.GrossPower,
                ResidualLoss = resolution.Residual.StoredPowerLoss,
                NetPower = resolution.NetPower,
                PowerAfter = _powerBeforeSpin + resolution.NetPower,
                RequiredPower = _requiredPower,
                CarriedWeight = _carriedWeight,
                Overloaded = _overloaded,
                CascadeBoards = cascadeBoards,
                ActivationOrder = activationOrder,
                ResidualAbsorbers = resolution.Residual.AbsorberCount,
                ResidualProliferators = resolution.Residual.ProliferatorCount,
                RiskLevel = ResolveRiskLevel(),
                Loadout = ResolveLoadout(),
                FrameTimeMs = frameTimeMs,
                GcAllocBytes = gcAllocBytes,
            };

            _sink.Write(in record);
            RecordCount++;

            _spinStartSeen = false;
        }

        /// <summary>
        /// 출처가 있으면 그 값을, 없으면 마지막으로 **관측된** 사건 값을, 둘 다 없으면
        /// "모른다"를 돌려준다. 출처를 먼저 보는 이유는 사건이 전이만 알리기 때문이다 —
        /// 런 시작 직후에는 아직 아무 전이도 없었지만 단계는 존재한다.
        /// </summary>
        private string ResolveRiskLevel()
        {
            if (RiskSource != null && RiskSource.TryGetRiskLevel(out RiskLevel level))
                return level.ToString();
            if (_riskObserved) return _riskLevel.ToString();
            return SpinTelemetryRecord.Unknown;
        }

        private string ResolveLoadout()
        {
            if (_context == null) return SpinTelemetryRecord.Unknown;
            if (!_context.TryGetLoadoutSummary(out string summary)) return SpinTelemetryRecord.Unknown;
            return string.IsNullOrEmpty(summary) ? SpinTelemetryRecord.NoneMarker : summary;
        }

        /// <summary>
        /// 계약 이름의 단일 출처는 스핀에 실제로 적용된 계약이다. `ContractSelected`
        /// 사건은 선택을 알릴 뿐이고, 계약 선택 단계가 없는 층에서는 아예 오지 않는다.
        /// </summary>
        private string ResolveContractLabel(in SpinResolution resolution)
        {
            if (!string.IsNullOrEmpty(resolution.Contract.Label)) return resolution.Contract.Label;
            if (!string.IsNullOrEmpty(_contractLabel)) return _contractLabel;
            return ResistanceContract.None.Label;
        }

        /// <summary>
        /// 캐스케이드 단계를 한 번만 훑어 요약과 **순서 보존 목록** 둘 다 만든다.
        /// 정화 개수와 최고 패턴은 `SpinResolution.Summary()`가 화면용으로 세는 것과 같은
        /// 정의여야 한다.
        ///
        /// 요약(개수·최고값)과 목록(순서)을 함께 내는 이유: 두 번 훑으면 "정화가 무엇인가"의
        /// 정의가 두 벌 생기고, 한쪽만 고쳤을 때 개수와 목록이 서로 다른 말을 하게 된다.
        /// 그 어긋남은 로그를 읽는 사람이 알아챌 수 없다.
        ///
        /// <paramref name="cascadeBoards"/>는 단계마다 **끝난 뒤의** 판이다. 시작 판은
        /// `InitialBoard`가 이미 들고 있으므로 둘을 이으면 사슬 전체가 복원된다.
        /// </summary>
        private static void SummarizeSteps(in SpinResolution resolution,
            out int normalSouls, out int purifyCount, out PatternKind best,
            out string[] cascadeBoards, out string[] activationOrder)
        {
            normalSouls = 0;
            purifyCount = 0;
            best = PatternKind.None;
            cascadeBoards = EmptyList;
            activationOrder = EmptyList;

            CascadeStep[] steps = resolution.Steps;
            if (steps == null || steps.Length == 0) return;

            cascadeBoards = new string[steps.Length];
            var order = new List<string>(steps.Length * 2);

            for (int i = 0; i < steps.Length; i++)
            {
                CascadeStep step = steps[i];
                cascadeBoards[i] = step.BoardAfter.ToString();

                string depth = step.Depth.ToString(CultureInfo.InvariantCulture);

                normalSouls += step.NormalSoulsHarvested;
                if (step.NormalSoulsHarvested > 0)
                {
                    order.Add(depth + ":Soul*" +
                              step.NormalSoulsHarvested.ToString(CultureInfo.InvariantCulture));
                }

                if (step.Purifies == null) continue;
                purifyCount += step.Purifies.Length;
                for (int p = 0; p < step.Purifies.Length; p++)
                {
                    PurifyEvent purify = step.Purifies[p];
                    if (purify.Pattern > best) best = purify.Pattern;

                    int cells = purify.Cells != null ? purify.Cells.Length : 0;
                    order.Add(depth + ":" + purify.Kind.ToString() + "/" + purify.Pattern.ToString() +
                              "*" + cells.ToString(CultureInfo.InvariantCulture));
                }
            }

            activationOrder = order.Count == 0 ? EmptyList : order.ToArray();
        }

        private void Warn(string message)
        {
            LastWarning = message;
            WarningReported?.Invoke("[상승] 텔레메트리: " + message);
        }
    }
}
