using System;
using System.Collections.Generic;
using Ascend.Prototype.Build;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Events;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>Pure-C# ten-floor run coordinator.</summary>
    public sealed class RunSession
    {
        private readonly IFloorPlanSource _floors;
        private readonly SpinEngine _engine;
        private readonly PowerThresholds _thresholds;
        private readonly List<FloorResult> _results = new List<FloorResult>();
        private readonly BuildLoadout _loadout = new BuildLoadout();
        private readonly List<BuildItem> _lastDeparted = new List<BuildItem>();
        private readonly List<FloorAscent> _ascents = new List<FloorAscent>();
        private ResidualState _residual;
        private FloorSession _current;
        private float _baseWeight;

        // ── 위험 판정은 런이 소유한다 (`D-1` 2026-08-05) ──────────────────────
        //
        // <see cref="Risk.RiskEvaluator"/> 는 히스테리시스 때문에 **입력의 함수가 아니라
        // 입력 궤적의 함수**다. 그래서 「언제 부르는가」가 곧 판정이다.
        //
        // 직전 판본은 `RiskStateView.LateUpdate` **한 곳**에서만 불렀다. 그 컴포넌트는
        // 연출이고 프레임당 한 번 도므로, 한 프레임 안에서 전이가 여러 번 일어나면
        // 중간값이 통째로 사라졌다. 실측 경로 —
        // `RouletteInteractionBridge.OnOverharvestPulled` 은 `PushYourLuck()`(앤티 지불,
        // 과수확 카운터 +1 → 점수 급등)과 `DoSpin()`(잔류 정리 → 점수 하락)을 **같은
        // 프레임에** 끝낸다. 점수 궤적이 2 → 8 → 6 이면
        //   · 단계별 샘플: 8 에서 Critical 진입, 6 은 이탈 5.5 위라 **Critical 유지**
        //   · 프레임 샘플: 6 하나만 보므로 진입 7 미만 → **Strain**
        // 이고, `AccidentRecorder.TrackPeakRisk` 가 그 값으로 최고 위험을 적으므로
        // **같은 시드가 다른 사고 기록을 남겼다.** 프레임 타이밍이 판정을 바꾼 것이다.
        //
        // 그래서 판정기는 상태를 바꾸는 쪽이 소유하고, 전이가 일어나는 자리마다
        // <see cref="EvaluateRisk"/> 를 부른다. 뷰는 <see cref="RiskLevel"/> 을 **읽기만** 한다.
        private readonly Risk.RiskEvaluator _riskEvaluator = new Risk.RiskEvaluator();

        /// <summary>왜 이 단계인가. 판정과 **같은 순간에** 지어야 기록과 어긋나지 않는다.</summary>
        private string _riskReason = NoRiskReason;

        /// <summary>위험 요인이 하나도 없을 때의 문구. 기록기가 같은 문자열을 비교한다.</summary>
        public const string NoRiskReason = "위험 요인 없음";

        /// <summary>층마다 그대로 넘어가는 과수확 수치 9종 (`UP-POWER-07`).</summary>
        private readonly OverharvestSnapshot _overharvest;

        /// <summary>
        /// 층마다 그대로 넘어가는 과적 수치 3종 (`UP-TECH-09` ⑤). 런 도중에 허용 중량이
        /// 바뀌면 앞선 층의 기록과 뒤 층의 판정이 다른 규칙을 쓰게 되므로 런이 소유한다.
        /// </summary>
        private readonly WeightSnapshot _weight;

        /// <summary>층마다 그대로 넘어가는 스핀 밸런스 10종 (`UP-TECH-09` ①③).</summary>
        private readonly SpinBalanceSnapshot _balance;

        /// <summary>
        /// 층마다 그대로 넘어가는 남은 스핀 정산 수치 (`T-05`). 런이 소유하는 이유는
        /// 과적·과수확과 같다 — 런 도중에 정산율이 바뀌면 앞 층의 기록과 뒤 층의 판정이
        /// 다른 규칙을 쓰게 되고, 사고 기록기가 적은 숫자를 재현할 수 없게 된다.
        /// </summary>
        private readonly RemainingSpinSettlementSnapshot _settlement;

        /// <summary>정산 수치의 출처. 하네스가 「에셋이 읽혔는가」를 이걸로 묻는다.</summary>
        public string SettlementSource => _settlement.Source;

        public RunSession(int seed = 1337, float startingWeight = 0f, float startingMoney = 0f)
            : this(seed, startingWeight, startingMoney,
                FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation)
        {
        }

        public RunSession(int seed, float startingWeight, float startingMoney,
            float anteRatio, float anteEscalation)
            : this(seed, startingWeight, startingMoney, anteRatio, anteEscalation, null)
        {
        }

        /// <summary>
        /// <paramref name="floors"/>가 null이면 10층 커리큘럼. Hero Slice는
        /// <see cref="HeroSliceFloorSource"/>를 넘긴다.
        /// </summary>
        public RunSession(int seed, float startingWeight, float startingMoney,
            float anteRatio, float anteEscalation, IFloorPlanSource floors)
            : this(seed, startingWeight, startingMoney,
                new OverharvestSnapshot(
                    Math.Max(0f, anteRatio), Math.Max(0f, anteEscalation),
                    OverharvestProfile.DefaultUnlockThreshold,
                    OverharvestProfile.DefaultApproachMachineDuckScale,
                    OverharvestProfile.DefaultMinSilenceSeconds,
                    OverharvestProfile.DefaultMaxSilenceSeconds,
                    OverharvestProfile.DefaultPassengerGazeDelaySeconds,
                    OverharvestProfile.DefaultResumeFadeSeconds,
                    OverharvestProfile.DefaultMaxExtraSpins),
                floors)
        {
        }

        /// <summary>
        /// 과수확 수치 전체를 받는 경로. `RunSessionBehaviour` 가 `OverharvestProfile.asset`
        /// 에서 스냅샷을 떠 넘긴다. 에셋이 없으면 코드 기본값이라 동작이 같다.
        /// </summary>
        public RunSession(int seed, float startingWeight, float startingMoney,
            OverharvestSnapshot overharvest, IFloorPlanSource floors)
            : this(seed, startingWeight, startingMoney, overharvest,
                WeightProfile.DefaultSnapshot, floors)
        {
        }

        /// <summary>
        /// 과적 수치까지 받는 경로. `RunSessionBehaviour` 가 `WeightProfile.asset` 에서
        /// 스냅샷을 떠 넘긴다. 에셋이 없으면 코드 프리셋이라 동작이 같다.
        /// </summary>
        public RunSession(int seed, float startingWeight, float startingMoney,
            OverharvestSnapshot overharvest, WeightSnapshot weight, IFloorPlanSource floors)
            : this(seed, startingWeight, startingMoney, overharvest, weight,
                SpinBalanceProfile.DefaultSnapshot, floors)
        {
        }

        /// <summary>
        /// 스핀 밸런스까지 받는 경로. `RunSessionBehaviour` 가 `SpinBalanceProfile.asset`
        /// 에서 스냅샷을 떠 넘긴다. 에셋이 없으면 코드 프리셋이라 동작이 같다.
        /// </summary>
        public RunSession(int seed, float startingWeight, float startingMoney,
            OverharvestSnapshot overharvest, WeightSnapshot weight,
            SpinBalanceSnapshot balance, IFloorPlanSource floors)
            : this(seed, startingWeight, startingMoney, overharvest, weight, balance,
                RemainingSpinSettlementProfile.DefaultSnapshot, floors)
        {
        }

        /// <summary>
        /// 남은 스핀 정산 수치까지 받는 경로. `RunSessionBehaviour` 가
        /// `RemainingSpinSettlementProfile.asset` 에서 스냅샷을 떠 넘긴다.
        ///
        /// 이 층이 없어서 그 프로파일은 만들어도 아무 효과가 없었다 —
        /// <see cref="FloorSession"/> 의 같은 이름 생성자 주석에 경위가 있다.
        /// </summary>
        public RunSession(int seed, float startingWeight, float startingMoney,
            OverharvestSnapshot overharvest, WeightSnapshot weight,
            SpinBalanceSnapshot balance, RemainingSpinSettlementSnapshot settlement,
            IFloorPlanSource floors)
        {
            _settlement = settlement;
            _floors = floors ?? new TenFloorSource();
            _engine = new SpinEngine(seed);
            _thresholds = PowerThresholds.Default;
            Seed = seed;
            _baseWeight = Math.Max(0f, startingWeight);
            Money = startingMoney;
            _overharvest = overharvest;
            _weight = weight;
            _balance = balance;

            // 적재가 어느 경로로 바뀌든 현재 층이 즉시 안다. 호출부마다 갱신을 기억하게
            // 하면 하나만 빠져도 무게와 요구 전력이 조용히 어긋난다 — 실제로 그렇게 됐다.
            _loadout.Changed += OnLoadoutChanged;

            CurrentFloor = _floors.FirstFloor;
            CreateCurrentFloor();

            // 시작 상태도 판정된 상태여야 한다. 안 하면 첫 전이가 일어나기 전까지
            // `RiskLevel` 이 「아직 아무것도 안 본 Stable」이고, 시작부터 과적인 런에서
            // 첫 스핀 전까지 방이 안전해 보인다.
            EvaluateRisk();
        }

        /// <summary>
        /// 적재 변경을 현재 층에 반영한다. **이미 확정된 층은 건드리지 않는다.**
        ///
        /// 하차는 층이 끝난 **뒤** `CompleteFloor` 안에서 일어나고, 그 시점의 `_current`는
        /// 아직 방금 확정된 그 층이다. 가드가 없으면 하차가 그 층의 `_carriedWeight`와
        /// `_requiredPower`를 사후에 바꾸고, `AccidentRecorder`는 한 프레임 뒤에 기록하므로
        /// **사고 기록기가 그 층에 실제로 적용되지 않았던 숫자를 적는다.**
        /// 같은 기록 안의 `result.RequiredPower`와도 어긋난다.
        ///
        /// 무게 전파를 고치면서 정확히 같은 부류의 결함을 방향만 뒤집어 만들었고,
        /// 독립 감사가 잡았다. 캐시를 갱신하는 코드는 "언제 갱신하지 말아야 하는가"를
        /// 함께 정해야 한다.
        /// </summary>
        private void OnLoadoutChanged()
        {
            // 최고치는 층이 확정된 뒤의 하차에도 갱신되어야 하므로 가드 앞에서 잰다.
            if (CarriedWeight > PeakCarriedWeight) PeakCarriedWeight = CarriedWeight;

            if (_current == null || _current.Result != null) return;
            _current.RefreshLoad(_baseWeight);
            EvaluateRisk();   // 적재가 바뀌면 과적 여부와 요구 전력 달성률이 함께 움직인다.
        }

        /// <summary>
        /// 한 층의 상승 정산. **클램프 뒤** 실제로 오른 층수와 실제로 지급한 돈을 담는다.
        ///
        /// `FloorResult.FloorsAscended`는 클램프 **전** 값이라 정산을 검증하는 데 쓸 수 없다.
        /// 그 차이 때문에 이중 지급 회귀 테스트가 잘못된 기대값을 계산했고, 결국
        /// 아무것도 검사하지 않는 단언으로 끝났다. 정산의 근거는 정산한 쪽이 남겨야 한다.
        /// </summary>
        public readonly struct FloorAscent
        {
            public readonly int FromFloor;
            public readonly int FloorsAscended;
            public readonly float ExcessPower;
            public readonly float PowerPerExtraFloor;
            /// <summary>초과 전력을 환산해 준 돈.</summary>
            public readonly float MoneyCredited;

            /// <summary>
            /// 남은 스핀 **운행 효율 정산**으로 준 돈 (`T-05` · `D-20260802-10`).
            ///
            /// ⚠ `MoneyCredited` 와 **따로 적는다.** 둘은 서로 다른 것을 갚는다 —
            /// 앞은 초과 전력, 뒤는 안 쓴 운행 여유. 합쳐 적으면 「초과 전력이
            /// 얼마였나」를 사후에 복원할 수 없고, 정산 비율을 바꿨을 때
            /// 어느 쪽이 움직였는지도 못 가른다.
            ///
            /// **그리고 이 필드가 없으면 장부가 안 맞는다.** 처음에 정산금을
            /// `Money` 에만 더하고 기록을 안 남겼더니 「소지금이 지급 기록 합계와
            /// 일치한다」 단정이 **90.00 차이로 즉시 걸렸다.** 그 단정이 옳았다 —
            /// 근거 없는 돈은 근거 없는 숫자다.
            /// </summary>
            public readonly float SettlementMoney;

            /// <summary>정산에 쓰인 남은 스핀 수. 0 이면 과수확을 골랐거나 다 썼다.</summary>
            public readonly int SettledSpins;

            public FloorAscent(int fromFloor, int floorsAscended, float excessPower,
                float powerPerExtraFloor, float moneyCredited,
                float settlementMoney = 0f, int settledSpins = 0)
            {
                FromFloor = fromFloor;
                FloorsAscended = floorsAscended;
                ExcessPower = excessPower;
                PowerPerExtraFloor = powerPerExtraFloor;
                MoneyCredited = moneyCredited;
                SettlementMoney = settlementMoney;
                SettledSpins = settledSpins;
            }

            /// <summary>기본 1층을 넘어 추가로 산 층 수.</summary>
            public int ExtraFloors => Math.Max(0, FloorsAscended - 1);

            /// <summary>이 층에서 받은 돈 전부. 장부 대조는 이 값을 합산한다.</summary>
            public float TotalMoney => MoneyCredited + SettlementMoney;
        }

        /// <summary>
        /// 이 런에서 일어난 일이 흐르는 곳. **런이 소유한다** — 런을 다시 시작하면
        /// 버스도 새로 태어나므로 옛 구독자가 두 런의 사건을 한 파일에 섞지 못한다.
        ///
        /// 텔레메트리·사운드·승객 반응·위험 연출이 전부 여기를 구독한다.
        /// </summary>
        public GameEventBus Events { get; } = new GameEventBus();

        /// <summary>이 런의 층 구성. HUD가 "1층 중 1층"인지 "10층 중 3층"인지 표시할 때 쓴다.</summary>
        public IFloorPlanSource Floors => _floors;

        /// <summary>엔진이 캐스케이드 하드 캡에 걸렸을 때 알림을 받으려는 어댑터용.</summary>
        public SpinEngine Engine => _engine;

        public int Seed { get; }
        public int CurrentFloor { get; private set; }
        public int HighestFloorReached { get; private set; }

        /// <summary>지금 실려 있는 승객·부품. 층을 건너 살아남으며 무게와 규칙을 함께 바꾼다.</summary>
        public BuildLoadout Loadout => _loadout;

        /// <summary>직전 층 도착에서 내린 승객. HUD와 사고 기록기가 읽는다.</summary>
        public IReadOnlyList<BuildItem> LastDeparted => _lastDeparted;

        /// <summary>층별 상승 정산 기록. 클램프 뒤 실제 층수와 실제 지급액이 담긴다.</summary>
        public IReadOnlyList<FloorAscent> Ascents => _ascents;

        /// <summary>기본 무게 + 적재 무게. 요구 전력과 위험 점수가 이 값을 본다.</summary>
        public float CarriedWeight => _baseWeight + _loadout.TotalWeight;

        /// <summary>
        /// 런 전체에서 가장 무거웠던 순간. 완주 시점의 적재는 증거가 되지 못한다 —
        /// 승객이 목적지 층에서 내리므로 10층에 닿으면 칸이 비어 있는 것이 정상이다.
        /// "적재 경로를 실제로 지났는가"를 물으려면 최고치를 봐야 한다.
        /// </summary>
        public float PeakCarriedWeight { get; private set; }

        /// <summary>허용 중량(짐꾼 보너스 포함). 넘으면 과적이다.</summary>
        public float WeightCapacity => _weight.CapacityWith(_loadout.TotalCapacityBonus);

        /// <summary>과적 수치의 출처. 하네스가 「에셋이 읽혔는가」를 이걸로 묻는다.</summary>
        public WeightSnapshot Weight => _weight;

        /// <summary>스핀 밸런스 수치의 출처와 값.</summary>
        public SpinBalanceSnapshot Balance => _balance;

        public bool IsOverloaded => CarriedWeight > WeightCapacity;
        public float Money { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsFailed { get; private set; }
        public string FailureReason { get; private set; }

        /// <summary>가장 최근 층에서 화물 포기로 무엇을 잃었는가. 없었으면 null.</summary>
        public string LastJettison { get; private set; }

        /// <summary>
        /// 화물 포기 구간에서 소지금으로 낸 대가의 누계.
        ///
        /// 원장 검사(`소지금 = 지급 합계`)가 이 변경을 즉시 잡아냈다 — 원장이 모르는
        /// 지출을 만들었기 때문이다. 검사를 느슨하게 하는 대신 지출을 원장에 넣는다.
        /// 불변식은 `소지금 = 지급 합계 − 대가 합계`가 된다.
        /// </summary>
        public float TotalJettisonPenalty { get; private set; }
        public FloorSession Current => _current;
        public FloorSession Floor => _current;
        public IReadOnlyList<FloorResult> Results => _results;
        public ResidualState Residual => _residual;
        public RunResult Result => new RunResult(
            IsComplete && !IsFailed, IsComplete, IsFailed, HighestFloorReached,
            Money, CarriedWeight, FailureReason, _results);

        // ── 위험 판정 (`D-1`) ────────────────────────────────────────────────

        /// <summary>
        /// 지금 판정된 위험 단계. **위험 단계의 단일 원본이다** — 뷰·기록기·사운드·
        /// 텔레메트리가 전부 이 값을 읽고, 아무도 스스로 다시 판정하지 않는다.
        /// </summary>
        public Risk.RiskLevel RiskLevel => _riskEvaluator.Current;

        /// <summary>마지막 판정의 원점수. 계기판과 디버그 패널이 「왜」를 설명할 때 쓴다.</summary>
        public float RiskScore => _riskEvaluator.CurrentScore;

        /// <summary>왜 이 단계인지 한 줄. 판정과 같은 순간에 지어진 문장이다.</summary>
        public string RiskReason => _riskReason;

        /// <summary>
        /// 위험 임계값 9종이 어디서 왔는가 (`UP-TECH-09` ⑦). 아무도 주입하지 않았으면
        /// 「필드 초기값」이다 — 「코드 프리셋」과 구분된다.
        /// </summary>
        public string RiskThresholdSource => _riskEvaluator.ThresholdSource;

        /// <summary>
        /// 위험 임계값을 밖에서 갈아 끼운다. `RiskThresholdProfile.asset` 은 씬 컴포넌트가
        /// 들고 있으므로(그 슬롯을 옮기면 씬 배선을 다시 해야 한다) Unity 어댑터 쪽에서
        /// 스냅샷만 넘겨받는다. **값이 아니라 스냅샷 구조체를 받는다** — 이 파일이 지키는
        /// 「UnityEngine 비의존」이 유지되고, 주입 여부는 <see cref="RiskThresholdSource"/>
        /// 로 반증할 수 있다.
        ///
        /// 주입하지 않아도 게임은 돈다 — <see cref="Risk.RiskEvaluator"/> 의 필드 초기값이
        /// `RiskThresholdProfile` 의 코드 프리셋과 **같은 수**이기 때문이다(그 일치는
        /// `ProfileTests` 가 대조한다).
        /// </summary>
        public void ApplyRiskThresholds(in RiskThresholdSnapshot thresholds)
        {
            _riskEvaluator.Apply(thresholds);
            EvaluateRisk();
        }

        /// <summary>
        /// 지금 상태를 위험 판정의 입력으로 옮긴다. **검증·기록용으로 밖에서도 읽는다** —
        /// 「무엇을 보고 그 단계가 나왔는가」를 물을 수 없으면 판정이 반증되지 않는다.
        /// </summary>
        public Risk.RiskInputs CurrentRiskInputs => ReadRiskInputs();

        /// <summary>
        /// 위험 단계를 다시 판정한다. **상태가 바뀐 자리에서만 부른다** — 매 프레임
        /// 부르면 히스테리시스가 프레임 수에 의존하게 되고, 안 부르면 궤적의 중간값이
        /// 사라진다. 둘 다 「같은 시드가 다른 기록을 남긴다」로 끝난다.
        /// </summary>
        public Risk.RiskLevel EvaluateRisk()
        {
            Risk.RiskInputs inputs = ReadRiskInputs();
            Risk.RiskLevel level = _riskEvaluator.Evaluate(in inputs);
            _riskReason = _riskEvaluator.Explain(in inputs);
            return level;
        }

        private Risk.RiskInputs ReadRiskInputs()
        {
            FloorSession f = _current;
            if (f == null)
            {
                // 층이 없으면 런이 끝난 것이다. 실패로 끝났으면 그 상태를 유지한다.
                return new Risk.RiskInputs(0, 0, 0, false, 1, 1f, IsFailed);
            }

            ResidualState residual = f.Residual;
            float ratio = f.RequiredPower > 0f ? f.Power / f.RequiredPower : 1f;
            bool floorFailed = f.Result != null && !f.Result.Succeeded;

            return new Risk.RiskInputs(
                residual.AbsorberCount, residual.ProliferatorCount,
                f.ExtraSpinsTaken, f.IsOverloaded, f.SpinsRemaining, ratio, floorFailed);
        }

        // ── 상태 전이 ────────────────────────────────────────────────────────
        //
        // 아래 다섯은 전부 「층의 상태를 바꾸고 곧바로 다시 판정한다」는 같은 모양이다.
        // 판정을 호출부(뷰·브리지)에 맡기면 한 곳만 빠뜨려도 그 경로의 궤적이 조용히
        // 사라지고, 그 사실은 사고 기록에서만 드러난다.

        public bool SelectContract(int choiceIndex)
        {
            if (_current == null || !_current.SelectContract(choiceIndex)) return false;
            EvaluateRisk();
            return true;
        }

        /// <summary>
        /// 앤티를 내고 한 번 더 당긴다. **여기서 반드시 판정한다** — 과수확 카운터가
        /// 오르는 이 순간이 점수가 가장 높은 지점이고, `RouletteInteractionBridge` 는
        /// 곧바로 같은 프레임에 <see cref="Spin"/> 을 부른다. 여기를 건너뛰면 그 봉우리를
        /// 관측하는 프레임이 **존재하지 않는다.**
        /// </summary>
        public bool PushYourLuck()
        {
            if (_current == null || !_current.PushYourLuck()) return false;
            EvaluateRisk();
            return true;
        }

        public bool ContinueSpinning() => PushYourLuck();

        public SpinResolution Spin()
        {
            if (_current == null) return default(SpinResolution);
            SpinResolution resolution = _current.Spin();
            // Steps 가 null 이면 상태 게이트에 막힌 호출이라 아무것도 바뀌지 않았다.
            if (resolution.Steps != null) EvaluateRisk();
            return resolution;
        }

        public FloorResult Bank()
        {
            if (_current == null) return null;
            FloorResult result = _current.Bank();
            if (result == null) return null;

            // 확정 **직후**·층 교체 **전**. 실패한 층의 Collapse 가 여기서 잡힌다 —
            // `CompleteFloor` 가 `_current` 를 비우고 나면 그 층은 더 이상 물어볼 수 없다.
            EvaluateRisk();
            CompleteFloor(result);
            EvaluateRisk();
            return result;
        }

        public FloorResult ForceResolve()
        {
            if (_current == null) return null;
            FloorResult result = _current.ForceResolve();
            if (result == null) return null;

            EvaluateRisk();
            CompleteFloor(result);
            EvaluateRisk();
            return result;
        }

        /// <summary>적재 단계에서 후보 하나를 싣는다.</summary>
        public bool TakeBuildOffer(int index)
        {
            if (_current == null || !_current.TakeOffer(index)) return false;
            EvaluateRisk();   // 무게가 바뀌면 과적 판정이 바뀐다.
            return true;
        }

        /// <summary>문을 닫고 다음 단계로 넘어간다. 아무것도 싣지 않아도 진행된다.</summary>
        public bool FinishBoarding()
        {
            if (_current == null || !_current.FinishBoarding()) return false;
            EvaluateRisk();
            return true;
        }

        /// <summary>Updates the load before the next floor is created.</summary>
        public bool AddWeight(float amount)
        {
            if (amount < 0f || IsComplete || IsFailed) return false;
            _baseWeight += amount;
            // 이미 만들어진 층에도 알린다. 안 그러면 층이 옛 무게로 요구 전력을 들고 있고
            // `IsOverloaded`가 거짓으로 남아 위험 단계가 올라가지 않는다.
            _current?.RefreshLoad(_baseWeight);
            EvaluateRisk();
            return true;
        }

        public bool SetCarriedWeight(float weight)
        {
            if (weight < 0f || IsComplete || IsFailed) return false;
            _baseWeight = weight;
            _current?.RefreshLoad(_baseWeight);
            EvaluateRisk();
            return true;
        }

        public void AddMoney(float amount)
        {
            if (!IsComplete && !IsFailed) Money += amount;
        }

        private void CreateCurrentFloor()
        {
            if (CurrentFloor < _floors.FirstFloor || CurrentFloor > _floors.LastFloor)
            {
                IsComplete = true;
                _current = null;
                Events.Publish(GameEventKind.RunEnded, HighestFloorReached, -1,
                    HighestFloorReached, Money, "완주");
                return;
            }

            FloorPlan plan = _floors.For(CurrentFloor);
            // 기본 무게만 넘긴다. 적재 무게는 층이 `_loadout`에서 직접 읽는다 —
            // 적재 단계에서 무게가 바뀌면 요구 전력이 그 자리에서 갱신되어야 하기 때문이다.
            _current = new FloorSession(plan, _engine, _thresholds,
                _baseWeight, _residual, _overharvest, _weight, _balance, _settlement, _loadout)
            {
                Events = Events,
            };
            Events.Publish(GameEventKind.FloorStarted, CurrentFloor, -1,
                _current.Plan.Spins, _current.RequiredPower, _current.Plan.CoreQuestion);
        }

        /// <summary>
        /// 목적지에 닿은 승객을 내리고 요금을 받는다. 부품은 목적지가 없어 남는다.
        ///
        /// 층 도착 직후, 다음 층을 만들기 **전에** 호출한다. 순서가 뒤집히면 이미 내린
        /// 승객의 무게로 요구 전력을 계산하게 된다.
        /// </summary>
        private void DisembarkAt(int floor)
        {
            _lastDeparted.Clear();
            List<BuildItem> leaving = _loadout.TakeDeparting(floor, out float reward);
            if (leaving.Count == 0) return;

            _lastDeparted.AddRange(leaving);
            Money += reward;
        }

        private void CompleteFloor(FloorResult result)
        {
            _results.Add(result);
            _residual = _current.Residual;

            if (!result.Succeeded)
            {
                IsFailed = true;
                FailureReason = result.FailureReason;
                _current = null;
                Events.Publish(GameEventKind.RunEnded, HighestFloorReached, -1,
                    HighestFloorReached, Money, FailureReason);
                return;
            }

            // 70~89% 는 오르되 대가를 치른다. 무엇을 잃었는지 기록에 남긴다.
            if (result.RequiresJettison)
            {
                LastJettison = PayJettisonCost();
                Events.Publish(GameEventKind.JettisonPaid, CurrentFloor, -1,
                    _loadout.Count, _loadout.TotalWeight, LastJettison);
            }
            else LastJettison = null;

            int from = CurrentFloor;
            int ascended = ClampAscent(CurrentFloor, result.FloorsAscended);

            // 도달 층은 건물 높이를 넘지 않는다. 10층 건물에서 "13층 도달"은 보고서에
            // 넣을 수 없는 숫자다.
            HighestFloorReached = Math.Max(HighestFloorReached,
                Math.Min(_floors.LastFloor, CurrentFloor + ascended));
            CurrentFloor += ascended;

            // 도착한 층에서 내린다. 다음 층을 만들기 전에 해야 이미 내린 승객의 무게가
            // 요구 전력에 섞이지 않는다. 건물 밖으로 나간 런은 전원 하차로 본다.
            DisembarkAt(Math.Min(CurrentFloor, _floors.LastFloor));

            // 추가 층에 쓴 전력은 돈이 되지 않는다. 예전에는 잉여 전체를 돈으로 주면서
            // 동시에 그 잉여로 층까지 올랐다 — 같은 전력을 두 번 쓴 것이다
            // (`AscendResult.AllocateSurplus`가 원래 막으려던 지점).
            float spentOnExtraFloors = Math.Max(0, ascended - result.Ascent.BaseFloors) *
                                       result.Ascent.PowerPerExtraFloor;
            float credited = Math.Max(0f, result.ExcessPower - spentOnExtraFloors);
            Money += credited;

            // 남은 스핀 **운행 효율 정산** (`T-05` 2026-08-02).
            //
            // 초과 전력 환산과 **따로** 더한다. 정산은 전력이 아니라 「안 쓴 운행
            // 여유」의 대가이고, 초과 전력에 섞으면 위 `spentOnExtraFloors` 공제에
            // 걸려 추가 층을 오른 만큼 정산이 깎인다 — 두 값은 서로 다른 것을 갚는다.
            //
            // 과수확을 고른 층에서는 `result.SettlementMoney` 가 0 이므로
            // 여기서 다시 판정하지 않는다. 조건을 두 곳에 두면 반드시 갈라진다.
            Money += result.SettlementMoney;
            _ascents.Add(new FloorAscent(from, ascended, result.ExcessPower,
                result.Ascent.PowerPerExtraFloor, credited,
                result.SettlementMoney, result.SettledSpins));
            CreateCurrentFloor();
        }

        /// <summary>
        /// 요구 전력의 70~89% 로 오를 때 치르는 대가. 무엇을 잃었는지 문장으로 남긴다.
        ///
        /// **기본값이며 교체 대상이다** (`ASSUMPTION_LOG` A-20260731-06).
        /// `MASTER_PRD`도 `PowerBand` 주석도 "승객·화물 포기 또는 큰 대가"까지만 말하고
        /// 무엇을 얼마나 빼앗는지는 정하지 않았다. 정해지지 않았다고 비워 두면
        /// 화면이 "화물 포기"라고 말한 뒤 아무 일도 일어나지 않는다 — 그것이
        /// 독립 감사가 잡아낸 상태다. 규칙이 없는 것보다 바꿀 수 있는 규칙이 낫다.
        ///
        /// 규칙: 실은 것이 있으면 **가장 무거운 것부터** 버려서 적재 무게의 절반
        /// 이상을 덜어낸다(최소 한 개). 실은 것이 없으면 소지금의 40% 를 잃는다.
        /// 둘 다 없으면 잃을 것이 없으므로 그대로 오른다.
        /// </summary>
        private string PayJettisonCost()
        {
            if (_loadout != null && _loadout.Count > 0)
            {
                float before = _loadout.TotalWeight;
                float target = before * 0.5f;
                var dropped = new List<string>();

                while (_loadout.Count > 0 &&
                       (dropped.Count == 0 || _loadout.TotalWeight > target))
                {
                    BuildItem heaviest = null;
                    foreach (BuildItem item in _loadout.Items)
                        if (heaviest == null || item.Weight > heaviest.Weight) heaviest = item;
                    if (heaviest == null) break;

                    if (!_loadout.Remove(heaviest.Id)) break;
                    dropped.Add(heaviest.Label);
                }

                float lost = before - _loadout.TotalWeight;
                return $"화물 포기 — {string.Join(", ", dropped)} ({lost:F0}kg)";
            }

            if (Money > 0f)
            {
                float penalty = Money * 0.4f;
                Money -= penalty;
                TotalJettisonPenalty += penalty;
                return $"대가 지불 — 소지금 {penalty:F0} (실은 것이 없었다)";
            }

            return "포기할 것도 낼 것도 없었다";
        }

        /// <summary>
        /// **지금 확정하면 몇 층 오르는가.** 계기탑(`AscentColumnView`)이 그리는 값이다.
        ///
        /// 사용자 지시(2026-08-04): 「전력량 쪽에 지금 전력이면 몇 층을 오를 수 있는지도
        /// 나와야 함」. 퍼센트보다 이쪽이 플레이어가 실제로 판단하는 단위다.
        ///
        /// ⚠ **화면과 실제 상승이 어긋나면 그게 최악이다.** 그래서 새 식을 쓰지 않고
        /// 실제 판정이 쓰는 둘을 **그대로** 부른다 —
        ///   ① <see cref="FloorSession.PreviewAscent"/> = `Resolve()` 와 같은 한 줄
        ///   ② <see cref="ClampAscent"/> = `CompleteFloor()` 가 부르는 그 함수
        /// 즉 이 값이 틀리면 게임도 같이 틀린다. 갈라질 수 없는 구조다.
        ///
        /// 상승하지 못하는 구간(`Crash`)에서는 0 이다 — `BaseFloors` 가 0 이라
        /// 자연히 그렇게 되고, 여기서 따로 분기하지 않는다.
        /// </summary>
        public int PreviewFloorsGained()
        {
            FloorSession floor = Current;
            if (floor == null) return 0;
            return Clamped(floor.PreviewAscent().FloorsAscended);
        }

        /// <summary>
        /// 임의의 전력·요구로 같은 값을 낸다. **검증과 캡처 스윕 전용.**
        /// 「표시한 층수가 실제 상승과 같은가」를 여러 전력값에서 대조할 수 있어야 한다.
        /// </summary>
        public int PreviewFloorsGained(float power, float required)
        {
            FloorSession floor = Current;
            if (floor == null) return 0;
            return Clamped(floor.PreviewAscent(power, required).FloorsAscended);
        }

        private int Clamped(int raw) => raw <= 0 ? 0 : ClampAscent(CurrentFloor, raw);

        /// <summary>
        /// 다층 상승이 삼켜서는 안 되는 층 앞에서 멈춘다.
        ///
        /// 자동 다층 상승은 높은 임계점의 보상이지만, 그대로 두면 커리큘럼을 지운다.
        /// 실측(시드 1337·4242)에서 1→2→3→4→**8**→9로 뛰어 5·6·7층을 통째로 건너뛰었고,
        /// 5개 시드 중 2개가 최종 층인 10층을 치르지 않고 런을 끝냈다. 가르치는 층과
        /// 종합 시험을 건너뛴 완주는 "10층까지 진행했다"의 증거가 되지 못한다.
        ///
        /// 세 가지를 막는다. 그 외의 건너뛰기는 보상으로 남긴다.
        ///   1) 최종 층 — 종합 시험은 반드시 치른다.
        ///   2) 빌드 보상 층 — 승객·부품을 얻는 유일한 지점이라 건너뛰면 빌드가 성립하지 않는다.
        ///   3) `MustBePlayed` 층 — 그 층에서만 소개되는 규칙이 있다.
        ///
        /// 3)이 뒤늦게 붙은 이유는 1)·2)만으로 부족하다는 것이 **측정으로** 드러났기 때문이다.
        /// 커리큘럼 재배치 직후 시드 200개 실측에서 4층(계약 첫 등장) 방문률이 34%,
        /// 3층 62% · 7층 54% · 9층 57% 였다(`Logs/curriculum_coverage.txt`).
        /// 완주율은 67%로 멀쩡했다 — "10층까지 갔다"가 "가르칠 것을 가르쳤다"를
        /// 전혀 보장하지 않는다는 뜻이다. 이번 목표는 "1층부터 10층까지 **연속** 플레이"다.
        ///
        /// 보상은 사라지지 않는다. 아래 호출자가 추가 층에 쓰지 않은 잉여를 전부 돈으로
        /// 지급하므로, 잘린 상승은 그만큼 소지금이 된다. 층 대신 돈으로 갚는 것이지
        /// 높은 전력이 무의미해지는 것이 아니다.
        /// </summary>
        private int ClampAscent(int from, int floorsAscended)
        {
            if (floorsAscended <= 1) return floorsAscended;

            // 최종 층에서의 상승은 런 종료다. 여기서 자르면 층을 벗어나지 못해
            // 같은 층이 무한히 다시 생성된다.
            if (from >= _floors.LastFloor) return floorsAscended;

            int target = Math.Min(from + floorsAscended, _floors.LastFloor);
            for (int floor = from + 1; floor < target; floor++)
            {
                FloorPlan plan = _floors.For(floor);
                if (plan.OffersBuildReward || plan.MustBePlayed)
                {
                    target = floor;
                    break;
                }
            }
            return Math.Max(1, target - from);
        }
    }
}
