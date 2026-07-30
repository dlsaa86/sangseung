using System;
using System.Collections.Generic;
using Ascend.Prototype.Build;
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
        private readonly float _baseWeight;
        private BuildItem[] _offers = Array.Empty<BuildItem>();
        private float _carriedWeight;
        private float _requiredPower;
        private SpinRuleSet _rules;
        private ResistanceContract _contract;
        private ResidualState _residual;
        private FloorResult _result;
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

        /// <summary>기본 허용 중량 + 짐꾼 보너스. 이 값을 넘으면 과적이다.</summary>
        public float Capacity => AllowedWeight + (_loadout != null ? _loadout.TotalCapacityBonus : 0f);

        public bool IsOverloaded => _carriedWeight > Capacity;

        /// <summary>지금 이 층에 실려 있는 것. 층이 소유하지 않는다 — 런의 것을 빌려 본다.</summary>
        public BuildLoadout Loadout => _loadout;

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
            return true;
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

            if (CanBank || SpinsRemaining == 0)
                Phase = FloorPhase.Decision;
            return resolution;
        }

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
            AscendResult ascent = AscendResult.Calculate(Power, RequiredPower, _thresholds);
            _result = new FloorResult(ascent, _totalAnte, ExtraSpinsTaken,
                _extraSpinNetPower, NetProfit);
            Phase = FloorPhase.Resolved;
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
