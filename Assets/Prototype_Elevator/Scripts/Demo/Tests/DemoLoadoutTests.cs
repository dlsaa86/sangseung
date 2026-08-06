using System;
using System.Collections.Generic;
using System.Text;
using Ascend.Prototype.Build;

namespace Ascend.Prototype.Demo.Tests
{
    /// <summary>
    /// 데모 적재 명세 검사. `BuildTests`와 같은 헤드리스 러너 규약을 쓴다
    /// (NUnit 미사용 근거는 `DECISION_LOG.md` D-20260730-06).
    ///
    /// 이 스위트가 지키는 것은 하나다 — **고른 것과 실린 것이 같은가.** 데모 도구가
    /// 조용히 다른 것을 실으면 그 위에서 내린 「이 빌드는 재미없다」는 판정이 전부
    /// 무효가 된다. 측정 도구가 거짓말하면 측정값이 아니라 결론이 썩는다
    /// (`PD2930_REPORT_20260806.md` §2가 같은 실패를 기록한다).
    /// </summary>
    public static class DemoLoadoutTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("빈 명세는 아무것도 싣지 않는다", TestEmptyApplies, ref passed, ref failed, report);
            Run("고른 것이 그대로 실린다", TestAppliesExactly, ref passed, ref failed, report);
            Run("적용은 기존 적재를 비우고 시작한다", TestApplyClearsFirst, ref passed, ref failed, report);
            Run("중복 id 는 한 번만 들어간다", TestNoDuplicates, ref passed, ref failed, report);
            Run("없는 id 는 문제로 보고된다", TestUnknownIdReported, ref passed, ref failed, report);
            Run("슬롯 초과는 문제로 보고되고 상한까지만 실린다", TestOverflowReported, ref passed, ref failed, report);
            Run("부호화·복호화가 왕복한다", TestEncodeRoundTrip, ref passed, ref failed, report);
            Run("축 프리셋은 그 축만 담는다", TestAxisPresetIsPure, ref passed, ref failed, report);
            Run("축 프리셋이 모든 축에서 비지 않는다", TestEveryAxisHasItems, ref passed, ref failed, report);
            Run("축마다 하나씩은 축이 겹치지 않는다", TestOnePerAxisIsDistinct, ref passed, ref failed, report);
            Run("보충은 이미 실린 것을 건드리지 않는다", TestTopUpKeepsExisting, ref passed, ref failed, report);
            Run("보충이 내린 자리를 채운다", TestTopUpRefillsRemoved, ref passed, ref failed, report);
            Run("null 적재에도 터지지 않는다", TestNullLoadoutIsSafe, ref passed, ref failed, report);

            return (passed, failed, report.ToString());
        }

        // ── 검사 ────────────────────────────────────────────────────────────

        private static string TestEmptyApplies()
        {
            var loadout = new BuildLoadout();
            int applied = new DemoLoadoutSpec().ApplyTo(loadout);
            if (applied != 0) return $"빈 명세가 {applied}개를 실었다";
            if (loadout.Count != 0) return $"적재가 {loadout.Count}개로 남았다";
            return null;
        }

        private static string TestAppliesExactly()
        {
            IReadOnlyList<BuildItem> all = BuildCatalog.All;
            if (all.Count < 3) return $"카탈로그가 {all.Count}개뿐이라 검사할 수 없다";

            var spec = new DemoLoadoutSpec(new[] { all[0].Id, all[1].Id, all[2].Id });
            var loadout = new BuildLoadout();
            int applied = spec.ApplyTo(loadout);

            if (applied != 3) return $"3개를 골랐는데 {applied}개가 실렸다";
            for (int i = 0; i < 3; i++)
                if (!loadout.Contains(all[i].Id)) return $"'{all[i].Id}' 가 실리지 않았다";
            return null;
        }

        private static string TestApplyClearsFirst()
        {
            IReadOnlyList<BuildItem> all = BuildCatalog.All;
            if (all.Count < 2) return "카탈로그가 부족하다";

            var loadout = new BuildLoadout();
            loadout.Add(all[0]);

            DemoLoadoutSpec.Solo(all[1].Id).ApplyTo(loadout);

            if (loadout.Contains(all[0].Id)) return "적용 전 적재가 남았다 — 관측 대상이 섞인다";
            if (!loadout.Contains(all[1].Id)) return "명세의 품목이 실리지 않았다";
            if (loadout.Count != 1) return $"적재가 {loadout.Count}개다";
            return null;
        }

        private static string TestNoDuplicates()
        {
            string id = BuildCatalog.All[0].Id;
            var spec = new DemoLoadoutSpec(new[] { id, id, id });
            if (spec.Count != 1) return $"중복 3개가 {spec.Count}개로 남았다";
            return null;
        }

        private static string TestUnknownIdReported()
        {
            var spec = new DemoLoadoutSpec(new[] { "존재하지-않는-id" });
            List<string> problems = spec.Problems();
            if (problems.Count == 0) return "없는 id 인데 문제가 보고되지 않았다";

            var loadout = new BuildLoadout();
            if (spec.ApplyTo(loadout) != 0) return "없는 id 가 실렸다";
            return null;
        }

        private static string TestOverflowReported()
        {
            IReadOnlyList<BuildItem> all = BuildCatalog.All;
            int want = BuildLoadout.MaxSlots + 1;
            if (all.Count < want) return $"카탈로그가 {all.Count}개라 초과를 만들 수 없다";

            var spec = new DemoLoadoutSpec();
            for (int i = 0; i < want; i++) spec.Add(all[i].Id);

            if (spec.Problems().Count == 0) return "슬롯 초과가 보고되지 않았다";

            var loadout = new BuildLoadout();
            int applied = spec.ApplyTo(loadout);
            if (applied != BuildLoadout.MaxSlots)
                return $"상한 {BuildLoadout.MaxSlots} 인데 {applied}개가 실렸다";
            return null;
        }

        private static string TestEncodeRoundTrip()
        {
            IReadOnlyList<BuildItem> all = BuildCatalog.All;
            if (all.Count < 2) return "카탈로그가 부족하다";

            var spec = new DemoLoadoutSpec(new[] { all[0].Id, all[1].Id });
            DemoLoadoutSpec back = DemoLoadoutSpec.Decode(spec.Encode());

            if (back.Count != spec.Count) return $"{spec.Count}개가 {back.Count}개로 돌아왔다";
            for (int i = 0; i < spec.Count; i++)
                if (back.Ids[i] != spec.Ids[i]) return $"{i}번이 '{spec.Ids[i]}' → '{back.Ids[i]}'";

            if (DemoLoadoutSpec.Decode("").Count != 0) return "빈 문자열이 비지 않았다";
            if (DemoLoadoutSpec.Decode(null).Count != 0) return "null 이 비지 않았다";
            return null;
        }

        private static string TestAxisPresetIsPure()
        {
            for (int a = 0; a < DemoLoadoutSpec.Axes.Length; a++)
            {
                BuildAxis axis = DemoLoadoutSpec.Axes[a];
                DemoLoadoutSpec spec = DemoLoadoutSpec.ForAxis(axis);

                for (int i = 0; i < spec.Count; i++)
                {
                    BuildItem item = BuildCatalog.ById(spec.Ids[i]);
                    if (item == null) return $"{axis} 프리셋에 없는 id '{spec.Ids[i]}'";
                    if (item.Axis != axis) return $"{axis} 프리셋에 {item.Axis} 품목 '{item.Label}'";
                }
            }
            return null;
        }

        private static string TestEveryAxisHasItems()
        {
            // 축 하나가 비면 그 방향은 **체험할 수단이 없다**. 카탈로그가 줄었을 때
            // 조용히 그렇게 되는 것을 막는다.
            for (int a = 0; a < DemoLoadoutSpec.Axes.Length; a++)
            {
                BuildAxis axis = DemoLoadoutSpec.Axes[a];
                if (DemoLoadoutSpec.ForAxis(axis).Count == 0)
                    return $"{axis} 축에 품목이 하나도 없다 — 그 빌드는 체험할 수 없다";
            }
            return null;
        }

        private static string TestOnePerAxisIsDistinct()
        {
            DemoLoadoutSpec spec = DemoLoadoutSpec.OnePerAxis();
            if (spec.Count == 0) return "대조군이 비어 있다";

            var seen = new List<BuildAxis>();
            for (int i = 0; i < spec.Count; i++)
            {
                BuildItem item = BuildCatalog.ById(spec.Ids[i]);
                if (item == null) return $"없는 id '{spec.Ids[i]}'";
                if (seen.Contains(item.Axis)) return $"{item.Axis} 축이 두 번 들어갔다";
                seen.Add(item.Axis);
            }
            return null;
        }

        private static string TestTopUpKeepsExisting()
        {
            IReadOnlyList<BuildItem> all = BuildCatalog.All;
            if (all.Count < 2) return "카탈로그가 부족하다";

            var loadout = new BuildLoadout();
            loadout.Add(all[0]);

            var spec = new DemoLoadoutSpec(new[] { all[0].Id, all[1].Id });
            int added = spec.TopUp(loadout);

            if (added != 1) return $"빠진 1개만 채워야 하는데 {added}개를 실었다";
            if (loadout.Count != 2) return $"적재가 {loadout.Count}개다";
            return null;
        }

        private static string TestTopUpRefillsRemoved()
        {
            IReadOnlyList<BuildItem> all = BuildCatalog.All;
            if (all.Count < 2) return "카탈로그가 부족하다";

            var spec = new DemoLoadoutSpec(new[] { all[0].Id, all[1].Id });
            var loadout = new BuildLoadout();
            spec.ApplyTo(loadout);

            loadout.Remove(all[0].Id);          // 목적지에서 내린 상황
            if (spec.TopUp(loadout) != 1) return "내린 자리가 채워지지 않았다";
            if (!loadout.Contains(all[0].Id)) return "재탑승했는데 적재에 없다";
            return null;
        }

        private static string TestNullLoadoutIsSafe()
        {
            var spec = DemoLoadoutSpec.Solo(BuildCatalog.All[0].Id);
            if (spec.ApplyTo(null) != 0) return "null 적재에 실었다고 보고했다";
            if (spec.TopUp(null) != 0) return "null 적재를 보충했다고 보고했다";
            return null;
        }

        // ── 러너 ────────────────────────────────────────────────────────────

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
    }
}
