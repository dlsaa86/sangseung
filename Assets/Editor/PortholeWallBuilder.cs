using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ascend.Prototype.Art;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// **원형 현창 3×3 장치벽을 씬에 앉히는 멱등 조립기** (`G-SLOT` · `UP-FIX-GLASS`).
    ///
    /// `PortholeMesh` 는 형상만 만든다 — 어디에 놓을지, 무슨 재질을 물릴지, 심볼 27개를
    /// 어떻게 앉힐지는 씬 소유자의 몫이고 그것이 이 파일이다.
    ///
    /// ── 왜 이것이 필요한가 ──────────────────────────────────────────────────
    ///
    /// `GRAPHICS_TARGET.md` §9 — G-SLOT 미달 7장의 원인이 **7/7 전부 「띠」**다.
    /// 세로로 길고 끊기지 않은 덩어리가 「슬롯머신 릴」의 화소적 정의이고,
    /// 지금 장치는 **세로 통관 3개**라 형상 자체가 그 정의를 만족한다.
    /// 색·재질로는 못 고친다. 형상을 바꿔야 한다.
    ///
    /// 그리고 §11 이 지목한 `03`·`04`·`24` 의 「밝은 무텍스처 면」은 **유리가 아니었다.**
    /// 렌더러를 하나씩 끄고 재서 확인했다(2026-08-02, 이 파일을 쓰기 전) —
    ///
    /// | 장 | 기준 밝은화소 | 심볼 27개 숨김 | 유리 3장 숨김 |
    /// |---|---|---|---|
    /// | `03_device_side` | 7.65% | **0.37%** | 12.70% (**올라간다**) |
    /// | `04_symbols`     | 10.18% | **0.79%** | 14.64% (**올라간다**) |
    ///
    /// 밝은 화소의 **95%가 심볼 프리미티브**다. 유리는 이미 `TEX_Glass_Smudged` 가
    /// 물려 있고(ST 1.5×6.3 · alpha 0.18) 오히려 뒤의 심볼을 **어둡게 만들고 있었다.**
    /// 그래서 이 작업이 `03`·`04`·`24` 를 고치는 방식은 「유리에 무늬를 넣는 것」이 아니라
    /// **심볼을 우물 안으로 밀어 넣고 0.17→0.11 로 줄이는 것**이다.
    ///
    /// ── 이 파일이 지키는 것 ─────────────────────────────────────────────────
    ///
    /// **1. 전부 절대값이다.** 두 번 돌리면 같은 결과가 나온다. 스케일·위치를
    ///    현재 값에서 유도하지 않는다 — `Reproportion Elevator Car` 가 계기판을
    ///    1/0.66 씩 두 번 민 실패가 그 반대편이다.
    ///
    /// **2. 배선을 건드리지 않는다.** `SpinBoardView._cells` 가 가리키는
    ///    `Tube_c/Cell_r` 트랜스폼을 **그대로 두고 위치만 옮긴다.** 계층을 다시 짜면
    ///    fileID 참조 9개가 전부 바뀌고, 그 재작성은 조용히 깨지는 종류다.
    ///
    /// **3. 열 순서를 뒤집지 않는다.** 판 로컬 +X 는 Y 90° 회전에서 월드 −Z 로 간다.
    ///    씬은 `Tube_0` 이 z=−0.50(플레이어 기준 **왼쪽**)이고 캡처 `04` 의 설명문이
    ///    「왼쪽 열 정상 영혼」이라고 적는다. 그래서 **씬 열 c 는 판 열 (2−c)** 에 앉는다.
    ///    이 한 줄이 없으면 결과판이 좌우로 뒤집히고 매니페스트 설명이 거짓이 된다.
    ///
    /// **4. 콜라이더를 죽이지 않는다.** `TubeFrame` 은 `Renderer` 만 끈다.
    ///    `GameObject.SetActive(false)` 로 끄면 박스 콜라이더까지 사라진다.
    /// </summary>
    public static class PortholeWallBuilder
    {
        // ══ 배치 상수 — 전부 실측에서 나왔다 ══════════════════════════════════

        /// <summary>
        /// 판 **뒷면**의 월드 x.
        ///
        /// ── 처음 −0.920 으로 잡았다가 −0.980 으로 물렀다. 이유를 남긴다 ─────────
        ///
        /// −0.920 은 심볼이 지금 서 있는 x(−0.840)를 **한 밀리도 안 옮기는** 값이었다.
        /// 판독 거리 보존이라는 점에서 옳았지만, 그 자세에서 캐비닛 앞끝이 x = −0.685 로
        /// 옛 통관 앞면(−0.800)보다 **0.115 m 더 튀어나온다.**
        ///
        /// 그리고 그 0.115 가 `03_device_side`·`08` 자세에서 **계기판 글자의 앞 1~2자를
        /// 먹었다.** 렌더로 확인했다 — 「전력 1616」이 「력 1616」, 「스핀 4/5」가 「핀 4/5」로
        /// 나왔다. 이건 8·9차 판정이 다섯 라운드에 걸쳐 고친 축(`UP-FIX-23`)을 되돌리는
        /// 것이고, `_leftPadUnits = 5.20` 이 딛고 선 전제를 깨는 것이다.
        ///
        /// 기하가 강경하다. 그 자세의 시선은 캐비닛 앞끝 x 에서 z ≈ −0.80 + 2.549·(−0.20 − x)
        /// 를 지난다. 캐비닛 +z 끝(0.72)을 넘으려면 **앞끝이 x ≤ −0.795** 여야 한다.
        /// 폭을 줄여서는 못 맞춘다 — 피치를 0.35 아래로 내려야 하고 그건 격자를 죽인다.
        ///
        /// 그래서 판을 뒤로 민다. 대가는 심볼이 −0.840 → **−0.900** 으로 0.060 m 멀어지는
        /// 것이다. `02` 시점(카메라 x 0.35) 기준 겉보기 크기가 64.7% → **61.6%** 로
        /// 3.1%p 더 줄어든다. 글자 두 자를 지우는 것보다 이쪽이 싸다.
        /// </summary>
        public const float PanelBackX = -0.980f;

        /// <summary>격자 중심의 월드 y. 눈높이 1.62 와 맞춘 기존 결과판 중심을 그대로 쓴다.</summary>
        public const float GridCenterY = 1.600f;

        /// <summary>격자 중심의 월드 z.</summary>
        public const float GridCenterZ = 0.000f;

        /// <summary>
        /// 테두리 폭. 기본값 0.140 이 아니라 **0.060** 이다.
        ///
        /// 캐비닛 반폭 = 1.5·피치 + 이 값 = 0.72 이고, 그것이 곧 계기판 라벨을 가리는
        /// 실루엣의 폭이다. 옛 통관 바깥끝이 z ±0.67 이었고 `_leftPadUnits = 5.20` 은
        /// 그 실루엣을 **아슬아슬하게** 비켜 가도록 잡힌 값이다(실측 여유 0.011 m).
        /// 0.140 이면 ±0.80 이 되어 그 여유가 사라진다.
        ///
        /// 부수 효과: `ExecutionLever`(z −1.015…−0.745)와의 z 간격이 0.005 → **0.025 m**.
        ///
        /// ⚠ 세로에도 같은 값이 쓰인다(위 테두리 + 아래 해치 띠).
        /// </summary>
        public const float FrameMargin = 0.060f;

        /// <summary>
        /// 테두리 밴드가 판 앞으로 나오는 두께. 기본값 0.050 이 아니라 **0.022** 다.
        ///
        /// 이 값이 캐비닛의 **가장 앞선 세로 실루엣**을 정한다(해치는 y 0.54…0.94 라
        /// 계기 글자와 높이가 다르다). 0.050 이면 밴드 앞끝이 x = −0.795 이고 그 자리에서
        /// 시선은 z = 0.717 < 0.72 로 **캐비닛 안**이다. 0.022 면 앞끝 −0.823, 시선
        /// z = 0.791 로 +z 끝(0.72)을 **0.071 m 여유로 넘긴다.**
        ///
        /// 리벳이 그 위에 9 mm 더 서지만(−0.814 · 시선 z 0.766) 그것도 넘는다.
        /// </summary>
        public const float FrameDepth = 0.022f;

        /// <summary>
        /// 심볼 한 변(m). <see cref="PortholeMesh.SymbolFitSize"/> 의 깊이 한계가
        /// 0.115 이고 <see cref="Ascend.Prototype.View.SpinBoardView"/> 의 정화 점등이
        /// 최대 1.35 배로 부풀린다. 0.110 × 1.35 = 0.1485 → 앞끝 z 0.154 로,
        /// 유리 **앞면**(0.165)은 넘지 않는다. 즉 점등 정점에도 유리 밖으로 나오지 않는다.
        /// </summary>
        public const float SymbolSize = 0.110f;

        // ── 재질 ────────────────────────────────────────────────────────────
        //
        // ⚠ **명도는 「최종 반사율」로 준다.** `Ascend/Stylized` 도 `URP/Lit` 도
        //   `_BaseMap` 을 `_BaseColor` 에 곱하므로, `_BaseColor` 를 그대로 적으면
        //   텍스처 평균만큼 어두워진다. 그 함정으로 이 저장소는 두 번 롤백했다.
        //   여기서는 `V = 목표반사율 / 텍스처평균` 으로 역산한다 — 절대값이라 멱등이다.

        /// <summary>
        /// 캐비닛 본체의 목표 반사율.
        ///
        /// 0.42 는 위계상 「구조물 0.16」보다 높다. 의도한 것이다 —
        /// 0.16 짜리 큰 면은 렌더 휘도가 20 언저리이고, 그러면 8×8 블록 표준편차가
        /// 4 를 못 넘어 **또 하나의 빈 평면**이 된다. `17_accident_recorder` 의
        /// 빈 평면 75.5% 가 정확히 그 상태다. 새 큰 면을 그렇게 만들지 않는다.
        /// </summary>
        public const float PanelAlbedo = 0.42f;

        /// <summary>
        /// 우물 안쪽의 목표 반사율. 캐비닛의 **0.29 배**다.
        /// 이 명도 단차가 창을 「구멍」으로 읽히게 만든다 — 메시 레인의 요구다.
        /// </summary>
        public const float WellAlbedo = 0.12f;

        /// <summary>
        /// 유리 알파. **0.25 · Transparent · ZWrite Off** 는 바꾸지 않는다.
        /// 불투명으로 바꾸면 안쪽 심볼 27개가 그대로 사라진다.
        /// </summary>
        public const float GlassAlpha = 0.25f;

        /// <summary>유리 색조. 알파만 위 값으로 덮고 RGB 는 통관 유리와 같은 계열을 쓴다.</summary>
        public static readonly Color GlassTint = new Color(0.70f, 0.72f, 0.70f);

        // ── 타일링 ──────────────────────────────────────────────────────────
        //
        // 메시 UV 는 `uvPerMeter = 1` 로 굽는다 → UV 단위 = 미터. 그래서 `_BaseMap_ST`
        // 가 곧 **미터당 반복 수**다.
        //
        // ρ(텍셀/화소) 유도: 캐비닛까지 약 1.25 m, 1080p·수직 FOV 60° 에서
        // 1 화소 ≈ 1.25 × 2·tan30° / 1080 ≈ 1.34 mm → 1 m ≈ 748 화면px.
        // 256px 텍스처를 N 회/m 로 깔면 ρ = 256·N / 748. 목표 ρ ≈ 1.05 → N ≈ 3.07.
        // 유리는 128px 이므로 같은 ρ 에 N ≈ 6.14.
        //
        // ⚠ **추정이다.** 조사표의 실측 ρ 목록에 이 면들은 없었다.
        //   지표 도구가 이 면을 덮게 되면 그 실측으로 갈아야 한다.
        public static readonly Vector2 PanelST = new Vector2(3.05f, 3.05f);
        public static readonly Vector2 WellST  = new Vector2(6.10f, 6.10f);
        public static readonly Vector2 GlassST = new Vector2(6.15f, 6.15f);

        private const string MeshDir = "Assets/Prototype_Elevator/Art/Meshes";
        private const string MatDir  = "Assets/Prototype_Elevator/Art/Materials";
        private const string TexDir  = "Assets/Prototype_Elevator/Art/Textures/Generated";

        /// <summary>
        /// 씬 배치에 쓰는 인자. `DefaultSpec` 에서 **테두리 두 값만** 바꾼다.
        /// 격자·개구부·베젤·우물·유리는 메시 레인이 정한 값 그대로다 —
        /// 그래야 `SymbolFitSize` 가 그쪽이 보증한 0.280 × 0.115 로 남는다.
        /// </summary>
        public static PortholeSpec Spec
        {
            get
            {
                PortholeSpec s = PortholeMesh.DefaultSpec;
                s.FrameMargin = FrameMargin;
                s.FrameDepth = FrameDepth;
                return s.Clamped();
            }
        }

        [MenuItem("Ascend/Graphics/Build Porthole Wall")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            Scene scene = AscendGraphicsBuilder.EnsureScene();
            if (!scene.IsValid()) return;

            var report = new StringBuilder("[상승] 현창 3×3 장치벽 조립 (`G-SLOT`)\n");
            PortholeSpec spec = Spec;

            // 메시 생성 단계를 명시적으로 연다. 직전 Play 세션이 닫아 두었을 수 있다.
            MeshBuildPhase.Open();

            // ── 1) 메시 3장 ────────────────────────────────────────────────
            Directory.CreateDirectory(MeshDir);
            Mesh panelMesh = WriteMesh("PM_PortholePanel", PortholeMesh.PanelMesh(spec, 1f), report,
                                       PortholeMesh.PanelTriangleBudget);
            Mesh wellMesh  = WriteMesh("PM_PortholeWell",  PortholeMesh.WellClusterMesh(spec, 1f), report,
                                       PortholeMesh.WellClusterTriangleBudget);
            Mesh glassMesh = WriteMesh("PM_PortholeGlass", PortholeMesh.GlassClusterMesh(spec, 1f), report,
                                       PortholeMesh.GlassClusterTriangleBudget);

            // ── 2) 재질 3장 ────────────────────────────────────────────────
            Material panelMat = EnsureStylized("PM_PortholePanel", "TEX_Machine_Housing",
                                               PanelST, PanelAlbedo, report);
            Material wellMat  = EnsureStylized("PM_PortholeWell",  "TEX_Machine_Housing",
                                               WellST,  WellAlbedo,  report);
            Material glassMat = EnsureGlass("PM_PortholeGlass", "TEX_Glass_Smudged", GlassST, report);

            // ── 3) 오브젝트 ────────────────────────────────────────────────
            Transform tubesRoot = FindByName("TubesRoot");
            if (tubesRoot == null) { Debug.LogError("[상승] TubesRoot 를 찾지 못했다."); return; }

            Transform wall = EnsureChild(tubesRoot, "PortholeWall");
            Undo.RecordObject(wall, "Place porthole wall");
            wall.localPosition = new Vector3(PanelBackX, GridCenterY, GridCenterZ);
            // 판 로컬 +Z(앞면)가 월드 +X(방 안쪽)를 보게 한다.
            wall.localRotation = Quaternion.Euler(0f, 90f, 0f);
            wall.localScale = Vector3.one;   // ⚠ UV 가 미터에서 나온다. 스케일하면 텍셀 밀도가 어긋난다

            EnsurePiece(wall, "PortholePanel", panelMesh, panelMat, true,  report);
            EnsurePiece(wall, "PortholeWell",  wellMesh,  wellMat,  false, report);
            EnsurePiece(wall, "PortholeGlass", glassMesh, glassMat, false, report);

            // ── 4) 통관 껍데기와 칸막이를 끈다 ─────────────────────────────
            //
            // `SetActive(false)` 가 아니라 **`Renderer.enabled = false`** 다.
            // `TubeFrame` 에는 박스 콜라이더가 붙어 있고 `Tube_1` 은
            // `BuildFigureView._gazeDevice` 가 가리키는 시선 표적이다.
            int hidden = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = r.gameObject.name;
                if (n != "TubeFrame" && !n.StartsWith("Divider_")) continue;
                if (!r.enabled) { hidden++; continue; }
                Undo.RecordObject(r, "Hide tube shell");
                r.enabled = false;
                EditorUtility.SetDirty(r);
                hidden++;
            }
            report.AppendLine($"  통관 껍데기·칸막이 렌더러 {hidden}개를 껐다 (콜라이더는 살아 있다)");

            // ── 5) 심볼 27개를 우물에 앉힌다 ───────────────────────────────
            int seated = 0, scaled = 0;
            for (int c = 0; c < 3; c++)
            {
                Transform tube = FindByName($"Tube_{c}");
                if (tube == null) { report.AppendLine($"  ⚠ Tube_{c} 없음"); continue; }

                for (int r = 0; r < 3; r++)
                {
                    Transform cell = tube.Find($"Cell_{r}");
                    if (cell == null) { report.AppendLine($"  ⚠ Tube_{c}/Cell_{r} 없음"); continue; }

                    // 씬 열 c → 판 열 (2−c). 이유는 클래스 주석 3번.
                    Vector3 seatLocal = PortholeMesh.SymbolSeat(spec, 2 - c, r, SymbolSize);
                    Vector3 seatWorld = wall.TransformPoint(seatLocal);

                    Undo.RecordObject(cell, "Seat board cell");
                    cell.position = seatWorld;
                    cell.rotation = Quaternion.identity;
                    cell.localScale = Vector3.one;
                    EditorUtility.SetDirty(cell);
                    seated++;

                    foreach (Transform sym in cell)
                    {
                        if (!sym.name.StartsWith("Sym_")) continue;
                        Undo.RecordObject(sym, "Resize symbol");
                        sym.localPosition = Vector3.zero;
                        sym.localRotation = Quaternion.identity;
                        sym.localScale = Vector3.one * SymbolSize;   // **절대값**
                        EditorUtility.SetDirty(sym);
                        scaled++;
                    }

                    report.AppendLine($"    Cell[c{c} r{r}] → 판열 {2 - c} · 월드 " +
                                      $"({seatWorld.x:F3}, {seatWorld.y:F3}, {seatWorld.z:F3})");
                }
            }
            report.AppendLine($"  칸 {seated}개 착석 · 심볼 {scaled}개를 {SymbolSize:F3} m 로 고정");

            // ── 6) 실측 보고 ───────────────────────────────────────────────
            Vector3 size = PortholeMesh.PanelSize(spec);
            PortholeMesh.PanelVerticalExtent(spec, out float bottom, out float top);
            Vector2 fit = PortholeMesh.SymbolFitSize(spec);
            report.AppendLine($"  인자 — 피치 {spec.CellPitch:F3} · 개구 반지름 {spec.OpeningRadius:F3} · " +
                              $"테두리 {spec.FrameMargin:F3} · 가로리브 {spec.HorizontalRibWidth:F4} / " +
                              $"세로리브 {spec.VerticalRibWidth:F4} (비 {spec.HorizontalRibBoost:F2})");
            report.AppendLine($"  캐비닛 — 폭 {size.x:F3} m · 높이 {size.y:F3} m · 앞끝 z(로컬) {size.z:F3} m");
            report.AppendLine($"  월드 — x [{PanelBackX:F3} .. {PanelBackX + size.z:F3}] · " +
                              $"y [{GridCenterY + bottom:F3} .. {GridCenterY + top:F3}] · " +
                              $"z [{GridCenterZ - size.x * 0.5f:F3} .. {GridCenterZ + size.x * 0.5f:F3}]");
            report.AppendLine($"  심볼 수용 한계 — 지름 {fit.x:F3} m · 깊이 {fit.y:F3} m " +
                              $"(쓰는 값 {SymbolSize:F3} · 점등 정점 {SymbolSize * 1.35f:F4})");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        // ══ 도우미 ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 메시를 에셋으로 굳힌다. **이미 있으면 그 객체에 덮어쓴다** — 새로 만들면
        /// GUID 가 바뀌어 씬의 `MeshFilter` 참조가 끊긴다.
        /// </summary>
        private static Mesh WriteMesh(string name, Mesh built, StringBuilder report, int budget)
        {
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            int tris = built.triangles.Length / 3;

            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                existing = built;
                report.AppendLine($"  메시 생성 — {name} · 삼각형 {tris} / 예산 {budget}");
            }
            else
            {
                existing.Clear();
                existing.indexFormat = built.indexFormat;
                existing.vertices = built.vertices;
                existing.normals = built.normals;
                existing.uv = built.uv;
                existing.triangles = built.triangles;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(built);
                report.AppendLine($"  메시 갱신 — {name} · 삼각형 {tris} / 예산 {budget}");
            }

            if (tris > budget)
                report.AppendLine($"    ⚠ 예산 초과 — {name} 이 {tris - budget} 삼각형 넘었다");
            return existing;
        }

        /// <summary>
        /// `Ascend/Stylized` 불투명 재질. `_BaseColor` 의 명도는
        /// **목표 반사율 ÷ 텍스처 평균**이다 — 현재 값에서 유도하지 않으므로 멱등이다.
        /// </summary>
        private static Material EnsureStylized(string name, string texName, Vector2 st,
                                               float albedo, StringBuilder report)
        {
            Shader shader = Shader.Find("Ascend/Stylized");
            if (shader == null) { report.AppendLine("  ⚠ `Ascend/Stylized` 없음 — URP/Lit 로 대체"); shader = Shader.Find("Universal Render Pipeline/Lit"); }

            Material mat = LoadOrCreate(name, shader);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texName}.png");
            if (tex == null) { report.AppendLine($"  ⚠ 텍스처 없음 — {texName}"); return mat; }

            Undo.RecordObject(mat, "Configure porthole material");
            if (mat.shader != shader) mat.shader = shader;

            float mean = Mathf.Max(0.05f, TextureMean(tex));
            float v = Mathf.Clamp01(albedo / mean);
            mat.SetColor("_BaseColor", new Color(v, v, v, 1f));
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", st);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            // 발광은 `MaterialPropertyBlock` 이 쓰는 자리다. 여기서는 확실히 꺼 둔다 —
            // `Ascend/Stylized` 는 키워드 없이 `_EmissionColor` 를 무조건 더한다.
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            if (mat.HasProperty("_EmissionMapEnabled"))
            {
                mat.SetFloat("_EmissionMapEnabled", 0f);
                mat.DisableKeyword("_EMISSIONMAP_ON");
            }
            EditorUtility.SetDirty(mat);
            report.AppendLine($"  재질 — {name} · {texName} · ST ({st.x:F2},{st.y:F2}) · " +
                              $"목표 반사율 {albedo:F3} ÷ 텍스처평균 {mean:F3} → V {v:F3}");
            return mat;
        }

        /// <summary>
        /// 유리 재질. **`URP/Lit` + Transparent + ZWrite Off + alpha 0.25.**
        /// `Ascend/Stylized` 는 `RenderType=Opaque` 라 여기 쓸 수 없다 — 쓰면 심볼이 사라진다.
        /// 셰이더만 바꾸면 URP 가 Opaque 기본값으로 되돌리므로 키워드·블렌드·큐를 직접 쓴다.
        /// </summary>
        private static Material EnsureGlass(string name, string texName, Vector2 st, StringBuilder report)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { report.AppendLine("  ⚠ URP/Lit 없음"); return null; }

            Material mat = LoadOrCreate(name, lit);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texName}.png");

            Undo.RecordObject(mat, "Configure porthole glass");
            if (mat.shader != lit) mat.shader = lit;

            mat.SetFloat("_Surface", 1f);            // 1 = Transparent
            mat.SetFloat("_Blend", 0f);              // 0 = Alpha
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

            // 색조는 상수, 알파도 상수. **절대값이다** — 현재 알파에 곱하면 두 번 돌릴
            // 때마다 투명해진다(통관 유리에서 이미 한 번 그 실패를 했다).
            mat.SetColor("_BaseColor", new Color(GlassTint.r, GlassTint.g, GlassTint.b, GlassAlpha));

            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", st);
                mat.SetTextureOffset("_BaseMap", Vector2.zero);
            }
            else report.AppendLine($"  ⚠ 텍스처 없음 — {texName}");

            EditorUtility.SetDirty(mat);
            report.AppendLine($"  재질 — {name} · {texName} · ST ({st.x:F2},{st.y:F2}) · " +
                              $"Transparent · alpha {GlassAlpha:F2} · ZWrite 0 · queue {mat.renderQueue}");
            return mat;
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// 텍스처 평균 밝기. **임포터를 건드리지 않는다** — PNG 바이트를 직접 읽어
        /// 임시 텍스처로 디코드한다. 남의 레인 에셋의 `isReadable` 을 켰다 껐다 하면
        /// 재임포트가 돌고, 그건 이 작업이 치를 이유가 없는 비용이다.
        /// </summary>
        private static readonly Dictionary<string, float> _meanCache = new Dictionary<string, float>();

        private static float TextureMean(Texture2D tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            if (_meanCache.TryGetValue(path, out float cached)) return cached;

            float mean = 1f;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (tmp.LoadImage(bytes))
                {
                    Color32[] px = tmp.GetPixels32();
                    double sum = 0;
                    for (int i = 0; i < px.Length; i++)
                        sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                    if (px.Length > 0) mean = (float)(sum / px.Length / 255.0);
                }
                Object.DestroyImmediate(tmp);
            }
            catch (System.Exception e)
            {
                // 조용히 1 을 쓰면 「보존이 적용되지 않았는데 성공처럼 보인다」가 된다.
                Debug.LogWarning($"[상승] `{tex.name}` 평균을 읽지 못했다 ({e.GetType().Name}) — " +
                                 "그 면은 텍스처 평균만큼 어두워진다.");
            }

            _meanCache[path] = mean;
            return mean;
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void EnsurePiece(Transform parent, string name, Mesh mesh, Material mat,
                                        bool castShadows, StringBuilder report)
        {
            Transform t = EnsureChild(parent, name);
            Undo.RecordObject(t, "Configure porthole piece");
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var mf = t.GetComponent<MeshFilter>();
            if (mf == null) mf = Undo.AddComponent<MeshFilter>(t.gameObject);
            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) mr = Undo.AddComponent<MeshRenderer>(t.gameObject);

            Undo.RecordObject(mf, "Set mesh");
            Undo.RecordObject(mr, "Set material");
            mf.sharedMesh = mesh;
            mr.sharedMaterials = new[] { mat };
            mr.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.enabled = true;
            EditorUtility.SetDirty(mf);
            EditorUtility.SetDirty(mr);

            Bounds b = mr.bounds;
            report.AppendLine($"  조각 — {name} · 월드 중심 ({b.center.x:F3},{b.center.y:F3},{b.center.z:F3}) " +
                              $"· 크기 ({b.size.x:F3},{b.size.y:F3},{b.size.z:F3}) · 그림자 {mr.shadowCastingMode}");
        }

        private static Transform FindByName(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }
    }
}
