using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ascend.Prototype.Build;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Headless
{
    /// <summary>
    /// 「명확히 다른 두 개 이상의 빌드 전략」(`MASTER_PRD` §2.3 · `CURRENT_PHASE` §2.3)이
    /// **데이터로 성립하는가**를 잰다.
    ///
    /// 방법: 품목마다 「그것 **하나만** 싣는 플레이어」를 만들어 완주율을 잰다.
    /// 무적재 기준선과의 차이가 그 품목의 **한계 기여**다. 12종의 기여가 서로 다르면
    /// 선택이 존재하고, 한 덩어리로 뭉치면 라벨만 다른 같은 품목이다.
    ///
    /// 한계 하나를 먼저 적는다 — **이 측정은 상호작용을 못 본다.** 「연쇄 조속기 + 사선
    /// 결속기」처럼 둘이 만나야 사는 조합은 단독 측정에서 둘 다 약하게 나온다.
    /// `pairs` 모드가 그 짝을 따로 본다.
    /// </summary>
    internal static class BuildProbe
    {
        /// <summary>층 도달만 재는 고정 정책. 계약은 항상 0번(대개 「계약 없음」).</summary>
        private static bool DriveOne(int seed, Func<BuildItem, bool> wants, out int highest)
        {
            var run = new RunSession(seed, 0f, 0f, FloorSession.DefaultAnteRatio,
                                     FloorSession.DefaultAnteEscalation, new TenFloorSource());
            highest = 0;
            int guard = 0;

            while (!run.IsComplete && !run.IsFailed && guard++ < 200)
            {
                FloorSession f = run.Current;
                if (f == null) break;
                if (f.Plan.Floor > highest) highest = f.Plan.Floor;

                if (f.Phase == FloorPhase.Boarding)
                {
                    // 제시된 것 중 원하는 것만 집는다. 인덱스는 집을 때마다 밀리므로
                    // 매번 앞에서 다시 훑는다 — 「0번을 반복해서 집는다」와 다르다.
                    bool tookSomething = true;
                    while (tookSomething)
                    {
                        tookSomething = false;
                        for (int i = 0; i < f.BuildOffers.Count; i++)
                        {
                            if (!wants(f.BuildOffers[i])) continue;
                            if (!run.TakeBuildOffer(i)) continue;
                            tookSomething = true;
                            break;
                        }
                    }
                    if (!run.FinishBoarding()) break;
                }

                if (f.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) break;

                int spinGuard = 0;
                while (f.Phase != FloorPhase.Resolved && spinGuard++ < 30)
                {
                    if (f.CanBank) { run.Bank(); break; }
                    if (f.SpinsRemaining <= 0) { run.ForceResolve(); break; }
                    run.Spin();
                }
                if (f.Phase != FloorPhase.Resolved) break;
            }
            return run.IsComplete;
        }

        private readonly record struct Score(double Clear, double MeanHighest);

        private static Score Measure(int seeds, Func<BuildItem, bool> wants)
        {
            int done = 0;
            long sumHighest = 0;
            for (int s = 0; s < seeds; s++)
            {
                if (DriveOne(20000 + s, wants, out int hi)) done++;
                sumHighest += hi;
            }
            return new Score(100.0 * done / seeds, (double)sumHighest / seeds);
        }

        public static void Run(int seeds, string outPath, bool pairs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 빌드 다양성 실측 — 품목별 한계 기여");
            sb.AppendLine();
            sb.AppendLine($"> 시드 20000~{20000 + seeds - 1} ({seeds}개) · 정책 고정" +
                          "(확정 가능해지면 확정 · 계약은 항상 0번)");
            sb.AppendLine("> 「그 품목 **하나만** 싣는 플레이어」의 완주율을 무적재와 비교한다.");
            sb.AppendLine();

            Score none = Measure(seeds, _ => false);
            Score all = Measure(seeds, _ => true);

            sb.AppendLine($"- 무적재 기준선: **{none.Clear:F2}%** (평균 최고층 {none.MeanHighest:F2})");
            sb.AppendLine($"- 전부 적재: **{all.Clear:F2}%** (평균 최고층 {all.MeanHighest:F2})");
            sb.AppendLine($"- 전부 적재의 이득: **{all.Clear - none.Clear:+0.00;-0.00}%p**");
            sb.AppendLine();

            sb.AppendLine("| 품목 | 종류 | 무게 | 단독 완주율 | 기여(%p) | 평균 최고층 | 효과 |");
            sb.AppendLine("|---|---|---:|---:|---:|---:|---|");

            var rows = new List<(string Label, double Delta, string Id)>();
            foreach (BuildItem item in BuildCatalog.All)
            {
                string id = item.Id;
                Score sc = Measure(seeds, b => b.Id == id);
                double delta = sc.Clear - none.Clear;
                rows.Add((item.Label, delta, id));
                sb.AppendLine($"| {item.Label} | {(item.Kind == BuildItemKind.Passenger ? "승객" : "부품")} " +
                              $"| {item.Weight:F0} | {sc.Clear:F2}% | **{delta:+0.00;-0.00}** " +
                              $"| {sc.MeanHighest:F2} | {item.EffectSummary()} |");
            }

            rows.Sort((x, y) => y.Delta.CompareTo(x.Delta));
            double best = rows[0].Delta, worst = rows[rows.Count - 1].Delta;

            sb.AppendLine();
            sb.AppendLine("## 판정");
            sb.AppendLine();
            sb.AppendLine($"- 최고 기여: **{rows[0].Label}** ({best:+0.00;-0.00}%p)");
            sb.AppendLine($"- 최저 기여: **{rows[rows.Count - 1].Label}** ({worst:+0.00;-0.00}%p)");
            sb.AppendLine($"- 기여 폭: **{best - worst:F2}%p**");
            sb.AppendLine();
            sb.AppendLine("| 순위 | 품목 | 기여 |");
            sb.AppendLine("|---:|---|---:|");
            for (int i = 0; i < rows.Count; i++)
                sb.AppendLine($"| {i + 1} | {rows[i].Label} | {rows[i].Delta:+0.00;-0.00}%p |");

            sb.AppendLine();
            sb.AppendLine("> **읽는 법.** 기여 폭이 표본 잡음보다 크면 품목 간 차이가 실재한다.");
            sb.AppendLine("> 잡음 크기는 `replicate` 모드로 따로 잰다 — 이 표만 보고 순위를 믿지 않는다.");
            sb.AppendLine("> 그리고 순위 자체는 **전략이 아니다.** 서로 다른 전략이 있다는 것은");
            sb.AppendLine("> 「1등이 있다」가 아니라 **「상황에 따라 1등이 바뀐다」**는 뜻이다.");

            if (pairs)
            {
                sb.AppendLine();
                sb.AppendLine("## 짝 — 상호작용이 있는가");
                sb.AppendLine();
                sb.AppendLine("단독 기여의 합보다 짝의 기여가 크면 **시너지**, 작으면 **중복**이다.");
                sb.AppendLine("시너지가 있는 짝이 하나도 없으면 「빌드」가 아니라 「목록」이다.");
                sb.AppendLine();
                var single = new Dictionary<string, double>();
                foreach (var r in rows) single[r.Id] = r.Delta;
                var items = BuildCatalog.All.ToArray();

                sb.AppendLine("| 짝 | 짝 기여 | 단독 합 | 차이 |");
                sb.AppendLine("|---|---:|---:|---:|");
                var synergy = new List<(string, double)>();
                for (int i = 0; i < items.Length; i++)
                {
                    for (int j = i + 1; j < items.Length; j++)
                    {
                        string a = items[i].Id, b = items[j].Id;
                        double pair = Measure(seeds, x => x.Id == a || x.Id == b).Clear - none.Clear;
                        double sum = single[a] + single[b];
                        synergy.Add(($"{items[i].Label} + {items[j].Label}", pair - sum));
                        sb.AppendLine($"| {items[i].Label} + {items[j].Label} | {pair:+0.00;-0.00} " +
                                      $"| {sum:+0.00;-0.00} | **{pair - sum:+0.00;-0.00}** |");
                    }
                }
                synergy.Sort((x, y) => y.Item2.CompareTo(x.Item2));
                sb.AppendLine();
                sb.AppendLine($"- 최대 시너지: **{synergy[0].Item1}** ({synergy[0].Item2:+0.00;-0.00}%p)");
                sb.AppendLine($"- 최대 중복: **{synergy[^1].Item1}** ({synergy[^1].Item2:+0.00;-0.00}%p)");
            }

            File.WriteAllText(outPath, sb.ToString());
            Console.Write(sb.ToString());
        }
    }
}
