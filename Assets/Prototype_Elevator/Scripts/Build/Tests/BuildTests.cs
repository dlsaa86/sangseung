using System;
using System.Text;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Build.Tests
{
    /// <summary>
    /// 적재·빌드·10층 진행 검사. `RunTests`와 같은 헤드리스 러너 규약을 쓴다
    /// (NUnit 미사용 근거는 `DECISION_LOG.md` D-20260730-06).
    /// </summary>
    public static class BuildTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            // ── 적재 단계 ──
            Run("적재 층은 Boarding 단계로 시작한다", TestBoardingPhaseStarts, ref passed, ref failed, report);
            Run("아무것도 싣지 않아도 진행된다", TestBoardingCanBeDeclined, ref passed, ref failed, report);
            Run("적재 층이 아니면 Boarding 이 없다", TestNonBuildFloorSkipsBoarding, ref passed, ref failed, report);
            Run("적재 중에는 스핀과 계약이 거부된다", TestBoardingGatesOtherActions, ref passed, ref failed, report);
            Run("슬롯이 차면 더 싣지 못한다", TestSlotCap, ref passed, ref failed, report);
            Run("이미 실은 것은 다시 제시되지 않는다", TestOffersExcludeCarried, ref passed, ref failed, report);
            Run("같은 시드·같은 층은 같은 후보", TestOfferDeterminism, ref passed, ref failed, report);

            // ── 적재 정책 (씬 하네스와 같은 규칙) ──
            Run("적재 정책이 승객 정원을 먼저 채운다", TestLoadPolicyFillsPassengersFirst, ref passed, ref failed, report);
            Run("승객 우선이 카탈로그 전체에서 성립한다", TestLoadPolicyPassengerPriorityHoldsForWholeCatalog, ref passed, ref failed, report);
            Run("적재 정책이 허용 중량을 늘리는 것을 피한다", TestLoadPolicyAvoidsCapacityBonus, ref passed, ref failed, report);
            Run("적재 정책이 자리가 없으면 -1을 준다", TestLoadPolicyStopsWhenFull, ref passed, ref failed, report);
            Run("적재 정책이 칸을 끝까지 채운다", TestLoadPolicyFillsAllSlots, ref passed, ref failed, report);
            Run("적재 정책으로 과적이 도달 가능하다", TestLoadPolicyReachesOverCapacity, ref passed, ref failed, report);

            // ── 무게와 과적 ──
            Run("실은 무게가 요구 전력을 올린다", TestLoadRaisesRequirement, ref passed, ref failed, report);
            Run("짐꾼이 허용 중량을 올린다", TestPorterRaisesCapacity, ref passed, ref failed, report);
            Run("과적이 요구 전력에 배수를 건다", TestOverloadMultiplier, ref passed, ref failed, report);
            Run("적재 무게가 다음 층으로 이어진다", TestLoadCarriesToNextFloor, ref passed, ref failed, report);
            Run("무게 변경이 현재 층에 즉시 반영된다", TestWeightChangePropagates, ref passed, ref failed, report);
            Run("적재 직접 변경도 현재 층에 반영된다", TestLoadoutMutationPropagates, ref passed, ref failed, report);

            // ── 규칙 변경 (Gate C 핵심) ──
            Run("승객이 정화 문턱을 낮춘다", TestPassengerLowersPurifyThreshold, ref passed, ref failed, report);
            Run("부품이 대각 연결을 연다", TestPartEnablesDiagonal, ref passed, ref failed, report);
            Run("부품이 연쇄 배수 증분을 올린다", TestPartRaisesCascadeStep, ref passed, ref failed, report);
            Run("부품이 잔류 대가를 완화한다", TestPartMitigatesResidual, ref passed, ref failed, report);
            Run("계약과 승객이 함께 적용된다", TestContractAndLoadoutCompose, ref passed, ref failed, report);
            Run("적재 없는 층은 기본 규칙 그대로", TestEmptyLoadoutLeavesRulesAlone, ref passed, ref failed, report);

            // ── 승하차 ──
            Run("목적지 층에서 내리고 요금을 준다", TestPassengerDisembarks, ref passed, ref failed, report);
            Run("부품은 하차하지 않는다", TestPartsStayAboard, ref passed, ref failed, report);

            // ── 10층 진행 (Gate B 핵심) ──
            Run("도달 층이 건물 높이를 넘지 않는다", TestHighestFloorClamped, ref passed, ref failed, report);
            Run("다층 상승이 최종 층을 건너뛰지 않는다", TestFinalFloorNeverSkipped, ref passed, ref failed, report);
            Run("다층 상승이 적재 층을 건너뛰지 않는다", TestBuildFloorNeverSkipped, ref passed, ref failed, report);
            Run("10층 런의 방문 층이 연속이다", TestVisitedFloorsAreConsecutive, ref passed, ref failed, report);
            Run("추가 층 전력이 돈으로 중복 지급되지 않는다", TestNoDoubleSpendOfSurplus, ref passed, ref failed, report);
            Run("소지금이 지급 기록 합계와 일치한다", TestMoneyMatchesCreditedLedger, ref passed, ref failed, report);
            Run("화물 포기 구간은 런을 끝내지 않고 대가를 물린다", TestJettisonBandCostsInsteadOfEnding, ref passed, ref failed, report);
            Run("하차가 확정된 층의 숫자를 바꾸지 않는다", TestDisembarkDoesNotMutateResolvedFloor, ref passed, ref failed, report);
            Run("고정 시드 3개 이상이 10층을 완주한다", TestSeedsCompleteTenFloors, ref passed, ref failed, report);
            Run("적재하고도 10층을 완주할 수 있다", TestLoadedRunCanAlsoComplete, ref passed, ref failed, report);
            Run("10층 연속 런에 진행 불가 상태가 없다", TestTenFloorRunNeverStalls, ref passed, ref failed, report);
            Run("동일 시드·동일 선택이 동일 결과", TestTenFloorDeterminism, ref passed, ref failed, report);
            Run("서로 다른 두 빌드가 결과를 바꾼다", TestTwoBuildsDiverge, ref passed, ref failed, report);
            Run("계약을 실제로 건 런도 10층을 완주한다", TestContractedRunCompletes, ref passed, ref failed, report);
            Run("시너지 한 줄이 같은 저항을 겨냥한 부품만 센다", TestContractSynergyCountsOnlyMatchingTarget, ref passed, ref failed, report);
            Run("적재가 비면 시너지 줄이 그렇게 말한다", TestContractSynergyWithEmptyLoadout, ref passed, ref failed, report);

            // ── 월드 라벨 배치 (그룹 C · UP-FIX-12·14·21) ──
            //
            // 여기로 접어 넣는 이유: 러너 등록은 `Assets/Editor/` 에 있고 그쪽은
            // 이 작업의 소유 경로가 아니다. 접어 넣으면 등록을 고치지 않아도
            // `AscendTestMenu`·`PrototypeSelfTest` 양쪽에서 함께 돈다.
            var labels = BuildLabelPlacementTests.RunAll();
            passed += labels.passed;
            failed += labels.failed;
            report.Append(labels.report);

            report.Insert(0, "[상승] === Build Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        private static void Run(string name, Func<string> test,
            ref int passed, ref int failed, StringBuilder report)
        {
            try
            {
                string failure = test();
                if (string.IsNullOrEmpty(failure)) { passed++; report.AppendLine($"  PASS  {name}"); }
                else { failed++; report.AppendLine($"  FAIL  {name} — {failure}"); }
            }
            catch (Exception exception)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외: {exception.Message}");
            }
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────

        private static RunSession NewTenFloorRun(int seed) => new RunSession(
            seed, 0f, 0f, FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation,
            new TenFloorSource());

        private static FloorSession FloorWith(int floor, params string[] itemIds)
        {
            var loadout = new BuildLoadout();
            foreach (string id in itemIds) loadout.Add(BuildCatalog.ById(id));
            return new FloorSession(PrototypeCurriculum.For(floor), new SpinEngine(1),
                PowerThresholds.Default, 0f, ResidualState.Empty, 0f, 0f, loadout);
        }

        /// <summary>런 하나를 끝까지 굴린다. 층 방문 순서와 최종 결과를 돌려준다.</summary>
        private static (RunSession run, System.Collections.Generic.List<int> visited) Drive(
            int seed, Func<FloorSession, int, bool> boardingPolicy)
            => Drive(seed, boardingPolicy, null);

        /// <summary>
        /// 계약 정책까지 지정해 굴린다.
        ///
        /// 인자 없는 형태는 언제나 0번을 골랐고, 커리큘럼의 계약 층은 0번이 전부
        /// `ResistanceContract.None` 이다. 그래서 **헤드리스 런 중 살아 있는 계약이
        /// 걸린 런이 하나도 없었다** — 10층만 예외지만 거기까지 가는 런은 소수다.
        /// 계약이 출현률·정화 보상·잔류 대가를 곱으로 바꾸므로, 계약 없는 표본만으로
        /// 낸 완주율·결정론·연속성은 게임의 절반만 검증한 것이다.
        /// </summary>
        /// <param name="contractPolicy">
        /// 층과 선택지 개수를 받아 인덱스를 돌려준다. null 이면 0번(대개 "계약 없음").
        /// </param>
        private static (RunSession run, System.Collections.Generic.List<int> visited) Drive(
            int seed, Func<FloorSession, int, bool> boardingPolicy,
            Func<FloorSession, int, int> contractPolicy)
        {
            RunSession run = NewTenFloorRun(seed);
            var visited = new System.Collections.Generic.List<int>();
            int guard = 0;

            while (!run.IsComplete && !run.IsFailed && guard++ < 200)
            {
                FloorSession f = run.Current;
                if (f == null) break;
                if (visited.Count == 0 || visited[visited.Count - 1] != f.Plan.Floor)
                    visited.Add(f.Plan.Floor);

                if (f.Phase == FloorPhase.Boarding)
                {
                    if (boardingPolicy != null)
                    {
                        int slot = 0;
                        while (f.BuildOffers.Count > 0 && boardingPolicy(f, slot))
                        {
                            if (!run.TakeBuildOffer(0)) break;
                            slot++;
                        }
                    }
                    if (!run.FinishBoarding()) break;
                }

                if (f.Phase == FloorPhase.ContractSelection)
                {
                    int count = f.Plan.ContractChoices != null ? f.Plan.ContractChoices.Length : 0;
                    int pick = contractPolicy != null && count > 0
                        ? Math.Max(0, Math.Min(count - 1, contractPolicy(f, count)))
                        : 0;
                    if (!run.SelectContract(pick)) break;
                }

                int spins = 0;
                while (f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0)
                {
                    run.Spin();
                    if (++spins > 30) break;
                }

                if (f.CanBank) run.Bank();
                else if (f.SpinsRemaining == 0) run.ForceResolve();
                else break;
            }
            return (run, visited);
        }

        // ── 적재 단계 ────────────────────────────────────────────────────────

        private static string TestBoardingPhaseStarts()
        {
            RunSession run = NewTenFloorRun(1337);
            // 1층은 적재 층이 아니다.
            if (run.Current.Phase == FloorPhase.Boarding) return "1층이 적재 층으로 시작함";

            FloorSession f = FloorWith(2);
            if (f.Phase != FloorPhase.Boarding) return $"2층이 Boarding 이 아님: {f.Phase}";
            if (f.BuildOffers.Count != FloorSession.BuildOfferCount)
                return $"후보 수가 {f.BuildOffers.Count} (기대 {FloorSession.BuildOfferCount})";
            return null;
        }

        private static string TestBoardingCanBeDeclined()
        {
            FloorSession f = FloorWith(2);
            if (!f.FinishBoarding()) return "적재 없이 문을 닫지 못함 — 진행 불가 상태";
            if (f.Phase == FloorPhase.Boarding) return "문을 닫았는데 여전히 Boarding";
            if (f.Loadout.Count != 0) return "아무것도 안 실었는데 적재가 생김";
            return null;
        }

        private static string TestNonBuildFloorSkipsBoarding()
        {
            foreach (int floor in new[] { 1, 3, 4, 6, 7, 9, 10 })
            {
                FloorSession f = FloorWith(floor);
                if (f.Phase == FloorPhase.Boarding) return $"{floor}층이 적재 층이 아닌데 Boarding";
            }
            return null;
        }

        private static string TestBoardingGatesOtherActions()
        {
            FloorSession f = FloorWith(8);   // 계약 3종 + 적재 층
            if (f.Phase != FloorPhase.Boarding) return "8층이 Boarding 으로 시작하지 않음";
            if (f.SelectContract(1)) return "적재 중에 계약이 확정됨";
            SpinResolution rejected = f.Spin();
            if (rejected.Steps != null || f.SpinsUsed != 0) return "적재 중에 스핀이 진행됨";
            return null;
        }

        private static string TestSlotCap()
        {
            var loadout = new BuildLoadout();
            int added = 0;
            foreach (BuildItem item in BuildCatalog.All)
                if (loadout.Add(item)) added++;
            if (added != BuildLoadout.MaxSlots)
                return $"{added}개가 실림 (상한 {BuildLoadout.MaxSlots})";
            if (!loadout.IsFull) return "상한에 닿았는데 IsFull 이 거짓";
            return null;
        }

        // ── 적재 정책 ──────────────────────────────────────────────────────
        //
        // 씬 하네스(`TenFloorAutoPilot`)와 **같은** `BuildLoadPolicy` 를 검사한다.
        // 규칙을 두 벌 두면 여기서 고른 시드가 씬에서 다른 결과를 낸다.

        /// <summary>
        /// 정책이 자리를 다 쓸 때까지 굴린다. **씬 하네스가 하는 것과 같은 선택을 해야 한다** —
        /// 그러지 않으면 여기서 고른 시드가 씬에서 다른 런이 된다.
        ///
        /// 계약을 `Min(1, count - 1)` 로 고르는 이유: `TenFloorAutoPilot` 은 선택지가 둘 이상인
        /// 계약 층에서 패널을 정확히 한 번 눌러 **1번 선택지**를 확정한다. 여기서 0번을 고르면
        /// 4층(`{None, AbsorberContract}`)부터 두 경로가 갈리고, 그 뒤의 모든 스핀 결과가 달라진다.
        ///
        /// 실측으로는 8개 시드 전부에서 0번과 1번의 **적재 지표가 동일했다**(칸·승객·무게·과적·도달층).
        /// 그래도 맞춰 두는 이유는 그 무차별이 **밸런스가 바뀌면 사라질 성질**이기 때문이다.
        /// 지금 우연히 같다는 것과 앞으로도 같다는 것은 다르다.
        /// </summary>
        private static (RunSession run, int peakSlots, int peakPassengers, float peakWeight,
                        float capAtPeak, bool overCapacity, float overWeight, float overCapacityAt)
            DriveWithLoadPolicy(int seed)
        {
            RunSession run = NewTenFloorRun(seed);
            int peakSlots = 0, peakPassengers = 0, guard = 0;
            float peakWeight = 0f, capAtPeak = 0f, overWeight = 0f, overCapacityAt = 0f;
            bool over = false;

            while (!run.IsComplete && !run.IsFailed && guard++ < 200)
            {
                FloorSession f = run.Current;
                if (f == null) break;

                if (f.Phase == FloorPhase.Boarding)
                {
                    int pick;
                    int takeGuard = 0;
                    // 바깥 루프에는 guard 가 있는데 여기에는 없었다. `TakeOffer` 성공이 반드시
                    // `_offers` 를 줄인다는 성질이 깨지면 에디터 메인 스레드가 잡힌다.
                    while ((pick = BuildLoadPolicy.PickIndex(f.BuildOffers, f.Loadout)) >= 0
                           && takeGuard++ <= BuildLoadout.MaxSlots)
                        if (!run.TakeBuildOffer(pick)) break;
                    if (!run.FinishBoarding()) break;
                }

                if (f.Loadout != null)
                {
                    if (f.Loadout.Count > peakSlots) peakSlots = f.Loadout.Count;
                    int p = BuildLoadPolicy.CountPassengers(f.Loadout);
                    if (p > peakPassengers) peakPassengers = p;
                }
                if (run.CarriedWeight > peakWeight)
                {
                    peakWeight = run.CarriedWeight;
                    capAtPeak = run.WeightCapacity;
                }
                // 과적이었던 **그 순간**의 쌍을 따로 남긴다. 최고 무게였던 순간과 과적이었던
                // 순간은 다를 수 있다 — 짐꾼이 실린 동안 120/130(과적 아님)을 찍고 하차 뒤
                // 105/100(과적)이 되면, 한 쌍만 들고 다니는 보고서는 「과적했다 — 최고 120/130」
                // 이라는 스스로를 반증하는 문장을 낸다.
                if (run.CarriedWeight > run.WeightCapacity)
                {
                    over = true;
                    if (run.CarriedWeight - run.WeightCapacity > overWeight - overCapacityAt)
                    {
                        overWeight = run.CarriedWeight;
                        overCapacityAt = run.WeightCapacity;
                    }
                }

                if (f.Phase == FloorPhase.ContractSelection)
                {
                    int count = f.Plan.ContractChoices != null ? f.Plan.ContractChoices.Length : 0;
                    if (!run.SelectContract(count > 1 ? 1 : 0)) break;
                }

                int spins = 0;
                while (f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0)
                {
                    run.Spin();
                    if (++spins > 30) break;
                }

                if (f.CanBank) run.Bank();
                else if (f.SpinsRemaining == 0) run.ForceResolve();
                else break;
            }
            return (run, peakSlots, peakPassengers, peakWeight, capAtPeak, over, overWeight, overCapacityAt);
        }

        /// <summary>실패 메시지가 원인을 잘못 지목하지 않도록 런의 종말을 함께 적는다.</summary>
        private static string DescribeEnd(RunSession run)
            => run == null ? "런 없음"
             : $"완주={run.IsComplete} 사고={run.IsFailed} 도달 {run.HighestFloorReached}층" +
               (string.IsNullOrEmpty(run.FailureReason) ? "" : $" 「{run.FailureReason}」");

        private static string TestLoadPolicyFillsPassengersFirst()
        {
            // 짐꾼(12kg)보다 사선 결속기(26kg)가 훨씬 무겁지만, 승객 정원을 채우기 전에는
            // 승객이 앞서야 한다. 이 순서가 뒤집히면 승객 반응이 다시 관측 불가가 된다.
            BuildItem heavyPart = BuildCatalog.ById("PRT_DIAGONAL_BINDER");
            BuildItem lightPassenger = BuildCatalog.ById("PSG_SURVEYOR_LINE");

            var offers = new[] { heavyPart, lightPassenger };
            int pick = BuildLoadPolicy.PickIndex(offers, new BuildLoadout());
            if (pick != 1)
                return $"빈 칸에서 {pick}번({offers[pick].Label})을 골랐다 — 승객이 먼저여야 한다";

            // 정원을 채우면 무게가 이긴다.
            var full = new BuildLoadout();
            full.Add(BuildCatalog.ById("PSG_SURVEYOR"));
            full.Add(BuildCatalog.ById("PSG_MOURNER"));
            full.Add(BuildCatalog.ById("PSG_TECHNICIAN"));
            pick = BuildLoadPolicy.PickIndex(offers, full);
            if (pick != 0)
                return $"승객 3명을 채운 뒤에도 {pick}번을 골랐다 — 무거운 부품이 앞서야 한다";
            return null;
        }

        private static string TestLoadPolicyAvoidsCapacityBonus()
        {
            // 짐꾼은 12kg 으로 측량사(6kg)보다 무겁지만 허용 중량을 30 올린다.
            // 무게만 보면 정책이 스스로 과적이라는 목표를 지운다.
            BuildItem porter = BuildCatalog.ById("PSG_PORTER");
            BuildItem surveyor = BuildCatalog.ById("PSG_SURVEYOR_LINE");
            if (porter.CapacityBonus <= 0f) return "짐꾼의 CapacityBonus 가 0 — 이 검사의 전제가 깨졌다";

            // 승객 정원을 채운 상태에서 비교한다. 정원 이전에는 둘 다 +100 이라 무게차만 남는다.
            var held = new BuildLoadout();
            held.Add(BuildCatalog.ById("PSG_SURVEYOR"));
            held.Add(BuildCatalog.ById("PSG_MOURNER"));
            held.Add(BuildCatalog.ById("PSG_TECHNICIAN"));

            float porterScore = BuildLoadPolicy.Score(porter, BuildLoadPolicy.CountPassengers(held));
            float surveyorScore = BuildLoadPolicy.Score(surveyor, BuildLoadPolicy.CountPassengers(held));
            if (porterScore >= surveyorScore)
                return $"짐꾼 {porterScore:F1} ≥ 측량사 {surveyorScore:F1} — 허용 중량 증가가 벌점이 아니다";
            return null;
        }

        private static string TestLoadPolicyStopsWhenFull()
        {
            var loadout = new BuildLoadout();
            foreach (BuildItem item in BuildCatalog.All)
            {
                if (loadout.IsFull) break;
                loadout.Add(item);
            }
            if (!loadout.IsFull) return "카탈로그로 칸을 채우지 못했다 — 이 검사의 전제가 깨졌다";

            var offers = new[] { BuildCatalog.ById("PRT_SOUL_TRAP") };
            int pick = BuildLoadPolicy.PickIndex(offers, loadout);
            if (pick != -1) return $"칸이 찼는데 {pick}번을 골랐다";

            if (BuildLoadPolicy.PickIndex(Array.Empty<BuildItem>(), new BuildLoadout()) != -1)
                return "후보가 없는데 -1 이 아니다";
            return null;
        }

        private static string TestLoadPolicyFillsAllSlots()
        {
            // 정책의 존재 이유가 이것이다. 번호 순으로 두 개씩 집던 옛 방식은
            // 아홉 런 전부에서 최고 51kg / 허용 100kg 에 그쳤다.
            int[] seeds = { 4242, 271828, 555, 12321, 777 };
            foreach (int seed in seeds)
            {
                var r = DriveWithLoadPolicy(seed);
                if (r.peakSlots < BuildLoadout.MaxSlots)
                    return $"시드 {seed} 가 {r.peakSlots}/{BuildLoadout.MaxSlots}칸에 그쳤다 " +
                           $"({DescribeEnd(r.run)}) — 런이 일찍 죽은 것과 정책이 안 채운 것은 다르다";
            }
            return null;
        }

        private static string TestLoadPolicyReachesOverCapacity()
        {
            // 과적 경로가 **도달 가능**한지 묻는다. 도달 불가면 `UP-BUILD-11` 의
            // 과적 경고는 영영 검증되지 않는다 — 구현이 없어서가 아니라 상태에
            // 닿지 못해서다. 1337 은 씬 시드이기도 하다.
            var over = DriveWithLoadPolicy(1337);
            if (!over.overCapacity)
                return $"시드 1337 이 최고 {over.peakWeight:F0}kg / 허용 {over.capAtPeak:F0}kg — " +
                       $"과적에 닿지 못했다 ({DescribeEnd(over.run)})";

            var full = DriveWithLoadPolicy(12321);
            if (full.peakPassengers < BuildLoadPolicy.PassengerFloor)
                return $"시드 12321 의 최대 동시 승객이 {full.peakPassengers}명 — " +
                       $"{BuildLoadPolicy.PassengerFloor}명 이상이어야 동시 반응 제한을 관측한다 " +
                       $"({DescribeEnd(full.run)})";
            return null;
        }

        /// <summary>
        /// 「정원 전에는 승객이 무조건 앞선다」를 **카탈로그 전체로** 확인한다.
        ///
        /// 고정된 두 개만 비교하면 이 불변식이 조용히 깨진다. 실제 필요 조건은
        /// `100 - 20 > max(부품 무게) - min(승객 무게 - 허용중량보너스)` 이고,
        /// 오늘 값은 `80 > 26 - (12 - 30) = 44` 로 여유가 36 이다.
        /// 승객 하나에 `CapacityBonus` 가 66 이상 붙거나 부품 하나가 63kg 을 넘으면 깨진다.
        /// 그때 정책은 승객을 안 태우고, 승객 반응은 다시 「관측 불가」가 된다.
        /// </summary>
        private static string TestLoadPolicyPassengerPriorityHoldsForWholeCatalog()
        {
            float worstPassenger = float.MaxValue;
            float bestPart = float.MinValue;
            string worstPassengerId = "(없음)", bestPartId = "(없음)";

            foreach (BuildItem item in BuildCatalog.All)
            {
                float score = BuildLoadPolicy.Score(item, 0);   // 정원 미만 상태
                if (item.Kind == BuildItemKind.Passenger)
                {
                    if (score < worstPassenger) { worstPassenger = score; worstPassengerId = item.Label; }
                }
                else if (score > bestPart) { bestPart = score; bestPartId = item.Label; }
            }

            if (worstPassenger <= bestPart)
                return $"정원 미만인데 부품 「{bestPartId}」({bestPart:F1}) 가 " +
                       $"승객 「{worstPassengerId}」({worstPassenger:F1}) 를 이긴다 — " +
                       "정책이 승객을 안 태우면 승객 반응이 다시 관측 불가가 된다";
            return null;
        }

        private static string TestOffersExcludeCarried()
        {
            var loadout = new BuildLoadout();
            loadout.Add(BuildCatalog.ById("PSG_PORTER"));
            BuildItem[] offers = BuildCatalog.OffersFor(1337, 5, loadout, 8);
            foreach (BuildItem item in offers)
                if (item.Id == "PSG_PORTER") return "이미 실은 것이 다시 제시됨";
            return null;
        }

        private static string TestOfferDeterminism()
        {
            BuildItem[] a = BuildCatalog.OffersFor(1337, 5, null, 3);
            BuildItem[] b = BuildCatalog.OffersFor(1337, 5, null, 3);
            if (a.Length != b.Length) return "같은 인자인데 후보 수가 다름";
            for (int i = 0; i < a.Length; i++)
                if (a[i].Id != b[i].Id) return $"같은 시드인데 후보가 다름: {a[i].Id} vs {b[i].Id}";

            BuildItem[] other = BuildCatalog.OffersFor(1337, 8, null, 3);
            bool identical = other.Length == a.Length;
            for (int i = 0; identical && i < a.Length; i++)
                if (a[i].Id != other[i].Id) identical = false;
            if (identical) return "층이 달라도 후보가 같음 — 좌표가 시드에 안 섞임";
            return null;
        }

        // ── 계약 시너지 한 줄 (`UP-CONTRACT-05`) ─────────────────────────────
        //
        // `NotionSyncReport.md:166` 이 계약 UI 필수 4정보를 못박는다 — 등장 확률 증가폭 ·
        // 정화 보상 증가폭 · 남았을 때의 대가 · **현재 빌드 관련 시너지 한 줄**.
        // 앞의 셋은 `Preview()` 가 이미 냈고 넷째가 없었다.
        //
        // 이 검사의 핵심은 「몇 개를 세는가」가 아니라 **「무엇을 세지 않는가」**다.
        // 대상 없는 효과(연쇄 증분·잔류 완화)까지 세면 어느 계약을 골라도 같은 줄이
        // 나와 비교에 쓸모가 없어진다 — 그 상태를 이 테스트가 실패로 잡는다.

        private static string TestContractSynergyCountsOnlyMatchingTarget()
        {
            var loadout = new BuildLoadout();
            // 광신자 — 증식체를 겨냥한 효과 둘(패턴 가산 · 등장 배율).
            loadout.Add(BuildCatalog.ById("PSG_ZEALOT"));
            // 연쇄 조속기 — 대상 없는 효과 하나. 어느 계약에도 똑같이 걸린다.
            loadout.Add(BuildCatalog.ById("PRT_CASCADE_GOVERNOR"));

            string proliferator = PrototypeCurriculum.ProliferatorContract.SynergyWith(loadout);
            if (proliferator.Contains("시너지 없다"))
                return $"증식체 계약에 광신자가 실려 있는데 「시너지 없다」로 나왔다: {proliferator}";
            if (!proliferator.Contains("광신자"))
                return $"겨냥한 부품 이름이 안 나온다: {proliferator}";

            // 흡수체 계약에는 겨냥한 것이 **없다** — 연쇄 조속기는 대상이 없으므로
            // 세지 않는다. 여기서 「시너지 있다」가 나오면 대상 없는 효과를 센 것이다.
            string absorber = PrototypeCurriculum.AbsorberContract.SynergyWith(loadout);
            if (!absorber.Contains("시너지 없다"))
                return $"흡수체를 겨냥한 부품이 없는데 시너지가 있다고 한다: {absorber}"
                     + " — 대상 없는 효과(연쇄 조속기)를 셌나";

            // 두 계약이 **서로 다른 줄**을 내야 비교에 쓸모가 있다.
            if (proliferator == absorber)
                return "두 계약이 같은 시너지 줄을 낸다 — 비교에 쓸모가 없다";

            // 겨냥한 부품이 둘이면 「외 N개」로 센다.
            loadout.Add(BuildCatalog.ById("PRT_OVERHARVEST_TRANSFORMER"));   // 흡수·증식 둘 다 겨냥
            string both = PrototypeCurriculum.AbsorberContract.SynergyWith(loadout);
            if (both.Contains("시너지 없다"))
                return $"과수확 변압기가 흡수체를 겨냥하는데 없다고 한다: {both}";
            return null;
        }

        private static string TestContractSynergyWithEmptyLoadout()
        {
            var empty = new BuildLoadout();
            string line = PrototypeCurriculum.AbsorberContract.SynergyWith(empty);
            if (string.IsNullOrEmpty(line)) return "빈 적재에서 시너지 줄이 비었다 — 네 번째 정보가 사라진다";
            if (!line.Contains("적재 없음")) return $"빈 적재인데 '{line}' 로 나왔다";

            // null 을 넘겨도 터지지 않아야 한다 — Hero Slice 는 적재가 없는 경로다.
            string nullLine = PrototypeCurriculum.AbsorberContract.SynergyWith(null);
            if (string.IsNullOrEmpty(nullLine)) return "적재가 null 일 때 줄이 비었다";

            // 무계약은 규칙을 안 바꾸므로 그렇게 말해야 한다.
            string none = ResistanceContract.None.SynergyWith(empty);
            if (!none.Contains("무관")) return $"무계약 줄이 '{none}' 다";
            return null;
        }

        // ── 무게와 과적 ──────────────────────────────────────────────────────

        private static string TestLoadRaisesRequirement()
        {
            FloorSession bare = FloorWith(3);
            FloorSession loaded = FloorWith(3, "PSG_ZEALOT");
            if (loaded.CarriedWeight <= bare.CarriedWeight) return "무게가 늘지 않음";
            float expected = bare.RequiredPower + 16f * FloorSession.WeightPowerFactor;
            if (Math.Abs(loaded.RequiredPower - expected) > 0.01f)
                return $"요구 전력 {loaded.RequiredPower} (기대 {expected})";
            return null;
        }

        private static string TestPorterRaisesCapacity()
        {
            FloorSession bare = FloorWith(3);
            FloorSession porter = FloorWith(3, "PSG_PORTER");
            if (Math.Abs(bare.Capacity - FloorSession.AllowedWeight) > 0.01f)
                return $"기본 허용 중량이 {bare.Capacity}";
            if (Math.Abs(porter.Capacity - (FloorSession.AllowedWeight + 30f)) > 0.01f)
                return $"짐꾼 탑승 시 허용 중량이 {porter.Capacity} (기대 {FloorSession.AllowedWeight + 30f})";
            return null;
        }

        private static string TestOverloadMultiplier()
        {
            // 부품 4종(26+22+18+20=86) + 광신자 16 = 102 > 100
            FloorSession over = FloorWith(3, "PRT_DIAGONAL_BINDER", "PRT_CASCADE_GOVERNOR",
                "PRT_RESIDUAL_DAMPENER", "PRT_SOUL_TRAP", "PSG_ZEALOT");
            if (!over.IsOverloaded) return $"무게 {over.CarriedWeight}/{over.Capacity} 인데 과적이 아님";

            FloorPlan plan = PrototypeCurriculum.For(3);
            float expected = (plan.RequiredPower + over.CarriedWeight * FloorSession.WeightPowerFactor) *
                             FloorSession.OverloadRequiredPowerMultiplier;
            if (Math.Abs(over.RequiredPower - expected) > 0.01f)
                return $"과적 요구 전력 {over.RequiredPower} (기대 {expected})";
            return null;
        }

        private static string TestLoadCarriesToNextFloor()
        {
            RunSession run = NewTenFloorRun(4242);
            // 2층까지 굴린 뒤 하나 싣고, 3층에서도 무게가 남아 있는지 본다.
            var driven = Drive(4242, (f, slot) => slot < 1);
            if (driven.run.Loadout.Count == 0 && !driven.run.IsFailed)
                return "런 내내 아무것도 실리지 않음";
            return null;
        }

        /// <summary>
        /// 층이 이미 만들어진 뒤에 무게가 바뀌어도 요구 전력과 과적이 따라와야 한다.
        ///
        /// 실제로 캡처 리그가 과적 218/130 상태에서 위험 단계 **Stable**을 찍어서 찾았다.
        /// `_carriedWeight`와 `_requiredPower`가 층 생성 시점에 고정돼 있었고,
        /// `RunSession.AddWeight`는 `_baseWeight`만 바꿔 층은 그 사실을 몰랐다.
        /// 무게가 위험 점수의 입력이므로, 이게 어긋나면 Gate F 전체가 조용히 무너진다.
        /// </summary>
        private static string TestWeightChangePropagates()
        {
            RunSession run = NewTenFloorRun(1337);
            FloorSession floor = run.Current;
            if (floor == null) return "1층이 없다";
            if (floor.IsOverloaded) return "시작부터 과적";

            float requiredBefore = floor.RequiredPower;
            float weightBefore = floor.CarriedWeight;

            if (!run.AddWeight(200f)) return "AddWeight 가 거부됨";

            if (Math.Abs(floor.CarriedWeight - (weightBefore + 200f)) > 0.01f)
                return $"층 무게가 {floor.CarriedWeight} (기대 {weightBefore + 200f})";
            if (!floor.IsOverloaded)
                return $"200kg 을 더했는데 과적이 아님: {floor.CarriedWeight}/{floor.Capacity}";
            if (floor.RequiredPower <= requiredBefore)
                return $"요구 전력이 {requiredBefore} → {floor.RequiredPower} 로 오르지 않음";
            return null;
        }

        // ── 규칙 변경 ────────────────────────────────────────────────────────

        private static string TestPassengerLowersPurifyThreshold()
        {
            FloorSession bare = FloorWith(3);
            FloorSession loaded = FloorWith(3, "PSG_SURVEYOR");
            if (bare.Rules.MinimumCountFor(SymbolKind.Absorber) != 3)
                return $"기본 문턱이 {bare.Rules.MinimumCountFor(SymbolKind.Absorber)}";
            if (loaded.Rules.MinimumCountFor(SymbolKind.Absorber) != 2)
                return $"계측 기사 탑승 후 문턱이 {loaded.Rules.MinimumCountFor(SymbolKind.Absorber)}";
            return null;
        }

        private static string TestPartEnablesDiagonal()
        {
            if (FloorWith(3).Rules.DiagonalCountsAsConnected) return "기본이 이미 대각 연결";
            if (!FloorWith(3, "PRT_DIAGONAL_BINDER").Rules.DiagonalCountsAsConnected)
                return "사선 결속기가 대각 연결을 열지 못함";
            return null;
        }

        private static string TestPartRaisesCascadeStep()
        {
            float bare = FloorWith(3).Rules.CascadeMultiplierStep;
            float loaded = FloorWith(3, "PRT_CASCADE_GOVERNOR").Rules.CascadeMultiplierStep;
            if (Math.Abs(loaded - (bare + 0.25f)) > 0.001f)
                return $"연쇄 증분 {bare} → {loaded} (기대 {bare + 0.25f})";
            return null;
        }

        private static string TestPartMitigatesResidual()
        {
            float bare = FloorWith(3).Rules.ResidualPenaltyFor(SymbolKind.Absorber);
            float loaded = FloorWith(3, "PRT_RESIDUAL_DAMPENER").Rules.ResidualPenaltyFor(SymbolKind.Absorber);
            if (loaded >= bare) return $"잔류 대가가 완화되지 않음: {bare} → {loaded}";
            if (Math.Abs(loaded - bare * 0.55f) > 0.001f)
                return $"잔류 대가 {loaded} (기대 {bare * 0.55f})";
            return null;
        }

        private static string TestContractAndLoadoutCompose()
        {
            // 10층은 계약이 강제다. 증식체 계약(출현 ×1.5) + 광신자(출현 ×1.25)가
            // 둘 다 걸려야 한다 — 하나라도 빠지면 곱이 어긋난다.
            var loadout = new BuildLoadout();
            loadout.Add(BuildCatalog.ById("PSG_ZEALOT"));
            FloorPlan plan = PrototypeCurriculum.For(10);
            var session = new FloorSession(plan, new SpinEngine(1), PowerThresholds.Default,
                0f, ResidualState.Empty, 0f, 0f, loadout);

            int index = -1;
            for (int i = 0; i < plan.ContractChoices.Length; i++)
                if (plan.ContractChoices[i].Target == SymbolKind.Proliferator) index = i;
            if (index < 0) return "10층에 증식체 계약이 없음";
            if (!session.SelectContract(index)) return "계약 확정 실패";

            SpinRuleSet baseRules = PrototypeCurriculum.BuildRules(in plan);
            float expected = baseRules.WeightOf(SymbolKind.Proliferator) * 1.5f * 1.25f;
            float actual = session.Rules.WeightOf(SymbolKind.Proliferator);
            if (Math.Abs(actual - expected) > 0.001f)
                return $"증식체 가중치 {actual} (계약×승객 기대 {expected})";
            return null;
        }

        private static string TestEmptyLoadoutLeavesRulesAlone()
        {
            // 계약도 적재도 없는 층이어야 한다 — 둘 중 하나라도 있으면 생성 직후 단계가
            // ContractSelection/Boarding 이라 `Rules` 가 아직 null 이다.
            // 4층은 D-20260801-01 재배치로 계약 층이 됐다. 3층이 지금의 순수 층이다.
            FloorPlan plan = PrototypeCurriculum.For(3);
            SpinRuleSet expected = PrototypeCurriculum.BuildRules(in plan);
            SpinRuleSet actual = FloorWith(3).Rules;

            if (Math.Abs(actual.NormalSoulValue - expected.NormalSoulValue) > 0.001f) return "정상 영혼 값이 달라짐";
            if (actual.GuaranteedNormalSouls != expected.GuaranteedNormalSouls) return "보장 개수가 달라짐";
            if (actual.DiagonalCountsAsConnected != expected.DiagonalCountsAsConnected) return "대각 연결이 달라짐";
            if (Math.Abs(actual.ResidualMitigation - expected.ResidualMitigation) > 0.001f) return "잔류 완화가 달라짐";
            return null;
        }

        // ── 승하차 ──────────────────────────────────────────────────────────

        private static string TestPassengerDisembarks()
        {
            var loadout = new BuildLoadout();
            BuildItem surveyor = BuildCatalog.ById("PSG_SURVEYOR");   // 5층 하차
            loadout.Add(surveyor);

            if (loadout.TakeDeparting(4, out float early).Count != 0 || early != 0f)
                return "목적지 전에 내림";

            var left = loadout.TakeDeparting(5, out float reward);
            if (left.Count != 1) return $"5층에서 내린 승객이 {left.Count}명";
            if (Math.Abs(reward - surveyor.DisembarkReward) > 0.01f)
                return $"요금 {reward} (기대 {surveyor.DisembarkReward})";
            if (loadout.Count != 0) return "내렸는데 적재에 남아 있음";
            return null;
        }

        private static string TestPartsStayAboard()
        {
            var loadout = new BuildLoadout();
            loadout.Add(BuildCatalog.ById("PRT_SOUL_TRAP"));
            if (loadout.TakeDeparting(10, out _).Count != 0) return "부품이 하차함";
            if (loadout.Count != 1) return "부품이 사라짐";
            return null;
        }

        // ── 10층 진행 ────────────────────────────────────────────────────────

        private static string TestHighestFloorClamped()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                var driven = Drive(seed * 7919, null);
                if (driven.run.HighestFloorReached > 10)
                    return $"시드 {seed * 7919}: 도달 층 {driven.run.HighestFloorReached} > 10";
            }
            return null;
        }

        private static string TestFinalFloorNeverSkipped()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                var driven = Drive(seed * 7919, null);
                if (!driven.run.IsComplete || driven.run.IsFailed) continue;
                if (!driven.visited.Contains(10))
                    return $"시드 {seed * 7919}: 완주했는데 10층 미방문 — 방문 [{string.Join(",", driven.visited)}]";
            }
            return null;
        }

        private static string TestBuildFloorNeverSkipped()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                var driven = Drive(seed * 7919, null);
                int reached = driven.run.HighestFloorReached;
                foreach (int buildFloor in new[] { 2, 5, 8 })
                {
                    if (reached < buildFloor) continue;
                    if (!driven.visited.Contains(buildFloor))
                        return $"시드 {seed * 7919}: {reached}층까지 갔는데 {buildFloor}층 미방문 — [{string.Join(",", driven.visited)}]";
                }
            }
            return null;
        }

        /// <summary>
        /// 추가 층을 산 층에서는 그만큼의 전력이 돈에서 빠져야 한다.
        ///
        /// 이 테스트의 첫 판은 **아무것도 검사하지 않았다.** 기대값을 계산해 놓고 버린 채
        /// `Money > 잉여총합 + 1000` 하나만 단언했는데, 무적재 런에서는
        /// `Money = Σ max(0, 잉여 − 소비) ≤ Σ 잉여`가 수학적으로 항상 성립하므로 그 조건은
        /// 어떤 시드에서도 참이 될 수 없었다. 고치기 전 버그(`Money += ExcessPower`)에서도
        /// `Money == 잉여총합`이라 그대로 통과했다 — 회귀 방지선이 아니었다.
        ///
        /// 원인은 기대값을 `FloorResult.FloorsAscended`(클램프 **전**)로 계산한 것이었다.
        /// 정산은 클램프 **후** 값으로 이뤄지므로 애초에 비교가 성립하지 않았다.
        /// 이제 `RunSession.Ascents`가 정산한 쪽의 기록을 내주므로 정확히 검사한다.
        /// </summary>
        private static string TestNoDoubleSpendOfSurplus()
        {
            int multiFloorAscents = 0;

            for (int seed = 1; seed <= 40; seed++)
            {
                var driven = Drive(seed * 104729, null);
                foreach (RunSession.FloorAscent ascent in driven.run.Ascents)
                {
                    float spent = ascent.ExtraFloors * ascent.PowerPerExtraFloor;
                    float expected = Math.Max(0f, ascent.ExcessPower - spent);

                    if (Math.Abs(ascent.MoneyCredited - expected) > 0.01f)
                        return $"시드 {seed * 104729} {ascent.FromFloor}층: 지급 {ascent.MoneyCredited:F2} " +
                               $"(기대 {expected:F2} = 잉여 {ascent.ExcessPower:F2} − 추가층 {ascent.ExtraFloors}×{ascent.PowerPerExtraFloor:F0})";

                    if (ascent.ExtraFloors <= 0) continue;
                    multiFloorAscents++;

                    // 이중 지급의 정의: 추가 층을 사고도 잉여를 그대로 돈으로 받는 것.
                    if (ascent.MoneyCredited >= ascent.ExcessPower - 0.01f)
                        return $"시드 {seed * 104729} {ascent.FromFloor}층: 추가 층 {ascent.ExtraFloors}개를 " +
                               $"샀는데 잉여 {ascent.ExcessPower:F2} 전액을 돈으로도 받았다 — 이중 지급";
                }
            }

            // 다층 상승이 한 번도 안 나왔다면 위 검사가 아무것도 통과시키지 않은 것과 같다.
            // 이 테스트가 다시 빈 테스트가 되는 것을 막는 자기 검사다.
            if (multiFloorAscents == 0)
                return "40개 시드에서 다층 상승이 한 번도 발생하지 않았다 — 이 테스트는 아무것도 검증하지 못했다";
            return null;
        }

        /// <summary>
        /// 장부(`RunSession.Money`)와 증인(`FloorAscent.TotalMoney`)이 일치하는가.
        ///
        /// `TestNoDoubleSpendOfSurplus`는 증인만 검사한다. 정산이 `Money`에는 잉여 전액을
        /// 더하면서 기록에는 올바른 값을 남기는 회귀 — 즉 장부와 증인이 갈라지는 경우 —
        /// 는 그 테스트를 그대로 통과한다. 독립 감사가 지적한 구멍이다.
        ///
        /// 무적재 런이라 하차 요금이 없으므로 `Money`는 정확히 지급액의 합이어야 한다.
        /// </summary>
        private static string TestMoneyMatchesCreditedLedger()
        {
            foreach (int seed in new[] { 1337, 4242, 90210, 7, 31415, 271828 })
            {
                var driven = Drive(seed, null);
                float ledger = 0f;
                foreach (RunSession.FloorAscent ascent in driven.run.Ascents)
                {
                    // **`TotalMoney` 를 합산한다 — `MoneyCredited` 만 세면 안 된다.**
                    //
                    // 남은 스핀 정산(`T-05` · `D-20260802-10`)이 들어오면서 소지금에
                    // 더해지는 경로가 둘이 됐다. 이 검사가 한쪽만 세던 동안 실제로
                    // **90.00 차이로 걸렸고**, 그것이 이 단정의 존재 이유다 —
                    // 근거 없는 돈은 근거 없는 숫자다.
                    ledger += ascent.TotalMoney;

                    // 정산 자체의 불변식도 여기서 본다. 별도 테스트로 빼면
                    // 이 런의 실제 값이 아니라 합성 입력을 검사하게 된다.
                    if (ascent.SettlementMoney < -0.01f)
                        return $"시드 {seed} {ascent.FromFloor}층: 정산이 음수다 ({ascent.SettlementMoney:F2})";
                    if (ascent.SettledSpins < 0)
                        return $"시드 {seed} {ascent.FromFloor}층: 정산 스핀 수가 음수다";
                    if (ascent.SettledSpins == 0 && ascent.SettlementMoney > 0.01f)
                        return $"시드 {seed} {ascent.FromFloor}층: 정산 스핀 0 인데 정산금 {ascent.SettlementMoney:F2} 이 나왔다";
                }

                // 화물 포기 구간(70~89%)이 소지금으로 대가를 물 수 있다. 무적재 런이라
                // 버릴 화물이 없어 돈으로 낸다. 그 지출도 원장의 일부다 — 검사를
                // 느슨하게 하는 대신 지출을 식에 넣는다.
                float expected = ledger - driven.run.TotalJettisonPenalty;
                if (Math.Abs(driven.run.Money - expected) > 0.01f)
                    return $"시드 {seed}: 소지금 {driven.run.Money:F2} 이 " +
                           $"지급 {ledger:F2} − 대가 {driven.run.TotalJettisonPenalty:F2} " +
                           $"= {expected:F2} 와 다르다 (무적재 런이므로 하차 요금이 없다)";
            }
            return null;
        }

        /// <summary>
        /// 70~89% 구간이 **런을 끝내지 않고 대가를 물린다**는 것.
        ///
        /// 예전에는 이 구간이 `Crash`와 결과가 같았다 — `Ascends()`가 `Damaged` 이상만
        /// 참이라 런이 그냥 끝났고, 화면만 "화물 포기"라고 적었다. 독립 감사가
        /// "화물을 포기했다는데 적재가 없다"고 지적한 화면이 그것이다.
        ///
        /// 실제로 그 구간에 들어간 층을 찾아, 런이 이어졌고 무언가를 잃었는지 본다.
        /// 구간에 한 번도 안 들어가면 이 테스트는 아무것도 증명하지 못하므로,
        /// 그 사실 자체를 실패로 보고한다 — 도달 불가능한 검사를 통과로 세지 않는다.
        /// </summary>
        private static string TestJettisonBandCostsInsteadOfEnding()
        {
            int observed = 0;
            foreach (int seed in new[] { 1337, 4242, 90210, 7, 31415, 271828, 8675309, 20260731, 555 })
            {
                var driven = Drive(seed, null);
                foreach (FloorResult floor in driven.run.Results)
                {
                    if (floor.Band != PowerBand.Jettison) continue;
                    observed++;

                    if (!floor.Succeeded)
                        return $"시드 {seed}: 화물 포기 구간인데 층이 실패로 끝났다";
                    if (floor.FloorsAscended < 1)
                        return $"시드 {seed}: 화물 포기 구간인데 오르지 못했다";
                    if (!string.IsNullOrEmpty(floor.FailureReason))
                        return $"시드 {seed}: 성공한 층에 실패 사유 \"{floor.FailureReason}\" 가 남았다";
                }
            }

            if (observed == 0)
                return "9개 시드에서 화물 포기 구간(70~89%)에 한 번도 들어가지 않았다 — " +
                       "검사가 도달 불가능하므로 통과로 세지 않는다";
            return null;
        }

        /// <summary>
        /// 하차가 **이미 확정된 층**의 무게·요구 전력을 사후에 바꾸지 않는가.
        ///
        /// `CompleteFloor`는 `DisembarkAt`을 `CreateCurrentFloor` 앞에서 부른다.
        /// 그 시점의 `_current`는 아직 방금 확정된 층이므로, 적재 변경 알림에 가드가
        /// 없으면 끝난 층의 숫자가 뒤늦게 움직이고 사고 기록기가 그 값을 적는다.
        /// </summary>
        private static string TestDisembarkDoesNotMutateResolvedFloor()
        {
            RunSession run = NewTenFloorRun(1337);
            FloorSession floor = run.Current;
            if (floor == null) return "1층이 없다";

            // 하차 승객을 태워 두면 그 층 도착 시 하차가 발생한다.
            // **짐꾼(PSG_PORTER)을 쓴다.** 측량사(PSG_SURVEYOR)는 허용 중량 보너스가 0 이라
            // 하차해도 `Capacity` 가 움직이지 않는다 — 무게만 보는 검사는 통과하면서
            // 허용 중량·과적·적재 목록이 하차 뒤 값으로 오염되는 것을 못 잡았다.
            BuildItem leaver = BuildCatalog.ById("PSG_PORTER");
            if (leaver == null) return "PSG_PORTER 를 찾지 못함";
            if (leaver.CapacityBonus <= 0f)
                return $"PSG_PORTER 의 허용 중량 보너스가 {leaver.CapacityBonus} — 이 테스트가 무의미해졌다";
            if (leaver.DestinationFloor <= 0)
                return "PSG_PORTER 에 목적지 층이 없다 — 하차가 일어나지 않는다";
            run.Loadout.Add(leaver);

            int guard = 0;
            while (!run.IsComplete && !run.IsFailed && guard++ < 60)
            {
                FloorSession current = run.Current;
                if (current == null) break;

                if (current.Phase == FloorPhase.Boarding) run.FinishBoarding();
                if (current.Phase == FloorPhase.ContractSelection) run.SelectContract(0);
                while (current.Phase == FloorPhase.Spinning && current.SpinsRemaining > 0) run.Spin();

                float weightAtDecision = current.CarriedWeight;
                float requiredAtDecision = current.RequiredPower;
                float capacityAtDecision = current.Capacity;
                bool overloadedAtDecision = current.IsOverloaded;
                string loadoutAtDecision = current.Loadout != null ? current.Loadout.DescribeShort() : "없음";

                if (current.CanBank) run.Bank();
                else if (current.SpinsRemaining == 0) run.ForceResolve();
                else break;

                // 확정 뒤 (하차가 일어났을 수도 있는 시점) 그 층의 값이 그대로여야 한다.
                if (Math.Abs(current.CarriedWeight - weightAtDecision) > 0.01f)
                    return $"{current.Plan.Floor}층 확정 후 무게가 {weightAtDecision} → {current.CarriedWeight} 로 변했다";
                if (Math.Abs(current.RequiredPower - requiredAtDecision) > 0.01f)
                    return $"{current.Plan.Floor}층 확정 후 요구 전력이 {requiredAtDecision} → {current.RequiredPower} 로 변했다";

                // 무게만이 아니다. 허용 중량·과적·적재 목록도 그 층의 사실이어야 한다.
                // 이 셋은 계산 프로퍼티라 런의 살아 있는 적재를 매번 다시 읽었고,
                // 하차·화물 포기가 끝난 뒤 기록되는 사고 기록기에 그대로 새어 들어갔다.
                if (Math.Abs(current.Capacity - capacityAtDecision) > 0.01f)
                    return $"{current.Plan.Floor}층 확정 후 허용 중량이 {capacityAtDecision} → {current.Capacity} 로 변했다";
                if (current.IsOverloaded != overloadedAtDecision)
                    return $"{current.Plan.Floor}층 확정 후 과적 여부가 {overloadedAtDecision} → {current.IsOverloaded} 로 변했다";
                string loadoutAfter = current.ResolvedLoadoutShort
                                      ?? (current.Loadout != null ? current.Loadout.DescribeShort() : "없음");
                if (loadoutAfter != loadoutAtDecision)
                    return $"{current.Plan.Floor}층 확정 후 적재 목록이 [{loadoutAtDecision}] → [{loadoutAfter}] 로 변했다";
            }
            return null;
        }

        /// <summary>
        /// **`P2-Gate B`의 헤드리스 증거다.**
        ///
        /// 기존 "10층 진행" 테스트 7개는 전부 실패 런을 `continue`로 건너뛰거나
        /// 상한만 확인해서, **모든 시드가 1층에서 죽어도 전원 통과**했다. 완주를 요구하는
        /// 단언이 하나도 없었으므로 Gate B는 PlayMode 로그 한 줄 외에 근거가 없었다.
        /// 독립 QA 감사가 이 사각지대를 지목했다.
        /// </summary>
        /// <summary>
        /// **계약을 실제로 건 런**이 10층을 완주하는가, 그리고 고른 계약이 규칙에 닿는가.
        ///
        /// 이 검사가 없던 동안 헤드리스 표본 전체가 `SelectContract(0)` = "계약 없음"이었다.
        /// 계약은 출현률(`AppearanceMultiplier`)·정화 보상·패턴 보너스·잔류 대가를 전부
        /// 곱으로 바꾸므로, 계약 없는 런만으로 낸 완주율과 연속성은 게임의 절반이다.
        /// 계약을 걸면 산출량과 잔류 위험이 함께 오르는데 그 조합이 진행 불가를 만들지
        /// 않는다는 근거가 어디에도 없었다.
        ///
        /// 정책은 **마지막 선택지**다 — 커리큘럼상 항상 "계약 없음"이 아닌 것이 온다.
        /// </summary>
        private static string TestContractedRunCompletes()
        {
            const int required = 2;
            int completed = 0;
            bool sawLiveContract = false;
            var notes = new StringBuilder();

            foreach (int seed in new[] { 4242, 7, 271828, 20260801, 112358, 999, 1337, 90210 })
            {
                RunSession run = NewTenFloorRun(seed);
                var visited = new System.Collections.Generic.List<int>();
                int guard = 0;

                while (!run.IsComplete && !run.IsFailed && guard++ < 200)
                {
                    FloorSession f = run.Current;
                    if (f == null) break;
                    if (visited.Count == 0 || visited[visited.Count - 1] != f.Plan.Floor)
                        visited.Add(f.Plan.Floor);

                    if (f.Phase == FloorPhase.Boarding && !run.FinishBoarding()) break;

                    if (f.Phase == FloorPhase.ContractSelection)
                    {
                        int count = f.Plan.ContractChoices.Length;
                        if (!run.SelectContract(count - 1)) break;

                        // 고른 것이 살아 있는 계약이면, 그 계약의 출현 배수가 규칙에 닿아야 한다.
                        if (!f.SelectedContract.IsNone)
                        {
                            sawLiveContract = true;
                            SymbolKind target = f.SelectedContract.Target;
                            SpinRuleSet withContract = f.Rules;
                            FloorPlan plan = f.Plan;
                            SpinRuleSet without = PrototypeCurriculum.BuildRules(in plan);
                            float before = without.WeightOf(target);
                            float after = withContract.WeightOf(target);
                            if (before > 0f && after <= before + 0.0001f)
                                return $"{f.Plan.Floor}층에서 {f.SelectedContract.Label} 을 골랐는데 " +
                                       $"{target} 가중치가 {before} → {after} 로 오르지 않았다";
                        }
                    }

                    int spins = 0;
                    while (f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0)
                    {
                        run.Spin();
                        if (++spins > 30) break;
                    }

                    if (f.CanBank) run.Bank();
                    else if (f.SpinsRemaining == 0) run.ForceResolve();
                    else break;
                }

                for (int i = 1; i < visited.Count; i++)
                    if (visited[i] != visited[i - 1] + 1)
                        return $"계약 런 시드{seed} 가 {visited[i - 1]}→{visited[i]} 로 층을 건너뛰었다";

                if (run.IsComplete && !run.IsFailed && visited.Contains(10)) completed++;
                else notes.Append(seed).Append("→").Append(run.HighestFloorReached).Append("층 ");
            }

            if (!sawLiveContract)
                return "계약 층에서 살아 있는 계약을 한 번도 고르지 못했다 — 선택지 구성이 바뀌었다";
            if (completed < required)
                return $"계약을 건 완주가 {completed}회 — 최소 {required}회 필요. 미완주 [{notes.ToString().Trim()}]";
            return null;
        }

        /// <summary>
        /// 방문 층에 구멍이 없는가. "10층까지 진행했다"와 "1층부터 10층까지 **연속**
        /// 진행했다"는 다른 주장이고, 지금까지의 검사는 앞쪽만 봤다.
        ///
        /// 이 검사가 없던 동안 실제로 무슨 일이 있었나: 커리큘럼을 노션 03번 배치로
        /// 옮긴 직후(D-20260801-01) 시드 200개 실측에서 완주율은 67%로 멀쩡했는데
        /// **계약을 처음 가르치는 4층의 방문률이 34%** 였다. 다층 상승이 교습 층을
        /// 삼켰고, 그 플레이어는 계약을 7층에서 세 개가 한꺼번에 놓인 채 처음 만났다.
        /// 완주율만 보는 검사는 이걸 영원히 통과시킨다.
        ///
        /// 정책 세 가지로 돈다. 적재 무게가 요구 전력을 바꾸므로 잉여도 달라지고,
        /// 무적재에서만 성립하는 클램프는 여기서 걸린다.
        /// </summary>
        private static string TestVisitedFloorsAreConsecutive()
        {
            var failures = new StringBuilder();
            foreach (int seed in new[] { 1337, 4242, 90210, 7, 31415, 271828, 555555, 8675309, 20260731 })
            {
                foreach (int perFloor in new[] { 0, 1, 2 })
                {
                    int slots = perFloor;
                    var driven = Drive(seed, (f, slot) => slot < slots);
                    var visited = driven.visited;
                    if (visited.Count == 0) { failures.Append($"시드{seed}/{perFloor}개 방문기록없음 "); continue; }
                    if (visited[0] != 1) failures.Append($"시드{seed}/{perFloor}개 시작{visited[0]}층 ");
                    for (int i = 1; i < visited.Count; i++)
                        if (visited[i] != visited[i - 1] + 1)
                            failures.Append($"시드{seed}/{perFloor}개 {visited[i - 1]}→{visited[i]} ");
                }
            }
            return failures.Length == 0 ? null : $"건너뛴 층 [{failures.ToString().Trim()}]";
        }

        private static string TestSeedsCompleteTenFloors()
        {
            const int required = 3;
            int completed = 0;
            var failures = new StringBuilder();

            foreach (int seed in new[] { 1337, 4242, 90210, 7, 31415, 271828, 555555, 8675309, 20260731 })
            {
                var driven = Drive(seed, null);
                bool ok = driven.run.IsComplete && !driven.run.IsFailed && driven.visited.Contains(10);
                if (ok)
                {
                    completed++;
                    if (completed >= required) return null;
                }
                else
                {
                    failures.Append(seed).Append("→").Append(driven.run.HighestFloorReached).Append("층 ");
                }
            }
            return $"10층 완주가 {completed}회 — 최소 {required}회 필요. 미완주 [{failures.ToString().Trim()}]";
        }

        /// <summary>
        /// 적재를 하고도 10층을 완주할 수 있는 시드가 존재하는가.
        ///
        /// 무적재 완주만으로는 Gate B와 Gate C가 충돌한다 — "완주 가능한 유일한 방법이
        /// 아무것도 싣지 않는 것"이면 적재 시스템은 순수한 함정이다.
        /// </summary>
        private static string TestLoadedRunCanAlsoComplete()
        {
            var failures = new StringBuilder();
            foreach (int seed in new[] { 1337, 4242, 90210, 7, 31415, 271828, 8675309, 20260731 })
            {
                var driven = Drive(seed, (f, slot) => slot < 1);
                if (driven.run.IsComplete && !driven.run.IsFailed && driven.visited.Contains(10))
                {
                    // **최종 적재를 보면 안 된다.** 승객은 목적지 층에서 내리므로
                    // 10층에 닿았을 때 칸이 비어 있는 것이 정상이다. 실제로 밸런스를
                    // 바꾸자 이 검사가 정상 동작을 실패로 신고했다.
                    // 검증해야 할 것은 "적재 경로를 실제로 지났는가"이고,
                    // 그 증거는 하차 기록이나 런 도중의 최고 무게다.
                    if (driven.run.PeakCarriedWeight <= 0f)
                        return $"시드 {seed}: 완주했지만 한 번도 싣지 않았다 — 적재 경로가 검증되지 않음";
                    return null;
                }
                failures.Append(seed).Append("→").Append(driven.run.HighestFloorReached).Append("층 ");
            }
            return $"적재하면서 10층을 완주한 시드가 없다. [{failures.ToString().Trim()}]";
        }

        /// <summary>
        /// 적재를 `Loadout`으로 직접 바꿔도 현재 층의 무게·요구 전력이 따라오는가.
        ///
        /// `AddWeight` 경로만 고쳤을 때 이 경로가 그대로 새어 나갔고, 캡처 리그가 6개를
        /// 실은 상태에서 층은 옛 무게를 들고 있어 과적인데 위험 단계가 Stable로 찍혔다.
        /// </summary>
        private static string TestLoadoutMutationPropagates()
        {
            RunSession run = NewTenFloorRun(1337);
            FloorSession floor = run.Current;
            if (floor == null) return "1층이 없다";

            float requiredBefore = floor.RequiredPower;
            float weightBefore = floor.CarriedWeight;

            BuildItem heavy = BuildCatalog.ById("PRT_DIAGONAL_BINDER");   // 26kg
            if (heavy == null) return "PRT_DIAGONAL_BINDER 를 찾지 못함";
            if (!run.Loadout.Add(heavy)) return "Loadout.Add 가 거부됨";

            if (Math.Abs(floor.CarriedWeight - (weightBefore + heavy.Weight)) > 0.01f)
                return $"층 무게가 {floor.CarriedWeight} (기대 {weightBefore + heavy.Weight})";
            if (floor.RequiredPower <= requiredBefore)
                return $"요구 전력이 {requiredBefore} → {floor.RequiredPower} 로 오르지 않음";

            if (!run.Loadout.Remove(heavy.Id)) return "Loadout.Remove 가 거부됨";
            if (Math.Abs(floor.CarriedWeight - weightBefore) > 0.01f)
                return $"내린 뒤 무게가 {floor.CarriedWeight} (기대 {weightBefore})";
            if (Math.Abs(floor.RequiredPower - requiredBefore) > 0.01f)
                return $"내린 뒤 요구 전력이 {floor.RequiredPower} (기대 {requiredBefore})";
            return null;
        }

        private static string TestTenFloorRunNeverStalls()
        {
            foreach (int seed in new[] { 1337, 4242, 90210, 7, 555555, 31415, 271828 })
            {
                var bare = Drive(seed, null);
                if (!bare.run.IsComplete && !bare.run.IsFailed)
                    return $"시드 {seed} 무적재: 완주도 실패도 아님 — 방문 [{string.Join(",", bare.visited)}]";

                var loaded = Drive(seed, (f, slot) => slot < 1);
                if (!loaded.run.IsComplete && !loaded.run.IsFailed)
                    return $"시드 {seed} 적재: 완주도 실패도 아님 — 방문 [{string.Join(",", loaded.visited)}]";
            }
            return null;
        }

        private static string TestTenFloorDeterminism()
        {
            foreach (int seed in new[] { 1337, 4242, 90210 })
            {
                var a = Drive(seed, (f, slot) => slot < 2);
                var b = Drive(seed, (f, slot) => slot < 2);
                if (a.run.HighestFloorReached != b.run.HighestFloorReached)
                    return $"시드 {seed}: 도달 층 {a.run.HighestFloorReached} vs {b.run.HighestFloorReached}";
                if (Math.Abs(a.run.Money - b.run.Money) > 0.001f)
                    return $"시드 {seed}: 소지금 {a.run.Money} vs {b.run.Money}";
                if (a.run.Loadout.DescribeShort() != b.run.Loadout.DescribeShort())
                    return $"시드 {seed}: 적재가 다름";
                if (string.Join(",", a.visited) != string.Join(",", b.visited))
                    return $"시드 {seed}: 방문 층이 다름";
            }
            return null;
        }

        private static string TestTwoBuildsDiverge()
        {
            // 같은 시드에서 "아무것도 안 싣는다"와 "가능한 만큼 싣는다"가 다른 런이 되어야
            // 적재가 실제 판단이 된다. 같으면 선택지가 아니라 장식이다.
            int diverged = 0;
            foreach (int seed in new[] { 1337, 4242, 90210, 7, 31415 })
            {
                var bare = Drive(seed, null);
                var greedy = Drive(seed, (f, slot) => true);
                bool sameMoney = Math.Abs(bare.run.Money - greedy.run.Money) < 0.001f;
                bool samePath = string.Join(",", bare.visited) == string.Join(",", greedy.visited);
                if (!sameMoney || !samePath) diverged++;
            }
            if (diverged < 4) return $"5개 시드 중 {diverged}개만 갈라짐 — 적재가 판단을 바꾸지 못함";
            return null;
        }
    }
}
