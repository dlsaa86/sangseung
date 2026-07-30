using System;
using System.Text;

namespace Ascend.Prototype.Risk.Tests
{
    /// <summary>
    /// 위험 산출기의 헤드리스 검증. `TECH_SPEC.md` §11 "과적과 위험 상태 계산" 항목이다.
    /// NUnit에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class RiskEvaluatorTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("위험 요인 없으면 Stable", TestStable, ref passed, ref failed, report);
            Run("잔류 저항이 쌓이면 Warning", TestResidualRaisesWarning, ref passed, ref failed, report);
            Run("과수확 1회만으로 Stable 을 벗어난다", TestSingleOverharvestLeavesStable, ref passed, ref failed, report);
            Run("과수확이 Critical 로 밀어올린다", TestOverharvestCritical, ref passed, ref failed, report);
            Run("과적이 점수를 올린다", TestOverloadRaisesScore, ref passed, ref failed, report);
            Run("스핀 소진 + 전력 미달이 점수를 올린다", TestShortfallRaisesScore, ref passed, ref failed, report);
            Run("히스테리시스 — 경계에서 떨지 않는다", TestHysteresis, ref passed, ref failed, report);
            Run("진입 임계값이 이탈 임계값보다 높다", TestThresholdOrdering, ref passed, ref failed, report);
            Run("층 실패는 점수와 무관하게 Collapse", TestFailureIsCollapse, ref passed, ref failed, report);
            Run("실패가 풀리면 점수 기반으로 복귀", TestCollapseRecovers, ref passed, ref failed, report);
            Run("원인 설명이 실제 요인을 담는다", TestExplain, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Risk Evaluator Tests ===\n");
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

        private static RiskInputs Clean() => new RiskInputs(0, 0, 0, false, 3, 1.2f, false);

        private static string TestStable()
        {
            var evaluator = new RiskEvaluator();
            if (evaluator.Evaluate(Clean()) != RiskLevel.Stable)
                return $"단계 {evaluator.Current} / 점수 {evaluator.CurrentScore}";
            if (evaluator.CurrentScore != 0f) return $"점수 {evaluator.CurrentScore}, 기대 0";
            return null;
        }

        private static string TestResidualRaisesWarning()
        {
            var evaluator = new RiskEvaluator();
            // 흡수체 2 + 증식체 2 = 2.0 + 2.4 = 4.4 → WarningEnter(3.0) 초과, CriticalEnter(7.0) 미만
            var inputs = new RiskInputs(2, 2, 0, false, 3, 0.8f, false);
            RiskLevel level = evaluator.Evaluate(in inputs);
            if (level != RiskLevel.Warning) return $"단계 {level} / 점수 {evaluator.CurrentScore}";
            return null;
        }

        private static string TestOverharvestCritical()
        {
            var evaluator = new RiskEvaluator();
            // 과수확 2회(5.2) + 흡수체 2(2.0) = 7.2 → CriticalEnter(7.0) 초과
            var inputs = new RiskInputs(2, 0, 2, false, 1, 1.1f, false);
            RiskLevel level = evaluator.Evaluate(in inputs);
            if (level != RiskLevel.Critical) return $"단계 {level} / 점수 {evaluator.CurrentScore}";
            return null;
        }

        private static string TestOverloadRaisesScore()
        {
            var evaluator = new RiskEvaluator();
            var light = new RiskInputs(1, 0, 0, false, 3, 1f, false);
            var heavy = new RiskInputs(1, 0, 0, true, 3, 1f, false);
            float lightScore = evaluator.Score(in light);
            float heavyScore = evaluator.Score(in heavy);
            if (heavyScore <= lightScore) return $"과적 {heavyScore} ≤ 비과적 {lightScore}";
            if (Math.Abs((heavyScore - lightScore) - evaluator.OverloadScore) > 0.001f)
                return "과적 가산이 설정값과 다름";
            return null;
        }

        private static string TestShortfallRaisesScore()
        {
            var evaluator = new RiskEvaluator();
            // 스핀이 남아 있으면 만회할 수 있으므로 가산되지 않는다.
            var canRecover = new RiskInputs(0, 0, 0, false, 1, 0.5f, false);
            var cannot = new RiskInputs(0, 0, 0, false, 0, 0.5f, false);
            if (evaluator.Score(in canRecover) != 0f)
                return $"스핀이 남았는데 미달 가산됨: {evaluator.Score(in canRecover)}";
            if (Math.Abs(evaluator.Score(in cannot) - evaluator.ShortfallScore) > 0.001f)
                return $"미달 가산 {evaluator.Score(in cannot)}, 기대 {evaluator.ShortfallScore}";

            // 스핀은 없지만 이미 요구 전력을 넘겼으면 미달이 아니다.
            var met = new RiskInputs(0, 0, 0, false, 0, 1.0f, false);
            if (evaluator.Score(in met) != 0f) return "달성 상태에서 미달 가산됨";
            return null;
        }

        private static string TestHysteresis()
        {
            // 가중치를 테스트가 직접 정한다. 밸런스 다이얼이 움직여도 히스테리시스라는
            // 성질 자체는 유지되어야 하고, 이 테스트가 검증하는 것은 그 성질이다.
            var evaluator = new RiskEvaluator
            {
                AbsorberWeight = 1f, ProliferatorWeight = 1f, OverharvestWeight = 1f,
                WarningEnter = 3f, WarningExit = 2f, CriticalEnter = 7f, CriticalExit = 5.5f,
            };

            evaluator.Evaluate(new RiskInputs(8, 0, 0, false, 1, 1.1f, false));   // 8 → Critical
            if (evaluator.Current != RiskLevel.Critical) return "선행 조건 실패: Critical 진입 안 됨";

            // 6.0 — CriticalEnter(7) 아래지만 CriticalExit(5.5) 위. 단계는 유지되어야 한다.
            RiskLevel held = evaluator.Evaluate(new RiskInputs(6, 0, 0, false, 1, 1.1f, false));
            if (held != RiskLevel.Critical)
                return $"이탈 임계값 위인데 단계가 내려감 ({held}, 점수 {evaluator.CurrentScore})";

            // 5.0 — CriticalExit 아래. 이제 Warning 으로 내려간다.
            RiskLevel dropped = evaluator.Evaluate(new RiskInputs(5, 0, 0, false, 1, 1.1f, false));
            if (dropped != RiskLevel.Warning)
                return $"이탈 임계값 아래인데 Critical 유지 ({dropped}, 점수 {evaluator.CurrentScore})";

            // 2.5 — WarningEnter(3) 아래지만 WarningExit(2) 위. Warning 유지.
            RiskLevel stillWarning = evaluator.Evaluate(new RiskInputs(2, 0, 0, false, 1, 1.1f, false));
            if (stillWarning != RiskLevel.Warning)
                return $"Warning 이탈 임계값 위인데 내려감 ({stillWarning}, 점수 {evaluator.CurrentScore})";

            // 1.0 — WarningExit 아래. Stable 복귀.
            if (evaluator.Evaluate(new RiskInputs(1, 0, 0, false, 1, 1.1f, false)) != RiskLevel.Stable)
                return $"충분히 내렸는데 Stable 로 복귀하지 않음 (점수 {evaluator.CurrentScore})";

            // 같은 점수를 반복 입력해도 단계가 흔들리지 않아야 한다.
            var steady = new RiskInputs(4, 0, 0, false, 3, 0.8f, false);
            RiskLevel first = evaluator.Evaluate(in steady);
            for (int i = 0; i < 50; i++)
                if (evaluator.Evaluate(in steady) != first)
                    return "같은 입력에서 단계가 흔들린다";
            return null;
        }

        /// <summary>
        /// `MASTER_PRD.md` §7의 요구를 수치로 고정한다 — 과수확은 "공간적 사건"이어야 한다.
        /// 한 번 당겼는데 방이 Stable 그대로면 그 문장이 거짓이 된다.
        /// 실제로 이 조건은 PlayMode 검증에서 먼저 깨졌고(가중치 2.6), 그래서 여기 남긴다.
        /// </summary>
        private static string TestSingleOverharvestLeavesStable()
        {
            var evaluator = new RiskEvaluator();
            if (evaluator.OverharvestWeight < evaluator.WarningEnter)
                return $"과수확 가중치 {evaluator.OverharvestWeight} < Warning 진입 {evaluator.WarningEnter}";

            // 잔류도 과적도 없는 가장 깨끗한 상황에서 과수확만 1회.
            var inputs = new RiskInputs(0, 0, 1, false, 2, 1.1f, false);
            RiskLevel level = evaluator.Evaluate(in inputs);
            if (level == RiskLevel.Stable)
                return $"과수확 1회 후에도 Stable (점수 {evaluator.CurrentScore})";
            return null;
        }

        private static string TestThresholdOrdering()
        {
            var evaluator = new RiskEvaluator();
            if (evaluator.WarningExit >= evaluator.WarningEnter)
                return $"Warning 이탈 {evaluator.WarningExit} ≥ 진입 {evaluator.WarningEnter}";
            if (evaluator.CriticalExit >= evaluator.CriticalEnter)
                return $"Critical 이탈 {evaluator.CriticalExit} ≥ 진입 {evaluator.CriticalEnter}";
            if (evaluator.WarningEnter >= evaluator.CriticalEnter)
                return "Warning 진입이 Critical 진입보다 낮지 않다";
            return null;
        }

        private static string TestFailureIsCollapse()
        {
            var evaluator = new RiskEvaluator();
            // 점수는 0인데 실패다. 결과가 나왔으면 그게 상태다.
            var inputs = new RiskInputs(0, 0, 0, false, 3, 1.5f, true);
            if (evaluator.Evaluate(in inputs) != RiskLevel.Collapse)
                return $"단계 {evaluator.Current}";
            return null;
        }

        private static string TestCollapseRecovers()
        {
            var evaluator = new RiskEvaluator();
            evaluator.Evaluate(new RiskInputs(0, 0, 0, false, 3, 1.5f, true));
            if (evaluator.Current != RiskLevel.Collapse) return "선행 조건 실패";

            // 재시작으로 실패가 풀린 상황. Collapse에 갇히면 새 런이 계속 붉게 보인다.
            RiskLevel recovered = evaluator.Evaluate(Clean());
            if (recovered == RiskLevel.Collapse) return "실패가 풀렸는데 Collapse 유지";

            evaluator.Reset();
            if (evaluator.Current != RiskLevel.Stable) return "Reset 이 Stable 로 되돌리지 않음";
            return null;
        }

        private static string TestExplain()
        {
            var evaluator = new RiskEvaluator();
            string clean = evaluator.Explain(Clean());
            if (!clean.Contains("없음")) return $"위험 없음 설명이 이상하다: {clean}";

            var loaded = new RiskInputs(2, 1, 3, true, 0, 0.4f, false);
            string text = evaluator.Explain(in loaded);
            foreach (string token in new[] { "흡수체", "증식체", "과수확", "과적", "소진" })
                if (!text.Contains(token)) return $"설명에 '{token}' 이 빠졌다: {text}";
            return null;
        }
    }
}
