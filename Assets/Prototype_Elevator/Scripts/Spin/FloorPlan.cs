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

        /// <summary>
        /// 다층 상승이 이 층을 건너뛸 수 없는가.
        ///
        /// `OffersBuildReward`와 별개로 두는 이유: 건너뛰면 안 되는 근거가 다르다.
        /// 적재 층은 "승객·부품을 얻는 유일한 지점"이라 막고(D-20260731-03),
        /// 이 플래그는 **이 층에서만 소개되는 규칙이 있어서** 막는다.
        ///
        /// 실측이 근거다(`Logs/curriculum_coverage.txt`, 시드 200개). 재배치 직후
        /// 층별 방문률은 3층 62% · 4층 **34%** · 7층 54% · 9층 57% 였다.
        /// 계약을 처음 가르치는 4층을 런의 3분의 2가 건너뛰고, 그 플레이어는 계약을
        /// 7층에서 세 개가 한꺼번에 놓인 상태로 처음 만난다. Teach → Test → Twist 가
        /// 무너진다. 완주율 100%와 "아무도 캐스케이드를 못 봤다"는 동시에 성립한다.
        ///
        /// 보상이 사라지지는 않는다. `RunSession`은 추가 층에 쓰지 **않은** 잉여 전력을
        /// 전부 돈으로 지급하므로, 상승이 잘리면 그 잉여가 그대로 소지금이 된다.
        /// `PowerBand.MultiFloor` 이상은 층 대신 돈으로 갚는다.
        /// </summary>
        public bool MustBePlayed;

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
    /// 가르치는 층이라 계약이 없고(4층에 처음 등장), 증식체도 없다(6층에 처음 등장).
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
        /// <summary>
        /// 층별 곡선의 덮어쓰기 (`UP-TECH-09` ④). 비어 있으면 코드 프리셋 그대로다 —
        /// **대체가 아니라 얹히는 것**이라, 에셋이 없을 때와 있을 때가 같은 게임이다.
        /// </summary>
        private readonly Data.Profiles.FloorCurriculumSnapshot _curriculum;

        /// <summary>계약 수치의 덮어쓰기 (`UP-TECH-09` ②). 폴백이면 갈아끼움 자체가 없다.</summary>
        private readonly Data.Profiles.ContractSnapshot _contracts;

        public TenFloorSource()
            : this(Data.Profiles.FloorCurriculumProfile.DefaultSnapshot)
        {
        }

        public TenFloorSource(Data.Profiles.FloorCurriculumSnapshot curriculum)
            : this(curriculum, Data.Profiles.ContractProfile.DefaultSnapshot)
        {
        }

        public TenFloorSource(Data.Profiles.FloorCurriculumSnapshot curriculum,
            Data.Profiles.ContractSnapshot contracts)
        {
            _curriculum = curriculum;
            _contracts = contracts;
        }

        public int FirstFloor => 1;
        public int LastFloor => 10;

        /// <summary>곡선 출처. 하네스가 「에셋이 읽혔는가」를 이걸로 묻는다.</summary>
        public Data.Profiles.FloorCurriculumSnapshot Curriculum => _curriculum;

        /// <summary>계약 수치 출처.</summary>
        public Data.Profiles.ContractSnapshot Contracts => _contracts;

        public FloorPlan For(int floor)
            => _contracts.Apply(_curriculum.Apply(PrototypeCurriculum.For(floor)));
    }

    /// <summary>
    /// 10층 커리큘럼. Teach → Test → Twist 순서로, 새 규칙은 한 층에 하나씩만 들어간다.
    ///
    /// **정본은 노션 03번 「첫 10층 학습 구간」이다** (`DECISION_LOG` D-20260801-01).
    /// 노션 내부에서 08 기술부록 §14 / 99번과 03번이 2~7층 한 칸씩 어긋나 있었고,
    /// `NotionSyncReport.md` §6.3 이 "작성자만 안다"는 이유로 승인 항목 A-2 로 올려 두었다.
    /// 사용자 지시가 층별 배치를 열 줄 모두 명시해 03번으로 확정됐다.
    ///
    /// 08/99번을 따르던 이전 배치와의 차이 — 옮긴 것은 **내용**이고 요구 전력 곡선은
    /// 층 번호에 그대로 뒀다. 배치와 밸런스를 한 번에 바꾸면 통과율이 달라졌을 때
    /// 원인을 가릴 수 없다. 곡선은 헤드리스 다시드 측정 뒤에 이 배열에서만 고친다.
    ///
    ///   1층 조작(+기본 정화) · 2층 직선 · 3층 연결·캐스케이드 · 4층 흡수체 계약
    ///   5층 빌드 선택 · 6층 증식체 · 7층 계약 비교 · 8층 적재 압박
    ///   9층 과수확 · 10층 종합
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
                // 03번은 1층을 "1인칭 조작 + 기본 수확"으로 묶는다.
                //
                // "개수만 넘으면 흩어져 있어도 정화"는 **더 이상 규칙이 아니다.**
                // `SpinRuleSet.RequireAdjacencyToPurify`(사용자 요청으로 채택,
                // `ASSUMPTION_LOG` A-20260731-07)가 붙어 있는 것만 터지게 바꿨으므로,
                // 남은 정화 패턴은 직선(3연속)과 연결 덩어리(4개 이상)뿐이다.
                // 그 둘은 각각 2층과 3층이 가르친다 — 1층이 가르칠 몫이 아니다.
                //
                // 그래서 1층의 몫은 "레버를 당기면 판이 돌고 정상 영혼이 전력이 된다"까지다.
                // 정화가 우연히 나올 수는 있지만 그것을 이 층의 교습 목표로 적지 않는다.
                TeachesRule   = "1인칭 이동·조준 클릭·자동 스핀·정상 영혼이 전력이 된다",
                RequiredPower = 350f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
            },
            new FloorPlan
            {
                Floor         = 2,
                CoreQuestion  = "흩어진 3개와 한 줄로 선 3개는 무엇이 다른가?",
                TeachesRule   = "직선 3개 패턴 배수",
                RequiredPower = 355f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                // 기본 밀도에서 직선은 스핀당 0.08회다. 5스핀이면 기대 0.4회 —
                // 플레이어 3분의 2가 "직선을 가르치는 층"에서 직선을 한 번도 못 본다.
                // 가르치는 층은 가르칠 것이 나와야 한다.
                ResistanceWeightScale = 1.6f,
                // 첫 적재 기회. 03번은 5층을 첫 빌드 층으로 두지만, 여기서 한 번 더 여는
                // 이유는 `RunSession.ClampAscent`(D-20260731-03)가 **빌드 보상 층만**
                // 건너뛰기에서 보호하기 때문이다. 보호 지점이 5·8층 둘뿐이면 다층 상승이
                // 2~4층을 통째로 건너뛸 수 있고, 그러면 직선·연결·계약을 한 번도 안 가르친
                // 런이 정상 경로가 된다. 적재 자체는 이 층의 "가르칠 것"이 아니다 —
                // 무게가 요구 전력을 올린다는 사실은 8층이 압박으로 가르친다.
                OffersBuildReward = true,
            },
            new FloorPlan
            {
                Floor         = 3,
                CoreQuestion  = "연결이 4개가 되면 판이 어떻게 무너지는가?",
                TeachesRule   = "4개 이상 직교 연결 → 제거 후 빈칸 재추첨과 첫 캐스케이드",
                RequiredPower = 365f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                // 4개 연결은 기본 밀도에서 스핀당 0.21회. 캐스케이드를 처음 보여주는 층이므로
                // 2층보다 한 단계 더 올려 5스핀 안에 거의 확실히 한 번은 터지게 한다.
                ResistanceWeightScale = 1.9f,
                MustBePlayed = true,   // 연결·캐스케이드는 여기서만 처음 나온다
            },
            new FloorPlan
            {
                Floor         = 4,
                CoreQuestion  = "위험을 더 불러들이고 더 큰 보상을 받을 것인가?",
                TeachesRule   = "흡수체 계약 — 출현률·보상·잔류 대가가 함께 오른다",
                // 계약이 처음 등장하는 층. 08/99번 배치에서는 6층이었다 (D-20260801-01).
                RequiredPower = 365f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract },
                MustBePlayed = true,   // 계약이라는 개념 자체가 여기서 처음 나온다
            },
            new FloorPlan
            {
                Floor         = 5,
                CoreQuestion  = "나는 어떤 엔진을 만들 것인가?",
                TeachesRule   = "승객·부품 보상과 빌드 방향 선택",
                // 03번의 "휴식 + 빌드 선택". 요구 전력을 올리지 않는 것이 휴식이다.
                RequiredPower = 365f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                OffersBuildReward = true,
            },
            new FloorPlan
            {
                Floor         = 6,
                CoreQuestion  = "남긴 증식체는 위험인가 다음 스핀의 재료인가?",
                TeachesRule   = "증식체와 잔류 가중치 이월",
                // 증식체가 처음 등장하는 층 — 여기서부터 풀이 3종이다. 08/99번에서는 7층.
                RequiredPower = 390f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, ProliferatorContract },
                MustBePlayed = true,   // 세 번째 심볼이 여기서만 처음 나온다
            },
            new FloorPlan
            {
                Floor         = 7,
                CoreQuestion  = "내 빌드는 어떤 저항을 더 잘 전력으로 바꾸는가?",
                TeachesRule   = "계약 비교 — 흡수체와 증식체 중 무엇이 내 빌드에 맞는가",
                // 두 계약이 처음으로 **나란히** 놓이는 층. 4층은 흡수체 하나, 6층은 증식체
                // 하나뿐이라 비교가 성립하지 않았다.
                RequiredPower = 475f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract, ProliferatorContract },
                MustBePlayed = true,   // 두 계약이 나란히 놓이는 유일한 층
            },
            new FloorPlan
            {
                Floor         = 8,
                CoreQuestion  = "무게를 더 지고도 요구 전력을 넘길 수 있는가?",
                TeachesRule   = "적재 압박 — 무게가 요구 전력과 과적 위험을 함께 올린다",
                RequiredPower = 480f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract, ProliferatorContract },
                // 마지막 적재 기회. 이 층이 가르치는 것이 곧 적재의 대가다.
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
                MustBePlayed = true,   // 푸시 유어 럭을 정면으로 묻는 유일한 층
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
            => BuildRules(in plan, Data.Profiles.SpinBalanceProfile.DefaultSnapshot);

        /// <summary>
        /// 밸런스 수치를 밖에서 받는 판본 (`UP-TECH-09` ①③). 인자 없는 판본은 코드
        /// 프리셋으로 위임하므로 동작이 같다. 층 풀 필터·저항 총량 보정은 그대로다 —
        /// 그건 밸런스 다이얼이 아니라 층 계획이 정하는 구조다.
        /// </summary>
        public static SpinRuleSet BuildRules(in FloorPlan plan, Data.Profiles.SpinBalanceSnapshot balance)
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault(balance);

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
