using System;
using System.Collections.Generic;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype
{
    /// <summary>
    /// Headless ten-floor balance simulation. This class intentionally has no Unity dependency;
    /// the only game-side operation it performs is calling the fixed SpinEngine contract.
    ///
    /// ## 이 시뮬레이터는 게임의 **일부**만 돈다 — 무엇이 빠졌는지 여기 적어 둔다
    ///
    /// 이 파일은 **밸런스를 판정하는 도구**다. 도구가 출시되는 게임과 다른 게임을 재고 있으면
    /// 없느니만 못하다 — 틀린 숫자가 근거의 얼굴을 하고 문서에 들어가기 때문이다.
    /// 그래서 「무엇을 재지 않는가」를 재는 코드 옆에 적는다. 아래는 `Run/RunSession`·
    /// `Run/FloorSession` 에는 있고 이 파일에는 **없는** 규칙이며, 두 파일을 나란히 놓고 센 것이다.
    ///
    /// ⚠ 「시뮬이 이만큼 모른다」는 이 파일의 산출을 인용할 때마다 함께 적어야 한다.
    /// 2026-08-05 확인: `PENDING_DECISIONS` PD-09 (무적재 완주율 59.0%/시드 200개)와
    /// P-20260804-08 (계약 비교/시드 150개)은 이 파일이 아니라 `Editor/CurriculumCoverageProbe`
    /// ·`Editor/BalanceSweep` 산이고, 그 둘은 `RunSession` 을 직접 몬다. 이 파일의 소비처는
    /// `Sim/Editor/SimMenu`·`Editor/BalanceProbe`·`Editor/PlaytestSimRunner` 셋이다.
    ///
    ///  1. **다층 상승** — 게임은 `AscendResult`(`PowerBand.MultiFloor` 이상)로 한 번에 여러
    ///     층을 오른다. 여기서는 1→10 을 한 층씩 순서대로 밟는다. 그래서 이 파일의
    ///     **「층별 방문률」은 게임의 방문률이 아니다** — 여기서는 「그 층 앞에서 죽지 않은
    ///     비율」이고, 게임에서는 「건너뛰지 않고 실제로 연 비율」이다. 둘은 반대 방향으로
    ///     움직인다(잘할수록 여기 방문률은 오르고 게임 방문률은 내려간다).
    ///  2. **`RunSession.ClampAscent`** — 최종 층·적재 층·`MustBePlayed` 앞에서 상승을 자르는
    ///     커리큘럼 보호. 1 이 없으니 여기서는 걸릴 자리가 없다.
    ///  3. **적재 → 무게 → 요구 전력·과적** (`WeightSnapshot.RequiredPowerFor`). 여기의
    ///     `requiredPower` 는 층 계획의 생값이다. `SimFloorRecord.allowedWeight`·`overloaded`
    ///     는 채워지지 않는다.
    ///  4. **적재 단계 자체** — `FloorPhase.Boarding`, `BuildCatalog.OffersFor`, 그리고
    ///     부품·승객이 규칙 다발에 얹는 효과(`BuildLoadout.ApplyTo`).
    ///  5. **남은 스핀 정산**과 과수확 선택 시의 정산 권리 소멸(`T-05`). 즉 여기의 「탐욕」은
    ///     게임의 탐욕보다 **싸다** — 버리는 정산이 없기 때문이다.
    ///  6. **돈** — 초과 전력 환산·화물 포기(`PayJettisonCost`)·`PowerBand.Damaged` 의 대가.
    ///     `SimRunRecord.finalMoney` 는 항상 0 이다.
    ///  7. **Mercy / Hunger** (`FloorSession.SpinsRemainingAtFirstReach`, `MercyHunger`).
    ///  8. **위험 단계·사고 기록·사건 버스·연출 계층 전부** (`RiskEvaluator`, `GameEventBus`).
    ///  9. **프로파일 에셋의 값 교체** — 게임은 `TenFloorSource` 를 통해
    ///     `FloorCurriculumSnapshot`·`ContractSnapshot` 을, `FloorSession` 을 통해
    ///     `SpinBalanceSnapshot` 을 받는다. 여기는 `PrototypeCurriculum.TenFloors` 와
    ///     인자 없는 `BuildRules` 를 직접 부르므로 **코드 프리셋에 고정**돼 있다.
    ///     지금은 에셋 기본값과 코드 프리셋이 같아 수치가 어긋나지 않지만, 누군가
    ///     `SpinBalanceProfile.asset` 을 고치는 순간 게임만 움직이고 시뮬은 안 움직인다.
    ///     판돈과 **같은 부류의 결함**이고, 고치는 법도 같다 — 스냅샷을 생성자로 받는다.
    ///     판돈 쪽을 먼저 고친 이유는 그쪽만 식이 **복제**돼 있었기 때문이다(값이 아니라
    ///     식이 갈라지면 프로파일을 안 건드려도 이미 다른 게임이다).
    ///
    /// 아래 넷은 **고쳤다** — 예전에는 이 목록에 함께 있었다.
    ///   · 시드 파생: 순차 스트림 → `SpinSeed.Derive(runSeed, floor, spinIndex)` (게임과 동일)
    ///   · 판돈: 하드코딩 `0.12 × (1 + 0.35n)` → `OverharvestSnapshot.AnteFor`
    ///   · 과수확 해금·추가 스핀 상한: 없음 → `IsUnlocked` / `MaxExtraSpins`
    ///   · 잔류 저항의 **층간 이월**: 층마다 초기화 → `RunSession.CompleteFloor` 와 같이 이월
    /// </summary>
    public sealed class RunSimulator
    {
        private readonly int _seed;

        /// <summary>
        /// 과수확 수치 9종. 게임(`FloorSession`)과 **같은 구조체**를 쓴다 — 판돈 식을 여기서
        /// 다시 쓰면 프로파일을 고쳐도 시뮬만 옛 값으로 돌고, 그 상태로 잰 숫자가
        /// 「게임을 측정했다」는 이름으로 문서에 들어간다.
        /// </summary>
        private readonly OverharvestSnapshot _overharvest;

        public RunSimulator(int seed = 1337)
            : this(seed, OverharvestProfile.DefaultSnapshot)
        {
        }

        /// <summary>
        /// 과수확 수치를 밖에서 받는 경로. 에셋을 든 호출자가 게임과 같은 값으로 밸런스를
        /// 재려면 이 자리가 있어야 한다. 에셋이 없으면 코드 기본값이라 동작이 같다.
        /// </summary>
        public RunSimulator(int seed, OverharvestSnapshot overharvest)
        {
            _seed = seed;
            _overharvest = overharvest;
        }

        /// <summary>
        /// Source-compatible shim for retired editor callers. The new simulator is intentionally
        /// asset-free, so these values are ignored and the deterministic default seed is used.
        /// </summary>
        public RunSimulator(object config, object ballDatabase, object combinationConfig,
                            object effectSettings, object passengerPool)
        {
            _seed = 1337;
            _overharvest = OverharvestProfile.DefaultSnapshot;
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
                // **잔류는 층을 건너 따라온다.** 여기서 비우던 것이 게임과 달랐다 —
                // `RunSession.CompleteFloor` 가 `_residual = _current.Residual` 로 받아
                // 다음 `FloorSession` 생성자에 그대로 넘긴다. 즉 마지막 스핀에 판에 남긴
                // 증식체는 **다음 층 첫 스핀의 가중치를 올린다**(`SpinEngine.PrepareRules`).
                // 층마다 비우면 「남긴 것의 대가」가 층 경계에서 조용히 사라져,
                // 판을 비우지 않고 넘기는 플레이가 공짜가 된다.
                //
                // 계약은 층마다 새로 고르는 것이 맞다 — 그건 게임도 같다.
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
                    // 요구 전력을 채운 뒤의 스핀은 게임에서 **추가 스핀**이다. 게임의 판정은
                    // `FloorSession.CanTakeExtraSpin` 하나이므로 여기서도 그 세 조건을 그대로
                    // 쓴다 — 해금 임계(`IsUnlocked`) · 남은 스핀 · 프로파일 상한.
                    // 셋 중 하나라도 막으면 레버가 아무것도 하지 않는 것이 게임의 동작이다.
                    bool requiredAlreadyMet = floorPower >= floor.RequiredPower;
                    int spinsRemaining = floor.Spins - spinIndex;
                    bool canTakeExtra = requiredAlreadyMet
                        && _overharvest.IsUnlocked(floorPower, floor.RequiredPower)
                        && spinsRemaining > 0
                        && floorRecord.additionalSpinChoices < _overharvest.MaxExtraSpins;

                    // 「선택이 있었다」는 실제로 당길 수 있었을 때만 성립한다. 상한에 닿아
                    // 레버가 죽어 있는 층을 결정으로 세면 선택률의 분모가 부푼다.
                    if (canTakeExtra) floorRecord.additionalSpinDecisions++;
                    if (requiredAlreadyMet && !canTakeExtra) break;
                    if (canTakeExtra && !policy.ShouldTakeAdditionalSpin(
                            floorPower, floor.RequiredPower, spinsRemaining - 1)) break;

                    bool additional = canTakeExtra;
                    bool firstAdditional = additional && floorRecord.additionalSpinChoices == 0;
                    float ante = 0f;
                    if (additional)
                    {
                        // 식을 여기서 다시 쓰지 않는다. `FloorSession.PushYourLuck` 이 부르는
                        // 것과 **같은 한 줄**이라 프로파일을 고치면 둘이 함께 움직인다.
                        ante = _overharvest.AnteFor(floorPower, floorRecord.additionalSpinChoices);
                        floorPower -= ante;
                        runPower -= ante;
                        floorRecord.totalAnte += ante;
                    }
                    // 시드는 순차 스트림이 아니라 (런 시드, 층, 스핀 인덱스) 좌표에서 나온다.
                    // `Run/FloorSession.Spin()` 과 **같은 두 줄**이다 — 여기가 갈라져 있던
                    // 동안 시뮬은 같은 시드로 게임과 **다른 판**을 돌렸다. 실측(시드 1000개)에서
                    // 같은 시드의 1층 최종 전력이 일치한 비율은 0.1~2.2% 였다.
                    // `Spin/SpinSeed` 의 클래스 주석이 순차 스트림을 쓰지 말라고 적어 둔
                    // 바로 그 경로였고, 그 경고를 어긴 유일한 호출자가 이 파일이었다.
                    int spinSeed = SpinSeed.Derive(engine.RunSeed, floor.Floor, spinIndex);
                    SpinResolution resolution = engine.SpinWithSeed(
                        spinSeed, rules, in floorRecord.selectedContract, in residual,
                        floor.Floor, spinIndex);
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
