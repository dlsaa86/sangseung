using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 씬 내장 `CarShell_*` 머티리얼에 생성 텍스처를 배선하는 **멱등** 조립기.
    ///
    /// 왜 이것이 필요한가: `GRAPHICS_TARGET` G-1 의 국소 분산 중앙값이 **0.00** 이다.
    /// 8×8 블록의 절반 이상이 표준편차 0 — 완전한 무지 면이라는 뜻이고,
    /// 실측상 씬의 31개 머티리얼 중 `_BaseMap` 이 걸린 것이 **0개**였다.
    /// 「평가받고 있는 대상이 아직 그레이박스다」(`GRAPHICS_TARGET` §0)의 실체가 이것이다.
    ///
    /// ── 이 파일이 지키는 세 가지 ─────────────────────────────────────────
    ///
    /// **1. `_BaseColor` 를 먼저 올린다.** `Ascend/Stylized` 는 `_BaseMap` 을
    ///    `_BaseColor` 에 **곱한다.** 현재 13장의 명도는 0.086~0.318 이고 새 알베도의
    ///    평균 밝기는 0.47~0.60 이라, 그대로 물리면 반사율이 0.24 → **0.12** 로 떨어져
    ///    **텍스처판이 그레이박스보다 어두워진다.** 이 저장소가 셰이더·머티리얼 교체를
    ///    두 번 되돌린 방식이 정확히 이것이다(6차 판정 「순손실」).
    ///    색상(H·S)은 유지하고 **명도(V)만** 올린다 — 색조는 텍스처가 나른다.
    ///
    /// **2. 타일링은 「월드 미터당 반복 수」다.** 고정 수치를 쓰지 않는다.
    ///    `_BaseMap_ST` 는 렌더러의 **실측 월드 크기**에 배율을 곱해서 계산한다.
    ///    고정 타일링은 캐빈이 확대되면 「늘어난 벽」이 되어 국소 분산을 다시 0 으로
    ///    되돌린다. 여기서는 확대 후 이 메뉴를 다시 돌리기만 하면 텍셀 밀도가 유지된다.
    ///
    /// **3. 실측을 보고한다.** 「배선했다」는 머티리얼을 세는 것이고 「화면에 보인다」는
    ///    화소를 세는 것이다. 이 저장소는 그 둘을 혼동해 두 번 실패했다
    ///    (`GRAPHICS_TARGET` G-1). 이 메뉴는 머티리얼 수만 보고하고, 화소는
    ///    `tools/capture-metrics.ps1` 이 판정한다 — **주장하지 않는다.**
    /// </summary>
    public static class AscendSurfaceWiring
    {
        public const string TexDir = "Assets/Prototype_Elevator/Art/Textures/Generated";

        /// <summary>배선 규칙 한 줄. 배율은 「월드 1m 당 텍스처 반복 수」다.</summary>
        private readonly struct Rule
        {
            public readonly string Material;   // 씬 내장 머티리얼 이름
            public readonly string Texture;    // TexDir 안의 파일 이름 (확장자 없이)
            public readonly float  PerMetre;   // 반복/m
            public readonly float  Value;      // `_BaseColor` 목표 명도 (HSV 의 V)
            public readonly string Emission;   // 발광 마스크. 없으면 null

            public Rule(string mat, string tex, float perMetre, float value, string emis = null)
            { Material = mat; Texture = tex; PerMetre = perMetre; Value = value; Emission = emis; }
        }

        // 텍스처 레인의 제안표를 그대로 옮긴 것이다. **배율만 옮겼고 타일링 수치는
        // 옮기지 않았다** — 제안표의 「6.4m 벽 환산」 열은 확대 후 치수를 가정한
        // 값이고, 이 씬의 실측 벽은 그것과 다르다(폭 2.40 · 깊이 3.00 · 높이 3.20).
        // 미터당 반복 수만이 치수와 무관한 불변량이다.
        private static readonly Rule[] Rules =
        {
            new Rule("CarShell_Floor",            "TEX_FloorPlate_Rust",   0.75f, 0.90f),
            new Rule("CarShell_Ceiling",          "TEX_WallPanel_Riveted", 0.50f, 0.88f),
            new Rule("CarShell_WallL",            "TEX_WallPanel_Riveted", 0.50f, 0.92f),
            new Rule("CarShell_WallR",            "TEX_WallPanel_Riveted", 0.50f, 0.92f),
            new Rule("CarShell_BackWall_Left",    "TEX_WallPanel_Riveted", 0.50f, 0.92f),
            new Rule("CarShell_BackWall_Right",   "TEX_WallPanel_Riveted", 0.50f, 0.92f),
            new Rule("CarShell_FrontWall",        "TEX_WallPaint_Peeled",  0.50f, 0.92f),
            new Rule("CarShell_BackWall_Lintel",  "TEX_Stencil_Warning",   0.50f, 0.95f),
            new Rule("CarShell_Handrail_R",       "TEX_Conduit_Cable",     2.00f, 0.90f),
            new Rule("CarShell_Handrail_B",       "TEX_Conduit_Cable",     2.00f, 0.90f),
            new Rule("CarShell_TankStand",        "TEX_Machine_Housing",   1.50f, 0.90f, "TEX_Machine_Housing_Emis"),

            // 로비는 **문 밖**이다. 캐빈과 같은 명도로 올리면 출입구의 깊이가 사라지고
            // 「안이 어둡고 밖이 밝다」는 공간 판독이 뒤집힌다. 텍스처는 물리되
            // 명도는 낮게 유지한다 — 이 둘은 다른 목적이다.
            new Rule("CarShell_LobbyFloor",       "TEX_Grating_Steel",     1.00f, 0.45f),
            new Rule("CarShell_LobbyBack",        "TEX_Concrete_Shaft",    0.40f, 0.38f),
        };

        [MenuItem("Ascend/Graphics/Wire Surface Textures")]
        public static void WireTextures()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            Scene scene = AscendGraphicsBuilder.EnsureScene();
            if (!scene.IsValid()) return;

            var report = new StringBuilder("[상승] 표면 텍스처 배선\n");

            // 1) 임포트 설정. **Repeat 이 아니면 타일링이 잘려 무지 면이 된다.**
            int fixedWrap = EnsureRepeatWrap(report);

            // 2) 머티리얼 이름 → 그 머티리얼을 쓰는 렌더러들의 실측 월드 크기
            var sizes = MeasureMaterialSizes();

            int wired = 0, missingMat = 0, missingTex = 0;
            foreach (Rule rule in Rules)
            {
                Material mat = FindSceneMaterial(rule.Material);
                if (mat == null) { report.AppendLine($"  ⚠ 머티리얼 없음 — {rule.Material}"); missingMat++; continue; }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{rule.Texture}.png");
                if (tex == null) { report.AppendLine($"  ⚠ 텍스처 없음 — {rule.Texture}"); missingTex++; continue; }

                Undo.RecordObject(mat, "Wire surface texture");

                // ── 명도 먼저 (경고 1) ──
                Color before = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                Color.RGBToHSV(before, out float h, out float s, out float _);
                Color lifted = Color.HSVToRGB(h, s, rule.Value);
                lifted.a = before.a;
                mat.SetColor("_BaseColor", lifted);

                // ── 타일링은 실측 크기 × 미터당 반복 수 (경고 2) ──
                Vector2 uv = TilingFor(sizes, rule.Material, rule.PerMetre);
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", uv);
                mat.SetTextureOffset("_BaseMap", Vector2.zero);

                // ── 발광 마스크 ──
                // ⚠ 기본값이 `"white"` 인 것이 안전 장치다. 검정을 곱하면
                // `MaterialPropertyBlock` 이 넣는 `_EmissionColor` 가 조용히 죽는다.
                if (mat.HasProperty("_EmissionMapEnabled"))
                {
                    if (rule.Emission != null)
                    {
                        var em = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{rule.Emission}.png");
                        if (em != null)
                        {
                            mat.SetTexture("_EmissionMap", em);
                            mat.SetTextureScale("_EmissionMap", uv);
                            mat.SetFloat("_EmissionMapEnabled", 1f);
                            mat.EnableKeyword("_EMISSIONMAP_ON");
                        }
                    }
                    else
                    {
                        mat.SetFloat("_EmissionMapEnabled", 0f);
                        mat.DisableKeyword("_EMISSIONMAP_ON");
                    }
                }

                EditorUtility.SetDirty(mat);
                wired++;
                sizes.TryGetValue(rule.Material, out Vector3 sz);
                report.AppendLine($"  {rule.Material,-24} ← {rule.Texture,-24} " +
                                  $"실측 {sz.x:F2}×{sz.y:F2}×{sz.z:F2} m · {rule.PerMetre:F2}회/m → tiling ({uv.x:F2}, {uv.y:F2}) · " +
                                  $"_BaseColor V {ValueOf(before):F3} → {rule.Value:F2}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            report.AppendLine($"  배선 {wired}장 / 규칙 {Rules.Length}개 · 머티리얼 없음 {missingMat} · 텍스처 없음 {missingTex} · " +
                              $"Repeat 로 고친 텍스처 {fixedWrap}장");
            report.AppendLine("  ⚠ 「배선했다」는 머티리얼 수다. **화면에 보이는가는 화소가 답한다** — " +
                              "`tools/capture-metrics.ps1` 의 G-1 국소 분산으로 판정할 것.");
            Debug.Log(report.ToString());
        }

        private static float ValueOf(Color c) { Color.RGBToHSV(c, out _, out _, out float v); return v; }

        /// <summary>
        /// 생성 텍스처 전부를 `Repeat` 으로 맞춘다. `Clamp` 면 타일링 &gt; 1 에서
        /// 가장자리 화소가 늘어나 **면 대부분이 단색으로 채워진다** — 배선했는데도
        /// 국소 분산이 안 오르는 전형적인 원인이다.
        /// </summary>
        private static int EnsureRepeatWrap(StringBuilder report)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                if (imp.wrapMode == TextureWrapMode.Repeat) continue;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
                changed++;
                report?.AppendLine($"  임포트 수정 — {System.IO.Path.GetFileName(path)} wrapMode → Repeat");
            }
            return changed;
        }

        /// <summary>머티리얼 이름 → 그것을 쓰는 렌더러들의 월드 바운드 크기(가장 큰 것).</summary>
        private static Dictionary<string, Vector3> MeasureMaterialSizes()
        {
            var map = new Dictionary<string, Vector3>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Vector3 size = r.bounds.size;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (!map.TryGetValue(m.name, out Vector3 cur) || size.sqrMagnitude > cur.sqrMagnitude)
                        map[m.name] = size;
                }
            }
            return map;
        }

        /// <summary>
        /// 실측 크기에서 UV 타일링을 만든다.
        ///
        /// 가장 얇은 축을 두께로 보고 나머지 둘을 면으로 쓴다. 바닥·천장이면 (x, z),
        /// 벽이면 (수평, 높이)다. Unity 기본 Cube 는 각 면이 UV 0..1 이므로
        /// **면의 미터 크기 × 미터당 반복 수**가 곧 타일링이다.
        /// </summary>
        private static Vector2 TilingFor(Dictionary<string, Vector3> sizes, string material, float perMetre)
        {
            if (!sizes.TryGetValue(material, out Vector3 s) || s == Vector3.zero)
                return Vector2.one;   // 못 재면 1×1 — 조용히 틀린 수를 쓰지 않는다

            float u, v;
            if (s.y <= s.x && s.y <= s.z) { u = s.x; v = s.z; }        // 바닥·천장
            else if (s.x <= s.y && s.x <= s.z) { u = s.z; v = s.y; }   // 좌우 벽
            else { u = s.x; v = s.y; }                                  // 앞뒤 벽

            return new Vector2(
                Mathf.Max(0.01f, u * perMetre),
                Mathf.Max(0.01f, v * perMetre));
        }

        /// <summary>씬 내장 머티리얼은 에셋 경로가 없다. 렌더러를 훑어서 찾는다.</summary>
        private static Material FindSceneMaterial(string name)
        {
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.name == name) return m;
            return null;
        }
    }
}
