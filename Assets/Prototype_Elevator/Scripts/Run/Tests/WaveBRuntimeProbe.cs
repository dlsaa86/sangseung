using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Audio;
using Ascend.Prototype.Events;
using Ascend.Prototype.Npc;
using Ascend.Prototype.Perf;
using Ascend.Prototype.Player;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.Telemetry;
using Ascend.Prototype.View;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// Pass 1 Wave B 배선이 **런타임에 실제로 움직이는가**를 숫자로 남긴다.
    ///
    /// 10층 오토파일럿은 `IInteractable.Interact()` 만 부르므로 조준·연출 경로를 지나가지
    /// 않는다. 그래서 "붙였다"와 "돈다"가 그 검증만으로는 구분되지 않는다 —
    /// 특히 <see cref="CollapseSequence"/> 는 Critical 과 Collapse 를 구분하기 위해 만든 것이라
    /// (수정 백로그 UP-FIX-05) 실제로 무언가가 움직였는지가 유일한 판정 근거다.
    ///
    /// 게이트가 아니라 **측정**이다. 여기서 PASS/FAIL 을 세지 않는 이유는, 이 값들이
    /// 승인 대기 연출 파라미터에 붙어 있어서 기준선을 지금 잠그면 다음 조정이
    /// 전부 회귀로 보이기 때문이다.
    /// </summary>
    public sealed class WaveBRuntimeProbe : MonoBehaviour
    {
        public const string ReportPath = "Logs/waveb_runtime.txt";
        private const string PrefKey = "Ascend.WaveBRuntimeProbe.Armed";

        private readonly StringBuilder _report = new StringBuilder();
        private int _errorLogs;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);
            var go = new GameObject("WaveBRuntimeProbe");
            go.AddComponent<WaveBRuntimeProbe>();
        }

        public static void Arm() => UnityEditor.EditorPrefs.SetBool(PrefKey, true);
#endif

        private void Awake() => Application.logMessageReceived += OnLog;
        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errorLogs++;
            if (_errorLogs <= 12) _report.AppendLine($"  콘솔오류  [{type}] {condition}");
        }

        private IEnumerator Start()
        {
            _report.AppendLine("=== Pass 1 Wave B 런타임 측정 ===");
            yield return null;
            yield return null;

            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var collapse = FindAnyObjectByType<CollapseSequence>();
            var audio = FindAnyObjectByType<AudioDirector>();
            var printer = FindAnyObjectByType<PaperTapePrinterView>();
            var telemetry = FindAnyObjectByType<TelemetryRecorderBehaviour>();
            var reactions = FindAnyObjectByType<PassengerReactionView>();
            var memory = FindAnyObjectByType<MemoryTrendProbe>();
            var render = FindAnyObjectByType<RenderBudgetProbe>();
            var riskBridge = FindAnyObjectByType<RiskEventBridge>();
            var approach = FindAnyObjectByType<OverharvestApproachBridge>();
            var interactor = FindAnyObjectByType<CrosshairInteractor>();

            _report.AppendLine($"  컴포넌트 존재: run={run != null} collapse={collapse != null} audio={audio != null} " +
                               $"printer={printer != null} telemetry={telemetry != null} reactions={reactions != null} " +
                               $"memory={memory != null} render={render != null} riskBridge={riskBridge != null} approach={approach != null}");

            // ── 1. 카메라 실제 월드 포즈 ─────────────────────────────────────
            Camera cam = Camera.main;
            Transform head = GameObject.Find("Player") != null ? GameObject.Find("Player").transform.Find("Head") : null;
            if (cam != null)
            {
                _report.AppendLine($"  [카메라] 부모={(cam.transform.parent != null ? cam.transform.parent.name : "<없음>")} " +
                                   $"lp={cam.transform.localPosition:F3} wp={cam.transform.position:F3} " +
                                   $"eul={cam.transform.eulerAngles:F1}");
                if (head != null)
                    _report.AppendLine($"  [카메라] Head wp={head.position:F3} → 카메라가 머리보다 {(cam.transform.position.y - head.position.y):F3} m 높다");
            }

            // ── 2. 종이 테이프 — 런 시작 헤더가 찍혔는가 ────────────────────
            yield return new WaitForSeconds(2f);
            if (printer != null)
                _report.AppendLine($"  [프린터] 인쇄된 줄 {printer.PrintedLines.Count} / 대기 {printer.PendingLines} / 인쇄중 {printer.IsPrinting}");
            LogTape(printer);

            // ── 3. 오디오 — 사건을 넣고 실제로 재생됐는지 센다 ──────────────
            RunSession session = run != null ? run.Session : null;
            GameEventBus bus = session != null ? session.Events : null;
            int cuesBefore = audio != null ? audio.PlayedCueCount : -1;
            if (bus != null)
            {
                bus.Publish(GameEventKind.NormalSoulHarvested, 1, 0, 3, 0f, "probe");
                bus.Publish(GameEventKind.CascadeStep, 1, 0, 2, 0f, "probe");
                bus.Publish(GameEventKind.PowerBanked, 1, 0, 0, 120f, "probe");
            }
            yield return new WaitForSeconds(1.5f);
            if (audio != null)
                _report.AppendLine($"  [오디오] 재생 {audio.PlayedCueCount}건(이전 {cuesBefore}) / 버림 {audio.DroppedCueCount} / 무음사건 {audio.SilentEventCount}");

            // ── 4. 과수확 정적 — 게인이 실제로 내려갔다 올라오는가 ──────────
            if (bus != null && audio != null)
            {
                bus.Publish(GameEventKind.OverharvestApproached, 1, -1, 0, 0f, "probe");
                yield return new WaitForSeconds(0.25f);
                float duringGain = audio.SilenceGain;
                yield return new WaitForSeconds(1.2f);
                float afterGain = audio.SilenceGain;
                _report.AppendLine($"  [정적] 접근 0.25초 후 게인 {duringGain:F3} → 1.45초 후 {afterGain:F3} (1.0 이 평상)");
            }
            if (approach != null && interactor != null)
                _report.AppendLine($"  [과수확 접근] IsApproaching={approach.IsApproaching} 조준대상={(interactor.CurrentInteractable == null ? "<없음>" : interactor.CurrentInteractable.GetType().Name)}");

            // ── 5. 붕괴 연출 — 무엇이 얼마나 움직였는가 ─────────────────────
            if (collapse != null)
            {
                Transform lampRig = Find("CeilingLampRig");
                Transform tank = Find("PowerTank");
                Transform sign = Find("FloorIndicator");
                Transform rig = Find("CameraRig");

                Vector3 lamp0 = Home(lampRig), tank0 = Home(tank), sign0 = Home(sign), rig0 = Home(rig);
                _report.AppendLine($"  [붕괴] 시작 전 lampRig={lamp0:F4} tank={tank0:F4} sign={sign0:F4} camRig={rig0:F4}");

                // 평상 상태 한 장. Collapse 와 나란히 놓지 않으면 "구분되는가"를 물을 수 없다.
                Shoot(cam, "01_stable_printer");
                collapse.Begin();

                float maxLamp = 0f, maxTank = 0f, maxSign = 0f, maxRig = 0f;
                float minLightMul = 99f, maxLightMul = -99f;
                var risk = FindAnyObjectByType<RiskStateView>();
                float t = 0f;
                bool shotBlackout = false, shotDrop = false;
                while (t < collapse.TotalSeconds + 0.3f)
                {
                    t += Time.deltaTime;
                    maxLamp = Mathf.Max(maxLamp, Mathf.Abs(Home(lampRig).y - lamp0.y));
                    maxTank = Mathf.Max(maxTank, Mathf.Abs(Home(tank).y - tank0.y));
                    maxSign = Mathf.Max(maxSign, Mathf.Abs(Home(sign).y - sign0.y));
                    maxRig = Mathf.Max(maxRig, Mathf.Abs(Home(rig).y - rig0.y));
                    if (risk != null)
                    {
                        minLightMul = Mathf.Min(minLightMul, risk.CabinLightMultiplier);
                        maxLightMul = Mathf.Max(maxLightMul, risk.CabinLightMultiplier);
                    }
                    if (!shotBlackout && collapse.Elapsed > 0.25f) { Shoot(cam, "02_collapse_blackout"); shotBlackout = true; }
                    if (!shotDrop && collapse.Elapsed > 0.90f) { Shoot(cam, "03_collapse_drop"); shotDrop = true; }
                    yield return null;
                }
                Shoot(cam, "04_after_collapse");

                _report.AppendLine($"  [붕괴] 최대 낙차 lampRig={maxLamp:F4}m tank={maxTank:F4}m sign={maxSign:F4}m camRig={maxRig:F4}m");
                _report.AppendLine($"  [붕괴] 실내등 배수 {minLightMul:F3} ~ {maxLightMul:F3} (평상 1.0 — 0 이면 완전 암전이 걸렸다)");
                _report.AppendLine($"  [붕괴] 종료 후 복귀 lampRig={Home(lampRig):F4} tank={Home(tank):F4} sign={Home(sign):F4} camRig={Home(rig):F4} playing={collapse.IsPlaying}");
                _report.AppendLine($"  [붕괴] 복귀 오차 lamp={Vector3.Distance(Home(lampRig), lamp0):F5} tank={Vector3.Distance(Home(tank), tank0):F5} sign={Vector3.Distance(Home(sign), sign0):F5} cam={Vector3.Distance(Home(rig), rig0):F5}");
            }

            // ── 6. 나머지 계측기 ─────────────────────────────────────────────
            if (reactions != null)
                _report.AppendLine($"  [승객반응] 시작 {reactions.StartedCount} / 억제 {reactions.SuppressedCount} / 진행중 {reactions.ActiveReactionCount}");
            if (telemetry != null)
                _report.AppendLine($"  [텔레메트리] 기록기={(telemetry.Recorder != null)} 파일={(telemetry.CurrentFilePath ?? "<없음>")}");
            if (memory != null)
                _report.AppendLine($"  [메모리] 표본 {memory.SampleCount}개 절단={memory.Truncated}");
            if (render != null)
                _report.AppendLine($"  [렌더예산] 표본 {render.TotalSamples}개 예산주입={render.HasBudget}");
            if (riskBridge != null)
                _report.AppendLine($"  [위험사건] 마지막 알림 {riskBridge.LastAnnounced}");

            LogTape(printer);
            _report.AppendLine($"콘솔오류 {_errorLogs}건");

            Write();
            yield return null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void LogTape(PaperTapePrinterView printer)
        {
            if (printer == null) return;
            var lines = printer.PrintedLines;
            _report.AppendLine($"  [테이프] {lines.Count}줄");
            for (int i = 0; i < lines.Count && i < 10; i++) _report.AppendLine("      | " + lines[i]);
        }

        /// <summary>
        /// 문지방에서 정면 벽(사고 기록기)을 본 고정 프레임. 카메라를 이 자리에 **고정**하는
        /// 이유는 붕괴 중 <c>CameraRig</c> 가 흔들려도 프레임이 같아야 두 장을 비교할 수
        /// 있기 때문이다 — 흔들리는 카메라로 찍으면 무엇이 움직였는지 알 수 없다.
        /// </summary>
        private void Shoot(Camera source, string name)
        {
            // 문지방 바로 안쪽(z=1.10)에서 정면 벽을 본다. z=1.30 · 목표 y=1.85 로 잡았을 때는
            // 프레임의 60%가 정면 벽 하나로 덮이고 기록기가 화면 밖 위쪽에 걸렸다 —
            // 두 장을 비교할 수 없는 그림이었다. 목표를 장치 중심(y≈2.02)에 맞춘다.
            var eye = new Vector3(0.65f, 1.62f, 1.10f);
            var look = new Vector3(0.55f, 2.02f, -1.43f);

            // 플레이어 카메라를 빌리지 않는다. 붕괴 중에는 그 카메라가 리그에 실려
            // 흔들리므로, 같은 프레임을 두 번 찍을 수 없어 비교가 성립하지 않는다.
            if (_shotCamera == null)
            {
                var go = new GameObject("WaveBProbeCamera");
                go.transform.SetParent(null, false);
                _shotCamera = go.AddComponent<Camera>();
                if (source != null)
                {
                    _shotCamera.clearFlags = source.clearFlags;
                    _shotCamera.backgroundColor = source.backgroundColor;
                    _shotCamera.cullingMask = source.cullingMask;
                    _shotCamera.nearClipPlane = source.nearClipPlane;
                    _shotCamera.farClipPlane = source.farClipPlane;
                }
                _shotCamera.fieldOfView = 60f;
                _shotCamera.enabled = false;
            }

            _shotCamera.transform.position = eye;
            _shotCamera.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture prevActive = RenderTexture.active;
            _shotCamera.targetTexture = rt;
            _shotCamera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "waveb");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            _report.AppendLine($"  [캡처] Captures/waveb/{name}.png");

            _shotCamera.targetTexture = null;
            RenderTexture.active = prevActive;
            Destroy(tex);
            rt.Release();
            Destroy(rt);
        }

        private Camera _shotCamera;

        private static Transform Find(string name)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == name) return t;
            return null;
        }

        private static Vector3 Home(Transform t) => t != null ? t.localPosition : Vector3.zero;

        private void Write()
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, _report.ToString());
                Debug.Log("[상승] Wave B 런타임 측정 → " + ReportPath + "\n" + _report);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[상승] Wave B 런타임 보고서 쓰기 실패: " + e.Message);
            }
        }
    }
}
