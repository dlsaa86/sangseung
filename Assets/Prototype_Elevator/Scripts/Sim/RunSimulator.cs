using System;
using System.Collections.Generic;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype
{
    /// <summary>
    /// Headless ten-floor balance simulation. This class intentionally has no Unity dependency;
    /// the only game-side operation it performs is calling the fixed SpinEngine contract.
    /// </summary>
    public sealed class RunSimulator
    {
        private readonly int _seed;

        public RunSimulator(int seed = 1337)
        {
            _seed = seed;
        }

        /// <summary>
        /// Source-compatible shim for retired editor callers. The new simulator is intentionally
        /// asset-free, so these values are ignored and the deterministic default seed is used.
        /// </summary>
        public RunSimulator(object config, object ballDatabase, object combinationConfig,
                            object effectSettings, object passengerPool)
        {
            _seed = 1337;
        }

        public SimReportData Simulate(int runCount = 1000)
        {
            if (runCount < 1) throw new ArgumentOutOfRangeException(nameof(runCount));

            var report = new SimReportData { runCount = runCount, seed = _seed };
            foreach (SimPolicy policy in SimPolicy.Defaults)
            {
                var policyReport = new SimPolicyReport { policyName = policy.name, runCount = runCount };
                for (int i = 0; i < runCount; i++)
                {
                    SimRunRecord run = RunOnce(_seed + i, policy, i);
                    policyReport.runs.Add(run);
                    Accumulate(policyReport, run);
                }
                FinalizeAverages(policyReport);
                report.policies.Add(policyReport);
            }

            SimReport.AddBalanceWarnings(report);
            return report;
        }

        /// <summary>Pure C# entry point for command-line tests or other non-Editor callers.</summary>
        public string RunReport(int runCount = 1000)
        {
            return SimReport.Format(Simulate(runCount));
        }

        public static string RunHeadless(int runCount = 1000, int seed = 1337)
        {
            return new RunSimulator(seed).RunReport(runCount);
        }

        public SimRunRecord RunOnce(int seed, SimPolicy policy, int runIndex)
        {
            if (policy == null) policy = SimPolicy.Balanced();

            var record = new SimRunRecord
            {
                runIndex = runIndex,
                seed = seed,
                policyName = policy.name,
                succeeded = true,
                failedFloor = 0,
                outcome = "InProgress",
                failureReason = string.Empty,
            };
            var engine = new SpinEngine(seed);
            ResidualState residual = ResidualState.Empty;
            float runPower = 0f;

            foreach (FloorPlan floor in PrototypeCurriculum.TenFloors)
            {
                // Each floor starts with a fresh board; residual is carried between spins
                // inside this floor only. The next floor's contract is selected fresh.
                residual = ResidualState.Empty;
                var floorRecord = new SimFloorRecord
                {
                    floor = floor.Floor,
                    floorIndex = floor.Floor,
                    requiredPower = floor.RequiredPower,
                    selectedContract = policy.ChooseContract(floor, in residual),
                    finalBand = PowerBand.Crash,
                    failureReason = string.Empty,
                };
                SpinRuleSet rules = PrototypeCurriculum.BuildRules(in floor);
                rules.Apply(in floorRecord.selectedContract);
                float floorPower = 0f;
                PowerBand previousBand = PowerThresholds.Default.BandFor(floorPower, floor.RequiredPower);
                int thresholdCrossings = 0;

                for (int spinIndex = 0; spinIndex < floor.Spins; spinIndex++)
                {
                    bool requiredAlreadyMet = floorPower >= floor.RequiredPower;
                    int spinsRemaining = floor.Spins - spinIndex - 1;
                    if (requiredAlreadyMet) floorRecord.additionalSpinDecisions++;
                    if (requiredAlreadyMet && !policy.ShouldTakeAdditionalSpin(
                            floorPower, floor.RequiredPower, spinsRemaining)) break;

                    bool additional = requiredAlreadyMet;
                    bool firstAdditional = additional && floorRecord.additionalSpinChoices == 0;
                    float ante = 0f;
                    if (additional)
                    {
                        ante = floorPower * 0.12f * (1f + 0.35f * floorRecord.additionalSpinChoices);
                        floorPower -= ante;
                        runPower -= ante;
                        floorRecord.totalAnte += ante;
                    }
                    SpinResolution resolution = engine.Spin(rules, in floorRecord.selectedContract, in residual);
                    SimSpinRecord spin = MeasureSpin(resolution, rules, spinIndex + 1, additional);
                    spin.additionalSpinAnte = ante;
                    spin.isFirstAdditionalSpin = firstAdditional;
                    floorRecord.spins.Add(spin);
                    floorPower += resolution.NetPower;
                    runPower += resolution.NetPower;
                    spin.cumulativePower = floorPower;

                    PowerBand currentBand = PowerThresholds.Default.BandFor(floorPower, floor.RequiredPower);
                    if (currentBand != previousBand) thresholdCrossings++;
                    previousBand = currentBand;
                    if (additional)
                    {
                        floorRecord.additionalSpinChoices++;
                        spin.additionalSpinNetAfterAnte = resolution.NetPower - ante;
                        spin.additionalSpinSucceeded = spin.additionalSpinNetAfterAnte >= 0f;
                        spin.additionalSpinLost = spin.additionalSpinNetAfterAnte < 0f;
                        if (spin.additionalSpinSucceeded) floorRecord.additionalSpinSuccesses++;
                        if (spin.additionalSpinLost) floorRecord.additionalSpinLosses++;
                        if (firstAdditional)
                        {
                            floorRecord.firstAdditionalSpinChoices++;
                            if (spin.additionalSpinSucceeded) floorRecord.firstAdditionalSpinSuccesses++;
                            if (spin.additionalSpinLost) floorRecord.firstAdditionalSpinLosses++;
                        }
                    }
                    residual = resolution.Residual;
                }

                floorRecord.totalWeight = TotalWeight(rules);
                floorRecord.finalPower = floorPower;
                floorRecord.finalBand = PowerThresholds.Default.BandFor(floorPower, floor.RequiredPower);
                floorRecord.passed = floorRecord.finalBand.Ascends();
                floorRecord.success = floorRecord.passed;
                floorRecord.surplus = floorPower - floor.RequiredPower;
                floorRecord.thresholdCrossings = thresholdCrossings;
                if (!floorRecord.passed)
                {
                    floorRecord.failureReason = floorRecord.finalBand.DisplayName();
                    record.succeeded = false;
                    record.failedFloor = floor.Floor;
                    record.outcome = "Failure";
                    record.failureReason = "" + floor.Floor + "층에서 " + floorRecord.failureReason;
                    record.highestFloor = floor.Floor - 1;
                    record.finalPower = runPower;
                    record.floors.Add(floorRecord);
                    return record;
                }
                record.floors.Add(floorRecord);
            }

            record.finalPower = runPower;
            record.outcome = "Success";
            record.highestFloor = PrototypeCurriculum.TenFloors.Count;
            return record;
        }

        private static SimSpinRecord MeasureSpin(
            in SpinResolution resolution, SpinRuleSet rules, int spinIndex, bool additional)
        {
            var record = new SimSpinRecord
            {
                spinIndex = spinIndex,
                cells = resolution.InitialBoard.ToArray(),
                normalSoulBasePower = rules.NormalSoulValue,
                normalSoulPower = resolution.NormalSoulPower,
                grossPower = resolution.GrossPower,
                netPower = resolution.NetPower,
                cascadeLength = resolution.ChainDepth,
                absorberResidualCount = resolution.Residual.AbsorberCount,
                proliferatorResidualCount = resolution.Residual.ProliferatorCount,
                absorberResidualCost = resolution.Residual.StoredPowerLoss,
                proliferatorResidualCost = resolution.Residual.NextProliferatorWeightAdd,
                wasAdditionalSpin = additional,
                resolution = resolution,
            };
            foreach (KeyValuePair<SymbolKind, float> pair in rules.Weights)
                record.symbolWeights[pair.Key] = pair.Value;

            int normalSouls = 0;
            if (resolution.Steps != null)
            {
                foreach (CascadeStep step in resolution.Steps)
                {
                    normalSouls += step.NormalSoulsHarvested;
                    if (step.Purifies == null) continue;
                    foreach (PurifyEvent purify in step.Purifies)
                    {
                        if (purify.Pattern == PatternKind.Scattered) record.basePurifyCount++;
                        if (purify.Pattern == PatternKind.Line) record.lineCount++;
                        if (purify.Pattern == PatternKind.Cluster || purify.Pattern == PatternKind.FullBoard)
                            record.clusterCount++;
                    }
                }
            }
            record.normalSouls = normalSouls;
            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds)
                record.resistanceCounts[kind] = resolution.InitialBoard.CountOf(kind);
            record.cascadeAdditionalPower = CascadePowerAfterFirst(resolution);
            return record;
        }

        private static float CascadePowerAfterFirst(in SpinResolution resolution)
        {
            if (resolution.Steps == null || resolution.Steps.Length < 2) return 0f;
            float result = 0f;
            for (int i = 1; i < resolution.Steps.Length; i++) result += resolution.Steps[i].StepPower;
            return result;
        }

        private static float TotalWeight(SpinRuleSet rules)
        {
            float total = 0f;
            foreach (KeyValuePair<SymbolKind, float> pair in rules.Weights)
                total += (float)Math.Max(0d, pair.Value);
            return total;
        }

        private static void Accumulate(SimPolicyReport report, SimRunRecord run)
        {
            if (run.succeeded) report.successfulRuns++;
            foreach (SimFloorRecord floor in run.floors)
            {
                int i = floor.floor - 1;
                if (i < 0 || i >= report.floorPasses.Length) continue;
                report.floorAttempts[i]++;
                if (floor.passed) report.floorPasses[i]++;
                report.averageFinalPowerByFloor[i] += floor.finalPower;
                report.averageRequiredPowerByFloor[i] += floor.requiredPower;
                report.additionalSpinChoicesByFloor[i] += floor.additionalSpinChoices;
                report.additionalSpinDecisionsByFloor[i] += floor.additionalSpinDecisions;
                report.additionalSpinSuccessesByFloor[i] += floor.additionalSpinSuccesses;
                report.additionalSpinLossesByFloor[i] += floor.additionalSpinLosses;
                    report.averageAnteByFloor[i] += floor.totalAnte;
                    report.firstAdditionalSpinCount += floor.firstAdditionalSpinChoices;
                    report.firstAdditionalSpinLossCount += floor.firstAdditionalSpinLosses;
                report.totalWeight += floor.totalWeight;
                Increment(report.thresholdCrossingDistribution, floor.thresholdCrossings);
                Increment(report.powerBandDistribution, floor.finalBand);
                if (!floor.selectedContract.IsNone)
                {
                    report.contractRuns++;
                    report.contractAverageFinalPower += floor.finalPower;
                }
                else report.noneAverageFinalPower += floor.finalPower;

                foreach (SimSpinRecord spin in floor.spins)
                {
                    report.basePurificationsByFloor[i] += spin.basePurifyCount;
                    report.linesByFloor[i] += spin.lineCount;
                    report.clustersByFloor[i] += spin.clusterCount;
                    report.cascadesByFloor[i] += spin.cascadeLength > 1 ? 1f : 0f;
                    report.cascadeAdditionalPowerByFloor[i] += spin.cascadeAdditionalPower;
                    report.absorberResidualsByFloor[i] += spin.absorberResidualCount;
                    report.proliferatorResidualsByFloor[i] += spin.proliferatorResidualCount;
                    report.residualCostsByFloor[i] += spin.absorberResidualCost + spin.proliferatorResidualCost;
                    report.averageNormalSoulBasePower += spin.normalSoulBasePower;
                    foreach (KeyValuePair<SymbolKind, int> pair in spin.resistanceCounts)
                        Add(report.totalResistanceCounts, pair.Key, pair.Value);
                    foreach (KeyValuePair<SymbolKind, float> pair in spin.symbolWeights)
                        Add(report.averageSymbolWeights, pair.Key, pair.Value);
                    report.symbolWeightSamples++;
                    Increment(report.cascadeLengthDistribution, spin.cascadeLength);
                }
            }
            report.averageFinalPower += run.finalPower;
        }

        private static void FinalizeAverages(SimPolicyReport report)
        {
            report.averageFinalPower /= Math.Max(1, report.runCount);
            int floors = 0;
            int spins = 0;
            for (int i = 0; i < report.floorAttempts.Length; i++)
            {
                report.averageFinalPowerByFloor[i] /= Math.Max(1, report.floorAttempts[i]);
                report.averageRequiredPowerByFloor[i] /= Math.Max(1, report.floorAttempts[i]);
                floors += report.floorAttempts[i];
                report.additionalSpinChoicesByFloor[i] /= Math.Max(1, report.floorAttempts[i]);
                report.additionalSpinDecisionsByFloor[i] /= Math.Max(1, report.floorAttempts[i]);
                report.additionalSpinSuccessesByFloor[i] /= Math.Max(1, report.floorAttempts[i]);
                report.additionalSpinLossesByFloor[i] /= Math.Max(1, report.floorAttempts[i]);
                report.averageAnteByFloor[i] /= Math.Max(1, report.floorAttempts[i]);
                if (report.additionalSpinDecisionsByFloor[i] > 0f)
                    report.additionalSpinChoicesByFloor[i] /= report.additionalSpinDecisionsByFloor[i];
            }
            foreach (int count in report.cascadeLengthDistribution.Values) spins += count;
            for (int i = 0; i < report.floorAttempts.Length; i++)
            {
                int denominator = CountSpinsForFloor(report, i);
                report.basePurificationsByFloor[i] /= Math.Max(1, denominator);
                report.linesByFloor[i] /= Math.Max(1, denominator);
                report.clustersByFloor[i] /= Math.Max(1, denominator);
                report.cascadesByFloor[i] /= Math.Max(1, denominator);
                report.cascadeAdditionalPowerByFloor[i] /= Math.Max(1, denominator);
                report.absorberResidualsByFloor[i] /= Math.Max(1, denominator);
                report.proliferatorResidualsByFloor[i] /= Math.Max(1, denominator);
                report.residualCostsByFloor[i] /= Math.Max(1, denominator);
            }
            report.totalWeight /= Math.Max(1, floors);
            report.averageNormalSoulBasePower /= Math.Max(1, spins);
            var weightKeys = new List<SymbolKind>(report.averageSymbolWeights.Keys);
            foreach (SymbolKind key in weightKeys)
                report.averageSymbolWeights[key] /= Math.Max(1, report.symbolWeightSamples);
            if (report.contractRuns > 0) report.contractAverageFinalPower /= report.contractRuns;
            int noneRuns = floors - report.contractRuns;
            if (noneRuns > 0) report.noneAverageFinalPower /= noneRuns;
            report.firstAdditionalSpinLossRate = report.firstAdditionalSpinCount == 0
                ? 0f
                : (float)report.firstAdditionalSpinLossCount / report.firstAdditionalSpinCount;
        }

        private static int CountSpinsForFloor(SimPolicyReport report, int floorIndex)
        {
            int count = 0;
            foreach (SimRunRecord run in report.runs)
                foreach (SimFloorRecord floor in run.floors)
                    if (floor.floor - 1 == floorIndex) count += floor.spins.Count;
            return count;
        }

        private static void Add(Dictionary<SymbolKind, int> dictionary, SymbolKind key, int value)
        {
            int current;
            dictionary.TryGetValue(key, out current);
            dictionary[key] = current + value;
        }

        private static void Add(Dictionary<SymbolKind, float> dictionary, SymbolKind key, float value)
        {
            float current;
            dictionary.TryGetValue(key, out current);
            dictionary[key] = current + value;
        }

        private static void Increment<T>(Dictionary<T, int> dictionary, T key)
        {
            int current;
            dictionary.TryGetValue(key, out current);
            dictionary[key] = current + 1;
        }
    }
}
