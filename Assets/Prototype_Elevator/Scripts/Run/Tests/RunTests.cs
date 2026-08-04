using System;
using System.Text;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Spin;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>Headless floor-loop checks, available from the Unity menu.</summary>
    public static class RunTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("계약 선택 전 Spin 거부", TestContractGate, ref passed, ref failed, report);
            Run("스핀 소진 후 추가 Spin 거부", TestSpinExhaustion, ref passed, ref failed, report);
            Run("요구 전력 달성 전에는 CanBank 거짓", TestCanBankGate, ref passed, ref failed, report);
            Run("확정 후 추가 Spin 거부", TestBankClosesFloor, ref passed, ref failed, report);
            Run("흡수체 잔류가 다음 스핀 전력을 차감", TestResidualCarry, ref passed, ref failed, report);
            Run("추가 스핀 선택 시 앤티 즉시 차감", TestAnteImmediateCharge, ref passed, ref failed, report);
            Run("연속 추가 스핀 앤티 비율 상승", TestAnteEscalation, ref passed, ref failed, report);
            Run("앤티로 요구 전력 아래 하락 가능", TestAnteCanLose, ref passed, ref failed, report);
            Run("PendingAnte와 실제 차감액 일치", TestPendingAnteMatchesCharge, ref passed, ref failed, report);
            Run("무게 증가가 RequiredPower 증가", TestWeightRaisesRequirement, ref passed, ref failed, report);
            Run("동일 시드·선택 결정론", TestDeterminism, ref passed, ref failed, report);

            // ── UP-POWER-07: 프로파일 값이 **게임을 실제로 바꾸는가** ──
            // 「읽는다」가 아니라 「바꾸면 결과가 달라진다」를 묻는다. 값을 망가뜨렸을 때
            // 실패하지 않는 검사는 그 값이 죽어 있다는 사실을 통과로 기록한다.
            Run("과수확 상한이 추가 스핀을 실제로 막는다", TestProfileExtraSpinCap, ref passed, ref failed, report);
            Run("해금 임계가 CanBank와 독립으로 작동", TestProfileUnlockThreshold, ref passed, ref failed, report);
            Run("판돈 비율이 프로파일에서 온다", TestProfileAnteRatio, ref passed, ref failed, report);

            // ── T-05: 남은 스핀 정산이 **주입되고 소멸하는가** ──
            // `SettlementTests` 는 스냅샷의 산수만 검사한다 — 그 산수가 옳아도
            // 층이 그 스냅샷을 **받지 못하면** 게임에서는 아무 일도 일어나지 않는다.
            // 실제로 그랬다: `_settlement` 에 대입하는 코드가 어디에도 없었고,
            // 프로파일을 만들어 배선해도 한 자리도 바뀌지 않았다. 열 개의 통과한
            // 검사가 그 사실을 하나도 잡지 못했다.
            Run("정산 수치가 층에 실제로 주입된다", TestSettlementInjected, ref passed, ref failed, report);
            Run("과수확을 고르면 정산 권리가 소멸한다", TestSettlementForfeited, ref passed, ref failed, report);
            Run("정산 소멸은 스핀 결과가 아니라 선택 시점에 일어난다",
                TestSettlementForfeitedAtChoice, ref passed, ref failed, report);

            // ── Hero Slice (CURRENT_PHASE.md) ──
            Run("Hero Slice 1층에 계약 3종·저항 2종", TestHeroSliceShape, ref passed, ref failed, report);
            Run("Hero Slice 계약 미선택 시 스핀 거부", TestHeroSliceContractGate, ref passed, ref failed, report);
            Run("Hero Slice 런은 1층 확정으로 끝난다", TestHeroSliceEndsAfterOneFloor, ref passed, ref failed, report);
            Run("Hero Slice 요구 전력 달성이 과수확 여지를 남김", TestHeroSliceLeavesSpins, ref passed, ref failed, report);
            Run("10층 커리큘럼이 보존됨", TestTenFloorCurriculumIntact, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Run Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        private static void Run(string name, Func<string> test,
            ref int passed, ref int failed, StringBuilder report)
        {
            try
            {
                string failure = test();
                if (string.IsNullOrEmpty(failure))
                {
                    passed++;
                    report.AppendLine($"  PASS  {name}");
                }
                else
                {
                    failed++;
                    report.AppendLine($"  FAIL  {name} — {failure}");
                }
            }
            catch (Exception exception)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외: {exception.Message}");
            }
        }

        private static string TestContractGate()
        {
            FloorSession session = NewSession(1, 100f, true, 1);
            SpinResolution rejected = session.Spin();
            if (session.SpinsUsed != 0 || rejected.Steps != null)
                return "계약 선택 전 스핀이 진행됨";
            if (!session.SelectContract(0) || session.Phase != FloorPhase.Spinning)
                return "계약 선택 후 Spinning 진입 실패";
            return null;
        }

        private static string TestSpinExhaustion()
        {
            FloorSession session = NewSession(2, 1f, false, 1);
            if (!session.TrySpin(out _)) return "첫 스핀 거부";
            int used = session.SpinsUsed;
            if (session.PushYourLuck()) return "스핀 소진 후 push 허용";
            session.Spin();
            if (session.SpinsUsed != used) return "스핀 소진 후 사용량 증가";
            return null;
        }

        private static string TestCanBankGate()
        {
            FloorSession session = NewSession(3, 100000000f, false, 1);
            session.Spin();
            if (session.CanBank) return "요구 전력 미달인데 CanBank=true";

            FloorSession achieved = FindFirstSessionWithPower(4, 1f);
            if (achieved == null || !achieved.CanBank) return "달성 가능한 케이스를 찾지 못함";
            return null;
        }

        private static string TestBankClosesFloor()
        {
            FloorSession session = FindFirstSessionWithPower(5, 1f);
            if (session == null) return "달성 가능한 케이스를 찾지 못함";
            if (session.Bank() == null || session.Phase != FloorPhase.Resolved)
                return "Bank가 층을 확정하지 않음";
            int used = session.SpinsUsed;
            session.Spin();
            if (session.SpinsUsed != used) return "확정 후 스핀이 진행됨";
            return null;
        }

        private static string TestResidualCarry()
        {
            for (int seed = 0; seed < 5000; seed++)
            {
                FloorSession session = NewSession(seed, 100000000f, false, 5);
                SpinResolution first = session.Spin();
                if (first.Residual.StoredPowerLoss <= 0f || session.Phase != FloorPhase.Spinning)
                    continue;

                SpinResolution second = session.Spin();
                if (second.NetPower >= second.GrossPower ||
                    second.Residual.StoredPowerLoss < 0f)
                    return null;
            }
            return "잔류 흡수체를 남기는 시드를 찾지 못함";
        }

        private static string TestAnteImmediateCharge()
        {
            FloorSession session = NewNormalOnlySession(11, 70f, 5);
            session.Spin();
            float before = session.Power;
            float ante = session.PendingAnte;
            if (ante <= 0f) return "공개할 앤티가 0";
            if (!session.PushYourLuck()) return "추가 스핀 선택 거부";
            if (Math.Abs((before - session.Power) - ante) > 0.001f)
                return $"차감 {before - session.Power}, 공개 앤티 {ante}";
            session.Spin();
            FloorResult result = session.Bank();
            if (result == null || Math.Abs(result.TotalAnte - ante) > 0.001f)
                return "FloorResult에 총 판돈이 기록되지 않음";
            return null;
        }

        private static string TestAnteEscalation()
        {
            FloorSession session = NewNormalOnlySession(12, 70f, 5);
            session.Spin();
            // 첫 스핀 직후의 전력을 그때 읽는다. 예전에는 90 을 박아 뒀는데
            // (정상 영혼 9개 × 10), `NormalSoulValue` 가 14 로 바뀌자 깨졌다.
            // 이 테스트가 지키는 것은 "앤티 = 그 시점 전력 × 비율"이라는 관계다.
            float powerAfterFirst = session.Power;
            float firstAnte = session.PendingAnte;
            if (!session.PushYourLuck()) return "첫 추가 스핀 선택 거부";
            session.Spin();
            float secondAnte = session.PendingAnte;
            float expectedSecondRatio = FloorSession.DefaultAnteRatio *
                (1f + FloorSession.DefaultAnteEscalation);
            if (Math.Abs(firstAnte - powerAfterFirst * FloorSession.DefaultAnteRatio) > 0.001f ||
                Math.Abs(secondAnte - session.Power * expectedSecondRatio) > 0.001f)
                return $"앤티 상승이 잘못됨: {firstAnte} (전력 {powerAfterFirst}), {secondAnte}";
            if (secondAnte <= firstAnte) return "연속 추가 스핀 앤티가 오르지 않음";
            return null;
        }

        private static string TestAnteCanLose()
        {
            // Find a normal default-pool first spin that banks, then make the
            // second spin's disclosed residual cost large enough to expose the
            // ante risk. Pool construction still goes through BuildRules.
            for (int seed = 0; seed < 10000; seed++)
            {
                FloorSession session = NewSession(seed, 70f, false, 5);
                session.Spin();
                if (!session.CanBank) continue;
                if (!session.PushYourLuck()) continue;
                session.Rules.AbsorberResidualPowerLoss = 100f;
                session.Spin();
                if (session.Power < session.RequiredPower && session.TotalAnte > 0f)
                    return null;
            }
            return "앤티 포함 추가 스핀 손실 케이스를 찾지 못함";
        }

        private static string TestPendingAnteMatchesCharge()
        {
            FloorSession session = NewNormalOnlySession(13, 70f, 5);
            session.Spin();
            float expected = session.Power * FloorSession.DefaultAnteRatio;
            float pending = session.PendingAnte;
            if (Math.Abs(pending - expected) > 0.001f)
                return $"PendingAnte {pending}, 기대 {expected}";
            float before = session.Power;
            session.PushYourLuck();
            if (Math.Abs(before - session.Power - pending) > 0.001f)
                return "공개 앤티와 실제 차감액 불일치";
            return null;
        }

        private static string TestWeightRaisesRequirement()
        {
            FloorSession light = NewSession(10, 100f, false, 1);
            FloorSession heavy = NewSession(10, 101f, false, 1);
            if (heavy.RequiredPower <= light.RequiredPower)
                return $"경량 {light.RequiredPower}, 중량 {heavy.RequiredPower}";
            FloorSession overloaded = NewSession(10, 200f, false, 1);
            if (overloaded.RequiredPower <= heavy.RequiredPower)
                return "과적 요구 전력이 증가하지 않음";
            return null;
        }

        private static string TestDeterminism()
        {
            FloorSession first = NewSession(7341, 10f, true, 5);
            FloorSession second = NewSession(7341, 10f, true, 5);
            if (!first.SelectContract(0) || !second.SelectContract(0)) return "계약 선택 실패";

            for (int i = 0; i < 5; i++)
            {
                SpinResolution left = first.Spin();
                SpinResolution right = second.Spin();
                if (!left.InitialBoard.Equals(right.InitialBoard) || left.NetPower != right.NetPower ||
                    left.Residual.StoredPowerLoss != right.Residual.StoredPowerLoss)
                    return $"스핀 {i + 1} 결과 불일치";
                if (first.Phase == FloorPhase.Decision)
                {
                    if (first.CanBank && second.CanBank)
                    {
                        first.PushYourLuck();
                        second.PushYourLuck();
                    }
                    else break;
                }
            }
            return null;
        }

        // ── Hero Slice ──

        private static string TestHeroSliceShape()
        {
            FloorPlan plan = PrototypeCurriculum.HeroSlice;
            if (!plan.IsValid) return "층 계획이 유효하지 않음(핵심 질문·스핀·요구 전력 확인)";
            if (plan.Floor != 1) return $"층 번호 {plan.Floor}";
            if (plan.ContractChoices == null || plan.ContractChoices.Length != 3)
                return $"계약 선택지 {plan.ContractChoices?.Length ?? 0}종, 기대 3";

            // CURRENT_PHASE §2.1은 계약 2종(흡수체·증식체)을 모두 요구한다.
            bool hasNone = false, hasAbsorber = false, hasProliferator = false;
            foreach (ResistanceContract c in plan.ContractChoices)
            {
                if (c.IsNone) hasNone = true;
                else if (c.Target == SymbolKind.Absorber) hasAbsorber = true;
                else if (c.Target == SymbolKind.Proliferator) hasProliferator = true;
            }
            if (!hasNone || !hasAbsorber || !hasProliferator)
                return "계약 없음/흡수체/증식체 중 빠진 것이 있음";

            // 저항체 2종이 모두 풀에 있어야 잔류 두 종류를 다 보여줄 수 있다.
            SpinRuleSet rules = PrototypeCurriculum.BuildRules(in plan);
            if (rules.WeightOf(SymbolKind.Absorber) <= 0f || rules.WeightOf(SymbolKind.Proliferator) <= 0f)
                return "저항체 2종이 심볼 풀에 모두 들어 있지 않음";
            if (rules.WeightOf(SymbolKind.NormalSoul) <= 0f)
                return "정상 영혼이 심볼 풀에 없음";
            return null;
        }

        private static string TestHeroSliceContractGate()
        {
            // Gate B: "계약 미선택 상태에서는 스핀할 수 없다."
            var run = new RunSession(4242, 0f, 0f,
                FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation,
                new HeroSliceFloorSource());

            if (run.Current.Phase != FloorPhase.ContractSelection)
                return $"1층 진입 단계 {run.Current.Phase}, 기대 ContractSelection";
            run.Spin();
            if (run.Current.SpinsUsed != 0) return "계약 전에 스핀이 진행됨";
            if (!run.SelectContract(1)) return "계약 선택 실패";
            if (run.Current.Phase != FloorPhase.Spinning) return "계약 후 Spinning 진입 실패";
            if (run.Current.SelectedContract.Target != SymbolKind.Absorber)
                return $"선택된 계약 {run.Current.SelectedContract.Label}";
            run.Spin();
            if (run.Current.SpinsUsed != 1) return "계약 후 스핀이 진행되지 않음";
            return null;
        }

        private static string TestHeroSliceEndsAfterOneFloor()
        {
            for (int seed = 0; seed < 400; seed++)
            {
                var run = new RunSession(seed, 0f, 0f,
                    FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation,
                    new HeroSliceFloorSource());
                run.SelectContract(1);
                while (run.Current != null && run.Current.SpinsRemaining > 0 && !run.Current.CanBank)
                    run.Spin();
                if (run.Current == null || !run.Current.CanBank) continue;

                FloorResult banked = run.Bank();
                if (banked == null) return "확정 실패";
                // 1층짜리 소스이므로 확정 즉시 런이 끝나야 한다. 안 끝나면 2층을 만들려다
                // TenFloor 계획으로 새어 나갔다는 뜻이다.
                if (!run.IsComplete) return "1층 확정 후에도 런이 계속됨";
                if (run.Current != null) return "런 종료 후에도 현재 층이 남아 있음";
                return null;
            }
            return "400시드 안에 요구 전력을 달성하는 케이스가 없음 — 요구 전력이 과다";
        }

        private static string TestHeroSliceLeavesSpins()
        {
            // 요구 전력을 넘긴 시점에 스핀이 남아 있어야 과수확이 선택이 된다.
            // 남지 않으면 "확정 아니면 없음"이라 푸시 유어 럭이 성립하지 않는다.
            int achieved = 0, withSpinsLeft = 0;
            const int samples = 200;
            for (int n = 0; n < samples; n++)
            {
                var run = new RunSession(100000 + n, 0f, 0f,
                    FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation,
                    new HeroSliceFloorSource());
                run.SelectContract(0);   // 가장 불리한 조건(계약 없음)으로 본다
                FloorSession floor = run.Current;
                while (floor.SpinsRemaining > 0 && !floor.CanBank) run.Spin();
                if (!floor.CanBank) continue;
                achieved++;
                if (floor.SpinsRemaining > 0) withSpinsLeft++;
            }

            if (achieved < samples / 2)
                return $"달성률 {achieved * 100 / samples}% — 절반도 요구 전력을 못 넘김";
            if (withSpinsLeft * 2 < achieved)
                return $"달성 {achieved}건 중 스핀이 남은 경우 {withSpinsLeft}건 — 과수확 선택이 거의 생기지 않음";
            return null;
        }

        private static string TestTenFloorCurriculumIntact()
        {
            // Hero Slice를 넣으면서 10층 커리큘럼을 덮어쓰지 않았는지. Phase 2의 자산이다.
            var source = new TenFloorSource();
            if (source.LastFloor != 10) return $"10층 소스의 마지막 층 {source.LastFloor}";
            for (int floor = 1; floor <= 10; floor++)
            {
                FloorPlan plan = source.For(floor);
                if (plan.Floor != floor) return $"{floor}층 조회가 {plan.Floor}층을 반환";
                if (!plan.IsValid) return $"{floor}층 계획이 유효하지 않음";
            }
            if (source.For(1).ContractChoices.Length != 0)
                return "10층 커리큘럼 1층에 계약이 생김 — Hero Slice가 새어 들어감";
            if (source.For(10).ContractChoices.Length != 2)
                return "10층 커리큘럼 10층 계약 구성이 바뀜";

            // 여기서부터는 `DECISION_LOG` D-20260801-01 이 확정한 노션 03번 배치를 고정한다.
            // 이전 배치(08 기술부록 §14 / 99번)는 2~7층이 한 칸씩 달랐고, 두 배치가 코드에서
            // 조용히 뒤바뀌어도 "10층까지 진행된다"는 검사는 그대로 통과했다.
            // 무엇을 **언제 처음** 가르치는가가 커리큘럼의 전부이므로 그것을 고정한다.

            // 계약은 4층에 처음 나온다. 그 전 어느 층에도 없어야 한다.
            for (int floor = 1; floor <= 3; floor++)
                if (source.For(floor).ContractChoices.Length != 0)
                    return $"계약이 {floor}층에 등장 — 정본은 4층 첫 등장(D-20260801-01)";
            if (source.For(4).ContractChoices.Length != 2)
                return "4층이 흡수체 계약 층이 아님 — 선택지가 2개(없음·흡수체)여야 한다";

            // 증식체는 6층에 처음 나온다. 그 전에는 풀에 없어야 한다.
            for (int floor = 1; floor <= 5; floor++)
                foreach (SymbolKind kind in source.For(floor).SymbolPool)
                    if (kind == SymbolKind.Proliferator)
                        return $"증식체가 {floor}층 풀에 있음 — 정본은 6층 첫 등장";
            bool sixHasProliferator = false;
            foreach (SymbolKind kind in source.For(6).SymbolPool)
                if (kind == SymbolKind.Proliferator) sixHasProliferator = true;
            if (!sixHasProliferator) return "6층 풀에 증식체가 없음 — 정본은 6층 첫 등장";

            // 계약 비교는 7층. 두 계약이 나란히 놓이는 첫 층이다.
            if (source.For(7).ContractChoices.Length != 3)
                return "7층이 계약 비교 층이 아님 — 선택지가 3개(없음·흡수체·증식체)여야 한다";

            // 적재 층은 2·5·8. `RunSession.ClampAscent`(D-20260731-03)가 보호하는 집합이라
            // 여기가 바뀌면 다층 상승이 무엇을 건너뛸 수 있는지가 함께 바뀐다.
            for (int floor = 1; floor <= 10; floor++)
            {
                bool expected = floor == 2 || floor == 5 || floor == 8;
                if (source.For(floor).OffersBuildReward != expected)
                    return $"{floor}층 적재 여부가 {source.For(floor).OffersBuildReward} — 적재 층은 2·5·8";
            }

            // 과수확 강조는 9층.
            if (!source.For(9).EmphasizePushYourLuck)
                return "9층이 과수확 강조 층이 아님";
            return null;
        }

        private static FloorSession FindFirstSessionWithPower(int seed, float required)
        {
            for (int offset = 0; offset < 1000; offset++)
            {
                FloorSession session = NewSession(seed + offset, required, false, 5);
                session.Spin();
                if (session.CanBank) return session;
            }
            return null;
        }

        private static FloorSession NewSession(int seed, float required,
            bool needsContract, int spins)
        {
            ResistanceContract[] choices = needsContract
                ? new[] { ResistanceContract.None, PrototypeCurriculum.AbsorberContract }
                : Array.Empty<ResistanceContract>();
            var plan = new FloorPlan
            {
                Floor = 1,
                RequiredPower = required,
                Spins = spins,
                SymbolPool = new[] { SymbolKind.NormalSoul, SymbolKind.Absorber },
                ContractChoices = choices,
            };
            return new FloorSession(plan, new SpinEngine(seed),
                PowerThresholds.Default, 0f);
        }

        // ── T-05 남은 스핀 정산 ────────────────────────────────────────────────

        /// <summary>
        /// 정산 스냅샷을 넘길 수 있는 층. 인자가 하나 더 있는 것 말고는
        /// <see cref="NewSession"/> 과 같다.
        /// </summary>
        private static FloorSession NewSettlementSession(int seed, float required, int spins,
            Data.Profiles.RemainingSpinSettlementSnapshot settlement)
        {
            var plan = new FloorPlan
            {
                Floor = 1,
                RequiredPower = required,
                Spins = spins,
                SymbolPool = new[] { SymbolKind.NormalSoul, SymbolKind.Absorber },
                ContractChoices = Array.Empty<ResistanceContract>(),
            };
            return new FloorSession(plan, new SpinEngine(seed), PowerThresholds.Default,
                0f, ResidualState.Empty, OverharvestProfile.DefaultSnapshot,
                Data.Profiles.WeightProfile.DefaultSnapshot,
                Data.Profiles.SpinBalanceProfile.DefaultSnapshot, settlement, null);
        }

        /// <summary>
        /// **회귀 방지선.** 정산율을 두 배로 준 층이 두 배를 정산하는가.
        ///
        /// 이 검사가 없던 동안 `_settlement` 는 필드 초기화값에 고정돼 있었고,
        /// 프로파일이 무슨 값을 들고 있든 결과가 같았다. 「배선했다」와
        /// 「그 값이 쓰였다」의 차이를 잡는 것이 이 한 줄의 전부다.
        /// </summary>
        private static string TestSettlementInjected()
        {
            // Arrange — 회당 비율만 두 배로 다른 두 층. 상한은 넉넉히 열어 둔다.
            var baseline = new Data.Profiles.RemainingSpinSettlementSnapshot(0.05f, 1f, 1f, "검사-기준");
            var doubled  = new Data.Profiles.RemainingSpinSettlementSnapshot(0.10f, 1f, 1f, "검사-2배");

            const float required = 1f;   // 1 이면 첫 스핀에 반드시 달성한다
            FloorSession low  = NewSettlementSession(9001, required, 5, baseline);
            FloorSession high = NewSettlementSession(9001, required, 5, doubled);

            // Act
            low.Spin();
            high.Spin();

            // Assert
            if (!low.CanBank || !high.CanBank) return "요구 전력 1 인데 첫 스핀에 달성하지 못했다";
            if (low.SpinsRemaining != high.SpinsRemaining)
                return "같은 시드인데 남은 스핀이 다르다 — 비교가 성립하지 않는다";
            if (low.PendingSettlementMoney <= 0f)
                return "기준 층의 정산이 0 이다 — 남은 스핀이 있는데 정산이 없다";

            float ratio = high.PendingSettlementMoney / low.PendingSettlementMoney;
            if (Math.Abs(ratio - 2f) > 0.01f)
                return $"정산율을 2배로 줬는데 정산은 {ratio:F3}배다 — 스냅샷이 층에 주입되지 않는다";
            return null;
        }

        /// <summary>과수확을 고르면 그 층의 정산 권리가 사라진다 (`T-05`).</summary>
        private static string TestSettlementForfeited()
        {
            // Arrange
            FloorSession session = NewSettlementSession(9002, 1f, 5,
                Data.Profiles.RemainingSpinSettlementProfile.DefaultSnapshot);
            session.Spin();
            if (!session.CanTakeExtraSpin) return "추가 스핀을 고를 수 없는 상태 — 전제가 성립하지 않는다";
            if (session.PendingSettlementMoney <= 0f) return "과수확 전인데 정산이 이미 0 이다";

            // Act
            if (!session.PushYourLuck()) return "과수확 선택이 거부됐다";

            // Assert
            if (session.CanSettleRemainingSpins) return "과수확 후에도 정산 권리가 남아 있다";
            if (session.PendingSettlementMoney != 0f)
                return $"과수확 후 정산이 {session.PendingSettlementMoney:F2} 다 — 0 이어야 한다";
            return null;
        }

        /// <summary>
        /// 소멸은 **선택 시점**에 일어난다. 스핀이 실행되기 전에 이미 권리가 없어야 한다 —
        /// 결과를 보고 무를 수 있으면 그것은 도박이 아니다.
        /// </summary>
        private static string TestSettlementForfeitedAtChoice()
        {
            // Arrange
            FloorSession session = NewSettlementSession(9003, 1f, 5,
                Data.Profiles.RemainingSpinSettlementProfile.DefaultSnapshot);
            session.Spin();
            if (!session.CanTakeExtraSpin) return "전제 불성립 — 추가 스핀 불가";
            int spinsBefore = session.SpinsUsed;

            // Act — 앤티만 내고 스핀은 아직 돌리지 않는다.
            if (!session.PushYourLuck()) return "과수확 선택이 거부됐다";

            // Assert
            if (session.SpinsUsed != spinsBefore)
                return "PushYourLuck 이 스핀까지 실행했다 — 선택과 실행이 붙어 있으면 시점을 검사할 수 없다";
            if (session.CanSettleRemainingSpins)
                return "스핀을 돌리기 전인데 정산 권리가 살아 있다 — 결과를 보고 무를 수 있다";
            return null;
        }

        // ── UP-POWER-07 ────────────────────────────────────────────────────────

        /// <summary>
        /// 기본 스냅샷에서 한 값만 바꾼다. 셋을 각각 따로 흔들어야 어느 값이
        /// 무엇을 움직이는지 갈린다 — 한꺼번에 바꾸면 하나만 살아 있어도 통과한다.
        /// </summary>
        private static OverharvestSnapshot Tweaked(float unlockThreshold, int maxExtraSpins,
            float anteRatio, float anteEscalation)
        {
            OverharvestSnapshot d = OverharvestProfile.DefaultSnapshot;
            return new OverharvestSnapshot(anteRatio, anteEscalation, unlockThreshold,
                d.ApproachMachineDuckScale, d.MinSilenceSeconds, d.MaxSilenceSeconds,
                d.PassengerGazeDelaySeconds, d.ResumeFadeSeconds, maxExtraSpins);
        }

        private static FloorSession NewTweakedSession(int seed, float required, int spins,
            OverharvestSnapshot overharvest)
        {
            var plan = new FloorPlan
            {
                Floor = 1,
                RequiredPower = required,
                Spins = spins,
                SymbolPool = new[] { SymbolKind.NormalSoul },
                ContractChoices = Array.Empty<ResistanceContract>(),
            };
            return new FloorSession(plan, new SpinEngine(seed), PowerThresholds.Default,
                0f, ResidualState.Empty, overharvest, null);
        }

        /// <summary>상한 1회. 두 번째 당김은 스핀이 남아 있어도 거부돼야 한다.</summary>
        private static string TestProfileExtraSpinCap()
        {
            FloorSession session = NewTweakedSession(12, 70f, 5,
                Tweaked(OverharvestProfile.DefaultUnlockThreshold, 1,
                    OverharvestProfile.DefaultAnteRatio, OverharvestProfile.DefaultAnteEscalation));
            session.Spin();
            if (!session.CanBank) return "첫 스핀이 요구 전력에 못 미쳐 검사 조건이 성립하지 않음";
            if (!session.PushYourLuck()) return "상한 1인데 첫 추가 스핀이 거부됨";
            session.Spin();

            // 조건이 성립하는지 먼저 확인한다 — 스핀이 이미 소진됐다면 상한이 아니라
            // 스핀 부족으로 막힌 것이고, 그러면 이 검사는 공허하게 통과한다.
            if (session.SpinsRemaining <= 0) return "남은 스핀이 0이라 상한을 검사할 수 없음";
            if (!session.CanBank) return "두 번째 시점에 CanBank가 거짓이라 상한을 검사할 수 없음";
            if (session.PushYourLuck()) return "상한 1을 넘어 두 번째 추가 스핀이 허용됨";
            if (session.PendingAnte != 0f) return $"상한 도달인데 PendingAnte가 {session.PendingAnte}";
            return null;
        }

        /// <summary>
        /// 해금 임계 300%. 100%를 넘어 <c>CanBank</c>는 참인데 과수확은 잠겨 있어야 한다 —
        /// 둘이 한 조건이면 이 검사가 실패한다.
        /// </summary>
        private static string TestProfileUnlockThreshold()
        {
            FloorSession session = NewTweakedSession(12, 70f, 5,
                Tweaked(3.0f, OverharvestProfile.DefaultMaxExtraSpins,
                    OverharvestProfile.DefaultAnteRatio, OverharvestProfile.DefaultAnteEscalation));
            session.Spin();
            if (!session.CanBank) return "첫 스핀이 요구 전력에 못 미쳐 검사 조건이 성립하지 않음";
            if (session.Power / session.RequiredPower >= 3.0f)
                return $"달성률 {session.Power / session.RequiredPower:F2}가 이미 임계 3.0 이상이라 검사 불가";
            if (session.IsOverharvestUnlocked) return "임계 300%인데 해금됨";
            if (session.PushYourLuck()) return "잠긴 상태에서 추가 스핀이 허용됨";
            return null;
        }

        /// <summary>판돈 비율 0.5. PendingAnte가 그 비율을 그대로 따라야 한다.</summary>
        private static string TestProfileAnteRatio()
        {
            FloorSession session = NewTweakedSession(12, 70f, 5,
                Tweaked(OverharvestProfile.DefaultUnlockThreshold,
                    OverharvestProfile.DefaultMaxExtraSpins, 0.5f, 0f));
            session.Spin();
            if (!session.CanBank) return "첫 스핀이 요구 전력에 못 미쳐 검사 조건이 성립하지 않음";
            float expected = session.Power * 0.5f;
            if (Math.Abs(session.PendingAnte - expected) > 0.001f)
                return $"PendingAnte {session.PendingAnte} ≠ 전력×0.5 {expected}";

            // 기본값(0.12)과 실제로 다른지도 본다. 우연히 같은 수가 나오면
            // "프로파일에서 왔다"의 증거가 되지 못한다.
            if (Math.Abs(session.AnteRatio - OverharvestProfile.DefaultAnteRatio) < 0.0001f)
                return "판돈 비율이 코드 기본값과 같다 — 프로파일 값이 반영되지 않음";
            return null;
        }

        private static FloorSession NewNormalOnlySession(int seed, float required, int spins)
        {
            var plan = new FloorPlan
            {
                Floor = 1,
                RequiredPower = required,
                Spins = spins,
                SymbolPool = new[] { SymbolKind.NormalSoul },
                ContractChoices = Array.Empty<ResistanceContract>(),
            };
            return new FloorSession(plan, new SpinEngine(seed),
                PowerThresholds.Default, 0f);
        }

#if UNITY_EDITOR
        [MenuItem("Ascend/Run Floor Tests")]
        public static void RunFromMenu()
        {
            var result = RunAll();
            if (result.failed > 0) Debug.LogError(result.report);
            else Debug.Log(result.report);
        }
#endif
    }
}
