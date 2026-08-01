using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ascend.Prototype.Build.Tests;
using Ascend.Prototype.Risk.Tests;
using Ascend.Prototype.Run.Tests;
using Ascend.Prototype.Spin.Tests;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 테스트 진입점 하나. 러너가 흩어져 있으면 "어느 걸 돌려야 그린인가"가 매번 달라진다.
    ///
    /// 왜 Unity Test Runner(NUnit)가 아닌가: 이 프로젝트에는 asmdef이 없어 모든 코드가
    /// `Assembly-CSharp`에 있다. Unity 규칙상 asmdef 어셈블리는 predefined `Assembly-CSharp`를
    /// **참조할 수 없으므로**, 테스트 어셈블리를 추가해도 게임 코드를 볼 수 없다.
    /// asmdef 도입은 별도 결정 없이 하지 않는다(`TECH_SPEC.md` §3).
    /// → `DECISION_LOG.md` D-20260730-06.
    /// </summary>
    public static class AscendTestMenu
    {
        /// <summary>
        /// 이 목록이 곧 "EditMode 그린"의 정의다. 스위트를 새로 만들고 여기에 넣지 않으면
        /// 깨져도 아무도 모른다 — `PrototypeSelfTest`가 같은 이유로 뒤늦게 네 스위트를
        /// 편입한 전례가 있다(그 파일의 주석).
        /// </summary>
        private static (int passed, int failed, string report)[] AllSuites() => new[]
        {
            SpinEngineTests.RunAll(),
            SpinRuleSetTests.RunAll(),
            RunTests.RunAll(),
            RiskEvaluatorTests.RunAll(),
            BuildTests.RunAll(),
            Telemetry.Tests.TelemetryTests.RunAll(),
            Data.Profiles.Tests.ProfileTests.RunAll(),
            Npc.Tests.PassengerReactionTests.RunAll(),
            Audio.Tests.AudioTests.RunAll(),
            Perf.Tests.PerfTests.RunAll(),
        };

        [MenuItem("Ascend/Run All EditMode Tests %#t")]
        public static void RunAll()
        {
            var suites = AllSuites();
            int passed = 0, failed = 0;
            var sb = new System.Text.StringBuilder(4096);
            foreach (var s in suites)
            {
                passed += s.passed;
                failed += s.failed;
                sb.Append(s.report).Append("\n\n");
            }
            sb.Append($"합계: {passed} PASS / {failed} FAIL");
            string report = sb.ToString();

            // **산출물을 손으로 유지하지 않는다.** `tools/verify-topdown.ps1`이 이 파일의
            // "합계:" 줄을 읽어 완료를 판정하는데, 지금까지 이 파일은 아무도 쓰지 않아
            // 스위트가 늘어도 옛 숫자가 그대로 남아 있었다. 판정 근거가 되는 파일은
            // 판정 대상을 실제로 돌린 쪽이 써야 한다.
            WriteArtifact("editmode_tests.txt", report);

            if (failed > 0) Debug.LogError($"[상승]\n{report}");
            else Debug.Log($"[상승]\n{report}");
        }

        /// <summary>
        /// `Logs/` 아래에 산출물을 쓴다. 실패해도 테스트 결과를 삼키지 않는다 —
        /// 쓰기 실패는 경고이지 테스트 실패가 아니다.
        /// </summary>
        private static void WriteArtifact(string fileName, string body)
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, fileName), body + "\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[상승] {fileName} 을 쓰지 못했다: {e.GetType().Name}: {e.Message}");
            }
        }

        [MenuItem("Ascend/Run PlayMode Hero Slice Check")]
        public static void RunPlayModeCheck()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            // 직전 결과를 지운다. 남아 있으면 이번에 안 돌았는데 돈 것처럼 읽힌다.
            string path = Path.Combine(Directory.GetCurrentDirectory(), HeroSliceAutoPilot.ReportPath);
            if (File.Exists(path)) File.Delete(path);

            HeroSliceAutoPilot.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] PlayMode 검증 시작. 끝나면 자동 종료하고 {HeroSliceAutoPilot.ReportPath} 에 남긴다.");
        }

        /// <summary>
        /// 10층 런을 상호작용만으로 끝까지 몬다. `P2-Gate B`의 증거를 만드는 진입점이다.
        /// Hero Slice 검증과 별개인 이유는 `TenFloorAutoPilot` 주석에 있다.
        /// </summary>
        [MenuItem("Ascend/Run PlayMode TenFloor Check")]
        public static void RunTenFloorCheck()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string path = Path.Combine(Directory.GetCurrentDirectory(), TenFloorAutoPilot.ReportPath);
            if (File.Exists(path)) File.Delete(path);

            TenFloorAutoPilot.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 10층 PlayMode 검증 시작 → {TenFloorAutoPilot.ReportPath}");
        }

        [MenuItem("Ascend/Capture Hero Slice Set")]
        public static void RunCaptureSet()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string manifest = Path.Combine(Directory.GetCurrentDirectory(), HeroSliceCaptureRig.ManifestPath);
            if (File.Exists(manifest)) File.Delete(manifest);

            HeroSliceCaptureRig.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 고정 캡처 시작 → {HeroSliceCaptureRig.OutputDirectory}");
        }

        /// <summary>`AUTONOMOUS_PROTOTYPE_GOAL.md` §12의 필수 캡처 세트를 만든다.</summary>
        [MenuItem("Ascend/Capture Ten Floor Set")]
        public static void RunTenFloorCaptureSet()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string manifest = Path.Combine(Directory.GetCurrentDirectory(), TenFloorCaptureRig.ManifestPath);
            if (File.Exists(manifest)) File.Delete(manifest);

            TenFloorCaptureRig.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 10층 고정 캡처 시작 → {TenFloorCaptureRig.OutputDirectory}");
        }

        /// <summary>
        /// `P2-Gate G`의 "최대 적재와 Critical 상태 측정". Hero Slice 측정과 별개인 이유는
        /// <see cref="LoadedCriticalPerfProbe"/> 주석에 있다 — 그쪽은 무적재·Stable 만 잰다.
        /// </summary>
        [MenuItem("Ascend/Measure Loaded + Critical Performance")]
        public static void RunLoadedCriticalProbe()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string report = Path.Combine(Directory.GetCurrentDirectory(), LoadedCriticalPerfProbe.ReportPath);
            if (File.Exists(report)) File.Delete(report);

            LoadedCriticalPerfProbe.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 최대적재+Critical 측정 시작 → {LoadedCriticalPerfProbe.ReportPath}");
        }

        [MenuItem("Ascend/Measure Hero Slice Performance")]
        public static void RunPerfProbe()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string report = Path.Combine(Directory.GetCurrentDirectory(), HeroSlicePerfProbe.ReportPath);
            if (File.Exists(report)) File.Delete(report);

            HeroSlicePerfProbe.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 성능 측정 시작 → {HeroSlicePerfProbe.ReportPath}");
        }
    }
}
