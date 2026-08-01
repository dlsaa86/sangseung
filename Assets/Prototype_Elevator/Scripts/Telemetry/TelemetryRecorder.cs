using System;
using Ascend.Prototype.Events;
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

        // ── 스핀 문맥 ──
        private bool _spinStartSeen;
        private bool _spinIsExtra;
        private float _powerBeforeSpin;

        private bool _attached;

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

                case GameEventKind.SpinStarted:
                    _spinStartSeen = true;
                    _spinIsExtra = e.Text == "추가 스핀";
                    _powerBeforeSpin = e.FloatValue;
                    PullContext(e.Floor);
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

            SummarizeSteps(in resolution, out int normalSouls, out int purifyCount, out PatternKind best);

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
            };

            _sink.Write(in record);
            RecordCount++;

            _spinStartSeen = false;
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
        /// 캐스케이드 단계를 한 스핀 요약으로 접는다. 정화 개수와 최고 패턴은
        /// `SpinResolution.Summary()`가 화면용으로 세는 것과 같은 정의여야 한다.
        /// </summary>
        private static void SummarizeSteps(in SpinResolution resolution,
            out int normalSouls, out int purifyCount, out PatternKind best)
        {
            normalSouls = 0;
            purifyCount = 0;
            best = PatternKind.None;

            if (resolution.Steps == null) return;
            foreach (CascadeStep step in resolution.Steps)
            {
                normalSouls += step.NormalSoulsHarvested;
                if (step.Purifies == null) continue;
                purifyCount += step.Purifies.Length;
                foreach (PurifyEvent purify in step.Purifies)
                    if (purify.Pattern > best) best = purify.Pattern;
            }
        }

        private void Warn(string message)
        {
            LastWarning = message;
            WarningReported?.Invoke("[상승] 텔레메트리: " + message);
        }
    }
}
