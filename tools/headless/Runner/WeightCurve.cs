using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Headless
{
    /// <summary>
    /// **대가 축이 어디서 무는가**를 직접 잰다 (`PD-28` 가설 검증).
    ///
    /// 세 감사(적재·빌드·계약)가 전부 「위험이 보상을 한 번도 못 넘는다」로 끝났다.
    /// 가설은 「대가 축은 선형이고 이득 축은 비선형이라 밀어붙이면 항상 이긴다」였다.
    /// 그 가설의 앞쪽 절반 — **대가 축이 선형인가** — 은 품목을 거치지 않고 잴 수 있다.
    /// `RunSession.AddWeight` 로 **효과가 전혀 없는 순수 무게**만 얹으면 되기 때문이다.
    ///
    /// 순수 무게는 품목의 이득을 하나도 안 주므로, 완주율 곡선은 **대가 축의 모양 그
    /// 자체**다. 곡선이 완만하면 대가는 선형이고, 어느 지점에서 급락하면 그 지점이
    /// 문턱이다. 문턱이 실제 적재 무게 범위(§실측 평균 0.500·최대 1.260 비율) 밖에
    /// 있으면 「대가는 있는데 닿지 않는다」가 확정된다.
    /// </summary>
    internal static class WeightCurve
    {
        public static void Run(int seeds, string outPath)
        {
            float[] extra = { 0f, 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 100f, 120f, 150f, 200f };

            var sb = new StringBuilder();
            sb.AppendLine("# 대가 축 실측 — 순수 무게만 얹었을 때의 완주율");
            sb.AppendLine();
            sb.AppendLine($"> 시드 20000~{20000 + seeds - 1} ({seeds}개) · 무적재 · 계약 항상 0번");
            sb.AppendLine("> **효과가 없는 순수 무게**를 런 시작에 얹는다. 이득이 0이므로");
            sb.AppendLine("> 이 곡선은 **대가 축의 모양 그 자체**다.");
            sb.AppendLine("> 기본 허용 중량은 100 (`FloorSession.AllowedWeight`), 과적 시 요구 전력 ×1.5.");
            sb.AppendLine();
            sb.AppendLine("| 추가 무게 | 허용 대비 | 완주율 | 직전 대비 | 과적 층 비율 |");
            sb.AppendLine("|---:|---:|---:|---:|---:|");

            double prev = double.NaN;
            var points = new List<(float W, double Clear)>();

            foreach (float w in extra)
            {
                int done = 0, floors = 0, over = 0;
                for (int s = 0; s < seeds; s++)
                {
                    var run = new RunSession(20000 + s, 0f, 0f, FloorSession.DefaultAnteRatio,
                                             FloorSession.DefaultAnteEscalation, new TenFloorSource());
                    if (w > 0f) run.SetCarriedWeight(w);

                    int guard = 0;
                    while (!run.IsComplete && !run.IsFailed && guard++ < 200)
                    {
                        FloorSession f = run.Current;
                        if (f == null) break;
                        if (f.Phase == FloorPhase.Boarding && !run.FinishBoarding()) break;
                        if (f.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) break;

                        floors++;
                        if (run.IsOverloaded) over++;

                        int spinGuard = 0;
                        while (f.Phase != FloorPhase.Resolved && spinGuard++ < 30)
                        {
                            if (f.CanBank) { run.Bank(); break; }
                            if (f.SpinsRemaining <= 0) { run.ForceResolve(); break; }
                            run.Spin();
                        }
                        if (f.Phase != FloorPhase.Resolved) break;
                    }
                    if (run.IsComplete) done++;
                }

                double clear = 100.0 * done / seeds;
                points.Add((w, clear));
                string delta = double.IsNaN(prev) ? "—" : $"{clear - prev:+0.00;-0.00}%p";
                sb.AppendLine($"| {w:F0} | {w / 100f:F2} | **{clear:F2}%** | {delta} " +
                              $"| {100.0 * over / Math.Max(1, floors):F2}% |");
                prev = clear;
            }

            // 문턱 탐지 — 직전 구간 대비 기울기가 갑자기 가팔라지는 지점.
            sb.AppendLine();
            sb.AppendLine("## 기울기 — 선형인가 계단인가");
            sb.AppendLine();
            sb.AppendLine("| 구간 | 무게당 완주율 변화 (%p / 무게10) |");
            sb.AppendLine("|---|---:|");
            for (int i = 1; i < points.Count; i++)
            {
                float dw = points[i].W - points[i - 1].W;
                double slope = (points[i].Clear - points[i - 1].Clear) / dw * 10.0;
                sb.AppendLine($"| {points[i - 1].W:F0} → {points[i].W:F0} | {slope:+0.00;-0.00} |");
            }

            sb.AppendLine();
            sb.AppendLine("> **읽는 법.** 기울기가 구간 내내 비슷하면 대가는 **선형**이고,");
            sb.AppendLine("> 「조금 더 실으면 조금 더 나빠진다」뿐이라 결정을 만들지 못한다.");
            sb.AppendLine("> 어느 구간에서 기울기가 급변하면 그 지점이 **문턱**이고, 그때는");
            sb.AppendLine("> 실제 적재가 그 문턱에 **닿는지**가 다음 질문이 된다.");

            File.WriteAllText(outPath, sb.ToString());
            Console.Write(sb.ToString());
        }
    }
}
