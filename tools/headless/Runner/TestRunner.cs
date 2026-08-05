using System;
using System.Collections.Generic;

namespace Ascend.Headless
{
    /// <summary>
    /// 프로젝트 자신의 헤드리스 검사 묶음을 유니티 없이 돌린다.
    ///
    /// ## 왜 있는가
    ///
    /// `CLAUDE.md` §7 은 「자체 검증(`Ascend/Run Self Tests`)을 코드 변경 뒤에 돌리고
    /// 커밋한다」를 요구한다. 그런데 그 유일한 실행 경로가 **에디터 메뉴**였다.
    /// 즉 에디터가 없거나, 프로젝트에 컴파일 오류가 있어 도메인이 리로드되지 않는
    /// 상태에서는 검사를 돌릴 방법이 아예 없었다 — 그리고 그때가 바로 검사가 가장
    /// 필요한 순간이다. 같은 문서가 「깨진 채로 쌓으면 원인 분리가 불가능해진다」고
    /// 적어 둔 상황이 정확히 그것이다.
    ///
    /// ## 두 규칙은 `README.md` 의 것을 그대로 따른다
    ///
    /// ① **판정을 여기서 다시 구현하지 않는다.** 아래 목록은 프로젝트의 `RunAll()` 을
    ///    **부르기만** 한다. 여기에 단정을 하나라도 새로 쓰면 두 갈래가 된다.
    /// ② 대역이 삼킨 것이 있으면 그 사실이 함께 찍힌다 (`Debug.ErrorCount`).
    ///
    /// ## 여기서 돌지 않는 것
    ///
    /// `MonoBehaviour` 를 실제로 실행하거나 씬을 잡는 검사(`BuildLabelPlacementTests`,
    /// 캡처 리그, 퍼포먼스 프로브)는 들어 있지 않다. 대역은 그것들을 **컴파일은 하지만
    /// 실행하지 않는다.** 흉내 낸 것을 재면 안 되므로 목록에 넣지 않는다 — 그 검사들은
    /// 여전히 에디터에서 돌려야 한다. 이 러너가 에디터 자체 검증을 **대체하지 않는다.**
    /// </summary>
    internal static class TestRunner
    {
        /// <summary>
        /// 이름과 실행기 쌍. 새 묶음을 붙일 때는 여기 한 줄만 더한다 —
        /// 호출부가 하나라야 「어떤 묶음이 돌았는가」가 한 곳에서 읽힌다.
        /// </summary>
        private static readonly (string Name, Func<(int passed, int failed, string report)> Run)[] Suites =
        {
            ("층 루프 (RunTests)",            Prototype.Run.Tests.RunTests.RunAll),
            ("절제·탐욕 등급 (MercyHunger)",  Prototype.Run.Tests.MercyHungerTests.RunAll),
            ("스핀 엔진 (SpinEngine)",        Prototype.Spin.Tests.SpinEngineTests.RunAll),
            ("규칙 다발 (SpinRuleSet)",       Prototype.Spin.Tests.SpinRuleSetTests.RunAll),
            ("시뮬레이터 일치 (SimParity)",   Prototype.Sim.Tests.SimulatorParityTests.RunAll),
        };

        internal static int Run()
        {
            int totalPassed = 0;
            int totalFailed = 0;
            var failedSuites = new List<string>();

            foreach ((string name, var run) in Suites)
            {
                int passed;
                int failed;
                string report;

                try
                {
                    (passed, failed, report) = run();
                }
                catch (Exception exception)
                {
                    // 묶음 하나가 터져도 나머지를 돌린다. 첫 예외에서 멈추면
                    // 「하나 고칠 때마다 한 번씩 전부 다시 돌리기」가 되고,
                    // 그 비용이 곧 검사를 안 돌리는 이유가 된다.
                    totalFailed++;
                    failedSuites.Add(name);
                    Console.WriteLine($"\n=== {name} ===");
                    Console.WriteLine($"  FAIL  묶음 자체가 예외로 중단됨 — {exception}");
                    continue;
                }

                totalPassed += passed;
                totalFailed += failed;
                if (failed > 0) failedSuites.Add(name);

                Console.WriteLine($"\n=== {name} ===");
                // 통과한 묶음은 결과 줄만 남긴다. 전부 찍으면 실패 한 줄이 수백 줄
                // 사이에 묻히고, 그러면 보고서를 읽지 않게 된다.
                Console.WriteLine(failed > 0 ? report : $"  {passed} PASS / 0 FAIL");
            }

            Console.WriteLine();
            Console.WriteLine($"[tests] 합계 {totalPassed} PASS / {totalFailed} FAIL");
            if (failedSuites.Count > 0)
                Console.WriteLine($"[tests] 실패 묶음: {string.Join(", ", failedSuites)}");
            Console.WriteLine($"shim: warnings={UnityEngine.Debug.WarningCount} " +
                              $"errors={UnityEngine.Debug.ErrorCount}");

            // 대역 오류도 실패로 센다. 대역이 조용히 삼킨 호출은 「검사가 통과했다」의
            // 근거를 무너뜨리는데, 그 사실이 종료 코드에 안 나오면 아무도 안 본다.
            return totalFailed > 0 || UnityEngine.Debug.ErrorCount > 0 ? 1 : 0;
        }
    }
}
