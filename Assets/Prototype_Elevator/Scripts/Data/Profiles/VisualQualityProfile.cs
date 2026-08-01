using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>품질 단계. 낮을수록 싸다. 승인 비교가 아니라 성능 대응용 다이얼이다.</summary>
    public enum VisualQualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    /// <summary>
    /// 성능에 직접 값을 매기는 렌더 다이얼. `MASTER_PRD.md` §13.2의 "지정 하드웨어 기준
    /// 성능 조건"을 지키려면 무엇을 줄일 수 있는지가 한 곳에 있어야 한다 — `UP-PLAT-05`.
    ///
    /// 지금은 광원 수·그림자 거리·파티클 상한·포스트프로세싱이 씬과 코드에 흩어져 있어서,
    /// 프레임이 모자랄 때 **무엇을 줄였는지 기록할 수가 없다.** 기록이 없으면 다음 측정이
    /// 직전 측정과 비교되지 않고, 그러면 `MASTER_PRD.md` §11의 「직전 승인 빌드보다
    /// 나빠지지 않았는가」를 판정할 근거가 사라진다.
    ///
    /// 여기 값은 **예산**이지 강제가 아니다. 강제는 URP 에셋과 씬이 한다.
    /// 이 프로파일은 "얼마까지 허용하기로 했는가"를 적어 두고, 감사가 실측을 대조하게 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualQualityProfile",
                     menuName = "Ascend/Profiles/Visual Quality", order = 103)]
    public sealed class VisualQualityProfile : ScriptableObject
    {
        [Tooltip("이 프로파일이 어느 단계를 뜻하는가. 리포트에 인용된다.")]
        [SerializeField] private VisualQualityTier _tier = VisualQualityTier.High;

        [Header("조명")]
        [Tooltip("한 프레임에 허용하는 실시간 광원 수. 엘리베이터 내부는 좁아서 광원이 겹치면 비용이 곱으로 붙는다.")]
        [Range(0, 16)] [SerializeField] private int _maxRealtimeLights = 8;

        [Tooltip("그림자 렌더 거리(미터). 실내가 무대이므로 멀리 볼 이유가 없다.")]
        [Range(0f, 150f)] [SerializeField] private float _shadowDistance = 30f;

        [Header("파티클")]
        [Tooltip("동시에 살아 있을 수 있는 파티클 총수. 캐스케이드가 깊어질수록 여기서 터진다.")]
        [Range(0, 20000)] [SerializeField] private int _maxSimultaneousParticles = 2000;

        [Header("래스터")]
        [Tooltip("화면 픽셀당 허용 평균 오버드로우 배수. 반투명 연기·유리가 이 값을 먹는다.")]
        [Range(1f, 8f)] [SerializeField] private float _overdrawBudget = 3.0f;

        [Tooltip("포스트프로세싱 사용 여부. 끄면 분위기를 크게 잃으므로 마지막에 끈다.")]
        [SerializeField] private bool _postProcessing = true;

        [Tooltip("URP 렌더 스케일. 1 미만이면 내부 해상도를 낮춰 그린다 — 결과판 심볼 판독성이 먼저 상한다(PRD §13.4).")]
        [Range(0.5f, 2f)] [SerializeField] private float _renderScale = 1.0f;

        public VisualQualityTier Tier => _tier;
        public int MaxRealtimeLights => _maxRealtimeLights;
        public float ShadowDistance => _shadowDistance;
        public int MaxSimultaneousParticles => _maxSimultaneousParticles;
        public float OverdrawBudget => _overdrawBudget;
        public bool PostProcessing => _postProcessing;
        public float RenderScale => _renderScale;

        /// <summary>
        /// 기본은 High다 — 기준 하드웨어가 RTX 3070(`PD-10`)이고, 프로토타입 단계에서
        /// 먼저 확인해야 하는 것은 "가장 좋은 그림이 목표 프레임에 드는가"이기 때문이다.
        /// Low부터 시작하면 무엇을 포기했는지 모른 채 통과한다.
        /// </summary>
        public void Reset()
        {
            ApplyTier(VisualQualityTier.High);
        }

        public void ApplyTier(VisualQualityTier tier)
        {
            VisualQualitySnapshot preset = PresetFor(tier);
            _tier = preset.Tier;
            _maxRealtimeLights = preset.MaxRealtimeLights;
            _shadowDistance = preset.ShadowDistance;
            _maxSimultaneousParticles = preset.MaxSimultaneousParticles;
            _overdrawBudget = preset.OverdrawBudget;
            _postProcessing = preset.PostProcessing;
            _renderScale = preset.RenderScale;
        }

        public VisualQualitySnapshot Snapshot()
        {
            return new VisualQualitySnapshot(_tier, _maxRealtimeLights, _shadowDistance,
                _maxSimultaneousParticles, _overdrawBudget, _postProcessing, _renderScale);
        }

        public static VisualQualitySnapshot DefaultSnapshot
        {
            get { return PresetFor(VisualQualityTier.High); }
        }

        /// <summary>
        /// 단계별 프리셋. 세 단계가 **모든 축에서 단조**여야 한다 — 한 축이라도 역전되면
        /// "Low로 내렸는데 더 느려졌다"가 생기고, 그 순간 이 프로파일은 진단 도구가 아니라
        /// 또 하나의 변수로 바뀐다. 테스트가 단조성을 검사한다.
        /// </summary>
        public static VisualQualitySnapshot PresetFor(VisualQualityTier tier)
        {
            switch (tier)
            {
                case VisualQualityTier.Low:
                    return new VisualQualitySnapshot(VisualQualityTier.Low, 4, 12f, 600, 2.0f, false, 0.85f);
                case VisualQualityTier.Medium:
                    return new VisualQualitySnapshot(VisualQualityTier.Medium, 6, 20f, 1200, 2.5f, true, 1.0f);
                default:
                    return new VisualQualitySnapshot(VisualQualityTier.High, 8, 30f, 2000, 3.0f, true, 1.0f);
            }
        }

        public static VisualQualitySnapshot SnapshotOrDefault(VisualQualityProfile profile, string caller)
        {
            if (profile != null) return profile.Snapshot();
            Debug.LogWarning($"[상승] VisualQualityProfile 이 배선되지 않았다 ({caller}). High 프리셋으로 진행한다.");
            return DefaultSnapshot;
        }
    }

    /// <summary>렌더 예산의 값 사본.</summary>
    public readonly struct VisualQualitySnapshot
    {
        public readonly VisualQualityTier Tier;
        public readonly int MaxRealtimeLights;
        public readonly float ShadowDistance;
        public readonly int MaxSimultaneousParticles;
        public readonly float OverdrawBudget;
        public readonly bool PostProcessing;
        public readonly float RenderScale;

        public VisualQualitySnapshot(VisualQualityTier tier, int maxRealtimeLights, float shadowDistance,
            int maxSimultaneousParticles, float overdrawBudget, bool postProcessing, float renderScale)
        {
            Tier = tier;
            MaxRealtimeLights = maxRealtimeLights;
            ShadowDistance = shadowDistance;
            MaxSimultaneousParticles = maxSimultaneousParticles;
            OverdrawBudget = overdrawBudget;
            PostProcessing = postProcessing;
            RenderScale = renderScale;
        }

        /// <summary>실측이 예산 안에 드는가. 감사가 이 하나로 판정할 수 있어야 한다.</summary>
        public bool WithinBudget(int realtimeLights, int particles, float measuredOverdraw)
        {
            return realtimeLights <= MaxRealtimeLights
                && particles <= MaxSimultaneousParticles
                && measuredOverdraw <= OverdrawBudget;
        }

        public string Describe()
        {
            return $"품질 {Tier} · 광원 ≤{MaxRealtimeLights} · 그림자 {ShadowDistance:0}m · " +
                   $"파티클 ≤{MaxSimultaneousParticles} · 오버드로우 ≤{OverdrawBudget:0.0}× · " +
                   $"포스트 {(PostProcessing ? "켬" : "끔")} · 렌더 스케일 {RenderScale:0.00}";
        }
    }
}

// 씬 배선 필요:
//   1. `Assets/Settings/` 의 URP 에셋(렌더 스케일·그림자 거리)이 이 프로파일과 일치해야 한다.
//      URP 에셋은 단일 소유 영역이라 여기서 고치지 않았다.
//   2. 성능 프로브가 `Describe()` 를 리포트에 찍고 `WithinBudget(...)` 으로 판정해야
//      `UP-PLAT-05` 의 「빌드 리포트 기록」이 성립한다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/VisualQualityProfile.asset (Low/Medium/High 3개 권장)
