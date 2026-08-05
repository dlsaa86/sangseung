using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascend.Prototype.Build;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Headless
{
    /// <summary>
    /// **서로 다른 빌드 전략이 실제로 존재하는가**를 잰다 (`MASTER_PRD` §2.3 ·
    /// 노션 §6.3 「내부 빌드 방향」).
    ///
    /// ## 왜 `build` 모드로는 이 질문에 답할 수 없나
    ///
    /// `BuildProbe` 는 「그 품목 **하나만** 싣는 플레이어」를 잰다. 그 측정에서는
    /// 조건부 효과가 **정의상 한 번도 발동하지 않는다** — 조건을 켜 줄 다른 품목이
    /// 없기 때문이다. 그래서 단독 표는 조건부 설계를 항상 과소평가하고,
    /// 「1등이 몇 %p 앞서는가」만 말한다.
    ///
    /// 그런데 설계 질문은 그게 아니다. **「1등을 못 뽑은 런도 갈 길이 있는가」**이다.
    /// 노션 §3.4 가 요구하는 것은 균등한 순위표가 아니라 **자동 정답의 부재**이고,
    /// 자동 정답은 「그것 없이는 못 간다」일 때 생긴다.
    ///
    /// ## 방법
    ///
    /// 축별 원형 빌드를 만들어(그 축의 품목만 집는 플레이어) 완주율을 잰다.
    /// 마지막 두 줄이 판정의 핵심이다 — **1등 품목을 금지한 플레이어**와
    /// **전부 집는 플레이어**를 나란히 놓는다. 앞이 뒤와 크게 벌어지지 않으면
    /// 「그것 없이도 갈 길이 있다」가 데이터로 성립한다.
    /// </summary>
    internal static class StrategyProbe
    {
        private sealed class Strategy
        {
            public string Name;
            public string Note;
            public Func<BuildItem, bool> Wants;
        }

        private static bool DriveOne(int seed, Func<BuildItem, bool> wants,
                                     out int highest, out float weight)
        {
            var run = new RunSession(seed, 0f, 0f, FloorSession.DefaultAnteRatio,
                                     FloorSession.DefaultAnteEscalation, new TenFloorSource());
            highest = 0;
            weight = 0f;
            int guard = 0;

            while (!run.IsComplete && !run.IsFailed && guard++ < 200)
            {
                FloorSession f = run.Current;
                if (f == null) break;
                if (f.Plan.Floor > highest) highest = f.Plan.Floor;

                if (f.Phase == FloorPhase.Boarding)
                {
                    bool took = true;
                    while (took)
                    {
                        took = false;
                        for (int i = 0; i < f.BuildOffers.Count; i++)
                        {
                            if (!wants(f.BuildOffers[i])) continue;
                            if (!run.TakeBuildOffer(i)) continue;
                            took = true;
                            break;
                        }
                    }
                    if (!run.FinishBoarding()) break;
                }

                if (f.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) break;

                if (f.CarriedWeight > weight) weight = f.CarriedWeight;

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

        private static (double clear, double meanHighest, double meanWeight)
            Measure(int seeds, Func<BuildItem, bool> wants)
        {
            int done = 0;
            long sumHighest = 0;
            double sumWeight = 0;
            for (int s = 0; s < seeds; s++)
            {
                if (DriveOne(20000 + s, wants, out int hi, out float w)) done++;
                sumHighest += hi;
                sumWeight += w;
            }
            return (100.0 * done / seeds, (double)sumHighest / seeds, sumWeight / seeds);
        }

        /// <summary>단독 기여 1등. 「이것 없이 갈 수 있는가」의 기준점이다.</summary>
        private const string TopItemId = "PRT_DIAGONAL_BINDER";

        public static void Run(int seeds, string outPath)
        {
            var strategies = new List<Strategy>
            {
                new Strategy { Name = "무적재", Note = "기준선 — 아무것도 안 싣는다",
                               Wants = _ => false },
                new Strategy { Name = "안정 축", Note = "기본 정화를 반복한다",
                               Wants = b => b.Axis == BuildAxis.Stability },
                new Strategy { Name = "패턴 축", Note = "직선과 모양의 값을 올린다",
                               Wants = b => b.Axis == BuildAxis.Pattern },
                new Strategy { Name = "연쇄 축", Note = "연결과 재충전을 잇는다",
                               Wants = b => b.Axis == BuildAxis.Cascade },
                new Strategy { Name = "잔류 축", Note = "남긴 저항을 재료로 쓴다",
                               Wants = b => b.Axis == BuildAxis.Residual },
                new Strategy { Name = "승객만", Note = "가볍게 가고 요금을 받는다",
                               Wants = b => b.Kind == BuildItemKind.Passenger },
                new Strategy { Name = "부품만", Note = "무겁게 가고 규칙을 바꾼다",
                               Wants = b => b.Kind == BuildItemKind.Part },
                new Strategy { Name = "가벼운 것만", Note = "무게 12 이하만 집는다",
                               Wants = b => b.Weight <= 12f },
                new Strategy { Name = "1등 금지", Note = "사선 결속기만 빼고 전부",
                               Wants = b => b.Id != TopItemId },
                new Strategy { Name = "전부", Note = "제시되면 무조건 집는다",
                               Wants = _ => true },
            };

            var sb = new StringBuilder();
            sb.AppendLine("# 빌드 전략 실측 — 서로 다른 길이 있는가");
            sb.AppendLine();
            sb.AppendLine($"> 시드 20000~{20000 + seeds - 1} ({seeds}개) · 계약은 항상 0번 · " +
                          "확정 가능해지면 즉시 확정");
            sb.AppendLine("> `build` 모드와 다른 질문을 묻는다 — 저쪽은 「품목 하나의 값」,");
            sb.AppendLine("> 여기는 **「그 방향으로 간 플레이어가 완주하는가」**다.");
            sb.AppendLine("> 조건부 효과는 단독 측정에서 정의상 발동하지 않으므로,");
            sb.AppendLine("> 조건부 설계를 판정하려면 이 표를 봐야 한다.");
            sb.AppendLine();
            sb.AppendLine("| 전략 | 완주율 | 평균 최고층 | 평균 최대 무게 | 설명 |");
            sb.AppendLine("|---|---:|---:|---:|---|");

            var results = new List<(string Name, double Clear)>();
            foreach (Strategy st in strategies)
            {
                var (clear, hi, w) = Measure(seeds, st.Wants);
                results.Add((st.Name, clear));
                sb.AppendLine($"| {st.Name} | **{clear:F2}%** | {hi:F2} | {w:F1} | {st.Note} |");
            }

            double none = results[0].Clear;
            double banned = results[results.Count - 2].Clear;
            double all = results[results.Count - 1].Clear;

            // 축 4종만 따로 본다. 「승객만/부품만」은 축이 아니라 분류라 섞으면 판정이 흐려진다.
            double axisBest = double.MinValue, axisWorst = double.MaxValue;
            string bestName = null, worstName = null;
            for (int i = 1; i <= 4; i++)
            {
                if (results[i].Clear > axisBest) { axisBest = results[i].Clear; bestName = results[i].Name; }
                if (results[i].Clear < axisWorst) { axisWorst = results[i].Clear; worstName = results[i].Name; }
            }

            sb.AppendLine();
            sb.AppendLine("## 판정");
            sb.AppendLine();
            sb.AppendLine($"- 축 최고: **{bestName}** {axisBest:F2}% · 축 최저: **{worstName}** {axisWorst:F2}%");
            sb.AppendLine($"- 축 간 폭: **{axisBest - axisWorst:F2}%p**");
            sb.AppendLine($"- 「1등 금지」 {banned:F2}% vs 「전부」 {all:F2}% → 차이 **{all - banned:F2}%p**");
            sb.AppendLine();
            sb.AppendLine("**읽는 법.** 두 줄만 보면 된다.");
            sb.AppendLine();
            sb.AppendLine("1. **축 간 폭**이 크면 「방향이 여럿」이 아니라 「정답 하나와 함정 셋」이다.");
            sb.AppendLine("2. **1등 금지 vs 전부**의 차이가 크면, 그 한 품목이 사실상 필수다 —");
            sb.AppendLine("   즉 그것을 제시받았는가가 런의 성패를 정하고 그건 선택이 아니라 추첨이다.");
            sb.AppendLine("   노션 §3.4 「가장 강한 순간에도 자동 정답이 생기지 않도록」이 이 줄을 본다.");
            sb.AppendLine();
            sb.AppendLine("> 잡음 크기는 `replicate` 로 따로 잰다. 이 표 하나로 순위를 믿지 않는다.");

            File.WriteAllText(outPath, sb.ToString());
            Console.Write(sb.ToString());
        }
    }
}
