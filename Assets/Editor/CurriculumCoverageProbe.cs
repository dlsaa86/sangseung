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
        // 표본 수는 호출자가 정한다 (2026-08-05). 근거는 `BalanceSweep` 의 같은 절.
        // 방문률은 층마다 분모가 다르므로(앞 층에서 죽은 런은 빠진다) 뒤 층일수록
        // 표본이 얇다 — 10층 방문률이 25% 면 200 시드의 실효 표본은 50이고 ±7%p 다.
        public const int DefaultSeedCount = 200;
        public const int DefaultFirstSeed = 1000;

        /// <summary>정책당 표본 시드 수. 에디터 기본값은 <see cref="DefaultSeedCount"/>.</summary>
        public static int SeedCount = DefaultSeedCount;

        /// <summary>첫 시드. 표본을 늘려도 앞 구간이 유지되도록 시작점은 고정한다.</summary>
        public static int FirstSeed = DefaultFirstSeed;

        /// <summary>에디터 기본값으로 되돌린다.</summary>
        public static void ResetSampling()
        {
            SeedCount = DefaultSeedCount;
            FirstSeed = DefaultFirstSeed;
        }

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

            foreach (int k in LoadCounts)
                Report(sb, LoadPolicyName(k), (f, slot) => slot < k);

            return sb.ToString();
        }

        // ── 적재량 축을 열어 둔다 (2026-08-05) ─────────────────────────────────
        //
        // 기본값 `{0, 1, 2}` 는 예전 하드코딩과 **같은 출력**을 낸다. 축을 연 이유는
        // 실측 하나 때문이다 — 시드 20000 개로 다시 재니 완주율이 적재량에 대해
        // **단조 증가**했다(0개 22.7% · 1개 32.0% · 2개 40.8%). 시드 200 개에서는
        // 0개와 1개가 27.0% 대 28.5% 로 붙어 있어 「1개는 의미 없다」로 읽혔는데,
        // 그 1.5%p 는 잡음이었다.
        //
        // 단조 증가 자체가 문제는 아니다. **문제는 어디서 꺾이는지 모른다는 것**이다.
        // 적재는 무게 → 요구 전력·과적 위험으로 대가를 치르게 설계돼 있으므로
        // (`UP-SPACE-06`·`UP-RISK`), 어느 적재량에서 곡선이 내려오는지가 그 대가가
        // 실제로 작동하는지를 판정한다. 내려오는 지점이 없으면 「많이 실을수록 좋다」가
        // 지배 전략이고, 그건 밸런스 수치가 아니라 **설계 결함**이다.
        // 그 판정을 하려면 3·4·5개를 재야 하고, 재려면 이 배열이 열려 있어야 한다.
        public static readonly int[] DefaultLoadCounts = { 0, 1, 2 };

        /// <summary>층당 적재 개수 축. 에디터 메뉴는 <see cref="DefaultLoadCounts"/> 로 돈다.</summary>
        public static int[] LoadCounts = DefaultLoadCounts;

        private static string LoadPolicyName(int k) => k <= 0 ? "무적재" : $"층당 {k}개 적재";

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
                        // 🔴 `TakeBuildOffer(0)` 이었다 — 「후보 번호가 가장 작은 것」 (2026-08-07 수정).
                        //
                        // `BuildLoadPolicy` 의 클래스 주석이 바로 그 동작을 결함으로 지목하고
                        // 있었다 — 「무엇을 집을지는 후보 번호가 가장 작은 것으로 정했다.
                        // 그 결과 아홉 런 전부에서 칸이 거의 비었다」. 정책이 그것을 고치려고
                        // 만들어졌는데 **이 프로브가 그 정책을 안 불렀다.**
                        //
                        // 같은 날 `BalanceSweep` 에서 더 심한 형태를 찾았다 — 거기는 적재를
                        // 아예 안 했다(`docs/runtime/HEADLESS_TEST_GAP.md`). 측정 경로 두 곳이
                        // 모두 정본 규칙을 우회하고 있었고, 그래서 「하네스와 헤드리스가 같은
                        // 것을 쓴다」는 그 주석의 약속이 어느 쪽에서도 참이 아니었다.
                        //
                        // 적재 **개수** 축(`LoadCounts`)의 의미는 그대로다 — k 개까지 집되,
                        // 무엇을 집을지만 정책이 정한다.
                        int slot = 0;
                        while (f.BuildOffers.Count > 0 && boardingPolicy(f, slot))
                        {
                            int pick = Ascend.Prototype.Build.BuildLoadPolicy.PickIndex(
                                f.BuildOffers, f.Loadout);
                            if (pick < 0 || !run.TakeBuildOffer(pick)) break;
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
