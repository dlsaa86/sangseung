using System;
using System.Collections.Generic;

namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 한 층이 플레이어에게 던지는 질문. 노션 03 "레벨 디자인 정의"의 "각 층은 한 가지 핵심
    /// 질문을 가져야 한다"를 타입으로 강제한다 — 질문 없는 층은 만들 수 없다.
    /// </summary>
    [Serializable]
    public struct FloorPlan
    {
        /// <summary>층 번호(1부터).</summary>
        public int Floor;

        /// <summary>이 층에서 플레이어가 배울 것 한 문장. 비어 있으면 설계 미완성으로 본다.</summary>
        public string CoreQuestion;

        /// <summary>이 층에 처음 소개되는 규칙. 층마다 하나만 허용한다(노션 03).</summary>
        public string TeachesRule;

        /// <summary>요구 전력.</summary>
        public float RequiredPower;

        /// <summary>기본 스핀 수. 프로토타입 임시값 5.</summary>
        public int Spins;

        /// <summary>이 층 심볼 풀에 등장하는 종류. 1~2층은 증식체를 빼서 복잡도를 낮춘다.</summary>
        public SymbolKind[] SymbolPool;

        /// <summary>
        /// 저항체 가중치 배율. 풀이 넓어질 때 개수 안전망이 죽는 것을 막는 보정이다.
        ///
        /// 저항체가 1종일 때는 9칸 중 기대 3.0개가 나와 "3개 이상 정화" 문턱을 딱 넘긴다.
        /// 2종이 되면 같은 총량이 둘로 쪼개져 종류당 2.25개로 떨어지고, 문턱 아래라
        /// 정화가 급감한다. 종류를 늘리는 것은 노션 03의 난이도 상승 수단인데, 보정이 없으면
        /// 난이도가 아니라 판의 산출량만 깎여 요구 전력 곡선이 거꾸로 내려간다.
        ///
        /// 그래서 종류가 늘면 저항체 총 가중치를 함께 올려 종류당 기대 개수를 유지한다.
        /// 대신 정상 영혼 비중이 줄어 기본 전력이 낮아지므로, 판이 실제로 더 위험해진다.
        /// </summary>
        public float ResistanceWeightScale;

        /// <summary>선택 가능한 계약. 비어 있으면 계약 단계를 건너뛴다.</summary>
        public ResistanceContract[] ContractChoices;

        /// <summary>승객·부품 보상이 제시되는 층인가(5층 휴식 등).</summary>
        public bool OffersBuildReward;

        /// <summary>요구 전력 달성 후 추가 스핀을 강하게 유도하는 층인가(9층).</summary>
        public bool EmphasizePushYourLuck;

        public bool IsValid => Floor > 0 && !string.IsNullOrEmpty(CoreQuestion) && Spins > 0 && RequiredPower > 0f;
    }

    /// <summary>
    /// 런이 어떤 층들로 이루어지는가. `RunSession`이 층 번호로 계획을 물어보는 유일한 창구다.
    ///
    /// 이 인터페이스가 없으면 10층 커리큘럼과 1층 Hero Slice가 같은 배열을 두고 싸운다.
    /// `CURRENT_PHASE.md`는 이번 세션 범위를 1층으로 제한하지만 10층 커리큘럼은 Phase 2
    /// 이후의 자산이므로, 덮어쓰지 않고 나란히 둔다.
    /// </summary>
    public interface IFloorPlanSource
    {
        int FirstFloor { get; }
        int LastFloor { get; }
        FloorPlan For(int floor);
    }

    /// <summary>
    /// 1층짜리 Hero Slice. `CURRENT_PHASE.md` §1이 요구하는 흐름
    /// `계약 선택 → 실행 레버 → 3×3 결과 → 정화·패턴·캐스케이드 → 전력 → 확정 또는 과수확`
    /// 을 한 층 안에서 전부 겪게 하는 것이 유일한 목적이다.
    ///
    /// 10층 커리큘럼의 1층과 다른 이유: 커리큘럼의 1층은 "레버를 당기면 무슨 일이 일어나는가"만
    /// 가르치는 층이라 계약이 없고(6층에 처음 등장), 증식체도 없다(7층에 처음 등장).
    /// 그 층으로는 이번 Phase의 통과 조건인 계약 2종·저항체 2종·과수확 선택을 검증할 수 없다.
    /// </summary>
    public sealed class HeroSliceFloorSource : IFloorPlanSource
    {
        public int FirstFloor => 1;
        public int LastFloor => 1;

        public FloorPlan For(int floor) => PrototypeCurriculum.HeroSlice;
    }

    /// <summary>노션 99의 10층 커리큘럼. Phase 2 이후의 기본 런.</summary>
    public sealed class TenFloorSource : IFloorPlanSource
    {
        public int FirstFloor => 1;
        public int LastFloor => 10;

        public FloorPlan For(int floor) => PrototypeCurriculum.For(floor);
    }

    /// <summary>
    /// 노션 99 "10층 테스트 구조"를 그대로 옮긴 커리큘럼. Teach → Test → Twist 순서로,
    /// 새 규칙은 한 층에 하나씩만 들어간다.
    ///
    /// 요구 전력 곡선은 임시값이다. 시뮬레이터가 층별 통과율을 뱉으면 여기서만 고친다.
    /// </summary>
    public static class PrototypeCurriculum
    {
        public static ResistanceContract AbsorberContract => new ResistanceContract
        {
            Target                    = SymbolKind.Absorber,
            Label                     = "흡수체 계약",
            AppearanceMultiplier      = 1.6f,
            PurifyRewardMultiplier    = 1.8f,
            PatternBonusAdd           = 0.5f,
            ResidualPenaltyMultiplier = 1.8f,
        };

        public static ResistanceContract ProliferatorContract => new ResistanceContract
        {
            Target                    = SymbolKind.Proliferator,
            Label                     = "증식체 계약",
            AppearanceMultiplier      = 1.5f,
            PurifyRewardMultiplier    = 1.5f,
            PatternBonusAdd           = 1.0f,
            ResidualPenaltyMultiplier = 2.0f,
        };

        private static readonly SymbolKind[] SoulAndAbsorber =
            { SymbolKind.NormalSoul, SymbolKind.Absorber };

        private static readonly SymbolKind[] FullPool =
            { SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.Proliferator };

        /// <summary>
        /// Hero Slice 1층. 이번 Phase의 통과 조건을 한 층에 압축한다.
        ///
        /// 요구 전력 460은 헤드리스 400시드 측정으로 정했다(측정표: `docs/runtime/ProgressLog.md`).
        /// 무계약 기준 5스핀 내 달성률 80%, 달성 스핀 중앙값 3 — 즉 절반 이상이 2스핀을
        /// 남겨둔 채 요구 전력을 넘긴다. 그 남은 스핀이 곧 과수확 선택지다.
        /// 너무 낮으면 1스핀에 끝나 선택이 생기지 않고, 너무 높으면 5스핀을 다 써도
        /// 못 넘겨 선택 자체가 없다. 둘 다 이번 세션이 검증하려는 것을 지운다.
        ///
        /// 최종 밸런스가 아니다 — `CURRENT_PHASE.md` §3 제외 항목. 이 숫자만 고치면 된다.
        /// </summary>
        public static FloorPlan HeroSlice => new FloorPlan
        {
            Floor         = 1,
            CoreQuestion  = "위험을 더 불러들일 것인가, 지금 확정할 것인가?",
            TeachesRule   = "계약 → 레버 → 정화·패턴·캐스케이드 → 확정 또는 과수확",
            RequiredPower = 460f,
            Spins         = 5,
            SymbolPool    = FullPool,
            ContractChoices = new[]
            {
                ResistanceContract.None,
                AbsorberContract,
                ProliferatorContract,
            },
            EmphasizePushYourLuck = true,
        };

        public static IReadOnlyList<FloorPlan> TenFloors => _tenFloors;

        private static readonly FloorPlan[] _tenFloors =
        {
            new FloorPlan
            {
                Floor         = 1,
                CoreQuestion  = "레버를 당기면 무슨 일이 일어나는가?",
                TeachesRule   = "1인칭 이동·조준 클릭·자동 스핀·정상 영혼 기본 전력",
                RequiredPower = 350f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
            },
            new FloorPlan
            {
                Floor         = 2,
                CoreQuestion  = "같은 저항체가 3개면 흩어져 있어도 정화되는가?",
                TeachesRule   = "개수 안전망 — 위치와 무관한 3개 기본 정화",
                RequiredPower = 355f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                // 첫 적재 기회. 무게가 요구 전력을 올린다는 사실을 계약보다 먼저 겪게 한다 —
                // 계약이 없는 층이라 배울 것이 하나뿐이다.
                OffersBuildReward = true,
            },
            new FloorPlan
            {
                Floor         = 3,
                CoreQuestion  = "흩어진 3개와 한 줄로 선 3개는 무엇이 다른가?",
                TeachesRule   = "직선 3개 패턴 배수",
                RequiredPower = 365f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                // 기본 밀도에서 직선은 스핀당 0.08회다. 5스핀이면 기대 0.4회 —
                // 플레이어 3분의 2가 "직선을 가르치는 층"에서 직선을 한 번도 못 본다.
                // 가르치는 층은 가르칠 것이 나와야 한다.
                ResistanceWeightScale = 1.6f,
            },
            new FloorPlan
            {
                Floor         = 4,
                CoreQuestion  = "연결이 4개가 되면 판이 어떻게 무너지는가?",
                TeachesRule   = "4개 이상 직교 연결 → 제거 후 빈칸 재추첨과 첫 캐스케이드",
                RequiredPower = 365f, Spins = 5, SymbolPool = SoulAndAbsorber,
                // 3~5층은 요구 전력이 같다. 새 규칙(직선·연결·빌드)을 하나씩 소개하는 구간이라
                // 숫자까지 같이 올리면 "규칙을 배우는 층"이 아니라 "숫자를 못 맞춘 층"이 된다.
                ContractChoices = Array.Empty<ResistanceContract>(),
                // 4개 연결은 기본 밀도에서 스핀당 0.21회. 캐스케이드를 처음 보여주는 층이므로
                // 3층보다 한 단계 더 올려 5스핀 안에 거의 확실히 한 번은 터지게 한다.
                ResistanceWeightScale = 1.9f,
            },
            new FloorPlan
            {
                Floor         = 5,
                CoreQuestion  = "나는 어떤 엔진을 만들 것인가?",
                TeachesRule   = "승객·부품 보상과 빌드 방향 선택",
                RequiredPower = 365f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                OffersBuildReward = true,
            },
            new FloorPlan
            {
                Floor         = 6,
                CoreQuestion  = "위험을 더 불러들이고 더 큰 보상을 받을 것인가?",
                TeachesRule   = "흡수체 계약 — 출현률·보상·잔류 대가가 함께 오른다",
                RequiredPower = 390f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract },
            },
            new FloorPlan
            {
                Floor         = 7,
                CoreQuestion  = "남긴 증식체는 위험인가 다음 스핀의 재료인가?",
                TeachesRule   = "증식체와 잔류 가중치 이월",
                RequiredPower = 475f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, ProliferatorContract },
            },
            new FloorPlan
            {
                Floor         = 8,
                CoreQuestion  = "내 빌드는 어떤 저항을 더 잘 전력으로 바꾸는가?",
                TeachesRule   = "계약 비교와 적재 무게로 인한 요구 전력 증가",
                RequiredPower = 480f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract, ProliferatorContract },
                // 마지막 적재 기회. 이 층의 핵심 질문이 "내 빌드"이므로 빌드를 완성할 자리를 준다.
                OffersBuildReward = true,
            },
            new FloorPlan
            {
                Floor         = 9,
                CoreQuestion  = "이미 올라갈 수 있는데, 한 번 더 돌릴 것인가?",
                TeachesRule   = "푸시 유어 럭 — 확정과 추가 스핀의 기대값 비교",
                RequiredPower = 490f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract, ProliferatorContract },
                EmphasizePushYourLuck = true,
            },
            new FloorPlan
            {
                Floor         = 10,
                CoreQuestion  = "계약·패턴·캐스케이드를 한 번에 쓸 수 있는가?",
                TeachesRule   = "새 규칙 없음 — 지금까지의 종합 시험",
                // 10층만 계약이 강제(선택지에 None이 없다)라 산출량이 크게 오른다.
                // 요구 전력의 급등은 그 보정이지, 보스를 숫자로만 어렵게 만든 것이 아니다.
                RequiredPower = 875f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { AbsorberContract, ProliferatorContract },
            },
        };

        public static FloorPlan For(int floor)
        {
            foreach (FloorPlan plan in _tenFloors)
                if (plan.Floor == floor) return plan;
            return _tenFloors[_tenFloors.Length - 1];
        }

        /// <summary>
        /// 층 계획에서 이번 스핀의 규칙 다발을 만든다. 계약 적용 직전까지의 단계다.
        ///
        /// 이 함수가 유일한 진입점이어야 한다. 시뮬레이터·런타임·테스트가 각자 풀을 구성하면
        /// 밸런스 수치가 조용히 갈라지고, 시뮬이 통과시킨 값이 실제 플레이에서 다르게 나온다.
        /// </summary>
        public static SpinRuleSet BuildRules(in FloorPlan plan)
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();

            // 이번 층 풀에 없는 종류는 가중치 0
            var pool = plan.SymbolPool ?? Array.Empty<SymbolKind>();
            foreach (SymbolKind kind in new[] { SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.Proliferator })
            {
                bool inPool = false;
                foreach (SymbolKind k in pool) if (k == kind) { inPool = true; break; }
                if (!inPool) rules.Weights[kind] = 0f;
            }

            // 저항체 총량 보정. 명시값이 없으면 저항 종류 수를 그대로 배율로 쓴다 —
            // 종류가 n개로 쪼개져도 종류당 기대 개수가 1종일 때와 같아진다.
            int resistanceTypes = 0;
            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds)
                if (rules.WeightOf(kind) > 0f) resistanceTypes++;

            float scale = plan.ResistanceWeightScale > 0f
                ? plan.ResistanceWeightScale
                : Math.Max(1, resistanceTypes);

            if (scale != 1f)
                foreach (SymbolKind kind in SymbolKinds.ResistanceKinds)
                    if (rules.WeightOf(kind) > 0f)
                        rules.Weights[kind] = rules.WeightOf(kind) * scale;

            return rules;
        }
    }
}
