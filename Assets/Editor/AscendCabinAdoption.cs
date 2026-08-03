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
            imp.materialLocation = ModelImporterMaterialLocation.External;

            imp.SaveAndReimport();
            imp.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                                        ModelImporterMaterialSearch.Everywhere);
            imp.SaveAndReimport();

            var remapped = imp.GetExternalObjectMap()
                              .Where(kv => kv.Value is Material)
                              .Select(kv => kv.Key.name + "→" + kv.Value.name).ToList();
            log.AppendLine($"  임포터 설정 완료. 재질 리맵 {remapped.Count}건: {string.Join(", ", remapped)}");
        }

        // ── 씬 배치 ───────────────────────────────────────────────────────────
        private static void PlaceIntoScene(StringBuilder log)
        {
            var old = GameObject.Find(RootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = RootName;
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            inst.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(inst, "Adopt cabin shell");

            // 셸은 정적이다 — 배칭과 조명 계산에 들어가야 한다.
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);

            var missing = new List<string>();
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var names = r.sharedMaterials.Select(m => m == null ? "(null)" : m.name).ToArray();
                if (names.Any(n => n == "(null)" || n.StartsWith("Default")))
                    missing.Add(r.name + ": " + string.Join("|", names));
            }
            log.AppendLine($"  배치: {RootName} 자식 {inst.transform.childCount}개");
            if (missing.Count > 0)
                log.AppendLine("  ⚠ 재질 미할당: " + string.Join(" / ", missing));
            else
                log.AppendLine("  재질 전부 할당됨");

            var tris = inst.GetComponentsInChildren<MeshFilter>(true)
                           .Where(f => f.sharedMesh != null)
                           .Sum(f => f.sharedMesh.triangles.Length / 3);
            log.AppendLine($"  삼각형 합계 {tris}");
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
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.048f, 0.055f, 0.062f);
            RenderSettings.ambientEquatorColor = new Color(0.034f, 0.038f, 0.042f);
            RenderSettings.ambientGroundColor = new Color(0.020f, 0.021f, 0.023f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.030f, 0.034f, 0.038f);
            RenderSettings.fogDensity = 0.055f;

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
                    l.shadowStrength = 0.92f;
                    log.AppendLine("  CabinLight: 따뜻한 점광 3.6 / range 7.5 / soft shadow");
                }
            }
            else log.AppendLine("  ⚠ CabinLight 못 찾음 — 조명이 램프 하나로 안 잡힌다");
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
