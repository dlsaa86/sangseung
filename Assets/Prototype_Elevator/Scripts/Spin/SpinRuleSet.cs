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

        /// <summary>
        /// 정상 영혼 1개가 주는 기본 전력.
        ///
        /// 10 → 14. 인접 규칙을 켜면서 전력 산출이 줄어든 만큼 되돌린 값이다.
        ///
        /// **처음에는 9개 시드로 튜닝하다가 잡음에 과적합했다.** 14.0·14.3·14.7 이
        /// 9개 시드에서는 4·4·6 완주로 갈렸는데, 20개 시드로 재니 셋 다 13/20 이었다.
        /// 표본이 작으면 없는 차이가 보인다. 20개 시드가 판단 기준이다.
        ///
        /// 20개 시드 완주율 (무적재 보수 정책)
        ///   옛 규칙(흩어짐 인정·전체 제거)  17/20 (85%)
        ///   현재 규칙 · 영혼 14.0            13/20 (65%)
        ///   현재 규칙 · 영혼 12.5            12/20 (60%)
        ///   현재 규칙 · 영혼 11.0             6/20 (30%)
        ///
        /// 옛 규칙보다 **어렵다**. 붙어 있으면 모양을 가리지 않고 인정하지만,
        /// 떨어진 같은 문양을 더는 같이 지우지 않기 때문이다.
        /// 85% 로 되돌리려면 이 값을 올리면 된다 — 그건 난이도 결정이다.
        /// </summary>
        public float NormalSoulValue = 14f;

        /// <summary>스핀마다 정상 영혼을 최소 이만큼 보장한다. 0이면 보장 없음(공정성 안전장치).</summary>
        public int GuaranteedNormalSouls = 0;

        // ── 개수 정화 ──

        /// <summary>종류별 기본 정화 최소 개수. 기본 3. 승객이 특정 저항만 2로 낮출 수 있다.</summary>
        public readonly Dictionary<SymbolKind, int> MinimumCountToPurify = new Dictionary<SymbolKind, int>();

        /// <summary>정화된 저항체 1개가 주는 기본 전력. 6 → 8.4 (영혼과 같은 1.4배).</summary>
        public float PurifyValuePerSymbol = 8.4f;

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

        /// <summary>
        /// true면 정화에 **인접**을 요구한다 — 직선(3연속) 또는 연결 덩어리(4개 이상)만
        /// 인정하고, 판 곳곳에 흩어진 개수만으로는 정화되지 않는다.
        ///
        /// 원래는 개수만 넘으면 `Scattered`("기본 정화")가 성립했다. 판 반대쪽 두 칸과
        /// 구석 한 칸이 서로 아무 관계도 없이 함께 사라지는 그림이라, 무엇이 왜 터졌는지
        /// 설명되지 않는다. 3×3 에서 직선과 연결 덩어리는 본래 인접하므로,
        /// 이 스위치는 "붙어 있는 것만 터진다"는 규칙이 된다.
        ///
        /// **기본값이며 교체 대상이다**(`ASSUMPTION_LOG` A-20260731-07).
        /// `Scattered`는 헛스핀을 줄이는 안전망이기도 했으므로 정화율이 내려간다.
        /// 되돌리려면 이 값을 false 로 두면 된다.
        /// </summary>
        public bool RequireAdjacencyToPurify = true;

        /// <summary>
        /// true면 정화가 **패턴에 참여한 칸만** 지운다. false면 그 종류의 칸을 판에서 전부 지운다.
        ///
        /// 판정에 인접을 요구해도(`RequireAdjacencyToPurify`) 제거가 인접을 무시하면
        /// 화면에서는 여전히 "안 붙은 것도 같이 터진다". 직선 3개가 성립하는 순간
        /// 판 반대쪽 구석의 같은 문양까지 사라졌다. 두 스위치는 함께 켜져야 뜻이 산다.
        ///
        /// **기본값이며 교체 대상이다**(`ASSUMPTION_LOG` A-20260731-07).
        /// </summary>
        public bool PurifyOnlyPatternCells = true;

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

        /// <summary>
        /// 🔴 **잔류 장부에서 지우는 개수** (PD-30 검침원, 2026-08-06). 0이면 옛 게임.
        ///
        /// 판을 바꾸지 않는다 — 남은 저항은 그대로 있고 **대가만** 면제된다.
        /// 판에서 지우면 다음 스핀의 저항 밀도가 내려가는데, 이 게임은 저항 밀도를
        /// 보상하므로(`docs/runtime/PENDING_DECISIONS.md` PD-29 §원인 ②) 그건
        /// 「완화」가 아니라 **약화**가 된다. 방향이 반대인 효과를 붙이지 않는다.
        /// </summary>
        public int ResidualForgiveCount = 0;

        /// <summary>
        /// 🔴 **연쇄가 이어질 때 추가로 다시 뒤집는 칸 수** (PD-30 연쇄 코일, 2026-08-06).
        /// 0이면 난수를 **한 번도 뽑지 않으므로** 옛 게임과 비트 단위로 같다.
        ///
        /// 이 값은 배수가 아니라 **판정 규칙**을 바꾼다 — 판이 한 번 더 무너질 기회를 산다.
        /// PD-30 의 진단이 「그 층위의 품목이 사선 결속기 하나뿐」이었고, 이것이 둘째다.
        /// </summary>
        public int ExtraCascadeRerollCells = 0;

        /// <summary>
        /// 층 규칙이 정한 연쇄 증분의 **기준선**. 적재가 이 값을 밀어 올렸는지 판정하는
        /// 데만 쓴다(<see cref="Build.BuildEffectCondition.CascadeAmplified"/>).
        ///
        /// 상수와 비교하지 않는 이유: `SpinBalanceProfile` 이 층별로 다른 증분을 줄 수 있어
        /// 「기본값보다 큰가」가 「적재가 올렸는가」와 같지 않다. 기준선을 들고 있어야
        /// 조건이 프로파일 변경에 흔들리지 않는다.
        /// </summary>
        public float BaseCascadeMultiplierStep = 0.5f;

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

        /// <summary>
        /// 🔴 **잔류 대가의 볼록도** (2026-08-05). 0이면 옛 선형식과 **비트 단위로 같다.**
        ///
        /// ## 왜 생겼나 — 세 축이 같은 결함을 공유했다
        ///
        /// 2026-08-05 실측에서 계약·적재·빌드 **세 축 전부**가 「밀어붙이면 항상 이긴다」로
        /// 끝났다. 계약은 선택지가 있는 **6개 층 전부**에서 클리어율이 인덱스 순서대로
        /// 단조 증가했다 — 즉 「계약 없음」이 항상 최악이고 고를 수 있으면 항상 더 센 계약이
        /// 나았다. 우연일 확률 `(1/2)³ × (1/6)³ ≈ 0.06%`
        /// (`docs/runtime/CONTRACT_DOMINANCE_AUDIT.md`).
        ///
        /// 공통 원인은 **모양**이었다. 계약의 이득(출현·보상·패턴 배수)은 캐스케이드를 타고
        /// **곱셈으로 복리**가 되는데, 대가(잔류 대가 ×2)는 남은 개수에 **선형**이었다.
        /// 그래서 판이 나빠질수록 대가가 커져야 할 자리에서 대가가 같은 기울기로만 늘었고,
        /// 저울이 한쪽으로만 기울었다.
        ///
        /// ## 무엇을 바꾸나
        ///
        /// 남긴 저항 하나하나가 아니라 **얼마나 많이 남겼는가**가 대가를 정한다.
        /// 두 개 남기는 것과 여섯 개 남기는 것은 세 배가 아니라 그 이상이어야 한다 —
        /// 노션 §7 이 위험을 「Stable → Strain → Critical → Collapse 처럼 단계적으로
        /// 증가」한다고 적어 둔 것이 이 모양이다. 그래야 노션 §4 의
        /// 「잔류 저항을 지금 제거할 것인가, 다음 실행의 재료로 남길 것인가」가
        /// 실제 질문이 된다 — 선형이면 답은 항상 「남긴다」다.
        ///
        /// ⚠ **이 값은 밸런스 변경이고 되돌릴 수 있다.** 0으로 두면 옛 게임이다.
        /// 근거와 되돌리는 법은 `docs/ASSUMPTION_LOG.md` 의 2026-08-05 항목에 있다.
        /// </summary>
        public float ResidualEscalation = DefaultResidualEscalation;

        /// <summary>2026-08-05 실측으로 정한 값. 0이 옛 동작이다.</summary>
        public const float DefaultResidualEscalation = 1f;


        /// <summary>
        /// 남은 저항 개수를 **대가 단위**로 바꾼다. 이 함수가 잔류 대가의 모양을 정한다.
        ///
        /// <paramref name="escalation"/> 이 0이면 개수를 그대로 돌려준다 — 옛 선형식이다.
        /// 0보다 크면 볼록 항이 붙고, 판 전체(9칸)를 남겼을 때 그 항이 정확히
        /// `escalation × 9` 가 되도록 정규화한다. 즉 `escalation == 1` 이면
        /// **최악의 판에서 대가가 두 배**가 되고, 그 사이는 부드럽게 오른다.
        ///
        /// 판정을 두 벌로 두지 않으려고 `static` 으로 뺐다 — 흡수체 손실과 (나중에 붙을 수
        /// 있는) 다른 잔류 대가가 같은 모양을 써야 한다.
        /// </summary>
        public static float ResidualLoadOf(int count, float escalation)
        {
            if (count <= 0) return 0f;
            if (escalation <= 0f) return count;
            float c = count;
            return c + escalation * c * c / SpinBoard.Cells;
        }

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
            => CreateDefault(Data.Profiles.SpinBalanceProfile.DefaultSnapshot);

        /// <summary>
        /// 밸런스 수치를 밖에서 받아 규칙 다발을 만든다 (`UP-TECH-09` ①③).
        ///
        /// 인자 없는 판본은 코드 프리셋으로 위임하므로 **동작이 같다.** 여기 있던
        /// 리터럴(5 / 2.5 / 2.5 / 3)은 <see cref="Data.Profiles.SpinBalanceProfile"/> 의
        /// 프리셋 상수로 옮겼고, 두 곳이 갈라지지 않도록 테스트가 대조한다.
        ///
        /// 패턴 배수 셋과 잔류·연쇄 값도 여기서 덮어쓴다. 필드 초기값으로만 두면
        /// 에셋을 물려도 그 넷은 코드 값을 계속 쓴다 — 「값을 옮겼는데 일부만 따라오는」
        /// 절반짜리 데이터화가 이 저장소의 반복 실패다.
        /// </summary>
        public static SpinRuleSet CreateDefault(Data.Profiles.SpinBalanceSnapshot balance)
        {
            var rules = new SpinRuleSet();
            rules.Weights[SymbolKind.NormalSoul]   = balance.WeightNormalSoul;
            rules.Weights[SymbolKind.Absorber]     = balance.WeightAbsorber;
            rules.Weights[SymbolKind.Proliferator] = balance.WeightProliferator;

            rules.LineMultiplier                 = balance.LineMultiplier;
            rules.ClusterMultiplier              = balance.ClusterMultiplier;
            rules.FullBoardMultiplier            = balance.FullBoardMultiplier;
            rules.CascadeMultiplierStep          = balance.CascadeMultiplierStep;
            rules.BaseCascadeMultiplierStep      = balance.CascadeMultiplierStep;
            rules.AbsorberResidualPowerLoss      = balance.AbsorberResidualPowerLoss;
            rules.ProliferatorResidualWeightAdd  = balance.ProliferatorResidualWeightAdd;

            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds)
            {
                rules.MinimumCountToPurify[kind]     = balance.MinimumCountToPurify;
                rules.PurifyRewardMultiplier[kind]   = 1f;
                rules.PatternBonusAdd[kind]          = 0f;
                rules.ResidualPenaltyMultiplier[kind] = 1f;
            }
            return rules;
        }

        /// <summary>계약을 규칙 다발에 적용한다. 세 값이 함께 움직이는 지점은 여기 한 곳뿐이다.</summary>
        public void Apply(in ResistanceContract contract) => Apply(in contract, null);

        /// <summary>
        /// 🔴 **적재를 읽는 계약** (PD-29 안 C, 2026-08-06).
        ///
        /// <paramref name="loadout"/> 이 `null` 이거나 계약이 조건을 안 갖고 있으면
        /// 세는 수가 0이라 **위 오버로드와 비트 단위로 같다.** 시뮬레이터·성능 프로브처럼
        /// 적재가 없는 호출부는 한 자리도 안 바뀐다.
        ///
        /// ⚠ **여기서 규칙을 읽어 조건을 판정하지 않는다.** 적재의 정적 카탈로그만 센다
        /// (<see cref="ResistanceContract.SynergyMatches"/>). 계약은 적재보다 **먼저**
        /// 적용되므로 이 시점의 규칙에는 적재가 아직 없고, 순서를 뒤집으면 계약의 곱셈이
        /// 승객의 가산 위에 얹혀 같은 조합이 다른 값을 낸다
        /// (`BuildLoadout.ApplyTo` 주석).
        /// </summary>
        public void Apply(in ResistanceContract contract, Build.BuildLoadout loadout)
        {
            if (contract.IsNone) return;
            SymbolKind t = contract.Target;

            int matches = contract.SynergyMatches(loadout);
            float purifyMultiplier = contract.PurifyRewardMultiplier +
                                     matches * contract.SynergyPurifyRewardPerMatch;
            float patternAdd = contract.PatternBonusAdd +
                               matches * contract.SynergyPatternBonusPerMatch;
            // 대가는 **가벼워지기만** 하고 1 아래로는 안 내려간다 — 계약이 잔류 대가를
            // 깎아 주는 물건이 되면 「세 값이 함께 움직인다」가 무너진다.
            float residualMultiplier = contract.ResidualPenaltyMultiplier -
                                       matches * contract.SynergyResidualReliefPerMatch;
            if (residualMultiplier < 1f) residualMultiplier = 1f;

            Weights[t] = WeightOf(t) * contract.AppearanceMultiplier;
            PurifyRewardMultiplier[t]    = PurifyRewardFor(t) * purifyMultiplier;
            PatternBonusAdd[t]           = PatternBonusFor(t) + patternAdd;
            ResidualPenaltyMultiplier[t] =
                (ResidualPenaltyMultiplier.TryGetValue(t, out float r) ? r : 1f) * residualMultiplier;
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
                RequireAdjacencyToPurify      = RequireAdjacencyToPurify,
                PurifyOnlyPatternCells        = PurifyOnlyPatternCells,
                MaxCascadeDepth               = MaxCascadeDepth,
                CascadeMultiplierStep         = CascadeMultiplierStep,
                RefillNormalSoulBias          = RefillNormalSoulBias,
                AbsorberResidualPowerLoss     = AbsorberResidualPowerLoss,
                ProliferatorResidualWeightAdd = ProliferatorResidualWeightAdd,
                ResidualMitigation            = ResidualMitigation,
                // ⚠ 아래 셋도 같은 이유로 복제한다 — `PrepareRules` 가 매 스핀 `Clone()` 한다.
                ResidualForgiveCount          = ResidualForgiveCount,
                ExtraCascadeRerollCells       = ExtraCascadeRerollCells,
                BaseCascadeMultiplierStep     = BaseCascadeMultiplierStep,
                // ⚠ 이 줄이 빠지면 잔류 대가가 스핀마다 기본값으로 되돌아간다.
                //   `SpinEngine.PrepareRules` 가 매 스핀 `Clone()` 을 부르기 때문이다.
                ResidualEscalation            = ResidualEscalation,
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
