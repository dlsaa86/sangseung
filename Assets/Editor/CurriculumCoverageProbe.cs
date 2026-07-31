using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 커리큘럼이 **실제로 가르쳐지는가**를 시드 다수로 잰다.
    ///
    /// 왜 완주율만으로 부족한가: `PowerBand.MultiFloor`(요구의 170% 이상)가 추가 층을 주므로
    /// 층을 건너뛴 채 10층에 도달할 수 있다. 실측된 방문 순서가 [1,2,3,5,6,8,10] 이면
    /// 4·7·9층은 **한 번도 열리지 않는다.** 완주율 100%와 "연결·캐스케이드를 아무도 못 봤다"가
    /// 동시에 성립한다. `RunSession.ClampAscent`(D-20260731-03)는 최종 층과 적재 층만
    /// 보호하므로 나머지 교습 층은 구조적으로 건너뛸 수 있다.
    ///
    /// 그래서 층별 방문률을 따로 낸다. 이 수치가 요구 전력 곡선을 고칠 근거다 —
    /// 방문률이 낮은 층은 그 **앞 층**의 요구 전력이 너무 낮아 잉여가 많다는 뜻이다.
    ///
    /// 결과는 `Logs/curriculum_coverage.txt`. 밸런스를 바꾼 뒤 같은 파일을 다시 만들어
    /// 비교한다.
    /// </summary>
    public static class CurriculumCoverageProbe
    {
        public const string ReportPath = "Logs/curriculum_coverage.txt";
        private const int SeedCount = 200;
        private const int FirstSeed = 1000;

        [MenuItem("Ascend/Probe Curriculum Coverage")]
        public static void Run()
        {
            string report = Measure();
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ReportPath), report);
            Debug.Log(report);
        }

        public static string Measure()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 커리큘럼 커버리지 ===");
            sb.AppendLine($"시드 {FirstSeed}~{FirstSeed + SeedCount - 1} ({SeedCount}개) / 정책별");
            sb.AppendLine();

            Report(sb, "무적재", (f, slot) => false);
            Report(sb, "층당 1개 적재", (f, slot) => slot < 1);
            Report(sb, "층당 2개 적재", (f, slot) => slot < 2);

            return sb.ToString();
        }

        private static void Report(StringBuilder sb, string policyName, Func<FloorSession, int, bool> policy)
        {
            var visits = new int[11];
            var contractSeen = new int[11];
            int completed = 0, failed = 0, reachedTen = 0;
            var bandCounts = new Dictionary<PowerBand, int>();
            long totalFloorsPlayed = 0;

            for (int i = 0; i < SeedCount; i++)
            {
                int seed = FirstSeed + i;
                RunOutcome o = Drive(seed, policy);
                foreach (int floor in o.Visited)
                    if (floor >= 1 && floor <= 10) visits[floor]++;
                foreach (int floor in o.ContractFloors)
                    if (floor >= 1 && floor <= 10) contractSeen[floor]++;
                totalFloorsPlayed += o.Visited.Count;
                if (o.Completed) completed++;
                if (o.Failed) failed++;
                if (o.Visited.Contains(10)) reachedTen++;
                foreach (var kv in o.Bands)
                {
                    bandCounts.TryGetValue(kv.Key, out int n);
                    bandCounts[kv.Key] = n + kv.Value;
                }
            }

            sb.AppendLine($"── {policyName} ──");
            sb.AppendLine($"  완주 {completed}/{SeedCount} ({100.0 * completed / SeedCount:F1}%)  " +
                          $"실패 {failed}  10층 도달 {reachedTen} ({100.0 * reachedTen / SeedCount:F1}%)");
            sb.AppendLine($"  런당 평균 방문 층 수 {(double)totalFloorsPlayed / SeedCount:F2} / 10");
            sb.AppendLine("  층별 방문률:");
            for (int f = 1; f <= 10; f++)
            {
                double pct = 100.0 * visits[f] / SeedCount;
                string bar = new string('#', Math.Max(0, (int)Math.Round(pct / 5.0)));
                string teach = Teaches(f);
                string mark = pct < 50.0 ? "  <<< 절반도 못 본다" : string.Empty;
                sb.AppendLine($"    {f,2}층 {pct,5:F1}%  {bar,-20} {teach}{mark}");
            }
            sb.Append("  전력 구간 분포: ");
            foreach (PowerBand band in Enum.GetValues(typeof(PowerBand)))
            {
                bandCounts.TryGetValue(band, out int n);
                if (n > 0) sb.Append($"{band.DisplayName()} {n}  ");
            }
            sb.AppendLine();
            sb.AppendLine();
        }

        private static string Teaches(int floor)
        {
            FloorPlan p = PrototypeCurriculum.For(floor);
            return p.TeachesRule ?? string.Empty;
        }

        private struct RunOutcome
        {
            public List<int> Visited;
            public List<int> ContractFloors;
            public Dictionary<PowerBand, int> Bands;
            public bool Completed;
            public bool Failed;
        }

        /// <summary>
        /// `BuildTests.Drive` 와 같은 구동 방식이다. 복사한 이유: 테스트 쪽은 private 이고,
        /// 측정 도구가 테스트 내부에 의존하면 테스트를 고칠 때마다 측정이 조용히 달라진다.
        /// 계약은 항상 0번(대개 "계약 없음")을 고른다 — 계약 선택의 영향이 아니라
        /// **층 도달**을 재는 것이 목적이므로 정책을 하나로 고정한다.
        /// </summary>
        private static RunOutcome Drive(int seed, Func<FloorSession, int, bool> boardingPolicy)
        {
            var run = new RunSession(seed, 0f, 0f, FloorSession.DefaultAnteRatio,
                                     FloorSession.DefaultAnteEscalation, new TenFloorSource());
            var visited = new List<int>();
            var contractFloors = new List<int>();
            var bands = new Dictionary<PowerBand, int>();
            int guard = 0;

            while (!run.IsComplete && !run.IsFailed && guard++ < 200)
            {
                FloorSession f = run.Current;
                if (f == null) break;
                if (visited.Count == 0 || visited[visited.Count - 1] != f.Plan.Floor)
                    visited.Add(f.Plan.Floor);

                if (f.Phase == FloorPhase.Boarding)
                {
                    if (boardingPolicy != null)
                    {
                        int slot = 0;
                        while (f.BuildOffers.Count > 0 && boardingPolicy(f, slot))
                        {
                            if (!run.TakeBuildOffer(0)) break;
                            slot++;
                        }
                    }
                    if (!run.FinishBoarding()) break;
                }

                if (f.Phase == FloorPhase.ContractSelection)
                {
                    contractFloors.Add(f.Plan.Floor);
                    if (!run.SelectContract(0)) break;
                }

                int spins = 0;
                while (f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0)
                {
                    run.Spin();
                    if (++spins > 30) break;
                }

                PowerBand band = PowerThresholds.Default.BandFor(f.Power, f.RequiredPower);
                bands.TryGetValue(band, out int bn);
                bands[band] = bn + 1;

                if (f.CanBank) run.Bank();
                else if (f.SpinsRemaining == 0) run.ForceResolve();
                else break;
            }

            return new RunOutcome
            {
                Visited = visited,
                ContractFloors = contractFloors,
                Bands = bands,
                Completed = run.IsComplete && !run.IsFailed,
                Failed = run.IsFailed,
            };
        }
    }
}
