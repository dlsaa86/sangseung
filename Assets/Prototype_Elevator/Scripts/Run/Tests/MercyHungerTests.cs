using System;
using System.Text;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// Mercy / Hunger 두 성과 축 (`DECISION_LOG` D-20260805-01 · Notion `MASTER PRD` §6).
    ///
    /// ## 무엇을 반증하려는가
    ///
    /// 이 규칙의 목적은 「잘한 플레이에 보상」이 아니다. §6.1 이 목적을 직접 적는다 —
    /// **순수 강화만 주면 숙련자가 더 강해져 이후 층이 쉬워진다.** 그래서 성과를
    /// 절제와 탐욕으로 **갈라** 서로 겨루게 한다.
    ///
    /// 따라서 검사해야 하는 것은 「등급이 계산되는가」가 아니라 **두 축이 실제로
    /// 상충하는가**다. 구체적으로 —
    ///
    /// | 검사 | 깨지면 무엇이 무너지나 |
    /// |---|---|
    /// | 더 돌리면 Mercy 가 내려간다 | 과수확이 공짜가 된다 — 브레이크를 걸 이유가 사라진다 |
    /// | 미달성은 Mercy None | 「달성하지 못한 절제」가 절제로 집계된다 |
    /// | 첫 달성 −1 과 0 의 구분 | 「끝내 못 채웠다」와 「마지막에 겨우 채웠다」가 같아진다 |
    /// | 요구 0 방어 | 0 나눗셈이 무한대를 Hunger III 로 만든다 |
    /// | 경계 정확 | 25.0% 가 I 인지 None 인지가 흔들린다 |
    /// | 단조성 | 「II 인데 III 는 아닌」 구간이 사라져 등급이 건너뛴다 |
    ///
    /// 순수 계산이라 씬 없이 전부 검사된다(`MASTER_PRD` §9.1).
    /// </summary>
    public static class MercyHungerTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();

            Run("기본 임계치가 Notion §6.2 시작값과 같다", TestDefaults, ref passed, ref failed, report);
            Run("남은 스핀이 늘면 Mercy 등급이 오른다", TestMercyLadder, ref passed, ref failed, report);
            Run("추가 스핀을 쓰면 Mercy 가 내려간다 (§6.2 저울)", TestMercyDecaysWithExtraSpins, ref passed, ref failed, report);
            Run("끝내 못 채우면 Mercy 는 None (−1 과 0 의 구분)", TestMercyNeverReached, ref passed, ref failed, report);
            Run("초과 비율이 늘면 Hunger 등급이 오른다", TestHungerLadder, ref passed, ref failed, report);
            Run("요구에 못 미치면 Hunger 는 None", TestHungerBelowRequired, ref passed, ref failed, report);
            Run("요구 전력 0 이면 Hunger 는 None (0 나눗셈 방어)", TestHungerZeroRequired, ref passed, ref failed, report);
            Run("등급 경계가 정확히 그 값에서 갈린다", TestExactBoundaries, ref passed, ref failed, report);
            Run("초과 비율은 음수가 되지 않는다", TestExcessRatioClamped, ref passed, ref failed, report);
            Run("기본 임계치가 단조롭다", TestMonotonic, ref passed, ref failed, report);
            Run("뒤집힌 임계치를 단조성 검사가 잡는다", TestMonotonicCatchesInversion, ref passed, ref failed, report);
            Run("두 축이 같은 층에서 상충한다 (지배 전략 없음)", TestAxesConflict, ref passed, ref failed, report);
            Run("표시 이름이 등급을 그대로 읽는다", TestDisplayNames, ref passed, ref failed, report);

            return (passed, failed, report.ToString());
        }

        private static MercyHungerThresholds T => MercyHungerThresholds.Default;

        private static void TestDefaults()
        {
            var t = MercyHungerThresholds.Default;
            Assert(t.MercySpinsForI == 1 && t.MercySpinsForII == 2 && t.MercySpinsForIII == 3,
                   $"Mercy 시작값이 1/2/3 이 아니다: {t.MercySpinsForI}/{t.MercySpinsForII}/{t.MercySpinsForIII}");
            Assert(Near(t.HungerExcessForI, 0.25f) && Near(t.HungerExcessForII, 0.60f)
                   && Near(t.HungerExcessForIII, 1.00f),
                   $"Hunger 시작값이 25/60/100% 가 아니다: {t.HungerExcessForI}/{t.HungerExcessForII}/{t.HungerExcessForIII}");
        }

        private static void TestMercyLadder()
        {
            Assert(MercyHunger.MercyFor(0, 0, T) == MercyGrade.None, "남은 0 이 None 이 아니다");
            Assert(MercyHunger.MercyFor(1, 0, T) == MercyGrade.I, "남은 1 이 I 가 아니다");
            Assert(MercyHunger.MercyFor(2, 0, T) == MercyGrade.II, "남은 2 가 II 가 아니다");
            Assert(MercyHunger.MercyFor(3, 0, T) == MercyGrade.III, "남은 3 이 III 가 아니다");
            Assert(MercyHunger.MercyFor(9, 0, T) == MercyGrade.III, "남은 9 가 III 를 넘었다 — 상한이 없다");
        }

        private static void TestMercyDecaysWithExtraSpins()
        {
            // §6.2 「스핀을 사용할수록 Mercy 기회는 낮아지고」.
            // 이게 깨지면 과수확이 공짜가 되어 브레이크를 걸 이유가 사라진다.
            Assert(MercyHunger.MercyFor(3, 0, T) == MercyGrade.III, "기준점이 III 가 아니다");
            Assert(MercyHunger.MercyFor(3, 1, T) == MercyGrade.II, "추가 1 회에 II 로 안 내려갔다");
            Assert(MercyHunger.MercyFor(3, 2, T) == MercyGrade.I, "추가 2 회에 I 로 안 내려갔다");
            Assert(MercyHunger.MercyFor(3, 3, T) == MercyGrade.None, "추가 3 회에 None 이 안 됐다");
            Assert(MercyHunger.MercyFor(3, 99, T) == MercyGrade.None, "과도한 추가 스핀이 음수로 감싸돌았다");
        }

        private static void TestMercyNeverReached()
        {
            Assert(MercyHunger.MercyFor(-1, 0, T) == MercyGrade.None,
                   "요구 전력을 끝내 못 채웠는데 Mercy 가 붙었다");
            // −1 과 0 은 다르다. 0 은 「마지막 스핀에 겨우 채웠다」로 달성이다.
            Assert(MercyHunger.MercyFor(0, 0, T) == MercyGrade.None, "남은 0 도 None 이어야 한다");
            Assert(MercyHunger.MercyFor(-1, 0, T) == MercyHunger.MercyFor(0, 0, T),
                   "이 두 값은 등급으로는 같지만 의미가 다르다 — 등급만 같으면 된다");
        }

        private static void TestHungerLadder()
        {
            Assert(MercyHunger.HungerFor(100f, 100f, T) == HungerGrade.None, "초과 0% 가 None 이 아니다");
            Assert(MercyHunger.HungerFor(124f, 100f, T) == HungerGrade.None, "초과 24% 가 None 이 아니다");
            Assert(MercyHunger.HungerFor(130f, 100f, T) == HungerGrade.I, "초과 30% 가 I 이 아니다");
            Assert(MercyHunger.HungerFor(170f, 100f, T) == HungerGrade.II, "초과 70% 가 II 가 아니다");
            Assert(MercyHunger.HungerFor(250f, 100f, T) == HungerGrade.III, "초과 150% 가 III 가 아니다");
        }

        private static void TestHungerBelowRequired()
        {
            Assert(MercyHunger.HungerFor(50f, 100f, T) == HungerGrade.None,
                   "요구에 못 미쳤는데 Hunger 가 붙었다");
            Assert(MercyHunger.HungerFor(0f, 100f, T) == HungerGrade.None, "전력 0 에 Hunger 가 붙었다");
        }

        private static void TestHungerZeroRequired()
        {
            // 0 으로 나누면 무한대가 나오고 무한대는 모든 임계치를 넘는다.
            Assert(MercyHunger.HungerFor(500f, 0f, T) == HungerGrade.None,
                   "요구 0 에서 Hunger 가 붙었다 — 0 나눗셈이 III 를 만든다");
            Assert(MercyHunger.HungerFor(500f, -10f, T) == HungerGrade.None, "음수 요구에서 Hunger 가 붙었다");
        }

        private static void TestExactBoundaries()
        {
            Assert(MercyHunger.HungerFor(125f, 100f, T) == HungerGrade.I, "정확히 25% 가 I 이 아니다");
            Assert(MercyHunger.HungerFor(160f, 100f, T) == HungerGrade.II, "정확히 60% 가 II 가 아니다");
            Assert(MercyHunger.HungerFor(200f, 100f, T) == HungerGrade.III, "정확히 100% 가 III 가 아니다");
        }

        private static void TestExcessRatioClamped()
        {
            Assert(Near(MercyHunger.ExcessRatio(150f, 100f), 0.5f), "초과 비율이 0.5 가 아니다");
            Assert(Near(MercyHunger.ExcessRatio(50f, 100f), 0f), "미달에서 비율이 음수다");
            Assert(Near(MercyHunger.ExcessRatio(50f, 0f), 0f), "요구 0 에서 비율이 0 이 아니다");
        }

        private static void TestMonotonic()
            => Assert(MercyHungerThresholds.Default.IsMonotonic, "기본 임계치가 단조롭지 않다");

        private static void TestMonotonicCatchesInversion()
        {
            var bad = new MercyHungerThresholds(3, 2, 1, 1.0f, 0.6f, 0.25f);
            Assert(!bad.IsMonotonic, "뒤집힌 임계치를 단조성 검사가 통과시켰다");
        }

        private static void TestAxesConflict()
        {
            // 층당 5스핀, 2스핀에 요구 달성 → 남은 3.
            // 멈추면 Mercy III · Hunger None. 세 번 더 돌리면 Mercy None 이 되고
            // 대신 Hunger 를 노린다. **어느 한쪽이 언제나 우세하면 선택이 아니다.**
            const int firstReachRemaining = 3;

            MercyGrade stop = MercyHunger.MercyFor(firstReachRemaining, 0, T);
            HungerGrade stopHunger = MercyHunger.HungerFor(100f, 100f, T);
            Assert(stop == MercyGrade.III && stopHunger == HungerGrade.None,
                   "즉시 멈춤이 Mercy III · Hunger None 이 아니다");

            MercyGrade greed = MercyHunger.MercyFor(firstReachRemaining, 3, T);
            HungerGrade greedHunger = MercyHunger.HungerFor(220f, 100f, T);
            Assert(greed == MercyGrade.None && greedHunger == HungerGrade.III,
                   "끝까지 돌림이 Mercy None · Hunger III 가 아니다");

            // 둘 다 최고 등급이 되는 경로가 있으면 저울이 아니다.
            Assert(!(MercyHunger.MercyFor(firstReachRemaining, 3, T) == MercyGrade.III
                     && greedHunger == HungerGrade.III),
                   "같은 플레이가 Mercy III 와 Hunger III 를 동시에 얻는다 — 지배 전략이다");
        }

        private static void TestDisplayNames()
        {
            Assert(MercyGrade.II.DisplayName() == "Mercy II", "Mercy 표시 이름이 틀리다");
            Assert(HungerGrade.III.DisplayName() == "Hunger III", "Hunger 표시 이름이 틀리다");
            Assert(MercyGrade.None.DisplayName() == "—", "None 표시가 대시가 아니다");
            Assert(HungerGrade.None.DisplayName() == "—", "None 표시가 대시가 아니다");
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Run(string name, Action test, ref int passed, ref int failed, StringBuilder report)
        {
            try { test(); passed++; report.AppendLine($"  PASS  {name}"); }
            catch (Exception e) { failed++; report.AppendLine($"  FAIL  {name}\n        {e.Message}"); }
        }
    }
}
