using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 스핀 밸런스 수치 10종 (`UP-TECH-09` ① 심볼 가중치 · ③ 패턴 배수).
    ///
    /// ## 무엇이 여기 들어오고 무엇이 안 들어오는가
    ///
    /// 들어오는 것은 **밸런스 다이얼**이다 — 바꿔도 규칙의 뜻이 달라지지 않고 난이도만
    /// 움직이는 수. 안 들어오는 것은 **규칙 자체**다.
    ///
    /// 그래서 `MaxCascadeDepth = 20` 은 여기 없다. `MASTER_PRD.md` §6 과
    /// `TECH_SPEC.md` §9 가 못박은 값이고, 낮추면 「연쇄가 20까지 간다」는 명세를 검증할
    /// 수 없다. 다이얼이 아니라 계약이다. `RequireAdjacencyToPurify` 같은 불리언 스위치도
    /// 마찬가지로 뺐다 — 그건 정화가 무엇인가를 정하는 규칙이지 세기가 아니다.
    ///
    /// 이 구분을 흐리면 프로파일이 「고쳐도 되는 것」과 「고치면 명세 위반인 것」을 같은
    /// 인스펙터에 나란히 놓게 된다. 그 상태에서 누가 20을 8로 내려도 아무도 못 막는다.
    ///
    /// ## 층별 수치는 왜 여기 없는가
    ///
    /// ④ 층별 요구 전력과 ② 계약 출현률은 `FloorPlan._tenFloors` 에 있고, 그 배열은
    /// **각 숫자의 근거가 주석으로 붙어 있다** — 「2층 저항 배율 1.6 은 기본 밀도에서
    /// 직선이 스핀당 0.08회라 5스핀이면 기대 0.4회, 즉 셋 중 둘이 직선을 가르치는
    /// 층에서 직선을 못 본다」 같은 것. 인스펙터로 옮기면 그 근거가 끊긴다.
    /// 별도 항목으로 다루며 이 프로파일의 범위가 아니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SpinBalanceProfile",
                     menuName = "Ascend/Profiles/SpinBalance", order = 103)]
    public sealed class SpinBalanceProfile : ScriptableObject
    {
        /// <summary>`UP-TECH-09` ①③ 이 요구하는 항목 수. 대조표가 셀 수 있어야 한다.</summary>
        public const int RequiredFieldCount = 10;

        [Header("심볼 가중치 (PRD §14.1 ①)")]
        [Tooltip("1. 정상 영혼의 추첨 가중치. 전력의 주 공급원이라 저항체보다 높아야 한다.")]
        [Min(0f)] [SerializeField] private float _weightNormalSoul = DefaultWeightNormalSoul;

        [Tooltip("2. 흡수체의 추첨 가중치.")]
        [Min(0f)] [SerializeField] private float _weightAbsorber = DefaultWeightAbsorber;

        [Tooltip("3. 증식체의 추첨 가중치.")]
        [Min(0f)] [SerializeField] private float _weightProliferator = DefaultWeightProliferator;

        [Header("패턴 배수 (PRD §14.1 ③)")]
        [Tooltip("4. 직선 3연속 배수. 흩어짐(1.0)보다 커야 「한 줄로 서는 것」에 뜻이 생긴다.")]
        [Min(0f)] [SerializeField] private float _lineMultiplier = DefaultLineMultiplier;

        [Tooltip("5. 4개 이상 직교 연결 배수. 직선보다 커야 3층의 교습이 성립한다.")]
        [Min(0f)] [SerializeField] private float _clusterMultiplier = DefaultClusterMultiplier;

        [Tooltip("6. 9칸 동일 저항 잭팟 배수.")]
        [Min(0f)] [SerializeField] private float _fullBoardMultiplier = DefaultFullBoardMultiplier;

        [Tooltip("7. 정화에 필요한 최소 개수.")]
        [Min(1)] [SerializeField] private int _minimumCountToPurify = DefaultMinimumCountToPurify;

        [Header("연쇄·잔류")]
        [Tooltip("8. 연쇄 단계마다 전체 전력에 더해지는 배수 증분. 2연쇄부터 적용된다. " +
                 "연쇄 하드 캡(20)은 여기 없다 — 그건 다이얼이 아니라 명세다.")]
        [Min(0f)] [SerializeField] private float _cascadeMultiplierStep = DefaultCascadeMultiplierStep;

        [Tooltip("9. 남은 흡수체 1개가 깎는 저장 전력.")]
        [Min(0f)] [SerializeField] private float _absorberResidualPowerLoss = DefaultAbsorberResidualPowerLoss;

        [Tooltip("10. 남은 증식체 1개가 다음 스핀 증식체 가중치에 더하는 값. 0이면 증식체가 " +
                 "쌓여도 판이 나빠지지 않아 「방치하면 불어난다」가 사라진다.")]
        [Min(0f)] [SerializeField] private float _proliferatorResidualWeightAdd = DefaultProliferatorResidualWeightAdd;

        // ── 코드 프리셋 ────────────────────────────────────────────────────────
        // `Spin.SpinRuleSet` 의 필드 초기값·`CreateDefault` 와 **같은 수**여야 한다.
        // 다르면 에셋을 만드는 순간 밸런스가 조용히 바뀐다. 테스트가 대조한다.
        public const float DefaultWeightNormalSoul = 5f;
        public const float DefaultWeightAbsorber = 2.5f;
        public const float DefaultWeightProliferator = 2.5f;
        public const float DefaultLineMultiplier = 2f;
        public const float DefaultClusterMultiplier = 3f;
        public const float DefaultFullBoardMultiplier = 10f;
        public const int DefaultMinimumCountToPurify = 3;
        public const float DefaultCascadeMultiplierStep = 0.5f;
        public const float DefaultAbsorberResidualPowerLoss = 8f;
        public const float DefaultProliferatorResidualWeightAdd = 0.6f;

        public SpinBalanceSnapshot Snapshot()
        {
            return new SpinBalanceSnapshot(_weightNormalSoul, _weightAbsorber, _weightProliferator,
                _lineMultiplier, _clusterMultiplier, _fullBoardMultiplier, _minimumCountToPurify,
                _cascadeMultiplierStep, _absorberResidualPowerLoss, _proliferatorResidualWeightAdd,
                name);
        }

        public static SpinBalanceSnapshot DefaultSnapshot
        {
            get
            {
                return new SpinBalanceSnapshot(DefaultWeightNormalSoul, DefaultWeightAbsorber,
                    DefaultWeightProliferator, DefaultLineMultiplier, DefaultClusterMultiplier,
                    DefaultFullBoardMultiplier, DefaultMinimumCountToPurify,
                    DefaultCascadeMultiplierStep, DefaultAbsorberResidualPowerLoss,
                    DefaultProliferatorResidualWeightAdd, SpinBalanceSnapshot.CodePresetName);
            }
        }

        /// <summary>
        /// 스냅샷을 뜨면서 **10개 필드를 전부 읽는다.** 자기모순이면 경고가 뜬다.
        ///
        /// 확인법: 인스펙터에서 직선 배수를 1.0 으로 내리거나 정상 영혼 가중치를 0으로
        /// 만들면 플레이 시작 시 콘솔에 경고 한 줄이 뜬다. 안 뜨면 배선이 끊긴 것이다.
        /// </summary>
        public static SpinBalanceSnapshot SnapshotOrDefault(SpinBalanceProfile profile, string caller)
        {
            if (profile == null) return DefaultSnapshot;

            SpinBalanceSnapshot snapshot = profile.Snapshot();
            string problem = snapshot.Validate();
            if (problem != null)
            {
                Debug.LogWarning($"[상승] SpinBalanceProfile '{profile.name}' 의 값이 자기모순이다 ({caller}): {problem}"
                                 + $"\n  {snapshot.Describe()}");
            }
            return snapshot;
        }
    }

    /// <summary>
    /// 스핀 밸런스 10종의 값 사본. `SpinRuleSet` 은 순수 C# 이라 `ScriptableObject` 를
    /// 알면 안 된다 — 이 구조체가 그 경계다.
    /// </summary>
    public readonly struct SpinBalanceSnapshot
    {
        public const string CodePresetName = "코드 프리셋";

        public readonly float WeightNormalSoul;
        public readonly float WeightAbsorber;
        public readonly float WeightProliferator;
        public readonly float LineMultiplier;
        public readonly float ClusterMultiplier;
        public readonly float FullBoardMultiplier;
        public readonly int MinimumCountToPurify;
        public readonly float CascadeMultiplierStep;
        public readonly float AbsorberResidualPowerLoss;
        public readonly float ProliferatorResidualWeightAdd;

        /// <summary>값이 어디서 왔는가. 에셋 이름이거나 <see cref="CodePresetName"/>.</summary>
        public readonly string SourceName;

        public SpinBalanceSnapshot(float weightNormalSoul, float weightAbsorber,
            float weightProliferator, float lineMultiplier, float clusterMultiplier,
            float fullBoardMultiplier, int minimumCountToPurify, float cascadeMultiplierStep,
            float absorberResidualPowerLoss, float proliferatorResidualWeightAdd, string sourceName)
        {
            WeightNormalSoul = weightNormalSoul;
            WeightAbsorber = weightAbsorber;
            WeightProliferator = weightProliferator;
            LineMultiplier = lineMultiplier;
            ClusterMultiplier = clusterMultiplier;
            FullBoardMultiplier = fullBoardMultiplier;
            MinimumCountToPurify = minimumCountToPurify;
            CascadeMultiplierStep = cascadeMultiplierStep;
            AbsorberResidualPowerLoss = absorberResidualPowerLoss;
            ProliferatorResidualWeightAdd = proliferatorResidualWeightAdd;
            SourceName = string.IsNullOrEmpty(sourceName) ? CodePresetName : sourceName;
        }

        public bool FromAsset => SourceName != CodePresetName;

        /// <summary>
        /// 10개 값이 서로 모순되는지 본다. 문제가 없으면 null.
        ///
        /// 「범위를 벗어났다」가 아니라 **「이 값이면 게임의 한 축이 조용히 사라진다」**만 잡는다.
        /// </summary>
        public string Validate()
        {
            if (WeightNormalSoul <= 0f)
                return $"정상 영혼 가중치가 {WeightNormalSoul} 이라 전력 공급원이 판에 안 나온다";
            if (LineMultiplier <= 1f)
                return $"직선 배수가 {LineMultiplier} 이라 흩어짐(1.0)과 구분되지 않는다 — 2층의 교습이 사라진다";
            if (ClusterMultiplier <= LineMultiplier)
                return $"연결 배수 {ClusterMultiplier} 가 직선 {LineMultiplier} 이하다 — 3층이 가르칠 것이 없어진다";
            if (FullBoardMultiplier <= ClusterMultiplier)
                return $"잭팟 배수 {FullBoardMultiplier} 가 연결 {ClusterMultiplier} 이하다 — 9칸 전부가 보상이 아니게 된다";
            if (MinimumCountToPurify < 1)
                return $"정화 최소 개수가 {MinimumCountToPurify} 다";
            if (CascadeMultiplierStep <= 0f)
                return $"연쇄 증분이 {CascadeMultiplierStep} 이라 연쇄가 길어져도 보상이 그대로다";
            return null;
        }

        public string Describe()
        {
            return $"가중치 영혼 {WeightNormalSoul:0.##}/흡수 {WeightAbsorber:0.##}/증식 {WeightProliferator:0.##}"
                 + $" · 패턴 직선 {LineMultiplier:0.##}/연결 {ClusterMultiplier:0.##}/잭팟 {FullBoardMultiplier:0.##}"
                 + $" · 정화 최소 {MinimumCountToPurify} · 연쇄 증분 {CascadeMultiplierStep:0.##}"
                 + $" · 잔류 흡수 {AbsorberResidualPowerLoss:0.##}/증식 {ProliferatorResidualWeightAdd:0.##}"
                 + $" (출처 {SourceName})";
        }
    }
}

// 씬 배선 필요:
//   `RunSessionBehaviour` 의 `_spinBalanceProfile` 슬롯에 아래 에셋을 물린다.
//   확인은 `RunSessionBehaviour.SpinBalanceSource` 로 한다 — 배선하지 않아도 코드
//   프리셋으로 같은 밸런스로 돌기 때문에 화면으로는 구분되지 않는다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/SpinBalanceProfile.asset
//   (Create ▸ Ascend ▸ Profiles ▸ SpinBalance).
// **하지 말 것**: 연쇄 하드 캡(20)과 `RequireAdjacencyToPurify` 같은 스위치를 이 에셋에
//   추가하지 않는다. 전자는 PRD §6·TECH_SPEC §9 가 못박은 명세고 후자는 규칙의 정의다.
//   다이얼이 아닌 것을 인스펙터에 올리면 명세 위반이 편집 실수와 구분되지 않는다.
