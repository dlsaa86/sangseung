using System;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Risk;

namespace Ascend.Prototype.Effects.Tests
{
    /// <summary>
    /// `PresentationProfile` 이 **실제로 읽히는가**를 본다 (`UP-TECH-09` ⑩).
    ///
    /// **왜 값 대조만으로는 부족한가:** 에셋의 기본값이 코드 프리셋과 글자 하나까지 같다.
    /// 그래서 배선을 통째로 떼어내도 숫자는 그대로고, 「데이터화했다」를 반증할 방법이
    /// 없다 — `AccessibilityProfile` 이 정확히 그 상태로 한동안 통과하고 있었다
    /// (`RiskStateView.AccessibilitySource` 주석). 여기서는 둘을 짝으로 본다:
    /// ① **출처** — 폴백이면 「코드 프리셋」이라고 적히므로 단정이 통과하지 않는다.
    /// ② **값이 흐르는가** — 에셋의 배열을 코드 프리셋과 **다른 값**으로 덮어쓰고
    ///    디렉터의 예산이 따라 움직이는지 본다. 안 움직이면 읽는 척만 하는 것이다.
    ///
    /// 씬을 열지 않는다. 비활성 `GameObject` 에 컴포넌트를 붙이면 `Awake` 가 돌지 않으므로
    /// (파티클 5종·머티리얼 생성 없이) 값 해석 경로만 떼어 볼 수 있다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class PresentationBindingTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("코드 프리셋이 예전 하드코딩 값과 같다 (동작 무변화)",
                TestCodePresetKeepsHistoricalNumbers, ref passed, ref failed, report);
            Run("정적 상한과 프로파일 기본값이 어긋나지 않는다",
                TestStaticMatchesProfileDefaults, ref passed, ref failed, report);
            Run("배선하지 않으면 출처가 「코드 프리셋」이다",
                TestUnwiredReportsCodePreset, ref passed, ref failed, report);
            Run("배선하면 출처가 에셋 이름이 된다",
                TestWiredReportsAssetName, ref passed, ref failed, report);
            Run("에셋 값을 바꾸면 디렉터 예산이 따라 바뀐다 (반증 대조)",
                TestAssetValuesReachTheDirector, ref passed, ref failed, report);
            Run("배열이 짧으면 코드 기본값으로 폴백한다",
                TestShortArrayFallsBack, ref passed, ref failed, report);
            Run("단계가 올라갈수록 상한이 줄지 않는다",
                TestBudgetIsMonotonic, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Effects (연출 프로파일 배선) Tests ===\n");
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

        // ── 값이 바뀌지 않았다는 것부터 ────────────────────────────────────────

        /// <summary>
        /// 데이터화는 **동작을 바꾸지 않는 변경**이어야 했다. 숫자를 하나라도 흘리면
        /// 다음 캡처가 직전 승인본과 달라지고, 그 차이의 원인이 리팩터링이라는 사실은
        /// 아무 데도 안 남는다. 그래서 옛 switch 문의 네 숫자를 여기 못박는다.
        /// </summary>
        private static string TestCodePresetKeepsHistoricalNumbers()
        {
            if (AmbientParticleDirector.MaxParticlesFor(RiskLevel.Stable) != 24)
                return $"Stable {AmbientParticleDirector.MaxParticlesFor(RiskLevel.Stable)}, 기대 24";
            if (AmbientParticleDirector.MaxParticlesFor(RiskLevel.Strain) != 48)
                return $"Strain {AmbientParticleDirector.MaxParticlesFor(RiskLevel.Strain)}, 기대 48";
            if (AmbientParticleDirector.MaxParticlesFor(RiskLevel.Critical) != 80)
                return $"Critical {AmbientParticleDirector.MaxParticlesFor(RiskLevel.Critical)}, 기대 80";
            if (AmbientParticleDirector.MaxParticlesFor(RiskLevel.Collapse) != 120)
                return $"Collapse {AmbientParticleDirector.MaxParticlesFor(RiskLevel.Collapse)}, 기대 120";
            return null;
        }

        private static string TestStaticMatchesProfileDefaults()
        {
            PresentationSnapshot defaults = PresentationProfile.DefaultSnapshot;
            foreach (RiskLevel level in Levels)
            {
                if (AmbientParticleDirector.MaxParticlesFor(level) != defaults.MaxParticlesFor(level))
                    return $"{level} — 정적 {AmbientParticleDirector.MaxParticlesFor(level)} vs " +
                           $"프로파일 {defaults.MaxParticlesFor(level)}";
            }
            return null;
        }

        // ── 출처 ──────────────────────────────────────────────────────────────

        private static string TestUnwiredReportsCodePreset()
        {
            GameObject host = null;
            try
            {
                AmbientParticleDirector director = NewDirector(out host);
                director.SetPresentationProfile(null);

                if (director.PresentationSource != AmbientParticleDirector.CodePresetSource)
                    return $"폴백 출처가 「{director.PresentationSource}」 다";
                // 하네스의 단정이 `EndsWith("Profile")` 이다. 폴백 이름이 그것을 만족하면
                // 배선이 끊겨도 통과한다 — 그 함정을 여기서 막는다.
                if (director.PresentationSource.EndsWith("Profile"))
                    return "폴백 출처가 「Profile」 로 끝난다 — 출처 단정이 공허해진다";
                if (director.BudgetFor(RiskLevel.Collapse) != 120)
                    return $"폴백 예산이 {director.BudgetFor(RiskLevel.Collapse)} 다";
                return null;
            }
            finally { Kill(host); }
        }

        private static string TestWiredReportsAssetName()
        {
            GameObject host = null;
            PresentationProfile profile = null;
            try
            {
                AmbientParticleDirector director = NewDirector(out host);
                profile = ScriptableObject.CreateInstance<PresentationProfile>();
                profile.name = "PresentationProfile";
                director.SetPresentationProfile(profile);

                if (director.PresentationSource != "PresentationProfile")
                    return $"출처가 「{director.PresentationSource}」 다";
                if (!director.PresentationSource.EndsWith("Profile"))
                    return "출처가 「Profile」 로 끝나지 않아 하네스 단정을 통과할 수 없다";
                return null;
            }
            finally { Kill(host); Kill(profile); }
        }

        // ── 값이 실제로 흐르는가 ──────────────────────────────────────────────

        /// <summary>
        /// 에셋의 사적 직렬화 필드를 코드 프리셋과 **다른 값**으로 덮어쓴다. 프로퍼티가
        /// 없어 이 방법뿐이고, 없으면 이 배선은 반증 불가능한 채로 남는다.
        /// </summary>
        private static string TestAssetValuesReachTheDirector()
        {
            GameObject host = null;
            PresentationProfile profile = null;
            try
            {
                AmbientParticleDirector director = NewDirector(out host);
                profile = ScriptableObject.CreateInstance<PresentationProfile>();
                profile.name = "밀도실험Profile";
                JsonUtility.FromJsonOverwrite("{\"_maxParticles\":[7,9,11,13]}", profile);

                if (profile.Snapshot().MaxParticlesFor(RiskLevel.Strain) != 9)
                    return "에셋 자체가 덮어써지지 않았다 — 이 대조는 아무것도 증명하지 못한다";

                director.SetPresentationProfile(profile);

                if (director.BudgetFor(RiskLevel.Stable) != 7
                    || director.BudgetFor(RiskLevel.Strain) != 9
                    || director.BudgetFor(RiskLevel.Critical) != 11
                    || director.BudgetFor(RiskLevel.Collapse) != 13)
                    return $"디렉터가 에셋 값을 읽지 않는다 — " +
                           $"{director.BudgetFor(RiskLevel.Stable)}/{director.BudgetFor(RiskLevel.Strain)}/" +
                           $"{director.BudgetFor(RiskLevel.Critical)}/{director.BudgetFor(RiskLevel.Collapse)}, " +
                           "기대 7/9/11/13";

                // 정적 폴백은 인스턴스 배선에 끌려다니지 않는다 — 둘은 다른 질문이다.
                if (AmbientParticleDirector.MaxParticlesFor(RiskLevel.Collapse) != 120)
                    return "에셋을 꽂았더니 코드 프리셋까지 바뀌었다";

                // 떼어내면 되돌아온다. 「한 번 읽고 굳었다」를 배제한다.
                director.SetPresentationProfile(null);
                if (director.BudgetFor(RiskLevel.Collapse) != 120)
                    return "에셋을 떼어냈는데 예산이 코드 프리셋으로 돌아오지 않는다";
                return null;
            }
            finally { Kill(host); Kill(profile); }
        }

        private static string TestShortArrayFallsBack()
        {
            GameObject host = null;
            PresentationProfile profile = null;
            try
            {
                AmbientParticleDirector director = NewDirector(out host);
                profile = ScriptableObject.CreateInstance<PresentationProfile>();
                profile.name = "짧은배열Profile";
                JsonUtility.FromJsonOverwrite("{\"_maxParticles\":[5,6]}", profile);
                director.SetPresentationProfile(profile);

                // 손으로 배열을 줄인 에셋이 파티클을 통째로 죽이면 안 된다.
                foreach (RiskLevel level in Levels)
                {
                    if (director.BudgetFor(level) <= 0)
                        return $"{level} 예산이 {director.BudgetFor(level)} 다 — 파티클이 통째로 죽는다";
                }
                if (director.BudgetFor(RiskLevel.Collapse) != 120)
                    return $"짧은 배열에서 폴백이 {director.BudgetFor(RiskLevel.Collapse)} 다";
                return null;
            }
            finally { Kill(host); Kill(profile); }
        }

        private static string TestBudgetIsMonotonic()
        {
            GameObject host = null;
            try
            {
                AmbientParticleDirector director = NewDirector(out host);
                for (int i = 1; i < Levels.Length; i++)
                {
                    if (director.BudgetFor(Levels[i]) < director.BudgetFor(Levels[i - 1]))
                        return $"{Levels[i]} 상한이 {Levels[i - 1]} 보다 작다 — 위험이 가벼워 보인다";
                }
                return null;
            }
            finally { Kill(host); }
        }

        // ── 공통 ──────────────────────────────────────────────────────────────

        private static readonly RiskLevel[] Levels =
        {
            RiskLevel.Stable, RiskLevel.Strain, RiskLevel.Critical, RiskLevel.Collapse,
        };

        /// <summary>
        /// 비활성으로 만든 뒤 컴포넌트를 붙인다. 그래야 `Awake` 가 돌지 않아
        /// 파티클 5종·공유 머티리얼·`FindAnyObjectByType` 가 전부 생략된다 —
        /// 이 스위트가 묻는 것은 값 해석 경로뿐이다.
        /// </summary>
        private static AmbientParticleDirector NewDirector(out GameObject host)
        {
            host = new GameObject("__PresentationBindingProbe__");
            host.SetActive(false);
            return host.AddComponent<AmbientParticleDirector>();
        }

        private static void Kill(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
