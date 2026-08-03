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
        //
        // 🔴 2026-08-04 6라운드 — 0.30 → 0.70. **스윕 표에서 고른 값이지 새 목표가 아니다.**
        //
        // 5라운드가 색조(g/r·b/r)를 `VISUAL_SPEC` §12 대역 안으로 넣었지만, 밝기 축 셋이
        // 대역 밖에 남아 있었다. 그 라운드에서 이 상수만 스윕해 재 둔 표가 근거다
        // (뷰 A/C/D 평균, post ON. 다른 상수는 하나도 건드리지 않았다):
        //
        //   exp    g/r      b/r      mean            p50       <0.02    레버 A/D
        //   0.30   0.840 ✅ 0.513 ✅ .0328 ✗(-40%)   .0186 ✗   53.0% ✗  6.01× / 5.32×
        //   0.70   0.868 ✅ 0.603 ✅ .0535 (-2.7%)    .0418 ✅  17.8% ✅ 4.37× / 4.05×
        //   0.95   0.856 ✅ 0.627 ✗ .0686 ✅          .0591 ✅  13.1% ✅ 3.77× / 3.56×
        //
        // 0.70 을 고른 이유는 **`<0.02` 가 53% → 17.8%** 이기 때문이다. 화면 절반이 완전
        // 검정인 상태는 §12 상한 32% 를 크게 벗어나고, 그건 "어두운 분위기"가 아니라
        // **화소가 없는 것**이다. mean 만 2.7% 모자라고 나머지 5개 지표는 대역 안이다.
        // 0.95 는 b/r 이 대역(0.45~0.62)을 벗어나고 레버 대비를 36% 태운다 — 5라운드가
        // 힘들게 되찾은 색조 축을 다시 내주는 거래라 쓰지 않는다.
        //
        // ⚠ 값은 **에셋이 아니라 이 상수**에 있어야 한다. 프로파일에만 손으로 넣으면
        //    다음 `Build Post Chain` 이 조용히 0.30 으로 되돌린다 (이 파일 규칙 2).
        public const float PostExposure = 0.70f;
        public const float Contrast     = 22f;
        public const float Saturation   = -12f;

        // Chromatic Aberration — `VISUAL_SPEC` §8 "지속적 색수차는 피한다".
        //
        // 🔴 2026-08-04 7라운드 — 0.10 → **0.00**. 되돌리지 않는다.
        //
        // G-6 상한(0.15)의 2/3 라 "규격 안"이었지만, `visual-criteria` **금지 #17
        // (지속적 색수차)** 은 상한이 아니라 **금지**다. 독립 평가(`visual-critic`,
        // cabin_v5/v6)가 REJECT 하며 최우선으로 지적한 것이 이 항목이다 —
        // B 우상단 「실행」 표찰이 파랑 획과 붉은 획으로 갈라져 **글자로 읽히지 않고**,
        // C 층수 표시판·D 가위문 슬랫에도 적·청 이중선이 남았다. 같은 폴더 `postOFF/`
        // 대조로 post 효과임이 확정됐고 기준선에는 없던 **신규 결함**이다.
        //
        // 훼손 대상이 하필 `VISUAL_SPEC` §5 상호작용 우선순위 **1위**인 레버 표찰
        // 두 개(「실행」·「과수확」)라, §8 의 나머지 절반 —
        // 「로우파이 스타일을 명분으로 결과 판독성을 희생하지 않는다」 — 에도 걸린다.
        //
        // 평가자 §E-1: **「이걸 안 끄면 이후 모든 명도 조정이 같은 위반 위에 쌓인다.」**
        // 그래서 값을 낮추는 게 아니라 0 이다. 0.03 도 표찰 획 폭(2~3px)에서는
        // 분리가 보인다 — 이 화면에서 CA 는 판독성과 교환 관계이지 강도 조절 대상이 아니다.
        //
        // ⚠ 다시 켜려면 **여기서** 켠다. 프로파일 에셋에만 넣으면 다음
        //   `Build Post Chain` 이 조용히 0 으로 되돌린다 (이 파일 규칙 2).
        //   그리고 켜기 전에 위 REJECT 를 뒤집을 근거가 있어야 한다.
        public const float ChromaticAberration = 0.00f;

        /// 🔴 2026-08-04 실측 — **이 한 줄이 방 전체를 초록으로 물들이고 있었다.**
        ///
        /// 옛 값 (0.92, 1.00, 0.96) 은 "그림자를 회녹색으로"라는 의도였지만 세 가지가 겹쳤다.
        ///  ① `PrepareShadowsMidtonesHighlights` 가 x/y/z 를 `GammaToLinearSpace` 로 통과시킨다.
        ///     0.92 → 0.828, 0.96 → 0.911 이라 **선형 배수는 (0.828, 1.000, 0.911)** —
        ///     감마 값이 시사하는 8% 가 아니라 실제로는 **g/r ×1.208** 이다.
        ///  ② `shadowsEnd` 가 기본 0.3 인데 이 방은 거의 전체가 휘도 0.3 미만이다.
        ///     즉 "그림자 밴드"가 아니라 **화면 전체**에 걸린다.
        ///  ③ 대비 22 가 그 위에서 한 번 더 벌린다.
        ///
        /// 결과: 사용자 레퍼런스가 g/r 0.829 (빨강 우세)인데 화면은 1.13~1.46 (초록 우세)였다.
        /// 기준선(작업 전 0.901)보다도 **멀어진** 유일한 축이고, 재는 축이 없어서 조용히 나빠졌다.
        ///
        /// ## 새 값을 어떻게 정했나 — 지어내지 않고 측정해서 풀었다
        ///
        /// 뷰 A/C/D · post ON 에서 레버 하나씩 단독으로 재고 로그 공간에서 연립했다.
        ///   L1 SMH shadows (0.92,1.00,0.96)→(1.00,0.92,0.88): g/r ×0.788  b/r ×0.794
        ///   L2 WhiteBalance temperature +30                 : g/r ×0.803  b/r ×0.582
        ///   L3 colorFilter (0.97,0.98,1.00)→(1.00,0.96,0.90): g/r ×0.934  b/r ×0.836
        ///   L4 WhiteBalance tint −30                        : g/r ×1.356 (**역방향** — 쓰지 않는다)
        /// 필요한 보정 g/r ×0.625 · b/r ×0.593 을 L1·L2 로 풀면 L1 강도 1.78 · L2 +6.1 이 나온다.
        /// L1 을 1.78 배로 늘리되 **휘도는 보존**(선형 (1.381, 0.850, 0.765), 휘도 배수 0.957 =
        /// 옛 값과 동일)하도록 정규화한 것이 아래 (1.158, 0.931, 0.888) 이다.
        ///
        /// 검증 실측 (뷰 A/C/D 평균, post ON): **g/r 0.840 · b/r 0.513**
        /// (레퍼런스 0.829 / 0.524, 허용 대역 0.78~0.90 / 0.45~0.62 — 둘 다 안쪽).
        /// 레버 대비는 오히려 올랐다 — A 5.88× → 6.05×, D 5.24× → 5.34×.
        ///
        /// ⚠ 값은 **감마 입력**이다. 선형 배수를 원하면 `GammaToLinearSpace` 를 먼저 통과시켜
        ///   계산하고 그 역함수로 돌려서 여기 적는다. 이 단위 착오가 원래 문제의 ①이었다.
        public static readonly Vector4 ShadowTint    = new Vector4(1.158f, 0.931f, 0.888f, 0f);
        public static readonly Vector4 MidtoneTint   = new Vector4(1.00f, 1.00f, 1.00f, 0f);
        public static readonly Vector4 HighlightTint = new Vector4(1.03f, 0.99f, 0.94f, 0f);

        /// <summary>
        /// 화이트 밸런스. 위 <see cref="ShadowTint"/> 주석의 L2 레버다.
        ///
        /// `ColorBalanceToLMSCoeffs` 는 LMS(von Kries) 공간에서 도는 **정통 색온도 보정**이고,
        /// LUT 체인에서 대비보다 **앞**에 있어 대비 22 가 그 효과를 한 번 더 벌려 준다.
        /// 그래서 파랑을 내리는 데는 `colorFilter` 보다 효율이 높다 (실측 b/r 감쇠 2.6배).
        ///
        /// tint 는 **0 으로 둔다.** 음수 tint 가 마젠타일 거라 예상하고 −30 을 재 봤더니
        /// g/r 이 1.327 → 1.799 로 **반대로 갔다.** 초록을 빼는 레버가 아니다.
        /// </summary>
        public const float WhiteBalanceTemperature = 6f;
        public const float WhiteBalanceTint        = 0f;

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

            var wb = GetOrAdd<WhiteBalance>(profile);
            Set(wb.temperature, WhiteBalanceTemperature);
            Set(wb.tint,        WhiteBalanceTint);

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
                               $"ColorAdjustments(exp {PostExposure} cont {Contrast} sat {Saturation}) " +
                               $"SMH(shadows {ShadowTint.x}/{ShadowTint.y}/{ShadowTint.z}) " +
                               $"WhiteBalance(temp {WhiteBalanceTemperature} tint {WhiteBalanceTint}) " +
                               $"ChromaticAberration({ChromaticAberration})");
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
        // ── `VISUAL_REFERENCE.md` §2 로 재구성 ────────────────────────────────
        //
        //  「주광이 **하나**다 — 철망 씌운 백열등. 따뜻한 호박색, 원뿔형 낙하, **빠른 감쇠**.
        //   나머지는 **거의 검정.** 구석·바닥 가장자리·좌벽 하단이 완전히 죽어 있다.」
        //
        //  지금 씬은 그 반대였다 — 앰비언트가 전체를 고르게 들어 올리고 방향광이
        //  실내를 균일하게 때렸다. 레퍼런스가 요구하는 것은 **광원 하나와 어둠**이다.
        //
        //  0.55 → 0.18. 실내에는 해가 없다. 방향광은 형태를 세우는 최소한으로만 남긴다 —
        //  **끄지는 않는다.** 주광 그림자가 꺼지면 `atten = pow(1,2.5) = 1` 이 되어
        //  셰이더의 `_FalloffPower` 가 다시 죽은 손잡이가 되고 G-4 계단이 사라진다.
        public const float SunIntensity = 0.18f;
        public const float SunShadowStrength = 0.85f;

        /// <summary>
        /// 캐빈 주광의 사거리. 7.0 → 4.6.
        ///
        /// **이것이 「빠른 감쇠」의 실체다.** 사거리가 7 이면 4.80 × 6.00 × 5.50 캐빈의
        /// 구석까지 빛이 닿아 레퍼런스가 요구하는 「구석이 완전히 검다」가 성립하지 않는다.
        /// 4.6 이면 등 아래 원뿔이 서고 벽 하단과 모서리가 죽는다.
        ///
        /// ⚠ `RiskStateView` 는 `_cabinLight` 의 **intensity 와 color 만** 매 프레임 덮는다
        /// (`RiskStateView.cs:439-441`). **range 는 건드리지 않으므로** 여기서 준 값이 산다.
        /// 세기를 여기서 바꾸는 것은 의미가 없다 — 런타임에 `1.6 × 프로파일` 로 덮인다.
        /// </summary>
        public const float CabinLightRange = 4.6f;

        /// <summary>
        /// 천장등 **발광**. 이것이 G-2 p95 의 잠긴 문이었다.
        ///
        /// 실측: `CeilingLamp` 는 y=3.14 에 실물로 있는데 `_EmissionColor` 가 **검정**이었다.
        /// 등이 프레임 안에 있어도 **빛나지 않으니** 밝은 화소가 생기지 않는다 —
        /// 「위험 프레임(p95 119/106/134/150)에 밝은 광원이 화각 안에 없다」는 진단의
        /// 물리적 원인이 이것이다. 포스트로는 없는 것을 밝게 만들 수 없다.
        ///
        /// 색은 호박색이다 — 레퍼런스 §2 「따뜻한 호박색」이고 `PD-22`(따뜻한 천장등 유지)와
        /// 같은 방향이다. 탈색하지 않는다.
        ///
        /// 세기 3.2 는 Bloom 임계 0.80 을 넉넉히 넘겨 번지게 하려는 값이다.
        /// </summary>
        public static readonly Color LampEmission = new Color(1.00f, 0.72f, 0.38f, 1f);
        public const float LampEmissionIntensity = 3.2f;

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
        /// 0.55 → **0.20.** 레퍼런스 §2 「나머지는 거의 검정」이 이 값이 하는 일이다.
        /// 앰비언트는 방향과 무관하게 모든 면을 똑같이 들어 올리므로 「제한적인 국소 조명」의
        /// 정확한 반대다. 이걸 내리지 않으면 등을 아무리 세게 해도 대비가 안 선다.
        ///
        /// ⚠ 위험 사다리의 **천장**이 이 값이라 단계 간격도 함께 줄어든다. 지금 좌벽 ΔL 이
        /// **42.71** 로 기준(15)의 2.8배라 내릴 여유가 있다고 판단했다 — 그래도 캡처로
        /// 확인해야 한다. 여유가 있다는 것은 예측이지 측정이 아니다.
        public const float AmbientScale = 0.20f;

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

            // ── 주광 하나: 사거리를 좁혀 원뿔을 만든다 ──
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point) continue;
                if (l.gameObject.name != "CabinLight") continue;
                Undo.RecordObject(l, "Build lighting");
                float beforeRange = l.range;
                l.range = CabinLightRange;
                l.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(l);
                report.AppendLine($"  CabinLight — range {beforeRange:F2} → {l.range:F2} (빠른 감쇠) · shadows {l.shadows}" +
                                  "  [세기·색은 RiskStateView 가 런타임에 덮으므로 여기서 안 건드린다]");
            }

            // ── 등을 **실제로 빛나게** 한다 ──
            // 등이 프레임 안에 있는데 발광이 검정이면 밝은 화소는 생기지 않는다.
            //
            // ⚠ **남의 머티리얼에 쓰지 않는다.** 첫 판본이 정확히 그 사고를 냈다 —
            // `CeilingLamp` 가 물고 있던 재질은 이름이 `Lit` 인 씬 재질이 아니라
            // `Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat`,
            // 즉 **URP 패키지의 불변 기본 머티리얼**이었다. 거기에 발광을 써 넣자
            // Unity 가 「immutable package 가 변경됐다」고 경고했고, 그대로 뒀으면
            // 그 기본 재질을 쓰는 **모든 오브젝트가 호박색으로 빛나고** 패키지 작업 중
            // 조용히 사라졌을 것이다. 통관 유리에는 이 방어를 넣어 두고 여기엔 빠뜨렸다.
            int lamps = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.gameObject.name != "CeilingLamp") continue;
                Material m = r.sharedMaterial;
                if (m == null || !m.HasProperty("_EmissionColor")) continue;

                string path = AssetDatabase.GetAssetPath(m);
                bool immutable = !string.IsNullOrEmpty(path) && path.StartsWith("Packages/");
                bool sharedElsewhere = false;
                foreach (Renderer other in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (other == r) continue;
                    foreach (Material om in other.sharedMaterials) if (om == m) sharedElsewhere = true;
                }

                if (immutable || sharedElsewhere)
                {
                    m = new Material(m) { name = "CeilingLampGlow" };
                    r.sharedMaterial = m;
                    report.AppendLine($"  ⚠ 원래 재질이 {(immutable ? "**패키지 불변 에셋**" : "다른 렌더러와 공유")} " +
                                      $"(`{path}`) — 씬 전용 사본 `CeilingLampGlow` 를 만들어 그쪽에 쓴다");
                }

                Undo.RecordObject(m, "Build lighting");
                Color before = m.GetColor("_EmissionColor");
                // **절대값이다.** 현재 값에 곱하면 두 번 돌릴 때마다 밝아진다.
                // 알파는 곱하지 않는다 — 색 × 세기에 알파까지 실리면 3.2 가 된다.
                Color glow = LampEmission * LampEmissionIntensity;
                glow.a = 1f;
                m.SetColor("_EmissionColor", glow);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(m);
                lamps++;
                report.AppendLine($"  CeilingLamp 발광 {before.ToString("F2")} → {glow.ToString("F2")} " +
                                  $"(재질 `{m.name}`, y={r.bounds.center.y:F2}) — G-2 p95 의 프레임 안 광원");
            }
            if (lamps == 0) report.AppendLine("  ⚠ CeilingLamp 렌더러를 찾지 못했다 — 프레임 안 광원이 없다");

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
