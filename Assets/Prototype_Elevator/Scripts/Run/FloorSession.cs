using System;
using System.Collections.Generic;
using Ascend.Prototype.Build;
using Ascend.Prototype.Events;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// Pure-C# state machine for one floor. It owns boarding, contract selection,
    /// spins, residual carry-over, the push-your-luck decision, and final ascent data.
    /// </summary>
    public sealed class FloorSession
    {
        // These are the defaults used by PrototypeConfig, kept here so the new
        // headless loop does not take a dependency on UnityEngine/ScriptableObject.
        public const float WeightPowerFactor = 2f;

        /// <summary>짐꾼 계열의 보너스를 더하기 전 기본 허용 중량.</summary>
        public const float AllowedWeight = 100f;
        public const float OverloadRequiredPowerMultiplier = 1.5f;
        public const float DefaultAnteRatio = 0.12f;
        public const float DefaultAnteEscalation = 0.35f;

        /// <summary>적재 층에서 한 번에 제시하는 후보 수.</summary>
        public const int BuildOfferCount = 3;

        private readonly SpinEngine _engine;
        private readonly PowerThresholds _thresholds;
        private readonly List<SpinResolution> _history = new List<SpinResolution>();
        private readonly BuildLoadout _loadout;
        private float _baseWeight;
        private BuildItem[] _offers = Array.Empty<BuildItem>();
        private float _carriedWeight;
        private float _requiredPower;
        private SpinRuleSet _rules;
        private ResistanceContract _contract;
        private ResidualState _residual;
        private FloorResult _result;
        private float _resolvedCapacity;
        private readonly float _anteRatio;
        private readonly float _anteEscalation;
        private float _totalAnte;
        private float _extraSpinNetPower;
        private float _lastAnte;
        private bool _activeSpinIsExtra;

        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight)
            : this(plan, engine, thresholds, carriedWeight, ResidualState.Empty)
        {
        }

        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual)
            : this(plan, engine, thresholds, carriedWeight, carriedResidual,
                DefaultAnteRatio, DefaultAnteEscalation)
        {
        }

        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight,
            float anteRatio, float anteEscalation)
            : this(plan, engine, thresholds, carriedWeight, ResidualState.Empty,
                anteRatio, anteEscalation)
        {
        }

        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual,
            float anteRatio, float anteEscalation)
            : this(plan, engine, thresholds, carriedWeight, carriedResidual,
                anteRatio, anteEscalation, null)
        {
        }

        /// <summary>
        /// <paramref name="loadout"/>는 런이 소유하며 층을 건너 살아남는다. null이면
        /// 적재 없는 층으로 동작한다(Phase 1 Hero Slice 경로가 이렇다).
        /// </summary>
        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual,
            float anteRatio, float anteEscalation, BuildLoadout loadout)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (plan.Spins <= 0) throw new ArgumentOutOfRangeException(nameof(plan), "A floor needs at least one spin.");

            Plan = plan;
            _engine = engine;
            _thresholds = thresholds;
            _loadout = loadout;
            _baseWeight = Math.Max(0f, carriedWeight);
            // 앞선 층에서 실은 것이 그대로 따라온다. 여기서 기본 무게만 쓰면 2층 이후
            // 적재가 요구 전력에 반영되지 않는다.
            RecomputeLoad();
            _residual = carriedResidual;
            _anteRatio = Math.Max(0f, anteRatio);
            _anteEscalation = Math.Max(0f, anteEscalation);
            _contract = ResistanceContract.None;
            _rules = null;

            // 적재가 요구 전력과 허용 중량을 바꾸므로 계약보다 먼저 끝나야 한다.
            if (plan.OffersBuildReward && _loadout != null)
            {
                _offers = BuildCatalog.OffersFor(engine.RunSeed, plan.Floor, _loadout, BuildOfferCount);
                if (_offers.Length > 0)
                {
                    Phase = FloorPhase.Boarding;
                    return;
                }
            }

            Phase = plan.ContractChoices != null && plan.ContractChoices.Length > 0
                ? FloorPhase.ContractSelection : FloorPhase.Spinning;

            if (Phase == FloorPhase.Spinning)
                BuildRules(_contract);
        }

        public FloorPlan Plan { get; }
        public FloorPhase Phase { get; private set; }
        public float CarriedWeight => _carriedWeight;

        /// <summary>
        /// 사건을 내보낼 곳. 런이 넣어 준다. null이면 아무 데도 보내지 않는다 —
        /// 헤드리스 단위 테스트는 버스 없이 층 하나만 돌릴 수 있어야 한다.
        /// </summary>
        public GameEventBus Events { get; set; }

        /// <summary>
        /// 이미 알린 임계점(%). 같은 층에서 100%를 두 번 알리지 않는다.
        /// 잔류 피해로 전력이 내려갔다 다시 올라가는 일이 실제로 있기 때문에
        /// "지금 넘었는가"가 아니라 "한 번이라도 넘었는가"로 판정한다.
        /// </summary>
        private int _announcedThreshold;

        /// <summary>
        /// 기본 허용 중량 + 짐꾼 보너스. 이 값을 넘으면 과적이다.
        ///
        /// **확정 뒤에는 확정 시점 값으로 얼어붙는다.** 계산 프로퍼티였을 때 무슨 일이
        /// 있었나: `_carriedWeight`와 `_requiredPower`는 확정 시 얼리는데(`RunSession`의
        /// `OnLoadoutChanged` 가드) 이 프로퍼티만 런이 소유한 살아 있는 `_loadout`을
        /// 매번 다시 읽었다. `CompleteFloor`가 화물 포기와 하차를 실행한 **뒤에**
        /// 사고 기록기가 한 프레임 늦게 기록하므로, 떠난 층의 레코드에 얼어붙은 무게와
        /// 하차 이후의 허용 중량이 한 줄에 섞여 들어갔다.
        /// </summary>
        public float Capacity => _result != null
            ? _resolvedCapacity
            : AllowedWeight + (_loadout != null ? _loadout.TotalCapacityBonus : 0f);

        public bool IsOverloaded => _carriedWeight > Capacity;

        /// <summary>지금 이 층에 실려 있는 것. 층이 소유하지 않는다 — 런의 것을 빌려 본다.</summary>
        public BuildLoadout Loadout => _loadout;

        /// <summary>
        /// 확정 시점의 적재 목록 문자열. `Loadout`은 런의 것이라 하차·포기 뒤에 달라진다.
        /// 기록은 **그 층에 실제로 실려 있던 것**을 적어야 한다.
        /// null 이면 아직 확정 전이므로 호출자가 `Loadout`을 직접 읽는다.
        /// </summary>
        public string ResolvedLoadoutShort { get; private set; }
        public string ResolvedLoadoutDetail { get; private set; }

        /// <summary>적재 단계에서 제시된 후보. 다른 단계에서는 비어 있다.</summary>
        public IReadOnlyList<BuildItem> BuildOffers => _offers;

        /// <summary>
        /// 후보 하나를 싣는다. 자리가 없거나 이미 실었으면 거부한다.
        /// 싣는 즉시 무게와 요구 전력이 갱신된다 — 문을 닫기 전에 대가를 보여줘야 선택이 된다.
        /// </summary>
        public bool TakeOffer(int index)
        {
            if (Phase != FloorPhase.Boarding || _loadout == null) return false;
            if (index < 0 || index >= _offers.Length) return false;

            BuildItem item = _offers[index];
            if (item == null || !_loadout.Add(item)) return false;

            var remaining = new List<BuildItem>(_offers.Length - 1);
            for (int i = 0; i < _offers.Length; i++)
                if (i != index) remaining.Add(_offers[i]);
            _offers = remaining.ToArray();

            RecomputeLoad();
            Events?.Publish(GameEventKind.ItemBoarded, Plan.Floor, -1,
                _loadout.Count, item.Weight, item.Label);
            return true;
        }

        /// <summary>문을 닫는다. 아무것도 싣지 않아도 진행할 수 있어야 한다(진행 불가 방지).</summary>
        public bool FinishBoarding()
        {
            if (Phase != FloorPhase.Boarding) return false;

            _offers = Array.Empty<BuildItem>();
            RecomputeLoad();
            Phase = Plan.ContractChoices != null && Plan.ContractChoices.Length > 0
                ? FloorPhase.ContractSelection : FloorPhase.Spinning;

            if (Phase == FloorPhase.Spinning)
                BuildRules(_contract);

            Events?.Publish(GameEventKind.BoardingFinished, Plan.Floor, -1,
                _loadout != null ? _loadout.Count : 0, _carriedWeight,
                IsOverloaded ? "과적" : null);
            return true;
        }

        /// <summary>
        /// 적재가 층 바깥에서 바뀌었을 때 무게와 요구 전력을 다시 계산한다.
        ///
        /// 필요한 이유: `_carriedWeight`와 `_requiredPower`는 층이 만들어질 때 한 번,
        /// 그리고 적재 단계에서 갱신된다. `RunSession.AddWeight`처럼 층이 이미 존재하는
        /// 상태에서 무게가 바뀌면 층은 그 사실을 모른 채 옛 요구 전력을 들고 있고,
        /// `IsOverloaded`도 거짓으로 남는다. 실제로 캡처 리그가 과적 218/130 상태에서
        /// 위험 단계 Stable을 찍었다 — 무게는 늘었는데 층이 모르고 있었다.
        /// </summary>
        /// <param name="baseWeight">
        /// 런이 들고 있는 기본 무게. 층의 사본이 아니라 **런의 현재 값**을 받아야 한다 —
        /// 인자 없이 자기 사본으로 다시 계산하면 아무것도 바뀌지 않는다.
        /// 처음에 인자 없는 형태로 만들었다가 테스트가 "층 무게가 0 (기대 200)"으로 잡았다.
        /// </param>
        public void RefreshLoad(float baseWeight)
        {
            _baseWeight = Math.Max(0f, baseWeight);
            RecomputeLoad();
        }

        private void RecomputeLoad()
        {
            _carriedWeight = _baseWeight + (_loadout != null ? _loadout.TotalWeight : 0f);
            _requiredPower = ComputeRequiredPower(Plan, _carriedWeight, Capacity);
        }
        public float Power { get; private set; }
        public float RequiredPower => _requiredPower;
        public int SpinsUsed { get; private set; }
        public int SpinsRemaining => Math.Max(0, Plan.Spins - SpinsUsed);
        public ResidualState Residual => _residual;
        public PowerBand CurrentBand => _thresholds.BandFor(Power, RequiredPower);
        public bool CanBank => Power >= RequiredPower;
        public float AnteRatio => _anteRatio;
        public float AnteEscalation => _anteEscalation;
        public int ExtraSpinsTaken { get; private set; }
        public float PendingAnte => CanBank && Phase == FloorPhase.Decision && SpinsRemaining > 0
            ? Power * AnteRatioForNextSpin : 0f;
        public float TotalAnte => _totalAnte;
        public float TotalStakedPower => _totalAnte;
        public float ExtraSpinNetPower => _extraSpinNetPower;
        public float NetProfit => _extraSpinNetPower - _totalAnte;
        public float LastAnte => _lastAnte;
        public ResistanceContract SelectedContract => _contract;
        public SpinRuleSet Rules => _rules;
        public IReadOnlyList<SpinResolution> History => _history;
        public FloorResult Result => _result;

        public bool SelectContract(int choiceIndex)
        {
            if (Phase != FloorPhase.ContractSelection) return false;
            if (Plan.ContractChoices == null || choiceIndex < 0 ||
                choiceIndex >= Plan.ContractChoices.Length) return false;

            _contract = Plan.ContractChoices[choiceIndex];
            BuildRules(_contract);
            Phase = FloorPhase.Spinning;
            Events?.Publish(GameEventKind.ContractSelected, Plan.Floor, -1,
                choiceIndex, 0f, _contract.Label);
            return true;
        }

        /// <summary>
        /// Re-enters spinning after the player declines to bank. This explicit
        /// transition keeps Decision meaningful while preserving a state-gated Spin().
        /// </summary>
        public bool PushYourLuck()
        {
            if (Phase != FloorPhase.Decision || !CanBank || SpinsRemaining <= 0 || _result != null)
                return false;

            // The ante is paid at choice time, before the engine consumes another
            // random result. PendingAnte is therefore the exact amount removed here.
            float ante = PendingAnte;
            Power -= ante;
            _totalAnte += ante;
            _lastAnte = ante;
            ExtraSpinsTaken++;
            _activeSpinIsExtra = true;
            Phase = FloorPhase.Spinning;
            Events?.Publish(GameEventKind.OverharvestPulled, Plan.Floor, SpinsUsed,
                ExtraSpinsTaken, ante);
            Events?.Publish(GameEventKind.ExtraSpinTaken, Plan.Floor, SpinsUsed,
                ExtraSpinsTaken, Power);
            return true;
        }

        public bool ContinueSpinning() => PushYourLuck();

        public SpinResolution Spin()
        {
            // A rejected action is represented by default(SpinResolution), never an
            // exception; callers can also use TrySpin when they need the bool.
            if (Phase != FloorPhase.Spinning || SpinsRemaining <= 0 || _rules == null)
                return default(SpinResolution);

            // 시드는 순차 스트림이 아니라 (런 시드, 층, 스핀 인덱스) 좌표에서 파생한다.
            // 그래야 "이 층 이 스핀"을 앞선 진행과 무관하게 단독 재현할 수 있다 —
            // TECH_SPEC §7, 파생 규칙의 단일 출처는 SpinSeed다.
            int spinSeed = SpinSeed.Derive(_engine.RunSeed, Plan.Floor, SpinsUsed);
            Events?.Publish(GameEventKind.SpinStarted, Plan.Floor, SpinsUsed,
                SpinsRemaining, Power, _activeSpinIsExtra ? "추가 스핀" : null);

            SpinResolution resolution = _engine.SpinWithSeed(
                spinSeed, _rules, in _contract, in _residual, Plan.Floor, SpinsUsed);
            _history.Add(resolution);
            _residual = resolution.Residual;
            Power += resolution.NetPower;
            if (_activeSpinIsExtra)
            {
                _extraSpinNetPower += resolution.NetPower;
                _activeSpinIsExtra = false;
            }
            SpinsUsed++;

            PublishSpinDetail(in resolution);

            if (CanBank || SpinsRemaining == 0)
                Phase = FloorPhase.Decision;
            return resolution;
        }

        /// <summary>
        /// 스핀 하나를 사건으로 쪼갠다. 사운드·승객 반응·텔레메트리가 이 흐름만 보고
        /// 각자 반응하며, 아무도 <see cref="SpinResolution"/>을 다시 해석하지 않는다.
        ///
        /// 순서가 곧 연출 순서다 — 열 공개 → 단계별(영혼·정화) → 잔류 → 임계점 → 종합.
        /// `MASTER_PRD.md` §6이 "시각적으로 생략하지 않는다"를 요구하므로 단계를 접지 않는다.
        /// </summary>
        private void PublishSpinDetail(in SpinResolution resolution)
        {
            GameEventBus bus = Events;
            if (bus == null) return;

            int floor = Plan.Floor;
            int idx = resolution.SpinIndex;

            for (int column = 0; column < SpinBoard.Columns; column++)
                bus.Publish(GameEventKind.ColumnRevealed, floor, idx, column);

            if (resolution.Steps != null)
            {
                foreach (CascadeStep step in resolution.Steps)
                {
                    if (step.NormalSoulsHarvested > 0)
                        bus.Publish(GameEventKind.NormalSoulHarvested, floor, idx,
                            step.NormalSoulsHarvested, step.NormalSoulPower);

                    if (step.Purifies != null)
                    {
                        foreach (PurifyEvent p in step.Purifies)
                        {
                            GameEventKind kind;
                            switch (p.Pattern)
                            {
                                case PatternKind.Line:      kind = GameEventKind.PurifyLine; break;
                                case PatternKind.Cluster:
                                case PatternKind.FullBoard: kind = GameEventKind.PurifyCluster; break;
                                default:                    kind = GameEventKind.PurifyScattered; break;
                            }
                            bus.Publish(kind, floor, idx,
                                p.Cells != null ? p.Cells.Length : 0, p.Power, p.Kind.DisplayName());
                        }
                    }

                    bus.Publish(GameEventKind.CascadeStep, floor, idx, step.Depth, step.StepPower);
                }
            }

            if (resolution.CascadeCapReached)
                bus.Publish(GameEventKind.CascadeCapReached, floor, idx, resolution.ChainDepth);

            if (resolution.Residual.StoredPowerLoss > 0f)
                bus.Publish(GameEventKind.ResidualDamage, floor, idx,
                    resolution.Residual.AbsorberCount, resolution.Residual.StoredPowerLoss,
                    resolution.Residual.Describe());

            AnnounceThresholds();

            bus.Publish(GameEventKind.SpinResolved, floor, idx,
                resolution.ChainDepth, resolution.NetPower, null, resolution);
        }

        /// <summary>
        /// 임계점 100 · 170 · 300%를 처음 넘는 순간에만 알린다.
        /// `MASTER_PRD.md` §7이 요구하는 과수확 잠금 해제도 100% 통과와 같은 사건이다.
        /// </summary>
        private void AnnounceThresholds()
        {
            if (RequiredPower <= 0f) return;
            int percent = (int)(Power / RequiredPower * 100f);

            foreach (int gate in ThresholdGates)
            {
                if (percent < gate || _announcedThreshold >= gate) continue;
                _announcedThreshold = gate;
                Events?.Publish(GameEventKind.PowerThresholdCrossed, Plan.Floor, SpinsUsed - 1,
                    gate, Power);
                if (gate == 100)
                    Events?.Publish(GameEventKind.OverharvestUnlocked, Plan.Floor, SpinsUsed - 1,
                        100, Power);
            }
        }

        /// <summary>승객이 반응하는 임계점(`MASTER_PRD.md` §9.2 목록의 세 지점).</summary>
        private static readonly int[] ThresholdGates = { 100, 170, 300 };

        private float AnteRatioForNextSpin =>
            AnteRatio * (1f + AnteEscalation * ExtraSpinsTaken);

        public bool TrySpin(out SpinResolution resolution)
        {
            if (Phase != FloorPhase.Spinning || SpinsRemaining <= 0 || _rules == null)
            {
                resolution = default(SpinResolution);
                return false;
            }
            resolution = Spin();
            return true;
        }

        public FloorResult Bank()
        {
            if (Phase != FloorPhase.Decision || !CanBank)
                return null;
            return Resolve();
        }

        public FloorResult ForceResolve()
        {
            if (Phase != FloorPhase.Decision || SpinsRemaining > 0)
                return null;
            return Resolve();
        }

        private FloorResult Resolve()
        {
            if (_result != null) return _result;

            // 확정 순간의 적재 상태를 뜬다. `_result` 를 세우기 **전에** 떠야 한다 —
            // `Capacity` 가 `_result != null` 로 분기하므로 순서를 뒤집으면
            // 아직 채우지 않은 `_resolvedCapacity`(0)를 읽는다.
            _resolvedCapacity = AllowedWeight + (_loadout != null ? _loadout.TotalCapacityBonus : 0f);
            ResolvedLoadoutShort = _loadout != null ? _loadout.DescribeShort() : null;
            ResolvedLoadoutDetail = _loadout != null ? _loadout.Describe() : null;

            AscendResult ascent = AscendResult.Calculate(Power, RequiredPower, _thresholds);
            _result = new FloorResult(ascent, _totalAnte, ExtraSpinsTaken,
                _extraSpinNetPower, NetProfit);
            Phase = FloorPhase.Resolved;

            Events?.Publish(GameEventKind.PowerBanked, Plan.Floor, SpinsUsed - 1,
                (int)_result.Band, Power, _result.Band.DisplayName());
            if (!_result.Succeeded)
                Events?.Publish(GameEventKind.CollapseBegan, Plan.Floor, SpinsUsed - 1,
                    (int)_result.Band, Power, _result.FailureReason);
            Events?.Publish(GameEventKind.FloorResolved, Plan.Floor, SpinsUsed - 1,
                _result.FloorsAscended, Power, _result.ToString());
            return _result;
        }

        private void BuildRules(in ResistanceContract contract)
        {
            // FloorPlan owns pool filtering and resistance scaling. Keep this as
            // the single runtime call site, then apply the selected contract once.
            FloorPlan plan = Plan;
            _rules = PrototypeCurriculum.BuildRules(in plan);
            _rules.Apply(in contract);

            // 발동 순서: 기본값 → 층 규칙 → 계약 → 승객·부품(`SpinRuleSet` 주석).
            // 승객이 계약보다 뒤인 이유는 계약의 곱셈이 승객의 가산 위에 얹히면
            // 같은 조합이 다른 값을 내기 때문이다.
            _loadout?.ApplyTo(_rules);
        }

        private static float ComputeRequiredPower(in FloorPlan plan, float weight, float capacity)
        {
            float required = plan.RequiredPower + weight * WeightPowerFactor;
            if (weight > capacity)
                required *= OverloadRequiredPowerMultiplier;
            return required;
        }
    }
}
