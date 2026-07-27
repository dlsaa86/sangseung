using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// Editor entry point for the capture harness. Opens the target scene, arms the bootstrap
    /// and enters Play Mode; CaptureSession takes over from there and exits Play Mode when done.
    /// </summary>
    public static class CaptureHarnessRunner
    {
        /// <summary>Root of all capture output, next to Assets/ and outside the asset database.</summary>
        public static string CapturesRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));

        [MenuItem("Tools/Capture Harness/Run Selected Capture Set")]
        public static void RunSelected()
        {
            CaptureSet set = Selection.activeObject as CaptureSet;
            if (set == null)
            {
                EditorUtility.DisplayDialog("Capture Harness",
                    "Select a CaptureSet asset in the Project window first.", "OK");
                return;
            }

            Run(set);
        }

        [MenuItem("Tools/Capture Harness/Run Selected Capture Set", isValidateFunction: true)]
        private static bool ValidateRunSelected() => Selection.activeObject is CaptureSet;

        [MenuItem("Tools/Capture Harness/Open Captures Folder")]
        public static void OpenCapturesFolder()
        {
            Directory.CreateDirectory(CapturesRoot);
            EditorUtility.RevealInFinder(CapturesRoot);
        }

        /// <summary>
        /// Runs a capture set. Safe to call from automation (Unity MCP, CLI) as long as the
        /// editor is idle — it returns immediately and the run completes asynchronously in Play Mode.
        /// </summary>
        public static void Run(CaptureSet set)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[CaptureHarness] Already in Play Mode. Exit Play Mode before starting a run.");
                return;
            }

            // Read everything off the asset up front. OpenScene triggers an unused-asset
            // sweep that can destroy the native CaptureSet object while this managed
            // reference still looks alive, so nothing below may touch `set`.
            string setName = set.name;
            string scenePath = set.ScenePath;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(set));

            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("[CaptureHarness] CaptureSet must be saved as an asset before running.");
                return;
            }

            if (!string.IsNullOrEmpty(scenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[CaptureHarness] Run cancelled — scene save was declined.");
                    return;
                }

                if (!File.Exists(scenePath))
                {
                    Debug.LogError("[CaptureHarness] Scene not found: " + scenePath);
                    return;
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string runId = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                           + "_" + setName;
            string outputDir = Path.Combine(CapturesRoot, runId);
            Directory.CreateDirectory(outputDir);

            SessionState.SetString(CaptureKeys.PendingSetGuid, guid);
            SessionState.SetString(CaptureKeys.RunId, runId);
            SessionState.SetString(CaptureKeys.OutputDir, outputDir);

            // Lets an evaluating agent find the newest run without listing and sorting the folder.
            File.WriteAllText(Path.Combine(CapturesRoot, "last-run.txt"), runId);

            Debug.Log("[CaptureHarness] Starting run " + runId + " → " + outputDir);
            EditorApplication.EnterPlaymode();
        }
    }
}
