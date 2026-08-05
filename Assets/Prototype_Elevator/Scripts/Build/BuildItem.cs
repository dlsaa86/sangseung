using System;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Build
{
    /// <summary>
    /// 빌드 방향. 노션 §6.3 「내부 빌드 방향」 여섯 줄을 그대로 축으로 삼는다.
    ///
    /// 이 축은 밸런스 수치가 아니라 **제시 규칙**에 쓰인다 — 층마다 3장을 서로 다른
    /// 축에서 뽑아, 플레이어가 「어느 방향으로 갈 것인가」를 고르게 만든다.
    /// 무작위 3장은 같은 방향이 겹치면 사실상 선택이 하나로 줄어든다.
    ///
    /// ⚠ **태그를 화면에 미리 보여주지 않는다.** 노션 §6.2 가 「태그·세트 카운터·추천
    /// 조합은 선공개하지 않는다」를 명시한다. 이 값은 제시를 고르는 데만 쓰고 UI 로 새지 않는다.
    /// </summary>
    public enum BuildAxis
    {
        /// <summary>기본 정화를 안정적으로 반복한다. 바닥을 받치는 축.</summary>
        Stability = 0,

        /// <summary>직선과 위치 보너스를 강화한다.</summary>
        Pattern,

        /// <summary>연결 제거와 재충전을 연쇄시킨다.</summary>
        Cascade,

        /// <summary>잔류 저항을 다음 실행의 재료로 쓴다.</summary>
        Residual,

        /// <summary>무게와 과수확 위험을 출력으로 바꾼다.</summary>
        Load,
    }

    /// <summary>승객인가 부품인가. 무게·영속성·하차 여부가 갈린다.</summary>
    public enum BuildItemKind
    {
        /// <summary>목적지에 도착하면 내린다. 가볍고, 내릴 때 보상을 준다.</summary>
        Passenger = 0,

        /// <summary>장치에 물린다. 런이 끝날 때까지 남고, 무겁다.</summary>
        Part = 1,
    }

    /// <summary>
    /// 승객·부품이 규칙 다발의 무엇을 건드리는가.
    ///
    /// 가상 함수 체인 대신 열거형 + 인자 하나로 두는 이유는 `SpinRuleSet`의 설계 근거와 같다 —
    /// 밸런스를 반복 수정하려면 "이 배수가 어디서 왔는가"를 스핀마다 통째로 찍을 수 있어야 하고,
    /// 델리게이트는 로그에 이름을 남기지 않는다. 여기 있는 값은 전부 `SpinRuleSet`이 이미
    /// 승객용으로 예약해 둔 필드에 대응한다.
    /// </summary>
    public enum BuildEffectKind
    {
        None = 0,

        /// <summary>대상 저항의 기본 정화 최소 개수를 <c>Amount</c>로 바꾼다(3 → 2).</summary>
        PurifyThreshold,

        /// <summary>스핀마다 정상 영혼을 최소 <c>Amount</c>개 보장한다.</summary>
        GuaranteeNormalSouls,

        /// <summary>대각 인접도 연결 덩어리로 인정한다.</summary>
        DiagonalConnects,

        /// <summary>빈칸 재충전 시 정상 영혼 확률에 <c>Amount</c>를 더한다.</summary>
        RefillSoulBias,

        /// <summary>잔류 대가 전체에 <c>Amount</c>를 곱한다(1 미만이 완화).</summary>
        ResidualMitigation,

        /// <summary>연쇄 단계당 배수 증분에 <c>Amount</c>를 더한다.</summary>
        CascadeStep,

        /// <summary>대상 저항의 패턴 배수에 <c>Amount</c>를 더한다.</summary>
        PatternBonus,

        /// <summary>대상 저항의 정화 보상 배수에 <c>Amount</c>를 곱한다.</summary>
        PurifyReward,

        /// <summary>정상 영혼 1개의 기본 전력에 <c>Amount</c>를 더한다.</summary>
        NormalSoulValue,

        /// <summary>한 저항에 대해 여러 패턴을 중복 판정한다.</summary>
        MultiplePatterns,

        /// <summary>대상 저항의 등장 가중치에 <c>Amount</c>를 곱한다.</summary>
        Appearance,

        /// <summary>직선 3개 패턴 배수에 <c>Amount</c>를 더한다(Notion 「적재·탑승·빌드」 측량사).</summary>
        LineMultiplier,

        /// <summary>4개 이상 연결 패턴 배수에 <c>Amount</c>를 더한다.</summary>
        ClusterMultiplier,
    }

    /// <summary>
    /// 🔴 **효과가 언제 발동하는가.** 이 축이 없던 것이 빌드 다양성 결함의 구조적 원인이었다.
    ///
    /// ## 왜 생겼나
    ///
    /// 2026-08-05 실측(`docs/runtime/BUILD_DIVERSITY_AUDIT.md`): 11종 중 **한 품목이
    /// 전체 이득의 83%**를 가져가고, **55개 짝 전부가 ±1%p 안에서 단순 합과 같았다.**
    /// 그 보고서의 표현이 정확하다 — 「**효과들이 서로를 모른다**」.
    ///
    /// 몰랐던 이유는 밸런스 수치가 아니라 **형태**였다. 모든 효과가 조건 없는 가산·곱셈
    /// 한 번이었으므로, 두 품목의 합이 각자의 합과 달라질 **경로가 존재하지 않았다.**
    /// 값을 아무리 조정해도 시너지 짝은 0개로 유지된다. 노션 §6.2 의
    /// 「둘 이상이 상태를 주고받을 때 예상 밖의 공명이 발생한다」가 데이터로 성립할 수 없었다.
    ///
    /// ## 어떻게 판정하나 — **순서 무관**이 이 설계의 핵심이다
    ///
    /// <see cref="BuildLoadout.ApplyTo"/> 가 두 패스로 돈다.
    /// ① 조건 없는 효과를 전부 적용한다. ② 그 결과 규칙에 대해서만 조건을 판정하고
    /// 조건부 효과를 적용한다.
    ///
    /// 조건은 **①이 끝난 상태에서만** 읽는다. 조건부 효과가 다른 조건부 효과의 조건을
    /// 켜지 못하므로, 같은 조합은 **집은 순서와 무관하게 같은 값**을 낸다. 이 성질이
    /// 없으면 「1층에서 A를 먼저 집었는가」로 6층 결과가 달라지고, 그건 플레이어가
    /// 설명할 수 없는 차이다(노션 §10 「최소 하나의 핵심 원인을 기억해야 한다」).
    ///
    /// 연쇄 깊이를 1로 묶은 것도 같은 이유다. 노션 §3.3 이 「초반에는 원인을 읽을 수
    /// 있게」를 요구하는데, 조건이 조건을 켜기 시작하면 무엇이 무엇을 켰는지 화면에서
    /// 읽을 수 없다.
    /// </summary>
    public enum BuildEffectCondition
    {
        /// <summary>항상 발동한다. 기본값이므로 옛 초기화 구문은 한 자리도 안 바뀐다.</summary>
        Always = 0,

        /// <summary>대각 연결이 인정될 때만 (사선 결속기 계열이 켠다).</summary>
        DiagonalConnects,

        /// <summary>정상 영혼 보장이 걸려 있을 때만 (영혼 포집망 계열이 켠다).</summary>
        SoulsGuaranteed,

        /// <summary>대상 저항의 정화 문턱이 2개 이하로 내려가 있을 때만 (계측 기사 계열).</summary>
        PurifyThresholdLowered,

        /// <summary>잔류 대가가 완화돼 있을 때만 (잔류 감쇠기 계열).</summary>
        ResidualMitigated,

        /// <summary>잔류 대가가 증폭돼 있을 때만 — 위험을 키운 빌드에만 보답한다.</summary>
        ResidualAmplified,

        /// <summary>중복 패턴 판정이 켜져 있을 때만.</summary>
        MultiplePatterns,

        /// <summary>이 층 판에 증식체가 등장할 때만.</summary>
        ProliferatorInPlay,
    }

    /// <summary>규칙 다발에 가하는 변경 하나. 대상이 필요 없는 종류는 <see cref="Target"/>를 무시한다.</summary>
    [Serializable]
    public struct BuildEffect
    {
        public BuildEffectKind Kind;
        public SymbolKind Target;
        public float Amount;

        /// <summary>
        /// 이 효과가 발동할 조건. <see cref="BuildEffectCondition.Always"/>(=0)가 기본값이라
        /// 조건을 안 적은 효과는 예전과 완전히 같이 동작한다.
        /// </summary>
        public BuildEffectCondition Condition;

        public static BuildEffect Of(BuildEffectKind kind, float amount)
            => new BuildEffect { Kind = kind, Target = SymbolKind.Empty, Amount = amount };

        public static BuildEffect Of(BuildEffectKind kind, SymbolKind target, float amount)
            => new BuildEffect { Kind = kind, Target = target, Amount = amount };

        /// <summary>
        /// 조건을 붙인 사본. 카탈로그에서 한 줄로 읽히도록 유창 형태로 둔다 —
        /// <c>BuildEffect.Of(CascadeStep, 0.35f).When(DiagonalConnects)</c>.
        /// </summary>
        public BuildEffect When(BuildEffectCondition condition)
        {
            BuildEffect copy = this;
            copy.Condition = condition;
            return copy;
        }

        /// <summary>조건 없이 항상 도는 효과인가. 1패스에서 적용된다.</summary>
        public bool IsUnconditional => Condition == BuildEffectCondition.Always;

        /// <summary>
        /// 1패스가 끝난 규칙에 대해 조건이 성립하는가.
        ///
        /// **여기서 규칙을 바꾸지 않는다.** 읽기만 한다 — 판정 중에 판정 대상이 움직이면
        /// 순서 무관이 깨진다.
        /// </summary>
        public bool IsSatisfiedBy(SpinRuleSet rules)
        {
            if (rules == null) return false;
            switch (Condition)
            {
                case BuildEffectCondition.Always:
                    return true;
                case BuildEffectCondition.DiagonalConnects:
                    return rules.DiagonalCountsAsConnected;
                case BuildEffectCondition.SoulsGuaranteed:
                    return rules.GuaranteedNormalSouls > 0;
                case BuildEffectCondition.PurifyThresholdLowered:
                    return rules.MinimumCountFor(Target) <= 2;
                case BuildEffectCondition.ResidualMitigated:
                    return rules.ResidualMitigation < 0.9999f;
                case BuildEffectCondition.ResidualAmplified:
                    return rules.ResidualMitigation > 1.0001f;
                case BuildEffectCondition.MultiplePatterns:
                    return rules.AllowMultiplePatternsPerKind;
                case BuildEffectCondition.ProliferatorInPlay:
                    return rules.WeightOf(SymbolKind.Proliferator) > 0f;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 조건을 사람이 읽는 한 마디로. 사고 기록기·디버그 패널이 그대로 찍는다 —
        /// 노션 §10 이 요구하는 인과 순서 ②「어떤 승객·부품이 반응했는가」가
        /// 화면에 나오려면 조건이 문장에 있어야 한다.
        /// </summary>
        public string DescribeCondition()
        {
            switch (Condition)
            {
                case BuildEffectCondition.Always:                 return null;
                case BuildEffectCondition.DiagonalConnects:       return "대각 연결이 인정될 때";
                case BuildEffectCondition.SoulsGuaranteed:        return "정상 영혼이 보장될 때";
                case BuildEffectCondition.PurifyThresholdLowered: return $"{Label(Target)} 정화 문턱이 내려가 있을 때";
                case BuildEffectCondition.ResidualMitigated:      return "잔류 대가가 완화돼 있을 때";
                case BuildEffectCondition.ResidualAmplified:      return "잔류 대가가 커져 있을 때";
                case BuildEffectCondition.MultiplePatterns:       return "중복 패턴 판정이 켜져 있을 때";
                case BuildEffectCondition.ProliferatorInPlay:     return "판에 증식체가 있을 때";
                default:                                          return "알 수 없는 조건";
            }
        }

        /// <summary>사고 기록기와 디버그 패널이 그대로 찍는 한 줄.</summary>
        public string Describe()
        {
            string body = DescribeEffect();
            string condition = DescribeCondition();
            return condition == null ? body : $"{condition} {body}";
        }

        private string DescribeEffect()
        {
            switch (Kind)
            {
                case BuildEffectKind.PurifyThreshold:
                    return $"{Label(Target)} 정화 문턱 {(int)Amount}개";
                case BuildEffectKind.GuaranteeNormalSouls:
                    return $"정상 영혼 최소 {(int)Amount}개 보장";
                case BuildEffectKind.DiagonalConnects:
                    return "대각 연결 인정";
                case BuildEffectKind.RefillSoulBias:
                    return $"재충전 정상 영혼 +{Amount:P0}";
                case BuildEffectKind.ResidualMitigation:
                    return $"잔류 대가 ×{Amount:F2}";
                case BuildEffectKind.CascadeStep:
                    return $"연쇄 배수 증분 +{Amount:F2}";
                case BuildEffectKind.PatternBonus:
                    return $"{Label(Target)} 패턴 배수 +{Amount:F1}";
                case BuildEffectKind.PurifyReward:
                    return $"{Label(Target)} 정화 보상 ×{Amount:F2}";
                case BuildEffectKind.NormalSoulValue:
                    return $"정상 영혼 전력 +{Amount:F0}";
                case BuildEffectKind.MultiplePatterns:
                    return "중복 패턴 판정";
                case BuildEffectKind.Appearance:
                    return $"{Label(Target)} 출현 ×{Amount:F2}";
                case BuildEffectKind.LineMultiplier:
                    return $"직선 배수 +{Amount:F1}";
                case BuildEffectKind.ClusterMultiplier:
                    return $"연결 배수 +{Amount:F1}";
                default:
                    return "효과 없음";
            }
        }

        private static string Label(SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.Absorber:     return "흡수체";
                case SymbolKind.Proliferator: return "증식체";
                case SymbolKind.NormalSoul:   return "정상 영혼";
                default:                      return "전체";
            }
        }

        /// <summary>
        /// 규칙 다발에 적용한다. 계약이 이미 적용된 뒤에 호출된다
        /// (`SpinRuleSet` 주석의 발동 순서: 기본값 → 층 규칙 → 계약 → 승객·부품).
        /// </summary>
        public void ApplyTo(SpinRuleSet rules)
        {
            if (rules == null) return;
            switch (Kind)
            {
                case BuildEffectKind.PurifyThreshold:
                    rules.MinimumCountToPurify[Target] = Math.Max(1, (int)Amount);
                    break;

                case BuildEffectKind.GuaranteeNormalSouls:
                    rules.GuaranteedNormalSouls =
                        Math.Max(rules.GuaranteedNormalSouls, (int)Amount);
                    break;

                case BuildEffectKind.DiagonalConnects:
                    rules.DiagonalCountsAsConnected = true;
                    break;

                case BuildEffectKind.RefillSoulBias:
                    rules.RefillNormalSoulBias += Amount;
                    break;

                case BuildEffectKind.ResidualMitigation:
                    rules.ResidualMitigation *= Amount;
                    break;

                case BuildEffectKind.CascadeStep:
                    rules.CascadeMultiplierStep += Amount;
                    break;

                case BuildEffectKind.PatternBonus:
                    rules.PatternBonusAdd[Target] = rules.PatternBonusFor(Target) + Amount;
                    break;

                case BuildEffectKind.PurifyReward:
                    rules.PurifyRewardMultiplier[Target] = rules.PurifyRewardFor(Target) * Amount;
                    break;

                case BuildEffectKind.NormalSoulValue:
                    rules.NormalSoulValue += Amount;
                    break;

                case BuildEffectKind.MultiplePatterns:
                    rules.AllowMultiplePatternsPerKind = true;
                    break;

                case BuildEffectKind.Appearance:
                    rules.Weights[Target] = rules.WeightOf(Target) * Amount;
                    break;

                case BuildEffectKind.LineMultiplier:
                    rules.LineMultiplier += Amount;
                    break;

                case BuildEffectKind.ClusterMultiplier:
                    rules.ClusterMultiplier += Amount;
                    break;
            }
        }
    }

    /// <summary>
    /// 엘리베이터에 실을 수 있는 것 하나. 승객과 부품을 한 타입으로 두는 이유는
    /// 무게·적재·규칙 변경이라는 세 축이 완전히 같기 때문이다. 다른 것은 하차뿐이고,
    /// 그건 <see cref="DestinationFloor"/> 하나로 갈린다.
    ///
    /// 이 타입은 `UnityEngine`에 의존하지 않는다. 그래야 헤드리스 테스트가 씬 없이 돈다.
    /// </summary>
    [Serializable]
    public sealed class BuildItem
    {
        public string Id;
        public string Label;
        public string Description;
        public BuildItemKind Kind;

        /// <summary>총중량에 더해지는 값.</summary>
        public float Weight;

        /// <summary>허용 중량에 더해지는 값. 짐꾼 계열만 0이 아니다.</summary>
        public float CapacityBonus;

        /// <summary>승객이 내리는 층. 0이면 하차하지 않는다(부품은 항상 0).</summary>
        public int DestinationFloor;

        /// <summary>하차 시 지급되는 요금.</summary>
        public float DisembarkReward;

        public BuildEffect[] Effects;

        /// <summary>
        /// 이 품목이 미는 빌드 방향. 노션 §6.3 「내부 빌드 방향」의 축과 대응한다.
        ///
        /// 제시 3장을 **서로 다른 축**에서 뽑는 데 쓴다
        /// (<see cref="BuildCatalog.OffersFor"/>). 무작위 3장은 같은 축이 겹치면
        /// 「고를 것이 하나뿐인 선택」이 되고, 그건 노션 §4 가 각 층에 요구하는
        /// 「최소 하나의 명확한 질문」을 만들지 못한다.
        /// </summary>
        public BuildAxis Axis;

        public bool LeavesAt(int floor)
            => Kind == BuildItemKind.Passenger && DestinationFloor > 0 && floor >= DestinationFloor;

        /// <summary>
        /// 품목 하나만 단독으로 적용한다. 두 패스를 연달아 돌리므로 **자기 자신이 켠
        /// 조건은 자기 조건부 효과에 반영된다** — 단일 품목에는 순서 문제가 없다.
        /// 여러 품목을 함께 적용할 때는 <see cref="BuildLoadout.ApplyTo"/> 를 쓴다.
        /// </summary>
        public void ApplyTo(SpinRuleSet rules)
        {
            ApplyUnconditionalTo(rules);
            ApplyConditionalTo(rules, rules);
        }

        /// <summary>1패스 — 조건 없는 효과만.</summary>
        public void ApplyUnconditionalTo(SpinRuleSet rules)
        {
            if (Effects == null) return;
            for (int i = 0; i < Effects.Length; i++)
                if (Effects[i].IsUnconditional) Effects[i].ApplyTo(rules);
        }

        /// <summary>
        /// 2패스 — 조건부 효과만. <paramref name="probe"/> 는 **1패스가 끝난 규칙**이고
        /// 조건은 거기서만 읽는다. <paramref name="rules"/> 와 같은 객체일 수 있다.
        /// </summary>
        public void ApplyConditionalTo(SpinRuleSet rules, SpinRuleSet probe)
        {
            if (Effects == null) return;
            for (int i = 0; i < Effects.Length; i++)
                if (!Effects[i].IsUnconditional && Effects[i].IsSatisfiedBy(probe))
                    Effects[i].ApplyTo(rules);
        }

        /// <summary>"정비공 (승객, 6kg) — 정상 영혼 전력 +2" 형태의 한 줄.</summary>
        public string Describe()
        {
            string kind = Kind == BuildItemKind.Passenger ? "승객" : "부품";
            string effects = EffectSummary();
            string dest = DestinationFloor > 0 ? $", {DestinationFloor}층 하차" : "";
            return $"{Label} ({kind}, {Weight:F0}kg{dest})" +
                   (string.IsNullOrEmpty(effects) ? "" : $" — {effects}");
        }

        public string EffectSummary()
        {
            if (Effects == null || Effects.Length == 0) return string.Empty;
            var parts = new string[Effects.Length];
            for (int i = 0; i < Effects.Length; i++) parts[i] = Effects[i].Describe();
            return string.Join(", ", parts);
        }
    }
}
