using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 레퍼런스 룸의 재질 7종(명세 §10)에 **생성 텍스처를 물린다.**
    ///
    /// ## 왜 별도 파일인가
    ///
    /// 형상(`AscendReferenceRoom`) · 배선(`AscendReferenceRoomRewire`) · 표면(이 파일)의
    /// **실패 방식이 다르다.** 형상이 틀리면 방이 이상하게 생기고, 배선이 틀리면 게임이
    /// 조용히 죽고, 표면이 틀리면 「무지 면」으로 남는다. 한 파일에 섞으면 캡처가
    /// 나빠졌을 때 어느 축이 원인인지 못 가른다 — 이 저장소가 「귀속 실패」로
    /// 반복해서 당한 것이 그것이다(`GRAPHICS_TARGET` §5.5).
    ///
    /// ## 타일링을 여기서 정하지 않는다
    ///
    /// 이 저장소는 타일링 모델로 **세 번** 실패했다(같은 문서 §6.3). 마지막 진단은
    /// 「하나의 `_BaseMap_ST` 로는 크기가 제각각인 렌더러 집단을 최적에 놓을 수 없다」였다.
    ///
    /// **그 문제가 여기엔 없다.** `ProcMeshBuilder` 가 UV 를 **월드 미터 기준**으로
    /// 굽기 때문이다(`PlanarUV(p, n, uvPerMeter)`). 즉 크기가 다른 벽·프레임·상자가
    /// 전부 같은 밀도를 갖고, `_BaseMap_ST` 는 (1,1,0,0) 이면 된다. 조정 손잡이가
    /// 아예 없는 것이 이 구조의 요점이다 — 없는 손잡이는 잘못 돌릴 수 없다.
    ///
    /// 밀도는 <see cref="Ascend.Prototype.Art.ReferenceRoomSpec.SurfaceUvPerMeter"/> 하나가
    /// 정하고, 그 값이 §14 의 128~256 px/m 안인지는 헤드리스 테스트가 검사한다.
    /// </summary>
    public static class AscendReferenceRoomTextures
    {
        private const string TexDir = "Assets/Prototype_Elevator/Art/Textures/Generated";
        private const string MatDir = "Assets/Prototype_Elevator/Art/Materials/Room";

        /// <summary>
        /// 재질 → 텍스처. 명세 §10 의 7종을 `AscendSurfaceSynth` 가 이미 만들어 둔
        /// 14장에 대응시킨다. **새 텍스처를 만들지 않는다** — 있는 것을 쓴다.
        /// </summary>
        private static readonly (string mat, string baseMap, string emisMap)[] Map =
        {
            // ① 검게 도장된 오래된 철판 — 벽·천장·프레임
            ("Steel",     "TEX_WallPanel_Riveted.png", null),
            // ② 도장이 벗겨진 노출 강철 — 보강 레일·볼트·링
            ("BareSteel", "TEX_WallPaint_Peeled.png",  null),
            // ③ 어두운 갈색·주황이 섞인 녹
            ("Rust",      "TEX_FloorPlate_Rust.png",   null),
            // ④ 긁힌 두꺼운 유리 — 관찰창·계기 화면
            ("Glass",     "TEX_Glass_Smudged.png",     null),
            // ⑤ 붉은 도장 금속 — 레버 그립·경고등·스트랩
            ("RedPaint",  "TEX_Machine_Housing.png",   "TEX_Machine_Housing_Emis.png"),
            // ⑥ 낡은 크림색 표지판
            ("Sign",      "TEX_Stencil_Warning.png",   null),
            // ⑦ 검은 고무·기름때 — 타공판·챔버·롤러
            ("Grease",    "TEX_Grating_Steel.png",     null),
            // ⑧ 모듈 칼라 — **무쇠**다. `TEX_WallPaint_Peeled` 는 근접에서 자갈로
            // 읽혀 「산화 강철」이 아니라 「콘크리트 덩어리」로 보였다.
            ("Collar",    "TEX_Machine_Housing.png",   null),
            ("RingSteel", "TEX_Machine_Housing.png",   null),
            // ⑨ 챔버 내부 — 텍스처는 붙이되 **틴트로 눌러** 거의 검게 만든다.
            ("ChamberDark", "TEX_Grating_Steel.png",   null),
        };

        [MenuItem("Ascend/Room/Wire Reference Room Textures")]
        public static void Wire()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 에셋을 고치지 않는다."); return; }

            var report = new StringBuilder("[상승] 레퍼런스 룸 표면 배선\n");
            int wired = 0, missing = 0;

            foreach ((string mat, string baseMap, string emisMap) in Map)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/RM_{mat}.mat");
                if (m == null) { report.AppendLine($"  ⚠ 머티리얼 없음 — RM_{mat}"); missing++; continue; }

                Texture2D tex = Load(baseMap, report, ref missing);
                if (tex != null && m.HasProperty("_BaseMap"))
                {
                    m.SetTexture("_BaseMap", tex);
                    // **ST 를 만지지 않는다.** UV 가 이미 월드 미터 기준이다.
                    m.SetTextureScale("_BaseMap", Vector2.one);
                    m.SetTextureOffset("_BaseMap", Vector2.zero);

                    // 🔴 **텍스처가 붙으면 `_BaseColor` 는 반사율이 아니라 틴트다.**
                    //
                    // 셰이더가 `albedo = _BaseColor.rgb * baseTex` 로 **곱한다.**
                    // 조립기가 넣은 값(철판 0.340)은 무지 면일 때의 반사율이고,
                    // 생성 텍스처는 이미 어두운 산업 팔레트를 담고 있다. 둘을 곱하면
                    // 0.340 × 0.2 ≈ **0.068** 이 되어 두 번 어두워진다 —
                    // 실측으로 p50 이 49 → 2 로 무너졌다.
                    //
                    // 색은 텍스처가 나르므로 여기서는 **거의 흰색**을 넣는다.
                    // 재질별 미세한 색조만 남겨 일곱 재질이 구분되게 한다.
                    Color tint = Tint(mat);
                    // ⚠ **알파를 덮어쓰지 않는다.** 유리는 조립기가 투명(α 0.34)으로
                    // 세워 두는데, 여기서 불투명 틴트를 그대로 쓰면 다시 막혀
                    // 영혼 구슬이 사라진다. 실제로 그렇게 사라진 적이 있다.
                    Color prev = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    tint.a = prev.a;
                    m.SetColor("_BaseColor", tint);
                    report.AppendLine($"     _BaseColor → 틴트 {tint.r:F2},{tint.g:F2},{tint.b:F2} (반사율은 텍스처가 나른다)");
                    wired++;
                }

                if (emisMap != null && m.HasProperty("_EmissionMap"))
                {
                    Texture2D e = Load(emisMap, report, ref missing);
                    if (e != null)
                    {
                        m.SetTexture("_EmissionMap", e);
                        m.SetFloat("_EmissionMapEnabled", 1f);
                        m.EnableKeyword("_EMISSIONMAP_ON");
                        report.AppendLine($"  RM_{mat} 발광 마스크 ← {emisMap}");
                    }
                }

                // 명세 §14 「거친 픽셀 페인팅」 — 근거리 그레인을 켠다. 중립 회색이
                // 기본이라 텍스처가 없으면 아무 일도 안 일어난다.
                if (m.HasProperty("_DetailMapEnabled"))
                {
                    m.SetFloat("_DetailMapEnabled", 0f);   // 아직 켜지 않는다 (한 배치에 한 축)
                    m.DisableKeyword("_DETAILMAP_ON");
                }

                EditorUtility.SetDirty(m);
                report.AppendLine($"  RM_{mat} ← {baseMap}  (ST 1,1,0,0 — UV 가 월드 미터 기준)");
            }

            AssetDatabase.SaveAssets();
            report.AppendLine($"  배선 {wired}장 · 누락 {missing}건");
            report.AppendLine($"  밀도 {Art.ReferenceRoomSpec.SurfaceTexelsPerMeter} px/m " +
                              $"(= 미터당 {Art.ReferenceRoomSpec.SurfaceUvPerMeter:F3} 반복 × {Art.ReferenceRoomSpec.SurfaceTextureSize}px)");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// 텍스처 위에 얹는 색조. 명세 §10 의 팔레트 방향만 남기고 밝기는 건드리지 않는다.
        ///
        /// 1.0 을 넘지 않는다 — 넘으면 텍스처의 밝은 화소가 1 을 넘어 블룸이 붙고,
        /// 명세 §11 이 「강한 블룸」을 금지한다.
        /// </summary>
        private static Color Tint(string mat)
        {
            switch (mat)
            {
                // 철판 — 올리브가 섞인 차콜 방향으로 아주 살짝
                // 🔴 **벽은 배경이다.** 0.94 로 두면 벽 화소가 장치만큼 밝아
                // 아홉 창이 배경에 묻힌다 — 캡처에서 방 전체가 균일한 베이지로
                // 읽혔고, 그 상태에서는 무엇을 봐야 하는지가 화면에 없다.
                // 칼라(1.00)와 벌리는 것이 목적이므로 **칼라를 올리지 않고
                // 벽을 내린다** — 올리면 블룸이 붙고 §11 이 그걸 금지한다.
                case "Steel":     return new Color(0.58f, 0.57f, 0.53f);
                // 노출 강철 — 중립
                case "BareSteel": return new Color(0.96f, 0.96f, 0.95f);
                // 녹 — 탁한 적갈색 쪽으로
                case "Rust":      return new Color(1.00f, 0.82f, 0.66f);
                // 유리 — 어둡게 눌러 「어두운 유리 안의 빛」이 읽히게 한다 (명세 §4)
                case "Glass":     return new Color(0.55f, 0.57f, 0.56f);
                // 붉은 도장 — 바랜 적색
                // 🔴 **붉어야 붉다.** 0.66/0.58 은 회색 무쇠 텍스처 위에서 갈색으로
                // 읽혔다 — 근접 캡처에서 레버 그립이 「나무 손잡이」처럼 보였고,
                // 명세는 「붉은 원통형 그립」을 요구한다. 녹색·청색 채널을 크게
                // 눌러야 무채색 텍스처가 실제로 붉어진다.
                case "RedPaint":  return new Color(1.00f, 0.34f, 0.26f);
                // 표지판 — 누렇게 변색된 크림
                case "Sign":      return new Color(1.00f, 0.97f, 0.88f);
                // 기름때 — 어둡게
                // 챔버 내부다. **밝으면 안쪽이 얕아 보인다** — 근접 캡처에서 챔버가
                // 갈색으로 밝게 떠 영혼 뒤의 「깊이」가 사라졌다.
                // 챔버 안. 0.16 은 이 방에서 가장 어두운 값이고, 그래야 영혼이
                // 「어둠 속의 빛」으로 읽힌다.
                case "ChamberDark": return new Color(0.16f, 0.15f, 0.15f);
                case "Grease":    return new Color(0.46f, 0.45f, 0.43f);
                // 칼라는 벽보다 **밝다** — 명세의 재질 계층에서 유일하게 밝은 금속이다.
                case "Collar":    return new Color(1.00f, 0.96f, 0.90f);
                case "RingSteel": return new Color(0.86f, 0.84f, 0.80f);
                default:          return Color.white;
            }
        }

        private static Texture2D Load(string file, StringBuilder report, ref int missing)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{file}");
            if (t == null) { report.AppendLine($"  ⚠ 텍스처 없음 — {file}"); missing++; return null; }
            EnsurePointFilter($"{TexDir}/{file}", report);
            return t;
        }

        /// <summary>
        /// 명세 §14 「텍스처 필터링은 Point 또는 약한 Bilinear」.
        ///
        /// 임포터를 여기서 강제하는 이유: `.meta` 를 손으로 고치면 다음 리임포트에
        /// 되돌아가고, 되돌아간 사실은 **캡처를 확대해 보기 전에는 안 보인다.**
        /// 이 저장소는 「설정했는데 적용되지 않았다」로 여러 번 실패했다.
        /// </summary>
        private static void EnsurePointFilter(string path, StringBuilder report)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            if (imp.filterMode == FilterMode.Point && imp.mipmapEnabled && imp.anisoLevel <= 1) return;

            imp.filterMode = FilterMode.Point;
            // 밉맵은 **켠다.** 끄면 먼 벽에서 픽셀이 반짝여(에일리어싱) 명세 §14 의
            // 「내부 해상도 320×240 에 어울리는 가독성」과 반대로 간다.
            imp.mipmapEnabled = true;
            imp.anisoLevel = 1;
            imp.SaveAndReimport();
            report.AppendLine($"     임포터 교정 — {System.IO.Path.GetFileName(path)} Point · mipmap ON · aniso 1");
        }
    }
}
