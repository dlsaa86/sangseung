using System;
using System.Text;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// 층 이동·라운드 목표 규칙 검사 (2026-08-09 코어 루프 재설계).
    ///
    /// 이 파일은 **순수 규칙만** 다룬다 — 런도 씬도 스핀도 모른다. 배선은 별도이고,
    /// 그 배선이 이 규칙을 실제로 부르는지는 다른 검사가 묻는다. 둘을 한 파일에 두면
    /// 「규칙은 맞는데 아무도 안 부른다」가 통과로 기록된다 — 이 저장소가 반복해서
    /// 당한 실패다(`DEAD_IMPLEMENTATION_AUDIT`).
    /// </summary>
    public static class ElevatorTravelTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();

            Run("오르내림 비용이 대칭이다", TestSymmetricCost, ref passed, ref failed, report);
            Run("이동 비용 = |층수| x 층당 전력", TestCostIsLinear, ref passed, ref failed, report);
            Run("전력이 모자라면 전부 거절한다 (부분 이동 없음)", TestInsufficientPowerRejects, ref passed, ref failed, report);
            Run("전력이 정확히 맞으면 이동한다", TestExactPowerAccepted, ref passed, ref failed, report);
            Run("최저층 아래·최고층 위로는 못 간다", TestBoundsRejected, ref passed, ref failed, report);
            Run("제자리는 공짜다", TestStayingStillIsFree, ref passed, ref failed, report);
            Run("내려가기가 실제로 된다 (상점·이벤트 층 대비)", TestDescendWorks, ref passed, ref failed, report);
            Run("목표까지 필요한 총 전력이 층수에 비례한다", TestPowerToReach, ref passed, ref failed, report);
            Run("이미 도달했으면 필요 전력 0", TestPowerToReachZeroWhenAtOrAbove, ref passed, ref failed, report);
            Run("보유 전력으로 갈 수 있는 최대 층수", TestMaxFloorsFor, ref passed, ref failed, report);
            Run("목표를 지나쳐도 도달이다", TestOvershootCounts, ref passed, ref failed, report);
            Run("미달이면 돈이 0", TestNoMoneyWhenShort, ref passed, ref failed, report);
            Run("도달 시 남은 스핀만큼 돈", TestMoneyFromUnusedSpins, ref passed, ref failed, report);
            Run("스핀을 다 쓰고 도달하면 돈이 0", TestReachedWithNoSpinsLeftPaysNothing, ref passed, ref failed, report);

            return (passed, failed, report.ToString());
        }

        private static ElevatorTravel Travel(float perFloor = 60f, int min = 1, int max = 100)
            => new ElevatorTravel(perFloor, min, max);

        // ── 이동 비용 ──────────────────────────────────────────────────────────
        private static string TestSymmetricCost()
        {
            var t = Travel();
            if (Math.Abs(t.CostFor(3) - t.CostFor(-3)) > 0.0001f)
                return $"위 {t.CostFor(3)} vs 아래 {t.CostFor(-3)}";
            return null;
        }

        private static string TestCostIsLinear()
        {
            var t = Travel(50f);
            if (Math.Abs(t.CostFor(4) - 200f) > 0.0001f) return $"4층에 {t.CostFor(4)}, 기대 200";
            if (Math.Abs(t.CostFor(1) - 50f) > 0.0001f) return $"1층에 {t.CostFor(1)}, 기대 50";
            return null;
        }

        private static string TestInsufficientPowerRejects()
        {
            var t = Travel(60f);
            // 3층에 180 이 필요한데 179 만 있다
            TravelResult r = t.Move(5, 3, 179f);
            if (r.Accepted) return "전력이 모자란데 통과됐다";
            if (r.ToFloor != 5) return $"거절인데 층이 {r.ToFloor} 로 움직였다";
            if (Math.Abs(r.PowerSpent) > 0.0001f) return $"거절인데 전력 {r.PowerSpent} 를 썼다";
            if (Math.Abs(r.PowerRemaining - 179f) > 0.0001f) return "거절인데 보유 전력이 바뀌었다";
            if (string.IsNullOrEmpty(r.Rejection)) return "거절 사유가 비었다";
            return null;
        }

        private static string TestExactPowerAccepted()
        {
            var t = Travel(60f);
            TravelResult r = t.Move(5, 3, 180f);
            if (!r.Accepted) return "정확히 맞는데 거절됐다: " + r.Rejection;
            if (r.ToFloor != 8) return $"{r.ToFloor} 층, 기대 8";
            if (Math.Abs(r.PowerRemaining) > 0.0001f) return $"잔여 {r.PowerRemaining}, 기대 0";
            return null;
        }

        private static string TestBoundsRejected()
        {
            var t = Travel(60f, 1, 10);
            if (t.Move(2, -5, 9999f).Accepted) return "최저층 아래로 갔다";
            if (t.Move(9, 4, 9999f).Accepted) return "최고층 위로 갔다";
            if (!t.Move(9, 1, 9999f).Accepted) return "최고층까지는 갈 수 있어야 한다";
            return null;
        }

        private static string TestStayingStillIsFree()
        {
            var t = Travel();
            TravelResult r = t.Move(4, 0, 0f);
            if (!r.Accepted) return "제자리가 거절됐다";
            if (Math.Abs(r.PowerSpent) > 0.0001f) return $"제자리인데 {r.PowerSpent} 를 썼다";
            return null;
        }

        private static string TestDescendWorks()
        {
            var t = Travel(60f, 1, 100);
            TravelResult r = t.Move(9, -3, 200f);
            if (!r.Accepted) return "하강이 거절됐다: " + r.Rejection;
            if (r.ToFloor != 6) return $"{r.ToFloor} 층, 기대 6";
            if (r.FloorsMoved != -3) return $"이동 {r.FloorsMoved}, 기대 -3 (부호가 살아 있어야 한다)";
            if (Math.Abs(r.PowerSpent - 180f) > 0.0001f) return $"소모 {r.PowerSpent}, 기대 180";
            return null;
        }

        // ── 목표까지의 총 전력 (버튼 위에 뜨는 값) ─────────────────────────────
        private static string TestPowerToReach()
        {
            var t = Travel(60f);
            if (Math.Abs(t.PowerToReach(1, 10) - 540f) > 0.0001f)
                return $"1→10 층에 {t.PowerToReach(1, 10)}, 기대 9x60=540";
            return null;
        }

        private static string TestPowerToReachZeroWhenAtOrAbove()
        {
            var t = Travel(60f);
            if (t.PowerToReach(10, 10) != 0f) return "이미 목표층인데 0 이 아니다";
            if (t.PowerToReach(12, 10) != 0f) return "목표를 지났는데 0 이 아니다";
            return null;
        }

        private static string TestMaxFloorsFor()
        {
            var t = Travel(60f);
            if (t.MaxFloorsFor(179f) != 2) return $"179 로 {t.MaxFloorsFor(179f)} 층, 기대 2";
            if (t.MaxFloorsFor(180f) != 3) return $"180 로 {t.MaxFloorsFor(180f)} 층, 기대 3";
            if (t.MaxFloorsFor(0f) != 0) return "전력 0 인데 움직일 수 있다고 한다";
            return null;
        }

        // ── 라운드 목표와 정산 ────────────────────────────────────────────────
        private static string TestOvershootCounts()
        {
            var g = new RoundGoal(10, 5, 4f);
            if (!g.IsReached(10)) return "정확히 도달인데 미달이라고 한다";
            if (!g.IsReached(13)) return "지나쳤는데 미달이라고 한다 — 오버슈트가 죽음이 되면 안 된다";
            if (g.IsReached(9)) return "9층인데 도달이라고 한다";
            return null;
        }

        private static string TestNoMoneyWhenShort()
        {
            var g = new RoundGoal(10, 5, 4f);
            if (g.MoneyFor(9, 3) != 0f) return $"미달인데 돈 {g.MoneyFor(9, 3)}";
            return null;
        }

        private static string TestMoneyFromUnusedSpins()
        {
            var g = new RoundGoal(10, 5, 4f);
            if (Math.Abs(g.MoneyFor(10, 3) - 12f) > 0.0001f)
                return $"남은 스핀 3 에 돈 {g.MoneyFor(10, 3)}, 기대 12";
            if (Math.Abs(g.MoneyFor(10, 1) - 4f) > 0.0001f)
                return $"남은 스핀 1 에 돈 {g.MoneyFor(10, 1)}, 기대 4";
            return null;
        }

        private static string TestReachedWithNoSpinsLeftPaysNothing()
        {
            var g = new RoundGoal(10, 5, 4f);
            if (g.MoneyFor(10, 0) != 0f)
                return $"스핀을 다 쓰고 도달했는데 돈 {g.MoneyFor(10, 0)} — 효율 보상이 아니게 된다";
            return null;
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
            catch (Exception e)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외 {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
