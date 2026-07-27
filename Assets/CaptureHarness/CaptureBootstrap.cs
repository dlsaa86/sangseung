using UnityEngine;

namespace Ascend.CaptureHarness
{
    /// <summary>
    /// SessionState keys shared between the editor-side runner and the play-mode bootstrap.
    /// SessionState survives the domain reload that happens on entering Play Mode; statics do not.
    /// </summary>
    public static class CaptureKeys
    {
        public const string PendingSetGuid = "CaptureHarness.PendingSetGuid";
        public const string RunId          = "CaptureHarness.RunId";
        public const string OutputDir      = "CaptureHarness.OutputDir";
    }

#if UNITY_EDITOR
    /// <summary>
    /// Spawns a CaptureSession once Play Mode starts, but only when the editor runner has
    /// armed a pending CaptureSet. Normal Play Mode sessions are unaffected.
    /// </summary>
    internal static class CaptureBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string guid = UnityEditor.SessionState.GetString(CaptureKeys.PendingSetGuid, string.Empty);
            if (string.IsNullOrEmpty(guid)) return;

            // Consume immediately so a manual Play press afterwards does not re-trigger a capture.
            UnityEditor.SessionState.SetString(CaptureKeys.PendingSetGuid, string.Empty);

            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            CaptureSet set = UnityEditor.AssetDatabase.LoadAssetAtPath<CaptureSet>(assetPath);
            if (set == null)
            {
                Debug.LogError("[CaptureHarness] Pending CaptureSet " + guid + " could not be loaded.");
                return;
            }

            GameObject host = new GameObject("~CaptureSession");
            CaptureSession session = host.AddComponent<CaptureSession>();
            session.Set = set;
            session.RunId = UnityEditor.SessionState.GetString(CaptureKeys.RunId, "unknown");
            session.OutputDirectory = UnityEditor.SessionState.GetString(CaptureKeys.OutputDir, string.Empty);
        }
    }
#endif
}
