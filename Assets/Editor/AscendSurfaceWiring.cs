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

        /// <summary>
        /// `_BaseColor` 명도를 어떻게 정할 것인가. **이 구분이 판독성을 지킨다.**
        ///
        /// 셰이더는 `_BaseMap` 을 `_BaseColor` 에 **곱한다.** 그래서 텍스처를 물리면
        /// 반사율이 텍스처 평균 밝기만큼 **떨어진다** — 아무것도 안 하면 텍스처판이
        /// 그레이박스보다 어두워지고, 그것이 과거 두 번의 롤백 원인이다.
        /// </summary>
        private enum Tone
        {
            /// <summary>목표 명도로 **올린다.** 캐빈 껍데기용 — 어두운 회색을 벗긴다.</summary>
            Lift,

            /// <summary>
            /// **현재 평균 반사율을 유지한다.** `V ← V / 텍스처평균` 으로 되돌린다.
            ///
            /// 계기판·게이지 배경·결과판처럼 **글자와 눈금이 그 위에 얹히는 면**은
            /// 어두운 것이 기능이다. 밝게 올리면 흰 글자와의 대비가 무너진다 —
            /// `VISUAL_SPEC` §8 「로우파이를 명분으로 판독성을 희생하지 않는다」.
            /// G-1 이 재는 것은 **밝기가 아니라 분산**이라, 밝기를 그대로 두고도
            /// 텍스처를 물리면 축은 올라간다.
            /// </summary>
            Preserve,
        }

        /// <summary>
        /// 배선 규칙 한 줄.
        ///
        /// ── 「미터당 반복 수」를 버린 이유 ────────────────────────────────────
        ///
        /// 이 표는 원래 `회/m` 를 들고 렌더러 실측 크기를 곱해 `_BaseMap_ST` 를 만들었다.
        /// 그 설계는 **월드 등방성**(어느 벽에서나 무늬 크기가 같다)은 보장하지만
        /// **화면 텍셀 밀도**는 보장하지 않는다. 화면 밀도는 카메라 거리와 시선각이 정한다.
        ///
        /// 그리고 캐빈을 4배로 키운 순간 같은 `회/m` 이 ρ(텍셀/화소)를 **2배**로 만들었다 —
        /// 확대가 그 전제를 깨뜨린 것이다.
        ///
        /// 조사 결과가 결정적이다. 화면 px/텍셀별 알베도 12장의 국소 대비 중앙값:
        ///
        ///     px/텍셀  16      8      4      3      2     1.5    1.0   0.75    0.5
        ///     relC   0.0000 0.0000 0.071  0.089  0.104  0.129  0.163  0.182  0.133
        ///
        /// **8 px/텍셀 이상에서 정확히 0** 이다 — 8×8 블록이 텍셀 하나 안에 들어가고
        /// Point 필터라 그 블록의 값이 전부 같아진다. 표준편차가 원리적으로 0 이 된다.
        /// 최적은 **0.75~1.0 px/텍셀 (ρ 1.0~1.33)**.
        ///
        /// 그래서 이제 `_BaseMap_ST` 를 **직접** 적는다. 아래 값은 확대 후 현재 씬에서
        /// ρ 를 실측해 목표 max(ρ) ≈ 1.1 로 역산한 것이다(ρ 는 ST 에 선형 비례).
        /// **크기에서 유도하지 않는다** — 유도는 화면 밀도를 모른다.
        ///
        /// ⚠ 캐빈 치수나 카메라 배치가 또 바뀌면 이 값들은 **다시 재야 한다.**
        /// 자동으로 따라가지 않는다. 그것이 이 방식이 치르는 대가이고, 대신
        /// 「화면에 무늬가 보이는가」를 직접 겨눈다.
        /// </summary>
        private readonly struct Rule
        {
            public readonly string  Material;  // 머티리얼 이름
            public readonly string  Texture;   // TexDir 안의 파일 이름 (확장자 없이)
            public readonly Vector2 ST;        // `_BaseMap_ST` 타일링 — 실측 역산값
            public readonly float   Value;     // Tone.Lift 일 때의 목표 명도 / Preserve 의 설계 원본
            public readonly string  Emission;  // 발광 마스크. 없으면 null
            public readonly Tone    Tone;      // 명도 정책
            public readonly bool    Stylize;   // URP/Lit → Ascend/Stylized 전환 여부

            public Rule(string mat, string tex, float stU, float stV, float value,
                        string emis = null, Tone tone = Tone.Lift, bool stylize = false)
            { Material = mat; Texture = tex; ST = new Vector2(stU, stV); Value = value;
              Emission = emis; Tone = tone; Stylize = stylize; }
        }

        // ⚠ **ST 값은 확대 후 현재 씬에서 ρ(텍셀/화소)를 실측해 역산한 것이다.**
        // 목표 max(ρ) ≈ 1.1. 「그대로」라고 적힌 것은 이미 최적 구간(ρ ≈ 1.1)이라 안 건드린다.
        // 「미측정」은 조사 표에 없던 면이라 직전 값을 그대로 옮겨 적은 것이다 —
        // **최적이라는 뜻이 아니라 아직 안 쟀다는 뜻이다.**
        private static readonly Rule[] Rules =
        {
            //                                                            ST u      ST v    명도   (측정 ρ → 목표)
            new Rule("CarShell_Floor",           "TEX_FloorPlate_Rust",   0.22f,   0.27f,  0.90f),   // ρ 19.76 — 최악
            new Rule("CarShell_WallL",           "TEX_WallPanel_Riveted", 6.60f,   5.67f,  0.92f),   // ρ 0.533 — 너무 성겼다
            new Rule("CarShell_WallR",           "TEX_WallPanel_Riveted", 6.60f,   5.67f,  0.92f),   // ρ 0.533
            new Rule("CarShell_FrontWall",       "TEX_WallPaint_Peeled",  5.66f,   5.99f,  0.92f),   // ρ 0.505
            new Rule("CarShell_BackWall_Left",   "TEX_WallPanel_Riveted", 1.375f,  2.75f,  0.92f),   // ρ 1.111 — 그대로 (최적)
            new Rule("CarShell_BackWall_Right",  "TEX_WallPanel_Riveted", 0.725f,  2.75f,  0.92f),   // 미측정
            new Rule("CarShell_Ceiling",         "TEX_WallPanel_Riveted", 2.60f,   3.20f,  0.88f),   // 미측정
            new Rule("CarShell_BackWall_Lintel", "TEX_Stencil_Warning",   0.50f,   1.725f, 0.95f),   // 미측정
            new Rule("CarShell_Handrail_R",      "TEX_Conduit_Cable",     0.12f,   3.80f,  0.90f),   // 미측정
            new Rule("CarShell_Handrail_B",      "TEX_Conduit_Cable",     0.14f,   0.01f,  0.90f),   // ρ 18.58
            new Rule("CarShell_TankStand",       "TEX_Machine_Housing",   0.24f,   1.02f,  0.90f, "TEX_Machine_Housing_Emis"), // 미측정

            // 로비는 **문 밖**이다. 캐빈과 같은 명도로 올리면 출입구의 깊이가 사라지고
            // 「안이 어둡고 밖이 밝다」는 공간 판독이 뒤집힌다. 텍스처는 물리되
            // 명도는 낮게 유지한다 — 이 둘은 다른 목적이다.
            new Rule("CarShell_LobbyFloor",      "TEX_Grating_Steel",     0.19f,   0.22f,  0.45f),   // ρ 17.71
            new Rule("CarShell_LobbyBack",       "TEX_Concrete_Shaft",    1.20f,   2.20f,  0.38f),   // 미측정

            // ── 화면 **중앙** ─────────────────────────────────────────────────
            //
            // G-1 국소 분산이 0.00 → 2.41 에서 멈춘 이유를 세어서 찾았다:
            // **머티리얼 13/31 배선인데 렌더러 슬롯은 102개 중 14개뿐**이었다.
            // 위의 `CarShell_*` 13장은 캐빈 껍데기이고 프레임 **가장자리**다.
            // 프레임 한가운데를 채우는 계기판·결과판·콘솔·레버는 전부
            // 무지 `URP/Lit` 인 `M_Gray_*` / `TenFloor_*` 슬롯 60여 개였다.
            //
            // 그래서 여기를 배선한다. 두 가지를 함께 한다 —
            // ① 텍스처 ② `URP/Lit` → `Ascend/Stylized`(안 쓰면 계단도 림도 안 걸린다).
            //
            // ⚠ **판독 면은 `Tone.Preserve` 다.** 아래 「글자·눈금이 얹히는 면」 넷은
            // 어두운 것이 기능이라 명도를 올리지 않는다.
            // ⚠ **셋 다 `Preserve` 다. 올리면 안 된다.**
            //
            // 첫 판본은 이 셋을 0.88 / 0.85 / 0.62 로 **올렸고, 그것이 순손실이었다.**
            // `HumanScaleLayout.cs:48-52` 가 세운 3단 명도 위계 —
            // 결과판 0.85 · 조작 대상 0.62 · 구조물 0.16 — 가 한꺼번에 0.83~0.94 로
            // 뭉쳐서 **「만질 수 있는 것」과 「배경 구조」가 같은 밝기가 됐다.**
            // 캡처 `21` 에서 통관 판과 심볼 타일이 다 같은 창백한 회색으로 붙어
            // 심볼 실루엣 대비까지 무너졌다. 명도는 위계이지 장식이 아니다.
            //
            // 값은 배선 전 씬 실측이고 `HumanScaleLayout` 의 상수와 같은 계열이다.
            //                                                       ST u    ST v   설계명도                       (측정 ρ)
            new Rule("M_Gray_Recessive",   "TEX_Machine_Housing", 0.11f, 0.11f, 0.170f, null, Tone.Preserve, true), // ρ 28.85 — 전체 최악
            new Rule("M_Gray_Interactive", "TEX_Machine_Housing", 0.46f, 0.46f, 0.657f, null, Tone.Preserve, true), // ρ 4.82
            new Rule("M_Gray_Button",      "TEX_Machine_Housing", 0.17f, 0.17f, 0.320f, null, Tone.Preserve, true), // ρ 13.28

            // 판독 면 — 명도 유지.
            // `Value` 는 **설계 원본 명도**(HSV 의 V)다. 배선 전 씬에서 실측한 값이고,
            // `Tone.Preserve` 가 이것을 텍스처 평균으로 나눠 평균 반사율을 되돌린다.
            // 현재 값이 아니라 이 상수를 쓰기 때문에 몇 번을 돌려도 같은 결과가 나온다.
            new Rule("M_Gray_Panel",       "TEX_Gauge_Enamel",    1.77f, 1.77f, 0.120f, null, Tone.Preserve, true), // ρ 1.86 — 이미 거의 최적
            new Rule("M_Gray_Console",     "TEX_Machine_Housing", 0.30f, 0.30f, 0.180f, null, Tone.Preserve, true), // ρ 11.08
            new Rule("M_Gray_BarBg",       "TEX_Gauge_Enamel",    0.20f, 0.20f, 0.100f, null, Tone.Preserve, true), // ρ 11.05
            // 결과판 32슬롯. 심볼 실루엣이 얹히는 면이라 **명도를 유지한다.**
            new Rule("M_Gray_Readout",     "TEX_Gauge_Enamel",    0.59f, 0.59f, 0.901f, null, Tone.Preserve, true), // ρ 7.47

            new Rule("TenFloor_212426",    "TEX_Machine_Housing", 1.18f, 1.18f, 0.150f, null, Tone.Preserve, true), // ρ 1.86
            new Rule("TenFloor_333633",    "TEX_WallPanel_Riveted",0.48f, 0.48f, 0.210f, null, Tone.Preserve, true), // ρ 4.54
            new Rule("TenFloor_756B4C",    "TEX_Pallet_Wood",     0.12f, 0.12f, 0.460f, null, Tone.Preserve, true), // ρ 17.87
        };

        // ── 손대지 않는 것과 그 이유 ─────────────────────────────────────────
        //
        //   M_Gray_BarFill        전력 게이지의 **채움 길이가 곧 수치**다. 텍스처를 물리면
        //                        채움 경계가 흐려져 「얼마나 찼는가」가 안 읽힌다.
        //   M_Gray_Light          상태 점등(녹색). 색 자체가 신호다.
        //   TenFloor_5C9E8C       청록 강조색 — 심볼·상태 구분에 쓰일 수 있어 건드리지 않는다.
        //   URP/Unlit (6슬롯)     조준 하이라이트. Unlit 이 의도다.
        //   Lit (1슬롯)           소유가 불분명하다. 모르는 것은 안 바꾼다.
        //   TMP 머티리얼          글자다.

        [MenuItem("Ascend/Graphics/Wire Surface Textures")]
        public static void WireTextures()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            Scene scene = AscendGraphicsBuilder.EnsureScene();
            if (!scene.IsValid()) return;

            var report = new StringBuilder("[상승] 표면 텍스처 배선\n");

            // 1) 임포트 설정. Repeat + Point 필터. 근거는 그 함수의 주석에 있다.
            int fixedWrap = EnsureImportSettings(report);

            // 2) 실측 월드 크기 — 이제 타일링에 쓰지 않는다. **보고에만 쓴다.**
            //    크기 유도는 화면 텍셀 밀도를 모른다(위 `Rule` 주석). 그래도 크기를
            //    함께 찍어 두면 다음에 ρ 를 다시 잴 때 무엇이 변했는지 알 수 있다.
            var sizes = MeasureMaterialSizes();

            Shader stylized = Shader.Find("Ascend/Stylized");
            if (stylized == null) report.AppendLine("  ⚠ `Ascend/Stylized` 셰이더를 찾지 못했다 — 전환을 건너뛴다");

            int wired = 0, missingMat = 0, missingTex = 0, stylizedCount = 0;
            _meanCache.Clear();
            foreach (Rule rule in Rules)
            {
                Material mat = FindSceneMaterial(rule.Material);
                if (mat == null) { report.AppendLine($"  ⚠ 머티리얼 없음 — {rule.Material}"); missingMat++; continue; }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{rule.Texture}.png");
                if (tex == null) { report.AppendLine($"  ⚠ 텍스처 없음 — {rule.Texture}"); missingTex++; continue; }

                Undo.RecordObject(mat, "Wire surface texture");

                Color before = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                string shaderBefore = mat.shader != null ? mat.shader.name : "null";

                // ── 셰이더 전환 ────────────────────────────────────────────
                //
                // ⚠ **발광 상태를 먼저 붙잡는다.** `URP/Lit` 은 `_EMISSION` 키워드가 꺼져
                // 있으면 `_EmissionColor` 에 값이 남아 있어도 화면에 안 나온다. 그런데
                // `Ascend/Stylized` 는 키워드 없이 `color += _EmissionColor.rgb` 를 무조건
                // 더한다(셰이더 615행). 그대로 전환하면 **꺼져 있던 발광이 갑자기 켜진다** —
                // 계기판 32슬롯이 한꺼번에 빛나면 G-3 발광 상한(6%)이 즉시 깨진다.
                if (rule.Stylize && stylized != null && mat.shader != stylized)
                {
                    bool emissionWasOn = mat.IsKeywordEnabled("_EMISSION");
                    Color keptEmission = mat.HasProperty("_EmissionColor")
                        ? mat.GetColor("_EmissionColor") : Color.black;

                    mat.shader = stylized;

                    mat.SetColor("_EmissionColor", emissionWasOn ? keptEmission : Color.black);
                    stylizedCount++;
                }

                // ── 명도 (경고 1) ──
                Color.RGBToHSV(before, out float h, out float s, out float v0);
                float targetV;
                if (rule.Tone == Tone.Lift)
                {
                    targetV = rule.Value;
                }
                else
                {
                    // 텍스처 평균만큼 되돌려 **평균 반사율을 보존**한다.
                    //
                    // ⚠ 나누는 대상은 `rule.Value`(**설계 원본 명도**)이지 `v0`(현재 값)가
                    // 아니다. 현재 값으로 나누면 두 번째 실행이 이미 나눈 값을 다시 나눠
                    // 실행할 때마다 밝아진다 — 첫 판본이 그랬고 멱등 검사가 `False` 로 잡았다.
                    // `Reproportion Elevator Car` 가 계기판을 1/0.66 씩 두 번 민 것과 같은 실패다.
                    // **값은 델타가 아니라 절대값으로 준다.**
                    float mean = Mathf.Max(0.05f, TextureMean(tex));
                    targetV = Mathf.Clamp01(rule.Value / mean);
                }
                Color lifted = Color.HSVToRGB(h, s, targetV);
                lifted.a = before.a;
                mat.SetColor("_BaseColor", lifted);

                // ── 타일링은 **실측 ρ 에서 역산한 절대값**이다 (위 `Rule` 주석) ──
                Vector2 uv = rule.ST;
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
                report.AppendLine($"  {rule.Material,-22} ← {rule.Texture,-22} " +
                                  $"ST ({uv.x:F3},{uv.y:F3}) · 실측 {sz.x:F2}×{sz.y:F2}×{sz.z:F2} m · " +
                                  $"V {ValueOf(before):F3} → {targetV:F3} [{rule.Tone}] · " +
                                  $"shader {shaderBefore} → {(mat.shader != null ? mat.shader.name : "null")}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            report.AppendLine($"  배선 {wired}장 / 규칙 {Rules.Length}개 · 머티리얼 없음 {missingMat} · 텍스처 없음 {missingTex} · " +
                              $"셰이더 전환 {stylizedCount}장 · 임포트 고친 텍스처 {fixedWrap}장");
            report.AppendLine($"  렌더러 슬롯 배선 — {CountWiredSlots()} (이 수가 G-1 을 움직인다. 머티리얼 수가 아니다)");
            report.AppendLine("  ⚠ 「배선했다」는 머티리얼 수다. **화면에 보이는가는 화소가 답한다** — " +
                              "`tools/capture-metrics.ps1` 의 G-1 국소 분산으로 판정할 것.");
            Debug.Log(report.ToString());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  통관 유리 3장 (`UP-FIX-47`)
        //
        //  16차 독립 평가와 전담 화소 조사가 **독립적으로** 같은 곳을 지목했다 —
        //  Unlit `TubeFrame` 이 프레임의 29.4% 를 차지하고 그 블록의 28.6% 가
        //  std 1.75 에 구조적으로 고정돼 있다. G-1 과 G-2 가 공존할 수 있는 유일한 창이다.
        //
        //  ── ⚠ 지시대로 `Ascend/Stylized` 로 바꾸지 **않았다.** 바꿀 수 없다 ──────
        //
        //  `AscendStylized.shader:142-146` 이 `RenderType=Opaque · Queue=Geometry` 이고
        //  블렌드 경로도 `_Surface`/`_Blend` 프로퍼티도 없다. 그림자까지 드리운다.
        //  현재 통관 재질은 **alpha 0.25 · Transparent · ZWrite Off** 인 유리다.
        //
        //  실측: 통관 부피 안에 렌더러 **34개**가 있고 그중 **27개가 심볼 메시**다
        //  (`Sym_NormalSoul`/`Sym_Absorber`/`Sym_Proliferator`, x=−0.84).
        //  통관을 불투명으로 바꾸면 **3×3 결과판 전체가 사라진다.**
        //  통관은 결과판을 *보여주는* 물건이므로 그건 개선이 아니라 게임의 판독 채널을
        //  없애는 것이다 — 「판독성이 스타일보다 우선한다」가 정확히 이 경우다.
        //
        //  그래서 **`URP/Lit` + Transparent** 로 간다. 요구된 세 효과를 다 준다 —
        //  ① 텍스처가 걸린다 ② **조명을 받는다**(형태 음영 · G-4 주사선이 측정 가능해진다)
        //  ③ 투명이 유지되어 심볼이 보인다. 못 주는 것은 `Ascend/Stylized` 의 계단·림뿐이고,
        //  그건 셰이더에 투명 패스가 생긴 뒤에나 가능하다(셰이더는 다른 레인 소유).
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 유리 알파. 0.25 → 0.18. 평가자가 「근백색 평면이 결과판을 하얗게 덮는다」고 한
        /// 것이 이 값이다. 낮추면 심볼 대비가 살아나고 G-2 의 p50 도 함께 내려간다.
        /// </summary>
        public const float GlassAlpha = 0.18f;

        /// <summary>
        /// 유리 타일링. **실측 ρ 가 아니라 계산값이다** — 이 면은 조사 표에 없었다.
        ///
        /// 유도: 통관 면은 0.30 m 폭 × 1.30 m 높이, 플레이어(x=0.55)에서 약 1.5 m.
        /// 1080p · 수직 FOV 60° 에서 1 화소 ≈ 1.5 × 2·tan30° / 1080 ≈ 1.6 mm.
        /// 텍스처는 128 px 이므로 텍셀을 화소와 같게 하려면
        ///   가로 0.30 / (0.0016 × 128) ≈ 1.46 회 · 세로 1.30 / (0.0016 × 128) ≈ 6.35 회.
        ///
        /// ⚠ **가정이 셋 붙어 있다**(거리 1.5 m · 정면 · FOV 60°). 지표 도구의 ρ 측정이
        /// 이 면을 덮게 되면 그 실측으로 갈아야 한다. 추정임을 숨기지 않는다.
        /// </summary>
        public static readonly Vector2 GlassST = new Vector2(1.5f, 6.3f);

        [MenuItem("Ascend/Graphics/Wire Tube Glass")]
        public static void WireTubeGlass()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            Scene scene = AscendGraphicsBuilder.EnsureScene();
            if (!scene.IsValid()) return;

            var report = new StringBuilder("[상승] 통관 유리 배선 (`UP-FIX-47`)\n");
            var glass = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/TEX_Glass_Smudged.png");
            if (glass == null) { Debug.LogError("[상승] TEX_Glass_Smudged 없음"); return; }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { Debug.LogError("[상승] URP/Lit 셰이더 없음"); return; }

            int done = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.gameObject.name != "TubeFrame") continue;
                Material mat = r.sharedMaterial;
                if (mat == null) continue;

                // 이 인스턴스를 통관 밖에서도 쓰면 복제한다. 실측상 지금은 공유가 없지만
                // (0건) 그건 지금의 사실이지 불변식이 아니다 — 남의 표면을 조용히 바꾸지 않는다.
                bool sharedElsewhere = false;
                foreach (Renderer other in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (other.gameObject.name == "TubeFrame") continue;
                    foreach (Material om in other.sharedMaterials) if (om == mat) sharedElsewhere = true;
                }
                if (sharedElsewhere)
                {
                    mat = new Material(mat) { name = "TubeGlass" };
                    r.sharedMaterial = mat;
                    report.AppendLine("  공유 인스턴스라 복제했다 → TubeGlass");
                }

                Undo.RecordObject(mat, "Wire tube glass");
                Color before = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                string shaderBefore = mat.shader != null ? mat.shader.name : "null";

                mat.shader = lit;

                // ── Transparent 를 **명시적으로** 세운다 ──
                // 셰이더만 바꾸면 URP 는 Opaque 기본값으로 되돌리고, 그러면 통관이
                // 불투명해져 심볼 27개가 사라진다. 키워드·블렌드·큐를 전부 직접 쓴다.
                mat.SetFloat("_Surface", 1f);                 // 1 = Transparent
                mat.SetFloat("_Blend", 0f);                   // 0 = Alpha
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_AlphaClip", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                // 유리답게 — 금속 아님, 반사 약간, 그림자는 받되 드리우지 않는다.
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
                if (mat.HasProperty("_ReceiveShadows")) mat.SetFloat("_ReceiveShadows", 1f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                mat.SetTexture("_BaseMap", glass);
                mat.SetTextureScale("_BaseMap", GlassST);
                mat.SetTextureOffset("_BaseMap", Vector2.zero);

                // 색조는 유지하고 알파만 내린다. **절대값이다** — 현재 알파에 곱하면
                // 두 번 돌릴 때마다 투명해진다(이미 한 번 그 실패를 했다).
                Color tint = new Color(before.r, before.g, before.b, GlassAlpha);
                mat.SetColor("_BaseColor", tint);

                EditorUtility.SetDirty(mat);
                done++;
                report.AppendLine($"  {r.gameObject.name} ({r.transform.parent?.name}) — shader {shaderBefore} → {mat.shader.name} " +
                                  $"· Transparent · alpha {before.a:F2} → {GlassAlpha:F2} · ST {GlassST} · queue {mat.renderQueue}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine($"  통관 {done}장 — 심볼은 계속 보인다(투명 유지). 불투명 전환은 결과판을 지운다");
            Debug.Log(report.ToString());
        }

        private static float ValueOf(Color c) { Color.RGBToHSV(c, out _, out _, out float v); return v; }

        private static readonly Dictionary<Texture2D, float> _meanCache = new Dictionary<Texture2D, float>();

        /// <summary>
        /// 텍스처의 평균 밝기. `Tone.Preserve` 가 「곱해서 어두워진 만큼」을 되돌리는 데 쓴다.
        ///
        /// 하드코딩하지 않는다 — 텍스처 레인이 생성기를 고치면 평균이 바뀌고,
        /// 상수는 그때 조용히 틀린 값이 된다. 읽을 수 없는 텍스처는 임포터를 잠깐
        /// 열었다가 **원래대로 되돌린다**(남의 레인 에셋의 설정을 바꾼 채 두지 않는다).
        /// </summary>
        private static float TextureMean(Texture2D tex)
        {
            if (tex == null) return 1f;
            if (_meanCache.TryGetValue(tex, out float cached)) return cached;

            string path = AssetDatabase.GetAssetPath(tex);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            bool restore = false;
            if (imp != null && !imp.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
                restore = true;
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            float mean = 1f;
            try
            {
                Color[] px = tex.GetPixels();
                if (px.Length > 0)
                {
                    double sum = 0;
                    for (int i = 0; i < px.Length; i++)
                        sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                    mean = (float)(sum / px.Length);
                }
            }
            catch (System.Exception e)
            {
                // 조용히 1 을 쓰지 않는다 — 그러면 `Preserve` 가 아무것도 안 한 채 성공처럼 보인다.
                Debug.LogWarning($"[상승] `{tex.name}` 평균을 읽지 못했다 ({e.GetType().Name}). " +
                                 "명도 보존이 적용되지 않는다 — 그 면은 텍스처 평균만큼 어두워진다.");
            }

            if (restore && imp != null) { imp.isReadable = false; imp.SaveAndReimport(); }

            _meanCache[tex] = mean;
            return mean;
        }

        /// <summary>텍스처가 걸린 렌더러 슬롯 수 / 전체. G-1 을 움직이는 것은 이 수다.</summary>
        private static string CountWiredSlots()
        {
            int wired = 0, total = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    total++;
                    if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) wired++;
                }
            return $"{wired}/{total}";
        }

        /// <summary>
        /// 생성 텍스처의 임포트 설정을 맞춘다. 둘 다 **국소 분산이 안 오르는 원인**이다.
        ///
        /// **① `Repeat`** — `Clamp` 면 타일링 &gt; 1 에서 가장자리 화소가 늘어나
        /// 면 대부분이 단색으로 채워진다.
        ///
        /// **② `Point` 필터** — 이쪽이 실측으로 잡힌 진짜 원인이다.
        /// 13장을 전부 배선한 뒤에도 포스트를 끈 진단 세트의 G-1 국소 분산 중앙값이
        /// **2.41** 이었다(통과선 12.0). 0.00 에서는 올라왔지만 「텍스처가 보인다」에는
        /// 한참 못 미친다.
        ///
        /// 산술이 원인을 지목한다 — 256px 텍스처를 0.50 회/m 로 깔면 **128 텍셀/m** 이고,
        /// 1080p 에서 2.4m 벽이 화면 800px 을 차지하면 **333 화면px/m** 이다.
        /// 즉 텍셀 하나가 화면 2.6px 로 **확대**되고, 그 사이를 bilinear 가 부드럽게 메운다.
        /// 8×8 블록 표준편차는 그 보간에 그대로 먹힌다 — 텍스처는 있는데 화소는 평평하다.
        ///
        /// `Point` 는 그 보간을 없앤다. 그리고 이것은 우회가 아니라 **스타일 락 그 자체**다 —
        /// `VISUAL_BIBLE` §2.1 의 PS1·초기 PS2 계열은 점 필터가 기본이다.
        /// </summary>
        private static int EnsureImportSettings(StringBuilder report)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                bool dirty = false;
                if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; dirty = true; }
                if (imp.anisoLevel != 0) { imp.anisoLevel = 0; dirty = true; }
                if (!dirty) continue;

                imp.SaveAndReimport();
                changed++;
                report?.AppendLine($"  임포트 수정 — {System.IO.Path.GetFileName(path)} wrap=Repeat filter=Point aniso=0");
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

        // `TilingFor` 를 지웠다 — 크기에서 타일링을 유도하던 함수다.
        //
        // 왜 지웠나: 그 함수는 **월드 등방성**을 지켰고, 그건 틀린 목표였다.
        // G-1 이 재는 것은 화면 8×8 블록의 표준편차이고 그건 **화면 텍셀 밀도**가 정한다.
        // 8 px/텍셀 이상이면 블록 전체가 텍셀 하나 안에 들어가 Point 필터로 값이 하나가 되고
        // 표준편차가 **원리적으로 0** 이 된다 — 텍스처가 아무리 잘 배선돼 있어도.
        // 캐빈을 4배로 키운 순간 같은 rep/m 이 ρ 를 2배로 만들어 그 구간에 들어갔다.
        //
        // 대체물은 `Rule.ST` 의 실측 역산 절대값이다. 대가는 자동 추종을 잃는 것이고,
        // 그래서 치수·카메라가 바뀌면 **다시 재야 한다**고 `Rule` 주석에 적어 뒀다.

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
