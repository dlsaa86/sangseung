using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascend.CaptureHarness.EditorTools;
using Ascend.Prototype.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 사용자 명세(2026-08-02 「산업용 화물 엘리베이터 내부」)를 형상으로 세우는
    /// **멱등 조립기**.
    ///
    /// ## 규칙
    ///
    /// 1. **숫자를 갖지 않는다.** 전부 <see cref="ReferenceRoomSpec"/> 에서 읽는다.
    ///    이 파일에 리터럴 치수가 등장하면 그것은 결함이다 — 두 곳에 적힌 치수는
    ///    반드시 갈라지고, 이 저장소는 그 사고를 이미 여러 번 겪었다.
    /// 2. **멱등이다.** 루트를 찾아 자식을 통째로 지우고 다시 만든다. 「없으면 만든다」는
    ///    두 번째 실행에서 고칠 기회를 잃고, 「현재 값에 곱한다」는 실행할 때마다 밀린다
    ///    (`Reproportion Elevator Car` 가 계기판을 두 번 밀었던 그 사고).
    /// 3. **명세 위반을 조립 전에 막는다.** <see cref="ReferenceRoomSpec.Violations"/> 가
    ///    비어 있지 않으면 아무것도 만들지 않고 멈춘다. 위반한 방을 세워 놓고
    ///    캡처로 발견하는 것이 가장 비싸다.
    /// 4. **모듈 이름은 명세 §12 그대로다.** 이름이 규약이므로 바꾸면 배선이 끊긴다.
    ///
    /// ## 이 조립기가 만들지 않는 것
    ///
    /// - **게임플레이 컴포넌트를 배선하지 않는다.** 기존 씬의 `SpinBoardView` ·
    ///   `InteractableLever` · `InstrumentPanelView` 등은 fileID 로 서로를 참조하고
    ///   있고, 그 참조는 **트랜스폼을 옮겨도 살아남는다.** 그래서 배치와 배선을
    ///   분리한다 — 이 파일은 형상만, 배선 이전은 <see cref="AscendReferenceRoomRewire"/>.
    /// - 텍스처. 표면 합성은 `AscendSurfaceSynth` 가 이미 하고 있고, 그쪽 소유다.
    /// </summary>
    public static class AscendReferenceRoom
    {
        public const string RootName = "ReferenceRoom";
        private const string MaterialDir = "Assets/Prototype_Elevator/Art/Materials/Room";

        // 명세 §12 의 모듈 이름. **문자열 상수로 둔다** — 배선 스크립트가 같은 이름을
        // 찾으므로, 오타가 나면 조용히 못 찾고 배선이 비어 버린다.
        public const string ShellName        = "ElevatorShell";
        public const string GateName         = "LeftScissorGate";
        public const string GrateName        = "FloorCenterGrate";
        public const string BorderName       = "FloorBorderPlates";
        public const string MachineName      = "SoulMachineFrame";
        public const string WindowModuleName = "SoulWindowModule";
        public const string GlassName        = "SoulWindowGlass";
        public const string SoulName         = "SoulObject";
        public const string LeverBaseName    = "ExecutionLeverBase";
        public const string LeverHandleName  = "ExecutionLeverHandle";
        public const string WarningLampName  = "WarningLamp";
        public const string PowerMeterName   = "PowerMeter";
        public const string ShelfName        = "StorageShelf";
        public const string PropsName        = "StorageProps";
        public const string CeilingLampName  = "CeilingLamp";
        public const string SignsName        = "SafetySigns";

        private static readonly Dictionary<string, Material> Palette = new Dictionary<string, Material>();
        private static StringBuilder _report;

        // ══════════════════════════════════════════════════════════════════════

        [MenuItem("Ascend/Room/Build Reference Room")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            // ── 명세 위반이면 아무것도 만들지 않는다 ──
            string[] violations = ReferenceRoomSpec.Violations();
            if (violations.Length > 0)
            {
                Debug.LogError("[상승] 명세 위반 " + violations.Length + "건 — 조립을 중단한다.\n  "
                               + string.Join("\n  ", violations));
                return;
            }

            Scene scene = AscendGraphicsBuilder.EnsureScene();
            if (!scene.IsValid()) return;

            _report = new StringBuilder("[상승] 레퍼런스 룸 조립 (명세 2026-08-02)\n");
            Palette.Clear();
            // **반드시 비운다.** 이 캐시는 정적이라 도메인이 살아 있는 동안 남는다.
            // 안 비우면 두 번째 실행이 첫 실행의 메시 객체를 그대로 돌려주고,
            // 그 사이에 형상 코드를 고쳤다면 **바뀌지 않은 방이 나온다** —
            // 「고쳤는데 화면이 그대로다」로 나타나는 가장 찾기 어려운 종류의 결함이다.
            BakedMeshes.Clear();
            EnsurePalette();

            GameObject root = ResetRoot(scene);

            BuildShell(root);
            BuildFloor(root);
            BuildCeilingAndLamp(root);
            BuildScissorGate(root);
            BuildSoulMachine(root);
            BuildLeverColumn(root);
            BuildPowerMeter(root);
            BuildStorage(root);
            AddColliders(root);
            PlaceCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            _report.AppendLine($"  실내 {ReferenceRoomSpec.InteriorWidth} × {ReferenceRoomSpec.InteriorDepth} × {ReferenceRoomSpec.InteriorHeight} m");
            _report.AppendLine($"  중앙 이동 공간 {ReferenceRoomSpec.ClearSpanX:F2} × {ReferenceRoomSpec.ClearSpanZ:F2} m " +
                               $"(요구 {ReferenceRoomSpec.RequiredClearX} × {ReferenceRoomSpec.RequiredClearZ})");
            _report.AppendLine("  명세 위반 0건");
            Debug.Log(_report.ToString());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  루트와 팔레트
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 루트를 찾아 **자식을 통째로 지운다.** 이것이 멱등성의 전부다.
        /// 「이미 있으면 재사용」은 전 실행이 남긴 잘못된 자식을 그대로 물려받는다.
        /// </summary>
        private static GameObject ResetRoot(Scene scene)
        {
            GameObject root = null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == RootName) { root = go; break; }

            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                _report.AppendLine($"  {RootName} 신설");
            }
            else
            {
                int killed = root.transform.childCount;
                for (int i = killed - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                _report.AppendLine($"  {RootName} 재구성 — 기존 자식 {killed}개 제거");
            }

            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            return root;
        }

        /// <summary>
        /// 명세 §10 의 재질 5~7종. **그 이상 만들지 않는다** — 명세가 통일을 요구한다.
        /// `.mat` 에셋으로 굽는 이유는 씬에 인라인 머티리얼이 쌓이면 YAML 이 부풀고
        /// 머지가 위험해지기 때문이다(이 저장소에 이미 24개가 인라인으로 들어 있다).
        /// </summary>
        private static void EnsurePalette()
        {
            if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

            // 명세 §10 「주요 재질은 5~7개 안에서 통일한다」.
            //
            // ⚠ **여기 있는 것은 「화면에서 보일 색」이 아니라 반사율이다.**
            // 명세 §10 의 「철판: 검은색에 가까운 짙은 회갈색」을 반사율 0.126 으로
            // 받아 적었다가 방이 **통째로 검게** 나왔다(p50 = 0). 빛이 되돌아올 것이
            // 없으면 그건 「어둡다」가 아니라 「없다」다.
            //
            // 레퍼런스 이미지의 금속도 중간 회색 반사율이고, 검게 보이는 이유는
            // 광원이 하나뿐이고 빠르게 감쇠하기 때문이다. **어둠은 조명이 만든다.**
            Mat("Steel",     new Color(0.340f, 0.318f, 0.272f));   // ① 검게 도장된 철판
            Mat("BareSteel", new Color(0.455f, 0.440f, 0.404f));   // ② 도장 벗겨진 강철
            Mat("Rust",      new Color(0.520f, 0.286f, 0.160f));   // ③ 녹
            Mat("Glass",     new Color(0.230f, 0.222f, 0.210f));   // ④ 긁힌 두꺼운 유리
            Mat("RedPaint",  new Color(0.560f, 0.140f, 0.110f));   // ⑤ 붉은 도장 금속
            Mat("Sign",      new Color(0.760f, 0.716f, 0.592f));   // ⑥ 낡은 크림색 표지판
            Mat("Grease",    new Color(0.190f, 0.180f, 0.166f));   // ⑦ 검은 고무·기름때
        }

        /// <summary>
        /// 재질별 (거칠기 보정, 금속성). 명세 §10 「거칠기 대체로 0.70~0.95 ·
        /// 모서리와 손이 닿은 부분만 부분적으로 매끄럽게 마모」.
        ///
        /// URP 는 거칠기가 아니라 **매끄러움**을 받으므로 `1 - roughness` 를 넣는다.
        /// 0.70~0.95 거칠기 = 0.05~0.30 매끄러움이다.
        /// </summary>
        private static (float smoothness, float metallic) Finish(string key)
        {
            switch (key)
            {
                case "Steel":     return (0.16f, 0.75f);   // 도장된 철판 — 약하게 반사
                case "BareSteel": return (0.30f, 0.90f);   // 노출 강철 — 손이 닿는 곳
                case "Rust":      return (0.05f, 0.15f);   // 녹은 금속이 아니다
                case "Glass":     return (0.62f, 0.10f);   // 긁힌 두꺼운 유리
                case "RedPaint":  return (0.22f, 0.30f);   // 도장
                case "Sign":      return (0.10f, 0.00f);   // 종이·에나멜 표지판
                case "Grease":    return (0.08f, 0.35f);   // 기름때 낀 고무·금속
                default:          return (0.15f, 0.50f);
            }
        }

        /// <summary>
        /// 🔴 **`Ascend/Stylized` 를 쓰지 않는다 — URP/Lit 을 쓴다.** (2026-08-02 사용자 지시)
        ///
        /// 사용자가 레퍼런스 이미지를 기준으로 못박으면서 「현재 비주얼 우선순위는
        /// 기존 PS1 스타일 문구보다 레퍼런스의 공간 구조·조명·재질·배치를 우선한다」고
        /// 지시했다. 그 지시가 이 한 줄을 바꾼다.
        ///
        /// 왜 이것이 결정적인가: `Ascend/Stylized` 의 `Quantize` 는
        /// `v &lt; 1/steps` 를 **정확히 0** 으로 만든다. URP 점광의 감쇠는 역제곱이라
        /// 실내 거리에서 0.05~0.3 에 몰리고, 결과는 두 가지 중 하나뿐이었다 —
        /// **완전한 검정**(`_BandFloor` 없이) 또는 **완전히 평평한 조명**(`_BandFloor` 로
        /// 첫 칸을 들어 올리면 모든 표면이 같은 칸에 들어간다). 이 세션에서 다섯 번
        /// 측정했고 다섯 번 다 그 둘 사이였다.
        ///
        /// 레퍼런스가 요구하는 것은 그 중간이 아니라 **연속적인 감쇠**다 — 등 아래는
        /// 밝고 구석은 죽는다. 그건 계단 셰이더가 원리적으로 못 만든다.
        /// 산업 금속의 무게감도 마찬가지로 거칠기·금속성 반사에서 나오는데
        /// 그 셰이더는 비물리라 그 축이 아예 없다.
        ///
        /// PS1 감각이 필요하면 **포스트 처리**로 건다(해상도·디더·그레인).
        /// 표면 셰이딩에 섞으면 이번처럼 둘 다 잃는다.
        /// </summary>
        private static Material Mat(string key, Color color)
        {
            string path = $"{MaterialDir}/RM_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material m = existing ?? AscendMaterialFactory.Create($"RM_{key}", color, false, out _);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null && m.shader != lit) m.shader = lit;

            (float smoothness, float metallic) f = Finish(key);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", f.smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", f.metallic);
            // 스펙큘러 하이라이트와 환경 반사를 켠다 — 이게 금속을 금속으로 보이게 한다.
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);

            // ── 유리는 **투명이어야 한다** ──────────────────────────────────
            //
            // ⚠ 이 블록이 없으면 관찰창 유리가 불투명이 되어 **영혼 구슬을 통째로
            // 가린다.** URP/Lit 으로 옮기면서 실제로 그렇게 됐고, 화면에서 아홉 창이
            // 전부 빈 회색 원으로 보였다. 한 번 손으로 고쳤는데 다음 조립에서 되돌아갔다 —
            // **조립기가 정본이므로 조립기가 알아야 한다.**
            if (key == "Glass")
            {
                m.SetFloat("_Surface", 1f);   // Transparent
                m.SetFloat("_Blend", 0f);     // Alpha
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                color.a = 0.34f;              // 구슬이 비치되 유리가 있다는 것은 읽힌다
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = -1;
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                color.a = 1f;
            }

            // **항상 전 필드를 다시 쓴다.** 재사용하되 상태는 새로 만든 것과 같아야 한다.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);

            // ── 이 두 줄이 없으면 방이 검게 나온다 ──────────────────────────
            //
            // `Ascend/Stylized` 의 `Quantize` 는 `v < 1/steps` 에서 정확히 0 이다.
            // URP 점광의 `distanceAttenuation` 은 역제곱이라 2.7m 에서 약 0.137 이고
            // 4단 계단의 첫 칸 경계 0.25 를 못 넘는다 — 즉 **이 방의 거의 모든 표면에서
            // 직접광 항이 통째로 사라진다.** `range` 를 늘려도 역제곱이 지배해 소용없다.
            //
            // `_BandFloor` 가 칸 값의 바닥만 들어 올린다. 기본값 0 은 종전과 수식적으로
            // 동일하므로 이 파일 밖의 머티리얼은 영향을 받지 않는다.
            if (m.HasProperty("_BandFloor")) m.SetFloat("_BandFloor", 0.55f);

            // ⚠ **`_ShadeGamma` 를 켜지 않는다 — 두 번 시도했고 두 번 다 검게 나왔다.**
            //
            //   ① 복원 단계 포함  → p99 47 → 1
            //   ② 복원 단계 제거  → p50 45 → 0
            //
            // ②는 산술적으로 **밝아져야** 했다. `pow(0.137, 1/2.6) = 0.465` 이고
            // 그러면 4단 중 1번 칸에 들어가 직전(0번 칸)보다 밝다. 예측과 반대 결과가
            // 나왔다는 것은 원인이 이 수식 밖에 있다는 뜻이고, 후보는 셰이더 변형
            // (`shader_feature_local` 조합)이 실제로 컴파일되는지 쪽이다.
            //
            // **근거 없이 세 번째를 시도하지 않는다** (`CLAUDE.md` 「동일 오류를 세 번
            // 이상 같은 방식으로 수정하지 않는다」). 측정으로 확인된 구성만 남긴다.
            //
            // 남은 대가: 첫 칸이 넓어 조명 계조가 평평하다. 이건 열린 결함이고
            // 진행 문서에 적었다 — 숨기지 않는다.
            if (m.HasProperty("_ShadeGammaEnabled")) m.SetFloat("_ShadeGammaEnabled", 0f);
            m.DisableKeyword("_SHADEGAMMA_ON");

            // 감쇠 거듭제곱은 **방향광**을 전제로 정해진 기본값(2.5)이다. 이 방의 주광은
            // 점광이라 그 손잡이가 처음으로 실제로 작동하고, 2.5 제곱은 실내 거리에서
            // 빛을 한 번 더 짓누른다. 「빠른 감쇠」는 광원의 range 와 ndotl 이 만든다.
            if (m.HasProperty("_FalloffPower")) m.SetFloat("_FalloffPower", 1.0f);
            if (m.HasProperty("_ShadowLift")) m.SetFloat("_ShadowLift", 0.75f);


            // 명세 §14 「디더링된 그림자와 색상 전환」 — 색 심도 양자화를 켠다.
            // 이 셰이더의 규약상 키워드를 켜야 코드가 컴파일된다.
            if (m.HasProperty("_ColorDepthEnabled"))
            {
                m.SetFloat("_ColorDepthEnabled", 1f);
                m.SetFloat("_ColorDepth", 20f);
                m.SetFloat("_DitherStrength", 1f);
                m.EnableKeyword("_COLORDEPTH_ON");
            }

            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);

            Palette[key] = m;
            return m;
        }

        private static Material P(string key) => Palette.TryGetValue(key, out Material m) ? m : null;

        // ══════════════════════════════════════════════════════════════════════
        //  §9 셸 — 벽·천장
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildShell(GameObject root)
        {
            GameObject shell = Child(root.transform, ShellName);

            float w = ReferenceRoomSpec.InteriorWidth;
            float d = ReferenceRoomSpec.InteriorDepth;
            float h = ReferenceRoomSpec.InteriorHeight;
            float t = ReferenceRoomSpec.ShellThickness;

            // 벽 네 장 + 천장. 바닥은 §8 이 따로 만든다.
            // 안쪽면이 정확히 명세 좌표에 오도록 **중심을 두께의 절반만큼 밖으로** 민다.
            Slab(shell.transform, "Wall_Left",  new Vector3(ReferenceRoomSpec.WallLeftX - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d + t * 2f), "Steel");
            Slab(shell.transform, "Wall_Right", new Vector3(ReferenceRoomSpec.WallRightX + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d + t * 2f), "Steel");
            Slab(shell.transform, "Wall_Rear",  new Vector3(0f, h * 0.5f, ReferenceRoomSpec.WallRearZ + t * 0.5f), new Vector3(w, h, t), "Steel");
            Slab(shell.transform, "Wall_Front", new Vector3(0f, h * 0.5f, ReferenceRoomSpec.WallFrontZ - t * 0.5f), new Vector3(w, h, t), "Steel");
            Slab(shell.transform, "Ceiling",    new Vector3(0f, h + t * 0.5f, 0f), new Vector3(w, t, d), "Steel");

            // ── 수평 보강 레일. 명세 §9 「허리 높이와 천장 가까이」 ──
            // 왼쪽 벽에는 걸지 않는다 — 거기는 가위문 개구부다.
            foreach (float y in new[] { ReferenceRoomSpec.WallRailLowY, ReferenceRoomSpec.WallRailHighY })
            {
                string tag = Mathf.Approximately(y, ReferenceRoomSpec.WallRailLowY) ? "Low" : "High";
                float p = ReferenceRoomSpec.WallRailProtrusion;
                Slab(shell.transform, $"Rail_Right_{tag}",
                     new Vector3(ReferenceRoomSpec.WallRightX - p * 0.5f, y, 0f),
                     new Vector3(p, ReferenceRoomSpec.WallRailHeight, d), "BareSteel");
                Slab(shell.transform, $"Rail_Rear_{tag}",
                     new Vector3(0f, y, ReferenceRoomSpec.WallRearZ - p * 0.5f),
                     new Vector3(w, ReferenceRoomSpec.WallRailHeight, p), "BareSteel");
                Slab(shell.transform, $"Rail_Front_{tag}",
                     new Vector3(0f, y, ReferenceRoomSpec.WallFrontZ + p * 0.5f),
                     new Vector3(w, ReferenceRoomSpec.WallRailHeight, p), "BareSteel");
            }

            // ── 세로 철판 이음매. 명세 §9 「세로 철판 이음매」 ──
            // 형상은 얇은 돌출 하나다 — 명세 §14 가 「작은 디테일은 지오메트리보다
            // 픽셀 텍스처로」라고 못박으므로 여기서는 **판 경계만** 세운다.
            BuildSeams(shell.transform, "Seam_Rear", w, ReferenceRoomSpec.WallRearZ, true);
            BuildSeams(shell.transform, "Seam_Front", w, ReferenceRoomSpec.WallFrontZ, false);

            _report.AppendLine($"  {ShellName} — 벽 4 · 천장 1 · 보강 레일 6 · 이음매");
        }

        private static void BuildSeams(Transform parent, string prefix, float wallWidth, float z, bool rear)
        {
            float pitch = ReferenceRoomSpec.WallPlateSeamPitch;
            int count = Mathf.Max(1, Mathf.RoundToInt(wallWidth / pitch) - 1);
            float step = wallWidth / (count + 1);
            float inset = 0.012f;
            float zc = rear ? z - inset * 0.5f : z + inset * 0.5f;

            for (int i = 1; i <= count; i++)
            {
                float x = -wallWidth * 0.5f + step * i;
                Slab(parent, $"{prefix}_{i}", new Vector3(x, ReferenceRoomSpec.InteriorHeight * 0.5f, zc),
                     new Vector3(0.03f, ReferenceRoomSpec.InteriorHeight, inset), "BareSteel");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §8 바닥
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildFloor(GameObject root)
        {
            GameObject border = Child(root.transform, BorderName);
            float t = ReferenceRoomSpec.ShellThickness;

            // 테두리 철판. 중앙 타공판이 앉을 자리만 비운 **틀**이다.
            // 통짜 바닥 위에 타공판을 얹으면 명세 §8 의 「약간 낮은」이 성립하지 않는다.
            float bx = ReferenceRoomSpec.FloorBorderX;
            float bz = ReferenceRoomSpec.FloorBorderZ;
            float w = ReferenceRoomSpec.InteriorWidth;
            float d = ReferenceRoomSpec.InteriorDepth;
            float gw = ReferenceRoomSpec.GrateWidth;
            float gd = ReferenceRoomSpec.GrateDepth;

            Slab(border.transform, "Border_Left",  new Vector3(ReferenceRoomSpec.WallLeftX + bx * 0.5f, -t * 0.5f, 0f), new Vector3(bx, t, d), "Steel");
            Slab(border.transform, "Border_Right", new Vector3(ReferenceRoomSpec.WallRightX - bx * 0.5f, -t * 0.5f, 0f), new Vector3(bx, t, d), "Steel");
            Slab(border.transform, "Border_Front", new Vector3(0f, -t * 0.5f, ReferenceRoomSpec.WallFrontZ + bz * 0.5f), new Vector3(gw, t, bz), "Steel");
            Slab(border.transform, "Border_Rear",  new Vector3(0f, -t * 0.5f, ReferenceRoomSpec.WallRearZ - bz * 0.5f), new Vector3(gw, t, bz), "Steel");

            // ── 중앙 타공 철판 ──
            // 명세 §14 「볼트, 녹, 긁힘 같은 작은 디테일은 실제 지오메트리보다 픽셀
            // 텍스처로 표현」 — 타공을 실제 구멍으로 뚫지 않는다. 2.7 × 3.5 m 에
            // 0.11 m 간격이면 구멍이 780개이고, 그것을 기하로 만들면 로우폴리 락이
            // 이 바닥 하나로 무너진다. 낮은 판 하나 + 타공 텍스처가 명세의 지시다.
            GameObject grate = Child(root.transform, GrateName);
            Slab(grate.transform, "Plate",
                 new Vector3(0f, -ReferenceRoomSpec.GrateRecess - t * 0.5f, 0f),
                 new Vector3(gw, t, gd), "Grease");

            // 타공판 둘레의 단차 테. 「약간 낮다」가 실루엣에서 읽히게 한다.
            float lip = 0.035f;
            Slab(grate.transform, "Lip_Left",  new Vector3(-gw * 0.5f - lip * 0.5f, -ReferenceRoomSpec.GrateRecess * 0.5f, 0f), new Vector3(lip, ReferenceRoomSpec.GrateRecess, gd), "BareSteel");
            Slab(grate.transform, "Lip_Right", new Vector3( gw * 0.5f + lip * 0.5f, -ReferenceRoomSpec.GrateRecess * 0.5f, 0f), new Vector3(lip, ReferenceRoomSpec.GrateRecess, gd), "BareSteel");
            Slab(grate.transform, "Lip_Front", new Vector3(0f, -ReferenceRoomSpec.GrateRecess * 0.5f, -gd * 0.5f - lip * 0.5f), new Vector3(gw + lip * 2f, ReferenceRoomSpec.GrateRecess, lip), "BareSteel");
            Slab(grate.transform, "Lip_Rear",  new Vector3(0f, -ReferenceRoomSpec.GrateRecess * 0.5f,  gd * 0.5f + lip * 0.5f), new Vector3(gw + lip * 2f, ReferenceRoomSpec.GrateRecess, lip), "BareSteel");

            // ── 타이다운 링. 명세 §8 「바닥과 거의 평평하게 접히는 구조」 ──
            // 높이 12mm 라 이동을 방해하지 않는다. 벽 가까이에 둔다.
            var mesh = new ProcMeshBuilder();
            mesh.AddPrism(Vector3.zero, 0.055f, 0.055f, ReferenceRoomSpec.TieDownRingHeight,
                          8, MeshAxis.Y, 0f, true, true, false, ReferenceRoomSpec.HeroUvPerMeter);
            Mesh ringMesh = SaveMesh(mesh.ToMesh("TieDownRing"), "TieDownRing");

            int n = ReferenceRoomSpec.TieDownRingCount;
            float zSpan = ReferenceRoomSpec.GrateDepth * 0.5f - 0.25f;
            for (int i = 0; i < n; i++)
            {
                // 좌우 벽 가까이 번갈아. 짝수는 왼쪽, 홀수는 오른쪽.
                bool left = i % 2 == 0;
                float x = left ? -gw * 0.5f + 0.18f : gw * 0.5f - 0.18f;
                float z = Mathf.Lerp(-zSpan, zSpan, n <= 2 ? 0.5f : (i / 2) / Mathf.Max(1f, (n / 2f) - 1f));
                var go = new GameObject($"TieDownRing_{i}");
                go.transform.SetParent(grate.transform, false);
                go.transform.localPosition = new Vector3(x, -ReferenceRoomSpec.GrateRecess, z);
                Render(go, ringMesh, "BareSteel");
            }

            _report.AppendLine($"  {BorderName} · {GrateName} — 타공판 {gw} × {gd} m " +
                               $"(테두리 X {bx:F2} / Z {bz:F2}) · 타이다운 링 {n}개");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §9·§11 천장 보강빔과 케이지 전구
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildCeilingAndLamp(GameObject root)
        {
            GameObject shell = root.transform.Find(ShellName).gameObject;

            // 보강빔. 명세 §9 「굵은 사각 보강빔」 · §11 「천장 보강빔이 굵은 그림자를 만든다」.
            int beams = ReferenceRoomSpec.CeilingBeamCount;
            float step = ReferenceRoomSpec.InteriorDepth / (beams + 1);
            for (int i = 1; i <= beams; i++)
            {
                float z = ReferenceRoomSpec.WallFrontZ + step * i;
                Slab(shell.transform, $"CeilingBeam_{i}",
                     new Vector3(0f, ReferenceRoomSpec.InteriorHeight - ReferenceRoomSpec.CeilingBeamDrop * 0.5f, z),
                     new Vector3(ReferenceRoomSpec.InteriorWidth, ReferenceRoomSpec.CeilingBeamDrop, ReferenceRoomSpec.CeilingBeamWidth),
                     "BareSteel");
            }

            // ── 케이지 전구 ──
            // 명세 §9 「짧은 금속 베이스에 직접 고정 · 길게 늘어진 펜던트 조명은 사용하지 않는다」.
            GameObject lamp = Child(root.transform, CeilingLampName);
            lamp.transform.localPosition = new Vector3(0f, ReferenceRoomSpec.CageLampBottomY, 0f);

            float dia = ReferenceRoomSpec.CageLampDiameter;
            float hgt = ReferenceRoomSpec.CageLampHeight;

            var b = new ProcMeshBuilder();
            // 베이스 (천장에 붙는 짧은 원통)
            b.AddPrism(new Vector3(0f, hgt - 0.03f, 0f), dia * 0.30f, dia * 0.34f, 0.06f,
                       8, MeshAxis.Y, 0f, true, true, false, 4f);
            // 보호망 상단 링
            b.AddPrism(new Vector3(0f, hgt - 0.075f, 0f), dia * 0.5f, dia * 0.5f, 0.018f,
                       ReferenceRoomSpec.CageLampRibs * 2, MeshAxis.Y, 0f, true, true, false, 4f);
            // 보호망 하단 링
            b.AddPrism(new Vector3(0f, 0.02f, 0f), dia * 0.34f, dia * 0.34f, 0.018f,
                       ReferenceRoomSpec.CageLampRibs * 2, MeshAxis.Y, 0f, true, true, false, 4f);
            // 세로살
            for (int i = 0; i < ReferenceRoomSpec.CageLampRibs; i++)
            {
                float a = i * Mathf.PI * 2f / ReferenceRoomSpec.CageLampRibs;
                var top = new Vector3(Mathf.Cos(a) * dia * 0.47f, hgt - 0.08f, Mathf.Sin(a) * dia * 0.47f);
                var bot = new Vector3(Mathf.Cos(a) * dia * 0.32f, 0.03f, Mathf.Sin(a) * dia * 0.32f);
                b.AddBox((top + bot) * 0.5f, new Vector3(0.012f, (top - bot).magnitude, 0.012f),
                         Quaternion.FromToRotation(Vector3.up, (top - bot).normalized), 0f, 4f);
            }
            Mesh cageMesh = SaveMesh(b.ToMesh("CageLampCage"), "CageLampCage");
            var cage = new GameObject("Cage");
            cage.transform.SetParent(lamp.transform, false);
            Render(cage, cageMesh, "BareSteel");

            // 전구. 명세 §10 「조명: 탁한 황백색」. 발광은 `RiskStateView` 가 런타임에
            // 덮으므로 여기서는 **형상과 기본 발광만** 준다.
            var bulbB = new ProcMeshBuilder();
            bulbB.AddPrism(Vector3.zero, dia * 0.10f, dia * 0.26f, hgt * 0.34f, 8, MeshAxis.Y, 0f, true, true, false, 6f);
            bulbB.AddPrism(new Vector3(0f, hgt * 0.24f, 0f), dia * 0.26f, dia * 0.10f, hgt * 0.14f, 8, MeshAxis.Y, 0f, true, true, false, 6f);
            Mesh bulbMesh = SaveMesh(bulbB.ToMesh("CageLampBulb"), "CageLampBulb");

            var bulb = new GameObject("Bulb");
            bulb.transform.SetParent(lamp.transform, false);
            bulb.transform.localPosition = new Vector3(0f, hgt * 0.30f, 0f);
            Renderer bulbRenderer = Render(bulb, bulbMesh, "Sign");
            var glow = new Material(bulbRenderer.sharedMaterial) { name = "RM_BulbGlow" };
            glow.SetColor("_BaseColor", ReferenceRoomSpec.CageLampColor);
            glow.SetColor("_EmissionColor", ReferenceRoomSpec.CageLampColor * 3.4f);
            glow.EnableKeyword("_EMISSION");
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SaveMaterial(glow, "BulbGlow");
            bulbRenderer.sharedMaterial = glow;

            // ── 주광 ──
            // 명세 §11 「주 광원은 천장의 케이지 전구 하나」 · 색온도 2700~3000K.
            // 이름을 `CabinLight` 로 둔다 — `RiskStateView` 와 기존 배선이 그 이름을 쓴다.
            var lightGo = new GameObject("CabinLight");
            lightGo.transform.SetParent(lamp.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, hgt * 0.45f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ReferenceRoomSpec.CageLampColor;
            // ⚠ 실측으로 정해진 값이다. `RiskStateView._baseLightIntensity` 도 배선기가
            // 같은 값으로 묶는다 — 안 묶으면 플레이 모드에서만 어두워진다.
            light.intensity = 5.4f;
            // 사거리는 방 대각선(약 5.4m)보다 짧게 — 명세 §11 「벽 모서리는 거의 검게」.
            light.range = 4.4f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;

            // 앰비언트는 **거의 0** 이다. 어둠은 조명이 만들어야 하고,
            // 앰비언트로 들어 올리면 명세 §11 「벽 모서리와 선반 아래는 거의 검게」가 무너진다.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.030f, 0.026f, 0.021f, 1f);

            _report.AppendLine($"  {CeilingLampName} — 케이지 지름 {dia} · 높이 {hgt} · 필라멘트 y={ReferenceRoomSpec.CageLampFilament.y:F2} " +
                               $"· 보강빔 {beams}개");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §3 왼쪽 가위문
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildScissorGate(GameObject root)
        {
            GameObject gate = Child(root.transform, GateName);
            gate.transform.localPosition = new Vector3(ReferenceRoomSpec.WallLeftX, 0f, 0f);

            float ow = ReferenceRoomSpec.GateOpeningWidth;
            float oh = ReferenceRoomSpec.GateOpeningHeight;
            float prot = ReferenceRoomSpec.GateProtrusion;

            // 개구부를 뚫기 위해 좌벽을 네 조각으로 다시 만든다. 이 조각들은
            // `ElevatorShell/Wall_Left` 를 **대체하지 않고 가린다** — 셸을 뚫으면
            // 셸 조립이 가위문을 알아야 하고, 그러면 두 모듈이 서로를 안다.
            float t = ReferenceRoomSpec.ShellThickness;
            float d = ReferenceRoomSpec.InteriorDepth;
            float h = ReferenceRoomSpec.InteriorHeight;
            float side = (d - ow) * 0.5f;

            Slab(gate.transform, "Jamb_Front", new Vector3(prot * 0.5f, h * 0.5f, -ow * 0.5f - side * 0.5f), new Vector3(prot, h, side), "Steel");
            Slab(gate.transform, "Jamb_Rear",  new Vector3(prot * 0.5f, h * 0.5f,  ow * 0.5f + side * 0.5f), new Vector3(prot, h, side), "Steel");
            Slab(gate.transform, "Header",     new Vector3(prot * 0.5f, oh + (h - oh) * 0.5f, 0f), new Vector3(prot, h - oh, ow), "Steel");

            // 승강로 어둠. 명세 §3 「문이 닫혔을 때도 바깥의 어두운 승강로가 좁은 틈 사이로 보인다」.
            Slab(gate.transform, "ShaftBackdrop", new Vector3(-0.42f, oh * 0.5f, 0f), new Vector3(0.05f, oh, ow), "Grease");

            // 가이드 레일. 명세 §3 「바닥과 천장에 각각 철제 가이드 레일」.
            float rail = ReferenceRoomSpec.GateRailSize;
            Slab(gate.transform, "Rail_Floor",   new Vector3(prot * 0.5f, rail * 0.5f, 0f), new Vector3(prot + 0.02f, rail, ow), "BareSteel");
            Slab(gate.transform, "Rail_Ceiling", new Vector3(prot * 0.5f, oh - rail * 0.5f, 0f), new Vector3(prot + 0.02f, rail, ow), "BareSteel");

            // ── 접이식 격자 ──
            BuildGateLattice(gate.transform, ow, oh);

            // ── 층수 표시기 ──
            // 명세 §3 「폭 0.65 · 높이 0.22 · 오래된 검은 금속 프레임 · 붉은 세그먼트 · 좌우 삼각 표시등」.
            var indicator = new GameObject("FloorIndicatorPanel");
            indicator.transform.SetParent(gate.transform, false);
            indicator.transform.localPosition = new Vector3(prot, ReferenceRoomSpec.FloorIndicatorCenterY, 0f);
            // 좌벽에 붙으므로 +X 를 향한다.
            indicator.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            float iw = ReferenceRoomSpec.FloorIndicatorWidth;
            float ih = ReferenceRoomSpec.FloorIndicatorHeight;
            Slab(indicator.transform, "Bezel", new Vector3(0f, 0f, 0.02f), new Vector3(iw, ih, 0.04f), "Steel");
            Slab(indicator.transform, "Readout", new Vector3(0f, 0f, -0.002f), new Vector3(iw * 0.46f, ih * 0.62f, 0.01f), "Glass");
            // 좌우 삼각 상승·하강 표시등. 삼각형은 3각 프리즘으로 만든다.
            var arrowB = new ProcMeshBuilder();
            arrowB.AddPrism(Vector3.zero, ih * 0.20f, ih * 0.20f, 0.012f, 3, MeshAxis.Z, 0f, true, true, false, 8f);
            Mesh arrowMesh = SaveMesh(arrowB.ToMesh("IndicatorArrow"), "IndicatorArrow");
            for (int i = 0; i < 2; i++)
            {
                var a = new GameObject(i == 0 ? "ArrowUp" : "ArrowDown");
                a.transform.SetParent(indicator.transform, false);
                a.transform.localPosition = new Vector3(i == 0 ? -iw * 0.36f : iw * 0.36f, 0f, -0.004f);
                a.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 90f : -90f);
                Render(a, arrowMesh, "RedPaint");
            }

            // 문 주변 보조등. 명세 §3 「작은 보조등 하나만 · 출입구 전체를 밝게 비추지 말고」.
            var gateLightGo = new GameObject("GateLamp");
            gateLightGo.transform.SetParent(gate.transform, false);
            gateLightGo.transform.localPosition = new Vector3(prot + 0.12f, oh - 0.18f, -ow * 0.32f);
            Light gl = gateLightGo.AddComponent<Light>();
            gl.type = LightType.Point;
            gl.color = ReferenceRoomSpec.GateLampColor;
            gl.intensity = 0.55f;
            gl.range = 1.5f;              // 문살 일부와 바닥 레일만 닿는 거리
            gl.shadows = LightShadows.None;

            _report.AppendLine($"  {GateName} — 개구부 {ow} × {oh} · 돌출 {prot} · 층수 표시기 {iw} × {ih} @ y={ReferenceRoomSpec.FloorIndicatorCenterY}");
        }

        /// <summary>
        /// 명세 §3 의 접이식 가위문 격자.
        ///
        /// 「얇은 철망이 아니라 무겁고 낡은 평철이 교차하는 구조」이므로 두 방향의
        /// 평철 다발을 만든다. 기울기는 명세가 준 마름모 간격에서 나온다 —
        /// 가로 180mm 에 세로 380mm 이므로 수평에서 약 64.6°.
        ///
        /// X자 링크의 **리벳 축**을 실제 오브젝트로 남긴다(명세 §12 「가위문 링크:
        /// 각 리벳 회전축」). 애니메이션이 그 자리를 필요로 하고, 없으면 나중에
        /// 형상을 다시 뜯어야 한다.
        /// </summary>
        private static void BuildGateLattice(Transform parent, float openWidth, float openHeight)
        {
            var lattice = new GameObject("Lattice");
            lattice.transform.SetParent(parent, false);
            lattice.transform.localPosition = new Vector3(ReferenceRoomSpec.GateProtrusion * 0.5f, 0f, 0f);

            float bw = ReferenceRoomSpec.GateFlatBarWidth;
            float bt = ReferenceRoomSpec.GateFlatBarThickness;
            float pz = ReferenceRoomSpec.GateLatticePitchZ;
            float py = ReferenceRoomSpec.GateLatticePitchY;
            float railY = ReferenceRoomSpec.GateRailSize;
            float y0 = railY;
            float y1 = openHeight - railY;
            float span = y1 - y0;

            // 한 평철이 위아래를 가로지르며 z 방향으로 이동하는 양.
            float run = span * (pz / py);
            float uv = ReferenceRoomSpec.SurfaceUvPerMeter;

            var b = new ProcMeshBuilder();
            var rivets = new List<Vector3>();

            for (int dir = 0; dir < 2; dir++)
            {
                float sign = dir == 0 ? 1f : -1f;
                // 두 다발이 개구부를 덮도록 시작점을 run 만큼 넉넉히 잡는다.
                float zStart = -openWidth * 0.5f - run;
                float zEnd = openWidth * 0.5f + run;
                for (float z = zStart; z <= zEnd + 0.001f; z += pz * 2f)
                {
                    var a = new Vector3(0f, y0, z);
                    var c = new Vector3(0f, y1, z + run * sign);
                    Vector3 mid = (a + c) * 0.5f;
                    Vector3 axis = c - a;

                    // 개구부 밖으로 완전히 나간 막대는 만들지 않는다.
                    if (Mathf.Max(a.z, c.z) < -openWidth * 0.5f) continue;
                    if (Mathf.Min(a.z, c.z) > openWidth * 0.5f) continue;

                    Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                    b.AddBox(mid, new Vector3(bt, axis.magnitude, bw), rot, 0f, uv);

                    // 이 막대가 지나는 리벳 축(반대 다발과 만나는 높이)을 기록한다.
                    for (int k = 0; k <= Mathf.RoundToInt(span / py); k++)
                    {
                        float ty = y0 + py * k;
                        if (ty > y1 + 0.001f) break;
                        float tz = z + (ty - y0) * (pz / py) * sign;
                        if (tz < -openWidth * 0.5f || tz > openWidth * 0.5f) continue;
                        if (dir == 0) rivets.Add(new Vector3(0f, ty, tz));
                    }
                }
            }

            Mesh latticeMesh = SaveMesh(b.ToMesh("ScissorGateLattice"), "ScissorGateLattice");
            Render(lattice, latticeMesh, "BareSteel");

            // 리벳 축. 애니메이션 피벗이므로 **빈 트랜스폼**으로 둔다 — 렌더러를 붙이면
            // 수백 개의 드로우콜이 되고, 명세 §14 가 작은 디테일을 텍스처로 넘기라고 한다.
            var pivots = new GameObject("LinkPivots");
            pivots.transform.SetParent(lattice.transform, false);
            for (int i = 0; i < rivets.Count; i++)
            {
                var p = new GameObject($"LinkPivot_{i}");
                p.transform.SetParent(pivots.transform, false);
                p.transform.localPosition = rivets[i];
            }

            // 하단 롤러. 명세 §3 「문 하단에는 바닥 레일을 따라 움직이는 작은 금속 롤러」.
            var rollerB = new ProcMeshBuilder();
            rollerB.AddPrism(Vector3.zero, ReferenceRoomSpec.GateRollerRadius, ReferenceRoomSpec.GateRollerRadius,
                             0.022f, 8, MeshAxis.X, 0f, true, true, false, 8f);
            Mesh rollerMesh = SaveMesh(rollerB.ToMesh("GateRoller"), "GateRoller");
            int rollers = Mathf.Max(2, Mathf.RoundToInt(openWidth / (pz * 3f)));
            for (int i = 0; i < rollers; i++)
            {
                float z = Mathf.Lerp(-openWidth * 0.5f + 0.12f, openWidth * 0.5f - 0.12f, i / (float)(rollers - 1));
                var r = new GameObject($"Roller_{i}");
                r.transform.SetParent(lattice.transform, false);
                r.transform.localPosition = new Vector3(0f, railY + ReferenceRoomSpec.GateRollerRadius, z);
                Render(r, rollerMesh, "Grease");
            }

            _report.AppendLine($"     격자 — 평철 {bw * 1000f:F0}×{bt * 1000f:F0}mm · 마름모 {pz * 1000f:F0}×{py * 1000f:F0}mm " +
                               $"· 리벳 축 {rivets.Count}개 · 롤러 {rollers}개");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §4 후면 3×3 영혼 통관 장치
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildSoulMachine(GameObject root)
        {
            GameObject machine = Child(root.transform, MachineName);
            // 장치 원점 = 후면 벽 안쪽면의 장치 중심. −Z 가 실내 쪽이다.
            machine.transform.localPosition = new Vector3(ReferenceRoomSpec.MachineCenterX, 0f, ReferenceRoomSpec.WallRearZ);

            float mw = ReferenceRoomSpec.MachineWidth;
            float mh = ReferenceRoomSpec.MachineHeight;
            float md = ReferenceRoomSpec.MachineDepth;
            float band = ReferenceRoomSpec.MachineFrameBand;
            float cy = ReferenceRoomSpec.MachineBottomY + mh * 0.5f;

            // 뒷판. 프레임 안쪽을 메운다.
            Slab(machine.transform, "BackPlate", new Vector3(0f, cy, -md * 0.35f), new Vector3(mw - band * 2f, mh - band * 2f, md * 0.3f), "Steel");

            // ── 두꺼운 사각 철제 프레임 ──
            // 명세 §4 「모서리는 완전히 둥글지 않고 각지고 투박한 형태」 — 모따기 없음.
            Slab(machine.transform, "Frame_Top",    new Vector3(0f, ReferenceRoomSpec.MachineTopY - band * 0.5f, -md * 0.5f), new Vector3(mw, band, md), "BareSteel");
            Slab(machine.transform, "Frame_Bottom", new Vector3(0f, ReferenceRoomSpec.MachineBottomY + band * 0.5f, -md * 0.5f), new Vector3(mw, band, md), "BareSteel");
            Slab(machine.transform, "Frame_Left",   new Vector3(-mw * 0.5f + band * 0.5f, cy, -md * 0.5f), new Vector3(band, mh, md), "BareSteel");
            Slab(machine.transform, "Frame_Right",  new Vector3( mw * 0.5f - band * 0.5f, cy, -md * 0.5f), new Vector3(band, mh, md), "BareSteel");

            // 벽 고정 볼트. 명세 §4 「큰 육각 볼트와 리벳으로 벽에 고정」.
            var boltB = new ProcMeshBuilder();
            boltB.AddPrism(Vector3.zero, 0.021f, 0.019f, 0.016f, 6, MeshAxis.Z, 0f, true, true, false, 12f);
            Mesh boltMesh = SaveMesh(boltB.ToMesh("MachineBolt"), "MachineBolt");
            var bolts = new GameObject("FrameBolts");
            bolts.transform.SetParent(machine.transform, false);
            int perSide = 5;
            for (int i = 0; i < perSide; i++)
            {
                float f = i / (float)(perSide - 1);
                AddBolt(bolts.transform, boltMesh, new Vector3(Mathf.Lerp(-mw * 0.5f + band * 0.5f, mw * 0.5f - band * 0.5f, f), ReferenceRoomSpec.MachineTopY - band * 0.5f, -md - 0.008f), $"Bolt_T{i}");
                AddBolt(bolts.transform, boltMesh, new Vector3(Mathf.Lerp(-mw * 0.5f + band * 0.5f, mw * 0.5f - band * 0.5f, f), ReferenceRoomSpec.MachineBottomY + band * 0.5f, -md - 0.008f), $"Bolt_B{i}");
                AddBolt(bolts.transform, boltMesh, new Vector3(-mw * 0.5f + band * 0.5f, Mathf.Lerp(ReferenceRoomSpec.MachineBottomY + band, ReferenceRoomSpec.MachineTopY - band, f), -md - 0.008f), $"Bolt_L{i}");
                AddBolt(bolts.transform, boltMesh, new Vector3( mw * 0.5f - band * 0.5f, Mathf.Lerp(ReferenceRoomSpec.MachineBottomY + band, ReferenceRoomSpec.MachineTopY - band, f), -md - 0.008f), $"Bolt_R{i}");
            }

            // ── 9개 관찰창 모듈 ──
            // 명세 §12 「SoulWindowModule 하나를 제작한 뒤 9개를 인스턴스로 배치한다」.
            BuildWindowMeshes(out Mesh ringMesh, out Mesh wellMesh, out Mesh glassMesh, out Mesh soulMesh, out Mesh windowBoltMesh);

            var grid = new GameObject("WindowGrid");
            grid.transform.SetParent(machine.transform, false);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    Vector2 c = ReferenceRoomSpec.WindowCenter(col, row);
                    var module = new GameObject($"{WindowModuleName}_{col}{row}");
                    module.transform.SetParent(grid.transform, false);
                    // 장치 원점이 이미 MachineCenterX 라 X 는 상대값으로 준다.
                    module.transform.localPosition = new Vector3(c.x - ReferenceRoomSpec.MachineCenterX, c.y, -md);

                    var ring = new GameObject("Ring");
                    ring.transform.SetParent(module.transform, false);
                    Render(ring, ringMesh, "BareSteel");

                    var well = new GameObject("Well");
                    well.transform.SetParent(module.transform, false);
                    Render(well, wellMesh, "Grease");

                    var soul = new GameObject(SoulName);
                    soul.transform.SetParent(module.transform, false);
                    // 명세 §4 「9개 모두 완전히 동일한 모양이 아니라 내부 실루엣과 빛의 세기를
                    // 조금씩 다르게 한다」 — 결정론적 해시로 흔든다. 난수를 쓰면 조립할
                    // 때마다 달라져 캡처 회귀 판정이 불가능해진다.
                    int seed = col * 3 + row;
                    float jitter = ProcMeshBuilder.HashSigned(seed * 977 + 13);
                    float scale = 1f + jitter * 0.18f;
                    soul.transform.localPosition = new Vector3(
                        ProcMeshBuilder.HashSigned(seed * 131 + 7) * 0.012f,
                        ProcMeshBuilder.HashSigned(seed * 197 + 3) * 0.012f,
                        -0.030f);
                    soul.transform.localScale = new Vector3(scale, scale * (1f + jitter * 0.1f), scale);
                    soul.transform.localRotation = Quaternion.Euler(0f, 0f, ProcMeshBuilder.Hash01(seed * 313) * 360f);
                    Renderer soulRenderer = Render(soul, soulMesh, "RedPaint");
                    soulRenderer.sharedMaterial = SoulMaterial(seed);

                    var glass = new GameObject(GlassName);
                    glass.transform.SetParent(module.transform, false);
                    glass.transform.localPosition = new Vector3(0f, 0f, -ReferenceRoomSpec.WindowProtrusion * 0.55f);
                    Render(glass, glassMesh, "Glass");

                    var mBolts = new GameObject("Bolts");
                    mBolts.transform.SetParent(module.transform, false);
                    for (int i = 0; i < ReferenceRoomSpec.WindowBoltCount; i++)
                    {
                        float a = i * Mathf.PI * 2f / ReferenceRoomSpec.WindowBoltCount + Mathf.PI / ReferenceRoomSpec.WindowBoltCount;
                        float r = (ReferenceRoomSpec.WindowGlassDiameter + ReferenceRoomSpec.WindowRingBand) * 0.5f;
                        AddBolt(mBolts.transform, windowBoltMesh,
                                new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -ReferenceRoomSpec.WindowProtrusion - 0.006f),
                                $"Bolt_{i}");
                    }
                }
            }

            // ── 하단 정비 패널 3개 ──
            // 명세 §4 「각각 위쪽 세로 열과 정렬 · 원형 잠금장치 하나씩 · 추가 버튼·모니터 없음」.
            var panels = new GameObject("ServicePanels");
            panels.transform.SetParent(machine.transform, false);
            var latchB = new ProcMeshBuilder();
            latchB.AddPrism(Vector3.zero, 0.042f, 0.038f, 0.022f, 8, MeshAxis.Z, 0f, true, true, false, 10f);
            latchB.AddBox(new Vector3(0f, 0f, -0.016f), new Vector3(0.062f, 0.014f, 0.012f), 0f, 10f);
            Mesh latchMesh = SaveMesh(latchB.ToMesh("ServiceLatch"), "ServiceLatch");

            float panelW = (mw - band * 2f) / ReferenceRoomSpec.ServicePanelCount;
            for (int i = 0; i < ReferenceRoomSpec.ServicePanelCount; i++)
            {
                float x = -mw * 0.5f + band + panelW * (i + 0.5f);
                var panel = new GameObject($"ServicePanel_{i}");
                panel.transform.SetParent(panels.transform, false);
                panel.transform.localPosition = new Vector3(x, ReferenceRoomSpec.ServicePanelCenterY, -md * 0.5f);
                Slab(panel.transform, "Plate", new Vector3(0f, 0f, -md * 0.5f + 0.02f),
                     new Vector3(panelW - 0.012f, ReferenceRoomSpec.ServicePanelHeight - 0.012f, 0.04f), "Steel");
                var latch = new GameObject("Latch");
                latch.transform.SetParent(panel.transform, false);
                latch.transform.localPosition = new Vector3(0f, 0f, -md * 0.5f - 0.012f);
                Render(latch, latchMesh, "BareSteel");
            }

            _report.AppendLine($"  {MachineName} — {mw} × {mh} × {md} · 벽 점유 {ReferenceRoomSpec.MachineWallCoverage * 100f:F1}% " +
                               $"· 관찰창 9 (링 {ReferenceRoomSpec.WindowRingDiameter:F3} · 유리 {ReferenceRoomSpec.WindowGlassDiameter} · 간격 {ReferenceRoomSpec.WindowPitch}) " +
                               $"· 정비 패널 {ReferenceRoomSpec.ServicePanelCount}");
        }

        /// <summary>
        /// 관찰창 모듈의 메시 넷. **한 번 만들어 9번 인스턴스한다**(명세 §12).
        /// 실루엣은 12각형이다(명세 §14 「완전한 원보다 12~16각형」).
        /// </summary>
        private static void BuildWindowMeshes(out Mesh ring, out Mesh well, out Mesh glass, out Mesh soul, out Mesh bolt)
        {
            int sides = ReferenceRoomSpec.WindowSilhouetteSides;
            float rGlass = ReferenceRoomSpec.WindowGlassDiameter * 0.5f;
            float rRing = ReferenceRoomSpec.WindowRingDiameter * 0.5f;
            float prot = ReferenceRoomSpec.WindowProtrusion;
            float uv = ReferenceRoomSpec.HeroUvPerMeter;

            // ── 링 ────────────────────────────────────────────────────────────
            // 사용자 지적: 「통관 디자인도 너무 대충」. 직전 판본은 **평평한 고리 하나**
            // 였고, 그러면 벽에 그린 원과 구분되지 않는다. 실제 현창의 실루엣은
            // **바깥 플랜지 · 안쪽 베젤 · 그 사이의 단** 셋이 겹쳐 만들어진다.
            var rb = new ProcMeshBuilder();
            float rMid = (rGlass + rRing) * 0.5f;

            for (int i = 0; i < sides; i++)
            {
                float am = (i + 0.5f) * Mathf.PI * 2f / sides;
                float cos = Mathf.Cos(am), sin = Mathf.Sin(am);
                Quaternion rot = Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg + 90f);

                // ① 바깥 플랜지 — 벽에 붙는 넓고 낮은 테
                float segOuter = 2f * Mathf.Tan(Mathf.PI / sides) * rRing;
                rb.AddBox(new Vector3(cos * rRing * 0.94f, sin * rRing * 0.94f, -prot * 0.22f),
                          new Vector3(segOuter, (rRing - rGlass) * 0.55f, prot * 0.44f), rot, 0f, uv);

                // ② 안쪽 베젤 — 유리를 무는 좁고 높은 테. 앞으로 더 나온다.
                float segInner = 2f * Mathf.Tan(Mathf.PI / sides) * rMid;
                rb.AddBox(new Vector3(cos * rMid, sin * rMid, -prot * 0.74f),
                          new Vector3(segInner, (rRing - rGlass) * 0.62f, prot * 0.56f), rot, 0f, uv);

                // ③ 두 테를 잇는 경사 단 — 이것이 두께를 만든다
                float segStep = 2f * Mathf.Tan(Mathf.PI / sides) * (rMid + rRing) * 0.5f;
                rb.AddBox(new Vector3(cos * (rMid + rRing) * 0.5f, sin * (rMid + rRing) * 0.5f, -prot * 0.5f),
                          new Vector3(segStep, (rRing - rGlass) * 0.34f, prot * 0.30f), rot, 0f, uv);
            }
            ring = SaveMesh(rb.ToMesh("SoulWindowRing"), "SoulWindowRing");

            // 우물 — 유리 뒤의 어두운 통. 명세 §4 「내부는 어둡고」.
            var wb = new ProcMeshBuilder();
            wb.AddPrism(new Vector3(0f, 0f, 0.03f), rGlass, rGlass, 0.09f, sides, MeshAxis.Z, 0f, true, true, false, uv);
            well = SaveMesh(wb.ToMesh("SoulWindowWell"), "SoulWindowWell");

            // 유리 — 얇은 12각 원판. 살짝 볼록하게 두 단.
            var gb = new ProcMeshBuilder();
            gb.AddPrism(Vector3.zero, rGlass * 0.99f, rGlass * 0.82f, 0.018f, sides, MeshAxis.Z, 0f, true, true, false, uv);
            glass = SaveMesh(gb.ToMesh("SoulWindowGlass"), "SoulWindowGlass");

            // 영혼 물질 — 명세 §4 「형태가 불분명한 붉은 구체 또는 응축된 에너지 덩어리」.
            // 얼굴·장기·눈·손 같은 신체 형태를 쓰지 않는다(명세 §4·§13).
            var sb = new ProcMeshBuilder();
            sb.AddPrism(Vector3.zero, rGlass * 0.22f, rGlass * 0.40f, rGlass * 0.36f, 8, MeshAxis.Z, 0f, true, true, false, uv);
            sb.AddPrism(new Vector3(0f, 0f, rGlass * 0.30f), rGlass * 0.40f, rGlass * 0.16f, rGlass * 0.30f, 8, MeshAxis.Z, 22f, true, true, false, uv);
            soul = SaveMesh(sb.ToMesh("SoulObject"), "SoulObject");

            var bb = new ProcMeshBuilder();
            bb.AddPrism(Vector3.zero, 0.013f, 0.011f, 0.012f, 6, MeshAxis.Z, 0f, true, true, false, 16f);
            bolt = SaveMesh(bb.ToMesh("SoulWindowBolt"), "SoulWindowBolt");
        }

        /// <summary>
        /// 영혼 발광 머티리얼. 명세 §4 「중심부만 약하게 붉게 발광 · 유리 전체를 밝히지
        /// 말고 · 가장 밝은 모듈도 주변을 강하게 밝힐 정도로 발광하지 않는다」.
        ///
        /// 세기를 칸마다 조금씩 다르게 하되 **결정론적**으로 만든다.
        /// </summary>
        private static Material SoulMaterial(int seed)
        {
            string key = $"SoulGlow_{seed}";
            string path = $"{MaterialDir}/RM_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material m = existing ?? new Material(P("RedPaint")) { name = $"RM_{key}" };

            // ⚠ **이 값들은 실측으로 정해졌다. 되돌리지 않는다.**
            //
            // 발광을 ×3.6 으로 줬더니 톤매핑이 채널을 전부 포화시켜 **색상 정보가
            // 사라졌다** — 구슬이 붉지 않고 **흰색**으로 보였고 붉은 화소가 0.13% 였다.
            // ×1.05 로 내리자 0.91% 로 올랐다. HDR 발광은 세게 줄수록 붉어지는 게
            // 아니라 **희어진다.** 1 을 살짝 넘겨 블룸만 걸리고 포화는 안 되는 값이다.
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null) m.shader = lit;
            m.SetFloat("_Smoothness", 0.55f);
            m.SetFloat("_Metallic", 0f);

            float k = 0.80f + (seed % 3) * 0.12f;
            m.SetColor("_BaseColor", new Color(0.22f, 0.02f, 0.01f, 1f));
            m.SetColor("_EmissionColor", new Color(1.00f, 0.11f, 0.04f) * (1.05f * k));
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §5 실행 레버
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildLeverColumn(GameObject root)
        {
            GameObject column = Child(root.transform, LeverBaseName);
            column.transform.localPosition = new Vector3(ReferenceRoomSpec.LeverColumnCenterX, 0f, ReferenceRoomSpec.WallRearZ);

            float cw = ReferenceRoomSpec.LeverColumnWidth;
            float ch = ReferenceRoomSpec.LeverColumnHeight;
            float cd = ReferenceRoomSpec.LeverColumnDepth;
            float uv = ReferenceRoomSpec.HeroUvPerMeter;

            Slab(column.transform, "Housing", new Vector3(0f, ReferenceRoomSpec.LeverColumnCenterY, -cd * 0.5f),
                 new Vector3(cw, ch, cd), "Steel");

            // 수직 슬롯. 명세 §5 「레버는 수직 슬롯을 따라 약 55도 범위로 움직인다」.
            // 슬롯이 보여야 회전축과 이동 방향이 읽힌다(명세 §5 마지막 줄).
            float slotH = 2f * ReferenceRoomSpec.LeverHandleLength * Mathf.Sin(ReferenceRoomSpec.LeverSwingDegrees * 0.5f * Mathf.Deg2Rad);
            Slab(column.transform, "Slot", new Vector3(0f, ReferenceRoomSpec.LeverPivotY, -cd - 0.004f),
                 new Vector3(0.055f, slotH, 0.02f), "Grease");

            // 기계식 걸쇠와 톱니형 잠금 홈. 명세 §5 「실제로 고정되고 움직일 수 있는 구조로 보이게」.
            var teeth = new GameObject("LatchTeeth");
            teeth.transform.SetParent(column.transform, false);
            int toothCount = 7;
            for (int i = 0; i < toothCount; i++)
            {
                float y = Mathf.Lerp(ReferenceRoomSpec.LeverPivotY - slotH * 0.5f, ReferenceRoomSpec.LeverPivotY + slotH * 0.5f, i / (float)(toothCount - 1));
                Slab(teeth.transform, $"Tooth_{i}", new Vector3(0.048f, y, -cd - 0.012f), new Vector3(0.030f, 0.012f, 0.026f), "BareSteel");
            }
            Slab(column.transform, "SpringGuide", new Vector3(-0.052f, ReferenceRoomSpec.LeverPivotY, -cd - 0.014f),
                 new Vector3(0.018f, slotH * 0.8f, 0.018f), "BareSteel");

            // ── 손잡이 ──
            // 명세 §12 「레버: 손잡이 하단의 실제 회전축」 — 피벗을 별도 트랜스폼으로 둔다.
            var pivot = new GameObject(LeverHandleName);
            pivot.transform.SetParent(column.transform, false);
            pivot.transform.localPosition = new Vector3(0f, ReferenceRoomSpec.LeverPivotY, -cd);

            float armLen = ReferenceRoomSpec.LeverHandleLength - ReferenceRoomSpec.LeverGripLength;
            // ── 손잡이 조립체 ────────────────────────────────────────────────
            // 사용자 지적: 「레버의 디자인이 너무 그냥 박스 형태」.
            // 실제 산업용 레버의 실루엣은 **축 보스 · 클레비스(포크) · 각진 팔 ·
            // 홈이 파인 그립** 넷으로 읽힌다. 상자 하나로는 어느 쪽이 축인지도 안 보인다.
            var hb = new ProcMeshBuilder();

            // ① 회전축 보스 — 굵은 원통 + 바깥 플랜지. 「여기가 축이다」를 형상으로 말한다.
            hb.AddPrism(Vector3.zero, 0.034f, 0.034f, 0.058f, 10, MeshAxis.Z, 0f, true, true, false, uv);
            hb.AddPrism(new Vector3(0f, 0f, -0.034f), 0.048f, 0.040f, 0.014f, 10, MeshAxis.Z, 0f, true, true, false, uv);
            // 보스 둘레 볼트 넷
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                hb.AddPrism(new Vector3(Mathf.Cos(a) * 0.040f, Mathf.Sin(a) * 0.040f, -0.044f),
                            0.008f, 0.007f, 0.010f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            }

            // ② 클레비스 — 보스를 감싸는 두 갈래 포크. 팔이 축에 **물려 있다**를 만든다.
            for (int s = -1; s <= 1; s += 2)
            {
                hb.AddBox(new Vector3(0f, s * 0.030f, -0.055f), new Vector3(0.020f, 0.016f, 0.075f), 0f, uv);
            }

            // ③ 팔 — 단면이 세로로 긴 각재. 정사각 단면은 방향이 안 읽힌다.
            hb.AddBox(new Vector3(0f, 0f, -armLen * 0.5f - 0.055f),
                      new Vector3(0.020f, 0.034f, armLen), 0f, uv);
            // 팔 중간 보강 리브
            hb.AddBox(new Vector3(0f, 0f, -armLen * 0.5f - 0.055f),
                      new Vector3(0.030f, 0.012f, armLen * 0.55f), 0f, uv);

            // ④ 그립 목 — 팔에서 그립으로 넘어가는 테이퍼. 이게 없으면 그립이 떠 보인다.
            hb.AddPrism(new Vector3(0f, 0f, -armLen - 0.045f), 0.017f, 0.026f, 0.038f,
                        8, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh armMesh = SaveMesh(hb.ToMesh("LeverArm"), "LeverArm");
            var arm = new GameObject("Arm");
            arm.transform.SetParent(pivot.transform, false);
            Render(arm, armMesh, "BareSteel");

            // 붉은 원통 그립. 명세 §5 「칠이 벗겨진 어두운 적색 도장 · 8각 이하」.
            // 원통 하나가 아니라 **홈이 파인 손잡이**로 만든다. 손이 닿는 물건은
            // 미끄럼 방지 홈이 있고, 그 홈이 실루엣에서 「잡는 것」이라고 말한다.
            var gb = new ProcMeshBuilder();
            float gr = ReferenceRoomSpec.LeverGripDiameter * 0.5f;
            int gs = ReferenceRoomSpec.LeverGripSides;
            float gl = ReferenceRoomSpec.LeverGripLength;

            // 뒤쪽 칼라(손이 미끄러져 빠지지 않게 하는 턱)
            gb.AddPrism(new Vector3(0f, 0f, gl * 0.5f - 0.012f), gr * 1.45f, gr * 1.45f, 0.016f,
                        gs, MeshAxis.Z, 0f, true, true, false, uv);

            // 본체를 다섯 마디로 나누고 마디마다 굵기를 번갈아 — 홈이 생긴다.
            const int knurls = 5;
            float seg = (gl - 0.030f) / knurls;
            for (int i = 0; i < knurls; i++)
            {
                float z = gl * 0.5f - 0.024f - seg * (i + 0.5f);
                float r = (i % 2 == 0) ? gr : gr * 0.80f;
                gb.AddPrism(new Vector3(0f, 0f, z), r, r, seg, gs, MeshAxis.Z,
                            (i % 2) * (180f / gs), true, true, false, uv);
            }

            // 앞쪽 마감 캡 — 둥근 끝. 잘린 원통은 「부러진 것」으로 보인다.
            gb.AddPrism(new Vector3(0f, 0f, -gl * 0.5f + 0.008f), gr * 1.18f, gr * 0.55f, 0.026f,
                        gs, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh gripMesh = SaveMesh(gb.ToMesh("LeverGrip"), "LeverGrip");
            var grip = new GameObject("Grip");
            grip.transform.SetParent(pivot.transform, false);
            grip.transform.localPosition = new Vector3(0f, 0f, -armLen - gl * 0.5f - 0.055f);
            Render(grip, gripMesh, "RedPaint");

            // ── 반동 ──
            // 명세 §5 「레버는 수직 슬롯을 따라 약 55도 범위로 움직인다」.
            // `LeverPhysics` 가 감쇠 스프링으로 각도를 적분한다 — **Lerp 는 끝에서
            // 멈추고, 사람이 「당겼다」를 읽는 신호는 도달이 아니라 반동이다**
            // (그 파일의 주석이 이 판단을 기록해 뒀다).
            //
            // 여기에 붙이는 이유: 이 피벗은 조립기가 만든 것이라 다른 주인이 없다.
            // 같은 트랜스폼에 두 주인을 두면 매 프레임 싸우고 그건 떨림으로 보인다.
            var phys = pivot.AddComponent<Prototype.Physics.LeverPhysics>();
            var pso = new SerializedObject(phys);
            SetFloat(pso, "_maxSwingDegrees", ReferenceRoomSpec.LeverSwingDegrees);
            SetVector(pso, "_axis", Vector3.right);          // 앞뒤로 당기는 레버
            SetFloat(pso, "_pullSign", -1f);                 // 아래로
            SetFloat(pso, "_omega", 17f);
            SetFloat(pso, "_zeta", 0.19f);                   // 두세 번 튄다
            SetFloat(pso, "_holdSeconds", 0.12f);            // 손이 아직 레버에 있다
            pso.ApplyModifiedProperties();

            // ── 경고등 ──
            GameObject lamp = Child(root.transform, WarningLampName);
            lamp.transform.localPosition = new Vector3(ReferenceRoomSpec.LeverColumnCenterX,
                                                      ReferenceRoomSpec.WarningLampCenterY,
                                                      ReferenceRoomSpec.WallRearZ);
            float lr = ReferenceRoomSpec.WarningLampDiameter * 0.5f;
            var lb = new ProcMeshBuilder();
            lb.AddPrism(new Vector3(0f, 0f, -0.030f), lr, lr, 0.060f, ReferenceRoomSpec.WarningLampSides, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh housingMesh = SaveMesh(lb.ToMesh("WarningLampHousing"), "WarningLampHousing");
            var housing = new GameObject("Housing");
            housing.transform.SetParent(lamp.transform, false);
            Render(housing, housingMesh, "BareSteel");

            var lensB = new ProcMeshBuilder();
            lensB.AddPrism(Vector3.zero, lr * 0.72f, lr * 0.56f, 0.030f, ReferenceRoomSpec.WarningLampSides, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh lensMesh = SaveMesh(lensB.ToMesh("WarningLampLens"), "WarningLampLens");
            var lens = new GameObject("Lens");
            lens.transform.SetParent(lamp.transform, false);
            lens.transform.localPosition = new Vector3(0f, 0f, -0.062f);
            Renderer lensRenderer = Render(lens, lensMesh, "RedPaint");

            // 명세 §5 「평상시에는 매우 약하게 빛남」 — 기본 발광을 낮게 두고,
            // 런타임에 `MaterialPropertyBlock` 이 점멸을 건다.
            var lensMat = new Material(P("RedPaint")) { name = "RM_WarningLens" };
            lensMat.SetColor("_BaseColor", ReferenceRoomSpec.FadedRed);
            lensMat.SetColor("_EmissionColor", new Color(0.85f, 0.12f, 0.07f) * 0.35f);
            lensMat.EnableKeyword("_EMISSION");
            lensMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SaveMaterial(lensMat, "WarningLens");
            lensRenderer.sharedMaterial = lensMat;

            // 보호 링과 볼트
            var ringB = new ProcMeshBuilder();
            for (int i = 0; i < ReferenceRoomSpec.WarningLampSides; i++)
            {
                float am = (i + 0.5f) * Mathf.PI * 2f / ReferenceRoomSpec.WarningLampSides;
                float segLen = 2f * Mathf.Tan(Mathf.PI / ReferenceRoomSpec.WarningLampSides) * lr * 0.86f;
                ringB.AddBox(new Vector3(Mathf.Cos(am) * lr * 0.86f, Mathf.Sin(am) * lr * 0.86f, -0.066f),
                             new Vector3(segLen, 0.026f, 0.020f),
                             Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg + 90f), 0f, uv);
            }
            Mesh guardMesh = SaveMesh(ringB.ToMesh("WarningLampGuard"), "WarningLampGuard");
            var guard = new GameObject("Guard");
            guard.transform.SetParent(lamp.transform, false);
            Render(guard, guardMesh, "BareSteel");

            // ── 표지판 ──
            // 명세 §5 「OVERHARVEST / CRITICAL SYSTEM · 페인트가 일부 벗겨진 공업용 스텐실」.
            // 글자는 텍스처가 나른다(명세 §14). 여기서는 판만 세운다.
            var sign = new GameObject("LeverSign");
            sign.transform.SetParent(column.transform, false);
            // 컬럼 원점이 이미 후면 벽이므로 z 는 상대값이다. 컬럼 전면(−cd)보다
            // 조금 더 앞에 붙여야 판이 하우징에 묻히지 않는다.
            sign.transform.localPosition = new Vector3(0f, ReferenceRoomSpec.LeverSignCenterY, -cd - 0.008f);
            Slab(sign.transform, "Plate", Vector3.zero,
                 new Vector3(ReferenceRoomSpec.LeverSignWidth, ReferenceRoomSpec.LeverSignHeight, 0.012f), "Sign");

            _report.AppendLine($"  {LeverBaseName} — 컬럼 {cw} × {ch} × {cd} @ x={ReferenceRoomSpec.LeverColumnCenterX:F2} " +
                               $"· 회전축 y={ReferenceRoomSpec.LeverPivotY} · 가동 {ReferenceRoomSpec.LeverSwingDegrees}° (슬롯 {slotH:F3}m) " +
                               $"· {WarningLampName} ⌀{ReferenceRoomSpec.WarningLampDiameter} @ y={ReferenceRoomSpec.WarningLampCenterY}");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §6 전력 표시기
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildPowerMeter(GameObject root)
        {
            GameObject meter = Child(root.transform, PowerMeterName);
            meter.transform.localPosition = new Vector3(ReferenceRoomSpec.PowerMeterCenterX,
                                                       ReferenceRoomSpec.PowerMeterCenterY,
                                                       ReferenceRoomSpec.WallRearZ);

            float w = ReferenceRoomSpec.PowerMeterWidth;
            float h = ReferenceRoomSpec.PowerMeterHeight;
            float d = ReferenceRoomSpec.PowerMeterDepth;
            float bezel = 0.055f;

            // 두꺼운 검은 철제 프레임. 명세 §6.
            Slab(meter.transform, "Frame_Top",    new Vector3(0f,  h * 0.5f - bezel * 0.5f, -d * 0.5f), new Vector3(w, bezel, d), "Steel");
            Slab(meter.transform, "Frame_Bottom", new Vector3(0f, -h * 0.5f + bezel * 0.5f, -d * 0.5f), new Vector3(w, bezel, d), "Steel");
            Slab(meter.transform, "Frame_Left",   new Vector3(-w * 0.5f + bezel * 0.5f, 0f, -d * 0.5f), new Vector3(bezel, h, d), "Steel");
            Slab(meter.transform, "Frame_Right",  new Vector3( w * 0.5f - bezel * 0.5f, 0f, -d * 0.5f), new Vector3(bezel, h, d), "Steel");
            Slab(meter.transform, "Back",         new Vector3(0f, 0f, -d * 0.15f), new Vector3(w, h, d * 0.3f), "Steel");

            // 화면. 명세 §6 「오래된 전광판 또는 세그먼트 숫자 장치 · 유리는 약간 흐리다」.
            // **`Readout` 은 배선 지점이다** — `InstrumentPanelView` 가 여기에 숫자를 그린다.
            var readout = new GameObject("Readout");
            readout.transform.SetParent(meter.transform, false);
            readout.transform.localPosition = new Vector3(0f, 0f, -d + 0.012f);
            Slab(readout.transform, "Screen", Vector3.zero, new Vector3(w - bezel * 2f, h - bezel * 2f, 0.012f), "Glass");

            // 텍스트 앵커 셋. 명세 §6 이 요구하는 세 줄의 자리를 **형상으로 남긴다** —
            // 나중에 TMP 를 붙일 때 위치를 다시 재지 않아도 되고, 캡처 판정이
            // 「어느 줄이 안 읽히는가」를 지목할 수 있다.
            Anchor(readout.transform, "Anchor_Power",    new Vector3(0f,  (h - bezel * 2f) * 0.34f, -0.010f));
            Anchor(readout.transform, "Anchor_Value",    new Vector3(0f,  0f,                       -0.010f));
            Anchor(readout.transform, "Anchor_Required", new Vector3(0f, -(h - bezel * 2f) * 0.36f, -0.010f));

            _report.AppendLine($"  {PowerMeterName} — {w} × {h} × {d} @ x={ReferenceRoomSpec.PowerMeterCenterX:F2} y={ReferenceRoomSpec.PowerMeterCenterY} " +
                               $"· 읽기 거리 요구 {ReferenceRoomSpec.PowerMeterReadDistance}m");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §7 오른쪽 보관 선반
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildStorage(GameObject root)
        {
            GameObject shelf = Child(root.transform, ShelfName);
            shelf.transform.localPosition = new Vector3(ReferenceRoomSpec.ShelfCenterX, 0f, ReferenceRoomSpec.ShelfCenterZ);

            float len = ReferenceRoomSpec.ShelfLength;
            float dep = ReferenceRoomSpec.ShelfDepth;
            float top = ReferenceRoomSpec.ShelfTopHeight;
            float tt = ReferenceRoomSpec.ShelfTopThickness;
            float low = ReferenceRoomSpec.ShelfLowerHeight;

            Slab(shelf.transform, "TopPlate", new Vector3(0f, top - tt * 0.5f, 0f), new Vector3(dep, tt, len), "BareSteel");
            Slab(shelf.transform, "LowerShelf", new Vector3(0f, low, 0f), new Vector3(dep - 0.06f, 0.03f, len - 0.10f), "Steel");

            // 용접된 앵글 철제 프레임 — 수직 지지대. 명세 §7 「네 개 이상」.
            int legs = ReferenceRoomSpec.ShelfLegCount;
            for (int i = 0; i < legs; i++)
            {
                float z = Mathf.Lerp(-len * 0.5f + 0.09f, len * 0.5f - 0.09f, i / (float)(legs - 1));
                for (int s = 0; s < 2; s++)
                {
                    float x = s == 0 ? -dep * 0.5f + 0.045f : dep * 0.5f - 0.045f;
                    Slab(shelf.transform, $"Leg_{i}_{s}", new Vector3(x, (top - tt) * 0.5f, z),
                         new Vector3(0.045f, top - tt, 0.045f), "BareSteel");
                }
            }
            // 앞뒤 가로대
            Slab(shelf.transform, "Rail_Front", new Vector3(-dep * 0.5f + 0.045f, low - 0.03f, 0f), new Vector3(0.035f, 0.035f, len - 0.14f), "BareSteel");
            Slab(shelf.transform, "Rail_Back",  new Vector3( dep * 0.5f - 0.045f, low - 0.03f, 0f), new Vector3(0.035f, 0.035f, len - 0.14f), "BareSteel");

            // ── 프롭 ──
            // 명세 §7 이 상판·하단에 놓을 물건을 **정확히 열거한다.** 그 이상 놓지 않는다
            // (명세 「별도의 지시가 없는 요소를 임의로 추가하지 마라」).
            GameObject props = Child(root.transform, PropsName);
            props.transform.localPosition = shelf.transform.localPosition;

            float topY = top;
            // 상판: 공구함 1 · TOOLS 01 상자 1 · 부품 바구니 1 · 오일통 2~3
            Prop(props.transform, "Toolbox",     new Vector3(0f, topY + 0.11f, -0.95f), new Vector3(0.24f, 0.22f, 0.44f), "BareSteel");
            Prop(props.transform, "Handle",      new Vector3(0f, topY + 0.24f, -0.95f), new Vector3(0.03f, 0.05f, 0.20f), "Grease");
            Prop(props.transform, "Box_Tools01", new Vector3(0f, topY + 0.08f, -0.34f), new Vector3(0.28f, 0.16f, 0.46f), "Steel");
            Prop(props.transform, "PartsBasket", new Vector3(0f, topY + 0.07f,  0.16f), new Vector3(0.26f, 0.14f, 0.30f), "Rust");
            Prop(props.transform, "OilCan_A",    new Vector3(-0.06f, topY + 0.13f, 0.66f), new Vector3(0.15f, 0.26f, 0.15f), "Grease");
            Prop(props.transform, "OilCan_B",    new Vector3( 0.10f, topY + 0.10f, 0.83f), new Vector3(0.13f, 0.20f, 0.13f), "Grease");
            Prop(props.transform, "OilCan_C",    new Vector3(-0.02f, topY + 0.08f, 1.06f), new Vector3(0.12f, 0.16f, 0.12f), "Rust");

            // 하단: 보급품 상자 2~3 · SUPPLIES 12 큰 상자 1 · 붉은 스트랩 공구 케이스 1 · 수리 장비 1
            float lowY = low + 0.015f;
            Prop(props.transform, "Supply_A",       new Vector3(0f, lowY + 0.14f, -0.92f), new Vector3(0.40f, 0.28f, 0.50f), "Steel");
            Prop(props.transform, "Supply_B",       new Vector3(0f, lowY + 0.12f, -0.32f), new Vector3(0.38f, 0.24f, 0.42f), "Rust");
            Prop(props.transform, "Box_Supplies12", new Vector3(0f, lowY + 0.17f,  0.28f), new Vector3(0.44f, 0.34f, 0.62f), "Steel");
            Prop(props.transform, "ToolCase",       new Vector3(0f, lowY + 0.10f,  0.92f), new Vector3(0.36f, 0.20f, 0.44f), "BareSteel");
            Prop(props.transform, "Strap",          new Vector3(0f, lowY + 0.10f,  0.92f), new Vector3(0.37f, 0.045f, 0.046f), "RedPaint");
            Prop(props.transform, "RepairRig",      new Vector3(0f, lowY + 0.09f,  1.22f), new Vector3(0.30f, 0.18f, 0.24f), "Grease");

            // ── 벽 표지판 ──
            GameObject signs = Child(root.transform, SignsName);
            signs.transform.localPosition = new Vector3(ReferenceRoomSpec.WallRightX, ReferenceRoomSpec.WallSignCenterY, 0f);
            signs.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            // 「STORAGE」 스텐실 + 짧은 밑줄. 글자는 텍스처가 나른다(명세 §14).
            Slab(signs.transform, "Stencil_Storage", new Vector3(-0.55f, 0.10f, 0.008f), new Vector3(0.62f, 0.16f, 0.006f), "Sign");
            Slab(signs.transform, "Stencil_Underline", new Vector3(-0.55f, -0.02f, 0.008f), new Vector3(0.62f, 0.022f, 0.006f), "Sign");
            // 안전 표지판. 명세 §7 「크림색 바탕에 검은 그림과 글씨」.
            Slab(signs.transform, "Sign_SafetyFirst", new Vector3(0.52f, 0.0f, 0.010f), new Vector3(0.52f, 0.62f, 0.008f), "Sign");

            _report.AppendLine($"  {ShelfName} — {len} × {dep} · 상판 {top} · 하단 {low} · 지지대 {legs}×2 " +
                               $"· {PropsName} 상판 7 / 하단 6 · {SignsName} 3");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §12 콜라이더 — **이게 없으면 플레이어가 바닥을 뚫고 떨어진다**
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 🔴 **이 함수가 없어서 게임이 시작하자마자 플레이어가 떨어졌다.**
        ///
        /// 구 셸(`GrayboxWorld/Car/Floor` 등)을 비활성화하면서 그 콜라이더도 같이
        /// 사라졌는데, 새 방에는 콜라이더를 붙이지 않았다. 진행 문서에 「콜라이더는
        /// 아직」이라고 **적어 두기까지 했으면서** 그것이 「미완성 항목」이 아니라
        /// **「게임이 즉시 망가지는 결함」**이라는 것을 못 봤다.
        ///
        /// 증상이 「화면이 검다」로 나타나는 것이 이 결함의 고약한 점이다 —
        /// 플레이어가 방 밖 허공으로 떨어지면 보이는 것은 조명 없는 빈 공간이고,
        /// 그건 「조명이 잘못됐다」와 화면에서 구분되지 않는다. 나는 이 세션에서
        /// 조명·재질·셰이더를 다섯 번 고쳤고, 그중 어느 것도 원인이 아니었다.
        /// **사용자가 직접 플레이해서 찾았다.**
        ///
        /// 명세 §12 를 그대로 따른다 —
        ///   · 벽과 선반에는 **단순한 Box Collider**
        ///   · 원형 관찰창에는 복잡한 메시 콜라이더를 쓰지 않는다
        ///   · 작은 볼트와 장식에는 개별 콜라이더를 만들지 않는다
        ///   · 중앙 이동 공간에는 보이지 않는 충돌체가 남아 있으면 안 된다
        /// </summary>
        private static void AddColliders(GameObject root)
        {
            // 이름으로 고른다. 「렌더러가 있으면 전부」로 하면 볼트 수백 개에
            // 콜라이더가 붙고, 그건 명세가 명시적으로 금지한다.
            var solid = new HashSet<string>
            {
                // 셸 — 바닥·벽·천장. 바닥이 이 목록의 존재 이유다.
                "Wall_Left", "Wall_Right", "Wall_Rear", "Wall_Front", "Ceiling",
                "Border_Left", "Border_Right", "Border_Front", "Border_Rear",
                // 장치 — 통과해 들어갈 수 없어야 한다
                "BackPlate", "Frame_Top", "Frame_Bottom", "Frame_Left", "Frame_Right",
                // 레버 컬럼과 전력 표시기
                "Housing", "Back",
                // 선반 — 명세 §7 「벽과 선반에는 단순한 Box Collider」
                "TopPlate", "LowerShelf",
                // 가위문 — 문틀과 격자
                "Jamb_Front", "Jamb_Rear", "Header", "Lattice",
            };

            int added = 0, floors = 0;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!solid.Contains(mf.gameObject.name)) continue;
                if (mf.gameObject.GetComponent<Collider>() != null) continue;
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                // `AddComponent<BoxCollider>` 는 메시 경계로 자동 맞춰지지만,
                // 명시적으로 다시 써서 멱등성을 지킨다.
                if (mf.sharedMesh != null)
                {
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                }
                added++;
            }

            // ── 바닥은 **따로, 확실하게** ──
            // 타공판(`FloorCenterGrate/Plate`)은 0.018m 낮게 앉아 있고 테두리는 별개다.
            // 둘 사이의 이음매로 빠질 여지를 없애기 위해 방 전체를 덮는 바닥 콜라이더를
            // 하나 더 둔다. 보이지 않는 판이지만 **중앙 이동 공간을 막지 않는다** —
            // 바닥면 아래에 있기 때문이다(명세 §12 마지막 줄).
            var floor = new GameObject("FloorCollider");
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -ReferenceRoomSpec.ShellThickness * 0.5f, 0f);
            var fc = floor.AddComponent<BoxCollider>();
            fc.size = new Vector3(ReferenceRoomSpec.InteriorWidth,
                                  ReferenceRoomSpec.ShellThickness,
                                  ReferenceRoomSpec.InteriorDepth);
            floors++;

            _report.AppendLine($"  콜라이더 — 구조물 {added}개 + 바닥 전면 {floors}개 " +
                               "(볼트·장식·관찰창에는 붙이지 않는다 — 명세 §12)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §2 카메라
        // ══════════════════════════════════════════════════════════════════════

        private static void PlaceCamera()
        {
            int touched = 0;
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.orthographic) continue;
                Undo.RecordObject(cam, "Reference room camera");
                // **수직값을 넣는다.** 명세는 수평 72~78 이고 Unity 필드는 수직이다.
                cam.fieldOfView = ReferenceRoomSpec.VerticalFov;
                EditorUtility.SetDirty(cam);
                touched++;
            }
            // ── 기준 구도 ──
            // 명세 §2 「앞쪽 중앙에서 후면 벽을 바라본다 · 카메라는 수평을 유지」.
            //
            // ⚠ **눈높이를 여기서 더하지 않는다.** 이 저장소는 눈높이 소유자를 둘로
            // 갈랐다가 1.62 가 두 번 더해져 시점이 천장 위로 간 적이 있다
            // (`TOPDOWN_PROGRESS` 「카메라가 눈높이보다 1.6m 높다」). 눈높이는 계층이
            // 소유하므로 여기서는 **바닥 위치와 방향만** 준다.
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Undo.RecordObject(player.transform, "Reference room camera");
                player.transform.position = new Vector3(0f, 0f, ReferenceRoomSpec.CameraZ);
                player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);   // +Z = 장치 벽
                EditorUtility.SetDirty(player);
                _report.AppendLine($"  Player — (0, 0, {ReferenceRoomSpec.CameraZ:F2}) 정면 +Z " +
                                   $"· 후면 벽까지 {ReferenceRoomSpec.CameraToRearWall:F2}m " +
                                   $"(눈높이는 계층이 소유 — 여기서 더하지 않는다)");
            }
            else _report.AppendLine("  ⚠ `Player` 를 찾지 못했다 — 기준 구도를 세우지 못했다");

            _report.AppendLine($"  카메라 {touched}대 — 수평 {ReferenceRoomSpec.HorizontalFovDegrees}° " +
                               $"→ 수직 {ReferenceRoomSpec.VerticalFov:F2}° (16:9)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  헬퍼
        // ══════════════════════════════════════════════════════════════════════

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        private static void SetVector(SerializedObject so, string field, Vector3 value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.vector3Value = value;
        }

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Anchor(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
        }

        /// <summary>축 정렬 상자 하나. 셸·프레임·판 대부분이 이것이다.</summary>
        private static GameObject Slab(Transform parent, string name, Vector3 localPos, Vector3 size, string material)
        {
            var b = new ProcMeshBuilder();
            b.AddBox(Vector3.zero, size, 0f, ReferenceRoomSpec.SurfaceUvPerMeter);
            Mesh mesh = SaveMesh(b.ToMesh(name), $"Slab_{name}_{size.x:F3}x{size.y:F3}x{size.z:F3}");

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Render(go, mesh, material);
            return go;
        }

        /// <summary>
        /// 프롭 상자. 명세 §12 「작은 볼트와 장식에는 개별 콜라이더를 만들지 않는다」에
        /// 따라 콜라이더를 붙이지 않는다.
        /// </summary>
        private static GameObject Prop(Transform parent, string name, Vector3 localPos, Vector3 size, string material)
            => Slab(parent, name, localPos, size, material);

        private static void AddBolt(Transform parent, Mesh mesh, Vector3 localPos, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Render(go, mesh, "BareSteel");
        }

        private static Renderer Render(GameObject go, Mesh mesh, string material)
        {
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = P(material);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return mr;
        }

        // ── 에셋 굽기 ────────────────────────────────────────────────────────
        //
        // 메시를 에셋으로 굽지 않으면 씬 YAML 안에 정점 배열이 통째로 들어간다.
        // 이 저장소는 씬 파일이 조용히 손상된 이력이 있고, 그 위험은 파일이 클수록 커진다.

        private const string MeshDir = "Assets/Prototype_Elevator/Art/Meshes/Room";

        /// <summary>이번 실행에서 이미 구운 메시. 같은 키를 두 번 구우면 에셋이 갈린다.</summary>
        private static readonly Dictionary<string, Mesh> BakedMeshes = new Dictionary<string, Mesh>();

        /// <summary>
        /// 메시를 에셋으로 굽고 **에셋 쪽 객체를 돌려준다.**
        ///
        /// ⚠ 돌려주는 것이 중요하다. 첫 판본은 `void` 였고 호출부가 <b>임시 메시</b>를
        /// 그대로 `MeshFilter` 에 물렸다. 그러면 씬은 에셋이 아니라 메모리 객체를
        /// 가리키고, 저장할 때 정점 배열이 **씬 YAML 안으로 통째로 들어가거나**
        /// 참조가 끊긴다. 두 결과 다 「조립 직후에는 멀쩡해 보이고 다음에 열면 깨져
        /// 있다」로 나타나는데, 그건 이 저장소가 직렬화 에셋에서 반복해서 당한
        /// 실패의 정확한 형태다.
        /// </summary>
        private static Mesh SaveMesh(Mesh mesh, string key)
        {
            if (!Directory.Exists(MeshDir)) Directory.CreateDirectory(MeshDir);
            string safe = key.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            string path = $"{MeshDir}/{safe}.asset";

            if (BakedMeshes.TryGetValue(path, out Mesh cached) && cached != null)
            {
                // 같은 형상을 또 만들었다. 임시 객체를 흘리지 않고 회수한다 —
                // `Mesh` 는 네이티브 객체라 GC 가 치우지 않는다.
                if (mesh != cached) Object.DestroyImmediate(mesh);
                return cached;
            }

            // 이름을 **두 경로 모두에서** 파일명과 맞춘다. `CopySerialized` 는 이름까지
            // 복사하므로, 생성 경로에서만 맞추면 재조립 때마다 경고가 돌아온다.
            mesh.name = safe;

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                BakedMeshes[path] = existing;
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            BakedMeshes[path] = mesh;
            return mesh;
        }

        private static void SaveMaterial(Material m, string key)
        {
            string path = $"{MaterialDir}/RM_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); return; }
            AssetDatabase.CreateAsset(m, path);
        }
    }
}
