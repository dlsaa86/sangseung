using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Headless
{
    /// <summary>
    /// **계약이 선택인가**를 잰다 (노션 §3.4 「가장 강한 순간에도 자동 정답이 생기지 않도록」).
    ///
    /// ## 방법
    ///
    /// 계약 선택지가 있는 층마다, **그 층에서만** 고정 인덱스를 고르는 플레이어를 만든다.
    /// 나머지 층은 항상 0번이다. 그 층의 인덱스만 바꿔 완주율을 재면, 차이는 그 층의
    /// 계약 선택에서만 온다.
    ///
    /// ## 무엇을 보면 되나
    ///
    /// 실측(`docs/runtime/CONTRACT_DOMINANCE_AUDIT.md`)이 잡은 것은 **단조성**이었다 —
    /// 선택지가 있는 6개 층 **전부**에서 클리어율이 인덱스 순서대로 증가했다.
    /// 우연일 확률 `(1/2)³ × (1/6)³ ≈ 0.06%`. 즉 「계약 없음」이 항상 최악이고
    /// 「고를 수 있으면 항상 더 센 것」이 정답이었다.
    ///
    /// 그래서 이 보고서의 판정 줄은 평균 격차가 아니라 **「단조 증가한 층이 몇 개인가」**다.
    /// 격차가 커도 층마다 정답이 다르면 그건 선택이고, 격차가 작아도 언제나 같은 방향이면
    /// 그건 정답이다.
    ///
    /// ⚠ 4층은 예외로 읽는다. 커리큘럼이 「첫 계약」을 가르치는 층이라 계약이 유리한 것이
    /// **교습**이다. 문제는 그 교습이 6·7·8·9·10층에서 한 번도 뒤집히지 않는 것이었다.
    /// </summary>
    internal static class ContractProbe
    {
        private static bool DriveOne(int seed, int targetFloor, int choice,
                                     Func<Ascend.Prototype.Build.BuildItem, bool> wants)
        {
            var run = new RunSession(seed, 0f, 0f, FloorSession.DefaultAnteRatio,
                                     FloorSession.DefaultAnteEscalation, new TenFloorSource());
            int guard = 0;

            while (!run.IsComplete && !run.IsFailed && guard++ < 200)
            {
                FloorSession f = run.Current;
                if (f == null) break;

                if (f.Phase == FloorPhase.Boarding)
                {
                    if (wants != null)
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
                    }
                    if (!run.FinishBoarding()) break;
                }

                if (f.Phase == FloorPhase.ContractSelection)
                {
                    int choices = f.Plan.ContractChoices != null ? f.Plan.ContractChoices.Length : 0;
                    int index = f.Plan.Floor == targetFloor ? Math.Min(choice, choices - 1) : 0;
                    if (!run.SelectContract(Math.Max(0, index))) break;
                }

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

        private static double Measure(int seeds, int floor, int choice,
                                      Func<Ascend.Prototype.Build.BuildItem, bool> wants = null)
        {
            int done = 0;
            for (int s = 0; s < seeds; s++)
                if (DriveOne(20000 + s, floor, choice, wants)) done++;
            return 100.0 * done / seeds;
        }

        /// <summary>
        /// 🔴 **진짜 질문은 여기다.** 「어느 계약이 센가」가 아니라
        /// **「내 빌드에 따라 답이 바뀌는가」**.
        ///
        /// 노션 §4 는 각 층이 「현재 빌드에 가장 유리한 저항 계약은 무엇인가?」를
        /// 묻게 하라고 적는다. 적재가 없는 표는 이 질문에 **구조적으로 답할 수 없다** —
        /// 빌드가 없으면 「빌드에 유리한」이 정의되지 않기 때문이다.
        /// 그래서 서로 다른 빌드를 태우고 같은 층의 계약 순위를 다시 잰다.
        ///
        /// 순위가 빌드에 따라 뒤집히면 계약은 **선택**이고, 어느 빌드로도 같은 것이
        /// 1등이면 그건 **정답**이다. 이 구분이 없으면 「격차를 줄였다」가
        /// 「선택으로 만들었다」로 잘못 읽힌다 — 격차가 0이어도 답이 하나면 선택이 아니다.
        /// </summary>
        private static void AppendBuildConditioned(StringBuilder sb, int seeds,
            IFloorPlanSource source, int[] floors)
        {
            var builds = new (string Name, Func<Ascend.Prototype.Build.BuildItem, bool> Wants)[]
            {
                ("적재 없음", null),
                ("흡수체 빌드", b => b.Id == "PSG_SURVEYOR" || b.Id == "PRT_RESIDUAL_DAMPENER"),
                ("증식체 빌드", b => b.Id == "PSG_ZEALOT" || b.Id == "PRT_OVERHARVEST_TRANSFORMER"),
            };

            sb.AppendLine();
            sb.AppendLine("## 빌드를 태우면 답이 바뀌는가");
            sb.AppendLine();
            sb.AppendLine("계약이 **선택**이려면 빌드에 따라 1등이 달라져야 한다.");
            sb.AppendLine("어느 빌드로도 같은 것이 1등이면 격차가 아무리 작아도 그건 정답이다.");
            sb.AppendLine();
            sb.AppendLine("| 층 | 빌드 | 인덱스별 완주율 | 1등 |");
            sb.AppendLine("|---:|---|---|---|");

            int flipped = 0;
            foreach (int floor in floors)
            {
                FloorPlan plan = source.For(floor);
                ResistanceContract[] choices = plan.ContractChoices;
                if (choices == null || choices.Length < 2) continue;

                string firstWinner = null;
                bool differs = false;

                foreach ((string name, var wants) in builds)
                {
                    var rates = new double[choices.Length];
                    int best = 0;
                    for (int i = 0; i < choices.Length; i++)
                    {
                        rates[i] = Measure(seeds, floor, i, wants);
                        if (rates[i] > rates[best]) best = i;
                    }

                    var cells = new List<string>(choices.Length);
                    for (int i = 0; i < choices.Length; i++)
                        cells.Add($"{ShortLabel(choices[i])} {rates[i]:F2}%");

                    string winner = ShortLabel(choices[best]);
                    if (firstWinner == null) firstWinner = winner;
                    else if (winner != firstWinner) differs = true;

                    sb.AppendLine($"| {floor} | {name} | {string.Join(" · ", cells)} | **{winner}** |");
                }

                if (differs) flipped++;
            }

            sb.AppendLine();
            sb.AppendLine($"- 빌드에 따라 **1등이 뒤집힌 층: {flipped}개 / {floors.Length}개**");
            sb.AppendLine();
            sb.AppendLine(flipped > 0
                ? "그 층들에서는 계약이 저울이다 — 무엇을 태웠는가가 답을 바꾼다."
                : "🔴 **어느 빌드로도 1등이 같다 — 계약은 아직 정답이다.** "
                  + "격차를 줄이는 것으로는 이 줄이 바뀌지 않는다.");
        }

        public static void Run(int seeds, string outPath)
        {
            var source = new TenFloorSource();

            var sb = new StringBuilder();
            sb.AppendLine("# 계약 실측 — 계약은 선택인가, 정답인가");
            sb.AppendLine();
            sb.AppendLine($"> 시드 20000~{20000 + seeds - 1} ({seeds}개) · 적재 없음");
            sb.AppendLine("> **그 층에서만** 계약 인덱스를 바꾼다. 나머지 층은 항상 0번이므로");
            sb.AppendLine("> 차이는 전부 그 층의 선택에서 온다.");
            sb.AppendLine();
            sb.AppendLine("| 층 | 선택지 | 인덱스별 완주율 | 최대 격차 | 단조 증가? |");
            sb.AppendLine("|---:|---:|---|---:|---|");

            int monotone = 0;
            int floorsWithChoice = 0;
            var lines = new List<string>();

            for (int floor = source.FirstFloor; floor <= source.LastFloor; floor++)
            {
                FloorPlan plan;
                try { plan = source.For(floor); }
                catch (Exception) { continue; }

                ResistanceContract[] choices = plan.ContractChoices;
                if (choices == null || choices.Length < 2) continue;
                floorsWithChoice++;

                var rates = new double[choices.Length];
                for (int i = 0; i < choices.Length; i++) rates[i] = Measure(seeds, floor, i);

                double min = double.MaxValue, max = double.MinValue;
                bool increasing = true;
                for (int i = 0; i < rates.Length; i++)
                {
                    if (rates[i] < min) min = rates[i];
                    if (rates[i] > max) max = rates[i];
                    if (i > 0 && rates[i] <= rates[i - 1]) increasing = false;
                }
                if (increasing) monotone++;

                var cells = new List<string>(rates.Length);
                for (int i = 0; i < rates.Length; i++)
                    cells.Add($"{ShortLabel(choices[i])} {rates[i]:F2}%");

                lines.Add($"| {floor} | {choices.Length} | {string.Join(" · ", cells)} " +
                          $"| {max - min:F2}%p | {(increasing ? "**예**" : "아니오")} |");
            }

            foreach (string line in lines) sb.AppendLine(line);

            sb.AppendLine();
            sb.AppendLine("## 판정");
            sb.AppendLine();
            sb.AppendLine($"- 선택지가 있는 층: **{floorsWithChoice}개**");
            sb.AppendLine($"- 그중 인덱스 순서대로 **단조 증가**한 층: **{monotone}개**");
            sb.AppendLine();
            if (monotone >= floorsWithChoice)
            {
                sb.AppendLine("🔴 **전부 단조 증가한다 — 계약은 선택이 아니다.**");
                sb.AppendLine("「고를 수 있으면 항상 더 센 것」이 정답이고, 그건 저울이 아니라 계단이다.");
            }
            else
            {
                sb.AppendLine($"층 {floorsWithChoice - monotone}개에서 순서가 깨졌다 — " +
                              "그 층들에서는 더 센 계약이 항상 낫지는 않다.");
            }
            sb.AppendLine();
            sb.AppendLine("> **4층은 예외로 읽는다.** 「첫 계약」을 가르치는 층이라 계약이");
            sb.AppendLine("> 유리한 것이 교습이다. 문제는 그 교습이 뒷 층에서 한 번도 뒤집히지");
            sb.AppendLine("> 않는 것이었다 — 6·7·8·9·10층을 본다.");

            // 선택지가 셋인 층만 본다. 둘뿐인 층은 「없음 vs 계약」이라
            // 빌드에 따라 뒤집히기를 기대하는 자리가 아니다.
            AppendBuildConditioned(sb, seeds, source, new[] { 7, 8, 9 });

            File.WriteAllText(outPath, sb.ToString());
            Console.Write(sb.ToString());
        }

        private static string ShortLabel(in ResistanceContract contract)
        {
            if (contract.IsNone) return "없음";
            return contract.Label != null && contract.Label.Length > 0
                ? contract.Label.Replace(" 계약", string.Empty)
                : "계약";
        }
    }
}
