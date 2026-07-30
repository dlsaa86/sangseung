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

            // ── 무게와 과적 ──
            Run("실은 무게가 요구 전력을 올린다", TestLoadRaisesRequirement, ref passed, ref failed, report);
            Run("짐꾼이 허용 중량을 올린다", TestPorterRaisesCapacity, ref passed, ref failed, report);
            Run("과적이 요구 전력에 배수를 건다", TestOverloadMultiplier, ref passed, ref failed, report);
            Run("적재 무게가 다음 층으로 이어진다", TestLoadCarriesToNextFloor, ref passed, ref failed, report);
            Run("무게 변경이 현재 층에 즉시 반영된다", TestWeightChangePropagates, ref passed, ref failed, report);

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
            Run("추가 층 전력이 돈으로 중복 지급되지 않는다", TestNoDoubleSpendOfSurplus, ref passed, ref failed, report);
            Run("10층 연속 런에 진행 불가 상태가 없다", TestTenFloorRunNeverStalls, ref passed, ref failed, report);
            Run("동일 시드·동일 선택이 동일 결과", TestTenFloorDeterminism, ref passed, ref failed, report);
            Run("서로 다른 두 빌드가 결과를 바꾼다", TestTwoBuildsDiverge, ref passed, ref failed, report);

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
                    if (!run.SelectContract(0)) break;

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
            FloorPlan plan = PrototypeCurriculum.For(4);
            SpinRuleSet expected = PrototypeCurriculum.BuildRules(in plan);
            SpinRuleSet actual = FloorWith(4).Rules;

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

        private static string TestNoDoubleSpendOfSurplus()
        {
            // 추가 층을 산 층에서는 그만큼의 전력이 돈에서 빠져야 한다.
            for (int seed = 1; seed <= 40; seed++)
            {
                RunSession run = NewTenFloorRun(seed * 104729);
                float money = 0f;
                var driven = Drive(seed * 104729, null);
                foreach (FloorResult result in driven.run.Results)
                {
                    int extra = Math.Max(0, result.FloorsAscended - result.Ascent.BaseFloors);
                    money += Math.Max(0f, result.ExcessPower - extra * result.Ascent.PowerPerExtraFloor);
                }
                // 하차 요금이 더해지므로 돈은 이 값 이상이어야 하고, 잉여 전체보다는 작아야 한다.
                float naive = 0f;
                foreach (FloorResult result in driven.run.Results) naive += result.ExcessPower;
                if (driven.run.Money > naive + 1000f)
                    return $"시드 {seed * 104729}: 돈 {driven.run.Money} 이 잉여 총합 {naive} 을 크게 넘음";
            }
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
