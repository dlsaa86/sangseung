using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ascend.Prototype.Audio;
using Ascend.Prototype.Build;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Npc;
using Ascend.Prototype.Perf;
using Ascend.Prototype.Player;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.Telemetry;
using Ascend.Prototype.View;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// Pass 1 Wave B — Wave A가 만든 컴포넌트를 씬에 붙이고 데이터 에셋을 만든다.
    ///
    /// **멱등하다.** "없으면 만든다"가 아니라 "항상 이 상태로 만든다"이다 — 모든 값은
    /// 델타가 아니라 절대값으로 쓴다. 두 번 돌려도 같은 결과가 나와야 하고,
    /// <see cref="Report"/>가 그 사실을 숫자로 증명한다(두 번의 출력이 글자 단위로 같다).
    ///
    /// 데이터 에셋만 예외다. 이미 있으면 손대지 않는다 — 인스펙터에서 조정한 밸런스를
    /// 빌더가 조용히 되돌리면 그 조정은 다음 실행까지만 사는 값이 된다.
    ///
    /// 왜 두 개의 빈 트랜스폼(<c>CameraRig</c>, <c>CeilingLampRig</c>)을 끼워 넣는가:
    /// <see cref="RiskStateView"/>가 <c>Head</c>와 <c>CeilingLamp</c>의 localPosition을
    /// **매 LateUpdate마다 절대값으로** 다시 쓴다. <see cref="CollapseSequence"/>도 LateUpdate에서
    /// 같은 필드를 쓰므로, 같은 트랜스폼을 주면 실행 순서에 따라 한쪽 연출이 통째로 사라진다.
    /// 그 실패는 콘솔에 아무것도 남기지 않는다. 부모를 하나 끼우면 두 연출이 곱해진다.
    /// </summary>
    public static class Pass1WaveBWiring
    {
        public const string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
        public const string ProfileFolder = "Assets/Prototype_Elevator/Data/Profiles";

        /// <summary>사고 기록기의 벽면 위치(엘리베이터 로컬). 정면 벽(z=-1.5) 안쪽 면에 붙인다.</summary>
        private static readonly Vector3 PrinterLocalPosition = new Vector3(0.55f, 2.05f, -1.43f);

        /// <summary>
        /// 정면 벽은 방 안쪽(+z)을 향한다. TMP 글자와 Unity Quad는 둘 다 **로컬 -Z에서** 보인다
        /// (씬의 기존 라벨 전부가 <c>dot(forward, toPlayer) &lt; 0</c>이고 Quad 법선은 (0,0,-1)이다).
        /// 그래서 읽는 면이 +z를 보게 하려면 Y로 180° 돌려야 한다.
        /// </summary>
        private static readonly Vector3 PrinterLocalEuler = new Vector3(0f, 180f, 0f);

        private static readonly Vector3 PrinterBodyScale = new Vector3(0.34f, 0.22f, 0.14f);
        private static readonly Vector3 TapeOriginLocalPosition = new Vector3(0f, -0.13f, -0.075f);

        [MenuItem("Ascend/Wire Pass 1 Wave B")]
        public static void Run()
        {
            string report = RunToString();
            Debug.Log("[상승]\n" + report);
            WriteArtifact("pass1_waveb_wiring.txt", report);
        }

        public static string RunToString()
        {
            var log = new StringBuilder(8192);
            log.AppendLine("=== Pass 1 Wave B 배선 ===");

            if (EditorApplication.isPlaying)
                return "Play 모드다. 씬을 고치지 않는다.";

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                return $"활성 씬이 {scene.path} 다. {ScenePath} 를 먼저 연다.";

            EnsureProfileAssets(log);
            WireScene(log, scene);

            log.AppendLine();
            log.Append(Report());
            return log.ToString();
        }

        // ────────────────────────────────────────────────────────────────────
        // 1. 데이터 에셋
        // ────────────────────────────────────────────────────────────────────

        private static void EnsureProfileAssets(StringBuilder log)
        {
            log.AppendLine("[에셋]");
            EnsureFolder(ProfileFolder);

            EnsureAsset<TargetHardwareProfile>("TargetHardwareProfile", log, a => a.Reset());
            EnsureAsset<OverharvestProfile>("OverharvestProfile", log, a => a.Reset());
            EnsureAsset<DangerFeedbackProfile>("DangerFeedbackProfile", log,
                a => a.ApplyPreset(RiskIntensity.Standard));
            EnsureAsset<VisualQualityProfile>("VisualQualityProfile", log, a => a.Reset());
            EnsureAsset<AudioMixProfile>("AudioMixProfile", log, a => a.Reset());
            EnsureAsset<AccessibilityProfile>("AccessibilityProfile", log, a => a.Reset());
            EnsureAsset<RunSummaryTemplate>("RunSummaryTemplate", log, a => a.Reset());
            EnsureAsset<PassengerReactionSet>("PassengerReactionSet", log, a => a.Reset());

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 있으면 그대로 둔다. 없으면 만들고 기본값을 찍는다.
        /// <c>CreateInstance</c>가 <c>Reset</c>을 불러 주는지에 기대지 않는다 —
        /// 그 동작은 문서화된 계약이 아니라 관찰된 부수 효과다.
        /// </summary>
        private static T EnsureAsset<T>(string fileName, StringBuilder log, Action<T> initialise)
            where T : ScriptableObject
        {
            string path = ProfileFolder + "/" + fileName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                log.AppendLine($"  유지  {path}");
                return existing;
            }

            var made = ScriptableObject.CreateInstance<T>();
            initialise?.Invoke(made);
            AssetDatabase.CreateAsset(made, path);
            log.AppendLine($"  생성  {path}");
            return made;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ────────────────────────────────────────────────────────────────────
        // 2. 씬 배선
        // ────────────────────────────────────────────────────────────────────

        private static void WireScene(StringBuilder log, UnityEngine.SceneManagement.Scene scene)
        {
            log.AppendLine();
            log.AppendLine("[씬]");

            GameObject runRoot = Require("AscendRun");
            GameObject player = Require("Player");
            GameObject car = Require("GrayboxWorld/Car");

            var runSession = runRoot.GetComponent<RunSessionBehaviour>();
            var riskView = runRoot.GetComponent<RiskStateView>();
            var recorder = runRoot.GetComponent<AccidentRecorder>();
            var figures = UnityEngine.Object.FindAnyObjectByType<BuildFigureView>();
            var interactor = player.GetComponent<CrosshairInteractor>();
            var lever = UnityEngine.Object.FindAnyObjectByType<InteractableOverharvestLever>();

            // ── 흔들림이 겹치지 않도록 부모를 끼운다 ────────────────────────
            Transform cameraRig = EnsureRig(player.transform.Find("Head"), "CameraRig", FindCamera(player), log);
            Transform lampRig = EnsureRig(car.transform, "CeilingLampRig", FindDeep(car.transform, "CeilingLamp"), log);

            // ── 사고 기록기(월드) ────────────────────────────────────────────
            Transform printer = BuildAccidentPrinter(car.transform, log);
            Transform tapeOrigin = printer.Find("TapeOrigin");

            // ── AscendRun 위의 컴포넌트들 ────────────────────────────────────
            var telemetry = Ensure<TelemetryRecorderBehaviour>(runRoot, log);
            new Wirer(telemetry)
                .Obj("_run", runSession)
                .Bool("_writeFiles", true)
                .Str("_directoryOverride", string.Empty)
                .Apply();

            var audio = Ensure<AudioDirector>(runRoot, log);
            new Wirer(audio)
                .Obj("_run", runSession)
                .Float("_masterVolume", 0.8f)
                .Float("_machineVolume", 1f)
                .Float("_eventVolume", 0.85f)
                .Float("_passengerVolume", 0.9f)
                .Float("_warningVolume", 0.95f)
                .Float("_silenceSeconds", 0.5f)
                .Float("_duckSeconds", 0.12f)
                .Float("_resumeSeconds", 0.25f)
                .Bool("_duckGlobalListener", true)
                .Float("_maxLeadSeconds", 1.5f)
                .Bool("_logCues", false)
                .Bool("_prewarmClips", true)
                .Apply();

            var riskBridge = Ensure<RiskEventBridge>(runRoot, log);
            new Wirer(riskBridge)
                .Obj("_run", runSession)
                .Obj("_risk", riskView)
                .Apply();

            var approach = Ensure<OverharvestApproachBridge>(runRoot, log);
            new Wirer(approach)
                .Obj("_run", runSession)
                .Obj("_interactor", interactor)
                .Obj("_lever", lever)
                .Float("_dwellSeconds", 0.15f)
                .Apply();

            var collapse = Ensure<CollapseSequence>(runRoot, log);
            var dropTargets = new List<UnityEngine.Object>
            {
                lampRig,                                   // 매달린 천장등 — 가장 크게 읽힌다
                FindDeep(car.transform, "PowerTank"),      // 받침에서 튀는 전력 탱크
                FindDeep(car.transform, "FloorIndicator"), // 문 위 층 표시가 떨어진다
            };
            new Wirer(collapse)
                .Obj("_run", runSession)
                .Obj("_risk", riskView)
                .Array("_dropTargets", dropTargets)
                .Obj("_cameraRig", cameraRig)
                .Float("_blackoutSeconds", 0.55f)
                .Float("_dropSeconds", 0.9f)
                .Float("_dropDistance", 0.42f)
                .Float("_relightSeconds", 1.8f)
                .Int("_flickerSeed", 20260801)
                .Apply();

            var reactionView = Ensure<PassengerReactionView>(runRoot, log);
            new Wirer(reactionView)
                .Obj("_run", runSession)
                .Obj("_figures", figures)
                .Obj("_reactions", AssetDatabase.LoadAssetAtPath<PassengerReactionSet>(
                    ProfileFolder + "/PassengerReactionSet.asset"))
                .Int("_maxConcurrent", 2)
                .Apply();

            var memory = Ensure<MemoryTrendProbe>(runRoot, log);
            new Wirer(memory)
                .Obj("_run", runSession)
                .Bool("_writeOnRunEnded", true)
                .Apply();

            // 예산은 0으로 둔다 = "대조하지 않는다". 확정되지 않은 예산을 넣고 매번
            // 초과라고 외치는 것보다 값만 남기는 쪽이 정직하다(그 클래스의 주석과 같은 이유).
            var render = Ensure<RenderBudgetProbe>(runRoot, log);
            new Wirer(render)
                .Bool("_autoSample", true)
                .Int("_sampleEveryNFrames", 1)
                .Int("_warmupFrames", 60)
                .Float("_drawCallBudget", 0f)
                .Float("_setPassBudget", 0f)
                .Float("_triangleBudget", 0f)
                .Float("_frameMsBudget", 0f)
                .Bool("_writeOnDestroy", true)
                .Apply();

            // ── 종이 테이프 프린터 ───────────────────────────────────────────
            var tape = Ensure<PaperTapePrinterView>(printer.gameObject, log);
            new Wirer(tape)
                .Obj("_run", runSession)
                .Obj("_recorder", recorder)
                .Obj("_tapeOrigin", tapeOrigin)
                .Obj("_tape", null)          // 런타임에 만든다
                .Obj("_tapeText", null)      // 런타임에 만든다
                .Obj("_printHead", null)
                .Float("_secondsPerLine", 0.18f)
                .Float("_lineHeight", 0.035f)
                .Int("_maxLines", 24)
                .Float("_headTravel", 0.02f)
                .Apply();

            // ── 승객 시선 대상 ───────────────────────────────────────────────
            // _gazeCeiling 은 비운다. 코드가 대상 null 일 때만 "위를 본다"(-28°)로 폴백하고,
            // 대상을 주면 y를 0으로 눌러 **수평** 방향만 쓰므로 천장을 배선하면 오히려 못 본다.
            new Wirer(figures)
                .Obj("_gazeDevice", FindByName("Tube_1"))
                .Obj("_gazeOverharvestLever", FindDeep(lever.transform, "HandlePivot"))
                .Obj("_gazeDoor", FindByName("DoorSign"))
                .Obj("_gazeCeiling", null)
                .Apply();

            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// <paramref name="child"/>를 <paramref name="parent"/> 아래의 이름 있는 빈 트랜스폼에
        /// 넣는다. 리그 자체는 항상 항등이므로 자식의 월드 포즈는 변하지 않는다.
        /// </summary>
        private static Transform EnsureRig(Transform parent, string rigName, Transform child, StringBuilder log)
        {
            if (parent == null) throw new Exception($"{rigName} 의 부모가 없다");

            Transform rig = parent.Find(rigName);
            if (rig == null)
            {
                var go = new GameObject(rigName);
                go.transform.SetParent(parent, false);
                rig = go.transform;
                log.AppendLine($"  생성  {PathOf(rig)}");
            }
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.identity;
            rig.localScale = Vector3.one;

            if (child != null && child.parent != rig)
            {
                Vector3 before = child.position;
                child.SetParent(rig, true);   // 월드 포즈 보존
                log.AppendLine($"  이동  {child.name} → {PathOf(rig)}  (world {before:F3} → {child.position:F3})");
            }
            return rig;
        }

        private static Transform BuildAccidentPrinter(Transform car, StringBuilder log)
        {
            Transform printer = car.Find("AccidentPrinter");
            if (printer == null)
            {
                var go = new GameObject("AccidentPrinter");
                go.transform.SetParent(car, false);
                printer = go.transform;
                log.AppendLine("  생성  GrayboxWorld/Car/AccidentPrinter");
            }
            printer.localPosition = PrinterLocalPosition;
            printer.localRotation = Quaternion.Euler(PrinterLocalEuler);
            printer.localScale = Vector3.one;

            Transform body = printer.Find("AccidentPrinterBody");
            if (body == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "AccidentPrinterBody";
                cube.transform.SetParent(printer, false);
                body = cube.transform;
                log.AppendLine("  생성  .../AccidentPrinter/AccidentPrinterBody");
            }
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            body.localScale = PrinterBodyScale;

            // 콜라이더는 남긴다. `CrosshairInteractor.FindTarget` 은 IInteractable 이 없는
            // 히트에서 closestDistance 를 갱신하지 않으므로 조준을 가로채지 않고,
            // `HumanScaleLayout` 의 "보이는 메시에 콜라이더 있음" 검사는 통과한다.
            var renderer = body.GetComponent<MeshRenderer>();
            var machine = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Prototype_Elevator/Materials/Graybox/M_Gray_Console.mat");
            if (renderer != null && machine != null) renderer.sharedMaterial = machine;

            Transform tapeOrigin = printer.Find("TapeOrigin");
            if (tapeOrigin == null)
            {
                var go = new GameObject("TapeOrigin");
                go.transform.SetParent(printer, false);
                tapeOrigin = go.transform;
                log.AppendLine("  생성  .../AccidentPrinter/TapeOrigin");
            }
            tapeOrigin.localPosition = TapeOriginLocalPosition;
            tapeOrigin.localRotation = Quaternion.identity;
            tapeOrigin.localScale = Vector3.one;

            return printer;
        }

        // ────────────────────────────────────────────────────────────────────
        // 3. 읽어서 확인 — 눈이 아니라 숫자로
        // ────────────────────────────────────────────────────────────────────

        [MenuItem("Ascend/Verify Pass 1 Wave B")]
        public static void RunReport() => Debug.Log("[상승]\n" + Report());

        /// <summary>
        /// 배선이 **런타임에 실제로 움직이는가**를 잰다. 10층 오토파일럿은
        /// <c>IInteractable.Interact()</c> 만 부르므로 연출 경로를 지나가지 않는다 —
        /// 그 검증만으로는 "붙였다"와 "돈다"가 구분되지 않는다.
        /// </summary>
        [MenuItem("Ascend/Run PlayMode Wave B Probe")]
        public static void RunRuntimeProbe()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError($"[상승] 활성 씬이 {scene.path} 다. {ScenePath} 를 먼저 연다.");
                return;
            }
            if (scene.isDirty)
            {
                Debug.LogError("[상승] 씬이 저장되지 않았다. 저장한 뒤 다시 부른다.");
                return;
            }

            string reportPath = global::Ascend.Prototype.Run.Tests.WaveBRuntimeProbe.ReportPath;
            string path = Path.Combine(Directory.GetCurrentDirectory(), reportPath);
            if (File.Exists(path)) File.Delete(path);

            global::Ascend.Prototype.Run.Tests.WaveBRuntimeProbe.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] Wave B 런타임 측정 시작 → {reportPath}");
        }

        public static string Report()
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("=== Pass 1 Wave B 확인 ===");

            sb.AppendLine("[에셋]");
            ReportAsset<TargetHardwareProfile>(sb, "TargetHardwareProfile",
                a => $"목표 {a.TargetFps:0} FPS / 하한 {a.HardFloorFps:0} FPS / {a.ReferenceWidth}×{a.ReferenceHeight} / 승인 {a.Ratified}");
            ReportAsset<OverharvestProfile>(sb, "OverharvestProfile",
                a => $"앤티 {a.AnteRatio:0.00} 증가 {a.AnteEscalation:0.00} 해금 {a.UnlockThreshold:0.00} 정적 {a.MinSilenceSeconds:0.00}~{a.MaxSilenceSeconds:0.00}s 추가스핀 ≤{a.MaxExtraSpins}");
            ReportAsset<DangerFeedbackProfile>(sb, "DangerFeedbackProfile",
                a => $"프리셋 {a.SourcePreset}({a.PresetName}) / Stable 광량 {a.For(RiskLevel.Stable).LightIntensity:0.00} · Collapse 광량 {a.For(RiskLevel.Collapse).LightIntensity:0.00} · Collapse 셰이크 {a.For(RiskLevel.Collapse).CameraShake:0.000}");
            ReportAsset<VisualQualityProfile>(sb, "VisualQualityProfile", a => a.Snapshot().Describe());
            ReportAsset<AudioMixProfile>(sb, "AudioMixProfile",
                a => $"기계 {a.VolumeFor(AudioChannel.Machine):0.00}→덕 {a.DuckedVolumeFor(AudioChannel.Machine):0.00} / 승객 {a.VolumeFor(AudioChannel.Passenger):0.00}→덕 {a.DuckedVolumeFor(AudioChannel.Passenger):0.00}");
            ReportAsset<AccessibilityProfile>(sb, "AccessibilityProfile",
                a => $"셰이크 {a.CameraShakeScale:0.00} / 스웨이 {a.WorldSwayScale:0.00} / 깜빡임 {a.AllowFlicker} ≤{a.MaxFlickerHz:0}Hz / 사이렌 {a.AllowSiren} / 자막 {a.ShowSubtitles}");
            ReportAsset<RunSummaryTemplate>(sb, "RunSummaryTemplate",
                a => $"항목 {a.ComposeLines(new RunSummaryData(0, 0, 0f, "", "", "", "", "", 0)).Length}종 / 1행 '{a.Snapshot().LabelFor(RunSummaryField.HighestFloor)}'");
            ReportAsset<PassengerReactionSet>(sb, "PassengerReactionSet",
                a => $"항목 {a.Entries.Count}개 / CollapseImminent 자세 {a.For(PassengerReactionEvent.CollapseImminent).Pose} 우선순위 {a.For(PassengerReactionEvent.CollapseImminent).Priority}");

            sb.AppendLine();
            sb.AppendLine("[컴포넌트]");
            GameObject runRoot = GameObject.Find("AscendRun");
            if (runRoot == null) { sb.AppendLine("  AscendRun 이 없다"); return sb.ToString(); }

            ReportComponent<TelemetryRecorderBehaviour>(sb, runRoot);
            ReportComponent<AudioDirector>(sb, runRoot);
            ReportComponent<RiskEventBridge>(sb, runRoot);
            ReportComponent<OverharvestApproachBridge>(sb, runRoot);
            ReportComponent<CollapseSequence>(sb, runRoot);
            ReportComponent<PassengerReactionView>(sb, runRoot);
            ReportComponent<MemoryTrendProbe>(sb, runRoot);
            ReportComponent<RenderBudgetProbe>(sb, runRoot);

            var figures = UnityEngine.Object.FindAnyObjectByType<BuildFigureView>();
            if (figures != null) ReportRefs(sb, figures);

            var printerView = UnityEngine.Object.FindAnyObjectByType<PaperTapePrinterView>();
            if (printerView != null) ReportRefs(sb, printerView);

            sb.AppendLine();
            sb.AppendLine("[좌표]");
            ReportTransform(sb, FindByName("CameraRig"));
            ReportTransform(sb, FindByName("Main Camera"));
            ReportTransform(sb, FindByName("CeilingLampRig"));
            ReportTransform(sb, FindByName("CeilingLamp"));
            ReportTransform(sb, FindByName("AccidentPrinter"));
            ReportTransform(sb, FindByName("AccidentPrinterBody"));
            ReportTransform(sb, FindByName("TapeOrigin"));

            var bodyT = FindByName("AccidentPrinterBody");
            if (bodyT != null)
            {
                var r = bodyT.GetComponent<Renderer>();
                if (r != null)
                    sb.AppendLine($"  프린터 몸체 bounds center=({r.bounds.center.x:F3},{r.bounds.center.y:F3},{r.bounds.center.z:F3}) size=({r.bounds.size.x:F3},{r.bounds.size.y:F3},{r.bounds.size.z:F3}) 재질={((r.sharedMaterial != null) ? r.sharedMaterial.name : "<null>")} 콜라이더={(bodyT.GetComponent<Collider>() != null)}");
            }

            return sb.ToString();
        }

        private static void ReportAsset<T>(StringBuilder sb, string fileName, Func<T, string> describe)
            where T : ScriptableObject
        {
            string path = ProfileFolder + "/" + fileName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { sb.AppendLine($"  없음  {path}"); return; }
            sb.AppendLine($"  있음  {path}");
            sb.AppendLine($"        {describe(asset)}");
        }

        private static void ReportComponent<T>(StringBuilder sb, GameObject host) where T : Component
        {
            var c = host.GetComponent<T>();
            if (c == null) { sb.AppendLine($"  없음  {typeof(T).Name} on {host.name}"); return; }
            ReportRefs(sb, c);
        }

        private static void ReportRefs(StringBuilder sb, Component c)
        {
            sb.AppendLine($"  {c.GetType().Name} on {PathOf(c.transform)}");
            var so = new SerializedObject(c);
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.propertyPath == "m_Script") continue;
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var o = it.objectReferenceValue;
                    sb.AppendLine($"      {it.propertyPath} = {(o == null ? "<null>" : PathOfObject(o))}");
                }
                else if (it.isArray && it.propertyType != SerializedPropertyType.String)
                {
                    sb.AppendLine($"      {it.propertyPath}[] size={it.arraySize}");
                    for (int i = 0; i < it.arraySize; i++)
                    {
                        var e = it.GetArrayElementAtIndex(i);
                        if (e.propertyType == SerializedPropertyType.ObjectReference)
                            sb.AppendLine($"        [{i}] = {(e.objectReferenceValue == null ? "<null>" : PathOfObject(e.objectReferenceValue))}");
                    }
                }
            }
        }

        private static void ReportTransform(StringBuilder sb, Transform t)
        {
            if (t == null) { sb.AppendLine("  <없음>"); return; }
            sb.AppendLine(string.Format(
                "  {0,-46} lp=({1:F3},{2:F3},{3:F3}) leul=({4:F1},{5:F1},{6:F1}) ls=({7:F3},{8:F3},{9:F3}) wp=({10:F3},{11:F3},{12:F3})",
                PathOf(t),
                t.localPosition.x, t.localPosition.y, t.localPosition.z,
                t.localEulerAngles.x, t.localEulerAngles.y, t.localEulerAngles.z,
                t.localScale.x, t.localScale.y, t.localScale.z,
                t.position.x, t.position.y, t.position.z));
        }

        // ────────────────────────────────────────────────────────────────────
        // 부품
        // ────────────────────────────────────────────────────────────────────

        /// <summary>여러 필드를 한 SerializedObject 로 모아 쓴다. 필드 이름이 틀리면 던진다 —
        /// 조용히 넘어가면 "배선했다"고 믿은 채 아무것도 안 붙은 씬이 남는다.</summary>
        private sealed class Wirer
        {
            private readonly SerializedObject _so;
            private readonly string _owner;

            public Wirer(Component c)
            {
                if (c == null) throw new Exception("Wirer 에 null 컴포넌트가 왔다");
                _so = new SerializedObject(c);
                _owner = c.GetType().Name;
            }

            private SerializedProperty P(string name)
            {
                var p = _so.FindProperty(name);
                if (p == null) throw new Exception($"{_owner} 에 직렬화 필드 '{name}' 이 없다");
                return p;
            }

            public Wirer Obj(string n, UnityEngine.Object v) { P(n).objectReferenceValue = v; return this; }
            public Wirer Bool(string n, bool v) { P(n).boolValue = v; return this; }
            public Wirer Float(string n, float v) { P(n).floatValue = v; return this; }
            public Wirer Int(string n, int v) { P(n).intValue = v; return this; }
            public Wirer Str(string n, string v) { P(n).stringValue = v; return this; }

            public Wirer Array(string n, IReadOnlyList<UnityEngine.Object> values)
            {
                var p = P(n);
                p.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                return this;
            }

            public void Apply() => _so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Ensure<T>(GameObject go, StringBuilder log) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) return c;
            c = go.AddComponent<T>();
            log.AppendLine($"  추가  {typeof(T).Name} → {go.name}");
            return c;
        }

        private static GameObject Require(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) throw new Exception($"씬에 '{path}' 가 없다");
            return go;
        }

        private static Transform FindCamera(GameObject player)
        {
            var cam = player.GetComponentInChildren<Camera>(true);
            return cam != null ? cam.transform : null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static Transform FindByName(string name)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == name) return t;
            return null;
        }

        private static string PathOf(Transform t)
        {
            if (t == null) return "<null>";
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private static string PathOfObject(UnityEngine.Object o)
        {
            if (o is Component c) return PathOf(c.transform) + " (" + o.GetType().Name + ")";
            if (o is GameObject g) return PathOf(g.transform);
            string assetPath = AssetDatabase.GetAssetPath(o);
            return string.IsNullOrEmpty(assetPath) ? o.name : assetPath;
        }

        private static void WriteArtifact(string fileName, string body)
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, fileName), body + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[상승] {fileName} 을 쓰지 못했다: {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
