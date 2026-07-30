using System;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Build
{
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

    /// <summary>규칙 다발에 가하는 변경 하나. 대상이 필요 없는 종류는 <see cref="Target"/>를 무시한다.</summary>
    [Serializable]
    public struct BuildEffect
    {
        public BuildEffectKind Kind;
        public SymbolKind Target;
        public float Amount;

        public static BuildEffect Of(BuildEffectKind kind, float amount)
            => new BuildEffect { Kind = kind, Target = SymbolKind.Empty, Amount = amount };

        public static BuildEffect Of(BuildEffectKind kind, SymbolKind target, float amount)
            => new BuildEffect { Kind = kind, Target = target, Amount = amount };

        /// <summary>사고 기록기와 디버그 패널이 그대로 찍는 한 줄.</summary>
        public string Describe()
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

        public bool LeavesAt(int floor)
            => Kind == BuildItemKind.Passenger && DestinationFloor > 0 && floor >= DestinationFloor;

        public void ApplyTo(SpinRuleSet rules)
        {
            if (Effects == null) return;
            for (int i = 0; i < Effects.Length; i++) Effects[i].ApplyTo(rules);
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
