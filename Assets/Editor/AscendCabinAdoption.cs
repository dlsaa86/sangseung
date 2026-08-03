using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// 블렌더에서 만든 카 내부 셸(`ELV_Cabin.fbx`)을 씬에 채택한다.
    ///
    /// ## 왜 블렌더로 갔나
    ///
    /// 2026-08-04 기준선 캡처에서 나온 것 — 배치와 구조는 레퍼런스와 맞는데
    /// **분위기가 안 나온다.** 원인은 둘이었다.
    ///
    ///  ① 평면 쿼드에 타일링 텍스처만 붙어 그림자가 생기지 않는다.
    ///     레퍼런스의 인상은 「오목 패널 + 돌출 스트랩 + 리벳 띠」가 만드는 **실제 음영**이다.
    ///     텍스처로는 그 음영이 안 나온다.
    ///  ② 바닥·천장·보·테두리·선반이 **무텍스처 밝은 회색**이었다. 벽은 15% 명도인데
    ///     이 슬래브들이 70% 라 값이 두 덩어리로 갈라졌다. 그 한 가지가
    ///     「블록아웃에 벽지 붙인 것」으로 읽히게 만든 주범이다.
    ///
    /// 그래서 면 분할을 **진짜 형상**으로 만들고(블렌더), 값 폭을 좁혔다.
    ///
    /// ## 되돌리는 법
    ///
    /// <see cref="Revert"/> 하나로 끝난다. 새 셸을 지우고 종전 오브젝트를 다시 켠다.
    /// 이 저장소는 셰이더·머티리얼 일괄 교체를 두 번 되돌린 이력이 있으므로
    /// (`AscendMaterialFactory` 주석), 채택은 **한 덩어리 · 원복 한 줄**로 둔다.
    /// </summary>
    internal static class AscendCabinAdoption
    {
        private const string FbxPath = "Assets/Prototype_Elevator/Art/Models/ELV_Cabin.fbx";
        private const string MatDir = "Assets/Prototype_Elevator/Materials/Cabin";
        private const string RootName = "CabinShell";
        private const string TexDir = "Assets/Prototype_Elevator/Art/Textures";

        /// <summary>스타일 셰이더를 쓸지. 되돌릴 일이 생기면 이 한 줄이다.</summary>
        private const bool UseStylized = true;

        /// <summary>
        /// 셸의 요(yaw). 블렌더 → FBX → Unity 축 변환에서 x 와 z 가 **함께** 뒤집혀 들어온다.
        ///
        /// 2026-08-04 실측 — 블렌더 (x, y, z) 가 유니티 (−x, z, −y) 로 떨어졌다.
        ///   · 가위문 개구부는 블렌더 −x 에 팠는데 유니티 x=+2.00 에 떨어졌다.
        ///     유니티의 <c>LeftScissorGate</c> 는 x=−1.96 이므로 **문이 벽으로 막혔다.**
        ///   · 장치 벽감은 블렌더 (+y, x=−0.35) 에 팠는데 유니티 (z=−2.30, x=+0.35) 에
        ///     떨어졌다. <c>SoulMachineFrame</c> 은 (z=+2.13, x=−0.35) 이므로 **반대 벽**이다.
        ///   · 벤치는 블렌더 +x(수납 벽)인데 유니티 x=−1.72(문 쪽)에 떨어졌다.
        ///
        /// y 축 180° 는 (x → −x, z → −z) 라 이 셋을 한 번에 맞춘다.
        /// 근본 원인은 <c>tools/blender/build_cabin.py</c> 헤더의 축 주석이 틀린 것이다
        /// (「+y = 장치 벽이 Unity +z」라고 적혀 있으나 실제로는 −z 로 간다).
        /// 거기를 고치고 FBX 를 다시 구우면 이 값은 0 이 된다.
        /// 지금은 FBX 를 다시 굽지 않고 채택 단계에서 흡수한다 — **절대값**이므로
        /// 몇 번을 돌려도 같은 자세가 된다.
        /// </summary>
        private const float ShellYaw = 180f;

        // ── 재질 정의 ─────────────────────────────────────────────────────────
        // 값 폭을 좁게 잡는다. 레퍼런스의 모든 면은 서로 몇 % 안쪽이고,
        // 면 분할은 밝기 차가 아니라 **음영**으로만 읽힌다.
        private struct MatDef
        {
            public string Name;
            public Color Base;
            public string Tex;
            public float Smooth;
            public Color Emission;
            public float Rim;
        }

        private static readonly MatDef[] Defs =
        {
            new MatDef { Name = "ELV_Iron",      Base = new Color(0.098f, 0.089f, 0.077f), Tex = "TEX_Iron_Rust",       Smooth = 0.10f, Rim = 0.22f },
            new MatDef { Name = "ELV_IronDark",  Base = new Color(0.058f, 0.054f, 0.050f), Tex = "TEX_Iron_Rust",       Smooth = 0.06f, Rim = 0.14f },
            new MatDef { Name = "ELV_Tread",     Base = new Color(0.076f, 0.068f, 0.059f), Tex = "TEX_FloorPlate_Rust", Smooth = 0.13f, Rim = 0.16f },
            new MatDef { Name = "ELV_Trim",      Base = new Color(0.116f, 0.103f, 0.085f), Tex = "TEX_Iron_Rust",       Smooth = 0.17f, Rim = 0.30f },
            new MatDef { Name = "ELV_Brass",     Base = new Color(0.196f, 0.150f, 0.086f), Tex = "TEX_Brass_Aged",      Smooth = 0.28f, Rim = 0.34f },
            new MatDef { Name = "ELV_LampGlass", Base = new Color(0.92f,  0.76f,  0.50f),  Tex = null,                  Smooth = 0.42f, Rim = 0.10f,
                         Emission = new Color(2.30f, 1.62f, 0.86f) },
        };

        /// <summary>종전 셸에서 새 셸이 대신하는 오브젝트들. 경로는 씬 루트부터.</summary>
        private static readonly string[] Superseded =
        {
            "ReferenceRoom/ElevatorShell/Wall_Left",
            "ReferenceRoom/ElevatorShell/Wall_Right",
            "ReferenceRoom/ElevatorShell/Wall_Rear",
            "ReferenceRoom/ElevatorShell/Wall_Front",
            "ReferenceRoom/ElevatorShell/Ceiling",
            "ReferenceRoom/FloorBorderPlates",
            "ReferenceRoom/FloorCenterGrate/Plate",
            "ReferenceRoom/FloorCenterGrate/Lip_Left",
            "ReferenceRoom/FloorCenterGrate/Lip_Right",
            "ReferenceRoom/FloorCenterGrate/Lip_Front",
            "ReferenceRoom/FloorCenterGrate/Lip_Rear",
            "ReferenceRoom/CeilingLamp/Cage",
            "ReferenceRoom/CeilingLamp/Bulb",
            "ReferenceRoom/StorageShelf",
        };

        /// <summary>접두사로 끄는 것들 — 레일·이음매·천장보는 새 벽에 형상으로 들어갔다.</summary>
        private static readonly string[] SupersededPrefixes =
        {
            "ReferenceRoom/ElevatorShell/Rail_",
            "ReferenceRoom/ElevatorShell/Seam_",
            "ReferenceRoom/ElevatorShell/CeilingBeam_",
        };

        [MenuItem("Ascend/Cabin/1. 블렌더 셸 채택")]
        public static void Adopt()
        {
            var log = new StringBuilder();
            log.AppendLine("=== ELV_Cabin 채택 ===");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) == null)
            {
                Debug.LogError($"[Cabin] FBX 없음: {FbxPath}. 블렌더에서 export() 를 먼저 돌린다.");
                return;
            }

            BuildMaterials(log);
            ConfigureImporter(log);
            PlaceIntoScene(log);
            DisableSuperseded(log);
            Relight(log);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 문 밖 통로. `DEVICE_DESIGN_SPEC` §2.6 이 **미달로 판정해 둔 실질 결함**이다
        /// (Notion 요구 3~5m, 종전 `Lobby` 1.4m). v1 캡처에서 가위문 너머로
        /// **생 스카이박스**가 보인 것(C 뷰 p99 0.561)도 같은 구멍이다.
        /// </summary>
        [MenuItem("Ascend/Cabin/3. 문 밖 통로 채택")]
        public static void AdoptShaft()
        {
            const string shaftFbx = "Assets/Prototype_Elevator/Art/Models/ELV_Shaft.fbx";
            const string shaftRoot = "CabinShaft";
            var log = new StringBuilder("=== ELV_Shaft 채택 ===\n");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(shaftFbx);
            if (prefab == null) { Debug.LogError($"[Shaft] FBX 없음: {shaftFbx}"); return; }

            var imp = AssetImporter.GetAtPath(shaftFbx) as ModelImporter;
            if (imp != null)
            {
                imp.globalScale = 1f;
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importTangents = ModelImporterTangents.None;
                imp.generateSecondaryUV = false;
                imp.addCollider = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                imp.SaveAndReimport();
            }

            var old = GameObject.Find(shaftRoot);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = shaftRoot;
            // 카 셸과 **같은 요**를 받아야 통로가 가위문 쪽(−x)에 선다.
            // 안 주면 반대편(+x) 벽 뒤에 생겨 화면에 영향이 없다.
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, ShellYaw, 0f));
            inst.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(inst, "Adopt shaft");
            ForceMaterials(inst, log);

            // 종전 백드롭 판이 통로를 가린다 — 통로가 생겼으니 그 판은 역할이 끝났다.
            var backdrop = FindByPath("ReferenceRoom/LeftScissorGate/ShaftBackdrop");
            if (backdrop != null && backdrop.activeSelf)
            {
                backdrop.SetActive(false);
                log.AppendLine("  ShaftBackdrop 비활성 (통로가 그 자리를 대신한다)");
            }

            // 통로 끝 벽등 — 이게 없으면 통로가 그냥 검은 구멍이라 깊이가 안 읽힌다.
            var lampGo = inst.transform.Find("ShaftLamp");
            if (lampGo == null)
            {
                var g = new GameObject("ShaftLamp");
                g.transform.SetParent(inst.transform, false);
                lampGo = g.transform;
            }
            lampGo.position = new Vector3(-4.51f, 1.90f, 1.28f);
            var sl = lampGo.GetComponent<Light>() ?? lampGo.gameObject.AddComponent<Light>();
            sl.type = LightType.Point;
            sl.color = new Color(1.00f, 0.74f, 0.46f);
            sl.intensity = 1.5f;
            sl.range = 4.2f;
            sl.shadows = LightShadows.None;
            log.AppendLine("  통로 벽등 배치 (깊이 판독)");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        [MenuItem("Ascend/Cabin/9. 채택 되돌리기")]
        public static void Revert()
        {
            var log = new StringBuilder("=== ELV_Cabin 원복 ===\n");
            var root = GameObject.Find(RootName);
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                log.AppendLine("  새 셸 제거");
            }
            foreach (var t in AllSuperseded())
            {
                var go = FindByPath(t);
                if (go != null && !go.activeSelf)
                {
                    go.SetActive(true);
                    log.AppendLine($"  복원 {t}");
                }
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        // ── 재질 ──────────────────────────────────────────────────────────────
        private static void BuildMaterials(StringBuilder log)
        {
            EnsureFolder(MatDir);
            var stylized = UseStylized ? Shader.Find("Ascend/Stylized") : null;
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            log.AppendLine($"  셰이더: stylized={(stylized != null ? "찾음" : "없음 → Lit 로 내려감")}");

            foreach (var d in Defs)
            {
                var path = $"{MatDir}/{d.Name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = stylized ?? lit;
                if (mat == null)
                {
                    mat = new Material(shader) { name = d.Name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != shader)
                {
                    mat.shader = shader;
                }

                SetColor(mat, "_BaseColor", d.Base);
                SetColor(mat, "_Color", d.Base);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", d.Smooth);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_RimStrength")) mat.SetFloat("_RimStrength", d.Rim);

                // 스타일 셰이더의 PS1 축 — 계단 명암 · 가파른 감쇠 · 회녹색 그림자.
                if (mat.HasProperty("_Steps")) mat.SetFloat("_Steps", 4f);
                if (mat.HasProperty("_FalloffPower")) mat.SetFloat("_FalloffPower", 2.9f);
                if (mat.HasProperty("_ShadowTint")) mat.SetColor("_ShadowTint", new Color(0.15f, 0.19f, 0.18f));
                if (mat.HasProperty("_ShadowLift")) mat.SetFloat("_ShadowLift", 0.42f);
                // 실내 점광에서 계단 0 번 칸이 0 이 되면 직접광이 통째로 사라진다.
                if (mat.HasProperty("_BandFloor")) mat.SetFloat("_BandFloor", 0.10f);

                if (d.Emission.maxColorComponent > 0f)
                {
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", d.Emission);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                if (!string.IsNullOrEmpty(d.Tex))
                {
                    var tex = LoadTexture(d.Tex);
                    if (tex != null)
                    {
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    }
                    else log.AppendLine($"    ⚠ 텍스처 못 찾음: {d.Tex} (색만 적용)");
                }
                EditorUtility.SetDirty(mat);
                log.AppendLine($"    {d.Name}  shader={mat.shader.name}");
            }
            AssetDatabase.SaveAssets();
        }

        private static Texture2D LoadTexture(string name)
        {
            foreach (var p in new[] { $"{TexDir}/{name}.png", $"{TexDir}/Generated/{name}.png" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null) return t;
            }
            var guids = AssetDatabase.FindAssets($"{name} t:Texture2D");
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        private static void SetColor(Material m, string prop, Color c)
        {
            if (m.HasProperty(prop)) m.SetColor(prop, c);
        }

        // ── 임포터 ────────────────────────────────────────────────────────────
        private static void ConfigureImporter(StringBuilder log)
        {
            var imp = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (imp == null) { log.AppendLine("  ⚠ ModelImporter 없음"); return; }

            imp.globalScale = 1f;
            imp.useFileUnits = true;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.isReadable = false;
            imp.importNormals = ModelImporterNormals.Import;   // 블렌더에서 face 로 구웠다
            imp.importTangents = ModelImporterTangents.None;   // 노멀맵을 쓰지 않는다
            imp.generateSecondaryUV = false;
            imp.addCollider = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            // .meta 에 External 이 **남아 있으면** 리맵이 통째로 무시된다.
            // 2026-08-04 실측: GetExternalObjectMap() 은 Cabin 폴더를 가리키는데
            // ForceUpdate 재임포트 뒤에도 렌더러는 Art/Models/Materials/*.mat 을 물었다.
            // 세터를 지우는 것만으로는 부족하다 — 저장된 값을 명시적으로 되돌려야 한다.
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;

            // `materialLocation = External` 은 Unity 6 에서 obsolete 다. 켜 두면 FBX 옆에
            // `Art/Models/Materials/*.mat` 을 자동 추출하는데, 그 추출본은 URP/Lit 이고
            // 이름이 우리 것과 같다. 그 상태에서 SearchAndRemapMaterials(Everywhere) 를
            // 부르면 **어느 쪽을 집을지 보장되지 않는다.**
            //
            // 2026-08-04 에 실제로 추출본이 걸렸다 — 리맵 6건이 전부 성공했다고 찍혔는데
            // 렌더러는 URP/Lit 을 물고 있었고, 값 폭을 좁히려고 잡은 베이스 컬러
            // (Iron 0.098) 대신 추출본의 0.323 이 들어가 3.3 배 밝았다. 진단 #1 을
            // 고치려던 채택이 진단 #1 을 그대로 재현한 셈이다.
            //
            // 그래서 검색하지 않고 **우리 폴더를 못박아** 건다.
            var remapped = new List<string>();
            foreach (var d in Defs)
            {
                var path = $"{MatDir}/{d.Name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { log.AppendLine($"    ⚠ 리맵 대상 없음: {path}"); continue; }
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), d.Name), mat);
                remapped.Add(d.Name);
            }
            imp.SaveAndReimport();

            var actual = imp.GetExternalObjectMap()
                            .Where(kv => kv.Value is Material)
                            .Select(kv => kv.Key.name + "→" + AssetDatabase.GetAssetPath(kv.Value))
                            .OrderBy(s => s).ToList();
            log.AppendLine($"  임포터 설정 완료. 재질 리맵 {actual.Count}건 (요청 {remapped.Count}건)");
            foreach (var s in actual) log.AppendLine("    " + s);
        }

        // ── 씬 배치 ───────────────────────────────────────────────────────────
        private static void PlaceIntoScene(StringBuilder log)
        {
            var old = GameObject.Find(RootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = RootName;
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, ShellYaw, 0f));
            inst.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(inst, "Adopt cabin shell");

            // 셸은 정적이다 — 배칭과 조명 계산에 들어가야 한다.
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);

            ForceMaterials(inst, log);

            var missing = new List<string>();
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var names = r.sharedMaterials.Select(m => m == null ? "(null)" : m.name).ToArray();
                if (names.Any(n => n == "(null)" || n.StartsWith("Default")))
                    missing.Add(r.name + ": " + string.Join("|", names));
            }
            log.AppendLine($"  배치: {RootName} 자식 {inst.transform.childCount}개, yaw={ShellYaw}°");
            if (missing.Count > 0)
                log.AppendLine("  ⚠ 재질 미할당: " + string.Join(" / ", missing));
            else
                log.AppendLine("  재질 전부 할당됨");

            var tris = inst.GetComponentsInChildren<MeshFilter>(true)
                           .Where(f => f.sharedMesh != null)
                           .Sum(f => f.sharedMesh.triangles.Length / 3);
            log.AppendLine($"  삼각형 합계 {tris}");
        }

        /// <summary>
        /// 인스턴스의 재질을 이름으로 Cabin 폴더 것에 **못박는다.**
        ///
        /// 임포터 리맵만 믿지 않는 이유: FBX 옆에 같은 이름의 추출본이 있고
        /// (`Art/Models/Materials/*.mat`, URP/Lit), 임포터 설정 한 줄이 어긋나면
        /// 조용히 그쪽이 물린다. 실제로 그렇게 걸렸고 **로그는 리맵 6건 성공이라고
        /// 찍혀 있었다** — 즉 리맵 성공 로그는 렌더러가 무엇을 물었는지 증명하지 않는다.
        ///
        /// 여기서 한 번 더 덮으면 임포터가 어떻게 굴든 결과가 같다.
        /// 이름 기준 절대 대입이라 몇 번을 돌려도 같은 상태가 된다.
        /// </summary>
        private static void ForceMaterials(GameObject inst, StringBuilder log)
        {
            var byName = new Dictionary<string, Material>();
            foreach (var d in Defs)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{d.Name}.mat");
                if (m != null) byName[d.Name] = m;
            }

            int swapped = 0;
            var unmatched = new List<string>();
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) { unmatched.Add(r.name + "[" + i + "]=(null)"); continue; }
                    // 추출본이든 임베드본이든 이름은 ELV_* 로 같다. 이름으로 건다.
                    if (byName.TryGetValue(mats[i].name, out var want))
                    {
                        if (mats[i] != want) { mats[i] = want; changed = true; swapped++; }
                    }
                    else unmatched.Add(r.name + "[" + i + "]=" + mats[i].name);
                }
                if (changed) r.sharedMaterials = mats;
            }
            log.AppendLine($"  재질 강제 대입: 교체 {swapped}슬롯"
                + (unmatched.Count > 0 ? " / ⚠ 이름 매칭 실패 " + string.Join(", ", unmatched.Distinct()) : " / 매칭 실패 없음"));
        }

        private static IEnumerable<string> AllSuperseded()
        {
            foreach (var s in Superseded) yield return s;
            foreach (var p in SupersededPrefixes)
            {
                var slash = p.LastIndexOf('/');
                var parentPath = p.Substring(0, slash);
                var prefix = p.Substring(slash + 1);
                var parent = FindByPath(parentPath);
                if (parent == null) continue;
                foreach (Transform c in parent.transform)
                    if (c.name.StartsWith(prefix, StringComparison.Ordinal))
                        yield return parentPath + "/" + c.name;
            }
        }

        private static void DisableSuperseded(StringBuilder log)
        {
            int n = 0, miss = 0;
            foreach (var path in AllSuperseded().Distinct())
            {
                var go = FindByPath(path);
                if (go == null) { miss++; continue; }
                if (go.activeSelf) { go.SetActive(false); n++; }
            }
            log.AppendLine($"  종전 셸 비활성 {n}개 (경로 못 찾음 {miss}개)");
        }

        // ── 조명 ──────────────────────────────────────────────────────────────
        private static void Relight(StringBuilder log)
        {
            // 레퍼런스의 조명은 「케이지 램프 하나의 따뜻한 웅덩이 + 빠른 감쇠 +
            // 거의 검은 구석」이다. 직전 씬은 대기광이 높아 전체가 균일하게 떴다.
            // 🔴 v1 실측으로 상향. 직전 값(0.020~0.048 · fog 0.055)은 화면의
            // **19~51% 를 완전 검정(<0.02)으로 뭉갰고** 실행 레버가 보이지 않았다.
            // `VISUAL_BIBLE` §6 의 실질 기준선이 여기서 걸렸다 —
            // 「어두워서 레버를 못 찾으면 분위기가 아니라 결함이다」.
            // 어둡게 만드는 것 자체가 목적이 아니라 **등 하나의 웅덩이가 읽히는 것**이
            // 목적이므로, 바닥을 올리고 대비는 점광이 만들게 둔다.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.132f, 0.145f, 0.158f);
            RenderSettings.ambientEquatorColor = new Color(0.096f, 0.104f, 0.112f);
            RenderSettings.ambientGroundColor = new Color(0.058f, 0.061f, 0.066f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.042f, 0.047f, 0.052f);
            // 4.6m 방에서 0.055 는 안개가 아니라 검은 커튼이었다.
            RenderSettings.fogDensity = 0.018f;

            var dir = GameObject.Find("Directional Light");
            if (dir != null)
            {
                var l = dir.GetComponent<Light>();
                if (l != null)
                {
                    // 실내 화물 엘리베이터에 태양은 없다. 형태를 잃지 않을 만큼만 남긴다.
                    l.intensity = 0.10f;
                    l.color = new Color(0.62f, 0.70f, 0.74f);
                    l.shadows = LightShadows.None;
                    log.AppendLine("  Directional Light 0.10 으로 낮춤");
                }
            }

            var cabin = FindByPath("ReferenceRoom/CeilingLamp/CabinLight");
            if (cabin != null)
            {
                var l = cabin.GetComponent<Light>();
                if (l != null)
                {
                    l.type = LightType.Point;
                    l.color = new Color(1.00f, 0.79f, 0.55f);
                    l.intensity = 3.6f;
                    l.range = 7.5f;
                    l.shadows = LightShadows.Soft;
                    l.shadowStrength = 0.80f;
                    log.AppendLine("  CabinLight: 따뜻한 점광 3.6 / range 7.5 / soft shadow");
                }
            }
            else log.AppendLine("  ⚠ CabinLight 못 찾음 — 조명이 램프 하나로 안 잡힌다");

            // 🔴 v1 실측 — `CabinLight`(y=2.724) 가 새 `ELV_CeilingLamp` 메시
            // (y 2.245~2.765) **안에** 들어가 있고 그림자가 켜져 있었다.
            // 등이 자기 하우징으로 자기 빛을 막았고, 그게 화면 40~51% 완전 검정의
            // 직접 원인이다. 광원을 옮기면 램프 위치와 어긋나므로
            // **하우징이 그림자를 던지지 않게** 한다 — 케이지 램프의 살은 어차피
            // 얇아서 실제로도 방을 가리지 않는다.
            var lampRoot = GameObject.Find(RootName);
            int noShadow = 0;
            if (lampRoot != null)
            {
                foreach (var r in lampRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.name.Contains("Lamp")) continue;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    noShadow++;
                }
            }
            log.AppendLine($"  램프 하우징 그림자 끔 {noShadow}개 (자기 차폐 제거)");

            // 실행 레버 국소광 — 「어두워서 레버를 못 찾으면 결함」(금지 11항).
            // 레버 자체를 밝히지 않고 **그 자리만** 약하게 든다. 스포트라 확산되지 않는다.
            var leverBase = FindByPath("ReferenceRoom/ExecutionLeverBase");
            if (leverBase != null)
            {
                var rigName = "ExecutionLeverKeyLight";
                var rig = leverBase.transform.Find(rigName);
                if (rig == null)
                {
                    var go = new GameObject(rigName);
                    go.transform.SetParent(leverBase.transform, false);
                    rig = go.transform;
                }
                rig.localPosition = new Vector3(0f, 1.55f, -0.85f);
                rig.localRotation = Quaternion.Euler(28f, 0f, 0f);
                var sl = rig.GetComponent<Light>() ?? rig.gameObject.AddComponent<Light>();
                sl.type = LightType.Spot;
                sl.color = new Color(0.98f, 0.86f, 0.70f);
                sl.intensity = 2.1f;
                sl.range = 3.0f;
                sl.spotAngle = 62f;
                sl.innerSpotAngle = 26f;
                sl.shadows = LightShadows.None;   // 그림자까지 켜면 다시 자기 차폐로 간다
                log.AppendLine("  실행 레버 전용 스포트 추가 (판독성 게이트)");
            }
            else log.AppendLine("  ⚠ ExecutionLeverBase 못 찾음 — 레버 판독성 미보장");
        }

        // ── 유틸 ──────────────────────────────────────────────────────────────
        private static GameObject FindByPath(string path)
        {
            var parts = path.Split('/');
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var cur = roots.FirstOrDefault(r => r.name == parts[0]);
            for (int i = 1; i < parts.Length && cur != null; i++)
            {
                var next = cur.transform.Find(parts[i]);
                cur = next != null ? next.gameObject : null;
            }
            return cur;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
