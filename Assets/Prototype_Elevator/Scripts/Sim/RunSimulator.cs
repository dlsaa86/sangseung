using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Headless replay of the full floor loop — no scene, no MonoBehaviour, no visuals.
    ///
    /// Every balance formula is delegated to FloorMath / CombinationResolver.DetermineType /
    /// CombinationConfig / EffectPipeline. The simulator deliberately owns no formula of its own:
    /// the moment it computes required power or combination score itself, the numbers it reports
    /// stop describing the game that actually ships.
    ///
    /// The turn order below mirrors RunController exactly:
    ///   FloorArrival -> PassengerSelection -> GenerationTurn xN -> PowerResolution
    ///   -> OverchargeAllocation -> Ascending
    /// </summary>
    public class RunSimulator
    {
        private readonly PrototypeConfig _config;
        private readonly BallDatabase _ballDatabase;
        private readonly CombinationConfig _combinationConfig;
        private readonly EffectResolverSettings _effectSettings;
        private readonly List<PassengerDefinition> _passengerPool;

        public RunSimulator(
            PrototypeConfig config,
            BallDatabase ballDatabase,
            CombinationConfig combinationConfig,
            EffectResolverSettings effectSettings,
            IReadOnlyList<PassengerDefinition> passengerPool)
        {
            _config = config;
            _ballDatabase = ballDatabase;
            _combinationConfig = combinationConfig;
            _effectSettings = effectSettings;
            _passengerPool = new List<PassengerDefinition>();
            if (passengerPool != null)
                foreach (PassengerDefinition p in passengerPool)
                    if (p != null) _passengerPool.Add(p);
        }

        /// <summary>Runs one complete run and returns its full record.</summary>
        public SimRunRecord RunOnce(int seed, SimPolicy policy, int runIndex)
        {
            var record = new SimRunRecord
            {
                runIndex = runIndex,
                seed = seed,
                policyName = policy != null ? policy.name : "(none)",
                outcome = "InProgress",
                failureReason = string.Empty
            };

            if (_config == null || _ballDatabase == null || _combinationConfig == null)
            {
                record.outcome = "Failure";
                record.failureReason = "설정 에셋 누락 (config/ballDatabase/combinationConfig)";
                return record;
            }
            if (policy == null) policy = SimPolicy.Balanced();

            // Seed derivation must match the runtime systems exactly, otherwise the simulation
            // explores a different random space than play mode does for the same seed.
            var ballRng      = new System.Random(seed);
            var effectRng    = new SystemEffectRandom(unchecked(seed * 397 ^ 0x5EED));
            var passengerRng = new System.Random(unchecked(seed * 31 ^ 0x9A55));
            var accidentRng  = new System.Random(unchecked(seed * 17 ^ 0x2ACC));
            var policyRng    = new System.Random(unchecked(seed * 7 ^ 0x1B0D));

            var drawer   = new BallDrawer(_ballDatabase, ballRng);
            var pipeline = new EffectPipeline(_effectSettings, effectRng);

            var boarded = new List<PassengerDefinition>();
            float money = _config.startingMoney;
            float power = _config.startingPower;
            float bankedPower = 0f;
            float floorStartPower = _config.startingPower;
            int floor = 0;
            int highestFloor = 0;
            int retriesThisFloor = 0;
            int totalRetries = 0;
            int totalAccidents = 0;
            bool candidatesPending = true;
            var candidates = new List<PassengerDefinition>();

            int guard = 0;
            int guardLimit = _config.targetFloor * (_config.maxRetriesPerFloor + 2) + 20;

            while (true)
            {
                if (++guard > guardLimit)
                {
                    record.outcome = "Failure";
                    record.failureReason = "시뮬레이션 반복 상한 도달";
                    break;
                }

                var fr = new SimFloorRecord { floorIndex = floor, retries = retriesThisFloor };

                // ── FloorArrival: draw this floor's candidates (only on first entry, not on retry) ──
                if (candidatesPending)
                {
                    candidates = DrawCandidates(passengerRng);
                    candidatesPending = false;
                }
                fr.candidatesOffered = DescribeCandidates(candidates);

                // ── PassengerSelection ──
                fr.passengerBoarded = "-";
                if (candidates.Count > 0 && boarded.Count < _config.maxPassengerSlots)
                {
                    if (policyRng.NextDouble() < policy.boardChance)
                    {
                        int pick = policyRng.Next(candidates.Count);
                        PassengerDefinition chosen = candidates[pick];
                        float projectedWeight = _config.startingWeight + SumWeight(boarded) + chosen.weight;
                        float projectedAllowed = _config.allowedWeight + SumAllowedBonus(boarded) + chosen.allowedWeightBonus;

                        if (projectedWeight <= projectedAllowed * policy.weightCeilingRatio)
                        {
                            boarded.Add(chosen);
                            candidates.RemoveAt(pick);
                            fr.passengerBoarded = Label(chosen);
                        }
                    }
                }

                // ── Load recalculation (mirrors RunController.RecalculateLoad) ──
                float weight = _config.startingWeight + SumWeight(boarded);
                float allowed = _config.allowedWeight + SumAllowedBonus(boarded);
                bool overloaded = weight > allowed;
                float required = FloorMath.ComputeRequiredPower(_config, floor, weight, overloaded);
                float accidentChance = FloorMath.ComputeAccidentChance(_config, weight, allowed);
                List<EffectDefinition> activeEffects = CollectEffects(boarded);

                fr.totalWeight = weight;
                fr.allowedWeight = allowed;
                fr.overloaded = overloaded;
                fr.requiredPower = required;
                fr.accidentChance = accidentChance;

                // ── GenerationTurn x N ──
                for (int turn = 1; turn <= _config.generationsPerFloor; turn++)
                {
                    List<BallDefinition> balls = drawer.DrawMany(3);
                    bool perfectStop = policyRng.NextDouble() < policy.perfectStopChance;

                    var ctx = BuildContext(balls, overloaded, perfectStop, turn, floor);
                    float before = ctx.ComputeCurrentPower();
                    pipeline.Run(ctx, activeEffects);

                    power += ctx.FinalPower;
                    money += ctx.MoneyDelta;

                    fr.turns.Add(new SimTurnRecord
                    {
                        turnIndex = turn,
                        ball0 = Id(balls, 0), ball1 = Id(balls, 1), ball2 = Id(balls, 2),
                        grade0 = Grade(balls, 0), grade1 = Grade(balls, 1), grade2 = Grade(balls, 2),
                        perfectStop = perfectStop,
                        combination = ctx.Combination.ToString(),
                        powerBeforeEffects = before,
                        powerAfterEffects = ctx.FinalPower,
                        moneyDelta = ctx.MoneyDelta,
                        effectLog = FlattenLog(ctx.Log)
                    });
                }

                // ── PowerResolution: accident is rolled before the comparison ──
                if (accidentChance > 0f && accidentRng.NextDouble() < accidentChance)
                {
                    float loss = FloorMath.ComputeAccidentPowerLoss(_config, power);
                    power -= loss;
                    totalAccidents++;
                    fr.accidentOccurred = true;
                    fr.accidentLoss = loss;
                }

                fr.finalPower = power;
                float surplus = power - required;

                if (surplus < 0f)
                {
                    retriesThisFloor++;
                    totalRetries++;
                    fr.success = false;
                    fr.retries = retriesThisFloor;
                    fr.overchargeChoice = "-";
                    record.floors.Add(fr);

                    if (retriesThisFloor > _config.maxRetriesPerFloor)
                    {
                        record.outcome = "Failure";
                        record.failureReason = $"{floor}층에서 요구 전력 미달 (재시도 {_config.maxRetriesPerFloor}회 초과)";
                        break;
                    }

                    // Retry the same floor: power resets, candidates are NOT redrawn.
                    power = floorStartPower;
                    continue;
                }

                fr.success = true;
                fr.surplus = surplus;

                // ── OverchargeAllocation ──
                OverchargeOption chosenOption = policyRng.NextDouble() < policy.ascendChance
                    ? FloorMath.BuildAscendOption(_config, surplus)
                    : FloorMath.BuildMoneyOption(_config, surplus);

                int extraFloors = 0;
                if (chosenOption.Mode == OverchargeMode.Ascend)
                {
                    extraFloors = chosenOption.FloorsGained;
                    bankedPower = chosenOption.PowerCarried;
                }
                else
                {
                    money += chosenOption.MoneyGained;
                    bankedPower = 0f;
                }
                fr.overchargeChoice = chosenOption.Label;

                // ── Ascending ──
                int climb = 1 + extraFloors;
                fr.floorsClimbed = climb;
                record.floors.Add(fr);

                floorStartPower = _config.startingPower + bankedPower;
                power = floorStartPower;
                bankedPower = 0f;
                retriesThisFloor = 0;

                floor += climb;
                highestFloor = Mathf.Max(highestFloor, floor);
                candidatesPending = true;

                if (floor >= _config.targetFloor)
                {
                    record.outcome = "Success";
                    break;
                }
            }

            record.highestFloor = highestFloor;
            record.finalMoney = money;
            record.totalAccidents = totalAccidents;
            record.totalRetries = totalRetries;
            return record;
        }

        // ── Helpers ──

        /// <summary>
        /// Builds the pipeline context without a CombinationResolver MonoBehaviour, reusing the
        /// same DetermineType and CombinationConfig lookups the runtime path uses.
        /// </summary>
        private GenerationContext BuildContext(
            List<BallDefinition> balls, bool overloaded, bool perfectStop, int turn, int floor)
        {
            var ctx = new GenerationContext
            {
                Balls = new List<BallDefinition>(balls),
                IsOverloaded = overloaded,
                PerfectStop = perfectStop,
                TurnIndex = turn,
                FloorIndex = floor,
                CombinationMultiplier = 1f
            };

            ctx.Combination = ctx.Balls.Count >= 3
                ? CombinationResolver.DetermineType(ctx.Balls[0], ctx.Balls[1], ctx.Balls[2])
                : CombinationType.None;

            ctx.CombinationBaseScore = _combinationConfig.GetBaseScore(ctx.Combination);
            ctx.CombinationMultiplier = _combinationConfig.GetMultiplier(ctx.Combination);
            return ctx;
        }

        /// <summary>Draws candidates without replacement, mirroring PassengerManager.GenerateCandidates.</summary>
        private List<PassengerDefinition> DrawCandidates(System.Random rng)
        {
            var result = new List<PassengerDefinition>();
            if (_passengerPool.Count == 0) return result;

            var available = new List<PassengerDefinition>(_passengerPool);
            int target = Mathf.Min(Mathf.Max(0, _config.passengerCandidatesPerFloor), available.Count);
            for (int i = 0; i < target; i++)
            {
                int idx = rng.Next(available.Count);
                result.Add(available[idx]);
                available.RemoveAt(idx);
            }
            return result;
        }

        private static List<EffectDefinition> CollectEffects(List<PassengerDefinition> boarded)
        {
            var effects = new List<EffectDefinition>();
            foreach (PassengerDefinition p in boarded)
            {
                if (p == null || p.effects == null) continue;
                foreach (EffectDefinition e in p.effects)
                    if (e != null) effects.Add(e);
            }
            return effects;
        }

        private static float SumWeight(List<PassengerDefinition> boarded)
        {
            float sum = 0f;
            foreach (PassengerDefinition p in boarded) if (p != null) sum += p.weight;
            return sum;
        }

        private static float SumAllowedBonus(List<PassengerDefinition> boarded)
        {
            float sum = 0f;
            foreach (PassengerDefinition p in boarded) if (p != null) sum += p.allowedWeightBonus;
            return sum;
        }

        private static string Label(PassengerDefinition p)
            => p == null ? "-" : (string.IsNullOrEmpty(p.displayName) ? p.id : p.displayName);

        private static string DescribeCandidates(List<PassengerDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0) return "-";
            var sb = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Label(candidates[i])).Append("(무게").Append(candidates[i].weight.ToString("F0")).Append(')');
            }
            return sb.ToString();
        }

        private static string Id(List<BallDefinition> balls, int i)
            => (balls != null && i < balls.Count && balls[i] != null) ? balls[i].id : "-";

        private static string Grade(List<BallDefinition> balls, int i)
            => (balls != null && i < balls.Count && balls[i] != null) ? balls[i].grade.ToString() : "-";

        private static string FlattenLog(List<EffectLogEntry> log)
        {
            if (log == null || log.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (EffectLogEntry e in log)
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(e.ToDisplayString());
            }
            return sb.ToString();
        }
    }
}
