using UnityEngine;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 저항 계약 수치 8종 (`UP-TECH-09` ② 계약 출현률). 계약 둘이 각각 네 값을 갖는다 —
    /// 출현 배율·정화 보상 배율·패턴 배수 가산·잔류 대가 배수.
    ///
    /// ## 계약은 「더 나오게 하고 더 아프게 하는」 거래다
    ///
    /// 네 값이 함께 움직여야 계약이 거래가 된다. 출현만 올리면 판이 나빠지기만 하고,
    /// 보상만 올리면 공짜다. 그래서 <see cref="ContractSnapshot.Validate"/> 는 「출현을
    /// 올렸는데 보상도 대가도 안 올렸다」 같은 조합을 잡는다 — 범위가 아니라 **거래가
    /// 성립하는가**를 본다.
    ///
    /// ## 왜 덮어쓰기인가
    ///
    /// 계약은 `FloorPlan.ContractChoices` 배열 안에 **값 복사본**으로 들어 있고, 그 배열은
    /// `_tenFloors` 정적 초기화 때 한 번 만들어진다. 즉 나중에 프로파일을 물려도 이미
    /// 만들어진 구조체를 고칠 수는 없다. 그래서 층을 읽는 순간
    /// (<see cref="ContractSnapshot.Apply"/>) 갈아 끼운다.
    ///
    /// **정적 배열을 제자리에서 고치지 않는다.** `ContractChoices` 는 층 계획들이 공유하는
    /// 배열이라, 한 번 덮어쓰면 그 뒤의 모든 런이 오염된다. 덮어쓸 것이 있을 때만 복사본을
    /// 만들고, 없으면 원본을 그대로 통과시킨다(할당 0).
    /// </summary>
    [CreateAssetMenu(fileName = "ContractProfile",
                     menuName = "Ascend/Profiles/Contract", order = 106)]
    public sealed class ContractProfile : ScriptableObject
    {
        /// <summary>`UP-TECH-09` ② 가 요구하는 항목 수. 계약 2종 × 4값.</summary>
        public const int RequiredFieldCount = 8;

        [Header("흡수체 계약 (PRD §14.1 ②)")]
        [Tooltip("1. 흡수체가 판에 나올 가중치에 곱하는 배율.")]
        [Min(0f)] [SerializeField] private float _absorberAppearance = DefaultAbsorberAppearance;

        [Tooltip("2. 흡수체를 정화했을 때 보상 배율.")]
        [Min(0f)] [SerializeField] private float _absorberPurifyReward = DefaultAbsorberPurifyReward;

        [Tooltip("3. 흡수체 패턴 배수에 더하는 값.")]
        [Min(0f)] [SerializeField] private float _absorberPatternBonus = DefaultAbsorberPatternBonus;

        [Tooltip("4. 남은 흡수체의 대가 배수.")]
        [Min(0f)] [SerializeField] private float _absorberResidualPenalty = DefaultAbsorberResidualPenalty;

        [Header("증식체 계약")]
        [Tooltip("5. 증식체가 판에 나올 가중치에 곱하는 배율.")]
        [Min(0f)] [SerializeField] private float _proliferatorAppearance = DefaultProliferatorAppearance;

        [Tooltip("6. 증식체를 정화했을 때 보상 배율.")]
        [Min(0f)] [SerializeField] private float _proliferatorPurifyReward = DefaultProliferatorPurifyReward;

        [Tooltip("7. 증식체 패턴 배수에 더하는 값.")]
        [Min(0f)] [SerializeField] private float _proliferatorPatternBonus = DefaultProliferatorPatternBonus;

        [Tooltip("8. 남은 증식체의 대가 배수. 증식체는 방치하면 불어나므로 흡수체보다 높다.")]
        [Min(0f)] [SerializeField] private float _proliferatorResidualPenalty = DefaultProliferatorResidualPenalty;

        // ── 코드 프리셋 ────────────────────────────────────────────────────────
        // `Spin.PrototypeCurriculum` 의 두 계약 정의와 **같은 수**여야 한다.
        public const float DefaultAbsorberAppearance = 1.6f;
        public const float DefaultAbsorberPurifyReward = 1.8f;
        public const float DefaultAbsorberPatternBonus = 0.5f;
        public const float DefaultAbsorberResidualPenalty = 2.0f;
        public const float DefaultProliferatorAppearance = 1.25f;
        public const float DefaultProliferatorPurifyReward = 1.2f;
        public const float DefaultProliferatorPatternBonus = 0.7f;
        public const float DefaultProliferatorResidualPenalty = 2.0f;

        public ContractSnapshot Snapshot()
        {
            return new ContractSnapshot(
                _absorberAppearance, _absorberPurifyReward, _absorberPatternBonus, _absorberResidualPenalty,
                _proliferatorAppearance, _proliferatorPurifyReward, _proliferatorPatternBonus,
                _proliferatorResidualPenalty, name);
        }

        /// <summary>
        /// 에셋 없이 도는 경로. 값은 코드 프리셋과 같지만 <see cref="ContractSnapshot.Overrides"/>
        /// 가 거짓이라 **아무것도 갈아 끼우지 않는다** — 배열 복사도 일어나지 않는다.
        /// </summary>
        public static ContractSnapshot DefaultSnapshot =>
            new ContractSnapshot(ContractSnapshot.CodePresetName);

        public static ContractSnapshot SnapshotOrDefault(ContractProfile profile, string caller)
        {
            if (profile == null) return DefaultSnapshot;

            ContractSnapshot snapshot = profile.Snapshot();
            string problem = snapshot.Validate();
            if (problem != null)
            {
                Debug.LogWarning($"[상승] ContractProfile '{profile.name}' 의 값이 자기모순이다 ({caller}): {problem}"
                                 + $"\n  {snapshot.Describe()}");
            }
            return snapshot;
        }

        public void Reset()
        {
            ResistanceContract absorber = PrototypeCurriculum.AbsorberContract;
            ResistanceContract proliferator = PrototypeCurriculum.ProliferatorContract;
            _absorberAppearance = absorber.AppearanceMultiplier;
            _absorberPurifyReward = absorber.PurifyRewardMultiplier;
            _absorberPatternBonus = absorber.PatternBonusAdd;
            _absorberResidualPenalty = absorber.ResidualPenaltyMultiplier;
            _proliferatorAppearance = proliferator.AppearanceMultiplier;
            _proliferatorPurifyReward = proliferator.PurifyRewardMultiplier;
            _proliferatorPatternBonus = proliferator.PatternBonusAdd;
            _proliferatorResidualPenalty = proliferator.ResidualPenaltyMultiplier;
        }
    }

    /// <summary>
    /// 계약 8종의 값 사본. `FloorPlan`·`TenFloorSource` 는 순수 C# 이라
    /// `ScriptableObject` 를 알면 안 된다 — 이 구조체가 그 경계다.
    /// </summary>
    public readonly struct ContractSnapshot
    {
        public const string CodePresetName = "코드 프리셋";

        public readonly float AbsorberAppearance;
        public readonly float AbsorberPurifyReward;
        public readonly float AbsorberPatternBonus;
        public readonly float AbsorberResidualPenalty;
        public readonly float ProliferatorAppearance;
        public readonly float ProliferatorPurifyReward;
        public readonly float ProliferatorPatternBonus;
        public readonly float ProliferatorResidualPenalty;

        public readonly string SourceName;

        /// <summary>
        /// 실제로 갈아 끼우는가. 폴백 스냅샷은 거짓이라 배열 복사조차 하지 않는다 —
        /// 「값이 같으니 덮어써도 결과가 같다」와 「아예 안 건드린다」는 다르고,
        /// 후자여야 에셋이 없을 때의 할당이 0이 된다.
        /// </summary>
        public readonly bool Overrides;

        /// <summary>폴백 전용. 값은 프리셋과 같지만 <see cref="Overrides"/>가 거짓이다.</summary>
        public ContractSnapshot(string sourceName)
            : this(ContractProfile.DefaultAbsorberAppearance, ContractProfile.DefaultAbsorberPurifyReward,
                   ContractProfile.DefaultAbsorberPatternBonus, ContractProfile.DefaultAbsorberResidualPenalty,
                   ContractProfile.DefaultProliferatorAppearance, ContractProfile.DefaultProliferatorPurifyReward,
                   ContractProfile.DefaultProliferatorPatternBonus, ContractProfile.DefaultProliferatorResidualPenalty,
                   sourceName, false)
        {
        }

        public ContractSnapshot(float absorberAppearance, float absorberPurifyReward,
            float absorberPatternBonus, float absorberResidualPenalty,
            float proliferatorAppearance, float proliferatorPurifyReward,
            float proliferatorPatternBonus, float proliferatorResidualPenalty, string sourceName)
            : this(absorberAppearance, absorberPurifyReward, absorberPatternBonus, absorberResidualPenalty,
                   proliferatorAppearance, proliferatorPurifyReward, proliferatorPatternBonus,
                   proliferatorResidualPenalty, sourceName, true)
        {
        }

        private ContractSnapshot(float absorberAppearance, float absorberPurifyReward,
            float absorberPatternBonus, float absorberResidualPenalty,
            float proliferatorAppearance, float proliferatorPurifyReward,
            float proliferatorPatternBonus, float proliferatorResidualPenalty,
            string sourceName, bool overrides)
        {
            AbsorberAppearance = absorberAppearance;
            AbsorberPurifyReward = absorberPurifyReward;
            AbsorberPatternBonus = absorberPatternBonus;
            AbsorberResidualPenalty = absorberResidualPenalty;
            ProliferatorAppearance = proliferatorAppearance;
            ProliferatorPurifyReward = proliferatorPurifyReward;
            ProliferatorPatternBonus = proliferatorPatternBonus;
            ProliferatorResidualPenalty = proliferatorResidualPenalty;
            SourceName = string.IsNullOrEmpty(sourceName) ? CodePresetName : sourceName;
            Overrides = overrides;
        }

        public bool FromAsset => SourceName != CodePresetName;

        /// <summary>
        /// 층 계획의 계약 선택지를 갈아 끼운다. <see cref="Overrides"/>가 거짓이면
        /// **원본 배열을 그대로 돌려준다** — 복사도 하지 않는다.
        ///
        /// 덮어쓸 때는 반드시 **새 배열**을 만든다. `ContractChoices` 는 정적 초기화로
        /// 만들어진 공유 배열이라, 제자리에서 고치면 그 뒤의 모든 런이 오염된다.
        /// </summary>
        public FloorPlan Apply(FloorPlan plan)
        {
            if (!Overrides) return plan;

            ResistanceContract[] choices = plan.ContractChoices;
            if (choices == null || choices.Length == 0) return plan;

            var copy = new ResistanceContract[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                ResistanceContract c = choices[i];
                if (c.Target == SymbolKind.Absorber)
                {
                    c.AppearanceMultiplier = AbsorberAppearance;
                    c.PurifyRewardMultiplier = AbsorberPurifyReward;
                    c.PatternBonusAdd = AbsorberPatternBonus;
                    c.ResidualPenaltyMultiplier = AbsorberResidualPenalty;
                }
                else if (c.Target == SymbolKind.Proliferator)
                {
                    c.AppearanceMultiplier = ProliferatorAppearance;
                    c.PurifyRewardMultiplier = ProliferatorPurifyReward;
                    c.PatternBonusAdd = ProliferatorPatternBonus;
                    c.ResidualPenaltyMultiplier = ProliferatorResidualPenalty;
                }
                // `None`(무계약)은 건드리지 않는다 — 그건 계약이 아니라 계약을 안 한 것이다.
                copy[i] = c;
            }
            plan.ContractChoices = copy;
            return plan;
        }

        /// <summary>
        /// 거래가 성립하는지 본다. 문제가 없으면 null.
        ///
        /// 계약은 「더 나오게 하고 더 아프게 하되 더 주는」 거래다. 출현만 올리면 판이
        /// 나빠지기만 하고, 대가가 1 이하면 계약이 순이득이라 선택이 아니게 된다.
        /// </summary>
        public string Validate()
        {
            if (AbsorberAppearance <= 1f && ProliferatorAppearance <= 1f)
                return $"두 계약의 출현 배율이 {AbsorberAppearance}/{ProliferatorAppearance} 라"
                     + " 계약을 걸어도 대상이 더 나오지 않는다 — 계약이 아무 일도 안 한다";
            if (AbsorberPurifyReward <= 1f && AbsorberPatternBonus <= 0f)
                return "흡수체 계약이 보상도 패턴 가산도 주지 않는다 — 위험만 늘리는 계약이다";
            if (ProliferatorPurifyReward <= 1f && ProliferatorPatternBonus <= 0f)
                return "증식체 계약이 보상도 패턴 가산도 주지 않는다 — 위험만 늘리는 계약이다";
            if (AbsorberResidualPenalty < 1f || ProliferatorResidualPenalty < 1f)
                return $"잔류 대가 배수가 1 미만이다 ({AbsorberResidualPenalty}/{ProliferatorResidualPenalty})"
                     + " — 계약이 잔류를 오히려 덜 아프게 만든다";
            return null;
        }

        public string Describe()
        {
            return $"흡수 출현 {AbsorberAppearance:0.##}/보상 {AbsorberPurifyReward:0.##}"
                 + $"/패턴 +{AbsorberPatternBonus:0.##}/대가 {AbsorberResidualPenalty:0.##}"
                 + $" · 증식 출현 {ProliferatorAppearance:0.##}/보상 {ProliferatorPurifyReward:0.##}"
                 + $"/패턴 +{ProliferatorPatternBonus:0.##}/대가 {ProliferatorResidualPenalty:0.##}"
                 + $" (출처 {SourceName}, 갈아끼움 {(Overrides ? "함" : "안 함")})";
        }
    }
}

// 씬 배선 필요:
//   `RunSessionBehaviour` 의 `_contractProfile` 슬롯에 아래 에셋을 물린다.
//   `RunSessionBehaviour.ContractSource` 가 「코드 프리셋」이 아닌 에셋 이름을 찍는지로
//   확인한다. 배선하지 않으면 갈아끼움 자체가 일어나지 않아 할당도 0이다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/ContractProfile.asset
//   (Create ▸ Ascend ▸ Profiles ▸ Contract). 생성 시 Reset() 이 현재 계약 값을 그대로
//   채우므로 만들기만 해서는 밸런스가 바뀌지 않는다.
// **하지 말 것**: `Target`·`Label` 을 이 에셋에 넣지 않는다. 어느 저항체를 대상으로
//   하는가는 계약의 정체지 다이얼이 아니다 — 흡수체 계약의 대상을 증식체로 바꾸면
//   4층과 6층이 가르치는 것이 뒤바뀐다.
