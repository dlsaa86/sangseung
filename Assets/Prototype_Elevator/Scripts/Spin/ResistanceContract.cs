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

        /// <summary>
        /// **현재 빌드와 관련된 시너지 한 줄** (`UP-CONTRACT-05`).
        ///
        /// `NotionSyncReport.md:166` 이 계약 UI 필수 4정보를 못박는다 — 등장 확률 증가폭 ·
        /// 정화 보상 증가폭 · 남았을 때의 대가 · **현재 빌드 관련 시너지 한 줄**.
        /// 앞의 셋은 <see cref="Preview"/> 가 이미 낸다. 넷째가 없었다.
        ///
        /// **문구를 지어내지 않는다.** 적재된 부품의 효과 중 **이 계약과 같은 저항을
        /// 겨냥한 것**만 세어 그대로 말한다. 규칙에 없는 관계를 쓰면 플레이어가 UI 를
        /// 근거로 잘못된 선택을 하고, 그건 정보 공개가 아니라 오정보다.
        ///
        /// 대상 없는 효과(연쇄 증분·잔류 완화 등)는 세지 않는다 — 어느 계약을 고르든
        /// 똑같이 걸리므로 「이 계약과의」 시너지가 아니다. 그것까지 세면 모든 계약이
        /// 같은 줄을 달게 되어 비교에 쓸모가 없어진다.
        /// </summary>
        public string SynergyWith(Build.BuildLoadout loadout)
        {
            if (IsNone) return "적재와 무관 — 규칙을 바꾸지 않는다";
            if (loadout == null || loadout.Count == 0) return "적재 없음 — 시너지 없다";

            int matched = 0;
            string first = null;
            System.Collections.Generic.IReadOnlyList<Build.BuildItem> items = loadout.Items;
            for (int i = 0; i < items.Count; i++)
            {
                Build.BuildEffect[] effects = items[i].Effects;
                if (effects == null) continue;
                for (int e = 0; e < effects.Length; e++)
                {
                    // 대상이 이 계약의 저항과 같은 것만. `SymbolKind.Empty` 는 「대상 없음」이다.
                    if (effects[e].Target != Target) continue;
                    matched++;
                    if (first == null) first = items[i].Label;
                }
            }

            if (matched == 0)
                return $"적재 {loadout.Count}칸 중 {Target.DisplayName()} 를 겨냥한 것 없음 — 시너지 없다";
            if (matched == 1)
                return $"{first} 이(가) 같은 {Target.DisplayName()} 를 겨냥한다 — 효과가 겹친다";
            return $"{first} 외 {matched - 1}개가 같은 {Target.DisplayName()} 를 겨냥한다 — 효과가 겹친다";
        }
    }
}
