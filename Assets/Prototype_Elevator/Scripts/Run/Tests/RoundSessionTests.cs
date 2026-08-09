using System;
using System.Text;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// 라운드 상태 기계 검사 (2026-08-09 코어 루프 재설계).
    /// <see cref="ElevatorTravelTests"/> 가 이동·정산 **산수**를 검사한다면 이쪽은
    /// **순서**를 검사한다 — 언제 끝나는가, 언제 돈이 나오는가, 무엇이 거절되는가.
    /// </summary>
    public static class RoundSessionTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();

            Run("스핀은 전력을 더하고 잔량을 줄인다", TestSpinAddsPower, ref passed, ref failed, report);
            Run("스핀 상한을 넘겨 돌릴 수 없다", TestSpinCap, ref passed, ref failed, report);
            Run("도달하면 그 자리에서 라운드가 끝난다", TestReachEndsRound, ref passed, ref failed, report);
            Run("도달 시 남은 스핀만큼 돈이 나온다", TestMoneyOnReach, ref passed, ref failed, report);
            Run("일찍 도달할수록 돈이 많다", TestEarlierIsRicher, ref passed, ref failed, report);
            Run("스핀이 남아 있으면 Resolve 가 거절된다", TestResolveRejectedWhileSpinsRemain, ref passed, ref failed, report);
            Run("스핀 소진 뒤에도 이동은 살아 있다", TestCanMoveAfterLastSpin, ref passed, ref failed, report);
            Run("스핀 소진 + 미달 = 추락", TestCrashWhenShort, ref passed, ref failed, report);
            Run("추락하면 돈이 0", TestCrashPaysNothing, ref passed, ref failed, report);
            Run("끝난 라운드는 더 이상 움직이지 않는다", TestNoMoveAfterOver, ref passed, ref failed, report);
            Run("전력 부족 이동은 상태를 바꾸지 않는다", TestFailedMoveIsInert, ref passed, ref failed, report);
            Run("내려갔다 올라와도 규칙이 성립한다", TestDescendThenAscend, ref passed, ref failed, report);
            Run("PowerToGoal 이 상승 버튼 조건과 일치한다", TestCanReachGoalNow, ref passed, ref failed, report);
            Run("이월 전력으로 이미 목표 위면 즉시 생존", TestStartAboveGoal, ref passed, ref failed, report);

            return (passed, failed, report.ToString());
        }

        private static RoundSession New(int target = 10, int spins = 5, int start = 1,
                                        float perFloor = 60f, float money = 4f, float carried = 0f)
            => new RoundSession(new RoundGoal(target, spins, money),
                                new ElevatorTravel(perFloor, 1, 100), start, carried);

        private static string TestSpinAddsPower()
        {
            var r = New();
            if (!r.Spin(120f)) return "첫 스핀이 거절됐다";
            if (Math.Abs(r.Power - 120f) > 0.0001f) return $"전력 {r.Power}, 기대 120";
            if (r.SpinsRemaining != 4) return $"잔량 {r.SpinsRemaining}, 기대 4";
            return null;
        }

        private static string TestSpinCap()
        {
            var r = New(target: 99, spins: 2);
            r.Spin(10f); r.Spin(10f);
            if (r.Spin(10f)) return "상한을 넘겨 돌아갔다";
            if (r.SpinsRemaining != 0) return $"잔량 {r.SpinsRemaining}";
            return null;
        }

        private static string TestReachEndsRound()
        {
            var r = New(target: 4, start: 1);          // 3층 이동 = 180
            r.Spin(200f);
            r.Move(3);
            if (r.Outcome != RoundOutcome.Survived) return $"결과 {r.Outcome}, 기대 Survived";
            if (!r.IsOver) return "도달했는데 라운드가 안 끝났다";
            if (r.CurrentFloor != 4) return $"{r.CurrentFloor} 층";
            return null;
        }

        private static string TestMoneyOnReach()
        {
            var r = New(target: 4, spins: 5, start: 1, money: 4f);
            r.Spin(200f);                                // 1회 사용 → 4회 남음
            r.Move(3);
            if (Math.Abs(r.MoneyEarned - 16f) > 0.0001f) return $"돈 {r.MoneyEarned}, 기대 4x4=16";
            return null;
        }

        private static string TestEarlierIsRicher()
        {
            var fast = New(target: 4, start: 1); fast.Spin(200f); fast.Move(3);
            var slow = New(target: 4, start: 1);
            slow.Spin(70f); slow.Spin(70f); slow.Spin(70f); slow.Move(3);
            if (!(fast.MoneyEarned > slow.MoneyEarned))
                return $"빠른 쪽 {fast.MoneyEarned} ≤ 느린 쪽 {slow.MoneyEarned} — 효율 보상이 성립하지 않는다";
            return null;
        }

        private static string TestResolveRejectedWhileSpinsRemain()
        {
            var r = New();
            r.Spin(10f);
            if (r.Resolve()) return "스핀이 남았는데 라운드가 끝났다";
            if (r.IsOver) return "거절인데 상태가 끝남으로 바뀌었다";
            return null;
        }

        private static string TestCanMoveAfterLastSpin()
        {
            var r = New(target: 4, spins: 2, start: 1);
            r.Spin(100f); r.Spin(100f);                  // 스핀 소진, 전력 200
            if (r.SpinsRemaining != 0) return "스핀이 남았다";
            TravelResult m = r.Move(3);
            if (!m.Accepted) return "마지막 스핀 뒤 이동이 거절됐다: " + m.Rejection;
            if (r.Outcome != RoundOutcome.Survived) return $"{r.Outcome} — 도달했어야 한다";
            return null;
        }

        private static string TestCrashWhenShort()
        {
            var r = New(target: 10, spins: 2, start: 1);
            r.Spin(60f); r.Spin(60f);                    // 2층분뿐
            r.Move(2);                                    // 3층까지
            if (!r.Resolve()) return "Resolve 가 거절됐다";
            if (r.Outcome != RoundOutcome.Crashed) return $"결과 {r.Outcome}, 기대 Crashed";
            return null;
        }

        private static string TestCrashPaysNothing()
        {
            var r = New(target: 10, spins: 1, start: 1);
            r.Spin(0f); r.Resolve();
            if (r.MoneyEarned != 0f) return $"추락인데 돈 {r.MoneyEarned}";
            return null;
        }

        private static string TestNoMoveAfterOver()
        {
            var r = New(target: 2, start: 1);
            r.Spin(100f); r.Move(1);                      // 도달 → 종료
            TravelResult m = r.Move(1);
            if (m.Accepted) return "끝난 라운드에서 이동이 통과됐다";
            if (r.CurrentFloor != 2) return $"{r.CurrentFloor} 층 — 끝난 뒤에 움직였다";
            return null;
        }

        private static string TestFailedMoveIsInert()
        {
            var r = New(target: 10, start: 1);
            r.Spin(59f);                                  // 1층에 60 필요
            int floor = r.CurrentFloor; float power = r.Power;
            TravelResult m = r.Move(1);
            if (m.Accepted) return "전력이 모자란데 통과됐다";
            if (r.CurrentFloor != floor || Math.Abs(r.Power - power) > 0.0001f)
                return $"거절인데 상태가 바뀌었다 ({floor}→{r.CurrentFloor}, {power}→{r.Power})";
            return null;
        }

        private static string TestDescendThenAscend()
        {
            var r = New(target: 6, spins: 5, start: 5);
            r.Spin(300f);
            TravelResult down = r.Move(-2);               // 3층으로, 120 소모
            if (!down.Accepted) return "하강이 거절됐다: " + down.Rejection;
            if (r.CurrentFloor != 3) return $"{r.CurrentFloor} 층, 기대 3";
            if (Math.Abs(r.Power - 180f) > 0.0001f) return $"전력 {r.Power}, 기대 180";
            TravelResult up = r.Move(3);                  // 6층 = 목표, 180 소모
            if (!up.Accepted) return "복귀 상승이 거절됐다: " + up.Rejection;
            if (r.Outcome != RoundOutcome.Survived) return $"{r.Outcome}";
            if (Math.Abs(r.Power) > 0.0001f) return $"잔여 전력 {r.Power}, 기대 0";
            return null;
        }

        private static string TestCanReachGoalNow()
        {
            var r = New(target: 4, start: 1);             // 3층 = 180 필요
            r.Spin(179f);
            if (Math.Abs(r.PowerToGoal - 180f) > 0.0001f) return $"필요 {r.PowerToGoal}, 기대 180";
            if (r.CanReachGoalNow) return "179 인데 갈 수 있다고 한다";
            r.Spin(1f);
            if (!r.CanReachGoalNow) return "180 인데 못 간다고 한다 — 버튼이 안 켜진다";
            return null;
        }

        private static string TestStartAboveGoal()
        {
            var r = New(target: 5, start: 7);
            if (r.Outcome != RoundOutcome.Survived)
                return $"이미 목표 위인데 {r.Outcome} — 스핀을 낭비하게 된다";
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
