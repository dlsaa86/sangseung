using System;

namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 층 시작에 한 번 선택하는 저항 계약. 매 스핀의 표적 선택을 대체하는 핵심 입력이다.
    ///
    /// 계약은 반드시 세 값을 **함께** 움직인다(노션 01 "층 시작 — 저항 계약"):
    ///   1) 해당 저항체의 등장 확률 증가
    ///   2) 해당 저항체의 정화 보상·패턴 배수 증가
    ///   3) 정화하지 못했을 때의 잔류 대가 증가
    /// 셋 중 하나만 바꾸면 계약이 "그냥 좋은 버프"가 되어 선택 고민이 사라진다.
    /// </summary>
    [Serializable]
    public struct ResistanceContract
    {
        /// <summary>계약 대상 저항체.</summary>
        public SymbolKind Target;

        /// <summary>UI에 노출되는 계약 이름. 예: "흡수체 계약".</summary>
        public string Label;

        /// <summary>대상 저항체의 출현 가중치에 곱하는 값. 1보다 크다.</summary>
        public float AppearanceMultiplier;

        /// <summary>대상 저항체의 정화 전력에 곱하는 값. 1보다 크다.</summary>
        public float PurifyRewardMultiplier;

        /// <summary>대상 저항체의 패턴 배수에 더하는 값.</summary>
        public float PatternBonusAdd;

        /// <summary>정화되지 않고 남은 대상 저항체의 잔류 대가에 곱하는 값. 1보다 크다.</summary>
        public float ResidualPenaltyMultiplier;

        /// <summary>계약 없음(1층 튜토리얼 등). 모든 배수가 1이고 대상이 없다.</summary>
        public static ResistanceContract None => new ResistanceContract
        {
            Target                    = SymbolKind.Empty,
            Label                     = "계약 없음",
            AppearanceMultiplier      = 1f,
            PurifyRewardMultiplier    = 1f,
            PatternBonusAdd           = 0f,
            ResidualPenaltyMultiplier = 1f,
        };

        public bool IsNone => Target == SymbolKind.Empty;

        /// <summary>
        /// 선택 전에 플레이어에게 보여줘야 하는 요약. 노션 03 "위험 계약은 선택 전에
        /// 등장률·보상·잔류 대가를 공개한다"는 규칙 때문에 UI가 아니라 데이터가 소유한다.
        /// </summary>
        public string Preview()
        {
            if (IsNone) return "계약 없음 — 출현률·보상·대가 변화 없음";
            string name = Target.DisplayName();
            return $"{name} 출현 ×{AppearanceMultiplier:0.##} / 정화 보상 ×{PurifyRewardMultiplier:0.##}" +
                   (PatternBonusAdd > 0f ? $" / 패턴 +{PatternBonusAdd:0.##}" : string.Empty) +
                   $" / 잔류 대가 ×{ResidualPenaltyMultiplier:0.##}";
        }
    }
}
