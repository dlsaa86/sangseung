using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Ascend.Prototype.EditorTools;

namespace Ascend.Headless
{
    /// <summary>
    /// 헤드리스 밸런스 러너.
    ///
    /// 프로젝트의 에디터 툴(`BalanceSweep` · `CurriculumCoverageProbe`)을 **수정 없이**
    /// 그대로 부른다. 여기서 판정을 다시 구현하면 그 순간 두 갈래가 되고, 이 저장소가
    /// 반복해서 당한 실패가 정확히 그것이다 — 재는 도구가 다른 게임을 잰다.
    ///
    /// <code>
    ///   dotnet run -c Release -- sweep     30000              # 밸런스 스윕
    ///   dotnet run -c Release -- coverage  20000              # 커리큘럼 커버리지
    ///   dotnet run -c Release -- loadcurve  8000              # 적재량 0~6개 축
    ///   dotnet run -c Release -- overload   4000              # 적재의 대가(과적) 실측
    ///   dotnet run -c Release -- build      2000              # 품목별 한계 기여
    ///   dotnet run -c Release -- buildpairs 1000              # + 짝 시너지 (55쌍, 느리다)
    ///   dotnet run -c Release -- weightcurve 8000             # 대가 축이 어디서 무는가
    ///   dotnet run -c Release -- replicate   300 out/r.md 24  # 표본 잡음 측정
    /// </code>
    ///
    /// 산출은 기본적으로 `tools/headless/out/` 에 떨어진다. **`docs/runtime/` 을 직접
    /// 덮어쓰지 않는다** — 표본 수가 다른 보고서가 같은 이름으로 섞이면 어느 쪽이
    /// 무엇인지 판정할 수 없게 된다. 채택할 산출만 사람이 옮긴다.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            string mode = args.Length > 0 ? args[0] : "sweep";
            int n = args.Length > 1 && int.TryParse(args[1], out int parsed) ? parsed : 0;
            string outPath = args.Length > 2 && args[2].Length > 0
                ? args[2]
                : Path.Combine("out", $"{mode}{(n > 0 ? "_" + n : string.Empty)}.md");

            string dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sw = Stopwatch.StartNew();
            string report;

            try
            {
                switch (mode)
                {
                    // 옛 판정과의 대조군. 2026-08-07 이전의 스윕은 적재를 **하지 않았고**,
                    // `FUN_CRITERIA` 의 모든 대역이 그 상태에서 정해졌다. 그 값이
                    // 무엇이었는지 다시 뽑을 수 없으면 「대역이 왜 움직였나」를 판정할 수 없다.
                    case "sweepnoload":
                        BalanceSweep.BoardBuilds = false;
                        goto case "sweep";

                    case "sweep":
                        if (n > 0)
                        {
                            BalanceSweep.SeedCount = n;
                            // 보정 표본은 본 표본을 따라가되 기본값 밑으로는 내리지 않는다.
                            BalanceSweep.CalibrationSeeds =
                                Math.Max(BalanceSweep.DefaultCalibrationSeeds, n / 4);
                        }
                        report = BalanceSweep.Measure();
                        break;

                    case "coverage":
                        if (n > 0) CurriculumCoverageProbe.SeedCount = n;
                        report = CurriculumCoverageProbe.Measure();
                        break;

                    case "loadcurve":
                        // 적재 곡선이 **어디서 꺾이는가**를 본다. 꺾이는 지점이 없으면
                        // 「전부 싣는다」가 지배 전략이라는 뜻이다.
                        if (n > 0) CurriculumCoverageProbe.SeedCount = n;
                        CurriculumCoverageProbe.LoadCounts = new[] { 0, 1, 2, 3, 4, 5, 6 };
                        report = CurriculumCoverageProbe.Measure();
                        break;

                    case "overload":
                        OverloadProbe.Run(n > 0 ? n : 2000, outPath);
                        return Finish(sw, mode, n, outPath);

                    case "build":
                    case "buildpairs":
                        BuildProbe.Run(n > 0 ? n : 2000, outPath, mode == "buildpairs");
                        return Finish(sw, mode, n, outPath);

                    case "strategy":
                        // 「품목 하나의 값」이 아니라 「그 방향으로 간 플레이어가
                        // 완주하는가」를 묻는다. 조건부 효과는 단독 측정에서 정의상
                        // 발동하지 않으므로 이 표가 없으면 조건부 설계를 판정할 수 없다.
                        StrategyProbe.Run(n > 0 ? n : 3000, outPath);
                        return Finish(sw, mode, n, outPath);

                    case "contracts":
                        // 「계약이 선택인가」. 평균 격차가 아니라 **단조성**을 본다 —
                        // 격차가 커도 층마다 정답이 다르면 선택이고, 작아도 언제나
                        // 같은 방향이면 정답이다.
                        ContractProbe.Run(n > 0 ? n : 3000, outPath);
                        return Finish(sw, mode, n, outPath);

                    case "weightcurve":
                        WeightCurve.Run(n > 0 ? n : 4000, outPath);
                        return Finish(sw, mode, n, outPath);

                    case "replicate":
                        Replicate.Run(n > 0 ? n : BalanceSweep.DefaultSeedCount,
                                      args.Length > 3 && int.TryParse(args[3], out int b) ? b : 20,
                                      outPath);
                        return Finish(sw, mode, n, outPath);

                    case "tests":
                        // 프로젝트 **자신의** 검사를 유니티 없이 돌린다. 판정을 여기서
                        // 다시 구현하지 않는다 — `RunTests.RunAll()` 등을 그대로 부른다.
                        //
                        // 왜 필요한가: `CLAUDE.md` §7 이 「코드 변경 뒤 자체 검증을 돌리고
                        // 커밋한다」를 요구하는데, 그 유일한 경로가 에디터 메뉴였다.
                        // 에디터가 없거나 컴파일이 깨진 상태에서는 **검사를 돌릴 방법이
                        // 아예 없었고**, 그때가 바로 검사가 가장 필요한 순간이다.
                        return TestRunner.Run();

                    default:
                        Console.Error.WriteLine($"unknown mode: {mode}");
                        Console.Error.WriteLine("modes: sweep | coverage | loadcurve | overload | build | buildpairs | weightcurve | strategy | contracts | replicate | tests");
                        return 2;
                }
            }
            finally
            {
                // 표본을 키운 채로 두지 않는다. 다음 호출이 조용히 다른 표본으로 돌면
                // 보고서 두 개가 같은 이름으로 비교 불가능해진다.
                BalanceSweep.ResetSampling();
                CurriculumCoverageProbe.ResetSampling();
            }

            File.WriteAllText(outPath, report);
            return Finish(sw, mode, n, outPath);
        }

        private static int Finish(Stopwatch sw, string mode, int n, string outPath)
        {
            sw.Stop();
            Console.WriteLine($"[{mode}{(n > 0 ? " n=" + n : string.Empty)}] " +
                              $"{sw.ElapsedMilliseconds} ms → {outPath}");
            // 대역이 조용히 삼킨 것이 없는지 함께 찍는다. 0 이 아니면 산출을 인용하기 전에 본다.
            Console.WriteLine($"shim: warnings={UnityEngine.Debug.WarningCount} " +
                              $"errors={UnityEngine.Debug.ErrorCount}");
            return UnityEngine.Debug.ErrorCount > 0 ? 1 : 0;
        }
    }
}
