using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// AD47 결과판 아홉 칸의 심볼 3종을 <c>SYM_SlotSymbols.fbx</c> 의 저작 메시로 세운다.
    ///
    /// ## 무엇을 바꾸는가
    ///
    /// 여기까지 결과판에 있던 것은 <see cref="Ascend.CaptureHarness.EditorTools"/> 의
    /// AD47 재배선이 만든 **프리미티브 그레이박스**였다 — 구 하나 / 구+구 / 구 넷.
    /// 색 외 차이가 실루엣뿐이라 「구를 몇 개 붙였나」로만 갈렸다.
    /// 이제 블렌더에서 저작한 실물 오브제로 갈아 끼운다.
    ///
    ///     Sym_NormalSoul    사과   SYM_Apple + SYM_Apple_Leaf
    ///     Sym_Absorber      눈알   SYM_Eye + Iris + Pupil + Cornea
    ///     Sym_Proliferator  버섯   SYM_Mushroom
    ///
    /// FBX 의 나머지 넷(뼈·심장·어금니·새싹)은 대응하는 <c>SymbolKind</c> 가 아직 없어
    /// 임포트만 하고 씬에 넣지 않는다.
    ///
    /// ## 이름은 계약이다
    ///
    /// 칸의 **직속 자식 이름** 세 개가 <see cref="Prototype.View.SpinBoardView.KindOf"/> 의
    /// switch 와 글자 단위로 같아야 한다. 그래서 이 스크립트는 껍데기(이름·좌표·토글 구조)를
    /// 그대로 두고 **안만 갈아 끼운다.** 조각은 그 이름 아래의 손자로 들어가므로
    /// <c>SetCell</c> 의 최상위 순회는 건드리지 않는다.
    ///
    /// ## 멱등이다
    ///
    /// 「없으면 만든다」가 아니라 **「항상 이 상태로 만든다」** — 메시·재질·좌표·회전·
    /// 스케일·활성 상태를 매번 절대값으로 다시 쓴다. 두 번 돌려 같은 결과여야 한다.
    /// 스케일을 델타로 곱하면 두 번째 실행에서 심볼이 두 번 줄어든다(이 저장소가
    /// `Reproportion Elevator Car` 에서 이미 당했다).
    ///
    /// ## 축 — 왜 회전이 항등인가
    ///
    /// 칸(챔버 유리에서 유도된 좌표계)의 축은 실측으로 이렇다.
    ///
    ///     칸 local +X → world −X      칸 local +Y → world +Z (플레이어 반대쪽)
    ///     칸 local +Z → world +Y (위)
    ///
    /// FBX 의 **메시 데이터**는 블렌더 축 그대로다(Z 위, 앞이 −Y). 임포트된 프리팹의
    /// 노드가 들고 있는 <c>Rx(270)·scale 100</c> 이 Unity 축으로 옮기는 변환인데,
    /// 우리는 메시를 직접 물리므로 그 변환을 우리가 쥔다. 그런데
    ///
    ///     블렌더 +Z(위)   → 칸 local +Z → world +Y (위)          ✔
    ///     블렌더 −Y(앞)   → 칸 local −Y → world −Z (플레이어)     ✔
    ///
    /// 즉 **칸 안에서는 회전이 항등**이고 스케일 100 만 남는다. 우연이 아니라
    /// 칸 좌표계가 이미 90° 돌아 있어서 두 회전이 서로 지워지는 것이다.
    /// (검산: Rx(270)·Rx(90) = Rx(360) = I)
    ///
    /// ## 크기
    ///
    /// 화면 크기는 <see cref="NormalSoulScreenSpan"/> 하나로 정한다 — 사과의 화면 폭을
    /// 그 값에 맞추는 **공통 배율**을 구해 셋에 똑같이 건다. 심볼마다 따로 정규화하지
    /// 않는 이유는 크기가 판독성 다섯 단서 중 하나이기 때문이다
    /// (`SYMBOL_DESIGN.md` §7 — 겉넓이 1.50배가 독립 단서 2로 센다).
    /// 따로 맞추면 그 축이 사라진다.
    /// </summary>
    internal static class AscendSlotSymbolSwap
    {
        public const string FbxPath = "Assets/Prototype_Elevator/Art/Models/SYM_SlotSymbols.fbx";
        private const string MatDir = "Assets/Prototype_Elevator/Materials/CabinAD47";

        /// <summary>
        /// FBX 메시 단위 → 미터. 임포트된 노드가 들고 있는 스케일과 같은 값이다
        /// (파일 단위가 cm 이고 <c>useFileScale</c> 이 켜져 있다).
        /// </summary>
        private const float MeshToMetres = 100f;

        /// <summary>
        /// 정상 영혼의 화면 지름(m). **현행 그레이박스 실측값**이다 —
        /// 교체 전 <c>Sym_NormalSoul</c> 의 월드 바운드가 0.0693 × 0.0693 이었다.
        /// 「화면상 크기를 지금과 비슷하게」가 요구였으므로 이 값이 기준점이 된다.
        /// </summary>
        private const float NormalSoulScreenSpan = 0.0693f;

        // 칸 직속 자식 이름 — SpinBoardView.KindOf 와 글자 단위로 같아야 한다.
        private const string NormalSoulName = "Sym_NormalSoul";
        private const string AbsorberName = "Sym_Absorber";
        private const string ProliferatorName = "Sym_Proliferator";

        /// <summary>한 조각 = 메시 하나와 그 서브메시 순서대로의 재질 키.</summary>
        private readonly struct Part
        {
            public readonly string Child;
            public readonly string Mesh;
            public readonly string[] Materials;

            public Part(string child, string mesh, params string[] materials)
            {
                Child = child;
                Mesh = mesh;
                Materials = materials;
            }
        }

        // 서브메시 순서는 FBX 프리팹의 MeshRenderer.sharedMaterials 에서 읽은 실측이다.
        //   SYM_Apple    [0]Apple [1]Stem
        //   SYM_Eye      [0]Sclera [1]Vein [2]Nerve
        //   SYM_Mushroom [0]MushCap [1]MushStem
        private static readonly Part[] NormalSoulParts =
        {
            new Part("Apple_Body", "SYM_Apple", "Apple", "AppleStem"),
            new Part("Apple_Leaf", "SYM_Apple_Leaf", "AppleLeaf"),
        };

        private static readonly Part[] AbsorberParts =
        {
            new Part("Eye_Ball", "SYM_Eye", "Sclera", "Vein", "Nerve"),
            new Part("Eye_Iris", "SYM_Eye_Iris", "Iris"),
            new Part("Eye_Pupil", "SYM_Eye_Pupil", "Pupil"),
            // 각막은 마지막이다 — 반투명이라 렌더 큐가 뒤에 서야 홍채·동공이 비쳐 보인다.
            new Part("Eye_Cornea", "SYM_Eye_Cornea", "Cornea"),
        };

        private static readonly Part[] ProliferatorParts =
        {
            new Part("Mush_Body", "SYM_Mushroom", "MushCap", "MushStem"),
        };

        [MenuItem("Ascend/Cabin/4. 결과판 심볼을 SM_SlotSymbols 로 교체")]
        public static void SwapAll()
        {
            var log = new StringBuilder("[상승] 결과판 심볼 교체\n");

            Dictionary<string, Mesh> meshes = LoadMeshes(log);
            if (meshes == null) return;

            Transform cells = FindBoardCells(log);
            if (cells == null) return;

            float scale = ResolveScale(meshes, log);

            int built = 0;
            for (int i = 0; i < cells.childCount; i++)
            {
                Transform cell = cells.GetChild(i);
                if (!cell.name.StartsWith("Cell_")) continue;
                BuildCell(cell, meshes, scale);
                built++;
            }
            log.AppendLine("  칸 " + built + "개 재구성");

            if (!EditorApplication.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            AppendMeasurements(cells, log);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// AD47 재배선이 칸을 새로 만들 때도 같은 심볼이 서도록 부르는 진입점.
        /// 이것이 없으면 재배선을 한 번 돌리는 순간 프리미티브 그레이박스가 되돌아온다.
        /// </summary>
        public static void BuildCell(Transform cell)
        {
            Dictionary<string, Mesh> meshes = LoadMeshes(null);
            if (meshes == null) return;
            BuildCell(cell, meshes, ResolveScale(meshes, null));
        }

        // ══════════════════════════════════════════════════════════════════════

        private static void BuildCell(Transform cell, Dictionary<string, Mesh> meshes, float scale)
        {
            BuildSymbol(cell, NormalSoulName, NormalSoulParts, meshes, scale);
            BuildSymbol(cell, AbsorberName, AbsorberParts, meshes, scale);
            BuildSymbol(cell, ProliferatorName, ProliferatorParts, meshes, scale);
        }

        /// <summary>
        /// 심볼 하나를 **항상 이 상태로** 세운다. 부모는 이름·스케일만 들고 그리지 않으며,
        /// 조각이 그 아래에 들어간다. 끝나면 꺼진 상태다 — <c>SpinBoardView</c> 가 켠다.
        /// </summary>
        private static void BuildSymbol(Transform cell, string name, Part[] parts,
                                        Dictionary<string, Mesh> meshes, float scale)
        {
            Transform root = EnsureChild(cell, name);

            // 부모는 그리지 않는다. 그레이박스 시절 여기 구 프리미티브가 직접 붙어 있었다.
            StripRenderable(root);

            Undo.RecordObject(root, "symbol root");
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;   // 위 주석의 축 검산 참조
            root.localScale = Vector3.one * (MeshToMetres * scale);

            // 조각들의 합집합 바운드 중심을 원점으로 옮긴다. **조각 좌표에 굽는다** —
            // 부모 위치에 넣으면 정화 맥동이 부모 스케일을 흔들 때 중심이 따라 흔들린다.
            Bounds group = GroupBounds(parts, meshes);
            Vector3 recentre = -group.center;

            var keep = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                Part part = parts[i];
                if (!meshes.TryGetValue(part.Mesh, out Mesh mesh) || mesh == null) continue;
                keep.Add(part.Child);

                Transform t = EnsureChild(root, part.Child);
                var go = t.gameObject;

                // ⚠ 컴포넌트를 **다 붙인 뒤에** 잡는다. `Undo.AddComponent` 는 대상을
                // 다시 직렬화해 직전에 얻어 둔 참조를 무효화한다(SymbolShapeFactory 주석).
                if (go.GetComponent<MeshFilter>() == null) Undo.AddComponent<MeshFilter>(go);
                if (go.GetComponent<MeshRenderer>() == null) Undo.AddComponent<MeshRenderer>(go);
                var mf = go.GetComponent<MeshFilter>();
                var mr = go.GetComponent<MeshRenderer>();

                mf.sharedMesh = mesh;

                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int s = 0; s < mats.Length; s++)
                {
                    string key = s < part.Materials.Length
                        ? part.Materials[s]
                        : part.Materials[part.Materials.Length - 1];
                    mats[s] = EnsureMaterial(key);
                }
                mr.sharedMaterials = mats;

                mr.enabled = true;
                // 챔버 안이라 그림자를 만들 이유가 없다 — 보이지 않는데 비용만 든다.
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                // 발광이 없으므로 조명이 전부다 — 프로브를 끄면 챔버 안에서 새까매진다.
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

                // 심볼은 조준 대상이 아니다. 콜라이더를 두면 뒤의 창·벽 조준을 방해한다.
                Collider col = go.GetComponent<Collider>();
                if (col != null) Undo.DestroyObjectImmediate(col);

                t.localPosition = recentre;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                if (!go.activeSelf) go.SetActive(true);
                EditorUtility.SetDirty(go);
            }

            Prune(root, keep);

            // 조립 직후의 정본 상태는 **전부 꺼짐**이다. SpinBoardView 가 하나만 켠다.
            if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
            EditorUtility.SetDirty(root.gameObject);
        }

        /// <summary>조각 전체를 감싸는 바운드(메시 단위). 재중심의 근거다.</summary>
        private static Bounds GroupBounds(Part[] parts, Dictionary<string, Mesh> meshes)
        {
            var b = new Bounds();
            bool first = true;
            for (int i = 0; i < parts.Length; i++)
            {
                if (!meshes.TryGetValue(parts[i].Mesh, out Mesh mesh) || mesh == null) continue;
                if (first) { b = mesh.bounds; first = false; }
                else b.Encapsulate(mesh.bounds);
            }
            return b;
        }

        /// <summary>
        /// 셋에 공통으로 걸 배율. 사과의 화면 폭(블렌더 X)이
        /// <see cref="NormalSoulScreenSpan"/> 이 되게 하는 값이다.
        /// 상수로 박지 않고 실측 바운드에서 유도한다 — FBX 가 바뀌면 따라 움직여야 한다.
        /// </summary>
        private static float ResolveScale(Dictionary<string, Mesh> meshes, StringBuilder log)
        {
            Bounds apple = GroupBounds(NormalSoulParts, meshes);
            float widthMetres = apple.size.x * MeshToMetres;
            if (widthMetres <= 0f) return 1f;
            float k = NormalSoulScreenSpan / widthMetres;
            log?.AppendLine(string.Format(
                "  공통 배율 k = {0:F4}  (사과 폭 {1:F1}mm → {2:F1}mm)",
                k, widthMetres * 1000f, NormalSoulScreenSpan * 1000f));
            return k;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  오브젝트
        // ══════════════════════════════════════════════════════════════════════

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "create symbol part");
            return go.transform;
        }

        /// <summary>부모가 스스로 그리지 않게 한다. 옛 프리미티브의 구 메시가 여기 있었다.</summary>
        private static void StripRenderable(Transform t)
        {
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null) Undo.DestroyObjectImmediate(mr);
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null) Undo.DestroyObjectImmediate(mf);
            Collider col = t.GetComponent<Collider>();
            if (col != null) Undo.DestroyObjectImmediate(col);
        }

        /// <summary>
        /// 허용 목록에 없는 자식을 **끈다.** 지우지 않는다 — 이 저장소는 씬 오브젝트를
        /// 지우지 않는다. 옛 그레이박스 조각(<c>Shell</c>/<c>Pit</c>/<c>Bud_*</c>)이 여기 걸린다.
        /// </summary>
        private static void Prune(Transform parent, List<string> allowed)
        {
            foreach (Transform child in parent)
            {
                if (allowed.Contains(child.name)) continue;
                if (child.gameObject.activeSelf)
                {
                    Undo.RecordObject(child.gameObject, "prune");
                    child.gameObject.SetActive(false);
                }
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled)
                {
                    Undo.RecordObject(mr, "prune renderer");
                    mr.enabled = false;
                    EditorUtility.SetDirty(mr);
                }
                EditorUtility.SetDirty(child.gameObject);
            }
        }

        private static Dictionary<string, Mesh> LoadMeshes(StringBuilder log)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[상승] " + FbxPath + " 를 찾을 수 없다 — 심볼 교체를 중단한다.");
                return null;
            }
            var map = new Dictionary<string, Mesh>();
            foreach (Object o in assets)
            {
                var m = o as Mesh;
                if (m != null) map[m.name] = m;
            }
            log?.AppendLine("  FBX 메시 " + map.Count + "개 로드");
            return map;
        }

        private static Transform FindBoardCells(StringBuilder log)
        {
            var cab = GameObject.Find("CabinAD47");
            if (cab == null) { Debug.LogError("[상승] CabinAD47 없음"); return null; }

            // `Find` 는 직속 자식만 본다 — BoardCells 는 소켓 아래로 옮겨갈 수 있다.
            foreach (Transform t in cab.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "BoardCells") continue;
                log?.AppendLine("  BoardCells 자식 " + t.childCount + "개");
                return t;
            }
            Debug.LogError("[상승] CabinAD47 아래에 BoardCells 가 없다");
            return null;
        }

        /// <summary>실측을 로그에 남긴다. 「보인다」가 아니라 숫자로 확인하기 위한 것이다.</summary>
        private static void AppendMeasurements(Transform cells, StringBuilder log)
        {
            if (cells.childCount == 0) return;
            Transform cell = cells.GetChild(0);
            log.AppendLine("  실측 (" + cell.name + ", 월드 mm):");
            foreach (Transform sym in cell)
            {
                bool wasOn = sym.gameObject.activeSelf;
                sym.gameObject.SetActive(true);
                var rs = sym.GetComponentsInChildren<Renderer>(false);
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    Vector3 s = b.size * 1000f;
                    Vector3 d = (b.center - cell.position) * 1000f;
                    log.AppendLine(string.Format(
                        "    {0,-18} 폭 {1,6:F1}  높이 {2,6:F1}  깊이 {3,6:F1}   중심오차 {4}",
                        sym.name, s.x, s.y, s.z, d.ToString("F1")));
                }
                sym.gameObject.SetActive(wasOn);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  재질 — 블렌더 절차적 재질의 대표색을 옮긴다
        // ══════════════════════════════════════════════════════════════════════
        //
        // FBX 에는 텍스처도 UV 도 없다(복셀 리메시가 UV 를 지운다). 임포트가 만든
        // 내장 재질은 절차적 노드를 읽지 못해 **거의 전부 기본 회색 0.906** 이다 —
        // 그대로 쓰면 일곱이 같은 회색 덩어리가 된다. 그래서 `build_orbs.py` 가
        // 실제로 쓴 **선형 색값**을 여기로 옮긴다.
        //
        // 노이즈로 섞이던 얼룩(blotch)은 재현할 수 없으므로 기본색에 25% 섞어
        // 「깨끗한 원색」이 되는 것만 막는다. 발광은 없다 — 원본 설계가 AD02 에서
        // 전부 걷어냈고(`SYMBOL_DESIGN.md` §4), 지시도 발광 금지다.

        private readonly struct Recipe
        {
            public readonly Color Base;
            public readonly float Smoothness;
            public readonly float Alpha;

            public Recipe(Color b, float smoothness, float alpha = 1f)
            {
                Base = b;
                Smoothness = smoothness;
                Alpha = alpha;
            }
        }

        private static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);

        private static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>
        {
            // ① 사과 — 탁한 황록 + 붉은 홍조. 왁스 광택
            { "Apple", new Recipe(
                Mix(Mix(new Color(0.082f, 0.086f, 0.026f), new Color(0.040f, 0.026f, 0.008f), 0.25f),
                    new Color(0.086f, 0.011f, 0.008f), 0.26f), 0.67f) },
            { "AppleStem", new Recipe(
                Mix(new Color(0.026f, 0.019f, 0.010f), new Color(0.013f, 0.009f, 0.005f), 0.25f), 0.18f) },
            { "AppleLeaf", new Recipe(
                Mix(new Color(0.058f, 0.072f, 0.024f), new Color(0.030f, 0.028f, 0.012f), 0.25f), 0.42f) },

            // ② 눈알 — 더러운 흰자 · 어두운 청록 홍채 · 순흑 동공 · 투명 각막
            { "Sclera", new Recipe(
                Mix(new Color(0.176f, 0.162f, 0.140f), new Color(0.086f, 0.034f, 0.028f), 0.25f), 0.67f) },
            { "Vein", new Recipe(
                Mix(new Color(0.105f, 0.014f, 0.012f), new Color(0.050f, 0.008f, 0.007f), 0.25f), 0.58f) },
            { "Nerve", new Recipe(
                Mix(new Color(0.082f, 0.056f, 0.052f), new Color(0.038f, 0.022f, 0.021f), 0.25f), 0.30f) },
            { "Iris", new Recipe(
                Mix(new Color(0.020f, 0.046f, 0.049f), new Color(0.006f, 0.014f, 0.017f), 0.25f), 0.78f) },
            { "Pupil", new Recipe(new Color(0.0018f, 0.0018f, 0.0021f), 0.70f) },
            { "Cornea", new Recipe(new Color(0.030f, 0.033f, 0.034f), 0.96f, 0.22f) },

            // ③ 버섯 — 청색 갓 + 창백한 자루. 일곱 중 유일한 한색이다
            { "MushCap", new Recipe(
                Mix(new Color(0.044f, 0.078f, 0.112f), new Color(0.014f, 0.026f, 0.046f), 0.25f), 0.56f) },
            { "MushStem", new Recipe(
                Mix(new Color(0.104f, 0.122f, 0.148f), new Color(0.044f, 0.056f, 0.074f), 0.25f), 0.39f) },
        };

        /// <summary>
        /// ⚠ **전 필드를 매번 다시 쓴다.** 기존 에셋을 재사용하면서 일부만 쓰면
        /// 지운 코드의 값이 에셋에 남아 계속 작동한다 — 이 저장소가 발광에서 한 번,
        /// 형상에서 한 번 당했다.
        /// </summary>
        private static Material EnsureMaterial(string key)
        {
            if (!System.IO.Directory.Exists(MatDir)) System.IO.Directory.CreateDirectory(MatDir);
            string path = MatDir + "/SYM_" + key + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material m = existing ?? new Material(lit) { name = "SYM_" + key };
            if (lit != null) m.shader = lit;

            Recipe r = Recipes.TryGetValue(key, out Recipe found)
                ? found
                : new Recipe(new Color(0.5f, 0.0f, 0.5f), 0.5f);   // 빠진 키는 자홍으로 튄다

            m.SetTexture("_BaseMap", null);
            m.SetTexture("_EmissionMap", null);
            m.DisableKeyword("_EMISSIONMAP_ON");
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionMapEnabled")) m.SetFloat("_EmissionMapEnabled", 0f);
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", r.Smoothness);
            var c = r.Base;
            c.a = r.Alpha;
            m.SetColor("_BaseColor", c);

            if (r.Alpha < 1f) Transparent(m);
            else Opaque(m);

            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        private static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        private static void Opaque(Material m)
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.renderQueue = -1;
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }
}
