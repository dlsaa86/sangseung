using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// 기계 벽의 「동그란 빛」 원인을 **플레이 모드에서** 가른다.
    ///
    /// ## 왜 플레이 모드여야만 하는가
    ///
    /// <c>RiskStateView.LateUpdate()</c> 가 런타임에 <c>RenderSettings.ambientLight</c> 를
    /// 덮어쓴다. 에디트 모드는 그 코드가 안 돌아 씬에 직렬화된 캄캄한 값이 그대로 보이고,
    /// 그 어둠에서는 원이 애초에 안 보인다 — **무엇을 바꾸든 「사라졌다」고 나온다.**
    /// 지난 네 세션의 오진이 전부 이 한 가지 원인이었다(커밋 <c>973eadb</c>).
    ///
    /// 그래서 이 도구는 <see cref="AscendAD47Shots"/> 의 「machine」 시점과 휘도식을
    /// 그대로 쓰되 **플레이 모드 안에서** 렌더한다. 두 도구의 숫자가 비교 가능해야
    /// 「에디트 0.1028」 같은 과거 기준선과 대조할 수 있기 때문이다.
    ///
    /// ## 무엇을 가르는가
    ///
    /// 원을 만드는 후보는 둘이고 둘 다 같은 그림을 낸다.
    ///   (가) 계단 스페큘러 — 큰 평판에서 <c>dot(N,h)</c> 가 천천히 변해 하이라이트가
    ///        넓게 퍼지고 <c>ceil(t*steps)/steps</c> 가 그 영역을 통째로 한 칸으로 점프시킨다.
    ///   (나) 점광 확산 웅덩이 — 역제곱 감쇠가 평판에 원형 웅덩이를 그린다.
    /// 광원을 끄면 둘 다 사라지므로 광원 실험으로는 못 가른다.
    /// **광원은 켠 채 스페큘러만 끄는 실험(C3)이 유일한 판별자다.**
    /// </summary>
    [InitializeOnLoad]
    internal static class CircleLightPlayDiag
    {
        private const string SpecKeyword = "_SPECULAR_ON";
        private const string SpecToggleProp = "_SpecularEnabled";

        // AscendAD47Shots 의 "machine" 샷과 **같은 값이어야 한다.** 다르면 과거 수치와
        // 대조가 안 된다.
        private static readonly Vector3 Eye  = new Vector3( 0.00f, 1.62f, -1.55f);
        private static readonly Vector3 Look = new Vector3(-0.30f, 1.10f,  2.10f);
        private const float Fov = 70f;

        // 사이렌 케이지(0.71, 2.21, 2.81)와 레버 베이(0.71, 1.26, 2.87)를 한 화면에 담는
        // 근접 시점. 「기계 벽 전체」 시점에서는 이 둘이 작게 잡혀 원이 있어도 묻힌다.
        private static readonly Vector3 LeverEye  = new Vector3(0.71f, 1.62f, 1.20f);
        private static readonly Vector3 LeverLook = new Vector3(0.71f, 1.75f, 2.85f);
        private const int W = 1280;
        private const int H = 720;

        // 자동 검출 창 크기. 원은 화면의 수십 %를 덮으므로 이 정도가 적당하다.
        private const int HotWin = 96;

        /// <summary>
        /// **고정 ROI.** 자동 검출은 매 실행마다 다른 자리를 고를 수 있어 수정 전후를
        /// 직접 비교할 수 없다. 2026-08-09 첫 진단이 |C0-C3| 최대로 고른 자리를 못 박아
        /// 두고, 이후 모든 실행이 같은 사각형을 잰다. (아래는 좌하단 기준 좌표다 —
        /// `GetRawTextureData` 도 `WorldToViewportPoint` 도 y 가 아래에서 위다.)
        /// </summary>
        private static readonly RectInt PinnedRoi = new RectInt(632, 448, HotWin, HotWin);

        // ── 무장은 **파일**로 한다 ────────────────────────────────────────────
        // `SessionState` 로 했더니 플레이 모드 진입 뒤 아무 일도 일어나지 않았고,
        // 상태가 밖에서 안 보여 원인을 못 갈랐다. 파일이면 읽을 수 있다.
        private static string OutDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "Captures", "circlediag");
        private static string ArmPath => Path.Combine(OutDir, "ARM");

        /// <summary>무엇이 실행됐는지 파일로 남긴다. 콘솔은 도메인 리로드에 지워진다.</summary>
        private static void Trace(string msg)
        {
            try
            {
                Directory.CreateDirectory(OutDir);
                File.AppendAllText(Path.Combine(OutDir, "trace.txt"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\n");
            }
            catch { /* 추적이 본작업을 막으면 안 된다 */ }
        }

        static CircleLightPlayDiag()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Trace("ctor  armed=" + File.Exists(ArmPath)
                  + " willPlay=" + EditorApplication.isPlayingOrWillChangePlaymode);
        }

        [MenuItem("Ascend/Diag — 동그란 빛 (플레이 모드)")]
        private static void Begin()
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(ArmPath, DateTime.Now.ToString("O"));
            Trace("MenuItem Begin → EnterPlaymode");
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// 플레이 모드 진입은 도메인 리로드의 일부라 창 포커스와 무관하게 불린다.
        /// `EditorApplication.update` 가 안 도는 상황에서도 여기까지는 온다.
        /// </summary>
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            Trace("playModeStateChanged " + change);
            if (change != PlayModeStateChange.EnteredPlayMode) return;

            // 에디터가 포커스를 잃으면 플레이 루프가 멈춘다(`runInBackground=false`).
            // 자동화 중에는 포커스가 없는 것이 정상이므로 런타임에 켠다. 플레이 모드를
            // 나가면 프로젝트 설정으로 되돌아가므로 직렬화를 건드리지 않는다.
            Application.runInBackground = true;

            if (!File.Exists(ArmPath)) { Trace("  무장 안 됨 — 아무것도 하지 않는다"); return; }

            // **MonoBehaviour 로 구동한다.** 이 세션에서 `EditorApplication.update` 는
            // 플레이 모드 중 내 델리게이트를 부르지 않았다. `Update()` 는 플레이어 루프가
            // 돌리므로 게임이 도는 한 반드시 불린다 — 실제로 게임 로직은 돌고 있었다.
            var go = new GameObject("__CircleDiagDriver") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<Driver>();
            Trace("  driver 생성");
        }

        // ── 판정된 수정 ───────────────────────────────────────────────────────
        //
        // 플레이 모드 실측(`report.txt`)이 메커니즘을 갈랐다 — 광원은 켠 채 스페큘러만
        // 끄면 원이 사라진다. 자동 검출 ROI 기준:
        //
        //     C0 기준선            0.2917
        //     C1 CabBulb 끔        0.0850   ← 빛 없는 바닥
        //     C3 스페큘러 전부 끔    0.1118
        //
        // 빛이 만든 신호 0.2067 중 **0.1799(87%)가 스페큘러**다. 확산 웅덩이는 13%.
        // 즉 (가) 계단 스페큘러이고, 세기·문턱을 만지는 완화는 이미 두 번 실패했다.
        //
        // 문짝에서 실제로 통했던 답과 같은 것을 적용한다 —
        // **큰 평면 판재를 `_SPECULAR_ON` 에서 제외한다.**
        // 「계단 스페큘러는 법선이 빨리 변하는 곳(볼트·레일·링)에만 켠다.」
        //
        // ⚠ **1차 목록은 불완전했다.** 기여도를 「고정 ROI 한 곳」에서만 재는 바람에
        // 다른 자리에 원을 만드는 판재가 전부 0.0000 으로 나왔다. 사용자가 「통관은
        // 됐는데 테두리·사이렌 쪽은 그대로」라고 지적해 드러났고, 측정을 **화면 어디든
        // 최대 변화**로 바꾸자 즉시 잡혔다:
        //
        //     BK_SM_Cab_MachineFrame  최대변화 0.0258 @ (688,536)  고정ROI -0.0000
        //     BK_SM_Siren_Cage        최대변화 0.0047 @ (728,488)  고정ROI -0.0000
        //
        // 교훈: **한 곳만 재는 지표는 그 곳 밖의 결함을 무죄로 만든다.**
        private static readonly string[] FlatPlates =
        {
            "BK_SM_ChamberArray",     // 최대변화 -0.1244  3×3 칸 판. 첫 원의 69%
            "BK_SM_Cab_PanelRecess",  // -0.0200
            "BK_SM_Harness_Frame",    // -0.0179
            "BK_SM_Harness_Fill",     // -0.0171
            // 2차 — 2.44×2.73×**0.03m** 평판. 진행 문서의 미해결 「창백한 띠」가 이것이다.
            "BK_SM_Cab_MachineFrame", // -0.0258
            // 3차 — **근접 시점에서만 드러났다.** 기계 벽 전체 시점에서는 대상이 작게
            // 잡혀 96×96 창이 주변 어둠과 평균내 버려 0.0000·0.0047 로 나왔다.
            // 사이렌/레버를 정면에서 보면 둘 다 통관과 같은 급이다.
            "BK_SM_LeverBay",         // 근접 0.1571 (광각 0.0000)
            // 이름은 「케이지」지만 실제로 원을 만드는 면은 사이렌 뒤 **평평한 배면판**이다.
            // 이름이 아니라 측정이 판정한다.
            "BK_SM_Siren_Cage",       // 근접 0.1126 (광각 0.0047)
            // 레버 손잡이 2장(0.0091 / 0.0056)은 **남긴다** — 곡면이라 법선이 빨리 변하고,
            // 규칙이 하이라이트를 켜 두라고 한 바로 그 부류다.
        };

        /// <summary>
        /// 평판 4장을 스페큘러에서 제외한다. **멱등이다** — 델타가 아니라 목표 상태를
        /// 절대값으로 쓰므로 몇 번을 돌려도 결과가 같다.
        /// </summary>
        [MenuItem("Ascend/Diag — 평판 스페큘러 제외 적용")]
        private static void ApplyFlatPlateExclusion()
        {
            Directory.CreateDirectory(OutDir);
            var sb = new StringBuilder();
            sb.AppendLine("=== 평판 스페큘러 제외 " + DateTime.Now.ToString("HH:mm:ss") + " ===");

            // 되돌릴 수 있게 첫 실행 때의 원래 값을 남긴다.
            string backup = Path.Combine(OutDir, "specular_backup.txt");
            bool writeBackup = !File.Exists(backup);
            var bk = new StringBuilder();

            foreach (string name in FlatPlates)
            {
                Material mat = LoadMaterial(name);
                if (mat == null) { sb.AppendLine("  MISSING " + name); continue; }

                bool beforeKw = mat.IsKeywordEnabled(SpecKeyword);
                float beforeProp = mat.HasProperty(SpecToggleProp) ? mat.GetFloat(SpecToggleProp) : -1f;
                if (writeBackup)
                    bk.AppendLine(name + "\t" + beforeKw + "\t" + beforeProp.ToString("F3"));

                // 목표 상태를 **절대값으로** 쓴다.
                // 키워드만 끄면 직렬화에서 되살아난다 — 토글 프로퍼티를 같이 내려야 한다.
                mat.DisableKeyword(SpecKeyword);
                if (mat.HasProperty(SpecToggleProp)) mat.SetFloat(SpecToggleProp, 0f);

                EditorUtility.SetDirty(mat);
                // `SaveAssets()` 는 폰트 아틀라스를 1×1 → 1024×1024 로 부풀린다. 대상만 저장한다.
                AssetDatabase.SaveAssetIfDirty(mat);

                sb.AppendLine(string.Format("  {0,-24} 키워드 {1} → {2}   {3} {4:F2} → {5:F2}",
                    name, beforeKw, mat.IsKeywordEnabled(SpecKeyword), SpecToggleProp,
                    beforeProp, mat.HasProperty(SpecToggleProp) ? mat.GetFloat(SpecToggleProp) : -1f));
            }

            if (writeBackup && bk.Length > 0) File.WriteAllText(backup, bk.ToString());
            File.AppendAllText(Path.Combine(OutDir, "apply.txt"), sb.ToString());
            Debug.Log("[상승] " + sb);
        }

        private static Material LoadMaterial(string name)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material " + name);
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) != name) continue;
                return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            return null;
        }

        /// <summary>
        /// 에디트 모드에서 같은 시점·같은 휘도식으로 평균 밝기만 잰다.
        /// **원의 유무 판정에는 쓰지 않는다** — 에디트 모드 앰비언트는 실측 0.019 로
        /// 플레이 모드(0.487)의 1/26 이라 원이 애초에 안 보인다. 오직 과거 기준선
        /// 0.1028 과 대조하기 위한 회귀 감시선이다.
        /// </summary>
        [MenuItem("Ascend/Diag — 눈높이 평균휘도 (에디트)")]
        private static void MeasureEditModeBrightness()
        {
            Directory.CreateDirectory(OutDir);
            var camGo = new GameObject("__BrightCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = Fov;
            cam.enabled = false;
            camGo.transform.SetPositionAndRotation(Eye,
                Quaternion.LookRotation((Look - Eye).normalized, Vector3.up));

            byte[] px = Shoot(cam, Path.Combine(OutDir, "editmode_machine.png"));
            Stat s = Measure(px, MachineRoi(cam), PinnedRoi);
            UnityEngine.Object.DestroyImmediate(camGo);

            string line = string.Format("{0}  에디트 machine 평균휘도={1:F4}  기계ROI={2:F4}  고정ROI={3:F4}  검정={4:F1}%  날림={5:F1}%",
                DateTime.Now.ToString("HH:mm:ss"), s.Full, s.Machine, s.Pin, s.BlackPct, s.BlownPct);
            File.AppendAllText(Path.Combine(OutDir, "editmode_brightness.txt"), line + "\n");
            Debug.Log("[상승] " + line);
        }

        /// <summary>
        /// 에디터 상태를 파일로 떨군다. MCP 가 막혔을 때 밖에서 상태를 확인할 유일한 길이다.
        /// **씬이 더러운 채로 두면** 다음 `OpenScene` 에서 모달이 떠 메인 스레드를 잡는다.
        /// </summary>
        [MenuItem("Ascend/Diag — 에디터 상태 덤프")]
        private static void DumpEditorState()
        {
            Directory.CreateDirectory(OutDir);
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string line = string.Format(
                "{0}  isPlaying={1}  scene={2}  sceneDirty={3}  compiling={4}  updating={5}",
                DateTime.Now.ToString("HH:mm:ss"), Application.isPlaying, s.name, s.isDirty,
                EditorApplication.isCompiling, EditorApplication.isUpdating);
            File.AppendAllText(Path.Combine(OutDir, "editor_state.txt"), line + "\n");
            Debug.Log("[상승] " + line);
        }

        /// <summary>플레이 루프에 올라타는 구동부. 씬이 안정된 뒤 한 번만 돈다.</summary>
        private sealed class Driver : MonoBehaviour
        {
            private int _frames;

            private void Update()
            {
                _frames++;
                if (_frames == 1) Trace("Driver.Update 첫 프레임 frameCount=" + Time.frameCount);
                if (_frames < 60) return;

                enabled = false;
                try
                {
                    Trace("RunSweep 시작 frameCount=" + Time.frameCount);
                    RunSweep();
                    Trace("RunSweep 완료");
                }
                catch (Exception e)
                {
                    Trace("RunSweep 예외: " + e);
                    Debug.LogError("[상승] 동그란 빛 진단 실패: " + e);
                }
                finally
                {
                    try { if (File.Exists(ArmPath)) File.Delete(ArmPath); } catch { }
                    EditorApplication.ExitPlaymode();
                }
            }
        }

        // ── 한 장의 측정 결과 ────────────────────────────────────────────────
        private struct Stat
        {
            public float Full;      // 전체 평균 휘도
            public float Machine;   // 기계 프레임 ROI 평균
            public float Hot;       // 자동 검출 ROI 평균
            public float Pin;       // 고정 ROI 평균 (수정 전후 비교용)
            public float BlackPct;
            public float BlownPct;
        }

        private sealed class MatState
        {
            public Material Mat;
            public bool HadKeyword;
            public bool HadToggleProp;
            public float ToggleValue;
        }

        private static void RunSweep()
        {
            string outDir = OutDir;
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            log.AppendLine("=== 동그란 빛 진단 (플레이 모드) ===");
            log.AppendLine("isPlaying=" + Application.isPlaying + "  frame=" + Time.frameCount);

            // ── 0. 런타임 사실 확인 ──────────────────────────────────────────
            Light bulb  = FindLightByPath("CabinAD47/LT_CabBulb");
            Light spill = FindLightByPath("CabinAD47/SOCKET_ElevPanel/LT_SoulSpill");
            Light refLamp = FindLightByPath("ReferenceRoom/CeilingLamp/CabinLight");

            log.AppendLine("-- 런타임 광원 --");
            log.AppendLine(Describe("LT_CabBulb", bulb));
            log.AppendLine(Describe("LT_SoulSpill", spill));
            log.AppendLine(Describe("ReferenceRoom/CeilingLamp/CabinLight (_cabinLight 배선 대상)", refLamp));
            Color amb = RenderSettings.ambientLight;
            log.AppendLine(string.Format("ambientLight={0}  실효휘도={1:F5}  mode={2}",
                amb.ToString("F5"), Lum(amb), RenderSettings.ambientMode));

            if (bulb == null) { Debug.LogError("[상승] LT_CabBulb 없음"); return; }

            // 움직임을 멈춘다. 조건 사이에 씬이 변하면 비교가 무의미해진다.
            // Update/LateUpdate 는 timeScale 0 에서도 계속 돌므로 앰비언트 덮어쓰기는 유지된다.
            float prevScale = Time.timeScale;
            Time.timeScale = 0f;

            var camGo = new GameObject("__CircleDiagCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = Fov;
            cam.enabled = false; // 게임 뷰에 끼어들지 않는다. Render() 로만 쓴다.
            camGo.transform.SetPositionAndRotation(Eye, Quaternion.LookRotation((Look - Eye).normalized, Vector3.up));

            List<MatState> specMats = CollectSpecMaterials();
            log.AppendLine("-- 스페큘러 켜진 머티리얼 " + specMats.Count + "장 --");

            RectInt roiMachine = MachineRoi(cam);
            log.AppendLine(string.Format("기계 프레임 ROI = x{0} y{1} w{2} h{3}",
                roiMachine.x, roiMachine.y, roiMachine.width, roiMachine.height));

            try
            {
                // ── 1. 고해상 4조건 ──────────────────────────────────────────
                byte[] c0 = Shoot(cam, Path.Combine(outDir, "C0_baseline.png"));

                bulb.enabled = false;
                byte[] c1 = Shoot(cam, Path.Combine(outDir, "C1_cabbulb_off.png"));
                bulb.enabled = true;

                bool spillWas = spill != null && spill.enabled;
                if (spill != null) spill.enabled = false;
                byte[] c2 = Shoot(cam, Path.Combine(outDir, "C2_soulspill_off.png"));
                if (spill != null) spill.enabled = spillWas;

                SetSpecAll(specMats, false);
                byte[] c3 = Shoot(cam, Path.Combine(outDir, "C3_spec_all_off.png"));
                SetSpecAll(specMats, true);

                // 원이 있는 자리 = 스페큘러를 껐을 때 가장 많이 변한 자리.
                // 관측자가 눈으로 고른 사각형이 아니라 **데이터가 고른** 사각형이다.
                RectInt roiHot = MaxDiffWindow(c0, c3, out float _);
                log.AppendLine(string.Format("자동 검출 ROI(|C0-C3| 최대) = x{0} y{1} w{2} h{3}",
                    roiHot.x, roiHot.y, roiHot.width, roiHot.height));

                log.AppendLine();
                log.AppendLine("-- 조건별 휘도표 (휘도 = 0.2126R+0.7152G+0.0722B, sRGB 0~1) --");
                log.AppendLine("조건                     전체평균   기계ROI   자동ROI   고정ROI   검정%   날림%");
                Row(log, "C0 기준선",            Measure(c0, roiMachine, roiHot));
                Row(log, "C1 CabBulb 끔",        Measure(c1, roiMachine, roiHot));
                Row(log, "C2 SoulSpill 끔",      Measure(c2, roiMachine, roiHot));
                Row(log, "C3 스페큘러 전부 끔",   Measure(c3, roiMachine, roiHot));

                // ── 2. 머티리얼별 기여 ───────────────────────────────────────
                Stat baseStat = Measure(c0, roiMachine, roiHot);
                log.AppendLine();
                log.AppendLine("-- 머티리얼 하나씩 끄기 (기준선 대비 고정ROI 하락폭 순) --");

                var contrib = new List<KeyValuePair<string, float>>();
                var where = new Dictionary<string, RectInt>();
                var contribPin = new Dictionary<string, float>();
                foreach (var ms in specMats)
                {
                    SetSpec(ms, false);
                    byte[] f = Shoot(cam, null);
                    SetSpec(ms, true);
                    Stat s = Measure(f, roiMachine, roiHot);

                    // **화면 어디든 최대 변화**로 잰다. 한 ROI 만 보면 다른 자리의 원을 놓친다.
                    RectInt r = MaxDiffWindow(c0, f, out float peak);
                    contrib.Add(new KeyValuePair<string, float>(ms.Mat.name, peak));
                    where[ms.Mat.name] = r;
                    contribPin[ms.Mat.name] = baseStat.Pin - s.Pin;
                }
                contrib.Sort((a, b) => b.Value.CompareTo(a.Value));
                int silent = 0;
                foreach (var kv in contrib)
                {
                    // 임계값을 낮게 둔다. 「임계 미만」으로 뭉뚱그리면 어떤 판재가
                    // 얼마나 남았는지 사용자와 대조할 수 없다 — 그게 1차 목록이
                    // 테두리를 놓친 방식이다.
                    if (kv.Value < 0.0003f) { silent++; continue; }
                    RectInt r = where[kv.Key];
                    log.AppendLine(string.Format(
                        "  {0,-34} 최대변화 {1:F4} @ (x{2},y{3})   고정ROI -{4:F4}",
                        kv.Key, kv.Value, r.x + HotWin / 2, r.y + HotWin / 2, contribPin[kv.Key]));
                }
                log.AppendLine("  (최대변화 < 0.0003 인 머티리얼 " + silent + "장은 생략)");

                // ── 3. 사이렌/레버 근접 시점 ─────────────────────────────────
                // **한 시점에서만 재면 그 시점에 안 보이는 결함이 무죄가 된다.**
                // 테두리(`MachineFrame`)를 놓친 것이 정확히 그 실패였다. 사용자가
                // 지목한 영역을 정면에서 다시 잰다.
                camGo.transform.SetPositionAndRotation(LeverEye,
                    Quaternion.LookRotation((LeverLook - LeverEye).normalized, Vector3.up));

                byte[] l0 = Shoot(cam, Path.Combine(outDir, "L0_lever_baseline.png"));
                SetSpecAll(specMats, false);
                byte[] l3 = Shoot(cam, Path.Combine(outDir, "L3_lever_spec_off.png"));
                SetSpecAll(specMats, true);

                MaxDiffWindow(l0, l3, out float leverPeak);
                log.AppendLine();
                log.AppendLine("-- 사이렌/레버 근접 시점 eye=" + LeverEye.ToString("F2") + " --");
                log.AppendLine(string.Format("  전체 스페큘러 기여 최대변화 = {0:F4}", leverPeak));

                var lever = new List<KeyValuePair<string, float>>();
                var leverWhere = new Dictionary<string, RectInt>();
                foreach (var ms in specMats)
                {
                    SetSpec(ms, false);
                    byte[] f = Shoot(cam, null);
                    SetSpec(ms, true);
                    RectInt r = MaxDiffWindow(l0, f, out float peak);
                    lever.Add(new KeyValuePair<string, float>(ms.Mat.name, peak));
                    leverWhere[ms.Mat.name] = r;
                }
                lever.Sort((a, b) => b.Value.CompareTo(a.Value));
                int leverSilent = 0;
                foreach (var kv in lever)
                {
                    if (kv.Value < 0.0003f) { leverSilent++; continue; }
                    RectInt r = leverWhere[kv.Key];
                    log.AppendLine(string.Format("  {0,-34} 최대변화 {1:F4} @ (x{2},y{3})",
                        kv.Key, kv.Value, r.x + HotWin / 2, r.y + HotWin / 2));
                }
                log.AppendLine("  (최대변화 < 0.0003 인 머티리얼 " + leverSilent + "장은 생략)");
            }
            finally
            {
                // 무엇이 실패했든 원래 상태로 되돌린다.
                SetSpecAll(specMats, true);
                if (bulb != null) bulb.enabled = true;
                Time.timeScale = prevScale;
                UnityEngine.Object.DestroyImmediate(camGo);
            }

            string reportPath = Path.Combine(outDir, "report.txt");
            File.WriteAllText(reportPath, log.ToString());
            Debug.Log("[상승] 진단 완료 → " + reportPath + "\n" + log);
        }

        // ── 렌더 ─────────────────────────────────────────────────────────────
        /// <summary>카메라를 RT 로 렌더해 sRGB 바이트를 돌려준다. path 가 있으면 PNG 도 남긴다.</summary>
        private static byte[] Shoot(Camera cam, string path)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(path, tex.EncodeToPNG());
            byte[] raw = tex.GetRawTextureData();

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return raw;
        }

        private static float LumAt(byte[] px, int i)
        {
            int o = i * 3;
            return (px[o] * 0.2126f + px[o + 1] * 0.7152f + px[o + 2] * 0.0722f) / 255f;
        }

        private static Stat Measure(byte[] px, RectInt machine, RectInt hot)
        {
            var s = new Stat();
            double full = 0; int black = 0, blown = 0;
            int n = W * H;
            for (int i = 0; i < n; i++)
            {
                float l = LumAt(px, i);
                full += l;
                if (l < 0.02f) black++;
                if (l > 0.95f) blown++;
            }
            s.Full = (float)(full / n);
            s.BlackPct = black * 100f / n;
            s.BlownPct = blown * 100f / n;
            s.Machine = RoiMean(px, machine);
            s.Hot = RoiMean(px, hot);
            s.Pin = RoiMean(px, PinnedRoi);
            return s;
        }

        private static float RoiMean(byte[] px, RectInt r)
        {
            if (r.width <= 0 || r.height <= 0) return 0f;
            double sum = 0; int c = 0;
            for (int y = r.y; y < r.y + r.height; y++)
            {
                if (y < 0 || y >= H) continue;
                for (int x = r.x; x < r.x + r.width; x++)
                {
                    if (x < 0 || x >= W) continue;
                    sum += LumAt(px, y * W + x); c++;
                }
            }
            return c == 0 ? 0f : (float)(sum / c);
        }

        /// <summary>
        /// 두 그림의 휘도차가 가장 큰 HotWin×HotWin 창을 적분영상으로 찾는다.
        ///
        /// ⚠ **이 함수가 있어야 하는 이유** — 처음엔 머티리얼별 기여를 「고정 ROI 한 곳」에서만
        /// 쟀다. 그러면 **다른 자리에 원을 만드는 머티리얼이 전부 0 으로 나온다.** 실제로
        /// 그래서 통관(ChamberArray)만 잡고 테두리·사이렌/레버 쪽을 무죄로 흘려보냈다.
        /// 기여도는 반드시 **화면 어디든 최대 변화**로 재야 한다.
        /// </summary>
        private static RectInt MaxDiffWindow(byte[] a, byte[] b, out float meanDiff)
        {
            var sat = new double[(W + 1) * (H + 1)];
            for (int y = 0; y < H; y++)
            {
                double rowSum = 0;
                for (int x = 0; x < W; x++)
                {
                    rowSum += Mathf.Abs(LumAt(a, y * W + x) - LumAt(b, y * W + x));
                    sat[(y + 1) * (W + 1) + (x + 1)] = sat[y * (W + 1) + (x + 1)] + rowSum;
                }
            }

            double best = -1; int bx = 0, by = 0;
            for (int y = 0; y + HotWin <= H; y += 8)
            {
                for (int x = 0; x + HotWin <= W; x += 8)
                {
                    double v = sat[(y + HotWin) * (W + 1) + (x + HotWin)]
                             - sat[y * (W + 1) + (x + HotWin)]
                             - sat[(y + HotWin) * (W + 1) + x]
                             + sat[y * (W + 1) + x];
                    if (v > best) { best = v; bx = x; by = y; }
                }
            }
            meanDiff = best <= 0 ? 0f : (float)(best / (HotWin * HotWin));
            return new RectInt(bx, by, HotWin, HotWin);
        }

        /// <summary>SM_Cab_MachineFrame 의 바운즈를 화면에 투영한 사각형.</summary>
        private static RectInt MachineRoi(Camera cam)
        {
            var rends = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Bounds? b = null;
            foreach (var r in rends)
                if (r.name == "SM_Cab_MachineFrame") { b = r.bounds; break; }
            if (b == null) return new RectInt(0, 0, 0, 0);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            Bounds bb = b.Value;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(
                    (i & 1) == 0 ? bb.min.x : bb.max.x,
                    (i & 2) == 0 ? bb.min.y : bb.max.y,
                    (i & 4) == 0 ? bb.min.z : bb.max.z);
                Vector3 sp = cam.WorldToViewportPoint(c);
                if (sp.z <= 0) continue;
                minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
            }
            if (minX > maxX) return new RectInt(0, 0, 0, 0);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(minX * W), 0, W - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(maxX * W), 0, W);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(minY * H), 0, H - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(maxY * H), 0, H);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        // ── 머티리얼 ─────────────────────────────────────────────────────────
        private static List<MatState> CollectSpecMaterials()
        {
            // `GetInstanceID()` 는 이 Unity 버전에서 obsolete-as-error 다. 머티리얼
            // 참조 자체로 중복을 거른다 — 같은 에셋은 같은 인스턴스라 그대로 동작한다.
            var seen = new HashSet<Material>();
            var list = new List<MatState>();
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var r in rends)
            {
                // 플레이 모드에서 어떤 스크립트가 `renderer.material` 을 만졌다면
                // 실제 렌더에 쓰이는 것은 인스턴스다. `sharedMaterials` 는 그 인스턴스를
                // 돌려주므로 **실제로 그려지는 머티리얼**을 잡는다.
                var mats = r.sharedMaterials;
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    if (!m.IsKeywordEnabled(SpecKeyword)) continue;
                    if (!seen.Add(m)) continue;
                    list.Add(new MatState
                    {
                        Mat = m,
                        HadKeyword = true,
                        HadToggleProp = m.HasProperty(SpecToggleProp),
                        ToggleValue = m.HasProperty(SpecToggleProp) ? m.GetFloat(SpecToggleProp) : 1f
                    });
                }
            }
            return list;
        }

        /// <summary>키워드만 세우면 직렬화에서 살아남지 않는다 — 토글 프로퍼티를 같이 세운다.</summary>
        private static void SetSpec(MatState ms, bool on)
        {
            if (ms.Mat == null) return;
            if (on) ms.Mat.EnableKeyword(SpecKeyword); else ms.Mat.DisableKeyword(SpecKeyword);
            if (ms.HadToggleProp) ms.Mat.SetFloat(SpecToggleProp, on ? ms.ToggleValue : 0f);
        }

        private static void SetSpecAll(List<MatState> list, bool on)
        {
            foreach (var ms in list) SetSpec(ms, on);
        }

        // ── 잡동사니 ─────────────────────────────────────────────────────────
        private static float Lum(Color c) => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;

        private static string Describe(string label, Light l)
        {
            if (l == null) return label + " = 없음";
            return string.Format("{0}: enabled={1} active={2} intensity={3:F4} range={4:F3} pos={5} color={6}",
                label, l.enabled, l.gameObject.activeInHierarchy, l.intensity, l.range,
                l.transform.position.ToString("F3"), l.color.ToString("F3"));
        }

        private static Light FindLightByPath(string path)
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                var t = l.transform;
                string s = t.name;
                while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
                if (s == path) return l;
            }
            return null;
        }

        private static void Row(StringBuilder sb, string name, Stat s)
        {
            sb.AppendLine(string.Format("{0,-22} {1:F4}    {2:F4}    {3:F4}    {4:F4}    {5,5:F1}   {6,5:F1}",
                name, s.Full, s.Machine, s.Hot, s.Pin, s.BlackPct, s.BlownPct));
        }
    }
}
