using System;
using System.Collections.Generic;

namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 한 스핀을 판정할 때 엔진이 읽는 규칙 다발.
    ///
    /// 승객·부품·계약·층 규칙은 전부 "이 값들을 수정하는 것"으로 표현한다. 가상 함수 체인 대신
    /// 평평한 숫자 다발을 쓰는 이유는 노션 99의 핵심 측정 로그 때문이다 — 어떤 배수가 어디서
    /// 왔는지 스핀마다 통째로 찍어야 밸런스를 반복 수정할 수 있다.
    ///
    /// 생성 순서(노션 02 "발동 순서"): 기본값 → 층 규칙 → 계약 → 승객·부품.
    /// </summary>
    [Serializable]
    public class SpinRuleSet
    {
        // ── 심볼 풀 ──

        /// <summary>종류별 추첨 가중치. 합이 1일 필요는 없다.</summary>
        public readonly Dictionary<SymbolKind, float> Weights = new Dictionary<SymbolKind, float>();

        /// <summary>정상 영혼 1개가 주는 기본 전력.</summary>
        public float NormalSoulValue = 10f;

        /// <summary>스핀마다 정상 영혼을 최소 이만큼 보장한다. 0이면 보장 없음(공정성 안전장치).</summary>
        public int GuaranteedNormalSouls = 0;

        // ── 개수 정화 ──

        /// <summary>종류별 기본 정화 최소 개수. 기본 3. 승객이 특정 저항만 2로 낮출 수 있다.</summary>
        public readonly Dictionary<SymbolKind, int> MinimumCountToPurify = new Dictionary<SymbolKind, int>();

        /// <summary>정화된 저항체 1개가 주는 기본 전력.</summary>
        public float PurifyValuePerSymbol = 6f;

        /// <summary>종류별 정화 보상 배수(계약이 여기에 곱해진다).</summary>
        public readonly Dictionary<SymbolKind, float> PurifyRewardMultiplier = new Dictionary<SymbolKind, float>();

        // ── 패턴 ──

        /// <summary>직선 3개 패턴 배수.</summary>
        public float LineMultiplier = 2f;

        /// <summary>4개 이상 직교 연결 배수.</summary>
        public float ClusterMultiplier = 3f;

        /// <summary>9칸 동일 저항 잭팟 배수.</summary>
        public float FullBoardMultiplier = 10f;

        /// <summary>종류별 패턴 배수 가산(계약의 PatternBonusAdd가 여기에 더해진다).</summary>
        public readonly Dictionary<SymbolKind, float> PatternBonusAdd = new Dictionary<SymbolKind, float>();

        /// <summary>true면 대각 인접도 연결 덩어리로 인정한다(사선 결속기).</summary>
        public bool DiagonalCountsAsConnected = false;

        /// <summary>한 저항체에 대해 여러 패턴을 중복 판정한다(업그레이드 해금).</summary>
        public bool AllowMultiplePatternsPerKind = false;

        // ── 캐스케이드 ──

        /// <summary>
        /// 캐스케이드 최대 연쇄 깊이 = 무한 루프 방지 하드 캡.
        ///
        /// 20은 `MASTER_PRD.md` §6과 `TECH_SPEC.md` §9가 못박은 값이라 밸런스 다이얼이 아니다.
        /// 낮추면 "연쇄가 20까지 간다"는 명세를 검증할 수 없고, 높이면 방지선이 사라진다.
        /// 연출이 감당 못 하는 길이가 문제라면 캡이 아니라 재생 속도로 푼다
        /// (§9 "10회 연쇄까지 시각 판독성을 유지해야 한다").
        /// </summary>
        public int MaxCascadeDepth = 20;

        /// <summary>연쇄 단계마다 전체 전력에 더해지는 배수 증분. 2연쇄부터 적용된다.</summary>
        public float CascadeMultiplierStep = 0.5f;

        /// <summary>빈칸 재충전 시 정상 영혼이 나올 확률에 더하는 보정(0~1). 장의사 계열.</summary>
        public float RefillNormalSoulBias = 0f;

        // ── 잔류 저항 ──

        /// <summary>남은 흡수체 1개가 깎는 저장 전력.</summary>
        public float AbsorberResidualPowerLoss = 8f;

        /// <summary>남은 증식체 1개가 다음 스핀 증식체 가중치에 더하는 값.</summary>
        public float ProliferatorResidualWeightAdd = 0.6f;

        /// <summary>종류별 잔류 대가 배수(계약의 ResidualPenaltyMultiplier가 여기에 곱해진다).</summary>
        public readonly Dictionary<SymbolKind, float> ResidualPenaltyMultiplier = new Dictionary<SymbolKind, float>();

        /// <summary>잔류 대가 전체를 완화하는 계수(0~1). 잔류 완화형 승객이 낮춘다.</summary>
        public float ResidualMitigation = 1f;

        // ── 조회 헬퍼 (없는 키를 기본값으로 흡수해 호출부를 단순하게 유지) ──

        public float WeightOf(SymbolKind kind)
            => Weights.TryGetValue(kind, out float w) ? (w > 0f ? w : 0f) : 0f;

        public int MinimumCountFor(SymbolKind kind)
            => MinimumCountToPurify.TryGetValue(kind, out int n) ? Math.Max(1, n) : 3;

        public float PurifyRewardFor(SymbolKind kind)
            => PurifyRewardMultiplier.TryGetValue(kind, out float m) ? m : 1f;

        public float PatternBonusFor(SymbolKind kind)
            => PatternBonusAdd.TryGetValue(kind, out float m) ? m : 0f;

        public float ResidualPenaltyFor(SymbolKind kind)
            => (ResidualPenaltyMultiplier.TryGetValue(kind, out float m) ? m : 1f) * ResidualMitigation;

        public float PatternMultiplierFor(PatternKind pattern, SymbolKind kind)
        {
            float baseMultiplier;
            switch (pattern)
            {
                case PatternKind.Scattered: baseMultiplier = 1f; break;
                case PatternKind.Line:      baseMultiplier = LineMultiplier; break;
                case PatternKind.Cluster:   baseMultiplier = ClusterMultiplier; break;
                case PatternKind.FullBoard: baseMultiplier = FullBoardMultiplier; break;
                default:                    return 0f;
            }
            return baseMultiplier + PatternBonusFor(kind);
        }

        /// <summary>프로토타입 고정안의 기본 규칙(계약·승객 적용 전).</summary>
        public static SpinRuleSet CreateDefault()
        {
            var rules = new SpinRuleSet();
            rules.Weights[SymbolKind.NormalSoul]   = 5f;
            rules.Weights[SymbolKind.Absorber]     = 2.5f;
            rules.Weights[SymbolKind.Proliferator] = 2.5f;

            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds)
            {
                rules.MinimumCountToPurify[kind]     = 3;
                rules.PurifyRewardMultiplier[kind]   = 1f;
                rules.PatternBonusAdd[kind]          = 0f;
                rules.ResidualPenaltyMultiplier[kind] = 1f;
            }
            return rules;
        }

        /// <summary>계약을 규칙 다발에 적용한다. 세 값이 함께 움직이는 지점은 여기 한 곳뿐이다.</summary>
        public void Apply(in ResistanceContract contract)
        {
            if (contract.IsNone) return;
            SymbolKind t = contract.Target;

            Weights[t] = WeightOf(t) * contract.AppearanceMultiplier;
            PurifyRewardMultiplier[t]    = PurifyRewardFor(t) * contract.PurifyRewardMultiplier;
            PatternBonusAdd[t]           = PatternBonusFor(t) + contract.PatternBonusAdd;
            ResidualPenaltyMultiplier[t] =
                (ResidualPenaltyMultiplier.TryGetValue(t, out float r) ? r : 1f) * contract.ResidualPenaltyMultiplier;
        }

        public SpinRuleSet Clone()
        {
            var copy = new SpinRuleSet
            {
                NormalSoulValue               = NormalSoulValue,
                GuaranteedNormalSouls         = GuaranteedNormalSouls,
                PurifyValuePerSymbol          = PurifyValuePerSymbol,
                LineMultiplier                = LineMultiplier,
                ClusterMultiplier             = ClusterMultiplier,
                FullBoardMultiplier           = FullBoardMultiplier,
                DiagonalCountsAsConnected     = DiagonalCountsAsConnected,
                AllowMultiplePatternsPerKind  = AllowMultiplePatternsPerKind,
                MaxCascadeDepth               = MaxCascadeDepth,
                CascadeMultiplierStep         = CascadeMultiplierStep,
                RefillNormalSoulBias          = RefillNormalSoulBias,
                AbsorberResidualPowerLoss     = AbsorberResidualPowerLoss,
                ProliferatorResidualWeightAdd = ProliferatorResidualWeightAdd,
                ResidualMitigation            = ResidualMitigation,
            };
            foreach (var kv in Weights)                   copy.Weights[kv.Key] = kv.Value;
            foreach (var kv in MinimumCountToPurify)      copy.MinimumCountToPurify[kv.Key] = kv.Value;
            foreach (var kv in PurifyRewardMultiplier)    copy.PurifyRewardMultiplier[kv.Key] = kv.Value;
            foreach (var kv in PatternBonusAdd)           copy.PatternBonusAdd[kv.Key] = kv.Value;
            foreach (var kv in ResidualPenaltyMultiplier) copy.ResidualPenaltyMultiplier[kv.Key] = kv.Value;
            return copy;
        }
    }
}
