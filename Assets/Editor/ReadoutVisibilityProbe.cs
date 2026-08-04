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
        };

        /// <summary>
        /// **전력 사다리** (`P-20260804-05` 권장안 B의 증거).
        ///
        /// 4차 평가의 유일한 2점 항목은 「기계 벽을 등지면 전력을 알 수 없다」였다.
        /// 채택안은 「천장등 색·밝기가 달성률을 따라간다」인데, **정지 이미지 한 장으로는
        /// 그것을 판정할 수 없다** — 한 장은 「이 방은 이런 색이다」밖에 말하지 않는다.
        /// 그래서 등을 돌린 두 포즈(C·E)를 달성률만 바꿔 여러 장 찍는다.
        /// 대조군이 있어야 「환경이 전력을 나른다」가 반증 가능한 주장이 된다.
        ///
        /// `p100` 이 **중립점**이다 — 프로파일이 r = 1 에서 항등이라 이 장은 기존
        /// `C_toward_gate`·`E_contract_wall` 과 같은 조명이어야 한다. 다르면 배선이 틀린 것이다.
        ///
        /// 마지막 칸은 **우선순위 증거**다. 같은 240% 인데 위험이 Collapse 이면
        /// 전력 채널의 권한이 0 이 되어 등이 위험 조명으로 돌아간다 —
        /// 「위험 조명이 우선」이라는 요구가 화면에서 확인된다.
        /// </summary>
        private static readonly (string suffix, float ratio, Risk.RiskLevel level)[] PowerLadder =
        {
            ("p000",          0.00f, Risk.RiskLevel.Stable),
            ("p060",          0.60f, Risk.RiskLevel.Stable),
            ("p100",          1.00f, Risk.RiskLevel.Stable),
            ("p240",          2.40f, Risk.RiskLevel.Stable),
            ("p240_collapse", 2.40f, Risk.RiskLevel.Collapse),
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

            const string dir = "Captures/symbols_v3_20260804";
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
            man.AppendLine("symbols_v3_20260804 capture manifest");
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

            // 전력 사다리. **밴드 집계에 넣지 않는다** — 접두어가 A/C/D 가 아니므로
            // `AppendBandTable` 의 평균이 오염되지 않는다(그 함수가 이름 첫 글자로 고른다).
            man.AppendLine();
            CapturePowerLadder(cam, rt, dir, man);

            man.AppendLine();
            AppendBandTable(man, bands);
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
        //  전력 사다리 — 「등을 돌려도 전력을 아는가」의 증거 (P-20260804-05 B)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 등을 돌린 두 포즈를 **달성률만 바꿔** 여러 장 찍는다.
        ///
        /// 세 가지를 반드시 지킨다.
        ///   ① 찍기 전에 광원의 원래 값을 **그대로 붙잡고** 끝나면 되돌린다.
        ///      되돌리지 않으면 사다리 마지막 칸(240% 의 붉은 등)이 씬에 저장되고
        ///      다음 캡처 전체가 오염된 조명으로 찍힌다.
        ///   ② 되돌린 값을 **다시 재서** 매니페스트에 적는다. 「되돌렸다」는 주장이
        ///      아니라 수치여야 한다.
        ///   ③ 미리보기는 `RenderSettings.ambientLight` 를 건드리지 않는다
        ///      (`RiskStateView.PreviewPowerAmbience` 의 주석 참조). 그 값은 렌더 설정
        ///      **전역**이라 에디트 모드에서 쓰면 그대로 저장된다.
        /// </summary>
        private static void CapturePowerLadder(Camera cam, RenderTexture rt, string dir, StringBuilder man)
        {
            var risk = Object.FindAnyObjectByType<Risk.RiskStateView>(FindObjectsInactive.Include);
            if (risk == null)
            {
                man.AppendLine("━━ 전력 사다리 ━━ ⚠ RiskStateView 가 없다 — 찍지 않았다");
                return;
            }

            Light lamp = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.transform.parent != null && l.transform.parent.name == "CeilingLamp"
                    && l.transform.parent.parent != null && l.transform.parent.parent.name == "ReferenceRoom")
                { lamp = l; break; }

            Color lampColor0 = lamp != null ? lamp.color : Color.white;
            float lampIntensity0 = lamp != null ? lamp.intensity : 0f;
            Color ambient0 = RenderSettings.ambientLight;

            // 🔴 **자기 출력 폴더를 먼저 비운다.** 사다리 구성(프리셋·단계)이 바뀌면
            // 옛 프레임이 남고, 증거 폴더에 남은 옛 프레임은 「지금의 화면」으로 읽힌다.
            // 이 저장소가 유령 서브메시로 이미 한 번 당한 것과 같은 종류의 거짓 증거다.
            string ladderDir = $"{dir}/power_ladder";
            if (Directory.Exists(ladderDir)) Directory.Delete(ladderDir, true);

            risk.EnsureProfilesLoaded();   // 에디트 모드에는 Awake 가 없다 — 출처가 「(미초기화)」로 실린다

            man.AppendLine("━━ 전력 사다리 — 등을 돌린 화각에서 달성률이 읽히는가 (P-20260804-05 B) ━━━━━━━");
            man.AppendLine($"  씬에 배선된 프로파일 {risk.PowerAmbienceSource}");
            man.AppendLine("  프리셋 두 벌을 나란히 찍는다 — 전력 환경 강도는 승인 대기 항목이라");
            man.AppendLine("  하나로 잠그지 않는다(`VISUAL_SPEC` §11). 글로 적으면 승인자가 고를 수 없다.");
            man.AppendLine();
            man.AppendLine("  ⚠ `p240_collapse` 는 **우선순위 증거**다 — 같은 240% 라도 위험이 Collapse 면");
            man.AppendLine("     전력 채널 권한이 0 이 되어 등이 위험 조명으로 돌아간다(권한 열이 0.00).");
            man.AppendLine("  ⚠ `p100` 은 **중립점**이다 — 프로파일이 r = 1 에서 항등이므로 이 장의 등 색·세기는");
            man.AppendLine("     프리셋과 무관하게 같아야 한다(두 표의 p100 행이 일치하는지로 확인한다).");
            man.AppendLine("     단 같은 포즈의 **고정 캡처**와는 미세하게 다르다 — 씬에 직렬화된 등 색");
            man.AppendLine("     (1.000,0.790,0.550)과 런타임이 계산하는 등 색(0.948,0.812,0.621)이 원래부터");
            man.AppendLine("     다르기 때문이다(이번 변경 이전부터 그랬다. 아래 「알려진 불일치」 참조).");
            man.AppendLine();

            var ladderPoses = new List<(string name, Vector3 eye, Vector3 look)>();
            foreach (var p in Poses)
                if (p.name == "C_toward_gate" || p.name == "E_contract_wall") ladderPoses.Add(p);

            var presets = new[]
            {
                Data.Profiles.PowerAmbienceIntensity.Standard,
                Data.Profiles.PowerAmbienceIntensity.Heavy,
            };

            foreach (var preset in presets)
            {
                risk.OverridePowerAmbienceForPreview(preset);
                string folder = preset.ToString().ToLowerInvariant();
                man.AppendLine($"  ── 프리셋 {preset} ({Data.Profiles.PowerAmbienceProfile.PresetDisplayName(preset)}) " +
                               $"→ power_ladder/{folder}/");
                man.AppendLine("  파일                              달성률  위험     등세기  등색(R,G,B)          권한  mean    g/r     b/r");

                foreach (var pose in ladderPoses)
                {
                    foreach (var step in PowerLadder)
                    {
                        risk.PreviewPowerAmbience(step.ratio, step.level);
                        Aim(cam, pose.eye, pose.look);
                        Color32[] px = Shot(cam, rt);
                        string leaf = $"{pose.name}_{step.suffix}";
                        SavePng(px, $"{dir}/power_ladder/{folder}/{leaf}.png");
                        SaveGrayPng(ToGray(px), $"{dir}/power_ladder/{folder}/gray/{leaf}.png");
                        Band b = Measure(px);
                        Color c = risk.EffectiveLampColor;
                        man.AppendLine($"  {leaf,-34}{step.ratio,6:P0}  {step.level,-8}" +
                                       $"{risk.EffectiveLampIntensity,7:F3}  " +
                                       $"({c.r:F3},{c.g:F3},{c.b:F3})  {risk.PowerAuthority,5:F2}  " +
                                       $"{b.mean,-8:F4}{b.gr,-8:F4}{b.br,-8:F4}");
                    }
                }
                man.AppendLine();
            }

            // 되돌린다. 그리고 되돌아왔는지 **잰다.**
            risk.RestorePowerAmbiencePreview();
            if (lamp != null)
            {
                lamp.color = lampColor0;
                lamp.intensity = lampIntensity0;
            }
            RenderSettings.ambientLight = ambient0;

            man.AppendLine($"  복원 뒤 프로파일 출처 {risk.PowerAmbienceSource} (미리보기 프리셋이 남아 있으면 여기 드러난다)");
            man.AppendLine();
            man.AppendLine("  ── 알려진 불일치 (이번 변경이 만든 것이 아니다. 발견해서 적는다) ──────────");
            man.AppendLine("  씬에 직렬화된 `CabinLight.color` 는 (1.000, 0.790, 0.550) 인데, 런타임");
            man.AppendLine("  `RiskStateView.ApplyLighting` 이 Stable 에서 계산하는 색은");
            man.AppendLine("  Lerp(필라멘트 (1.00,0.78,0.46), Stable (0.85,0.87,0.92), 0.35) = (0.948,0.812,0.621) 이다.");
            man.AppendLine("  즉 **에디트 모드 고정 캡처의 등 색은 플레이 모드가 내는 색이 아니다.**");
            man.AppendLine("  배선기는 세기(`_baseLightIntensity`)만 광원과 동기화하고 색은 하지 않는다.");
            man.AppendLine("  이번 라운드 범위 밖이라 고치지 않았다 — 고치면 A~F 전부의 §12 수치가 움직인다.");
            man.AppendLine();
            man.AppendLine($"  복원 확인 — 등 세기 {lampIntensity0:F4} → {(lamp != null ? lamp.intensity : 0f):F4} · " +
                           $"등색 ({lampColor0.r:F4},{lampColor0.g:F4},{lampColor0.b:F4}) → " +
                           $"({(lamp != null ? lamp.color.r : 0f):F4},{(lamp != null ? lamp.color.g : 0f):F4},{(lamp != null ? lamp.color.b : 0f):F4}) · " +
                           $"앰비언트 {ambient0.r:F4},{ambient0.g:F4},{ambient0.b:F4} → " +
                           $"{RenderSettings.ambientLight.r:F4},{RenderSettings.ambientLight.g:F4},{RenderSettings.ambientLight.b:F4}");
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
