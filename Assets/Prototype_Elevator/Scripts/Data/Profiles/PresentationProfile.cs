using UnityEngine;
using Ascend.Prototype.Risk;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// PRD §14.1 「반드시 데이터화할 항목」 12종 중 **아직 어느 에셋에도 없던 세 줄**을 담는다 —
    /// ⑩ 파티클 밀도와 연출 속도 · ⑪ 애니메이션 속도와 정지 구간 · ⑫ 재질 색·발광·거칠기·오염 강도
    /// (`UP-TECH-09`).
    ///
    /// **왜 별도 에셋인가**: <see cref="VisualQualityProfile"/> 은 Low→Medium→High 가 모든 축에서
    /// 단조여야 하는 **성능 다이얼**이고 테스트가 그 단조성을 검사한다. 연출 속도와 재질 색은
    /// 성능 축이 아니라 **연출 축**이라 단조일 이유가 없다 — 같은 에셋에 넣으면 그 테스트가
    /// 뜻을 잃거나, 뜻을 지키려고 연출 값이 성능 등급에 끌려다닌다.
    ///
    /// **지금은 아무도 읽지 않는다.** 값이 전부 현재 코드 상수와 같으므로 꽂아도 동작이 바뀌지
    /// 않고, 꽂지 않아도 바뀌지 않는다. 이것을 「데이터화 완료」로 적지 않는다 —
    /// 파일 끝의 「소비처 배선 필요」 목록이 남은 절반이다.
    /// </summary>
    [CreateAssetMenu(fileName = "PresentationProfile",
                     menuName = "Ascend/Profiles/Presentation", order = 108)]
    public sealed class PresentationProfile : ScriptableObject
    {
        /// <summary><see cref="RiskLevel"/> 단계 수. 배열 길이가 다르면 코드 기본값으로 폴백한다.</summary>
        public const int LevelCount = 4;

        // ── ⑩ 파티클 밀도 ────────────────────────────────────────────────────
        // 출처: `Scripts/Effects/AmbientParticleDirector.MaxParticlesFor`.
        // 그쪽이 아직 원본이며 이 값은 사본이다. 테스트가 두 값의 일치를 검사하므로
        // 한쪽만 고치면 EditMode 가 빨간불이 된다 — 사본이 조용히 낡는 것을 막는 유일한 장치다.

        public const int DefaultParticlesStable = 24;
        public const int DefaultParticlesStrain = 48;
        public const int DefaultParticlesCritical = 80;
        public const int DefaultParticlesCollapse = 120;

        [Header("⑩ 파티클 밀도 (단계별 동시 상한, PRD §12.5)")]
        [Tooltip("Stable / Strain / Critical / Collapse 순서. 시스템 하나당 상한이며 합산이 아니다.")]
        [SerializeField] private int[] _maxParticles =
        {
            DefaultParticlesStable, DefaultParticlesStrain,
            DefaultParticlesCritical, DefaultParticlesCollapse,
        };

        // ── ⑩⑪ 연출 속도와 정지 구간 ─────────────────────────────────────────
        // 출처: `Scripts/View/SpinPresenter.cs` 의 [Header("템포 (초)")] 블록과
        //       `Scripts/View/OverharvestUnlockEffect.cs` 의 [Header("타이밍 (초)")] 블록.
        // 그쪽은 MonoBehaviour 의 [SerializeField] 라 **씬에 굳어 있다** — 씬을 열지 않으면
        // 값을 볼 수도 바꿀 수도 없고, 프리셋 비교(PRD Phase 6)를 만들 수 없다.

        public const float DefaultColumnRevealInterval = 0.32f;
        public const float DefaultReadPause = 0.45f;
        public const float DefaultPurifyPulse = 0.55f;
        public const float DefaultEmptyHold = 0.30f;
        public const float DefaultRefillHold = 0.40f;
        public const float DefaultChainSpeedup = 0.86f;
        public const float DefaultMinTempoScale = 0.35f;
        public const float DefaultUnlockFlashSeconds = 0.18f;
        public const float DefaultUnlockSettleSeconds = 0.9f;

        [Header("⑪ 연출 속도 (초)")]
        [Tooltip("통관 한 열이 결과를 드러내는 간격.")]
        [SerializeField, Min(0f)] private float _columnRevealInterval = DefaultColumnRevealInterval;

        [Tooltip("정화 칸이 맥동하는 시간.")]
        [SerializeField, Min(0f)] private float _purifyPulse = DefaultPurifyPulse;

        [Tooltip("잠금 해제 섬광 길이.")]
        [SerializeField, Min(0f)] private float _unlockFlashSeconds = DefaultUnlockFlashSeconds;

        [Tooltip("잠금 해제 뒤 가라앉는 시간.")]
        [SerializeField, Min(0f)] private float _unlockSettleSeconds = DefaultUnlockSettleSeconds;

        [Header("⑪ 정지 구간 (초) — 여기를 0으로 두면 무엇이 왜 일어났는지가 사라진다")]
        [Tooltip("판을 읽을 시간. 정화가 시작되기 전 정지.")]
        [SerializeField, Min(0f)] private float _readPause = DefaultReadPause;

        [Tooltip("제거된 판(빈칸)을 보여주는 시간.")]
        [SerializeField, Min(0f)] private float _emptyHold = DefaultEmptyHold;

        [Tooltip("재충전된 판을 보여주는 시간.")]
        [SerializeField, Min(0f)] private float _refillHold = DefaultRefillHold;

        [Header("⑪ 연쇄 압축")]
        [Tooltip("연쇄가 길어질수록 이 비율로 빨라진다. 1이면 압축 없음.")]
        [SerializeField, Range(0.3f, 1f)] private float _chainSpeedup = DefaultChainSpeedup;

        [Tooltip("아무리 빨라져도 이 배율 아래로는 내려가지 않는다 — 20연쇄가 순식간에 지나가면 안 된다.")]
        [SerializeField, Range(0.05f, 1f)] private float _minTempoScale = DefaultMinTempoScale;

        // ── ⑫ 재질 ───────────────────────────────────────────────────────────
        // 출처: `Scripts/View/ElevatorGrayboxView.ColIdle`(기본 표면색),
        //       `Scripts/Build/BuildFigureView`(_Smoothness 0.06 / _Metallic 0),
        //       `Scripts/View/OverharvestUnlockEffect._stripeEmission`(3.2).
        // 「오염 강도」는 **현재 코드에 아예 없다.** 기본값 0 이 곧 지금 화면이며,
        // 0 이 아닌 값을 쓰는 소비처가 생기기 전까지 이 필드는 자리만 잡는다.

        public static readonly Color DefaultSurfaceColor = new Color(0.55f, 0.56f, 0.60f);
        public const float DefaultSurfaceSmoothness = 0.06f;
        public const float DefaultSurfaceMetallic = 0f;
        public const float DefaultEmissionIntensity = 3.2f;
        public const float DefaultGrimeAmount = 0f;

        [Header("⑫ 재질")]
        [Tooltip("환경 표면 기본색. 그레이박스 유휴색과 같게 둔다.")]
        [SerializeField] private Color _surfaceColor = new Color(0.55f, 0.56f, 0.60f);

        [Tooltip("표면 매끄러움(0~1). 거칠기는 1 - 이 값이다.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceSmoothness = DefaultSurfaceSmoothness;

        [Tooltip("금속성(0~1). 그레이박스는 0 이다 — 금속으로 두면 조명 없이 새까맣게 보인다.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceMetallic = DefaultSurfaceMetallic;

        [Tooltip("발광 세기 배율. 경고 띠·계기판이 이 값을 곱해 쓴다.")]
        [SerializeField, Min(0f)] private float _emissionIntensity = DefaultEmissionIntensity;

        [Tooltip("오염 강도(0~1). 0 이 현재 화면이다. 올리면 표면이 어두워지고 거칠어진다.")]
        [SerializeField, Range(0f, 1f)] private float _grimeAmount = DefaultGrimeAmount;

        /// <summary>인스펙터에서 갓 만든 에셋을 코드 기본값으로 채운다. Unity가 호출한다.</summary>
        public void Reset()
        {
            _maxParticles = new[]
            {
                DefaultParticlesStable, DefaultParticlesStrain,
                DefaultParticlesCritical, DefaultParticlesCollapse,
            };

            _columnRevealInterval = DefaultColumnRevealInterval;
            _purifyPulse = DefaultPurifyPulse;
            _unlockFlashSeconds = DefaultUnlockFlashSeconds;
            _unlockSettleSeconds = DefaultUnlockSettleSeconds;
            _readPause = DefaultReadPause;
            _emptyHold = DefaultEmptyHold;
            _refillHold = DefaultRefillHold;
            _chainSpeedup = DefaultChainSpeedup;
            _minTempoScale = DefaultMinTempoScale;

            _surfaceColor = DefaultSurfaceColor;
            _surfaceSmoothness = DefaultSurfaceSmoothness;
            _surfaceMetallic = DefaultSurfaceMetallic;
            _emissionIntensity = DefaultEmissionIntensity;
            _grimeAmount = DefaultGrimeAmount;
        }

        public PresentationSnapshot Snapshot()
        {
            return new PresentationSnapshot(
                ParticleArray(),
                _columnRevealInterval, _readPause, _purifyPulse, _emptyHold, _refillHold,
                _chainSpeedup, _minTempoScale, _unlockFlashSeconds, _unlockSettleSeconds,
                _surfaceColor, _surfaceSmoothness, _surfaceMetallic, _emissionIntensity, _grimeAmount);
        }

        /// <summary>배열이 비거나 길이가 어긋나면 코드 기본값을 돌려준다.</summary>
        private int[] ParticleArray()
        {
            if (_maxParticles != null && _maxParticles.Length >= LevelCount) return _maxParticles;
            return new[]
            {
                DefaultParticlesStable, DefaultParticlesStrain,
                DefaultParticlesCritical, DefaultParticlesCollapse,
            };
        }

        public static PresentationSnapshot DefaultSnapshot
        {
            get
            {
                return new PresentationSnapshot(
                    new[]
                    {
                        DefaultParticlesStable, DefaultParticlesStrain,
                        DefaultParticlesCritical, DefaultParticlesCollapse,
                    },
                    DefaultColumnRevealInterval, DefaultReadPause, DefaultPurifyPulse,
                    DefaultEmptyHold, DefaultRefillHold,
                    DefaultChainSpeedup, DefaultMinTempoScale,
                    DefaultUnlockFlashSeconds, DefaultUnlockSettleSeconds,
                    DefaultSurfaceColor, DefaultSurfaceSmoothness, DefaultSurfaceMetallic,
                    DefaultEmissionIntensity, DefaultGrimeAmount);
            }
        }

        public static PresentationSnapshot SnapshotOrDefault(PresentationProfile profile, string caller)
        {
            if (profile != null) return profile.Snapshot();
            Debug.LogWarning($"[상승] PresentationProfile 이 배선되지 않았다 ({caller}). 코드 프리셋으로 진행한다.");
            return DefaultSnapshot;
        }
    }

    /// <summary>연출 값의 사본. 연출 코드가 에셋 유무와 무관하게 같은 API를 쓴다.</summary>
    public readonly struct PresentationSnapshot
    {
        private readonly int[] _maxParticles;

        public readonly float ColumnRevealInterval;
        public readonly float ReadPause;
        public readonly float PurifyPulse;
        public readonly float EmptyHold;
        public readonly float RefillHold;
        public readonly float ChainSpeedup;
        public readonly float MinTempoScale;
        public readonly float UnlockFlashSeconds;
        public readonly float UnlockSettleSeconds;

        public readonly Color SurfaceColor;
        public readonly float SurfaceSmoothness;
        public readonly float SurfaceMetallic;
        public readonly float EmissionIntensity;
        public readonly float GrimeAmount;

        public PresentationSnapshot(int[] maxParticles,
            float columnRevealInterval, float readPause, float purifyPulse,
            float emptyHold, float refillHold, float chainSpeedup, float minTempoScale,
            float unlockFlashSeconds, float unlockSettleSeconds,
            Color surfaceColor, float surfaceSmoothness, float surfaceMetallic,
            float emissionIntensity, float grimeAmount)
        {
            _maxParticles = maxParticles;
            ColumnRevealInterval = columnRevealInterval;
            ReadPause = readPause;
            PurifyPulse = purifyPulse;
            EmptyHold = emptyHold;
            RefillHold = refillHold;
            ChainSpeedup = chainSpeedup;
            MinTempoScale = minTempoScale;
            UnlockFlashSeconds = unlockFlashSeconds;
            UnlockSettleSeconds = unlockSettleSeconds;
            SurfaceColor = surfaceColor;
            SurfaceSmoothness = surfaceSmoothness;
            SurfaceMetallic = surfaceMetallic;
            EmissionIntensity = emissionIntensity;
            GrimeAmount = grimeAmount;
        }

        /// <summary>거칠기. 셰이더가 매끄러움이 아니라 거칠기를 받을 때 쓴다.</summary>
        public float SurfaceRoughness => 1f - SurfaceSmoothness;

        public int MaxParticlesFor(RiskLevel level)
        {
            int[] values = _maxParticles;
            if (values == null || values.Length < PresentationProfile.LevelCount)
                return PresentationProfile.DefaultParticlesStable;

            int index = (int)level;
            if (index < 0) index = 0;
            if (index >= values.Length) index = values.Length - 1;
            return values[index];
        }

        /// <summary>이번 연쇄 단계의 템포 배율. 압축이 하한 아래로 내려가지 않는다.</summary>
        public float TempoScaleAtDepth(int depth)
        {
            if (depth <= 1) return 1f;
            float scale = 1f;
            for (int i = 1; i < depth; i++) scale *= ChainSpeedup;
            return scale < MinTempoScale ? MinTempoScale : scale;
        }

        public string Describe()
        {
            return $"파티클 {MaxParticlesFor(RiskLevel.Stable)}/{MaxParticlesFor(RiskLevel.Strain)}/" +
                   $"{MaxParticlesFor(RiskLevel.Critical)}/{MaxParticlesFor(RiskLevel.Collapse)} · " +
                   $"열 공개 {ColumnRevealInterval:0.00}s · 정지 {ReadPause:0.00}/{EmptyHold:0.00}/{RefillHold:0.00}s · " +
                   $"연쇄 압축 {ChainSpeedup:0.00} (하한 {MinTempoScale:0.00}) · " +
                   $"거칠기 {SurfaceRoughness:0.00} · 발광 {EmissionIntensity:0.0} · 오염 {GrimeAmount:0.00}";
        }
    }
}

// 소비처 배선 필요 — 이것이 되기 전까지 PRD §14.1 의 ⑩⑪⑫ 는 SKELETON 이다:
//   1. `Scripts/Effects/AmbientParticleDirector` 가 `MaxParticlesFor` 정적 스위치 대신
//      `PresentationProfile` 참조를 받아 `Snapshot().MaxParticlesFor(level)` 을 쓴다.
//   2. `Scripts/View/SpinPresenter` 가 [Header("템포 (초)")] 의 일곱 필드를 이 스냅샷에서 읽는다.
//      지금은 씬에 굳어 있어 프리셋 비교(PRD Phase 6)를 만들 수 없다.
//   3. `Scripts/View/OverharvestUnlockEffect` 가 섬광·정착 길이와 `_stripeEmission` 을 읽는다.
//   4. 표면 재질 네 축은 소비처가 아직 없다. `.mat` 은 단일 소유 영역이므로,
//      머티리얼을 코드로 세우는 뷰(`ElevatorGrayboxView`·`BuildFigureView`)가 읽는 것이 먼저다.
//   위 넷은 전부 다른 소유 영역이라 여기서 고치지 않았다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/PresentationProfile.asset
