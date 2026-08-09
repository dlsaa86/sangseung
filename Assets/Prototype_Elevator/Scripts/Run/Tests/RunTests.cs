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
            Run("흡수체 잔류가 같은 스핀 순전력에서 차감", TestResidualDeductedFromNet, ref passed, ref failed, report);
            Run("증식체 잔류가 다음 스핀으로 넘어간다", TestResidualFeedsNextSpin, ref passed, ref failed, report);
            Run("요구 전력이 올라도 층이 교착되지 않는다", TestRequirementRiseDoesNotDeadlock, ref passed, ref failed, report);
            Run("추가 스핀 선택 시 앤티 즉시 차감", Shelved(TestAnteImmediateCharge), ref passed, ref failed, report);
            Run("연속 추가 스핀 앤티 비율 상승", Shelved(TestAnteEscalation), ref passed, ref failed, report);
            Run("앤티로 요구 전력 아래 하락 가능", Shelved(TestAnteCanLose), ref passed, ref failed, report);
            Run("PendingAnte와 실제 차감액 일치", Shelved(TestPendingAnteMatchesCharge), ref passed, ref failed, report);
            Run("무게 증가가 RequiredPower 증가", TestWeightRaisesRequirement, ref passed, ref failed, report);
            Run("동일 시드·선택 결정론", TestDeterminism, ref passed, ref failed, report);

            // ── UP-POWER-07: 프로파일 값이 **게임을 실제로 바꾸는가** ──
            // 「읽는다」가 아니라 「바꾸면 결과가 달라진다」를 묻는다. 값을 망가뜨렸을 때
            // 실패하지 않는 검사는 그 값이 죽어 있다는 사실을 통과로 기록한다.
            Run("과수확 상한이 추가 스핀을 실제로 막는다", Shelved(TestProfileExtraSpinCap), ref passed, ref failed, report);
            Run("해금 임계가 CanBank와 독립으로 작동", TestProfileUnlockThreshold, ref passed, ref failed, report);
            Run("판돈 비율이 프로파일에서 온다", Shelved(TestProfileAnteRatio), ref passed, ref failed, report);

            // ── T-05: 남은 스핀 정산이 **주입되고 소멸하는가** ──
            // `SettlementTests` 는 스냅샷의 산수만 검사한다 — 그 산수가 옳아도
            // 층이 그 스냅샷을 **받지 못하면** 게임에서는 아무 일도 일어나지 않는다.
            // 실제로 그랬다: `_settlement` 에 대입하는 코드가 어디에도 없었고,
            // 프로파일을 만들어 배선해도 한 자리도 바뀌지 않았다. 열 개의 통과한
            // 검사가 그 사실을 하나도 잡지 못했다.
            Run("정산 수치가 층에 실제로 주입된다", TestSettlementInjected, ref passed, ref failed, report);
            Run("과수확을 고르면 정산 권리가 소멸한다", Shelved(TestSettlementForfeited), ref passed, ref failed, report);
            Run("정산 소멸은 스핀 결과가 아니라 선택 시점에 일어난다",
                Shelved(TestSettlementForfeitedAtChoice), ref passed, ref failed, report);

            // 보류가 **실제로 걸렸는지**를 묻는 회귀 검사. 위의 `Shelved` 들은 스위치를
            // 켜고 도는 것이라, 이 한 줄이 없으면 「기본 설정에서 과수확이 안 열린다」를
            // 증명하는 검사가 하나도 없게 된다 — 스위치를 되돌려 놓고도 전부 통과한다.
            Run("보류된 과수확은 기본 설정에서 열리지 않는다", TestOverharvestShelvedByDefault,
                ref passed, ref failed, report);

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

        /// <summary>
        /// **보류된** 과수확 하위 시스템을 검증하는 검사를 감싼다
        /// (<see cref="PrototypeFeatures.Overharvest"/>, 2026-08-09 사용자 결정).
        ///
        /// 기능을 껐다고 그 검사를 지우거나 스킵하지 않는다 — `CLAUDE.md` 가 금지한다.
        /// 대신 **범위 안에서만** 켜서 예전과 똑같이 돌린다. 그래야 되살릴 때
        /// 밸런스가 그대로라는 것이 지금도 계속 증명된다.
        ///
        /// 스코프를 쓰는 이유: 직접 대입하면 예외가 났을 때 켜진 채로 남아
        /// **그 뒤의 검사들이 조용히 통과한다.** `Run` 이 예외를 잡아 계속 진행하므로
        /// 그 사고는 실제로 일어날 수 있다.
        /// </summary>
        private static Func<string> Shelved(Func<string> test)
        {
            return () =>
            {
                using (PrototypeFeatures.EnableOverharvest()) return test();
            };
        }

        /// <summary>
        /// 보류가 실제로 걸렸는가. 전력·열쇠 조건을 **둘 다 만족시킨 뒤에도** 잠겨 있어야 한다 —
        /// 조건 미달로 잠긴 것과 보류로 잠긴 것을 구분하지 못하면 이 검사는 아무것도 지키지 못한다.
        /// </summary>
        private static string TestOverharvestShelvedByDefault()
        {
            if (PrototypeFeatures.Overharvest)
                return "기본값이 켜져 있다 — 앞선 검사가 스코프를 되돌리지 않았거나 보류가 풀렸다";

            FloorSession session = NewNormalOnlySession(11, 70f, 5);
            session.Spin();

            if (!session.HasOverharvestKey)
                return "열쇠 조건이 이미 거짓이라 보류 때문에 잠긴 것인지 구분할 수 없다";
            if (session.IsOverharvestUnlocked)
                return "보류 상태인데 과수확이 열려 있다";
            if (session.CanTakeExtraSpin)
                return "보류 상태인데 추가 스핀을 고를 수 있다";
            if (session.PendingAnte != 0f)
                return $"보류 상태인데 공개 앤티가 {session.PendingAnte}";
            if (session.PushYourLuck())
                return "보류 상태인데 PushYourLuck 이 받아들여졌다";

            // 같은 세션을 스위치만 켜서 다시 보면 열려야 한다. 이 대조가 없으면
            // 「보류 때문에 잠겼다」와 「원래 조건이 안 맞아 잠겼다」가 구분되지 않는다.
            using (PrototypeFeatures.EnableOverharvest())
            {
                if (!session.IsOverharvestUnlocked)
                    return "스위치를 켜도 잠겨 있다 — 이 검사가 보류를 검증하지 못한다";
            }
            return null;
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

        /// <summary>
        /// 🔴 **이 검사는 자기 이름의 부정을 통과 조건으로 갖고 있었다.**
        ///
        /// 옛 이름은 「흡수체 잔류가 **다음 스핀** 전력을 차감」이었고 통과 조건은
        /// `second.NetPower >= second.GrossPower` — 즉 **차감이 일어나지 않았을 때만 참**이었다.
        /// 게다가 그 조건은 첫 후보 시드에서 곧바로 `return null`(통과)로 빠졌다.
        ///
        /// 이름도 틀렸다. 흡수체 잔류는 다음 스핀으로 넘어가지 않는다 —
        /// <c>SpinEngine</c> 의 <c>NetPower = grossPower - residual.StoredPowerLoss</c> 는
        /// **그 스핀이 자기 최종 보드에 남긴** 흡수체를 같은 스핀의 순전력에서 뺀다
        /// (노션 §5.2 판정 순서 9 「미정화 저항의 잔류 효과 적용」이 그 스핀의 마지막 단계다).
        /// 다음 스핀으로 실제로 넘어가는 것은 증식체 쪽뿐이고, 그건
        /// <see cref="TestResidualFeedsNextSpin"/> 이 따로 본다.
        ///
        /// 그래서 이름과 조건을 둘 다 이름이 말하는 **불변식**으로 고쳤다 —
        /// 순전력은 총전력에서 잔류 손실을 정확히 뺀 값이다. 이 형태라야
        /// 판정식이 바뀌면 실패한다.
        /// </summary>
        private static string TestResidualDeductedFromNet()
        {
            int checkedSpins = 0;

            for (int seed = 0; seed < 5000; seed++)
            {
                FloorSession session = NewSession(seed, 100000000f, false, 5);
                while (session.Phase == FloorPhase.Spinning && session.SpinsRemaining > 0)
                {
                    SpinResolution spin = session.Spin();
                    if (spin.Steps == null) break;
                    if (spin.Residual.StoredPowerLoss <= 0f) continue;

                    checkedSpins++;
                    float expected = spin.GrossPower - spin.Residual.StoredPowerLoss;
                    if (Math.Abs(spin.NetPower - expected) > 0.0001f)
                        return $"시드 {seed} 스핀 {spin.SpinIndex}: 순전력 {spin.NetPower:0.####} ≠ "
                             + $"총전력 {spin.GrossPower:0.####} − 잔류 손실 {spin.Residual.StoredPowerLoss:0.####}"
                             + $" (= {expected:0.####})";
                    if (spin.NetPower >= spin.GrossPower)
                        return $"시드 {seed} 스핀 {spin.SpinIndex}: 잔류 흡수체 "
                             + $"{spin.Residual.AbsorberCount}개가 남았는데 순전력이 총전력 이상이다";
                }

                // 표본이 충분히 모이면 5000 시드를 끝까지 돌지 않는다.
                if (checkedSpins >= 200) return null;
            }

            // **못 찾은 것을 통과로 기록하지 않는다.** 옛 판본이 그렇게 새어 나갔다.
            return checkedSpins > 0 ? null : "잔류 흡수체를 남기는 스핀을 하나도 찾지 못함";
        }

        /// <summary>
        /// 🔴 **`FloorSession` 의 `_residual = resolution.Residual;` 한 줄을 지워도
        /// 587개 검사 중 0개가 실패했다.** 이 검사가 그 한 줄을 지키라고 있다.
        ///
        /// 스핀 사이에 실제로 넘어가는 상태는 하나뿐이다 — 남은 증식체가 다음 스핀의
        /// 증식체 출현 가중치에 더해진다(<c>SpinEngine.PrepareRules</c>). 노션 §6.3 의
        /// 「잔류 저항을 다음 실행의 재료로 사용하는 빌드」가 성립하려면 이 경로가 살아 있어야 한다.
        ///
        /// 판정을 여기서 다시 구현하지 않는다. 같은 엔진을 같은 시드로 두 번 부르되
        /// **잔류만 다르게** 넣고, 층이 낸 결과가 「잔류를 넘긴 쪽」과 같은지 본다.
        /// 두 결과가 애초에 같은 시드는 증거가 되지 못하므로 건너뛴다.
        /// </summary>
        private static string TestResidualFeedsNextSpin()
        {
            for (int seed = 0; seed < 5000; seed++)
            {
                FloorSession session = NewProliferatorSession(seed, 100000000f, 3);
                SpinResolution first = session.Spin();
                if (first.Residual.NextProliferatorWeightAdd <= 0f ||
                    session.Phase != FloorPhase.Spinning) continue;

                SpinRuleSet rules = session.Rules;
                int nextSeed = SpinSeed.Derive(seed, session.Plan.Floor, session.SpinsUsed);
                var none = ResistanceContract.None;
                var empty = ResidualState.Empty;
                ResidualState carried = first.Residual;

                SpinResolution withCarry = new SpinEngine(seed).SpinWithSeed(
                    nextSeed, rules, in none, in carried, session.Plan.Floor, session.SpinsUsed);
                SpinResolution withoutCarry = new SpinEngine(seed).SpinWithSeed(
                    nextSeed, rules, in none, in empty, session.Plan.Floor, session.SpinsUsed);

                // 가중치가 달라도 같은 판이 나올 수 있다. 그런 시드는 아무것도 증명하지 못한다.
                if (BoardsEqual(withCarry.FinalBoard, withoutCarry.FinalBoard) &&
                    Math.Abs(withCarry.GrossPower - withoutCarry.GrossPower) < 0.0001f) continue;

                SpinResolution actual = session.Spin();

                if (BoardsEqual(actual.FinalBoard, withoutCarry.FinalBoard) &&
                    !BoardsEqual(actual.FinalBoard, withCarry.FinalBoard))
                    return $"시드 {seed}: 앞 스핀이 증식체 잔류 "
                         + $"+{carried.NextProliferatorWeightAdd:0.###} 를 남겼는데 다음 스핀이 "
                         + "잔류 없는 결과와 같다 — 잔류가 층에서 다음 스핀으로 넘어가지 않았다";

                if (!BoardsEqual(actual.FinalBoard, withCarry.FinalBoard))
                    return $"시드 {seed}: 다음 스핀 결과가 잔류를 넘긴 엔진 결과와도 다르다 "
                         + "— 층과 엔진이 서로 다른 입력을 쓰고 있다";

                return null;
            }
            return "증식체 잔류가 결과를 바꾸는 시드를 찾지 못함";
        }

        private static bool BoardsEqual(SpinBoard a, SpinBoard b)
        {
            for (int column = 0; column < SpinBoard.Columns; column++)
                for (int row = 0; row < SpinBoard.Rows; row++)
                    if (a[column, row] != b[column, row]) return false;
            return true;
        }

        /// <summary>
        /// 🔴 **`CE-1` 재현.** 확정 단계에서 적재가 늘어 요구 전력이 오르면
        /// 네 출구가 동시에 닫히던 자리다. 시드 1337·1338·1339 가 실측 재현 시드다.
        ///
        /// 검사가 묻는 것은 「요구 전력이 얼마인가」가 아니라 **「층이 끝날 수 있는가」**다.
        /// 값을 고정하면 밸런스를 잠그게 되므로, 여기서는 진행 가능성만 본다.
        /// </summary>
        private static string TestRequirementRiseDoesNotDeadlock()
        {
            for (int seed = 1337; seed <= 1339; seed++)
            {
                FloorSession session = NewSession(seed, 40f, false, 5);
                session.Spin();
                if (session.Phase != FloorPhase.Decision || !session.CanBank) continue;

                // 층 도중에 무게가 붙는다 — 캡처 리그(`TenFloorCaptureRig`)가 쓰는 경로다.
                session.RefreshLoad(session.CarriedWeight + 200f);
                if (session.CanBank) continue;   // 이 시드로는 교착 조건이 안 만들어진다

                if (session.Phase != FloorPhase.Spinning)
                    return $"시드 {seed}: 요구 전력이 {session.RequiredPower:0.#} 로 올라 "
                         + $"달성이 무너졌는데 단계가 {session.Phase} 다 — 네 출구가 모두 닫혔다";

                // 실제로 끝까지 갈 수 있어야 한다. 출구가 열린 것과 층이 끝나는 것은 다르다.
                int guard = 0;
                while (session.Result == null && guard++ < 32)
                {
                    if (session.Phase == FloorPhase.Spinning && session.SpinsRemaining > 0)
                    {
                        session.Spin();
                        continue;
                    }
                    if (session.Bank() != null || session.ForceResolve() != null) break;
                    return $"시드 {seed}: 단계 {session.Phase} · 남은 스핀 "
                         + $"{session.SpinsRemaining} 에서 진행할 수 있는 동작이 없다";
                }

                if (session.Result == null)
                    return $"시드 {seed}: 32회 안에 층이 끝나지 않았다 (단계 {session.Phase})";
                return null;
            }
            return null;   // 세 시드 모두 조건이 성립하지 않으면 이 검사는 아무 말도 하지 않는다
        }

        private static FloorSession NewProliferatorSession(int seed, float required, int spins)
        {
            var plan = new FloorPlan
            {
                Floor = 1,
                RequiredPower = required,
                Spins = spins,
                SymbolPool = new[] { SymbolKind.NormalSoul, SymbolKind.Proliferator },
                ContractChoices = Array.Empty<ResistanceContract>(),
            };
            return new FloorSession(plan, new SpinEngine(seed),
                PowerThresholds.Default, 0f);
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
