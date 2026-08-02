using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 포스트 체인과 조명을 세우는 **멱등** 조립기.
    ///
    /// 왜 필요한가: 2026-08-02 정찰이 이 씬에 포스트 처리가 **통째로 꺼져 있다**는 것을
    /// 찾았다. `Main Camera` 의 `m_RenderPostProcessing: 0` 이고 씬에 `Volume` 이 0개라,
    /// `PC_RPAsset` 이 물고 있던 `SampleSceneProfile`(Bloom·Tonemapping·Vignette)이
    /// **한 픽셀도 반영되지 않았다.** 독립 시각 평가가 14회 연속 REJECT 인 채
    /// 스타일 점수가 2.20 에서 안 움직인 원인 중 하나다.
    ///
    /// 이 파일의 규칙 (`CLAUDE.md` — 이 저장소가 실제로 당한 실패에서 나왔다):
    ///
    /// 1. **값은 델타가 아니라 절대값으로 준다.** `Reproportion Elevator Car` 가 멱등이
    ///    아니어서 두 번 돌자 계기판이 1/0.66 씩 두 번 밀린 전례가 있다. 여기의 모든
    ///    설정은 "현재 값에 곱하거나 더하지" 않고 **항상 같은 수를 쓴다.**
    /// 2. **"없으면 만든다"가 아니라 "항상 이 상태로 만든다".** 오브젝트가 이미 있으면
    ///    재사용하되 모든 필드를 다시 쓴다. 생성할 때만 설정하면 고칠 기회가 사라진다.
    /// 3. 프로파일은 `SampleSceneProfile` 을 건드리지 않는다 — 그건
    ///    `Assets/Scenes/SampleScene.unity` 가 쓰는 것이고, RP 에셋의 기본 볼륨이기도 하다.
    ///    씬 볼륨(priority 10)이 그 위에 덮는다.
    /// </summary>
    public static class AscendGraphicsBuilder
    {
        public const string ScenePath  = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
        public const string ArtDir     = "Assets/Prototype_Elevator/Art";
        public const string ProfilePath = ArtDir + "/AscendPostProfile.asset";

        /// <summary>씬 볼륨 오브젝트 이름. 이름으로 찾으므로 바꾸면 두 개가 생긴다.</summary>
        public const string VolumeObjectName = "PostVolume";

        // ══════════════════════════════════════════════════════════════════════
        //  G-6 통과선 (`docs/GRAPHICS_TARGET.md` §2)
        //
        //  Tonemapping 활성 · Bloom 활성(발광에만 걸리는 임계) · Vignette 활성 ·
        //  Film Grain 활성 · Color Adjustments 활성(채도 상승 금지) ·
        //  Chromatic Aberration ≤ 0.15 · **Motion Blur / DoF 금지**(판독성).
        //
        //  숫자를 여기 상수로 둔다 — 프로파일 에셋만 고치면 다음에 이 메뉴를 돌릴 때
        //  조용히 되돌아간다. 그것이 이 저장소가 반복해서 당한 실패다.
        // ══════════════════════════════════════════════════════════════════════

        // Tonemapping — Neutral. ACES 는 채도를 올리고 어두운 쪽을 뭉갠다.
        // `VISUAL_SPEC` §1 의 "탁한 산업 팔레트"와 반대 방향이라 쓰지 않는다.
        public const TonemappingMode ToneMode = TonemappingMode.Neutral;

        // Bloom — 발광에만 걸려야 한다. G-6 상한: 화면 평균 휘도 상승 ≤ 12%.
        //
        // **임계 1.05 는 너무 높았다.** 이 씬의 발광 재질은 대부분 선형 0.8~1.0 에서
        // 끝나서 아무것도 번지지 않았고, 발광 화소 비율이 0.83% → **0.257%** 로
        // 오히려 떨어졌다(G-3 하한 1.0% 미달). 임계를 재질이 실제로 닿는 값 아래로
        // 내리고 세기를 올린다. 값은 캡처 실측으로 정했다.
        public const float BloomThreshold = 0.80f;
        public const float BloomIntensity = 1.20f;
        public const float BloomScatter   = 0.70f;

        // Vignette — G-6 상한: 가장자리 감광 ≤ 45%.
        //
        // 0.34 는 **좌벽 회귀 감시선을 깨뜨렸다.** 좌벽 스트립(x300-390 · y30-300)은
        // 프레임 좌상단이라 비네트를 정면으로 맞는데, 그 영역이 어두워지면서 위험
        // 4단계의 ΔL 이 17.13 → **9.56** 으로 주저앉았다(기준 15). 감쇠는 곱셈이라
        // 단계 사이의 절대 차이도 같은 비율로 줄어든다. 넓고 얕게 바꾼다.
        //
        // 0.26 은 **G-6 상한도 깼다.** 에디터 모드 스윕(120px 코너 블록, 비네트 0 대비)에서
        // 최악 코너 감광이 **64.6%** 였다. 감광이 그레이딩 LUT **앞**에 적용되므로
        // 대비 22 가 그 어두워짐을 한 번 더 증폭한다 — 그래서 어두운 좌측 코너(64.6%)와
        // 밝은 우측 코너(35.1%)가 같은 반지름인데도 갈린다.
        //
        //   int/smooth  최악코너   int/smooth  최악코너
        //   0.10/0.42    10.2%     0.18/0.42    35.4%   ← 채택
        //   0.14/0.42    21.5%     0.22/0.42    50.7%   ← 이미 초과
        //
        // 0.18 로 내리면 좌벽 스트립도 함께 밝아져 ΔL 에는 **유리하다.**
        public const float VignetteIntensity  = 0.18f;
        public const float VignetteSmoothness = 0.42f;

        // Film Grain — G-6 상한: 프레임 간 화소 차 표준편차 ≤ 6.
        public const FilmGrainLookup GrainType = FilmGrainLookup.Medium1;
        public const float GrainIntensity = 0.30f;
        public const float GrainResponse  = 0.75f;

        // Color Adjustments — 채도는 **내리기만** 한다 (G-6: 채도 상승 금지).
        //
        // 노출 −0.15 는 방향이 틀렸다. 비네트·대비·채도 셋이 전부 **내리는** 쪽이라
        // 결과는 "대비가 커진 그림"이 아니라 그냥 **어두워진 그림**이었다 —
        // p50 이 80 → 55 로 내려가 여러 장이 G-2 하한 36 아래로 떨어졌고
        // p95 는 133 → 106 으로 멀어졌다. 어두운 쪽은 비네트가 만들고
        // 밝은 쪽은 노출과 블룸이 만들어야 양쪽이 벌어진다.
        // 0.45 → 0.30: 비네트를 0.26 → 0.18 로 내린 만큼 화면이 통째로 밝아진다.
        // p50 상한 96 에 `02`(89)·`15`(92)가 이미 붙어 있어 그대로 두면 넘어간다.
        public const float PostExposure = 0.30f;
        public const float Contrast     = 22f;
        public const float Saturation   = -12f;

        // Chromatic Aberration — `VISUAL_SPEC` §8 "지속적 색수차는 피한다".
        // G-6 상한 0.15 의 2/3 로 둔다. 0 으로 두고 싶으면 이 상수만 0 으로.
        public const float ChromaticAberration = 0.10f;

        // 그림자를 회녹색으로, 하이라이트를 미지근하게. `VISUAL_BIBLE` 의 산업 팔레트.
        public static readonly Vector4 ShadowTint    = new Vector4(0.92f, 1.00f, 0.96f, 0f);
        public static readonly Vector4 MidtoneTint   = new Vector4(1.00f, 1.00f, 1.00f, 0f);
        public static readonly Vector4 HighlightTint = new Vector4(1.03f, 0.99f, 0.94f, 0f);

        // 카메라 안티에일리어싱. PS1 룩에서는 각진 실루엣이 스타일이라 기본값은 None 이다.
        // A/B 를 뽑을 때만 `SetCameraAntialiasing` 으로 바꾼다.
        public const AntialiasingMode DefaultAntialiasing = AntialiasingMode.None;

        [MenuItem("Ascend/Graphics/Build Post Chain")]
        public static void BuildPostChain()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다.");
                return;
            }

            var report = new StringBuilder("[상승] 포스트 체인 조립\n");
            Scene scene = EnsureScene();
            if (!scene.IsValid()) return;

            VolumeProfile profile = BuildProfile(report);
            EnsureSceneVolume(scene, profile, report);
            EnsureCameraPost(report);
            EnsureHdrGrading(report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            report.AppendLine("  씬 저장 완료");
            Debug.Log(report.ToString());
        }

        // ── 프로파일 ────────────────────────────────────────────────────────
        //
        // 항상 전 필드를 다시 쓴다. `Add<T>(overrides:true)` 는 이미 있으면 기존 것을
        // 돌려주므로 두 번 돌려도 컴포넌트가 두 개 생기지 않는다.

        public static VolumeProfile BuildProfile(StringBuilder report)
        {
            if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                report?.AppendLine($"  프로파일 신설 — {ProfilePath}");
            }
            else report?.AppendLine($"  프로파일 재사용 — {ProfilePath}");

            int pruned = PruneNulls(profile);
            if (pruned > 0) report?.AppendLine($"  ⚠ 참조가 끊긴 오버라이드 {pruned}개 제거 — 다시 만든다");

            var tone = GetOrAdd<Tonemapping>(profile);
            Set(tone.mode, ToneMode);

            var bloom = GetOrAdd<Bloom>(profile);
            Set(bloom.threshold, BloomThreshold);
            Set(bloom.intensity, BloomIntensity);
            Set(bloom.scatter,   BloomScatter);
            Set(bloom.clamp,     65472f);
            Set(bloom.tint,      Color.white);
            Set(bloom.highQualityFiltering, false);   // PS1 룩 — 부드럽게 만들 이유가 없다
            Set(bloom.dirtIntensity, 0f);

            var vig = GetOrAdd<Vignette>(profile);
            Set(vig.color,      Color.black);
            Set(vig.center,     new Vector2(0.5f, 0.5f));
            Set(vig.intensity,  VignetteIntensity);
            Set(vig.smoothness, VignetteSmoothness);
            Set(vig.rounded,    false);

            var grain = GetOrAdd<FilmGrain>(profile);
            Set(grain.type,      GrainType);
            Set(grain.intensity, GrainIntensity);
            Set(grain.response,  GrainResponse);

            var color = GetOrAdd<ColorAdjustments>(profile);
            Set(color.postExposure, PostExposure);
            Set(color.contrast,     Contrast);
            Set(color.colorFilter,  new Color(0.97f, 0.98f, 1.00f, 1f));
            Set(color.hueShift,     0f);
            Set(color.saturation,   Saturation);

            var smh = GetOrAdd<ShadowsMidtonesHighlights>(profile);
            Set(smh.shadows,    ShadowTint);
            Set(smh.midtones,   MidtoneTint);
            Set(smh.highlights, HighlightTint);

            var ca = GetOrAdd<ChromaticAberration>(profile);
            Set(ca.intensity, ChromaticAberration);

            // **금지 항목을 지운다.** 누가 손으로 넣어 놓으면 판독성이 죽는다
            // (`GRAPHICS_TARGET` G-6: Motion Blur / DoF 금지).
            int purged = 0;
            purged += Remove<MotionBlur>(profile);
            purged += Remove<DepthOfField>(profile);
            if (purged > 0) report?.AppendLine($"  금지 오버라이드 {purged}개 제거 (MotionBlur/DoF)");

            EditorUtility.SetDirty(profile);
            report?.AppendLine($"  오버라이드 {profile.components.Count}종 — " +
                               $"Tonemapping({ToneMode}) Bloom(th {BloomThreshold} int {BloomIntensity}) " +
                               $"Vignette({VignetteIntensity}) FilmGrain({GrainIntensity}) " +
                               $"ColorAdjustments(cont {Contrast} sat {Saturation}) SMH ChromaticAberration({ChromaticAberration})");
            return profile;
        }

        private static void EnsureSceneVolume(Scene scene, VolumeProfile profile, StringBuilder report)
        {
            GameObject go = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == VolumeObjectName) { go = root; break; }
            }

            if (go == null)
            {
                go = new GameObject(VolumeObjectName);
                SceneManager.MoveGameObjectToScene(go, scene);
                report?.AppendLine($"  {VolumeObjectName} 신설");
            }
            else report?.AppendLine($"  {VolumeObjectName} 재사용");

            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;

            var vol = go.GetComponent<Volume>();
            if (vol == null) vol = go.AddComponent<Volume>();
            vol.isGlobal      = true;
            vol.priority      = 10f;      // RP 기본 볼륨(SampleSceneProfile) 위에 덮는다
            vol.weight        = 1f;
            vol.blendDistance = 0f;
            vol.sharedProfile = profile;

            // 전역 볼륨은 콜라이더가 필요 없다. 누가 붙여 놨으면 조용히 두지 않고 지운다 —
            // 콜라이더가 있으면 플레이어 캡슐이 걸린다.
            var col = go.GetComponent<Collider>();
            if (col != null) { Object.DestroyImmediate(col); report?.AppendLine("  PostVolume 의 Collider 제거"); }

            report?.AppendLine($"  Volume — global {vol.isGlobal} · priority {vol.priority} · profile {profile.name}");
        }

        private static void EnsureCameraPost(StringBuilder report)
        {
            int touched = 0;
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                if (data.renderType != CameraRenderType.Base) continue;
                data.renderPostProcessing = true;
                data.antialiasing         = DefaultAntialiasing;
                data.antialiasingQuality  = AntialiasingQuality.High;
                data.dithering            = true;   // 밴딩 — HDR 그레이딩으로 넘어가면 더 눈에 띈다
                cam.allowHDR              = true;
                touched++;
                report?.AppendLine($"  카메라 `{cam.name}` — post ON · AA {DefaultAntialiasing} · dithering ON · HDR {cam.allowHDR}");
            }
            if (touched == 0) report?.AppendLine("  ⚠ Base 카메라를 찾지 못했다");
        }

        /// <summary>
        /// `PC_RPAsset` 의 `m_ColorGradingMode` 를 HighDynamicRange 로.
        ///
        /// LowDynamicRange 면 그레이딩 LUT 가 0..1 로 잘려 **Tonemapping 이 할 일이 없다** —
        /// 1 을 넘는 발광이 LUT 앞에서 이미 클램프되므로 Bloom 임계 1.05 도 성립하지 않는다.
        /// 대가: LUT 가 log 인코딩으로 바뀌어 비용이 조금 늘고 어두운 쪽 밴딩 거동이 달라진다.
        /// 그래서 dithering 을 함께 켠다.
        /// </summary>
        private static void EnsureHdrGrading(StringBuilder report)
        {
            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null) { report?.AppendLine("  ⚠ URP 에셋을 찾지 못했다"); return; }

            var so = new SerializedObject(rp);
            SerializedProperty p = so.FindProperty("m_ColorGradingMode");
            if (p == null) { report?.AppendLine("  ⚠ m_ColorGradingMode 프로퍼티 없음"); return; }

            int before = p.intValue;
            p.intValue = (int)ColorGradingMode.HighDynamicRange;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
            report?.AppendLine($"  {rp.name}.colorGradingMode {(ColorGradingMode)before} → HighDynamicRange");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  조명 — 그림자 예산이 통째로 낭비되고 있었다
        //
        //  `PC_RPAsset` 은 4캐스케이드 · 2048 shadowmap · High Soft Shadow 를 전부 켜 두고
        //  있는데, 씬의 `Directional Light` 는 `m_Shadows.m_Type: 0` — **그림자를 안 만든다.**
        //  실제로 그림자를 드리우던 것은 range 7 짜리 `CabinLight` 포인트 라이트 하나뿐이다.
        //
        //  이것이 두 축을 동시에 막고 있었다.
        //   · G-2 — 가려진 곳이 어두워지지 않으니 휘도 5퍼센타일이 바닥에서 뜬다
        //   · G-4 — 셰이더 레인의 실측: 주광 그림자가 꺼져 있어 `atten = pow(1, 2.5) = 1`,
        //           그래서 `_FalloffPower` 가 **주광에 대해 죽은 손잡이**였다
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 주광 세기. 실내라서 방향광이 벽을 균일하게 때리면 위험 연출이 죽는다 —
        /// 주역은 실용 광원(천장등·계기 발광·통관)이고 방향광은 형태를 세우는 보조다.
        /// </summary>
        public const float SunIntensity = 0.55f;
        public const float SunShadowStrength = 0.85f;

        /// <summary>
        /// 씬 앰비언트 배율. **줄이는 것이 목적이지 0 이 목적이 아니다.**
        ///
        /// ⚠ 이 값은 위험 연출과 직접 얽혀 있다. `RiskStateView.ApplyAmbient` 가
        /// 씬의 `RenderSettings.ambientLight` 를 `_originalAmbient` 로 붙잡고
        /// `RiskAmbientLadder.CeilingFor(baseAmbient, ...)` 로 4단계 사다리의 **천장**을
        /// 계산한다. 너무 낮추면 `BandIsSufficient` 가 깨져 단계 간격이 하한에 눌리고,
        /// 그러면 **좌벽 ΔL 회귀 감시선(≥ 15)이 무너진다.**
        /// 그래서 절반 조금 아래까지만 내리고 캡처로 확인한다.
        /// </summary>
        public const float AmbientScale = 0.55f;

        /// <summary>앰비언트 원본. 배율이 아니라 **절대값**으로 둔다 — 두 번 돌려도 같아야 한다.</summary>
        public static readonly Color AmbientBase = new Color(0.260f, 0.270f, 0.310f, 1f);

        [MenuItem("Ascend/Graphics/Build Lighting")]
        public static void BuildLighting()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            Scene scene = EnsureScene();
            if (!scene.IsValid()) return;

            var report = new StringBuilder("[상승] 조명 재설계\n");

            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                Undo.RecordObject(l, "Build lighting");
                LightShadows beforeShadows = l.shadows;
                float beforeIntensity = l.intensity;

                // **절대값으로 준다.** 「지금 값의 절반」 같은 델타를 쓰면 두 번 돌릴 때
                // 계속 어두워진다 — `Reproportion Elevator Car` 가 그렇게 계기판을 밀었다.
                l.shadows        = LightShadows.Soft;
                l.shadowStrength = SunShadowStrength;
                l.intensity      = SunIntensity;
                EditorUtility.SetDirty(l);

                report.AppendLine($"  {l.gameObject.name} — shadows {beforeShadows} → {l.shadows} " +
                                  $"(strength {l.shadowStrength:F2}) · intensity {beforeIntensity:F2} → {l.intensity:F2}");
            }

            Color ambient = AmbientBase * AmbientScale;
            ambient.a = 1f;
            report.AppendLine($"  앰비언트 {RenderSettings.ambientLight.ToString("F3")} → {ambient.ToString("F3")} " +
                              $"(원본 {AmbientBase.ToString("F3")} × {AmbientScale:F2}) · 모드 {RenderSettings.ambientMode}");
            RenderSettings.ambientLight = ambient;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("  ⚠ 위험 4단계 좌벽 ΔL 은 **캡처로 확인해야 한다** — " +
                              "앰비언트는 `RiskAmbientLadder` 의 천장이라 여기를 내리면 단계 간격이 함께 줄어든다.");
            Debug.Log(report.ToString());
        }

        // ── AA A/B ──────────────────────────────────────────────────────────

        [MenuItem("Ascend/Graphics/Antialiasing - None")]
        public static void AaNone() => SetCameraAntialiasing(AntialiasingMode.None);

        [MenuItem("Ascend/Graphics/Antialiasing - SMAA")]
        public static void AaSmaa() => SetCameraAntialiasing(AntialiasingMode.SubpixelMorphologicalAntiAliasing);

        public static void SetCameraAntialiasing(AntialiasingMode mode)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }
            Scene scene = EnsureScene();
            if (!scene.IsValid()) return;

            int n = 0;
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null || data.renderType != CameraRenderType.Base) continue;
                data.antialiasing = mode;
                data.antialiasingQuality = AntialiasingQuality.High;
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[상승] 카메라 {n}대 안티에일리어싱 → {mode}");
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────

        internal static Scene EnsureScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path == ScenePath) return scene;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// 오버라이드를 가져오거나 만든다. **만들면 반드시 프로파일의 서브에셋으로 붙인다.**
        ///
        /// 안 붙이면 조용히 사라진다. `VolumeProfile.Add&lt;T&gt;()` 는 메모리에
        /// `ScriptableObject` 를 만들어 리스트에 넣기만 하고 **에셋 파일에 쓰지 않는다.**
        /// 그 상태로 저장하면 프로파일에는 "어딘가에 있는 오브젝트"를 가리키는 참조 7개가
        /// 적히고, 다음 리임포트에서 그 대상이 없으므로 전부 null 이 되어 리스트가 비워진다.
        ///
        /// 실제로 그렇게 됐다 — 캡처 두 번은 정상으로 나왔는데(메모리에 살아 있었다)
        /// 그 뒤 에셋을 다시 읽으니 `components.Count == 0` 이었다. 포스트 체인 전체가
        /// 리임포트 한 번에 사라지는 상태였고, 그 사이의 캡처는 "되는 것처럼 보이는" 증거였다.
        /// </summary>
        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing) && existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            created.name = typeof(T).Name;
            created.hideFlags = HideFlags.HideInHierarchy;   // 프로젝트 창에서 접히게 둔다
            profile.components.Add(created);

            if (AssetDatabase.Contains(profile))
                AssetDatabase.AddObjectToAsset(created, profile);

            return created;
        }

        /// <summary>
        /// 참조가 끊긴 오버라이드를 걷어낸다. 위의 실패로 이미 저장된 프로파일에는 null 이
        /// 남아 있을 수 있고, null 이 섞인 리스트는 `TryGet` 에서 예외를 던진다.
        /// </summary>
        private static int PruneNulls(VolumeProfile profile)
        {
            int removed = 0;
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                if (profile.components[i] == null) { profile.components.RemoveAt(i); removed++; }
            }
            return removed;
        }

        private static int Remove<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T found) || found == null) return 0;
            profile.components.Remove(found);
            // 서브에셋도 같이 지운다 — 리스트에서만 빼면 파일 안에 유령이 남는다.
            if (AssetDatabase.Contains(found)) Object.DestroyImmediate(found, true);
            return 1;
        }

        /// <summary>절대값으로 쓴다. `overrideState` 를 반드시 켠다 — 안 켜면 조용히 무시된다.</summary>
        private static void Set<T>(VolumeParameter<T> p, T value)
        {
            p.overrideState = true;
            p.value = value;
        }
    }
}
