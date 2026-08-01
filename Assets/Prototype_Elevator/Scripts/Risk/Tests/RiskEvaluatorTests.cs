using System;
using System.Text;
using UnityEngine;

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
            Run("잔류 저항이 쌓이면 Strain", TestResidualRaisesStrain, ref passed, ref failed, report);
            Run("과수확 1회만으로 Stable 을 벗어난다", TestSingleOverharvestLeavesStable, ref passed, ref failed, report);
            Run("과수확이 Critical 로 밀어올린다", TestOverharvestCritical, ref passed, ref failed, report);
            Run("과적이 점수를 올린다", TestOverloadRaisesScore, ref passed, ref failed, report);
            Run("스핀 소진 + 전력 미달이 점수를 올린다", TestShortfallRaisesScore, ref passed, ref failed, report);
            Run("히스테리시스 — 경계에서 떨지 않는다", TestHysteresis, ref passed, ref failed, report);
            Run("진입 임계값이 이탈 임계값보다 높다", TestThresholdOrdering, ref passed, ref failed, report);
            Run("층 실패는 점수와 무관하게 Collapse", TestFailureIsCollapse, ref passed, ref failed, report);
            Run("실패가 풀리면 점수 기반으로 복귀", TestCollapseRecovers, ref passed, ref failed, report);
            Run("원인 설명이 실제 요인을 담는다", TestExplain, ref passed, ref failed, report);

            // 명도축 — 그룹 B. 색상만 움직이던 상태를 못박아 두지 않으면 다시 돌아간다.
            Run("앰비언트 명도가 4단계 단조 하강한다", TestAmbientValueMonotone, ref passed, ref failed, report);
            Run("인접 단계 명도차가 하한 0.08 이상 (프리셋 3종)", TestAmbientValueStep, ref passed, ref failed, report);
            Run("Stable 앰비언트 명도는 승인된 값 그대로다", TestAmbientCeilingUnchanged, ref passed, ref failed, report);
            Run("새 사다리가 색상 전용 경로보다 인접 간격이 크다", TestBeatsHueOnlyPath, ref passed, ref failed, report);
            Run("명도 교체가 색상·채도를 보존한다", TestWithValuePreservesHueSaturation, ref passed, ref failed, report);
            Run("역전된 프로파일도 단조 하강으로 교정된다", TestLadderRepairsNonMonotone, ref passed, ref failed, report);

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

        private static string TestResidualRaisesStrain()
        {
            var evaluator = new RiskEvaluator();
            // 흡수체 2 + 증식체 2 = 2.0 + 2.4 = 4.4 → StrainEnter(3.0) 초과, CriticalEnter(7.0) 미만
            var inputs = new RiskInputs(2, 2, 0, false, 3, 0.8f, false);
            RiskLevel level = evaluator.Evaluate(in inputs);
            if (level != RiskLevel.Strain) return $"단계 {level} / 점수 {evaluator.CurrentScore}";
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
                StrainEnter = 3f, StrainExit = 2f, CriticalEnter = 7f, CriticalExit = 5.5f,
            };

            evaluator.Evaluate(new RiskInputs(8, 0, 0, false, 1, 1.1f, false));   // 8 → Critical
            if (evaluator.Current != RiskLevel.Critical) return "선행 조건 실패: Critical 진입 안 됨";

            // 6.0 — CriticalEnter(7) 아래지만 CriticalExit(5.5) 위. 단계는 유지되어야 한다.
            RiskLevel held = evaluator.Evaluate(new RiskInputs(6, 0, 0, false, 1, 1.1f, false));
            if (held != RiskLevel.Critical)
                return $"이탈 임계값 위인데 단계가 내려감 ({held}, 점수 {evaluator.CurrentScore})";

            // 5.0 — CriticalExit 아래. 이제 Strain 으로 내려간다.
            RiskLevel dropped = evaluator.Evaluate(new RiskInputs(5, 0, 0, false, 1, 1.1f, false));
            if (dropped != RiskLevel.Strain)
                return $"이탈 임계값 아래인데 Critical 유지 ({dropped}, 점수 {evaluator.CurrentScore})";

            // 2.5 — StrainEnter(3) 아래지만 StrainExit(2) 위. Strain 유지.
            RiskLevel stillStrain = evaluator.Evaluate(new RiskInputs(2, 0, 0, false, 1, 1.1f, false));
            if (stillStrain != RiskLevel.Strain)
                return $"Strain 이탈 임계값 위인데 내려감 ({stillStrain}, 점수 {evaluator.CurrentScore})";

            // 1.0 — StrainExit 아래. Stable 복귀.
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
            if (evaluator.OverharvestWeight < evaluator.StrainEnter)
                return $"과수확 가중치 {evaluator.OverharvestWeight} < Strain 진입 {evaluator.StrainEnter}";

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
            if (evaluator.StrainExit >= evaluator.StrainEnter)
                return $"Strain 이탈 {evaluator.StrainExit} ≥ 진입 {evaluator.StrainEnter}";
            if (evaluator.CriticalExit >= evaluator.CriticalEnter)
                return $"Critical 이탈 {evaluator.CriticalExit} ≥ 진입 {evaluator.CriticalEnter}";
            if (evaluator.StrainEnter >= evaluator.CriticalEnter)
                return "Strain 진입이 Critical 진입보다 낮지 않다";
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

        // ── 앰비언트 명도축 ────────────────────────────────────────────────
        //
        // 7차 독립 판정: 「가장 약한 인접 쌍은 Strain↔Critical 이다. 둘 다 따뜻한 갈색 방이고
        // **밝기 차가 없다**. 「기준점으로부터의 거리」는 인접 구분 가능성을 재지 못한다 —
        // 색상만 움직이고 명도가 그대로면 거리가 벌어져도 사람 눈에는 같은 밴드다.」
        //
        // 그래서 여기서 재는 것은 색거리가 아니라 **인접 쌍의 명도차**다. 백로그 §5.1 이
        // 「색거리 6.1 → 13.4, 두 배」를 개선으로 적었다가 인접 쌍을 하나도 못 가른 이력이
        // 있다 — 두 배가 된 것은 Stable 로부터의 거리였고 사람이 보는 것은 옆 단계와의 차이다.

        /// <summary>
        /// 씬(`Prototype_Elevator.unity`)의 `m_AmbientSkyColor`. `m_AmbientMode: 3`(Flat)이라
        /// 이 값이 곧 <c>RenderSettings.ambientLight</c> 다.
        ///
        /// **한계**: 여기에 베껴 둔 상수라 씬 쪽이 바뀌면 이 테스트는 그 사실을 모른다.
        /// 다만 단조성과 최소 간격 자체는 <see cref="RiskAmbientLadder.Build"/> 가 설정과
        /// 무관하게 강제하고(<see cref="TestLadderRepairsNonMonotone"/>), 이 상수들은
        /// **출하 설정에서 실제로 얼마가 나오는지**를 못박기 위한 것이다.
        /// </summary>
        private static readonly Color SceneAmbient = new Color(0.26f, 0.27f, 0.31f);

        /// <summary><c>RiskStateView._ambientBlend</c> 의 기본값.</summary>
        private const float SceneAmbientBlend = 0.55f;

        /// <summary><c>RiskStateView._ambientValueFloorRatio</c> 의 기본값.</summary>
        private const float SceneFloorRatio = 0.20f;

        private static float[] LadderFor(RiskIntensity intensity)
        {
            RiskProfile[] levels = RiskProfile.Preset(intensity);
            float ceiling = RiskAmbientLadder.CeilingFor(SceneAmbient, levels[0], SceneAmbientBlend);
            return RiskAmbientLadder.Build(levels, ceiling, SceneFloorRatio);
        }

        private static string Describe(float[] ladder)
        {
            return $"[{ladder[0]:F4} / {ladder[1]:F4} / {ladder[2]:F4} / {ladder[3]:F4}]";
        }

        private static string TestAmbientValueMonotone()
        {
            foreach (RiskIntensity intensity in new[]
                     { RiskIntensity.Restrained, RiskIntensity.Standard, RiskIntensity.Heavy })
            {
                float[] ladder = LadderFor(intensity);
                for (int i = 1; i < ladder.Length; i++)
                    if (ladder[i] >= ladder[i - 1])
                        return $"{intensity}: {(RiskLevel)i} 가 {(RiskLevel)(i - 1)} 보다 어둡지 않다 {Describe(ladder)}";
            }
            return null;
        }

        private static string TestAmbientValueStep()
        {
            foreach (RiskIntensity intensity in new[]
                     { RiskIntensity.Restrained, RiskIntensity.Standard, RiskIntensity.Heavy })
            {
                float[] ladder = LadderFor(intensity);
                float step = RiskAmbientLadder.MinAdjacentStep(ladder);

                // 부동소수 여유 — Build 가 정확히 하한에 붙여 놓는 경우가 있다.
                if (step < RiskAmbientLadder.MinValueStep - 0.0005f)
                    return $"{intensity}: 최소 인접 명도차 {step:F4} < 하한 " +
                           $"{RiskAmbientLadder.MinValueStep:F2} {Describe(ladder)}";

                // 정보를 어둠에 숨기지 않는다 (VISUAL_SPEC §6 Collapse).
                if (ladder[ladder.Length - 1] <= RiskAmbientLadder.HardFloor)
                    return $"{intensity}: Collapse 명도 {ladder[ladder.Length - 1]:F4} 가 하한에 붙었다 — 암전이다";
            }
            return null;
        }

        private static string TestAmbientCeilingUnchanged()
        {
            // Stable 은 7차 판정이 채택한 기준점이다. 기준점을 같이 밀면 그 승인이 무효가 된다.
            RiskProfile[] levels = RiskProfile.Preset(RiskIntensity.Standard);
            float ceiling = RiskAmbientLadder.CeilingFor(SceneAmbient, levels[0], SceneAmbientBlend);
            float[] ladder = LadderFor(RiskIntensity.Standard);

            if (Mathf.Abs(ladder[0] - ceiling) > 0.0001f)
                return $"Stable 명도 {ladder[0]:F4} ≠ 색상 전용 경로의 Stable {ceiling:F4}";

            // 색상 전용 경로의 Stable 값과도 같아야 한다 — 바뀐 것은 아래 세 단계뿐이다.
            float[] hueOnly = RiskAmbientLadder.HueOnlyLadder(SceneAmbient, levels, SceneAmbientBlend);
            if (Mathf.Abs(ladder[0] - hueOnly[0]) > 0.0001f)
                return $"Stable 이 옛 경로({hueOnly[0]:F4})와 달라졌다: {ladder[0]:F4}";
            return null;
        }

        private static string TestBeatsHueOnlyPath()
        {
            foreach (RiskIntensity intensity in new[]
                     { RiskIntensity.Restrained, RiskIntensity.Standard, RiskIntensity.Heavy })
            {
                RiskProfile[] levels = RiskProfile.Preset(intensity);
                float[] hueOnly = RiskAmbientLadder.HueOnlyLadder(SceneAmbient, levels, SceneAmbientBlend);
                float[] ladder = LadderFor(intensity);

                float before = RiskAmbientLadder.MinAdjacentStep(hueOnly);
                float after = RiskAmbientLadder.MinAdjacentStep(ladder);
                if (after <= before)
                    return $"{intensity}: 최소 인접 명도차가 나아지지 않았다 " +
                           $"(색상 전용 {before:F4} {Describe(hueOnly)} → 새 사다리 {after:F4} {Describe(ladder)})";
            }
            return null;
        }

        private static string TestWithValuePreservesHueSaturation()
        {
            // 균일 RGB 배수가 곧 HSV 의 V 교체라는 근거. H·S 가 움직이면 7차가 채택한
            // 색조 진행(파랑이 빠지며 따뜻해지다 Collapse 에서 빨강이 뛴다)이 깨진다.
            foreach (RiskProfile profile in RiskProfile.Preset(RiskIntensity.Standard))
            {
                Color source = profile.LightColor;
                Color.RGBToHSV(source, out float h0, out float s0, out float v0);

                foreach (float target in new[] { 0.62f, 0.43f, 0.30f })
                {
                    Color moved = RiskAmbientLadder.WithValue(source, target);
                    Color.RGBToHSV(moved, out float h1, out float s1, out float v1);

                    if (Mathf.Abs(v1 - target) > 0.001f)
                        return $"V 가 목표에 안 맞는다: {v1:F4} ≠ {target:F2}";
                    if (Mathf.Abs(h1 - h0) > 0.002f)
                        return $"H 가 움직였다: {h0:F4} → {h1:F4} (V {v0:F3} → {v1:F3})";
                    if (Mathf.Abs(s1 - s0) > 0.002f)
                        return $"S 가 움직였다: {s0:F4} → {s1:F4}";
                }
            }
            return null;
        }

        private static string TestLadderRepairsNonMonotone()
        {
            // 프리셋은 사람이 인스펙터에서 고치는 승인 대기 데이터다. 「값이 그렇게 들어와서」가
            // 변명이 되면 안 된다 — 뒤집힌 데이터가 들어와도 화면은 뒤집히지 않아야 한다.
            RiskProfile[] levels = RiskProfile.Preset(RiskIntensity.Standard);
            levels[0].LightIntensity = 0.30f;   // Stable 을 가장 어둡게
            levels[1].LightIntensity = 1.00f;
            levels[2].LightIntensity = 0.95f;
            levels[3].LightIntensity = 0.90f;   // Collapse 를 가장 밝게

            float ceiling = RiskAmbientLadder.CeilingFor(SceneAmbient, levels[0], SceneAmbientBlend);
            float[] ladder = RiskAmbientLadder.Build(levels, ceiling, SceneFloorRatio);

            for (int i = 1; i < ladder.Length; i++)
                if (ladder[i] > ladder[i - 1] - RiskAmbientLadder.MinValueStep + 0.0005f)
                    return $"역전 입력이 교정되지 않았다 {Describe(ladder)}";
            if (ladder[ladder.Length - 1] < RiskAmbientLadder.HardFloor)
                return $"교정이 하한 아래로 내렸다: {ladder[ladder.Length - 1]:F4}";
            return null;
        }
    }
}
