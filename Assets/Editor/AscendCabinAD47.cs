using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// 블렌더 AD47 캐빈(<c>ELV_Cabin_AD47.fbx</c>)의 머티리얼을 채택한다.
    ///
    /// ## 왜 따로 있나
    ///
    /// <see cref="AscendCabinAdoption"/> 은 옛 <c>ELV_Cabin.fbx</c> 의 머티리얼 6장을
    /// 다룬다. AD47 은 24장이고 이름 체계도 다르다(<c>M_Cab_*</c>, <c>M_Elev_*</c>,
    /// <c>M_Door_*</c>). 옛 표를 늘리면 두 FBX 가 같은 표를 공유하게 되어, 한쪽을
    /// 손볼 때마다 다른 쪽이 조용히 바뀐다. 표를 갈라 둔다.
    ///
    /// ## 값의 출처 — 추정이 아니라 굽기
    ///
    /// <c>Base</c> 는 **블렌더에서 Cycles 디퓨즈 베이크로 실측한 알베도**다
    /// (2026-08-08, 32×32, direct/indirect 끔 = 색만). 그래서 노후화 노드 그룹
    /// (<c>NG_Elev_Aged60_Room</c>)이 얹은 녹·때·모서리 마모가 이미 섞여 있다.
    /// 눈으로 고른 색이 아니다.
    ///
    /// 굽기가 검정을 돌려준 12장은 디퓨즈가 없는 것들이다 — 발광체(구슬·전구·
    /// 사이렌·게이지 눈금)와 유리. 그건 아래에서 손으로 준다. 그 둘을 섞어 적으면
    /// 「왜 이 값은 실측이고 저 값은 아닌가」를 나중에 아무도 구분하지 못한다.
    ///
    /// ## 이득(Gain)이 왜 하나가 아닌가
    ///
    /// 블렌더는 전구 하나 + AgX(노출 2.3)로 본다. 유니티는 GI 베이크가 없어
    /// 같은 알베도가 훨씬 어둡게 떨어진다. 캐빈 표면은 ×1.85 로 기존 셸의
    /// 채택 대역(<c>ELV_Iron</c> 최종 0.343)에 맞췄고, **기계 쪽은 원본이
    /// 0.02~0.04 라 같은 배수로는 순검정이 된다.** 그래서 기계에만 ×5 를 준다.
    /// 공동(<c>CavityDark</c>)과 바닥 구멍(<c>Cab_Void</c>)은 **어두운 것이 목적**이라
    /// 올리지 않는다 — 배수를 일괄 적용했다면 구멍이 사라졌을 것이다.
    ///
    /// ## 텍스처를 아껴 쓰는 이유
    ///
    /// AD47 은 판재·리벳·이음매가 **실제 형상**이다. 여기에 리벳 텍스처를 더 붙이면
    /// 2026-08-07 에 사용자가 천장에서 지적한 「모델링 격자와 텍스처 격자가 겹쳐
    /// 어색하다」가 벽에서 재현된다. 그래서 구조가 있는 텍스처는 쓰지 않고
    /// 결·녹 정도만 얹는다.
    /// </summary>
    internal static class AscendCabinAD47
    {
        private const string FbxPath = "Assets/Prototype_Elevator/Art/Models/ELV_Cabin_AD47.fbx";
        private const string MatDir = "Assets/Prototype_Elevator/Materials/CabinAD47";
        private const string TexDir = "Assets/Prototype_Elevator/Art/Textures";

        /// <summary>불투명 표면 템플릿. 파라미터·셰이더 키워드를 통째로 물려받는다.</summary>
        private const string OpaqueTemplate = "Assets/Prototype_Elevator/Materials/Cabin/ELV_Iron.mat";

        /// <summary>발광체 템플릿. 배출 키워드가 이미 켜져 있다.</summary>
        private const string EmissiveTemplate = "Assets/Prototype_Elevator/Materials/Cabin/ELV_LampGlass.mat";

        private struct Def
        {
            public string Name;
            public Color Base;      // 블렌더 실측 알베도 (선형) 또는 손으로 준 값
            public float Gain;
            public string Tex;      // null = 텍스처 없음
            public float Rim;
            public Color Emission;  // default(Color) = 발광 없음
            public bool Measured;   // false = 굽기가 검정을 돌려줘 손으로 준 값
        }

        private const float CabGain = 1.85f;   // 캐빈 표면 — 기존 채택 대역에 맞춘다
        private const float MachGain = 5.0f;   // 기계 — 원본이 0.02~0.04 라 그대로면 순검정
        private const float Keep = 1.0f;       // 어두운 것이 목적인 곳

        private static readonly Def[] Defs =
        {
            // ── 캐빈 표면 (실측) ───────────────────────────────────────────────
            new Def { Name="M_Cab_Wall",      Base=new Color(0.2310f,0.2230f,0.2080f), Gain=CabGain,  Tex="TEX_Iron_Rust",       Rim=0.24f, Measured=true },
            new Def { Name="M_Cab_Ceil",      Base=new Color(0.1637f,0.1572f,0.1498f), Gain=CabGain,  Tex="TEX_Iron_Rust",       Rim=0.20f, Measured=true },
            // 바닥만 배수가 1 인 이유 — 바닥은 천장 램프를 **정면으로** 받는 유일한 큰 면이라
            // 같은 알베도라도 벽보다 훨씬 밝게 떨어진다. ×1.85 를 주면 벽 대비 휘도비가
            // 2.00 이 되어 「방이 두 값 덩어리로 갈라진다」(2026-08-08 실측).
            // 1.0 에서 비가 1.29 로 붙는다 — 램프를 마주 보는 면이니 1.00 이 아니라
            // 조금 밝은 것이 맞다. 이 값은 눈이 아니라 화면 영역 평균으로 재서 골랐다.
            new Def { Name="M_Cab_Floor",     Base=new Color(0.1764f,0.1591f,0.1478f), Gain=1.0f,     Tex="TEX_FloorPlate_Rust", Rim=0.16f, Measured=true },
            new Def { Name="M_Cab_Trim",      Base=new Color(0.1460f,0.1381f,0.1320f), Gain=CabGain,  Tex="TEX_Iron_Rust",       Rim=0.30f, Measured=true },
            new Def { Name="M_Cab_Iron",      Base=new Color(0.1861f,0.1814f,0.1702f), Gain=CabGain,  Tex="TEX_Iron_Rust",       Rim=0.22f, Measured=true },
            new Def { Name="M_Cab_BoltSteel", Base=new Color(0.1480f,0.1480f,0.1147f), Gain=CabGain,  Tex="TEX_Iron_Rust",       Rim=0.34f, Measured=true },
            new Def { Name="M_Door_Panel",    Base=new Color(0.2057f,0.1940f,0.1832f), Gain=CabGain,  Tex="TEX_Iron_Rust",       Rim=0.26f, Measured=true },
            new Def { Name="M_Door_Trim",     Base=new Color(0.1606f,0.1472f,0.1362f), Gain=CabGain,  Tex="TEX_Iron_Rust",       Rim=0.30f, Measured=true },

            // ── 어두운 것이 목적인 곳 — 배수를 걸지 않는다 ─────────────────────
            new Def { Name="M_Cab_Void",        Base=new Color(0.0510f,0.0510f,0.0510f), Gain=Keep, Tex=null, Rim=0.05f, Measured=true },
            new Def { Name="M_Elev_CavityDark", Base=new Color(0.0149f,0.0143f,0.0116f), Gain=3.0f, Tex=null, Rim=0.04f, Measured=true },

            // ── 기계 (실측이지만 배수가 크다) ──────────────────────────────────
            new Def { Name="M_Elev_FrameSteel",          Base=new Color(0.0318f,0.0306f,0.0265f), Gain=MachGain, Tex="TEX_Machine_Housing", Rim=0.26f, Measured=true },
            new Def { Name="M_Elev_FrameSteel_Chamber",  Base=new Color(0.0318f,0.0306f,0.0265f), Gain=MachGain, Tex="TEX_Machine_Housing", Rim=0.26f, Measured=true },
            new Def { Name="M_Elev_ChamberBezel",         Base=new Color(0.0392f,0.0383f,0.0370f), Gain=MachGain, Tex="TEX_Machine_Housing", Rim=0.30f, Measured=true },
            new Def { Name="M_Elev_ChamberBezel_Chamber", Base=new Color(0.0368f,0.0359f,0.0318f), Gain=MachGain, Tex="TEX_Machine_Housing", Rim=0.30f, Measured=true },
            new Def { Name="M_Elev_BoltSteel",           Base=new Color(0.0613f,0.0613f,0.0591f), Gain=4.5f,     Tex="TEX_Iron_Rust",       Rim=0.34f, Measured=true },
            new Def { Name="M_Elev_BoltSteel_Chamber",   Base=new Color(0.0613f,0.0613f,0.0591f), Gain=4.5f,     Tex="TEX_Iron_Rust",       Rim=0.34f, Measured=true },
            new Def { Name="M_Elev_DarkIron",            Base=new Color(0.0195f,0.0190f,0.0180f), Gain=MachGain, Tex="TEX_Iron_Rust",       Rim=0.18f, Measured=true },
            new Def { Name="M_Elev_LeverIron",           Base=new Color(0.0199f,0.0199f,0.0199f), Gain=MachGain, Tex="TEX_Iron_Rust",       Rim=0.28f, Measured=true },

            // ── 굽기가 검정을 돌려준 것들 — 손으로 준다 ────────────────────────
            // 유리. 스타일라이즈드는 불투명이라 투명 대신 「연기 낀 어두운 판」으로 둔다.
            new Def { Name="M_Elev_ChamberGlass", Base=new Color(0.0900f,0.0950f,0.0980f), Gain=1f, Tex="TEX_Glass_Smudged", Rim=0.55f, Measured=false },
            new Def { Name="M_Elev_LeverPlate",   Base=new Color(0.0980f,0.0950f,0.0900f), Gain=1f, Tex="TEX_Iron_Rust",     Rim=0.24f, Measured=false },
            new Def { Name="M_Elev_GaugeTrack",   Base=new Color(0.0600f,0.0580f,0.0540f), Gain=1f, Tex=null,                Rim=0.16f, Measured=false },
            new Def { Name="M_Elev_TickPaint",    Base=new Color(0.5200f,0.5000f,0.4500f), Gain=1f, Tex=null,                Rim=0.20f, Measured=false },
            new Def { Name="M_Elev_GaugeFace",    Base=new Color(0.2600f,0.2450f,0.2200f), Gain=1f, Tex="TEX_Gauge_Enamel",  Rim=0.20f, Measured=false },

            // 발광체. 배수를 걸지 않는다 — 여기를 올리면 방을 통째로 태운다.
            new Def { Name="M_Cab_Bulb",          Base=new Color(0.9804f,0.9529f,0.8784f), Gain=1f, Tex=null, Rim=0.10f, Measured=true,
                      Emission=new Color(2.30f, 1.62f, 0.86f) },
            new Def { Name="M_Elev_LampEmissive", Base=new Color(0.8600f,0.7400f,0.5200f), Gain=1f, Tex=null, Rim=0.10f, Measured=false,
                      Emission=new Color(1.80f, 1.28f, 0.70f) },
            new Def { Name="M_Elev_SirenLens",    Base=new Color(0.6200f,0.1400f,0.0900f), Gain=1f, Tex=null, Rim=0.30f, Measured=false,
                      Emission=new Color(2.10f, 0.34f, 0.16f) },
            new Def { Name="M_Elev_GaugeFill",    Base=new Color(0.7000f,0.4600f,0.2000f), Gain=1f, Tex=null, Rim=0.14f, Measured=false,
                      Emission=new Color(1.40f, 0.82f, 0.34f) },
            // 구슬은 아직 디자인 미결정이다 (2026-08-08 사용자: 그레이박스로).
            // 여기서는 「형태를 읽을 수 있는 호박색 발광」까지만 준다.
            new Def { Name="M_Elev_SoulCore",     Base=new Color(0.5400f,0.3800f,0.2200f), Gain=1f, Tex=null, Rim=0.40f, Measured=false,
                      Emission=new Color(1.55f, 0.92f, 0.44f) },
            new Def { Name="M_Elev_SoulHalo",     Base=new Color(0.3000f,0.2200f,0.1400f), Gain=1f, Tex=null, Rim=0.50f, Measured=false,
                      Emission=new Color(0.62f, 0.38f, 0.18f) },
        };

        [MenuItem("Ascend/Cabin/2. AD47 머티리얼 채택")]
        public static void Adopt()
        {
            var log = new StringBuilder("[상승] AD47 머티리얼 채택\n");
            EnsureFolder(MatDir);

            var opaque = AssetDatabase.LoadAssetAtPath<Material>(OpaqueTemplate);
            var emissive = AssetDatabase.LoadAssetAtPath<Material>(EmissiveTemplate);
            if (opaque == null)
            {
                Debug.LogError("[상승] 템플릿을 못 찾았다: " + OpaqueTemplate
                             + " — 이 스크립트는 파라미터를 템플릿에서 물려받는다. 값만 새로 쓰면 셰이더 키워드가 빠진다.");
                return;
            }

            var built = new Dictionary<string, Material>();
            foreach (var d in Defs)
            {
                bool glows = d.Emission.maxColorComponent > 0f;
                var template = glows && emissive != null ? emissive : opaque;

                string path = MatDir + "/" + d.Name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(template) { name = d.Name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                else
                {
                    // 템플릿이 바뀌었을 수 있다. 파라미터를 다시 물려받고 값만 덮는다.
                    mat.shader = template.shader;
                    mat.CopyPropertiesFromMaterial(template);
                    mat.name = d.Name;
                }

                var albedo = Scaled(d.Base, d.Gain);
                mat.SetColor("_BaseColor", albedo);
                if (mat.HasProperty("_RimStrength")) mat.SetFloat("_RimStrength", d.Rim);

                var tex = d.Tex == null ? null : LoadTexture(d.Tex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);

                if (glows)
                {
                    mat.SetColor("_EmissionColor", d.Emission);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }

                EditorUtility.SetDirty(mat);
                built[d.Name] = mat;

                log.AppendLine(string.Format("    {0,-30} {1} albedo=({2:F3},{3:F3},{4:F3}) tex={5}{6}",
                    d.Name, d.Measured ? "실측" : "수동", albedo.r, albedo.g, albedo.b,
                    d.Tex ?? "-", glows ? "  발광" : ""));
            }

            AssetDatabase.SaveAssets();

            // ── 임포터 리맵 ───────────────────────────────────────────────────
            // 이걸 하지 않으면 FBX 를 다시 구울 때마다 URP/Lit 기본값으로 되돌아간다.
            var imp = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (imp == null)
            {
                Debug.LogError("[상승] FBX 임포터를 못 찾았다: " + FbxPath);
                return;
            }
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in built)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            imp.SaveAndReimport();

            // ── 검증 — 씬 인스턴스가 실제로 우리 머티리얼을 물었는가 ───────────
            int wrong = 0, ok = 0;
            var missing = new HashSet<string>();
            var cab = GameObject.Find("CabinAD47");
            if (cab != null)
            {
                foreach (var r in cab.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) { wrong++; continue; }
                        if (built.ContainsKey(m.name)) ok++;
                        else { wrong++; missing.Add(m.name); }
                    }
            }
            log.AppendLine(string.Format("\n  씬 렌더러 슬롯: 채택 {0} · 미채택 {1}", ok, wrong));
            if (missing.Count > 0)
                log.AppendLine("  표에 없는 머티리얼: " + string.Join(", ", missing.OrderBy(s => s)));

            Debug.Log(log.ToString());
        }

        /// <summary>알파를 건드리지 않고 RGB 만 배수한다. <c>Color * float</c> 는 알파까지 곱한다.</summary>
        private static Color Scaled(Color c, float g)
            => new Color(Mathf.Clamp01(c.r * g), Mathf.Clamp01(c.g * g), Mathf.Clamp01(c.b * g), c.a);

        private static Texture2D LoadTexture(string name)
        {
            foreach (var p in new[] { TexDir + "/" + name + ".png", TexDir + "/Generated/" + name + ".png" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null) return t;
            }
            Debug.LogWarning("[상승] 텍스처를 못 찾았다: " + name);
            return null;
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
