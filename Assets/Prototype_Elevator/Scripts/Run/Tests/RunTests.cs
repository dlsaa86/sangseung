using System;
using System.Text;
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
            float firstAnte = session.PendingAnte;
            if (!session.PushYourLuck()) return "첫 추가 스핀 선택 거부";
            session.Spin();
            float secondAnte = session.PendingAnte;
            float expectedSecondRatio = FloorSession.DefaultAnteRatio *
                (1f + FloorSession.DefaultAnteEscalation);
            if (Math.Abs(firstAnte - 90f * FloorSession.DefaultAnteRatio) > 0.001f ||
                Math.Abs(secondAnte - session.Power * expectedSecondRatio) > 0.001f)
                return $"앤티 상승이 잘못됨: {firstAnte}, {secondAnte}";
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
