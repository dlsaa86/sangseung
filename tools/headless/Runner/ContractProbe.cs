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
        /// <summary>
        /// 지정한 품목을 층 제시와 무관하게 태운다.
        ///
        /// ⚠ **측정할 층에서 태운다. 1층에서 태우면 사라진다.**
        /// `RunSession` 은 허용 중량을 넘으면 무거운 것부터 **투하한다**(jettison).
        /// 1층에서 부품을 강제로 실으면 그 자리에서 버려져, 7층에 도달했을 때
        /// 적재가 비어 있다 — 실제로 그렇게 재고 있었고 「전부 하차했다」로 찍혔다.
        /// </summary>
        private static bool Board(RunSession run, string[] ids)
        {
            if (ids == null || ids.Length == 0) return true;
            for (int i = 0; i < ids.Length; i++)
            {
                Ascend.Prototype.Build.BuildItem item = Ascend.Prototype.Build.BuildCatalog.ById(ids[i]);
                if (item == null || !run.Loadout.Add(item)) return false;
            }
            return true;
        }

        private static bool DriveOne(int seed, int targetFloor, int choice, string[] ids)
        {
            var run = new RunSession(seed, 0f, 0f, FloorSession.DefaultAnteRatio,
                                     FloorSession.DefaultAnteEscalation, new TenFloorSource());
            int guard = 0;
            bool boarded = ids == null || ids.Length == 0;

            while (!run.IsComplete && !run.IsFailed && guard++ < 200)
            {
                FloorSession f = run.Current;
                if (f == null) break;

                if (!boarded && f.Plan.Floor == targetFloor)
                {
                    if (!Board(run, ids)) return false;
                    boarded = true;
                }
                if (f.Phase == FloorPhase.Boarding && !run.FinishBoarding()) break;

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

        /// <summary>
        /// 🔴 **그 층에 실제로 무엇이 타고 있었는가.**
        ///
        /// 이 열이 없어서 하루를 태웠다. 「흡수체 빌드」라고 이름 붙인 줄이 7층에서
        /// **44%는 빈 적재**였다 — 흡수체 축의 핵심 품목 계측 기사는 `DestinationFloor = 5`
        /// 라 측정 층 전에 내리고, 남는 잔류 감쇠기는 제시에 안 뜨는 시드가 많았다.
        /// 빈 적재끼리 비교하면 「빌드에 따라 답이 바뀌는가」의 답은 언제나 「안 바뀐다」다.
        /// 빌드가 없었으니까.
        ///
        /// 지금은 <see cref="Board"/> 로 강제 탑승시키므로 이 열은 **하차로 사라진 것**을
        /// 잡는 감시자다. 측정이 무엇을 쟀는지 스스로 말하지 않으면, 0 이라는 결과가
        /// 「효과가 없다」인지 「입력이 없었다」인지 구분되지 않는다.
        /// </summary>
        private static string AboardAt(int floor, string[] ids)
        {
            if (ids == null || ids.Length == 0) return "—";
            var run = new RunSession(20000, 0f, 0f, FloorSession.DefaultAnteRatio,
                                     FloorSession.DefaultAnteEscalation, new TenFloorSource());
            int guard = 0;
            while (!run.IsComplete && !run.IsFailed && guard++ < 200)
            {
                FloorSession f = run.Current;
                if (f == null) break;
                if (f.Plan.Floor == floor && !Board(run, ids)) return "**탑승 실패**";
                if (f.Phase == FloorPhase.Boarding && !run.FinishBoarding()) break;
                if (f.Plan.Floor == floor)
                    return f.Loadout == null || f.Loadout.Count == 0
                        ? "**없음 — 전부 하차했다**" : f.Loadout.DescribeShort();
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
            return "**그 층에 닿지 못했다**";
        }

        private static double Measure(int seeds, int floor, int choice, string[] ids = null)
        {
            int done = 0;
            for (int s = 0; s < seeds; s++)
                if (DriveOne(20000 + s, floor, choice, ids)) done++;
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
            // 🔴 **제시에 맡기지 않는다** (2026-08-06).
            //
            // 예전 판본은 「그 품목이 제시에 뜨면 집는」 플레이어였다. 그래서 7층에서
            // 흡수체 빌드의 **44%가 빈 적재**였고(위 열이 그걸 찍는다), 빈 적재끼리
            // 비교하니 계약 계수를 0 → 0.6 으로 올려도 완주율이 20.17% 에 **못 박혀**
            // 있었다. 효과가 없었던 게 아니라 **입력이 없었다.**
            //
            // 지금은 층 시작에 직접 태운다. 「빌드를 태우면」이라는 열 이름이
            // 실제로 태운다는 뜻이 되어야 한다.
            //
            // ⚠ **지속되는 품목만 고른다.** 계측 기사(`DestinationFloor = 5`)처럼
            //    측정 층 전에 내리는 승객은 태워도 그 층에는 없다.
            var builds = new (string Name, string[] Ids)[]
            {
                ("적재 없음", Array.Empty<string>()),
                // 각 축에서 **한 품목씩**. 둘 이상 태우면 무게가 커져 과적 대가가 섞이고,
                // 그러면 「계약이 뒤집혔는가」가 아니라 「어느 빌드가 더 무거운가」를 재게 된다.
                // 과수확 변압기는 두 저항을 **모두** 겨냥해 양쪽 계약에 같이 붙으므로 뺐다 —
                // 대조군이 되려면 겨냥이 갈려 있어야 한다.
                //
                // 🔴 **대조군을 겨냥 개수로 맞춘다** (2026-08-07, PD-31).
                //
                // 직전 판본의 흡수체 빌드는 잔류 감쇠기였고 **흡수체 겨냥 효과가 1개**뿐이었다.
                // 계약 시너지는 겨냥 효과의 **개수**를 세므로(`SynergyMatchCap = 3`),
                // 겨냥 1개 vs 겨냥 3개(광신자)를 견주는 것은 계약을 잰 것이 아니라
                // **비대칭한 카탈로그를 잰 것**이다. PD-29 의 여백이 잡음 안이었던 이유다.
                //
                // 응결기는 광신자와 같은 모양이다 — 겨냥 3개, 무게 20 vs 16, 둘 다 지속.
                // 이제야 「같은 저울에 올린」 비교가 된다.
                ("흡수체 빌드", new[] { "PRT_ABSORBER_CONDENSER" }),  // 무게 20 · 흡수체 겨냥 3
                ("증식체 빌드", new[] { "PSG_ZEALOT" }),              // 무게 16 · 증식체 겨냥 3
                // 옛 대조군을 남긴다. 「바꿔서 좋아진 것」과 「원래 그랬던 것」이
                // 같은 표에 있어야 다음 세션이 되돌릴지 판단할 수 있다.
                ("흡수체 빌드(감쇠기)", new[] { "PRT_RESIDUAL_DAMPENER" }), // 무게 18 · 흡수체 겨냥 1
            };

            sb.AppendLine();
            sb.AppendLine("## 빌드를 태우면 답이 바뀌는가");
            sb.AppendLine();
            sb.AppendLine("계약이 **선택**이려면 빌드에 따라 1등이 달라져야 한다.");
            sb.AppendLine("어느 빌드로도 같은 것이 1등이면 격차가 아무리 작아도 그건 정답이다.");
            sb.AppendLine();
            sb.AppendLine("측정이 무엇을 쟀는지 스스로 말하게 한다 — **그 층에 실제로 타고 있던 것**을");
            sb.AppendLine("함께 찍는다. 이름만 「흡수체 빌드」이고 판은 비어 있으면 답은 언제나 안 바뀐다.");
            sb.AppendLine();
            sb.AppendLine("| 층 | 빌드 | 그 층에 타고 있던 것 | 인덱스별 완주율 | 1등 | 2등과의 격차 |");
            sb.AppendLine("|---:|---|---|---|---|---:|");

            int flipped = 0;
            double widestFlipMargin = 0d;
            int widestFlipFloor = 0;
            foreach (int floor in floors)
            {
                FloorPlan plan = source.For(floor);
                ResistanceContract[] choices = plan.ContractChoices;
                if (choices == null || choices.Length < 2) continue;

                string firstWinner = null;
                bool differs = false;
                // 이 층에서 **뒤집은 쪽**이 얼마나 앞섰는가. 뒤집혔다는 사실보다
                // 이 수가 중요하다 — 단일 셀 잡음(σ≈0.76%p)보다 작으면 「뒤집혔다」는
                // 다음 시드 블록에서 사라진다 (PD-29 종결 노트가 정확히 그 상태였다).
                double floorFlipMargin = 0d;

                foreach ((string name, string[] ids) in builds)
                {
                    var rates = new double[choices.Length];
                    int best = 0;
                    for (int i = 0; i < choices.Length; i++)
                    {
                        rates[i] = Measure(seeds, floor, i, ids);
                        if (rates[i] > rates[best]) best = i;
                    }

                    // 1등이 2등을 얼마나 앞섰는가.
                    double runnerUp = double.NegativeInfinity;
                    for (int i = 0; i < choices.Length; i++)
                        if (i != best && rates[i] > runnerUp) runnerUp = rates[i];
                    double margin = rates[best] - runnerUp;

                    var cells = new List<string>(choices.Length);
                    for (int i = 0; i < choices.Length; i++)
                        cells.Add($"{ShortLabel(choices[i])} {rates[i]:F2}%");

                    string winner = ShortLabel(choices[best]);
                    if (firstWinner == null) firstWinner = winner;
                    else if (winner != firstWinner)
                    {
                        differs = true;
                        if (margin > floorFlipMargin) floorFlipMargin = margin;
                    }

                    sb.AppendLine($"| {floor} | {name} | {AboardAt(floor, ids)} " +
                                  $"| {string.Join(" · ", cells)} | **{winner}** | {margin:+0.00;-0.00}%p |");
                }

                if (differs)
                {
                    flipped++;
                    if (floorFlipMargin > widestFlipMargin)
                    {
                        widestFlipMargin = floorFlipMargin;
                        widestFlipFloor = floor;
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"- 빌드에 따라 **1등이 뒤집힌 층: {flipped}개 / {floors.Length}개**");
            if (flipped > 0)
            {
                sb.AppendLine($"- 가장 넓게 뒤집힌 격차: **{widestFlipMargin:F2}%p** ({widestFlipFloor}층)");
                sb.AppendLine();
                sb.AppendLine(widestFlipMargin >= 1.5d
                    ? "✅ **잡음 밖에서 뒤집힌다.** 단일 셀 σ≈0.76%p 의 2배를 넘는다 — "
                      + "다음 시드 블록에서도 남을 격차다."
                    : "⚠ **뒤집혔지만 여백이 얇다.** 단일 셀 σ≈0.76%p 안이라 "
                      + "다음 시드 블록에서 사라질 수 있다. 「뒤집혔다」는 사실이고 "
                      + "「튼튼하게 뒤집혔다」는 아직 아니다.");
            }
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
