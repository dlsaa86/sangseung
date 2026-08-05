using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ascend.Prototype.EditorTools;
namespace Ascend.Headless
{
    /// <summary>
    /// 같은 스윕을 **서로 겹치지 않는 시드 블록**으로 여러 번 돌려, 각 지표가 표본 잡음만으로
    /// 얼마나 흔들리는지 잰다. 「N=300 의 판정을 믿어도 되는가」에 답하는 유일한 방법이다 —
    /// 한 번 돌린 값의 소수점 자리수는 정밀도에 대해 아무것도 말해 주지 않는다.
    /// </summary>
    internal static class Replicate
    {
        private sealed record Metric(string Key, Regex Row, int Group);

        // 보고서에서 뽑을 지표. 행 라벨로 잡는다 — 표 위치가 바뀌어도 따라간다.
        private static readonly Metric[] Metrics =
        {
            new("초반 1–3 클리어율",  new Regex(@"^\| 초반 1–3 \| 97–100% \|[^|]*\|[^|]*\| \*\*([\d.]+)%"), 1),
            new("중반 4–7 클리어율",  new Regex(@"^\| 중반 4–7 \| 90–96% \|[^|]*\|[^|]*\| \*\*([\d.]+)%"), 1),
            new("후반 8–10 클리어율", new Regex(@"^\| 후반 8–10 \| 82–92% \|[^|]*\|[^|]*\| \*\*([\d.]+)%"), 1),
            new("10층 방문률",        new Regex(@"^\| 10 \| ([\d.]+)% \| [\d.]+% \| 새 규칙"), 1),
            new("10층 클리어율",      new Regex(@"^\| 10 \| [\d.]+% \| ([\d.]+)% \| 새 규칙"), 1),
            new("선택 발생 층 비율",  new Regex(@"^\| 선택이 \*\*발생\*\*하는 층 비율 \|[^|]*\| ([\d.]+)%"), 1),
            new("과수확 선택률",      new Regex(@"^\| 기대값 정책의 과수확 선택률 \|[^|]*\| ([\d.]+)%"), 1),
            new("EV 비율",            new Regex(@"^\| 안전 확정 EV ÷ 추가 스핀 EV \|[^|]*\| \*\*([\d.]+)\*\*"), 1),
            new("3연쇄 이상 비율",    new Regex(@"^\| 3연쇄 이상 스핀 비율 \|[^|]*\|[^|]*\| \*\*([\d.]+)%"), 1),
            new("정화 발생 비율",     new Regex(@"^\| 정화 발생 스핀 비율 \|[^|]*\|[^|]*\| \*\*([\d.]+)%"), 1),
            new("관측 최대 연쇄",     new Regex(@"^\| 관측 최대 연쇄 \|[^|]*\| \d+ \| (\d+)"), 1),
        };

        public static void Run(int blockSize, int blocks, string outPath)
        {
            var samples = new Dictionary<string, List<double>>();
            foreach (var m in Metrics) samples[m.Key] = new List<double>();

            for (int b = 0; b < blocks; b++)
            {
                BalanceSweep.SeedCount = blockSize;
                BalanceSweep.CalibrationSeeds = BalanceSweep.DefaultCalibrationSeeds;
                // 블록끼리 시드가 겹치지 않게 띄운다. 보정 패스도 같은 블록 안에서 돈다.
                BalanceSweep.FirstSeed = BalanceSweep.DefaultFirstSeed + b * blockSize * 4;
                string rep = BalanceSweep.Measure();
                foreach (string line in rep.Split('\n'))
                {
                    string t = line.TrimEnd('\r');
                    foreach (var m in Metrics)
                    {
                        Match hit = m.Row.Match(t);
                        if (hit.Success)
                            samples[m.Key].Add(double.Parse(hit.Groups[m.Group].Value, CultureInfo.InvariantCulture));
                    }
                }
                Console.Write($"\rblock {b + 1}/{blocks}   ");
            }
            Console.WriteLine();
            BalanceSweep.ResetSampling();

            var sb = new StringBuilder();
            sb.AppendLine($"# 표본 잡음 측정 — 블록 {blocks}개 × 시드 {blockSize}개 (겹치지 않음)");
            sb.AppendLine();
            sb.AppendLine("| 지표 | 평균 | 최소 | 최대 | 폭 | 표준편차 | 95% 구간 (±1.96σ) |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---|");
            foreach (var m in Metrics)
            {
                var v = samples[m.Key];
                if (v.Count == 0) { sb.AppendLine($"| {m.Key} | (행 못 찾음) | | | | | |"); continue; }
                double mean = v.Average();
                double sd = v.Count < 2 ? 0 : Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / (v.Count - 1));
                sb.AppendLine($"| {m.Key} | {mean:F2} | {v.Min():F2} | {v.Max():F2} | " +
                              $"{v.Max() - v.Min():F2} | {sd:F2} | {mean - 1.96 * sd:F2} ~ {mean + 1.96 * sd:F2} |");
            }
            File.WriteAllText(outPath, sb.ToString());
            Console.WriteLine("→ " + outPath);
            Console.Write(sb.ToString());
        }
    }
}
