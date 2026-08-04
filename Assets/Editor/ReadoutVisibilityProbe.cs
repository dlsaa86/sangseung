using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 「이 물체가 화면에 몇 화소를 그리는가」를 **재는** 도구. (`UP-FIX-82`·`UP-FIX-87`)
    ///
    /// ## 왜 필요한가
    ///
    /// 2026-08-04 세션이 계기 5개와 계약 표찰 3개를 「네 포즈 전부 0픽셀」로 적고
    /// 원인 후보 둘(방향·가림)을 **추정**으로 남겼다. 둘 다 틀렸다 —
    /// 실제 원인은 ① 렌더러가 꺼져 있었고 ② 글자가 정렬 규칙 때문에 0.56m 옆에 있었다.
    /// 추정으로 고치면 세 번 옮기게 된다. 그래서 재는 도구를 먼저 만든다.
    ///
    /// ## 재는 방법 — 차분 렌더
    ///
    /// 같은 포즈를 **두 번** 렌더한다. 한 번은 대상 렌더러를 켜고, 한 번은 끄고.
    /// 달라진 화소 수가 곧 그 물체가 최종 이미지에 기여한 화소다.
    /// 레이캐스트 표본이나 화면 AABB 와 달리 **가림·투명·포스트까지 포함**되고,
    /// 「보이는가」가 아니라 「몇 화소인가」를 돌려준다.
    ///
    /// 대상에 콜라이더가 없어도 되고, 셰이더가 무엇이든 상관없다.
    /// </summary>
    public static class ReadoutVisibilityProbe
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const float FovVertical = 60f;

        /// <summary>
        /// A~D 는 `Captures/cabin_v8_20260804/manifest.txt` 에서 그대로 옮겼다. 재유도하지 않았다.
        ///
        /// **E 는 새로 넣는다.** 계약 명판 셋은 우벽 (1.95, 1.22~1.68, −1.80) 에 있는데
        /// A~D 네 포즈 전부에서 **카메라 뒤**다 (전방 내적 −0.14 / −2.08 / −1.03 / −0.067).
        /// 즉 「네 포즈에서 0픽셀」은 씬 결함만이 아니라 **캡처 세트의 구멍**이었다.
        /// 직전 독립 평가도 같은 것을 지적했다 — 「실행/과수확/전력탱크 3물체가
        /// 다 들어오는 지점을 캡처 세트에 추가해야 한다」.
        /// </summary>
        private static readonly (string name, Vector3 eye, Vector3 look)[] Poses =
        {
            ("A_entry_to_machine", new Vector3(0.30f, 1.62f, -1.90f), new Vector3(-0.35f, 1.30f, 2.28f)),
            ("B_machine_front",    new Vector3(-0.35f, 1.62f, 0.30f), new Vector3(-0.35f, 1.45f, 2.13f)),
            ("C_toward_gate",      new Vector3(0.55f, 1.62f, 0.30f),  new Vector3(-2.00f, 1.25f, -0.10f)),
            ("D_wide_corner",      new Vector3(1.55f, 1.72f, -1.95f), new Vector3(-1.10f, 1.10f, 1.90f)),
            ("E_contract_wall",    new Vector3(0.55f, 1.62f, -0.55f), new Vector3(1.95f, 1.45f, -1.80f)),
        };

        /// <summary>
        /// **추가 포즈.** 기존 A~F 는 한 값도 건드리지 않는다 — 고정 세트의 비교 가능성이
        /// 거기 걸려 있다. 덧붙이기만 한다.
        ///
        /// `G_gauge_face` 가 `UP-FIX-88` 의 증거다. 4차 평가가 「B·F 에서 계기판이 우측
        /// 프레임 밖으로 절단」이라고 잡았고, 좌표로 재 보니 원인이 **판이 아니라 화각**이었다 —
        ///
        ///   판독면 `Screen`      월드 X 0.983 … 1.773
        ///   포즈 B 프레임 우단   월드 X 1.447   ← 판독면의 **59%** 만 담는다
        ///   포즈 F 프레임 우단   월드 X 1.661
        ///
        /// 즉 B 는 계기판을 다 담을 수 없는 화각이고, 그건 글자 크기로 풀 수 있는 문제가
        /// 아니다(줄이면 `UP-FIX-86` 이 되돌아간다). 계기판을 **온전히 담는 포즈를
        /// 하나 추가**해 「이 판은 잘리지 않는다」를 판정 가능하게 만든다.
        /// 플레이어가 실제로 취하는 자세다 — 계기를 읽으려면 그 앞에 선다.
        /// </summary>
        private static readonly (string name, Vector3 eye, Vector3 look)[] ExtraPoses =
        {
            // 판독 내용의 실제 중심(글리프 X 0.995…1.540 · Y 1.05…1.79 → 약 (1.27, 1.44))을
            // 정면으로 본다. 판독면 중심(1.378)을 보면 내용이 화면 왼쪽으로 치우친다 —
            // 내용이 면의 왼쪽에 붙어 있기 때문이다(`UP-FIX-88` 의 왼쪽 정렬).
            ("G_gauge_face", new Vector3(1.27f, 1.62f, 0.95f), new Vector3(1.27f, 1.44f, 2.05f)),

            // **신설 (2026-08-04)** — 실행 레버와 그 위의 운행 계기탑을 한 화각에 담는다.
            // 사용자 지시가 「전력표시기는 레버 위 두는 게 적당할 것 같다. 그래야 한눈에
            // 보고 결정하지」였으므로, 「한눈에」가 성립하는지 판정할 수 있는 포즈가 있어야 한다.
            // 눈높이 1.62 에서 레버 기둥 바닥(0.42)부터 탑 상단(2.716)까지 전부 들어온다
            // (거리 2.84 m · 수직 화각 60° → 프레임 세로 3.28 m).
            // ⚠ 기존 A~G 는 한 값도 건드리지 않았다. 덧붙이기만 한다.
            ("H_lever_column", new Vector3(0.758f, 1.62f, -0.60f), new Vector3(0.758f, 1.72f, 2.24f)),
        };

        /// <summary>
        /// **전력 스윕** — 「점점 차오르는 것이 보이는가」의 증거 (사용자 지시 2026-08-04).
        ///
        /// 🔴 직전 라운드의 `PowerLadder`(천장등 색·밝기가 달성률을 따라감)를 **버렸다.**
        /// 5차 독립 평가가 실측으로 기각했다 — 값을 못 싣고(p060·p100 육안 구분 불가),
        /// 회색조에서 사라지고, `p000`(0%·Stable) 과 `p240_collapse`(240%·Collapse) 가
        /// **같은 화면**을 냈다. 그 채널을 더 세게 만들지 말라는 것이 이번 라운드의 지시다.
        ///
        /// 대신 **물리 계기의 채움 높이**를 스윕한다. 높이는 회색조에서 살아남고,
        /// 0% 와 240% 가 같은 그림이 될 수 없다 — 하나는 빈 관이고 하나는 꽉 찬 관이다.
        ///
        /// 값은 임계점 표의 경계에 맞춘다: 100%(요구·2단 잠금 해제) · 170%(다층 상승) ·
        /// 240%(과수확 구간). 즉 **각 칸이 게임 규칙의 한 구간을 대표한다.**
        /// </summary>
        private static readonly (string suffix, float ratio)[] PowerSweep =
        {
            ("p000", 0.00f),
            ("p050", 0.50f),
            ("p100", 1.00f),
            ("p170", 1.70f),
            ("p240", 2.40f),
        };

        /// <summary>`UP-FIX-87` 이 「네 포즈 전부 0픽셀」로 지목한 여덟.</summary>
        private static readonly string[] Targets =
        {
            // 계기 글자 다섯 줄 — 2차 독립 평가가 「숫자 0개」로 지목한 것이 이쪽이다.
            // 판(배경 쿼드)은 렌더되는데 글자만 0 이었다.
            "GrayboxWorld/Car/InstrumentPanel/FloorLabel",
            "GrayboxWorld/Car/InstrumentPanel/PowerLabel",
            "GrayboxWorld/Car/InstrumentPanel/RequiredLabel",
            "GrayboxWorld/Car/InstrumentPanel/StatusLabel",
            "GrayboxWorld/Car/InstrumentPanel/CascadeLabel",
            "GrayboxWorld/Car/InstrumentPanel/PanelBack",
            "GrayboxWorld/Car/InstrumentPanel/PowerBarBg",
            "GrayboxWorld/Car/InstrumentPanel/PowerBarPivot/PowerBarFill",
            "GrayboxWorld/Car/InstrumentPanel/OverloadLight",
            "GrayboxWorld/Car/InstrumentPanel/OverloadHousing",
            "GrayboxWorld/Car/ContractPlaqueLabel_0",
            "GrayboxWorld/Car/ContractPlaqueLabel_1",
            "GrayboxWorld/Car/ContractPlaqueLabel_2",
            // 참고용 — 임계 눈금도 같은 스윕에 꺼져 있었다.
            "GrayboxWorld/Car/InstrumentPanel/PowerBarTicks/Tick_100",
            "GrayboxWorld/Car/InstrumentPanel/PowerBarTicks/Tick_300",
            "GrayboxWorld/Car/ContractPlaque_0",
        };

        [MenuItem("Ascend/Diag/Measure Readout Visibility")]
        public static void Measure()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 재지 않는다."); return; }

            var log = new StringBuilder("[상승] === 기여 화소 측정 (차분 렌더) ===\n");
            log.AppendLine($"  해상도 {Width}x{Height} · 수직 FOV {FovVertical} · post ON");

            Camera cam = MakeCamera(out UniversalAdditionalCameraData data);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            cam.targetTexture = rt;

            var found = new List<(string path, Renderer[] rs)>();
            foreach (string p in Targets)
            {
                GameObject go = GameObject.Find(p);
                if (go == null) { log.AppendLine($"  ⚠ 없음 — {p}"); continue; }
                Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
                found.Add((p, rs));
            }

            log.AppendLine();
            log.AppendLine("  물체".PadRight(46) + string.Join("", System.Array.ConvertAll(Poses, s => s.name.PadLeft(20))));

            foreach (var (path, rs) in found)
            {
                var row = new StringBuilder("  " + Leaf(path).PadRight(44));
                foreach (var pose in Poses)
                {
                    Aim(cam, pose.eye, pose.look);
                    Color32[] on = Shot(cam, rt);
                    var was = new bool[rs.Length];
                    for (int i = 0; i < rs.Length; i++) { was[i] = rs[i].enabled; rs[i].enabled = false; }
                    Color32[] off = Shot(cam, rt);
                    for (int i = 0; i < rs.Length; i++) rs[i].enabled = was[i];

                    int diff = 0;
                    for (int i = 0; i < on.Length; i++)
                    {
                        int d = Mathf.Abs(on[i].r - off[i].r) + Mathf.Abs(on[i].g - off[i].g) + Mathf.Abs(on[i].b - off[i].b);
                        if (d > 6) diff++;   // 6/765 미만은 톤매핑 잡음이다
                    }
                    row.Append(diff.ToString("N0").PadLeft(20));
                }
                log.AppendLine(row.ToString());
            }

            // 포즈마다 프레임을 한 장씩 남긴다. 수치만 남기면 다음 사람이
            // 「그래서 어떻게 생겼나」를 다시 찍어야 한다.
            log.AppendLine();
            foreach (var pose in Poses)
            {
                Aim(cam, pose.eye, pose.look);
                SavePng(Shot(cam, rt), $"Captures/readout_probe/{pose.name}.png");
                log.AppendLine($"  Captures/readout_probe/{pose.name}.png " +
                               $"eye=({pose.eye.x:F2}, {pose.eye.y:F2}, {pose.eye.z:F2}) " +
                               $"lookAt=({pose.look.x:F2}, {pose.look.y:F2}, {pose.look.z:F2})");
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cam.gameObject);
            _ = data;
            Debug.Log(log.ToString());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  심볼 회색조 분리 — `UP-FIX-82` 의 유일한 합격 기준
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 결과판을 정면에서 찍고 **회색조로 바꾼 뒤** 아홉 칸을 각각 잰다.
        ///
        /// 「색으로 구분된다」는 근거가 아니다(`visual-criteria` B-2 #5 —
        /// 「회색조로 바꿨을 때 구분이 사라지면 실패」). 그래서 색을 버리고
        /// **형태에서만 나오는 통계** 셋을 뽑는다 —
        ///
        ///   ① 밝은 덩어리 **개수** (증식체 3 vs 나머지 1)
        ///   ② 밝은 화소가 채운 bbox 의 **폭** (구 > 흡수체)
        ///   ③ bbox 대비 채움률 = **볼록함** (매끈한 구는 높고 뾰족한 것은 낮다)
        ///
        /// 셋 다 색 채널을 쓰지 않는다. 통과 기준은 「세 종류가 이 셋으로 갈리는가」다.
        /// </summary>
        [MenuItem("Ascend/Diag/Symbol Grayscale Separation")]
        public static void GrayscaleSeparation()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 재지 않는다."); return; }

            var log = new StringBuilder("[상승] === 심볼 회색조 분리 (UP-FIX-82) ===\n");

            GameObject grid = GameObject.Find("SoulMachineFrame/WindowGrid");
            if (grid == null) { Debug.LogError("[상승] WindowGrid 를 찾지 못했다"); return; }

            Camera cam = MakeCamera(out _);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            cam.targetTexture = rt;

            // 결과판 정면. 아홉 창의 중심을 겨냥해 세 열이 대칭으로 들어오게 한다.
            Vector3 center = Vector3.zero;
            int n = 0;
            for (int col = 0; col < 3; col++)
                for (int row = 0; row < 3; row++)
                {
                    Transform m = grid.transform.Find($"SoulWindowModule_{col}{row}");
                    if (m != null) { center += m.position; n++; }
                }
            if (n > 0) center /= n;
            // 판 전체가 여유를 두고 들어오는 거리를 **계산한다.** 1.45m 로 고정했을 때
            // 세로 화각(1.67m)이 캐비닛 높이(1.79m)보다 작아 위아래가 잘렸다.
            float need = Art.ReferenceRoomSpec.MachineHeight * 1.25f;
            float dist = need * 0.5f / Mathf.Tan(FovVertical * 0.5f * Mathf.Deg2Rad);
            Vector3 eye = center + new Vector3(0f, 0f, -dist);
            Aim(cam, eye, center);

            Color32[] px = Shot(cam, rt);
            byte[] gray = ToGray(px);
            SavePng(px, "Captures/symbol_probe/board_color.png");
            SaveGrayPng(gray, "Captures/symbol_probe/board_gray.png");

            log.AppendLine($"  카메라 eye=({eye.x:F2}, {eye.y:F2}, {eye.z:F2}) → 판 중심 ({center.x:F2}, {center.y:F2}, {center.z:F2})");
            log.AppendLine("  Captures/symbol_probe/board_color.png · board_gray.png");
            log.AppendLine();
            log.AppendLine("  ⚠ 아홉 칸을 **한 장에서** 재지 않는다. 창마다 시선각이 달라 같은 형상이");
            log.AppendLine("     다른 통계를 내고, 심볼이 도어면보다 90mm 뒤라 시차로 표본 원이 밀려");
            log.AppendLine("     클램프 링이 섞여 들어온다. 창마다 **정면에서 따로** 찍어 같은 조건으로 잰다.");
            log.AppendLine();
            log.AppendLine("  칸  종류           밝은화소   덩어리   bbox(px)      채움률   최대덩어리   최대휘도");

            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 3; row++)
                {
                    Transform m = grid.transform.Find($"SoulWindowModule_{col}{row}");
                    Transform cell = m != null ? m.Find($"Cell_{row}") : null;
                    if (cell == null) continue;

                    string kind = "(빈칸)";
                    foreach (Transform c in cell)
                        if (c.gameObject.activeSelf && View.SpinBoardView.KindOf(c.name) != Spin.SymbolKind.Empty)
                            kind = c.name.Substring(4);

                    // 창 하나가 화면 세로의 절반을 차지하는 거리. 창마다 같다.
                    float d = Art.ReferenceRoomSpec.WindowGlassDiameter
                              / Mathf.Tan(FovVertical * 0.5f * Mathf.Deg2Rad);
                    Aim(cam, m.position + new Vector3(0f, 0f, -d), m.position);
                    byte[] g1 = ToGray(Shot(cam, rt));

                    // 표본 원 = 유리 반지름의 0.80 배. 링과 볼트는 그 밖이다.
                    Vector3 sp = cam.WorldToScreenPoint(m.position);
                    Vector3 edge = cam.WorldToScreenPoint(
                        m.position + Vector3.up * (Art.ReferenceRoomSpec.WindowGlassDiameter * 0.40f));
                    int rad = Mathf.Max(8, Mathf.RoundToInt(Vector3.Distance(sp, edge)));

                    var stat = Analyze(g1, Mathf.RoundToInt(sp.x), Mathf.RoundToInt(sp.y), rad);
                    log.AppendLine($"  {col}{row}  {kind,-14}{stat.lit,9:N0}{stat.blobs,9}   " +
                                   $"{stat.w,3}x{stat.h,-3}      {stat.fill,6:F3}{stat.largest,11:N0}" +
                                   $"{stat.peak,10}");
                }
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cam.gameObject);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 판정용 고정 캡처 세트. **색과 회색조를 짝으로 남긴다.**
        ///
        /// 회색조를 함께 내는 이유는 `visual-criteria` B-2 #5 가 「회색조로 바꿨을 때
        /// 구분이 사라지면 실패」이기 때문이다. 평가자가 직접 변환하게 두면 변환식이
        /// 매번 달라지고, 그러면 판정이 아니라 변환을 비교하게 된다.
        /// </summary>
        [MenuItem("Ascend/Diag/Capture Symbol Evidence Set")]
        public static void CaptureEvidence()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 찍지 않는다."); return; }

            const string dir = "Captures/symbols_v6_20260805";
            var log = new StringBuilder($"[상승] === 고정 캡처 세트 → {dir} ===\n");

            // 🔴 찍기 **전에** 유령 서브메시를 지운다. 3차 평가의 「전력 0 /」가
            // 이것을 안 하고 찍은 결과다 — 씬 데이터에 없는 글자가 캡처에 있었다.
            // 깨끗하면 0 이라 멱등하고, 아니면 캡처가 거짓 증거가 되는 것을 막는다.
            var ghostLog = new StringBuilder();
            int ghosts = PrototypeEditor.KoreanLabelFontFix.ClearGhostSubMeshes(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(), ghostLog);

            Camera cam = MakeCamera(out UniversalAdditionalCameraData data);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            cam.targetTexture = rt;

            var man = new StringBuilder();
            man.AppendLine("symbols_v6_20260805 capture manifest");
            man.AppendLine($"resolution {Width}x{Height}  fovVertical {FovVertical}  antialiasing None  " +
                           $"RT ARGB32 sRGB  MSAA off  post {(data != null && data.renderPostProcessing ? "ON" : "OFF")}");
            man.AppendLine($"machineFingerprint {SystemInfo.operatingSystemFamily}|{SystemInfo.graphicsDeviceType}|" +
                           $"{SystemInfo.graphicsDeviceName}|{Application.unityVersion}");
            man.AppendLine("gray/ 는 같은 프레임의 sRGB 가중 휘도(0.2126/0.7152/0.0722) 변환이다.");
            man.AppendLine($"촬영 전 유령 서브메시 정리 {ghosts}개 (TMP 가 안 지운 옛 글리프. 0 이면 이미 깨끗)");
            man.AppendLine();

            var bands = new List<(string name, Band b)>();
            foreach (var pose in Poses)
            {
                Aim(cam, pose.eye, pose.look);
                Color32[] px = Shot(cam, rt);
                SavePng(px, $"{dir}/{pose.name}.png");
                SaveGrayPng(ToGray(px), $"{dir}/gray/{pose.name}.png");
                bands.Add((pose.name, Measure(px)));
                man.AppendLine($"{pose.name}  eye=({pose.eye.x:F2}, {pose.eye.y:F2}, {pose.eye.z:F2}) " +
                               $"lookAt=({pose.look.x:F2}, {pose.look.y:F2}, {pose.look.z:F2})" +
                               GlyphNote(cam));
                man.AppendLine("   " + LeverLabelNote(cam));
            }

            // 추가 포즈. A~E 뒤에 붙이므로 기존 다섯의 파일도 수치도 바뀌지 않는다.
            foreach (var pose in ExtraPoses)
            {
                Aim(cam, pose.eye, pose.look);
                Color32[] px = Shot(cam, rt);
                SavePng(px, $"{dir}/{pose.name}.png");
                SaveGrayPng(ToGray(px), $"{dir}/gray/{pose.name}.png");
                bands.Add((pose.name, Measure(px)));
                man.AppendLine($"{pose.name}  eye=({pose.eye.x:F2}, {pose.eye.y:F2}, {pose.eye.z:F2}) " +
                               $"lookAt=({pose.look.x:F2}, {pose.look.y:F2}, {pose.look.z:F2})" +
                               GlyphNote(cam) + PanelClipNote(cam));
            }

            // 결과판 정면 — 심볼 3종 판정의 본 그림.
            var derived = new List<(string name, Vector3 eye, Vector3 look)>();
            GameObject grid = GameObject.Find("SoulMachineFrame/WindowGrid");
            if (grid != null)
            {
                Vector3 c = Vector3.zero; int n = 0;
                foreach (Transform m in grid.transform) { c += m.position; n++; }
                if (n > 0) c /= n;
                float need = Art.ReferenceRoomSpec.MachineHeight * 1.25f;
                float dist = need * 0.5f / Mathf.Tan(FovVertical * 0.5f * Mathf.Deg2Rad);
                Vector3 eye = c + new Vector3(0f, 0f, -dist);
                derived.Add(("F_board_front", eye, c));
                Aim(cam, eye, c);
                Color32[] px = Shot(cam, rt);
                SavePng(px, $"{dir}/F_board_front.png");
                SaveGrayPng(ToGray(px), $"{dir}/gray/F_board_front.png");
                man.AppendLine($"F_board_front  eye=({eye.x:F2}, {eye.y:F2}, {eye.z:F2}) " +
                               $"lookAt=({c.x:F2}, {c.y:F2}, {c.z:F2})");
                man.AppendLine();
                man.AppendLine("보드 상태(저장된 씬의 기본 판): 정상 5 · 흡수 2 · 증식 2");
                man.AppendLine("  열0 [정상/흡수/정상]  열1 [증식/정상/흡수]  열2 [정상/증식/정상]  (행 0 = 위)");
            }

            // 전력 스윕. **밴드 집계에 넣지 않는다** — 접두어가 A/C/D 가 아니므로
            // `AppendBandTable` 의 평균이 오염되지 않는다(그 함수가 이름 첫 글자로 고른다).
            man.AppendLine();
            CapturePowerSweep(cam, rt, dir, man);

            man.AppendLine();
            AppendBandTable(man, bands);
            man.AppendLine();
            AppendColumnGlyphTable(cam, man);
            man.AppendLine();
            AppendRepeaterTable(cam, man);
            man.AppendLine();
            AppendWeightTable(cam, rt, man);
            man.AppendLine();
            AppendPanelClipTable(cam, man, derived);
            man.AppendLine();
            AppendShaftNote(man);

            Write(Encoding.UTF8.GetBytes(man.ToString()), $"{dir}/manifest.txt");
            log.Append(man);

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cam.gameObject);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 이 포즈에서 계기 글리프가 **몇 화소**인가. 「키웠다」가 아니라 수치로 적는다.
        ///
        /// 글리프 사각형 네 모서리를 화면에 투영해 세로 길이를 잰다. 잉크 자체는
        /// 이 상자보다 조금 작지만, 상자는 카메라·거리·FOV 만으로 정해지므로
        /// **노출이나 임계값에 흔들리지 않는다** — 라운드 간 비교에 그게 필요하다.
        /// </summary>
        private static string GlyphNote(Camera cam)
        {
            GameObject panel = GameObject.Find("GrayboxWorld/Car/InstrumentPanel");
            if (panel == null) return string.Empty;
            float min = float.MaxValue, max = 0f;
            int lines = 0;
            foreach (TMPro.TMP_Text t in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                t.ForceMeshUpdate();
                var info = t.textInfo;
                for (int i = 0; i < info.characterCount; i++)
                {
                    TMPro.TMP_CharacterInfo ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;
                    Vector3 a = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.bottomLeft));
                    Vector3 b = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.topRight));
                    if (a.z <= 0f || b.z <= 0f) continue;
                    if (!BoxTouchesFrame(a, b)) continue;   // 화각 밖 글자를 판독 범위에 넣지 않는다
                    float h = Mathf.Abs(b.y - a.y);
                    if (h <= 0.01f) continue;
                    min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                    lines++;
                }
            }
            if (lines == 0) return "   계기 글리프: 화각 밖";
            return $"   계기 글리프 높이 {min:F0}~{max:F0} px ({lines} 글자)";
        }

        /// <summary>
        /// 두 레버 표찰이 이 포즈에서 **몇 화소인가.** 「키웠다」가 아니라 「B 에서 몇 px」이다.
        ///
        /// 세로만 재던 <see cref="GlyphNote"/> 로는 이 항목을 못 잡는다 —
        /// 3차 평가가 지적한 `실행` 9 px 은 **가로**였고, 그 가로를 뭉갠 것이 크기가
        /// 아니라 면 방향(rotY 90°)이었다. 그래서 가로·세로·면 방향을 함께 적는다.
        /// `과수확` 은 손대지 않은 대조군이다.
        /// </summary>
        private static string LeverLabelNote(Camera cam)
        {
            var parts = new List<string>();
            foreach (string p in new[] { "GrayboxWorld/Car/Console/ExecutionLabel",
                                         "GrayboxWorld/Car/OverharvestLever/OverharvestLabel" })
            {
                GameObject go = GameObject.Find(p);
                string leaf = Leaf(p).Replace("Label", string.Empty);
                if (go == null) { parts.Add($"{leaf}: 없음"); continue; }
                var t = go.GetComponent<TMPro.TMP_Text>();
                if (t == null) { parts.Add($"{leaf}: TMP 없음"); continue; }
                t.ForceMeshUpdate();

                float wMin = float.MaxValue, hMin = float.MaxValue;
                int n = 0;
                TMP_TextInfoScan(t, cam, ref wMin, ref hMin, ref n);
                float facing = Vector3.Dot(go.transform.forward,
                                           (go.transform.position - cam.transform.position).normalized);
                if (n == 0) parts.Add($"{leaf}: 화각 밖");
                else parts.Add($"{leaf} {wMin:F0}x{hMin:F0}px/자 (facing {facing:+0.00;-0.00})");
            }
            return "레버 표찰 — " + string.Join(" · ", parts);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  전력 스윕 — 「점점 차오르는 것이 보이는가」의 증거 (사용자 지시 2026-08-04)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 운행 계기탑을 **달성률만 바꿔** 여러 장 찍는다. 정지 이미지 한 장으로는
        /// 「차오른다」를 판정할 수 없으므로 같은 화각의 계열이 필요하다.
        ///
        /// 세 가지를 반드시 지킨다 — 직전 라운드의 조명 사다리가 지키던 것과 같다.
        ///   ① 찍기 전에 원래 상태를 **그대로 붙잡고** 끝나면 되돌린다.
        ///      되돌리지 않으면 스윕 마지막 칸(240%)이 씬에 저장되고 고정 캡처가 오염된다.
        ///   ② 되돌린 값을 **다시 재서** 매니페스트에 적는다. 「되돌렸다」는 주장이 아니라 수치여야 한다.
        ///   ③ 층수는 **게임이 쓰는 규칙으로만** 낸다 — `RunSession.PreviewFloorsGained(power, required)`.
        ///      화면에 그린 숫자와 실제 상승이 갈라지면 그게 최악이다.
        ///
        /// 회색조를 짝으로 낸다. `visual-criteria` B-2 #5 가 「회색조로 바꿨을 때 구분이
        /// 사라지면 실패」이고, 직전 라운드가 정확히 거기서 죽었다.
        /// </summary>
        private static void CapturePowerSweep(Camera cam, RenderTexture rt, string dir, StringBuilder man)
        {
            GameObject col = GameObject.Find("ReferenceRoom/AscentColumn");
            if (col == null)
            { man.AppendLine("━━ 전력 스윕 ━━ ⚠ AscentColumn 이 없다 — 찍지 않았다"); return; }

            Transform pivot = FindIn(col.transform, "TankFillPivot");
            Renderer fill = FindIn(col.transform, "TankFill")?.GetComponent<Renderer>();
            Renderer band = FindIn(col.transform, "Tick_100")?.GetComponent<Renderer>();
            Transform pin = FindIn(col.transform, "LockPin");
            TMPro.TMP_Text spinNum = TmpIn(col.transform, "SpinDrum", "Numeral");
            TMPro.TMP_Text ascNum = TmpIn(col.transform, "AscentDrum", "Numeral");
            TMPro.TMP_Text powerLine = TmpIn(col.transform, "DataPlate", "PowerLine");
            TMPro.TMP_Text reserve = TmpIn(col.transform, "DataPlate", "ReserveLine");
            var pips = new List<Renderer>();
            for (int i = 0; i < 6; i++)
            {
                Transform p = FindIn(col.transform, $"SpinPip_{i}");
                if (p != null) pips.Add(p.GetComponent<Renderer>());
            }
            var view = col.GetComponent<View.AscentColumnView>();
            if (pivot == null || fill == null || view == null)
            { man.AppendLine("━━ 전력 스윕 ━━ ⚠ 계기탑 배선이 비어 있다 — 찍지 않았다"); return; }

            // 🔴 **반복기도 함께 민다** (UP-FIX-92 / UP-FIX-95).
            //
            // 에디트 모드라 `LateUpdate` 가 돌지 않으므로 스윕이 직접 값을 쓴다.
            // 탑만 밀면 캡처에 「탑 전력 516 240% · 벽 전력 0」이 그대로 찍힌다 —
            // 6차 평가가 새로 잡은 결함이 정확히 그 모양이었고, 그때는 원인이
            // **하네스가 탑만 구동한 것**인지 런타임 불일치인지 캡처로 구분조차 되지
            // 않았다. 여기서 같이 밀면 그 물음이 사라진다.
            var repPivots = new List<Transform>();
            var repFills = new List<Renderer>();
            var repBands = new List<Renderer>();
            var repLines = new List<TMPro.TMP_Text>();
            var repLine0 = new List<string>();
            for (int i = 0; i < view.RepeaterCount; i++)
            {
                Transform rp = view.RepeaterFillPivot(i);
                if (rp == null || rp.parent == null) continue;
                Transform rroot = rp.parent;
                repPivots.Add(rp);
                repFills.Add(FindIn(rp, "TankFill")?.GetComponent<Renderer>());
                repBands.Add(FindIn(rroot, "Tick_100")?.GetComponent<Renderer>());
                Transform pl = rroot.Find("PowerLine");
                TMPro.TMP_Text plt = pl != null ? pl.GetComponent<TMPro.TMP_Text>() : null;
                repLines.Add(plt);
                repLine0.Add(plt != null ? plt.text : null);
            }

            // 원래 상태를 붙잡는다.
            Vector3 pivot0 = pivot.localScale;
            Vector3 pin0 = pin != null ? pin.localPosition : Vector3.zero;
            string spin0 = spinNum != null ? spinNum.text : null;
            string asc0 = ascNum != null ? ascNum.text : null;
            string pow0 = powerLine != null ? powerLine.text : null;
            string res0 = reserve != null ? reserve.text : null;

            // 자기 출력 폴더를 먼저 비운다. 구성이 바뀌면 옛 프레임이 「지금의 화면」으로 읽힌다.
            string sweepDir = $"{dir}/power_sweep";
            if (Directory.Exists(sweepDir)) Directory.Delete(sweepDir, true);

            // 층수는 실제 규칙에서만 온다. 세션이 없으면 만든다(순수 C# 이라 에디트 모드에서 돈다).
            var runBehaviour = Object.FindAnyObjectByType<Run.RunSessionBehaviour>(FindObjectsInactive.Include);
            Run.RunSession session = runBehaviour != null ? runBehaviour.Session : null;
            if (session == null && runBehaviour != null)
            {
                runBehaviour.ResetRun();
                session = runBehaviour.Session;
            }
            float required = session != null && session.Current != null ? session.Current.RequiredPower : 0f;
            int spinsPlanned = session != null && session.Current != null ? session.Current.Plan.Spins : 3;
            if (required <= 0f) required = 350f;   // 세션을 못 만들면 1층 임시값으로라도 스윕한다

            man.AppendLine("━━ 전력 스윕 — 「점점 차오르는 것이 보이는가」 (사용자 지시 2026-08-04) ━━━━━━");
            man.AppendLine($"  기준 층 요구 전력 {required:F0} · 계획 스핀 {spinsPlanned}회 · " +
                           $"세션 {(session == null ? "없음(임시값)" : "RunSessionBehaviour.Session")}");
            man.AppendLine("  🔴 직전 라운드의 조명 사다리(`power_ladder/`)는 **버렸다** — 5차 평가가 실측으로");
            man.AppendLine("     기각했다(값 못 실음 · 회색조 소실 · 0%↔240% 동일 화면). 이번 채널은 **채움 높이**다.");
            man.AppendLine();

            // 🔴 **포즈마다 「그 포즈가 읽어야 할 계기」를 함께 들고 다닌다** (UP-FIX-92).
            //
            // 직전 판본은 네 포즈 전부를 **계기탑의** 채움 기둥으로 쟀다. 기계 벽을 등진
            // `C`·`E` 에서는 탑이 화각 밖이라 그 측정은 의미가 없고, 게다가 `Clamp` 가
            // 범위 밖 좌표를 화면 가장자리로 접어 **그럴듯한 숫자**를 냈다(6차 평가가
            // 매니페스트의 `G_gauge_face` 행에서 잡아낸 그 결함). 이제 각 포즈는
            // 자기 벽에 있는 계기를 잰다 — 탑이 없으면 반복기를 잰다.
            var sweepPoses = new List<(string name, Vector3 eye, Vector3 look, Transform gauge)>();
            foreach (var p in Poses)
                if (p.name == "A_entry_to_machine") sweepPoses.Add((p.name, p.eye, p.look, pivot));
            foreach (var p in ExtraPoses)
                if (p.name == "H_lever_column") sweepPoses.Add((p.name, p.eye, p.look, pivot));
            foreach (var p in Poses)
            {
                if (p.name == "C_toward_gate" && view.RepeaterCount > 0)
                    sweepPoses.Add((p.name, p.eye, p.look, view.RepeaterFillPivot(0)));
                if (p.name == "E_contract_wall" && view.RepeaterCount > 1)
                    sweepPoses.Add((p.name, p.eye, p.look, view.RepeaterFillPivot(1)));
            }

            var emptyRef = new int[sweepPoses.Count];   // 포즈별 「빈 관」 기준값 (p000 에서 잰다)
            for (int i = 0; i < emptyRef.Length; i++) emptyRef[i] = -1;

            var head = new StringBuilder($"  {"파일",-22}{"달성률",6}{"전력",8}{"층수",6}{"채움m",8}");
            foreach (var p in sweepPoses)
                head.Append($"{Abbrev(p.name),7}{"대비",7}");
            man.AppendLine(head.ToString());
            man.AppendLine("  ※ 각 포즈는 **그 화각 안에 있는** 계기를 잰다 — A·H 는 계기탑,");
            man.AppendLine("     C·E 는 전력 반복기(UP-FIX-92). 화각 밖이면 0 이 나온다(프러스텀 확인 있음).");
            foreach (var (suffix, ratio) in PowerSweep)
            {
                float power = required * ratio;
                int floors = session != null ? session.PreviewFloorsGained(power, required) : -1;
                int spinsLeft = Mathf.Clamp(spinsPlanned - Mathf.FloorToInt(ratio * 1.5f), 0, spinsPlanned);
                bool unlocked = ratio >= 1f;

                // 계기탑을 그 상태로 **직접** 민다. 플레이 모드가 아니므로 `LateUpdate` 가 돌지 않는다.
                float fillM = AscentColumnSpec.TankHeight *
                              Mathf.Clamp(ratio, 0f, AscentColumnSpec.MaxRatio) / AscentColumnSpec.MaxRatio;
                Vector3 s = pivot.localScale; s.y = fillM; pivot.localScale = s;
                Color fillColor = ratio < 1f ? new Color(0.62f, 0.56f, 0.40f)
                                : ratio >= 2.2f ? new Color(0.88f, 0.20f, 0.11f)
                                : new Color(0.78f, 0.26f, 0.16f);
                Color bandColor = unlocked ? new Color(0.86f, 0.22f, 0.14f) : new Color(0.34f, 0.11f, 0.09f);
                SetGauge(fill, fillColor, 0.55f);
                SetGauge(band, bandColor, unlocked ? 0.45f : 0f);

                string powerText = $"전력 {power:F0}   {ratio * 100f:F0}%";
                for (int i = 0; i < repPivots.Count; i++)
                {
                    Vector3 rs = repPivots[i].localScale; rs.y = fillM; repPivots[i].localScale = rs;
                    SetGauge(repFills[i], fillColor, 0.55f);
                    SetGauge(repBands[i], bandColor, unlocked ? 0.45f : 0f);
                    if (repLines[i] != null) repLines[i].SetText(powerText);
                }
                if (pin != null) pin.localPosition = unlocked
                    ? new Vector3(pin0.x + 0.066f, pin0.y, pin0.z) : pin0;
                for (int i = 0; i < pips.Count; i++)
                {
                    bool exists = i < spinsPlanned;
                    if (pips[i].gameObject.activeSelf != exists) pips[i].gameObject.SetActive(exists);
                    if (!exists) continue;
                    bool left = i < spinsLeft;
                    SetGauge(pips[i], left ? new Color(0.74f, 0.26f, 0.17f) : new Color(0.10f, 0.095f, 0.088f),
                             left ? 0.30f : 0f);
                }
                if (spinNum != null) spinNum.SetText(spinsLeft.ToString());
                if (ascNum != null) ascNum.SetText(floors <= 0 ? "0" : "+" + floors);
                if (powerLine != null) powerLine.SetText(powerText);
                if (reserve != null) reserve.SetText($"배수 {ratio:F2}배   손실 {(unlocked ? Mathf.RoundToInt(power * 0.12f).ToString() : "—")}");

                var fillPx = new int[sweepPoses.Count];
                var borePxs = new int[sweepPoses.Count];
                int idx = 0;
                foreach (var pose in sweepPoses)
                {
                    Aim(cam, pose.eye, pose.look);
                    Color32[] px = Shot(cam, rt);
                    string leaf = $"{pose.name}_{suffix}";
                    SavePng(px, $"{sweepDir}/{leaf}.png");
                    byte[] gray = ToGray(px);
                    SaveGrayPng(gray, $"{sweepDir}/gray/{leaf}.png");

                    // 🔴 **빈 관의 밝기를 기준으로 삼는다.** 첫 판본은 관 안 화소의
                    // 중앙값을 기준으로 썼는데, 170%·240% 처럼 관이 거의 다 차면
                    // 중앙값이 곧 채움값이 되어 문턱이 채움 위로 올라가고 **0 px 이 나온다.**
                    // 즉 가장 꽉 찬 두 칸이 「비었다」로 기록됐다 — 도구가 결과를 뒤집은 것이다.
                    // `p000`(첫 칸)에서 잰 빈 관 값을 포즈별로 붙잡아 전 칸에 같은 문턱을 쓴다.
                    Transform g = pose.gauge;
                    if (g != null)
                    {
                        if (emptyRef[idx] < 0) emptyRef[idx] = GrayColumnMedian(cam, gray, g);
                        fillPx[idx] = GrayFillHeight(cam, gray, g, emptyRef[idx]);
                        borePxs[idx] = GrayBoreHeight(cam, g);
                    }
                    idx++;
                }

                var row = new StringBuilder($"  {suffix,-22}{ratio,6:P0}{power,8:F0}{floors,6}{fillM,8:F3}");
                for (int i = 0; i < sweepPoses.Count; i++)
                    row.Append($"{fillPx[i],7}{(borePxs[i] > 0 ? (fillPx[i] / (float)borePxs[i]).ToString("P0") : "-"),7}");
                man.AppendLine(row.ToString());
            }

            // 되돌린다. 그리고 되돌아왔는지 **잰다.**
            pivot.localScale = pivot0;
            if (pin != null) pin.localPosition = pin0;
            SetGauge(fill, new Color(0.62f, 0.56f, 0.40f), 0f);
            SetGauge(band, new Color(0.34f, 0.11f, 0.09f), 0f);
            foreach (Renderer p in pips) { p.gameObject.SetActive(true); SetGauge(p, new Color(0.10f, 0.095f, 0.088f), 0f); }
            if (spinNum != null && spin0 != null) spinNum.SetText(spin0);
            if (ascNum != null && asc0 != null) ascNum.SetText(asc0);
            if (powerLine != null && pow0 != null) powerLine.SetText(pow0);
            if (reserve != null && res0 != null) reserve.SetText(res0);

            // 반복기도 되돌린다. 되돌리지 않으면 **다음 고정 캡처가 스윕의 마지막 칸을**
            // 「저장된 씬의 기본 상태」로 찍는다.
            var repAfter = new StringBuilder();
            for (int i = 0; i < repPivots.Count; i++)
            {
                Vector3 rs = repPivots[i].localScale; rs.y = 0f; repPivots[i].localScale = rs;
                SetGauge(repFills[i], new Color(0.62f, 0.56f, 0.40f), 0f);
                SetGauge(repBands[i], new Color(0.34f, 0.11f, 0.09f), 0f);
                if (repLines[i] != null && repLine0[i] != null) repLines[i].SetText(repLine0[i]);
                repAfter.Append($"[{i}] y {repPivots[i].localScale.y:F4} 「{repLine0[i]}」  ");
            }

            man.AppendLine();
            man.AppendLine($"  복원 확인 — 탱크 채움 y {pivot0.y:F4} → {pivot.localScale.y:F4} · " +
                           $"잠금핀 x {pin0.x:F4} → {(pin != null ? pin.localPosition.x : 0f):F4} · " +
                           $"스핀 「{spin0}」 · 층수 「{asc0}」 · 전력 「{pow0}」");
            man.AppendLine($"  반복기 복원 — {(repAfter.Length == 0 ? "반복기 없음" : repAfter.ToString())}");
            man.AppendLine();
            AppendAscentRuleCheck(session, required, man);
        }

        /// <summary>
        /// **화면이 그리는 층수가 실제 상승과 같은가.** 세 값 이상에서 대조한다.
        ///
        /// `AscentColumnView` 는 `RunSession.PreviewFloorsGained()` 만 부르고, 그 함수는
        /// `FloorSession.PreviewAscent()`(= `Resolve()` 와 같은 한 줄)와 `ClampAscent()`
        /// (= `CompleteFloor()` 가 부르는 그 함수)를 그대로 부른다. 즉 갈라질 수 없다 —
        /// 그러나 「갈라질 수 없다」는 주장이므로 표로 남긴다.
        /// </summary>
        private static void AppendAscentRuleCheck(Run.RunSession session, float required, StringBuilder man)
        {
            man.AppendLine("━━ 「몇 층 오르는가」 ↔ 실제 판정 대조 (ClampAscent 경유) ━━━━━━━━━━━━━━━━━━");
            if (session == null) { man.AppendLine("  ⚠ 세션을 만들지 못해 대조하지 못했다"); return; }
            man.AppendLine($"  현재 층 {session.CurrentFloor} · 요구 전력 {required:F0} · 임계점 표 " +
                           "Crash .70 / Jettison .90 / Damaged 1.00 / Rewarded 1.30 / MultiFloor 1.70 / " +
                           "Overharvest 2.20 / Runaway 3.00");
            man.AppendLine("  달성률   전력     밴드            원시 상승  ClampAscent  화면 표시");
            float[] probes = { 0.00f, 0.60f, 0.85f, 1.00f, 1.40f, 1.70f, 2.40f, 3.20f };
            var th = Spin.PowerThresholds.Default;
            foreach (float r in probes)
            {
                float power = required * r;
                var ascent = session.Current.PreviewAscent(power, required);
                int shown = session.PreviewFloorsGained(power, required);
                man.AppendLine($"  {r,6:P0}{power,9:F0}   {Spin.PowerBands.DisplayName(th.BandFor(power, required)),-14}" +
                               $"{ascent.FloorsAscended,9}{shown,13}{(shown <= 0 ? "0" : "+" + shown),11}");
            }
            man.AppendLine("  ⚠ `원시 상승`과 `ClampAscent`가 다른 행은 커리큘럼 보호가 다층 점프를 자른 것이다");
            man.AppendLine("     (`RunSession.ClampAscent` — 빌드 보상 층·필수 층·최종 층 앞에서 멈춘다).");
            man.AppendLine("     화면은 **자른 뒤의 값**을 그린다. 자르기 전 값을 그리면 화면이 게임을 배신한다.");
        }

        private static void SetGauge(Renderer r, Color color, float emission)
        {
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(Shader.PropertyToID("_BaseColor"), color);
            block.SetColor(Shader.PropertyToID("_EmissionColor"), color * emission);
            r.SetPropertyBlock(block);
        }

        /// <summary>탱크 관 전체의 화면 세로 길이(px). 채움 비율의 분모다.</summary>
        private static int GrayBoreHeight(Camera cam, Transform pivot)
        {
            Vector3 a = cam.WorldToScreenPoint(pivot.position);
            Vector3 b = cam.WorldToScreenPoint(pivot.position + Vector3.up * AscentColumnSpec.TankHeight);
            if (a.z <= 0f || b.z <= 0f) return 0;
            if (!BoxTouchesFrame(a, b)) return 0;   // 화각 밖 — 「450px」 같은 유령 수치를 막는다
            return Mathf.RoundToInt(Mathf.Abs(b.y - a.y));
        }

        /// <summary>
        /// **회색조에서** 채움 기둥이 몇 화소인가. 색을 쓰지 않는다 —
        /// 「회색조로 바꿨을 때 구분이 사라지면 실패」가 기준이기 때문이다.
        ///
        /// 관의 화면 중심선을 따라 위에서 아래로 훑어, 관 바닥 대비 밝은 구간의 길이를 센다.
        /// </summary>
        private static int GrayFillHeight(Camera cam, byte[] gray, Transform pivot, int emptyRef)
        {
            if (!ColumnSpan(cam, pivot, out Vector2 lo, out Vector2 hi)) return 0;
            int thr = emptyRef + 10;   // 0~255. 채움은 발광이라 빈 관보다 확실히 위다
            int lit = 0;
            int y0 = Mathf.RoundToInt(lo.y), y1 = Mathf.RoundToInt(hi.y);
            for (int y = y0; y <= y1; y++)
                if (SampleAlong(gray, lo, hi, y, out byte v) && v >= thr) lit++;
            return lit;
        }

        /// <summary>빈 관의 기준 밝기. `p000` 프레임에서 한 번만 잰다.</summary>
        private static int GrayColumnMedian(Camera cam, byte[] gray, Transform pivot)
        {
            if (!ColumnSpan(cam, pivot, out Vector2 lo, out Vector2 hi)) return 255;
            var vals = new List<byte>();
            int y0 = Mathf.RoundToInt(lo.y), y1 = Mathf.RoundToInt(hi.y);
            for (int y = y0; y <= y1; y++)
                if (SampleAlong(gray, lo, hi, y, out byte v)) vals.Add(v);
            if (vals.Count == 0) return 255;
            vals.Sort();
            return vals[vals.Count / 2];
        }

        /// <summary>
        /// 관의 **투영선을 따라** 한 화소를 읽는다.
        ///
        /// 🔴 직전 판본은 아래·위 끝의 x 평균 **하나**로 세로줄을 훑었다. 계기탑처럼
        /// 정면으로 보는 벽에서는 오차가 작지만, `C_toward_gate`·`E_contract_wall`
        /// 처럼 벽을 **비스듬히** 보는 화각에서는 관의 투영이 기울어 표본선이 위아래
        /// 끝에서 관을 벗어나 뒷판·벽을 읽는다. 그 결과 **빈 관이 26 px·67 px 로**
        /// 측정됐다 — 도구가 「비었는데 차 있다」고 말한 것이다.
        /// y 마다 x 를 보간하면 기울어도 관 안에 머무른다.
        /// </summary>
        private static bool SampleAlong(byte[] gray, Vector2 lo, Vector2 hi, int y, out byte value)
        {
            value = 0;
            if (y < 0 || y >= Height) return false;
            float span = hi.y - lo.y;
            float t = Mathf.Abs(span) < 0.001f ? 0f : Mathf.Clamp01((y - lo.y) / span);
            int x = Mathf.RoundToInt(Mathf.Lerp(lo.x, hi.x, t));
            if (x < 0 || x >= Width) return false;
            value = gray[y * Width + x];
            return true;
        }

        /// <summary>관의 아래·위 끝을 **화면 좌표 그대로** 돌려준다(정수 x 하나로 접지 않는다).</summary>
        private static bool ColumnSpan(Camera cam, Transform pivot, out Vector2 lo, out Vector2 hi)
        {
            lo = hi = Vector2.zero;
            Vector3 bottom = cam.WorldToScreenPoint(pivot.position);
            Vector3 top = cam.WorldToScreenPoint(pivot.position + Vector3.up * AscentColumnSpec.TankHeight);
            if (bottom.z <= 0f || top.z <= 0f) return false;
            if (Mathf.Abs(top.y - bottom.y) <= 2f) return false;

            // 🔴 프러스텀 확인. 이게 없으면 화각 **밖**의 관도 화면 가장자리 화소를 읽어
            //   그럴듯한 숫자를 낸다 — 6차 독립 평가가 매니페스트의 `G_gauge_face` 행을
            //   「이미지에 계기탑이 없는데 450px 라고 적혀 있다」로 잡아낸 그 결함이다.
            //   `Clamp` 가 범위 밖을 조용히 가장자리로 접는 것이 원인이었다.
            if (!BoxTouchesFrame(bottom, top)) return false;

            Vector2 b = new Vector2(bottom.x, bottom.y);
            Vector2 t = new Vector2(top.x, top.y);
            lo = b.y <= t.y ? b : t;
            hi = b.y <= t.y ? t : b;
            return true;
        }

        /// <summary>포즈 이름의 앞머리 한 글자(`A_entry_to_machine` → `A`). 표 폭을 위한 것이다.</summary>
        private static string Abbrev(string poseName)
        {
            int i = poseName.IndexOf('_');
            return i > 0 ? poseName.Substring(0, i) : poseName;
        }

        /// <summary>
        /// 투영된 상자가 화면 사각형과 **닿는가.** `WorldToScreenPoint` 는 화각 밖도
        /// 좌표를 돌려주므로(z &gt; 0 이면 성공한다), 이 확인이 없으면 화면에 없는 글자가
        /// 판독 화소로 집계된다.
        /// </summary>
        private static bool BoxTouchesFrame(Vector3 a, Vector3 b)
        {
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
            float y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            return x1 >= 0f && x0 <= Width && y1 >= 0f && y0 <= Height;
        }

        /// <summary>
        /// 상자가 프레임 **밖으로 걸쳐 있는가** (UP-FIX-102).
        ///
        /// 🔴 프러스텀 확인만으로는 부족하다. 7차 독립 평가가 잡았다 —
        /// `D_wide_corner` 에서 반복기가 프레임 왼쪽 끝에 잘려 화면에는 「0 0%」만
        /// 남는데, 매니페스트는 그것을 「전력줄 20px」로 **온전히 잰다.**
        /// 「닿는가」와 「다 들어왔는가」는 다른 물음이고, 판독 화소는 뒤쪽이어야 한다.
        /// </summary>
        private static bool BoxClipped(Vector3 a, Vector3 b)
        {
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
            float y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            return x0 < 0f || x1 > Width || y0 < 0f || y1 > Height;
        }

        private static Transform FindIn(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform f = FindIn(root.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        private static TMPro.TMP_Text TmpIn(Transform root, string parent, string leaf)
        {
            Transform p = FindIn(root, parent);
            if (p == null) return null;
            Transform l = p.Find(leaf);
            return l != null ? l.GetComponent<TMPro.TMP_Text>() : null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  계기탑 판독 — 글리프 화소와 시각 무게
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 계기탑의 1순위 숫자가 **각 포즈에서 몇 화소인가.** 「크게 만들었다」가 아니라
        /// 수치여야 한다 — 5차 평가가 「현재 계기 글자 A 13~18px 은 부족하다」고 적었다.
        /// </summary>
        private static void AppendColumnGlyphTable(Camera cam, StringBuilder man)
        {
            man.AppendLine("━━ 운행 계기탑 글리프 (글리프 상자 기준 · 한글 안정 판독 하한 16 px) ━━━━━━━━━━");
            GameObject col = GameObject.Find("ReferenceRoom/AscentColumn");
            if (col == null) { man.AppendLine("  ⚠ AscentColumn 이 없다"); return; }

            var all = new List<(string name, Vector3 eye, Vector3 look)>(Poses);
            all.AddRange(ExtraPoses);
            man.AppendLine("  포즈                  스핀숫자  층수숫자  캡션    전력줄  예비줄   탱크관 세로");
            foreach (var p in all)
            {
                Aim(cam, p.eye, p.look);
                man.AppendLine($"  {p.name,-22}{Px(cam, col, "SpinDrum", "Numeral"),-10}" +
                               $"{Px(cam, col, "AscentDrum", "Numeral"),-10}" +
                               $"{Px(cam, col, "SpinDrum", "Caption"),-8}" +
                               $"{Px(cam, col, "DataPlate", "PowerLine"),-8}" +
                               $"{Px(cam, col, "DataPlate", "ReserveLine"),-9}" +
                               $"{BorePx(cam, col)}");
            }
        }

        /// <summary>
        /// **전력 반복기가 각 포즈에서 실제로 몇 화소인가** (UP-FIX-92).
        ///
        /// 「C·E 에도 달았다」는 주장이 아니라 수치여야 한다. 여섯 라운드 동안 이 항목이
        /// 2점이었던 이유가 「그 화각에 전력 정보가 0개」였으므로, 반증은 **그 화각에서
        /// 잰 화소**뿐이다. 화각 밖이면 `화각밖` 이 찍힌다 — 프러스텀 확인이 붙어 있다.
        /// </summary>
        private static void AppendRepeaterTable(Camera cam, StringBuilder man)
        {
            man.AppendLine("━━ 전력 반복기 판독 (UP-FIX-92 · 한글 안정 판독 하한 16 px) ━━━━━━━━━━━━━━━");
            var view = Object.FindAnyObjectByType<View.AscentColumnView>(FindObjectsInactive.Include);
            if (view == null || view.RepeaterCount == 0)
            { man.AppendLine("  ⚠ 반복기가 배선되지 않았다"); return; }

            var all = new List<(string name, Vector3 eye, Vector3 look)>(Poses);
            all.AddRange(ExtraPoses);
            var hdr = new StringBuilder("  포즈                  ");
            for (int i = 0; i < view.RepeaterCount; i++)
                hdr.Append($"[{i}] 관세로   전력줄     ");
            man.AppendLine(hdr.ToString());
            foreach (var p in all)
            {
                Aim(cam, p.eye, p.look);
                var row = new StringBuilder($"  {p.name,-22}");
                for (int i = 0; i < view.RepeaterCount; i++)
                {
                    Transform rp = view.RepeaterFillPivot(i);
                    if (rp == null || rp.parent == null) { row.Append("-        "); continue; }
                    int bore = GrayBoreHeight(cam, rp);
                    Transform pl = rp.parent.Find("PowerLine");
                    var tmp = pl != null ? pl.GetComponent<TMPro.TMP_Text>() : null;
                    string linepx = "-";
                    if (tmp != null)
                    {
                        tmp.ForceMeshUpdate();
                        float h = 0f; int n = 0, clip = 0;
                        var info = tmp.textInfo;
                        for (int c = 0; c < info.characterCount; c++)
                        {
                            TMPro.TMP_CharacterInfo ch = info.characterInfo[c];
                            if (!ch.isVisible) continue;
                            Vector3 a = cam.WorldToScreenPoint(tmp.transform.TransformPoint(ch.bottomLeft));
                            Vector3 b = cam.WorldToScreenPoint(tmp.transform.TransformPoint(ch.topRight));
                            if (a.z <= 0f || b.z <= 0f) continue;
                            if (!BoxTouchesFrame(a, b)) continue;
                            if (BoxClipped(a, b)) { clip++; continue; }
                            h = Mathf.Max(h, Mathf.Abs(b.y - a.y)); n++;
                        }
                        linepx = n == 0 ? (clip > 0 ? "잘림" : "화각밖")
                               : clip > 0 ? $"{h:F0}px(잘림{clip})" : $"{h:F0}px";
                    }
                    row.Append($"{(bore <= 0 ? "화각밖" : bore + "px"),-10}{linepx,-12}");
                }
                man.AppendLine(row.ToString());
            }
            man.AppendLine("  ※ 관세로 = 탱크 안지름의 화면 세로. 채움 높이의 분모다.");
        }

        private static string Px(Camera cam, GameObject col, string parent, string leaf)
        {
            TMPro.TMP_Text t = TmpIn(col.transform, parent, leaf);
            if (t == null) return "-";
            t.ForceMeshUpdate();
            float h = 0f; int n = 0, clipped = 0;
            var info = t.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMPro.TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                Vector3 a = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.bottomLeft));
                Vector3 b = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.topRight));
                if (a.z <= 0f || b.z <= 0f) continue;
                if (!BoxTouchesFrame(a, b)) continue;   // 화면에 없는 글자를 판독 화소로 세지 않는다
                if (BoxClipped(a, b)) { clipped++; continue; }   // 잘린 글자는 판독 화소가 아니다
                h = Mathf.Max(h, Mathf.Abs(b.y - a.y)); n++;
            }
            if (n == 0) return clipped > 0 ? "잘림" : "화각밖";
            return clipped > 0 ? $"{h:F0}px(잘림{clipped})" : $"{h:F0}px";
        }

        private static string BorePx(Camera cam, GameObject col)
        {
            Transform pivot = FindIn(col.transform, "TankFillPivot");
            if (pivot == null) return "-";
            int h = GrayBoreHeight(cam, pivot);
            return h <= 0 ? "화각밖" : $"{h}px";
        }

        /// <summary>
        /// **시각 무게 비교** — 계기탑이 실행 레버 표찰보다 무거워지면 안 된다.
        ///
        /// `VISUAL_SPEC` §5 는 1순위가 「현재 사용 가능한 핵심 레버」다. 표시기를 더 밝고
        /// 크게 만들면 `UP-FIX-89`(위계 역전)·`UP-FIX-90`(방 안 최고 백색이 계기 텍스트)이
        /// 함께 악화된다 — 그래서 **기여 화소와 그 화소의 평균 휘도**를 둘 다 잰다.
        /// 차분 렌더라 가림·투명·포스트가 전부 포함된다.
        /// </summary>
        private static void AppendWeightTable(Camera cam, RenderTexture rt, StringBuilder man)
        {
            man.AppendLine("━━ 시각 무게 — 「표시기가 실행 레버보다 무거워지지 않았는가」 ━━━━━━━━━━━━━━━");
            man.AppendLine("  차분 렌더: 대상 렌더러를 켜고/끄고 두 번 찍어 달라진 화소를 센다.");
            man.AppendLine("  설정 잉크 = 씬에 저장된 TMP 색의 휘도 (렌더와 무관한 사실).");
            man.AppendLine("  렌더 평균/최대 = 기여 화소의 휘도 (0~1, sRGB 가중. post ON 이라 블룸 포함).");
            man.AppendLine("  ⚠ 렌더 평균만으로 비교하지 않는다 — 밝은 글자일수록 헤일로가 넓어져");
            man.AppendLine("     저휘도 가장자리 화소가 늘고 평균이 **내려간다**(실행 표찰이 그 예다).");
            man.AppendLine();

            var targets = new (string label, string[] paths)[]
            {
                ("실행 표찰(1순위)",   new[] { "GrayboxWorld/Car/Console/ExecutionLabel" }),
                ("탑 큰숫자 2개",      new[] { "ReferenceRoom/AscentColumn/SpinDrum/Numeral",
                                               "ReferenceRoom/AscentColumn/AscentDrum/Numeral" }),
                ("탑 케이스 전체",     new[] { "ReferenceRoom/AscentColumn" }),
                ("계기판 글자 5줄",    new[] { "GrayboxWorld/Car/InstrumentPanel/FloorLabel",
                                               "GrayboxWorld/Car/InstrumentPanel/PowerLabel",
                                               "GrayboxWorld/Car/InstrumentPanel/RequiredLabel",
                                               "GrayboxWorld/Car/InstrumentPanel/StatusLabel",
                                               "GrayboxWorld/Car/InstrumentPanel/CascadeLabel" }),
                ("과수확 표찰(3순위)", new[] { "GrayboxWorld/Car/OverharvestLever/OverharvestLabel" }),
            };

            foreach (var pose in new[] { "A_entry_to_machine", "D_wide_corner", "H_lever_column" })
            {
                (string name, Vector3 eye, Vector3 look) p = default;
                bool found = false;
                foreach (var q in Poses) if (q.name == pose) { p = q; found = true; }
                foreach (var q in ExtraPoses) if (q.name == pose) { p = q; found = true; }
                if (!found) continue;

                Aim(cam, p.eye, p.look);
                man.AppendLine($"  ── {pose} ──");
                man.AppendLine("     대상                  기여 화소   설정 잉크   렌더 평균   렌더 최대");
                foreach (var (label, paths) in targets)
                {
                    var rs = new List<Renderer>();
                    foreach (string path in paths)
                    {
                        GameObject go = GameObject.Find(path);
                        if (go != null) rs.AddRange(go.GetComponentsInChildren<Renderer>(true));
                    }
                    if (rs.Count == 0) { man.AppendLine($"     {label,-22}(없음)"); continue; }

                    Color32[] on = Shot(cam, rt);
                    var was = new bool[rs.Count];
                    for (int i = 0; i < rs.Count; i++) { was[i] = rs[i].enabled; rs[i].enabled = false; }
                    Color32[] off = Shot(cam, rt);
                    for (int i = 0; i < rs.Count; i++) rs[i].enabled = was[i];

                    int diff = 0; double sum = 0; float peak = 0f;
                    for (int i = 0; i < on.Length; i++)
                    {
                        int d = Mathf.Abs(on[i].r - off[i].r) + Mathf.Abs(on[i].g - off[i].g) + Mathf.Abs(on[i].b - off[i].b);
                        if (d <= 6) continue;
                        diff++;
                        float lum = (on[i].r * 0.2126f + on[i].g * 0.7152f + on[i].b * 0.0722f) / 255f;
                        sum += lum;
                        if (lum > peak) peak = lum;
                    }
                    // 「설정 잉크」는 **씬에 저장된 TMP 색의 휘도**다. 렌더 평균은 블룸이
                    // 만든 저휘도 가장자리 화소에 끌려 내려가므로(밝게 할수록 헤일로가 넓어져
                    // 평균이 **낮아진다**) 단독으로는 비교축이 못 된다. 세 축을 같이 낸다.
                    float set = 0f; int setN = 0;
                    foreach (string path in paths)
                    {
                        GameObject go = GameObject.Find(path);
                        if (go == null) continue;
                        foreach (var tm in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        { set += 0.2126f * tm.color.r + 0.7152f * tm.color.g + 0.0722f * tm.color.b; setN++; }
                    }
                    string setCol = setN == 0 ? "-" : (set / setN).ToString("F3");
                    man.AppendLine($"     {label,-22}{diff,10:N0}{setCol,12}{(diff > 0 ? (sum / diff) : 0),12:F3}{peak,12:F3}");
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  계기판 절단 — UP-FIX-88 을 수치로 남긴다
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>이 포즈에서 계기 글자 줄이 프레임 밖으로 나가는가. 나가면 몇 px 인가.</summary>
        private static string PanelClipNote(Camera cam)
        {
            float over = PanelOverflowPx(cam, out string worst);
            if (over <= 0f) return "   잘림 없음";
            return $"   ⚠ 우측 절단 {over:F0} px ({worst})";
        }

        /// <summary>
        /// 계기 라벨 다섯 줄 중 프레임 우단을 가장 많이 넘는 양(px). 0 이면 온전하다.
        /// **글리프 상자로 잰다** — `Renderer.bounds` 는 TMP 의 미사용 정점 때문에
        /// 허깨비를 포함한다(이 파일이 이미 두 번 적어 둔 함정).
        /// </summary>
        private static float PanelOverflowPx(Camera cam, out string worst)
        {
            worst = "-";
            GameObject panel = GameObject.Find("GrayboxWorld/Car/InstrumentPanel");
            if (panel == null) return 0f;
            float over = 0f;
            foreach (TMPro.TMP_Text t in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                t.ForceMeshUpdate();
                var info = t.textInfo;
                float xmax = float.MinValue;
                for (int i = 0; i < info.characterCount; i++)
                {
                    TMPro.TMP_CharacterInfo ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;
                    Vector3 a = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.bottomLeft));
                    Vector3 b = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.topRight));
                    if (a.z <= 0f || b.z <= 0f) continue;
                    xmax = Mathf.Max(xmax, Mathf.Max(a.x, b.x));
                }
                if (xmax <= float.MinValue + 1f) continue;
                float d = xmax - Width;
                if (d > over) { over = d; worst = t.name; }
            }
            return over;
        }

        /// <summary>
        /// 포즈별 계기판 절단량 표. `UP-FIX-88` 이 「고쳤다」인지 「줄었다」인지를
        /// 다음 라운드가 인상이 아니라 수치로 대조할 수 있게 남긴다.
        /// </summary>
        private static void AppendPanelClipTable(Camera cam, StringBuilder man,
                                                 List<(string name, Vector3 eye, Vector3 look)> extra)
        {
            man.AppendLine("━━ 계기판 우측 절단 (UP-FIX-88) — 글리프 상자 기준, 프레임 폭 1920 ━━━━━━━━━━");
            var all = new List<(string name, Vector3 eye, Vector3 look)>(Poses);
            all.AddRange(ExtraPoses);
            if (extra != null) all.AddRange(extra);
            foreach (var p in all)
            {
                Aim(cam, p.eye, p.look);
                float over = PanelOverflowPx(cam, out string worst);
                man.AppendLine($"  {p.name,-22}{(over <= 0f ? "온전" : $"우측으로 {over:F0} px 넘침 ({worst})")}");
            }
            man.AppendLine("  ⚠ B 는 구조적으로 담을 수 없는 화각이다 — 판독면 `Screen` 이 월드 X 0.983…1.773 인데");
            man.AppendLine("     이 포즈의 프레임 우단이 X 1.447 이다(눈 x −0.35, 수평 FOV 91.5°, 라벨 깊이 z 2.051).");
            man.AppendLine("     판독면의 59% 만 담긴다. 글자를 줄이면 `UP-FIX-86` 이 되돌아가므로 줄이지 않았다.");
            man.AppendLine("     계기판이 온전히 담기는 화각은 `G_gauge_face` 다.");
        }

        /// <summary>보이는 글리프의 화면 가로·세로 **최솟값**. 최악의 글자가 기준이다.</summary>
        private static void TMP_TextInfoScan(TMPro.TMP_Text t, Camera cam, ref float wMin, ref float hMin, ref int n)
        {
            var info = t.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMPro.TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                Vector3 a = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.bottomLeft));
                Vector3 b = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.topRight));
                Vector3 c = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.topLeft));
                Vector3 d = cam.WorldToScreenPoint(t.transform.TransformPoint(ch.bottomRight));
                if (a.z <= 0f || b.z <= 0f || c.z <= 0f || d.z <= 0f) continue;
                if (!BoxTouchesFrame(a, b) && !BoxTouchesFrame(c, d)) continue;   // 화각 밖은 제외
                float h = Mathf.Max(Mathf.Abs(b.y - a.y), Mathf.Abs(c.y - d.y));
                float w = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(c.x - d.x));
                if (h <= 0.01f) continue;
                wMin = Mathf.Min(wMin, w); hMin = Mathf.Min(hMin, h);
                n++;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  VISUAL_SPEC §12 수치 대역 — 매니페스트에 되살린다
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 한 프레임의 §12 지표. 계수와 정의는 `tools/measure_reference_band.py` 와 같다 —
        /// sRGB 인코딩된 값을 그대로 0..1 로 나눠 가중 휘도를 낸다(선형화하지 않는다).
        /// 파이썬과 다른 식을 쓰면 v7·v8 매니페스트의 수치와 비교가 성립하지 않는다.
        /// </summary>
        private struct Band { public float mean, p50, p99, darkPct, brightPct, gr, br; }

        /// <summary>
        /// 🔴 **3차 평가가 §12 를 「판정 불가」로 남긴 이유가 이 표의 부재였다.**
        /// 평가자에게 화소 통계 도구가 없고, 없으면 눈으로 대체하는 대신 판정을 비운다.
        /// 그래서 캡처 하네스가 직접 낸다 — 다음 라운드가 다시 비지 않도록.
        /// </summary>
        private static Band Measure(Color32[] px)
        {
            const int Bins = 4096;
            var hist = new int[Bins];
            double sum = 0, rs = 0, gs = 0, bs = 0;
            int dark = 0, bright = 0;
            for (int i = 0; i < px.Length; i++)
            {
                float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f;
                float lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                sum += lum; rs += r; gs += g; bs += b;
                if (lum < 0.02f) dark++;
                if (lum > 0.50f) bright++;
                hist[Mathf.Clamp((int)(lum * (Bins - 1) + 0.5f), 0, Bins - 1)]++;
            }
            var band = new Band
            {
                mean = (float)(sum / px.Length),
                darkPct = 100f * dark / px.Length,
                brightPct = 100f * bright / px.Length,
                gr = (float)(gs / System.Math.Max(rs, 1e-9)),
                br = (float)(bs / System.Math.Max(rs, 1e-9)),
                p50 = Percentile(hist, px.Length, 0.50f, Bins),
                p99 = Percentile(hist, px.Length, 0.99f, Bins),
            };
            return band;
        }

        private static float Percentile(int[] hist, int total, float q, int bins)
        {
            int want = Mathf.Clamp(Mathf.RoundToInt(total * q), 1, total);
            int acc = 0;
            for (int i = 0; i < bins; i++)
            {
                acc += hist[i];
                if (acc >= want) return i / (float)(bins - 1);
            }
            return 1f;
        }

        /// <summary>
        /// v8 매니페스트와 **같은 표 모양**으로 낸다. 모양이 다르면 라운드 간 대조가
        /// 사람 손을 타고, 그 순간부터 「비교했다」가 근거가 아니게 된다.
        /// 평균 열은 v7·v8 이 쓴 A/C/D 세 뷰다 — 그래야 그 숫자들과 직접 비교된다.
        /// </summary>
        private static void AppendBandTable(StringBuilder man, List<(string name, Band b)> bands)
        {
            man.AppendLine("━━ VISUAL_SPEC §12 수치 대역 (post ON · 저장소 규약 = measure_reference_band.py 와 동일 식) ━━");
            man.AppendLine("  뷰                   mean     p50      p99      <0.02%   >0.50%   g/r      b/r");
            foreach (var (name, b) in bands)
                man.AppendLine($"  {name,-20}{b.mean,-9:F4}{b.p50,-9:F4}{b.p99,-9:F4}" +
                               $"{b.darkPct,-9:F2}{b.brightPct,-9:F2}{b.gr,-9:F4}{b.br,-9:F4}");

            var avg = new Band();
            int n = 0;
            foreach (var (name, b) in bands)
            {
                if (name[0] != 'A' && name[0] != 'C' && name[0] != 'D') continue;
                avg.mean += b.mean; avg.p50 += b.p50; avg.p99 += b.p99;
                avg.darkPct += b.darkPct; avg.brightPct += b.brightPct;
                avg.gr += b.gr; avg.br += b.br; n++;
            }
            if (n == 0) return;
            avg.mean /= n; avg.p50 /= n; avg.p99 /= n;
            avg.darkPct /= n; avg.brightPct /= n; avg.gr /= n; avg.br /= n;

            man.AppendLine();
            man.AppendLine($"  A/C/D 평균 ({n} 뷰) — v7·v8 매니페스트와 같은 집계축");
            man.AppendLine("  지표      값        허용 대역          판정");
            man.AppendLine(Row("mean", avg.mean, 0.055f, 0.075f, "F4"));
            man.AppendLine(Row("p50", avg.p50, 0.040f, 0.062f, "F4"));
            man.AppendLine(Row("p99", avg.p99, 0.25f, 0.36f, "F4"));
            man.AppendLine(Row("<0.02%", avg.darkPct, 0f, 32.0f, "F2"));
            man.AppendLine(Row(">0.50%", avg.brightPct, 0f, 0.5f, "F2"));
            man.AppendLine(Row("g/r", avg.gr, 0.78f, 0.90f, "F4"));
            man.AppendLine(Row("b/r", avg.br, 0.45f, 0.62f, "F4"));
            man.AppendLine("  (v8: mean .0535 · p50 .0416 · p99 .1839 · <0.02 18.06 · >0.50 0.15 · g/r .8763 · b/r .6032)");
        }

        private static string Row(string label, float v, float lo, float hi, string fmt)
        {
            bool ok = v >= lo - 1e-6f && v <= hi + 1e-6f;
            string band = lo <= 0f ? $"≤ {hi.ToString(fmt)}" : $"{lo.ToString(fmt)} ~ {hi.ToString(fmt)}";
            return $"  {label,-10}{v.ToString(fmt),-10}{band,-19}{(ok ? "안" : "밖")}";
        }

        /// <summary>
        /// **C 포즈가 무엇을 보고 있는지 매니페스트에 못박는다.**
        ///
        /// 3차 평가가 잡은 회귀(가위문 너머가 닫힌 셔터)는 A·D 화소 통계로는 안 잡혔다.
        /// 구현자의 복구 검증표에 C 가 없었기 때문이다. 그래서 「승강로가 열려 있는가」를
        /// **문장이 아니라 상태값으로** 매니페스트에 남긴다 — 다음 라운드가 표만 보고도
        /// 회귀를 알 수 있어야 한다.
        /// </summary>
        private static void AppendShaftNote(StringBuilder man)
        {
            man.AppendLine("━━ 승강로 개구부 (C_toward_gate 가 보는 것) ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            GameObject shaft = null;
            foreach (GameObject g in UnityEditor.SceneManagement.EditorSceneManager
                                                .GetActiveScene().GetRootGameObjects())
                if (g.name == "CabinShaft") shaft = g;

            Transform backdrop = null;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "ShaftBackdrop") backdrop = t;

            man.AppendLine($"  CabinShaft            {(shaft == null ? "없음" : "있음 · 활성 " + shaft.activeInHierarchy)}");
            man.AppendLine($"  ShaftBackdrop(막이판)  {(backdrop == null ? "없음" : (backdrop.gameObject.activeInHierarchy ? "🔴 활성 — 승강로가 막혀 있다" : "비활성 — 통로가 열려 있다"))}");
            if (shaft != null)
                foreach (Light l in shaft.GetComponentsInChildren<Light>(true))
                    man.AppendLine($"  통로 광원 {l.name,-16} 활성 {l.gameObject.activeInHierarchy} · " +
                                   $"세기 {l.intensity:F2} · 사거리 {l.range:F2} · 위치 ({l.transform.position.x:F2}, {l.transform.position.y:F2}, {l.transform.position.z:F2})");
        }

        private struct CellStat { public int lit, blobs, w, h, largest, peak; public float fill; }

        /// <summary>
        /// 창 하나를 잰다. 임계값은 **창 안의 최대 휘도에 상대적**이다 —
        /// 절대 임계를 쓰면 노출이 조금만 달라져도 셋 다 0 이 되거나 셋 다 꽉 찬다.
        /// </summary>
        private static CellStat Analyze(byte[] gray, int cx, int cy, int rad)
        {
            int x0 = Mathf.Clamp(cx - rad, 0, Width - 1), x1 = Mathf.Clamp(cx + rad, 0, Width - 1);
            int y0 = Mathf.Clamp(cy - rad, 0, Height - 1), y1 = Mathf.Clamp(cy + rad, 0, Height - 1);
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var stat = new CellStat();
            if (w <= 1 || h <= 1) return stat;

            // 원형 마스크. 사각으로 자르면 모서리가 클램프 링에 걸린다.
            int r2 = rad * rad;
            bool Inside(int x, int y)
            {
                int dx = x0 + x - cx, dy = y0 + y - cy;
                return dx * dx + dy * dy <= r2;
            }

            // 🔴 임계값을 「최대의 45%」로 잡았더니 **유리 원판**이 통째로 잡혔다.
            // 유리는 챔버보다 밝은 중간 회색이라 그 문턱을 그냥 넘는다 — 재고 있던 것이
            // 심볼이 아니라 창이었고, 그래서 아홉 칸의 bbox 가 전부 91x91(창 지름)이었다.
            //
            // 창 안 화소의 **중앙값**이 곧 유리의 값이다(면적을 유리가 지배한다).
            // 중앙값과 최대의 중간을 문턱으로 삼으면 유리는 빠지고 심볼만 남는다.
            byte peak = 0;
            var hist = new int[256];
            int count = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Inside(x, y)) continue;
                    byte g = gray[(y0 + y) * Width + (x0 + x)];
                    hist[g]++; count++;
                    if (g > peak) peak = g;
                }
            stat.peak = peak;
            int median = 0, acc = 0;
            for (int i = 0; i < 256 && acc < count / 2; i++) { acc += hist[i]; median = i; }
            int thr = Mathf.Max(24, median + Mathf.RoundToInt((peak - median) * 0.50f));

            var mask = new bool[w * h];
            int bx0 = int.MaxValue, by0 = int.MaxValue, bx1 = int.MinValue, by1 = int.MinValue;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Inside(x, y)) continue;
                    if (gray[(y0 + y) * Width + (x0 + x)] < thr) continue;
                    mask[y * w + x] = true;
                    stat.lit++;
                    if (x < bx0) bx0 = x; if (x > bx1) bx1 = x;
                    if (y < by0) by0 = y; if (y > by1) by1 = y;
                }
            if (stat.lit == 0) return stat;

            stat.w = bx1 - bx0 + 1;
            stat.h = by1 - by0 + 1;
            stat.fill = stat.lit / (float)(stat.w * stat.h);

            // 연결 성분. 잡음 덩어리를 세지 않도록 최소 크기를 둔다.
            int minBlob = Mathf.Max(12, stat.lit / 24);
            var seen = new bool[w * h];
            var stack = new Stack<int>();
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i] || seen[i]) continue;
                int size = 0;
                stack.Push(i); seen[i] = true;
                while (stack.Count > 0)
                {
                    int p = stack.Pop(); size++;
                    int px = p % w, py = p / w;
                    if (px > 0 && mask[p - 1] && !seen[p - 1]) { seen[p - 1] = true; stack.Push(p - 1); }
                    if (px < w - 1 && mask[p + 1] && !seen[p + 1]) { seen[p + 1] = true; stack.Push(p + 1); }
                    if (py > 0 && mask[p - w] && !seen[p - w]) { seen[p - w] = true; stack.Push(p - w); }
                    if (py < h - 1 && mask[p + w] && !seen[p + w]) { seen[p + w] = true; stack.Push(p + w); }
                }
                if (size >= minBlob) stat.blobs++;
                if (size > stat.largest) stat.largest = size;
            }
            return stat;
        }

        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ⚠ **새로 만든 카메라는 `renderPostProcessing` 기본값이 false 다.**
        /// 2026-08-04 에 그것 때문에 기준선 캡처 세 벌이 post OFF 로 찍혔고,
        /// 같은 씬에서 post ON 과 2.1배 차이가 났다 — 평가 대상이 플레이어가 보는
        /// 화면이 아니었다. 씬 카메라의 설정을 그대로 복사한다.
        /// </summary>
        private static Camera MakeCamera(out UniversalAdditionalCameraData data)
        {
            var go = new GameObject("~ProbeCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            Camera src = Camera.main;
            if (src == null)
                foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                { src = c; break; }

            if (src != null)
            {
                cam.CopyFrom(src);
                var srcData = src.GetUniversalAdditionalCameraData();
                data = cam.GetUniversalAdditionalCameraData();
                if (srcData != null && data != null)
                {
                    data.renderPostProcessing = srcData.renderPostProcessing;
                    data.antialiasing = AntialiasingMode.None;
                    data.volumeLayerMask = srcData.volumeLayerMask;
                }
            }
            else data = cam.GetUniversalAdditionalCameraData();

            cam.fieldOfView = FovVertical;
            cam.aspect = Width / (float)Height;
            cam.targetTexture = null;
            cam.enabled = false;
            return cam;
        }

        private static void Aim(Camera cam, Vector3 eye, Vector3 look)
        {
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
        }

        private static Color32[] Shot(Camera cam, RenderTexture rt)
        {
            RenderTexture prev = RenderTexture.active;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply(false);
            Color32[] px = tex.GetPixels32();
            RenderTexture.active = prev;
            Object.DestroyImmediate(tex);
            return px;
        }

        /// <summary>sRGB 가중 휘도. 이 저장소의 보고 규약과 같은 계수다.</summary>
        private static byte[] ToGray(Color32[] px)
        {
            var g = new byte[px.Length];
            for (int i = 0; i < px.Length; i++)
                g[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(px[i].r * 0.2126f + px[i].g * 0.7152f + px[i].b * 0.0722f), 0, 255);
            return g;
        }

        private static void SavePng(Color32[] px, string relative)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(px); tex.Apply(false);
            Write(tex.EncodeToPNG(), relative);
            Object.DestroyImmediate(tex);
        }

        private static void SaveGrayPng(byte[] gray, string relative)
        {
            var px = new Color32[gray.Length];
            for (int i = 0; i < gray.Length; i++) px[i] = new Color32(gray[i], gray[i], gray[i], 255);
            SavePng(px, relative);
        }

        private static void Write(byte[] bytes, string relative)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes);
        }

        private static string Leaf(string path) => path.Substring(path.LastIndexOf('/') + 1);
    }
}
