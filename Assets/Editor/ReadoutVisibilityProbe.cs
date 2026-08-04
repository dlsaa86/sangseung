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

            const string dir = "Captures/symbols_v1_20260804";
            var log = new StringBuilder($"[상승] === 고정 캡처 세트 → {dir} ===\n");
            Camera cam = MakeCamera(out UniversalAdditionalCameraData data);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            cam.targetTexture = rt;

            var man = new StringBuilder();
            man.AppendLine("symbols_v1_20260804 capture manifest");
            man.AppendLine($"resolution {Width}x{Height}  fovVertical {FovVertical}  antialiasing None  " +
                           $"RT ARGB32 sRGB  MSAA off  post {(data != null && data.renderPostProcessing ? "ON" : "OFF")}");
            man.AppendLine($"machineFingerprint {SystemInfo.operatingSystemFamily}|{SystemInfo.graphicsDeviceType}|" +
                           $"{SystemInfo.graphicsDeviceName}|{Application.unityVersion}");
            man.AppendLine("gray/ 는 같은 프레임의 sRGB 가중 휘도(0.2126/0.7152/0.0722) 변환이다.");
            man.AppendLine();

            foreach (var pose in Poses)
            {
                Aim(cam, pose.eye, pose.look);
                Color32[] px = Shot(cam, rt);
                SavePng(px, $"{dir}/{pose.name}.png");
                SaveGrayPng(ToGray(px), $"{dir}/gray/{pose.name}.png");
                man.AppendLine($"{pose.name}  eye=({pose.eye.x:F2}, {pose.eye.y:F2}, {pose.eye.z:F2}) " +
                               $"lookAt=({pose.look.x:F2}, {pose.look.y:F2}, {pose.look.z:F2})");
            }

            // 결과판 정면 — 심볼 3종 판정의 본 그림.
            GameObject grid = GameObject.Find("SoulMachineFrame/WindowGrid");
            if (grid != null)
            {
                Vector3 c = Vector3.zero; int n = 0;
                foreach (Transform m in grid.transform) { c += m.position; n++; }
                if (n > 0) c /= n;
                float need = Art.ReferenceRoomSpec.MachineHeight * 1.25f;
                float dist = need * 0.5f / Mathf.Tan(FovVertical * 0.5f * Mathf.Deg2Rad);
                Vector3 eye = c + new Vector3(0f, 0f, -dist);
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

            Write(Encoding.UTF8.GetBytes(man.ToString()), $"{dir}/manifest.txt");
            log.Append(man);

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cam.gameObject);
            Debug.Log(log.ToString());
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
