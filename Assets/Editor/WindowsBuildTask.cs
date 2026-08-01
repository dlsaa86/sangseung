using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// Windows64 플레이어 빌드. `MASTER_PRD.md` §13.5 와 `P2-Gate G` 가 요구하는
    /// "실행 가능한 Windows 빌드 또는 빌드 차단 원인"의 산출 경로다.
    ///
    /// 왜 별도 스크립트인가: Unity MCP 의 `RunCommand` 샌드박스는 `BuildPipeline.BuildPlayer`
    /// 를 사용자 상호작용이 필요한 호출로 보고 거부한다("User interactions are not supported").
    /// 자동화가 빌드를 못 돌리면 게이트 증거를 사람 손에 의존하게 되므로, 진입점을 프로젝트
    /// 안에 두고 MCP 는 <see cref="Schedule"/> 만 부른다.
    ///
    /// 왜 `delayCall` 인가: 빌드는 수 분간 메인 스레드를 잡는다. MCP 호출 안에서 동기로 돌면
    /// 그 호출이 타임아웃까지 매달리고, 호출자는 빌드 실패와 타임아웃을 구분할 수 없다.
    /// 결과는 <see cref="ReportPath"/> 파일 하나로만 판정한다 — 파일이 없으면 아직 도는 중이다.
    /// </summary>
    public static class WindowsBuildTask
    {
        public const string ReportPath = "Logs/build_report.txt";
        public const string OutputDirectory = "Builds/Windows";
        private const string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";

        /// <summary>
        /// 개발 빌드 산출물. 릴리스와 **따로 둔다** — 하나를 덮어쓰면
        /// `verify-topdown.ps1` 의 C7 이 가리키는 실행 파일이 어느 쪽인지 알 수 없다.
        /// </summary>
        public const string DevOutputDirectory = "Builds/WindowsDev";

        public const string DevReportPath = "Logs/build_report_dev.txt";

        [MenuItem("Ascend/Build Windows64")]
        public static void Schedule()
        {
            ScheduleInternal(false);
        }

        /// <summary>
        /// 개발 빌드. `DEVELOPMENT_BUILD` 가 정의되고 프로파일러 카운터가 살아 있다.
        ///
        /// **왜 별도 메뉴가 필요한가** — 세 항목이 이것 없이는 증명될 수 없다.
        /// ① `UP-TECH-03`(필수 참조 누락 시 개발 빌드에서 즉시 오류) — 검사기의 런타임
        ///    경로가 `#if DEVELOPMENT_BUILD || UNITY_EDITOR` 안이라, 릴리스 빌드에는
        ///    **코드 자체가 들어가지 않는다.** 지금까지의 증거는 전부 에디터 쪽이었다.
        /// ② `UP-TECH-04`(90 FPS) — 에디터 게임 뷰가 디스플레이에 동기돼 중앙값이
        ///    상한에 눌린다. 산출물이 스스로 「중앙값으로는 판정할 수 없다」고 적는다.
        ///    빌드에서 재는 것이 유일한 길이다.
        /// ③ `UP-TECH-05`(매 프레임 0 B GC) — 플레이어에 `GC Allocated In Frame`
        ///    카운터가 있어야 하고, 그것은 개발 빌드 옵션이 켜져야 붙는다.
        ///
        /// `AllowDebugging` 은 켜지 않는다 — 디버거 연결을 기다리며 멈출 수 있고,
        /// 여기서 재려는 것은 프레임 비용이지 중단점이 아니다.
        /// </summary>
        [MenuItem("Ascend/Build Windows64 (Development)")]
        public static void ScheduleDevelopment()
        {
            ScheduleInternal(true);
        }

        private static void ScheduleInternal(bool development)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] Play 모드에서는 빌드하지 않는다.");
                return;
            }

            string root = Directory.GetCurrentDirectory();
            string report = Path.Combine(root, development ? DevReportPath : ReportPath);
            // 직전 결과를 지운다. 남아 있으면 이번에 안 돌았는데 돈 것처럼 읽힌다.
            if (File.Exists(report)) File.Delete(report);

            Debug.Log($"[상승] Windows64 {(development ? "개발 " : string.Empty)}빌드 시작 → {(development ? DevReportPath : ReportPath)}");
            Run(development);
        }

        /// <summary>
        /// 동기 빌드. `delayCall` 로 미루지 않는 이유: MCP `RunCommand` 는 호출마다 임시
        /// 어셈블리를 컴파일하고 그때 도메인이 리로드되면서 **등록해 둔 `delayCall` 이 지워진다.**
        /// 실제로 예약만 되고 빌드가 영영 돌지 않았다. 몇 분간 메인 스레드를 잡는 대신
        /// 결과가 반드시 남는 쪽을 택한다 — 호출자는 <see cref="ReportPath"/> 로 판정한다.
        /// </summary>
        public static void Run() => Run(false);

        public static void Run(bool development)
        {
            string root = Directory.GetCurrentDirectory();
            string outDir = Path.Combine(root, development ? DevOutputDirectory : OutputDirectory);
            string report = Path.Combine(root, development ? DevReportPath : ReportPath);
            double t0 = EditorApplication.timeSinceStartup;

            var sb = new StringBuilder();
            sb.AppendLine($"=== Windows64 {(development ? "개발" : "릴리스")} 빌드 ===");
            sb.AppendLine($"unity: {Application.unityVersion}");
            sb.AppendLine($"target: {BuildTarget.StandaloneWindows64}");
            sb.AppendLine($"scene: {ScenePath}");
            // 이 줄이 있어야 나중에 「어느 빌드에서 잰 값인가」를 되물을 수 있다.
            // 릴리스에는 DEVELOPMENT_BUILD 가 정의되지 않아 진단·프로파일러 경로가 통째로 빠진다.
            sb.AppendLine($"development: {development}  (DEVELOPMENT_BUILD {(development ? "정의됨" : "없음")})");

            try
            {
                Directory.CreateDirectory(outDir);

                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                {
                    sb.AppendLine("result: BLOCKED");
                    sb.AppendLine("차단 원인: Windows Build Support 모듈이 설치돼 있지 않다.");
                    File.WriteAllText(report, sb.ToString());
                    Debug.LogError("[상승] 빌드 차단 — Windows Build Support 없음");
                    return;
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(outDir, "Upandup_DDD.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    // `AllowDebugging` 은 일부러 빼 둔다 — 디버거를 기다리며 멈출 수 있고,
                    // 개발 빌드로 재려는 것은 프레임 비용이지 중단점이 아니다.
                    options = development
                        ? (BuildOptions.Development | BuildOptions.ConnectWithProfiler)
                        : BuildOptions.None,
                };

                BuildReport rep = BuildPipeline.BuildPlayer(options);
                BuildSummary s = rep.summary;

                sb.AppendLine($"result: {s.result}");
                sb.AppendLine($"totalErrors: {s.totalErrors} / totalWarnings: {s.totalWarnings}");
                sb.AppendLine($"totalSize: {s.totalSize} bytes ({s.totalSize / 1048576.0:F1} MB)");
                sb.AppendLine($"outputPath: {s.outputPath}");
                sb.AppendLine($"elapsedSec: {EditorApplication.timeSinceStartup - t0:F1}");

                foreach (BuildStep step in rep.steps)
                    foreach (BuildStepMessage msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            sb.AppendLine($"ERR [{step.name}] {msg.content}");

                File.WriteAllText(report, sb.ToString());
                Debug.Log($"[상승] 빌드 종료 {s.result} → {ReportPath}");
            }
            catch (Exception e)
            {
                sb.AppendLine("result: EXCEPTION");
                sb.AppendLine(e.ToString());
                File.WriteAllText(report, sb.ToString());
                Debug.LogError($"[상승] 빌드 예외 {e.Message}");
            }
        }
    }
}
