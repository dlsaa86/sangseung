using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ascend.CaptureHarness
{
    /// <summary>
    /// Runs one CaptureSet inside Play Mode: pins the clock and RNG, renders the main camera
    /// into a fixed-size RenderTexture at each shot, and writes PNGs plus a manifest describing
    /// the exact conditions. Two runs of the same CaptureSet are pixel-comparable.
    ///
    /// Spawned automatically by CaptureBootstrap — you do not place this in a scene by hand.
    /// </summary>
    public class CaptureSession : MonoBehaviour
    {
        public CaptureSet Set;
        public string OutputDirectory;
        public string RunId;

        private const int MaxRecordedLogs = 200;

        /// <summary>Wall-clock ceiling for a whole run. Without it a stall would leave the
        /// editor stuck in Play Mode forever, which breaks unattended automation.</summary>
        private const float WatchdogSeconds = 300f;

        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _written = new List<string>();

        private Camera _camera;
        private RenderTexture _target;
        private RenderTexture _resolve;
        private RenderTexture _previousCameraTarget;
        private bool _finished;

        /// <summary>A Screen Space Overlay canvas we temporarily pointed at the capture camera.</summary>
        private struct RedirectedCanvas
        {
            public Canvas Canvas;
            public Camera PreviousCamera;
            public float PreviousPlaneDistance;
        }

        private readonly List<RedirectedCanvas> _redirected = new List<RedirectedCanvas>();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    if (_errors.Count < MaxRecordedLogs) _errors.Add(type + ": " + condition);
                    break;
                case LogType.Warning:
                    if (_warnings.Count < MaxRecordedLogs) _warnings.Add(condition);
                    break;
            }
        }

        private IEnumerator Start()
        {
            if (Set == null)
            {
                Debug.LogError("[CaptureHarness] No CaptureSet assigned. Aborting.");
                Finish(false);
                yield break;
            }

            if (string.IsNullOrEmpty(OutputDirectory) || !Directory.Exists(OutputDirectory))
            {
                Debug.LogError("[CaptureHarness] Output directory '" + OutputDirectory +
                               "' does not exist. Aborting rather than writing to the working directory.");
                Finish(false);
                yield break;
            }

            StartCoroutine(Watchdog());

            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("[CaptureHarness] No camera tagged MainCamera in the scene. Aborting.");
                Finish(false);
                yield break;
            }

            ApplyDeterminism();
            CreateTarget();

            for (int i = 0; i < Set.WarmupFrames; i++)
                yield return null;

            for (int i = 0; i < Set.Shots.Count; i++)
            {
                CaptureShot shot = Set.Shots[i];

                foreach (string key in shot.KeysBefore)
                    yield return PressKey(key);

                if (shot.OverridePose)
                {
                    _camera.transform.SetPositionAndRotation(shot.Position, Quaternion.Euler(shot.EulerAngles));
                    _camera.fieldOfView = shot.FieldOfView;
                }

                for (int f = 0; f < shot.SettleFrames; f++)
                    yield return null;

                // Render on demand rather than waiting for end-of-frame. WaitForEndOfFrame only
                // resumes when the Game View repaints, which an unfocused or hidden editor never
                // does — that would hang the whole run under automation.
                RenderCaptureFrame();
                WriteShot(i, shot);
            }

            Finish(true);
        }

        /// <summary>
        /// Pins everything that would otherwise make two runs differ: frame duration, RNG,
        /// vsync and framerate throttling.
        /// </summary>
        private bool _previousRunInBackground;

        private void ApplyDeterminism()
        {
            UnityEngine.Random.InitState(Set.RandomSeed);

            // Without this the player loop stalls whenever the editor loses focus, so an
            // unattended run would simply never progress.
            _previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            // captureDeltaTime makes every frame advance exactly 1/fps of game time regardless
            // of how long it really took, so time-dependent motion lands identically each run.
            Time.captureDeltaTime = 1f / Set.CaptureFps;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        private void RestoreDeterminism()
        {
            Time.captureDeltaTime = 0f;
            Application.runInBackground = _previousRunInBackground;
        }

        private IEnumerator Watchdog()
        {
            float deadline = Time.realtimeSinceStartup + WatchdogSeconds;
            while (!_finished)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogError("[CaptureHarness] Run exceeded " + WatchdogSeconds +
                                   "s and was aborted. Captured " + _written.Count + " shot(s).");
                    Finish(false);
                    yield break;
                }
                yield return null;
            }
        }

        private void CreateTarget()
        {
            int samples = Mathf.Max(1, Set.MsaaSamples);

            _target = new RenderTexture(Set.Width, Set.Height, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "CaptureHarness_Target",
                antiAliasing = samples,
                useMipMap = false
            };
            _target.Create();

            // ReadPixels cannot read a multisampled surface directly, so an MSAA run needs a
            // plain resolve target to blit into first.
            if (samples > 1)
            {
                _resolve = new RenderTexture(Set.Width, Set.Height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "CaptureHarness_Resolve",
                    antiAliasing = 1,
                    useMipMap = false
                };
                _resolve.Create();
            }

            _previousCameraTarget = _camera.targetTexture;
            _camera.targetTexture = _target;

            RedirectOverlayCanvases();
        }

        /// <summary>
        /// Screen Space Overlay canvases draw straight to the backbuffer and bypass any camera
        /// targetTexture, so UI would be missing from every capture. Point them at the capture
        /// camera for the duration of the run — close to the near plane so they stay on top,
        /// which is what Overlay effectively does.
        /// </summary>
        private void RedirectOverlayCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                _redirected.Add(new RedirectedCanvas
                {
                    Canvas = canvas,
                    PreviousCamera = canvas.worldCamera,
                    PreviousPlaneDistance = canvas.planeDistance
                });

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _camera;
                canvas.planeDistance = _camera.nearClipPlane + 0.01f;
            }

            if (_redirected.Count > 0)
                Debug.Log("[CaptureHarness] Redirected " + _redirected.Count +
                          " Screen Space Overlay canvas(es) into the capture camera.");
        }

        private void RestoreCanvases()
        {
            foreach (RedirectedCanvas entry in _redirected)
            {
                if (entry.Canvas == null) continue;

                entry.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                entry.Canvas.worldCamera = entry.PreviousCamera;
                entry.Canvas.planeDistance = entry.PreviousPlaneDistance;
            }
            _redirected.Clear();
        }

        /// <summary>
        /// Forces the capture camera to render into its target right now. Under a Scriptable
        /// Render Pipeline the supported path is a render request; Camera.Render() is only the
        /// fallback for a built-in pipeline, where render requests are not available.
        /// </summary>
        private void RenderCaptureFrame()
        {
            UniversalRenderPipeline.SingleCameraRequest request =
                new UniversalRenderPipeline.SingleCameraRequest();

            if (RenderPipeline.SupportsRenderRequest(_camera, request))
            {
                request.destination = _target;
                RenderPipeline.SubmitRenderRequest(_camera, request);
                return;
            }

            _camera.Render();
        }

        /// <summary>The texture ReadPixels should be pointed at for this run.</summary>
        private RenderTexture ReadbackSource
        {
            get
            {
                if (_resolve == null) return _target;
                Graphics.Blit(_target, _resolve);
                return _resolve;
            }
        }

        private void ReleaseTarget()
        {
            RestoreCanvases();

            if (_camera != null) _camera.targetTexture = _previousCameraTarget;

            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
                _target = null;
            }

            if (_resolve != null)
            {
                _resolve.Release();
                Destroy(_resolve);
                _resolve = null;
            }
        }

        private void WriteShot(int index, CaptureShot shot)
        {
            string safeName = string.IsNullOrWhiteSpace(shot.Name) ? "shot" : shot.Name;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalid, '_');

            string fileName = index.ToString("00", CultureInfo.InvariantCulture) + "_" + safeName + ".png";
            string fullPath = Path.Combine(OutputDirectory, fileName);

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = ReadbackSource;

            Texture2D readback = new Texture2D(Set.Width, Set.Height, TextureFormat.RGB24, false);
            readback.ReadPixels(new Rect(0, 0, Set.Width, Set.Height), 0, 0);
            readback.Apply();

            RenderTexture.active = previousActive;

            try
            {
                File.WriteAllBytes(fullPath, readback.EncodeToPNG());
                _written.Add(fileName);
            }
            catch (Exception e)
            {
                Debug.LogError("[CaptureHarness] Failed to write " + fullPath + ": " + e.Message);
            }
            finally
            {
                Destroy(readback);
            }
        }

        /// <summary>
        /// Presses and releases a key through the Input System's low-level event queue so that
        /// polling code (Keyboard.current.xKey.wasPressedThisFrame) observes it normally.
        /// </summary>
        private IEnumerator PressKey(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName)) yield break;

            if (!Enum.TryParse(keyName.Trim(), true, out Key key))
            {
                Debug.LogError("[CaptureHarness] Unknown Input System key '" + keyName + "'.");
                yield break;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                Debug.LogError("[CaptureHarness] No keyboard device available for simulated input.");
                yield break;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private void Finish(bool completed)
        {
            if (_finished) return;
            _finished = true;

            ReleaseTarget();
            RestoreDeterminism();
            WriteManifest(completed);

            Debug.Log("[CaptureHarness] Run " + RunId + " finished. " +
                      _written.Count + " shot(s), " + _errors.Count + " error(s).");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void WriteManifest(bool completed)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"runId\": ").Append(Json(RunId)).Append(",\n");
            sb.Append("  \"completed\": ").Append(completed ? "true" : "false").Append(",\n");
            sb.Append("  \"captureSet\": ").Append(Json(Set != null ? Set.name : null)).Append(",\n");
            sb.Append("  \"scenePath\": ").Append(Json(Set != null ? Set.ScenePath : null)).Append(",\n");
            sb.Append("  \"unityVersion\": ").Append(Json(Application.unityVersion)).Append(",\n");
            sb.Append("  \"width\": ").Append(Set != null ? Set.Width : 0).Append(",\n");
            sb.Append("  \"height\": ").Append(Set != null ? Set.Height : 0).Append(",\n");
            sb.Append("  \"captureFps\": ").Append(Set != null ? Set.CaptureFps : 0).Append(",\n");
            sb.Append("  \"randomSeed\": ").Append(Set != null ? Set.RandomSeed : 0).Append(",\n");
            sb.Append("  \"warmupFrames\": ").Append(Set != null ? Set.WarmupFrames : 0).Append(",\n");
            sb.Append("  \"errorCount\": ").Append(_errors.Count).Append(",\n");
            sb.Append("  \"warningCount\": ").Append(_warnings.Count).Append(",\n");
            AppendArray(sb, "shots", _written);
            sb.Append(",\n");
            AppendArray(sb, "errors", _errors);
            sb.Append(",\n");
            AppendArray(sb, "warnings", _warnings);
            sb.Append("\n}\n");

            try
            {
                File.WriteAllText(Path.Combine(OutputDirectory, "manifest.json"), sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[CaptureHarness] Failed to write manifest: " + e.Message);
            }
        }

        private static void AppendArray(StringBuilder sb, string key, List<string> values)
        {
            sb.Append("  \"").Append(key).Append("\": [");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("\n    ").Append(Json(values[i]));
            }
            if (values.Count > 0) sb.Append("\n  ");
            sb.Append(']');
        }

        private static string Json(string value)
        {
            if (value == null) return "null";

            StringBuilder sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
