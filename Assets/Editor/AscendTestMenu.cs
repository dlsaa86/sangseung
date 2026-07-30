using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        [MenuItem("Ascend/Run All EditMode Tests %#t")]
        public static void RunAll()
        {
            var spin = SpinEngineTests.RunAll();
            var run = RunTests.RunAll();
            int passed = spin.passed + run.passed;
            int failed = spin.failed + run.failed;

            string report = $"{spin.report}\n\n{run.report}\n\n" +
                            $"[상승] 합계: {passed} PASS / {failed} FAIL";

            if (failed > 0) Debug.LogError(report);
            else Debug.Log(report);
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
    }
}
