using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// 블렌더에서 구운 알베도를 AD47 캐빈에 입힌다.
    ///
    /// ## 왜 타일 텍스처로는 안 되나
    ///
    /// 첫 채택은 머티리얼마다 `TEX_Iron_Rust` 같은 128px 타일 하나를 붙였다.
    /// 블렌더의 인상은 **다중 스케일 절차적 노후화**(녹·때·모서리 마모·얼룩이
    /// 월드 좌표 박스 투영으로 겹친 것)에서 나오고, 그건 타일 한 장으로 재현되지
    /// 않는다. 게다가 AD47 의 UV0 은 **월드 미터**(x −2.00..2.10)여서 128px 이
    /// 4m 벽에 32px/m 로 늘어났다 — 반복이 눈에 보인다.
    ///
    /// 그래서 블렌더에서 **Cycles 디퓨즈 굽기**(direct/indirect 끔 = 색만)로
    /// 오브젝트마다 실제 알베도를 굽고 그걸 그대로 쓴다. 굽기용 UV(`UVBake`)는
    /// Smart UV Project 로 새로 만들어 0..1 에 채웠고, FBX 로 내보낼 때 **첫 UV
    /// 레이어에 복사**해 유니티의 UV0 이 되게 했다 — `Ascend/Stylized` 는
    /// TEXCOORD0 만 읽기 때문이다.
    ///
    /// 해상도는 크기별로 1024 / 512 / 256 이다. 4m 벽에 1024 면 256px/m 로,
    /// 이 프로젝트의 PS1~초기 PS2 방향과 맞는 밀도다.
    ///
    /// ## 발광체와 유리는 굽지 않는다
    ///
    /// 디퓨즈 굽기는 발광과 투과를 담지 못한다(그래서 12장이 검정으로 나왔다).
    /// 구슬·전구·사이렌·게이지 눈금·챔버 유리를 쓰는 오브젝트는 **손으로 준
    /// 머티리얼을 그대로 둔다.** 그 목록을 코드에 박아 두는 이유는, 나중에
    /// 「왜 이것만 안 구웠나」를 묻게 되기 때문이다.
    /// </summary>
    internal static class AscendAD47Baked
    {
        private const string BakeDir = "Assets/Prototype_Elevator/Art/Textures/BakedAD47";
        private const string MatDir = "Assets/Prototype_Elevator/Materials/CabinAD47/Baked";
        private const string Template = "Assets/Prototype_Elevator/Materials/Cabin/ELV_Iron.mat";

        /// <summary>굽기가 담지 못하는 머티리얼. 이걸 쓰는 오브젝트는 건너뛴다.</summary>
        private static readonly HashSet<string> NotBakeable = new HashSet<string>
        {
            "M_Cab_Bulb", "M_Elev_LampEmissive", "M_Elev_SirenLens",
            "M_Elev_GaugeFill", "M_Elev_SoulCore", "M_Elev_SoulHalo",
            "M_Elev_ChamberGlass",
        };

        // ── 이득 세 갈래 ───────────────────────────────────────────────────
        // 구운 값은 블렌더 기준(전구 하나 + AgX 2.3)이고 유니티는 그 톤매핑이 없다.
        // 하나로 맞출 수 없어 세 갈래로 나눴다. 전부 화면 영역 평균으로 재서 골랐다.

        /// <summary>캐빈 표면. ×1.75 는 평균휘도 0.53 · 날림 3.3% 로 방이 하얗게 떴다.
        /// 0.50 에서 0.15 — 굽기 이전 대역(0.13)에 붙으면서 질감은 살아 있다.</summary>
        private const float CabGain = 0.50f;

        /// <summary>기계 내부. 원본 알베도가 0.02~0.04 라 캐빈과 같은 이득이면 순검정 슬래브가 된다.</summary>
        private const float MachineGain = 2.40f;

        /// <summary>기계 테두리. 캐빈 표면 재질(밝은 볼트강)로 구워져서 기계 이득을 같이
        /// 받으면 **방에서 가장 밝은 면**이 된다. 벽보다 살짝 밝은 정도로 둔다.</summary>
        private const float MachineFrameGain = 0.62f;

        [MenuItem("Ascend/Cabin/7. AD47 굽기 텍스처 적용")]
        public static void Apply()
        {
            var log = new StringBuilder("[상승] AD47 굽기 텍스처 적용\n");
            var cab = GameObject.Find("CabinAD47");
            if (cab == null) { Debug.LogError("[상승] CabinAD47 없음"); return; }

            var template = AssetDatabase.LoadAssetAtPath<Material>(Template);
            if (template == null) { Debug.LogError("[상승] 템플릿 없음: " + Template); return; }
            EnsureFolder(MatDir);

            // 기계에 속한 렌더러를 미리 가려낸다 — 이득 대역이 다르다
            var socket = cab.transform.Find("SOCKET_ElevPanel");
            var inMachine = new HashSet<Renderer>();
            if (socket != null)
                foreach (var r in socket.GetComponentsInChildren<Renderer>(true)) inMachine.Add(r);

            int applied = 0, skippedEmissive = 0, missingTex = 0;
            var skipNames = new List<string>();

            foreach (var r in cab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats.Any(m => m != null && NotBakeable.Contains(StripInstance(m.name))))
                {
                    skippedEmissive++;
                    if (skipNames.Count < 12) skipNames.Add(r.name);
                    continue;
                }

                string texPath = BakeDir + "/BK_" + r.name.Replace('.', '_') + ".png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null) { missingTex++; continue; }

                string matPath = MatDir + "/BK_" + r.name.Replace('.', '_') + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(template) { name = "BK_" + r.name.Replace('.', '_') };
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else
                {
                    mat.shader = template.shader;
                    mat.CopyPropertiesFromMaterial(template);
                }

                // 구운 맵이 색을 전부 들고 있다. `_BaseColor` 는 이득만 담당한다.
                float gain = r.name == "SM_Cab_MachineFrame" ? MachineFrameGain
                           : (inMachine.Contains(r) ? MachineGain : CabGain);
                mat.SetColor("_BaseColor", new Color(gain, gain, gain, 1f));
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                EditorUtility.SetDirty(mat);

                var newMats = new Material[mats.Length];
                for (int i = 0; i < newMats.Length; i++) newMats[i] = mat;
                r.sharedMaterials = newMats;
                EditorUtility.SetDirty(r);
                applied++;
            }

            AssetDatabase.SaveAssets();
            log.AppendLine(string.Format("  적용 {0} · 발광/유리라 건너뜀 {1} · 텍스처 없음 {2}",
                applied, skippedEmissive, missingTex));
            if (skipNames.Count > 0) log.AppendLine("  건너뛴 예: " + string.Join(", ", skipNames));
            log.AppendLine(string.Format("  이득 — 캐빈 ×{0:F2} · 기계 ×{1:F2} · 기계 테두리 ×{2:F2}",
                CabGain, MachineGain, MachineFrameGain));
            Debug.Log(log.ToString());
        }

        /// <summary>런타임 인스턴스 이름의 " (Instance)" 꼬리를 뗀다.</summary>
        private static string StripInstance(string n)
        {
            int i = n.IndexOf(" (Instance)");
            return i >= 0 ? n.Substring(0, i) : n;
        }

        /// <summary>구운 PNG 의 임포트 설정. 굽기 결과는 색 데이터이므로 sRGB 다.</summary>
        [MenuItem("Ascend/Cabin/7b. 굽기 텍스처 임포트 설정")]
        public static void ConfigureImporters()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { BakeDir });
            int n = 0;
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Clamp;      // 고유 UV 라 반복하지 않는다
                imp.filterMode = FilterMode.Bilinear;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
                n++;
            }
            Debug.Log("[상승] 굽기 텍스처 임포트 설정 " + n + "장");
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
