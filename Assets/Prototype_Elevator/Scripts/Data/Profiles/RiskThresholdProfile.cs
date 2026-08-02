using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 위험 판정 수치 9종 (`UP-TECH-09` ⑦ 위험 임계값).
    ///
    /// ## ⑧ 과 무엇이 다른가
    ///
    /// `DangerFeedbackProfile`(⑧)은 **위험해 보이는 방법**을 정한다 — 조명 세기, 깜빡임,
    /// 흔들림, 험의 크기. 여기(⑦)는 **무엇이 위험인가**를 정한다 — 잔류 흡수체 하나가
    /// 몇 점인지, 몇 점부터 Strain 인지.
    ///
    /// 둘이 한 에셋에 있으면 「연출이 약해 보인다」는 이유로 임계값을 내리는 일이
    /// 생긴다. 그건 연출 조정이 아니라 난이도 변경이다.
    ///
    /// ## 히스테리시스는 장식이 아니다
    ///
    /// `TECH_SPEC.md` §6.4 가 진입·이탈 임계값을 따로 요구하는 이유는 경계에서 단계가
    /// 떨리면 조명과 소리가 초당 몇 번씩 뒤집혀 판독성이 무너지기 때문이다. 그건 긴장이
    /// 아니라 고장으로 보인다. <see cref="RiskThresholdSnapshot.Validate"/> 가 이탈 ≥ 진입
    /// 조합을 잡는 것이 그래서다 — 인스펙터 `Min` 으로는 못 막는 종류의 오류다.
    ///
    /// ## 과수확 한 번은 반드시 방을 바꾼다
    ///
    /// `MASTER_PRD.md` §7: 과수확은 「단순한 점수 버튼이 아니라 조명·음향·진동·승객
    /// 반응·기계 상태를 변화시키는 공간적 사건」이어야 한다. 한 번 당겼는데 방이 그대로
    /// Stable 이면 그 문장이 거짓이 된다. 그래서 <c>OverharvestWeight ≥ StrainEnter</c> 는
    /// 밸런스 취향이 아니라 **불변식**이고, 프로파일이 그것을 깨면 경고가 뜬다.
    /// `RiskEvaluator` 쪽에도 같은 불변식을 지키는 테스트가 이미 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "RiskThresholdProfile",
                     menuName = "Ascend/Profiles/RiskThreshold", order = 104)]
    public sealed class RiskThresholdProfile : ScriptableObject
    {
        /// <summary>`UP-TECH-09` ⑦ 이 요구하는 항목 수.</summary>
        public const int RequiredFieldCount = 9;

        [Header("위험 점수 가중치 (PRD §14.1 ⑦)")]
        [Tooltip("1. 남은 흡수체 1개당 점수. 저장 전력을 직접 깎으므로 즉시 위험이다.")]
        [Min(0f)] [SerializeField] private float _absorberWeight = DefaultAbsorberWeight;

        [Tooltip("2. 남은 증식체 1개당 점수. 다음 스핀을 더 나쁘게 만드는 지연된 위험이라 조금 높다.")]
        [Min(0f)] [SerializeField] private float _proliferatorWeight = DefaultProliferatorWeight;

        [Tooltip("3. 과수확 1회당 점수. 반드시 Strain 진입 임계값 이상이어야 한다 — " +
                 "한 번 당겼는데 방이 그대로면 PRD §7 이 거짓이 된다.")]
        [Min(0f)] [SerializeField] private float _overharvestWeight = DefaultOverharvestWeight;

        [Tooltip("4. 과적 상태의 고정 점수.")]
        [Min(0f)] [SerializeField] private float _overloadScore = DefaultOverloadScore;

        [Tooltip("5. 스핀을 다 쓰고도 요구 전력에 못 미칠 때의 고정 점수.")]
        [Min(0f)] [SerializeField] private float _shortfallScore = DefaultShortfallScore;

        [Header("진입·이탈 임계값 (이탈이 항상 더 낮아야 한다)")]
        [Tooltip("6. Strain 진입 점수.")]
        [Min(0f)] [SerializeField] private float _strainEnter = DefaultStrainEnter;

        [Tooltip("7. Strain 이탈 점수. 진입보다 낮아야 경계에서 단계가 떨리지 않는다.")]
        [Min(0f)] [SerializeField] private float _strainExit = DefaultStrainExit;

        [Tooltip("8. Critical 진입 점수.")]
        [Min(0f)] [SerializeField] private float _criticalEnter = DefaultCriticalEnter;

        [Tooltip("9. Critical 이탈 점수. 진입보다 낮아야 한다.")]
        [Min(0f)] [SerializeField] private float _criticalExit = DefaultCriticalExit;

        // ── 코드 프리셋 ────────────────────────────────────────────────────────
        // `Risk.RiskEvaluator` 의 필드 초기값과 **같은 수**여야 한다. 테스트가 대조한다.
        public const float DefaultAbsorberWeight = 1.0f;
        public const float DefaultProliferatorWeight = 1.2f;
        public const float DefaultOverharvestWeight = 3.2f;
        public const float DefaultOverloadScore = 3.0f;
        public const float DefaultShortfallScore = 4.0f;
        public const float DefaultStrainEnter = 3.0f;
        public const float DefaultStrainExit = 2.0f;
        public const float DefaultCriticalEnter = 7.0f;
        public const float DefaultCriticalExit = 5.5f;

        public RiskThresholdSnapshot Snapshot()
        {
            return new RiskThresholdSnapshot(_absorberWeight, _proliferatorWeight,
                _overharvestWeight, _overloadScore, _shortfallScore,
                _strainEnter, _strainExit, _criticalEnter, _criticalExit, name);
        }

        public static RiskThresholdSnapshot DefaultSnapshot
        {
            get
            {
                return new RiskThresholdSnapshot(DefaultAbsorberWeight, DefaultProliferatorWeight,
                    DefaultOverharvestWeight, DefaultOverloadScore, DefaultShortfallScore,
                    DefaultStrainEnter, DefaultStrainExit, DefaultCriticalEnter,
                    DefaultCriticalExit, RiskThresholdSnapshot.CodePresetName);
            }
        }

        /// <summary>
        /// 스냅샷을 뜨면서 **9개 필드를 전부 읽는다.** 자기모순이면 경고가 뜬다.
        ///
        /// 확인법: 인스펙터에서 Strain 이탈을 진입보다 높게 만들거나 과수확 점수를
        /// Strain 진입 아래로 내리면 플레이 시작 시 콘솔에 경고 한 줄이 뜬다.
        /// </summary>
        public static RiskThresholdSnapshot SnapshotOrDefault(RiskThresholdProfile profile, string caller)
        {
            if (profile == null) return DefaultSnapshot;

            RiskThresholdSnapshot snapshot = profile.Snapshot();
            string problem = snapshot.Validate();
            if (problem != null)
            {
                Debug.LogWarning($"[상승] RiskThresholdProfile '{profile.name}' 의 값이 자기모순이다 ({caller}): {problem}"
                                 + $"\n  {snapshot.Describe()}");
            }
            return snapshot;
        }
    }

    /// <summary>
    /// 위험 판정 수치 9종의 값 사본. <see cref="Ascend.Prototype.Risk.RiskEvaluator"/> 는
    /// 순수 C# 이라 `ScriptableObject` 를 알면 안 된다 — 이 구조체가 그 경계다.
    /// </summary>
    public readonly struct RiskThresholdSnapshot
    {
        public const string CodePresetName = "코드 프리셋";

        public readonly float AbsorberWeight;
        public readonly float ProliferatorWeight;
        public readonly float OverharvestWeight;
        public readonly float OverloadScore;
        public readonly float ShortfallScore;
        public readonly float StrainEnter;
        public readonly float StrainExit;
        public readonly float CriticalEnter;
        public readonly float CriticalExit;

        public readonly string SourceName;

        public RiskThresholdSnapshot(float absorberWeight, float proliferatorWeight,
            float overharvestWeight, float overloadScore, float shortfallScore,
            float strainEnter, float strainExit, float criticalEnter, float criticalExit,
            string sourceName)
        {
            AbsorberWeight = absorberWeight;
            ProliferatorWeight = proliferatorWeight;
            OverharvestWeight = overharvestWeight;
            OverloadScore = overloadScore;
            ShortfallScore = shortfallScore;
            StrainEnter = strainEnter;
            StrainExit = strainExit;
            CriticalEnter = criticalEnter;
            CriticalExit = criticalExit;
            SourceName = string.IsNullOrEmpty(sourceName) ? CodePresetName : sourceName;
        }

        public bool FromAsset => SourceName != CodePresetName;

        /// <summary>
        /// 9개 값이 서로 모순되는지 본다. 문제가 없으면 null.
        ///
        /// 인스펙터 `Min` 은 음수만 막는다. 여기서 잡는 것은 **범위 안에서 기능이
        /// 무너지는 조합**이다 — 히스테리시스가 뒤집히거나, 단계 순서가 어긋나거나,
        /// 과수확이 방을 못 바꾸는 것.
        /// </summary>
        public string Validate()
        {
            if (StrainExit >= StrainEnter)
                return $"Strain 이탈 {StrainExit} 이 진입 {StrainEnter} 이상이다 — 경계에서 단계가 떨린다";
            if (CriticalExit >= CriticalEnter)
                return $"Critical 이탈 {CriticalExit} 이 진입 {CriticalEnter} 이상이다 — 경계에서 단계가 떨린다";
            if (StrainEnter >= CriticalEnter)
                return $"Strain 진입 {StrainEnter} 이 Critical 진입 {CriticalEnter} 이상이다 — Strain 을 건너뛴다";
            if (OverharvestWeight < StrainEnter)
                return $"과수확 점수 {OverharvestWeight} 가 Strain 진입 {StrainEnter} 미만이다"
                     + " — 한 번 당겨도 방이 Stable 이라 PRD §7 이 거짓이 된다";
            if (AbsorberWeight <= 0f && ProliferatorWeight <= 0f)
                return "잔류 가중치가 둘 다 0이라 저항체를 남겨도 위험해지지 않는다";
            return null;
        }

        public string Describe()
        {
            return $"가중치 흡수 {AbsorberWeight:0.##}/증식 {ProliferatorWeight:0.##}/과수확 {OverharvestWeight:0.##}"
                 + $" · 고정 과적 {OverloadScore:0.##}/미달 {ShortfallScore:0.##}"
                 + $" · Strain {StrainExit:0.##}→{StrainEnter:0.##}"
                 + $" · Critical {CriticalExit:0.##}→{CriticalEnter:0.##} (출처 {SourceName})";
        }
    }
}

// 씬 배선 필요:
//   `Scripts/Risk/RiskStateView.cs` 의 `_thresholdProfile` 슬롯에 아래 에셋을 물린다.
//   `RiskStateView.ThresholdSource` 가 「코드 프리셋」이 아닌 에셋 이름을 찍는지로 확인한다.
//   연출 출처인 `ProfileSource` 와는 다른 축이다 — 둘을 같이 봐야 「연출은 에셋, 판정은
//   코드」인 절반 배선을 잡는다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/RiskThresholdProfile.asset
//   (Create ▸ Ascend ▸ Profiles ▸ RiskThreshold). 생성 직후 값은 코드 프리셋과 같다.
// 주의: 여기 값과 `DangerFeedbackProfile`(⑧) 을 한 에셋으로 합치지 않는다. 「연출이 약해
//   보인다」는 이유로 임계값을 내리는 일이 생기고, 그건 연출 조정이 아니라 난이도 변경이다.
