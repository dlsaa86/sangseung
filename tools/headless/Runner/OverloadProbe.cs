using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;
namespace Ascend.Headless
{
    /// <summary>
    /// 「적재가 대가를 치르는가」를 직접 잰다. 커버리지 프로브는 완주율만 보므로
    /// **과적이 한 번이라도 발생하는지**를 답하지 못한다. 여기서는 매 층 최대한 싣고
    /// 무게·허용치·과적 여부를 층마다 기록한다.
    /// </summary>
    internal static class OverloadProbe
    {
        public static void Run(int seeds, string outPath)
        {
            var peakRatio = new List<double>();
            int overloadedFloors = 0, totalFloors = 0, runsWithOverload = 0;
            var itemsPerFloor = new List<int>();
            var weightAt = new double[11];
            var capAt = new double[11];
            var nAt = new int[11];
            int completed = 0;

            for (int s = 0; s < seeds; s++)
            {
                var run = new RunSession(20000 + s, 0f, 0f,
                    FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation, new TenFloorSource());
                bool sawOverload = false;
                int guard = 0;
                while (!run.IsComplete && !run.IsFailed && guard++ < 200)
                {
                    FloorSession f = run.Current;
                    if (f == null) break;

                    if (f.Phase == FloorPhase.Boarding)
                    {
                        int taken = 0;
                        while (f.BuildOffers.Count > 0 && run.TakeBuildOffer(0)) taken++;
                        itemsPerFloor.Add(taken);
                        if (!run.FinishBoarding()) break;
                    }
                    if (f.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) break;

                    int fl = f.Plan.Floor;
                    if (fl >= 1 && fl <= 10)
                    {
                        weightAt[fl] += run.CarriedWeight; capAt[fl] += run.WeightCapacity; nAt[fl]++;
                    }
                    totalFloors++;
                    double cap = run.WeightCapacity;
                    if (cap > 0) peakRatio.Add(run.CarriedWeight / cap);
                    if (run.IsOverloaded) { overloadedFloors++; sawOverload = true; }

                    // 스핀을 소진하거나 확정 가능해지면 확정 — 적재 축만 보려고 정책을 고정한다.
                    int spinGuard = 0;
                    while (f.Phase != FloorPhase.Resolved && spinGuard++ < 20)
                    {
                        if (f.CanBank) { run.Bank(); break; }
                        if (f.SpinsRemaining <= 0) { run.ForceResolve(); break; }
                        run.Spin();
                    }
                    if (f.Phase != FloorPhase.Resolved) break;
                }
                if (sawOverload) runsWithOverload++;
                if (run.IsComplete) completed++;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# 적재 대가 실측 — 매 층 최대 적재 정책");
            sb.AppendLine();
            sb.AppendLine($"- 시드 {seeds}개 · 완주 {completed} ({100.0 * completed / seeds:F1}%)");
            sb.AppendLine($"- 관측 층 {totalFloors} · **과적 발생 층 {overloadedFloors} ({100.0 * overloadedFloors / Math.Max(1, totalFloors):F2}%)**");
            sb.AppendLine($"- **과적을 한 번이라도 겪은 런 {runsWithOverload} / {seeds} ({100.0 * runsWithOverload / seeds:F2}%)**");
            sb.AppendLine($"- 층당 실제로 실린 개수: 평균 {itemsPerFloor.DefaultIfEmpty(0).Average():F2} · 최대 {itemsPerFloor.DefaultIfEmpty(0).Max()}");
            sb.AppendLine($"- 무게/허용치 비율: 평균 {peakRatio.DefaultIfEmpty(0).Average():F3} · **최대 {peakRatio.DefaultIfEmpty(0).Max():F3}** (1.0 을 넘어야 과적)");
            sb.AppendLine();
            sb.AppendLine("| 층 | 평균 적재 무게 | 평균 허용 중량 | 비율 |");
            sb.AppendLine("|---|---:|---:|---:|");
            for (int f = 1; f <= 10; f++)
            {
                if (nAt[f] == 0) continue;
                double w = weightAt[f] / nAt[f], c = capAt[f] / nAt[f];
                sb.AppendLine($"| {f} | {w:F1} | {c:F1} | {(c > 0 ? w / c : 0):F3} |");
            }
            File.WriteAllText(outPath, sb.ToString());
            Console.Write(sb.ToString());
        }
    }
}
