using System;

namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 🔴 **계약의 추가 이득이 언제 붙는가** (PD-29 안 C, 2026-08-06).
    ///
    /// ## 왜 생겼나
    ///
    /// 2026-08-05 실측(`docs/runtime/CONTRACT_DOMINANCE_AUDIT.md`): 7·8·9층에서
    /// **적재 없음·흡수체 빌드·증식체 빌드 셋 다 증식체 계약이 1등**이었다.
    /// 노션 §4 가 각 층에 요구하는 질문은 「**현재 빌드에 가장 유리한** 저항 계약은
    /// 무엇인가」인데, 어느 빌드로도 답이 같으면 그 질문은 데이터로 성립하지 않는다.
    ///
    /// 격차를 줄이는 것으로는 이 줄이 바뀌지 않는다 — **격차가 0이어도 답이 하나면
    /// 선택이 아니다.** 필요한 것은 값이 아니라 **계약이 적재를 읽는 경로**였다.
    /// <see cref="Ascend.Prototype.Build.BuildEffectCondition"/> 이 품목 사이에
    /// 시너지 짝 0개 → 4개를 만든 것과 **같은 처방**이다.
    ///
    /// ## 무엇을 읽는가 — **규칙이 아니라 적재 목록**이다
    ///
    /// 조건은 적재된 품목이 **선언한 효과**를 센다. 계약 적용 시점의 규칙 다발을
    /// 읽지 않는다. 발동 순서가 「기본값 → 층 → 계약 → 승객·부품」이라 계약이 적용될
    /// 때 적재는 아직 규칙에 반영돼 있지 않기 때문이다. 순서를 뒤집어 해결하지 않은
    /// 이유는 <see cref="Ascend.Prototype.Build.BuildLoadout.ApplyTo"/> 주석에 있다 —
    /// 계약의 곱셈이 승객의 가산 위에 얹히면 같은 조합이 다른 값을 낸다.
    ///
    /// 카탈로그(정적 데이터)만 읽으므로 **순서 무관**이 그대로 유지된다.
    ///
    /// ## UI 가 이미 하던 말을 실제로 만든다
    ///
    /// <see cref="ResistanceContract.SynergyWith"/> 는 예전부터 「N개가 같은 저항을
    /// 겨냥한다 — 효과가 겹친다」를 화면에 찍고 있었다. **겹치지 않았다.** 규칙에는
    /// 그런 경로가 없었으므로 그 줄은 사실이 아닌 정보였다. 세는 함수를 한 벌로 묶어
    /// 화면의 문장과 판정식이 **같은 수**를 쓴다.
    /// </summary>
    public enum ContractSynergyCondition
    {
        /// <summary>추가 이득 없음. **기본값이므로 옛 계약 초기화 구문은 한 자리도 안 바뀐다.**</summary>
        Never = 0,

        /// <summary>적재 효과 중 이 계약의 저항을 겨냥한 것이 있을 때. 겨냥한 개수만큼 붙는다.</summary>
        TargetedByLoadout,

        /// <summary>적재가 잔류 대가를 키워 뒀을 때(과수확 계열).</summary>
        ResidualAmplifiedByLoadout,

        /// <summary>적재가 잔류 대가를 완화해 뒀을 때(감쇠기 계열).</summary>
        ResidualMitigatedByLoadout,
    }

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

        // ── PD-29 안 C — 빌드 의존 (2026-08-06) ─────────────────────────────
        //
        // 아래 셋이 기본값(Never / 0 / 0)이면 이 구조체는 **개편 전과 비트 단위로
        // 같다.** 되돌리는 방법이 "계수를 0으로"인 것은 의도된 설계다 —
        // 2026-08-05 개편 셋(볼록도·조건 축·잔류)이 전부 같은 성질을 갖는다.

        /// <summary>추가 이득이 붙는 조건. <see cref="ContractSynergyCondition.Never"/> 가 기본값.</summary>
        public ContractSynergyCondition SynergyCondition;

        /// <summary>조건이 맞은 **효과 하나당** 정화 보상 배수에 더한다. 0이면 옛 게임.</summary>
        public float SynergyPurifyRewardPerMatch;

        /// <summary>
        /// 조건이 맞은 효과 하나당 패턴 배수에 더한다. 0이면 옛 게임.
        ///
        /// ⚠ **패턴은 캐스케이드마다 다시 곱해져 복리로 붙는다.** 증식체 계약의
        /// `PatternBonusAdd` 가 1.0 → 0.7 로 내려간 것이 정확히 그 이유다
        /// (<see cref="PrototypeCurriculum.ProliferatorContract"/> 주석).
        /// 시너지 이득은 그래서 정화 보상 쪽에 싣고 이 값은 0으로 둔다.
        /// </summary>
        public float SynergyPatternBonusPerMatch;

        /// <summary>
        /// 🔴 조건이 맞은 효과 하나당 **잔류 대가 배수를 깎는다.** 0이면 옛 게임.
        ///
        /// ## 왜 이 축이 필요했나 — 실측이 시켰다
        ///
        /// 정화 보상 계수만 0 → 0.30 → 0.45 → 0.60 으로 올려 봤더니 흡수체 빌드의
        /// 완주율이 20.00 → 20.17 → 20.17 → 20.17% 로 **거의 움직이지 않았다.**
        /// 원인은 계수가 아니라 적재였다 — 흡수체 축의 핵심 품목 계측 기사는
        /// `DestinationFloor = 5` 라 **7층 전에 내린다.** 계약 순위를 재는 7·8·9층에는
        /// 잔류 감쇠기 하나만 남아 세는 개수가 1이다. 이득 축만으로는 상한에 닿지 못한다.
        ///
        /// ## 그래서 대가 쪽을 연다
        ///
        /// 잔류 대가는 2026-08-05 개편으로 **볼록**해졌다 — 많이 남길수록 가파르게
        /// 커진다. 그래서 같은 계수라도 대가 쪽이 이득 쪽보다 크게 움직인다.
        /// 뜻도 정확하다: 「이 저항을 다룰 줄 아는 빌드에게는 이 계약의 대가가 가볍다」.
        ///
        /// ⚠ **1 미만으로는 안 내려간다.** 계약이 잔류 대가를 **깎아 주는** 물건이
        /// 되면 세 값이 함께 움직인다는 계약의 정의가 무너진다.
        /// </summary>
        public float SynergyResidualReliefPerMatch;

        /// <summary>
        /// 몇 개까지 세는가. 상한이 없으면 적재가 늘수록 계약이 무한히 세지고,
        /// 그러면 「빌드에 맞는 계약」이 아니라 「많이 실은 쪽이 이긴다」가 된다.
        /// </summary>
        public const int SynergyMatchCap = 3;

        /// <summary>계약 없음(1층 튜토리얼 등). 모든 배수가 1이고 대상이 없다.</summary>
        public static ResistanceContract None => new ResistanceContract
        {
            Target                    = SymbolKind.Empty,
            Label                     = "계약 없음",
            AppearanceMultiplier      = 1f,
            PurifyRewardMultiplier    = 1f,
            PatternBonusAdd           = 0f,
            ResidualPenaltyMultiplier = 1f,
            SynergyCondition          = ContractSynergyCondition.Never,
        };

        public bool IsNone => Target == SymbolKind.Empty;

        /// <summary>추가 이득이 실제로 붙을 수 있는 계약인가. 둘 다 0이면 조건을 세지 않는다.</summary>
        public bool HasSynergyBonus =>
            SynergyCondition != ContractSynergyCondition.Never &&
            (SynergyPurifyRewardPerMatch != 0f || SynergyPatternBonusPerMatch != 0f ||
             SynergyResidualReliefPerMatch != 0f);

        /// <summary>
        /// 조건에 맞는 적재 효과의 개수. <see cref="SynergyMatchCap"/> 에서 잘린다.
        ///
        /// **화면과 판정식이 같은 수를 쓴다** — <see cref="SynergyWith"/> 도 이 함수를 부른다.
        /// 두 벌로 두면 UI 가 「3개가 겹친다」고 쓰는데 판정은 2개만 세는 상태가 생기고,
        /// 그건 정보 공개가 아니라 오정보다(노션 §3.2 「선택 전에 공개한다」).
        /// </summary>
        public int SynergyMatches(Build.BuildLoadout loadout)
        {
            if (!HasSynergyBonus) return 0;
            int raw = CountMatches(loadout, out _);
            return raw > SynergyMatchCap ? SynergyMatchCap : raw;
        }

        /// <summary>
        /// 조건에 해당하는 효과를 센다. **정적 카탈로그만 읽는다** — 규칙 다발을 보지 않으므로
        /// 계약을 적재보다 먼저 적용하는 순서가 유지된다.
        ///
        /// 조건부 효과도 **선언돼 있으면 센다.** 그 효과가 이번 층에 실제로 발동하는지는
        /// 규칙을 봐야 알 수 있고, 규칙을 보는 순간 순서 무관이 깨진다. 「겨냥하고 있다」와
        /// 「이번에 터진다」를 구분하지 않는 대신, <see cref="SynergyWith"/> 가 화면에
        /// 똑같이 「겨냥한다」로 쓴다 — 판정과 문장이 어긋나지 않는 쪽을 골랐다.
        /// </summary>
        private int CountMatches(Build.BuildLoadout loadout, out string firstLabel)
        {
            firstLabel = null;
            if (loadout == null || loadout.Count == 0) return 0;

            int matched = 0;
            System.Collections.Generic.IReadOnlyList<Build.BuildItem> items = loadout.Items;
            for (int i = 0; i < items.Count; i++)
            {
                Build.BuildEffect[] effects = items[i].Effects;
                if (effects == null) continue;
                for (int e = 0; e < effects.Length; e++)
                {
                    if (!Matches(in effects[e])) continue;
                    matched++;
                    if (firstLabel == null) firstLabel = items[i].Label;
                }
            }
            return matched;
        }

        private bool Matches(in Build.BuildEffect effect)
        {
            switch (SynergyCondition)
            {
                case ContractSynergyCondition.TargetedByLoadout:
                    // `SymbolKind.Empty` 는 「대상 없음」이다. 대상 없는 효과(연쇄 증분·
                    // 잔류 완화 등)를 세면 **모든 계약이 같은 수를 받아** 비교에 쓸모가 없어진다.
                    return effect.Target == Target && Target != SymbolKind.Empty;
                case ContractSynergyCondition.ResidualAmplifiedByLoadout:
                    return effect.Kind == Build.BuildEffectKind.ResidualMitigation && effect.Amount > 1f;
                case ContractSynergyCondition.ResidualMitigatedByLoadout:
                    return effect.Kind == Build.BuildEffectKind.ResidualMitigation && effect.Amount < 1f;
                default:
                    return false;
            }
        }

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

            // 🔴 2026-08-06: 세는 곳이 한 벌이 됐다. 예전에는 이 함수가 자기 루프로
            // 세고 판정식은 아무것도 안 봤다 — 화면은 「효과가 겹친다」고 썼는데
            // 규칙에는 겹칠 경로가 없었다. 이제 같은 수가 실제 이득이 된다.
            int raw = CountMatches(loadout, out string first);
            if (raw == 0)
                return $"적재 {loadout.Count}칸 중 {Describe()} 없음 — 시너지 없다";

            int counted = SynergyMatches(loadout);
            string capped = raw > counted ? $"({counted}개까지 센다) " : string.Empty;
            string gain = counted > 0 && SynergyPurifyRewardPerMatch != 0f
                ? $" — 정화 보상 +{counted * SynergyPurifyRewardPerMatch:0.##}"
                : " — 효과가 겹친다";

            return raw == 1
                ? $"{first} 이(가) {Describe()} {capped}{gain}"
                : $"{first} 외 {raw - 1}개가 {Describe()} {capped}{gain}";
        }

        /// <summary>조건을 사람이 읽는 한 마디로. 화면 문장과 판정이 같은 말을 쓰게 한다.</summary>
        private string Describe()
        {
            switch (SynergyCondition)
            {
                case ContractSynergyCondition.TargetedByLoadout:
                    return $"같은 {Target.DisplayName()} 를 겨냥한다";
                case ContractSynergyCondition.ResidualAmplifiedByLoadout:
                    return "잔류 대가를 키운다";
                case ContractSynergyCondition.ResidualMitigatedByLoadout:
                    return "잔류 대가를 완화한다";
                default:
                    return "이 계약과 맞물리는 것";
            }
        }
    }
}
