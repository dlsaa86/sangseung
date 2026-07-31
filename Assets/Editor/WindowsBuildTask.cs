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

        [MenuItem("Ascend/Build Windows64")]
        public static void Schedule()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] Play 모드에서는 빌드하지 않는다.");
                return;
            }

            string root = Directory.GetCurrentDirectory();
            string report = Path.Combine(root, ReportPath);
            // 직전 결과를 지운다. 남아 있으면 이번에 안 돌았는데 돈 것처럼 읽힌다.
            if (File.Exists(report)) File.Delete(report);

            Debug.Log($"[상승] Windows64 빌드 시작 → {ReportPath}");
            Run();
        }

        /// <summary>
        /// 동기 빌드. `delayCall` 로 미루지 않는 이유: MCP `RunCommand` 는 호출마다 임시
        /// 어셈블리를 컴파일하고 그때 도메인이 리로드되면서 **등록해 둔 `delayCall` 이 지워진다.**
        /// 실제로 예약만 되고 빌드가 영영 돌지 않았다. 몇 분간 메인 스레드를 잡는 대신
        /// 결과가 반드시 남는 쪽을 택한다 — 호출자는 <see cref="ReportPath"/> 로 판정한다.
        /// </summary>
        public static void Run()
        {
            string root = Directory.GetCurrentDirectory();
            string outDir = Path.Combine(root, OutputDirectory);
            string report = Path.Combine(root, ReportPath);
            double t0 = EditorApplication.timeSinceStartup;

            var sb = new StringBuilder();
            sb.AppendLine("=== Windows64 빌드 ===");
            sb.AppendLine($"unity: {Application.unityVersion}");
            sb.AppendLine($"target: {BuildTarget.StandaloneWindows64}");
            sb.AppendLine($"scene: {ScenePath}");

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
                    options = BuildOptions.None,
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
