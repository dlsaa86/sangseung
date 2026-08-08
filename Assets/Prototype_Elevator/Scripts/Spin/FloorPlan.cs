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
    /// ## 요구 전력 곡선은 2026-08-04 에 **측정으로 다시 세웠다**
    ///
    /// 위 문장(「시뮬레이터가 층별 통과율을 뱉으면 여기서만 고친다」)이 이번에 실행됐다.
    /// 도구는 `Ascend/Solve Balance Curve`, 산출은 `docs/runtime/BALANCE_SOLVE.md`.
    /// 식은 하나다 — **요구 전력 = 목표 소요 스핀 × 실측 평균 순전력.**
    /// 목표 스핀 곡선은 `BalanceSweep.TargetSpins` 에 있고 근거는 `A-20260804-08`.
    ///
    /// 옛 곡선(350/355/365/365/365/390/475/480/490/875)이 왜 안 됐나 —
    /// **같은 요구 전력이 층마다 전혀 다른 난이도였다.** 3층은 스핀 1.08회면 끝나고
    /// 5층은 2.66회가 필요했는데 요구는 둘 다 365 였다. 요구 전력은 난이도가 아니라
    /// 난이도의 **그림자**이고, 판의 산출량이 몸이다.
    ///
    /// ### ⚠ 새 곡선은 단조 증가가 아니다 — 알고 채택했다
    ///
    /// 215 / 430 / 585 / 630 / **290** / 570 / 570 / 575 / 640 / 835.
    /// 5층이 꺼진다. 이유는 계약이다 — 5층은 계약이 없는 휴식 층이라 판의 산출이
    /// 151.5 로 최저이고(다른 층은 300~485), 요구 전력이 그대로 따라간다.
    ///
    /// 4층은 한 번 920 까지 갔다가 630 으로 내렸다. 그 층의 산출 485.5 는
    /// **계약을 건 플레이어 기준**인데, 계약이 처음 등장하는 층에서 그 기준으로
    /// 요구를 잡으면 계약을 아직 모르는 플레이어가 벽에 막힌다 — 고정 시드 8개 중
    /// 6개가 4층에서 죽었다. `BalanceSweep.TargetSpins` 의 4층 주석에 경위가 있다.
    ///
    /// 즉 **요구 전력의 요철이 곧 「이 층에 계약이 걸려 있는가」의 표시**다.
    /// 그 자체로는 읽히는 정보지만, 노션 03 의 「난이도 확장 순서 ① 요구 전력 증가」가
    /// 뜻하는 상승감과는 어긋난다 → `PENDING_DECISIONS` P-20260804-04.
    ///
    /// 이 요철을 없애려면 층별 밀도를 더 손봐야 하는데, 그러면 연쇄 빈도(현재 14.7%,
    /// 상한 15%)가 대역을 넘는다. **지금 통과 중인 지표를 미관을 위해 깨지 않는다.**
    /// </summary>
    public static class PrototypeCurriculum
    {
        /// <summary>
        /// ## 잔류 대가가 2026-08-04 에 1.8 → 2.0 으로 올랐다
        ///
        /// 곡선 재조정으로 저항 밀도가 내려가자 **잔류 저항도 같이 줄어들어 계약의
        /// 대가가 얇아졌다.** 흡수체 잔류는 `StoredPowerLoss` 로 `NetPower` 에서
        /// 직접 차감되므로 이 배수는 **실제로 대가로 작동한다** — 증식체 쪽과 다르다
        /// (아래 계약의 경고 참조).
        ///
        /// 2.6 까지 올려 봤다가 2.0 으로 되돌렸다. 2.6 에서는 7층 클리어율이
        /// 82.6% → 78.2% 로 떨어져 흡수체가 「계약 없음」(83.7%)보다도 나빠졌다 —
        /// 대가가 보상을 넘으면 그 계약은 함정이 된다. 노션 03 의 플레이테스트
        /// 실패 항목이 양쪽 방향을 다 금지한다.
        /// </summary>
        public static ResistanceContract AbsorberContract => new ResistanceContract
        {
            Target                    = SymbolKind.Absorber,
            Label                     = "흡수체 계약",
            AppearanceMultiplier      = 1.6f,
            PurifyRewardMultiplier    = 1.8f,
            PatternBonusAdd           = 0.5f,
            ResidualPenaltyMultiplier = 2.0f,

            // 🔴 PD-29 안 C (2026-08-06) — **빌드가 받쳐줄 때 진가가 나온다.**
            //
            // 흡수체 계약은 세 층(7·8·9) 전부에서 증식체 계약에 졌다. 값을 올려
            // 이기게 만들면 이번엔 **어느 빌드로도** 흡수체가 1등이 되어 정답이
            // 반대로 옮겨갈 뿐이다 — 그건 PD-29 가 요구하는 것이 아니다.
            // 그래서 기본값은 그대로 두고 **적재가 흡수체를 겨냥한 만큼만** 얹는다.
            //
            // 증식체보다 계수가 큰 이유: 흡수체 계약이 밑에서 출발하므로 같은 계수로는
            // 순위가 안 뒤집힌다. 「받쳐주는 빌드에서만 1등」이 되는 최소치를
            // `dotnet run -- contracts` 로 재서 정했다.
            SynergyCondition             = ContractSynergyCondition.TargetedByLoadout,
            SynergyPurifyRewardPerMatch   = 0.30f,
            SynergyResidualReliefPerMatch = 0.50f,
        };

        /// <summary>
        /// ## 패턴 보너스가 1.0 → 0.7 로 내렸다
        ///
        /// 두 계약이 나란히 놓이는 7~9층에서 증식체가 흡수체를 **일관되게** 이겼다
        /// (90.0 vs 82.6 · 97.2 vs 84.2 · 100.0 vs 83.8). 세 층 전부 같은 방향이면
        /// 그건 분산이 아니라 지배 전략이다.
        ///
        /// 원인은 `PatternBonusAdd` 1.0 이 `PurifyRewardMultiplier` 1.8 보다
        /// 값이 컸다는 것이다 — 패턴은 캐스케이드마다 다시 곱해지므로 복리로 붙는다.
        /// ## ⚠ 「잔류 대가를 올린다」는 이 계약에 **통하지 않는다** (실측으로 확인)
        ///
        /// 첫 시도는 `ResidualPenaltyMultiplier` 를 2.0 → 3.2 로 올리는 것이었다.
        /// 결과는 반대였다 — 7·8·9층 클리어율이 90/97/100 에서 **100/100/100** 이 됐다.
        ///
        /// 코드를 보면 이유가 있다. `SpinEngine.BuildResidual` 에서
        ///   · 흡수체 잔류 → `StoredPowerLoss` → `NetPower` 에서 **차감된다** (진짜 대가)
        ///   · 증식체 잔류 → `NextProliferatorWeightAdd` → 다음 스핀의 증식체 **가중치 상승**
        /// 이고, 증식체 계약을 든 플레이어에게 증식체는 곧 점수다. 즉 이 계약에서
        /// `ResidualPenaltyMultiplier` 는 대가 배수가 아니라 **눈덩이 배수**다.
        /// 이름이 하는 말과 코드가 하는 일이 다르다.
        ///
        /// 그래서 대가 축을 되돌리고(2.0) **보상 축을 깎았다** —
        /// 출현률 1.5 → 1.25, 정화 보상 1.5 → 1.2.
        ///
        /// **남은 설계 결함은 고치지 않았다.** 노션 03 의 6층 질문은 「증식을 위험으로
        /// 볼지 대형 패턴 재료로 볼지」인데, 지금 구현에서 증식체 잔류는 **재료일 뿐
        /// 위험이 아니다.** 위험 축을 새로 만드는 것은 「새 핵심 시스템 추가」에 해당해
        /// 자율 세션의 범위 밖이다 → `PENDING_DECISIONS` P-20260804-03.
        /// </summary>
        public static ResistanceContract ProliferatorContract => new ResistanceContract
        {
            Target                    = SymbolKind.Proliferator,
            Label                     = "증식체 계약",
            AppearanceMultiplier      = 1.25f,
            PurifyRewardMultiplier    = 1.2f,
            PatternBonusAdd           = 0.7f,
            ResidualPenaltyMultiplier = 2.0f,

            // 증식체 쪽 계수를 더 작게 둔다. 이 계약의 이득은 `PatternBonusAdd` 를 타고
            // **캐스케이드마다 다시 곱해지므로** 같은 계수를 주면 시너지까지 복리가 된다
            // (위 §「패턴 보너스가 1.0 → 0.7 로 내렸다」와 같은 이유).
            SynergyCondition             = ContractSynergyCondition.TargetedByLoadout,
            SynergyPurifyRewardPerMatch   = 0.15f,
            SynergyResidualReliefPerMatch = 0.15f,
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
                RequiredPower = 330f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                ResistanceWeightScale = 1.0f,
            },
            new FloorPlan
            {
                Floor         = 2,
                CoreQuestion  = "흩어진 3개와 한 줄로 선 3개는 무엇이 다른가?",
                TeachesRule   = "직선 3개 패턴 배수",
                RequiredPower = 430f, Spins = 5, SymbolPool = SoulAndAbsorber,
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
                RequiredPower = 550f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                // 4개 연결은 기본 밀도에서 스핀당 0.21회. 캐스케이드를 처음 보여주는 층이므로
                // 2층보다 한 단계 더 올려 5스핀 안에 거의 확실히 한 번은 터지게 한다.
                ResistanceWeightScale = 1.8f,
                MustBePlayed = true,   // 연결·캐스케이드는 여기서만 처음 나온다
            },
            new FloorPlan
            {
                Floor         = 4,
                CoreQuestion  = "위험을 더 불러들이고 더 큰 보상을 받을 것인가?",
                TeachesRule   = "흡수체 계약 — 출현률·보상·잔류 대가가 함께 오른다",
                // 계약이 처음 등장하는 층. 08/99번 배치에서는 6층이었다 (D-20260801-01).
                RequiredPower = 680f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract },
                // 3층과 같은 밀도를 유지한다. 계약은 「저항을 더 불러들이는」 거래인데
                // 판에 저항이 희박하면 그 거래가 무엇을 바꾸는지 볼 수 없다.
                ResistanceWeightScale = 1.1f,
                MustBePlayed = true,   // 계약이라는 개념 자체가 여기서 처음 나온다
            },
            new FloorPlan
            {
                Floor         = 5,
                CoreQuestion  = "나는 어떤 엔진을 만들 것인가?",
                TeachesRule   = "승객·부품 보상과 빌드 방향 선택",
                // 03번의 "휴식 + 빌드 선택". 요구 전력을 올리지 않는 것이 휴식이다.
                RequiredPower = 820f, Spins = 5, SymbolPool = SoulAndAbsorber,
                ContractChoices = Array.Empty<ResistanceContract>(),
                // 휴식은 **요구 전력**을 안 올리는 것이지 판을 비우는 것이 아니다.
                // 밀도를 낮추면 「내가 만든 엔진이 어떻게 도는지」를 볼 판이 없어진다.
                ResistanceWeightScale = 1.1f,
                OffersBuildReward = true,
            },
            new FloorPlan
            {
                Floor         = 6,
                CoreQuestion  = "남긴 증식체는 위험인가 다음 스핀의 재료인가?",
                TeachesRule   = "증식체와 잔류 가중치 이월",
                // 증식체가 처음 등장하는 층 — 여기서부터 풀이 3종이다. 08/99번에서는 7층.
                RequiredPower = 970f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, ProliferatorContract },
                // ⚠ 6~10층 배율이 3 이상인 이유 — **종류 수 보정을 이미 포함한 값이다.**
                //
                // 명시하지 않으면 `BuildRules` 가 저항 **종류 수**를 배율로 쓴다(=2).
                // 그 2는 「종류가 둘로 쪼개져도 종류당 기대 개수를 1종일 때와 같게」
                // 만드는 보정일 뿐이고, 그 기준점은 배율 **1.0** 이다.
                // 그런데 3~5층은 1.9 로 돌고 있다. 그래서 옛 배치에서는 6층에 들어서는
                // 순간 종류당 밀도가 3층의 절반 이하로 **떨어졌다** — 실측으로
                // 스핀당 순전력 337.8(3층) → 211.3(6층). 층이 올라가는데 판은 묽어졌다.
                //
                // 「난이도 확장 순서 ③ 저항체 종류 추가」(노션 03)가 뜻하는 것은
                // 판이 묽어지는 것이 아니라 **위험이 두 갈래로 갈리는 것**이다.
                // 그래서 1종 기준 밀도(1.7~2.1)에 종류 수 보정(×2)을 곱해 적는다.
                ResistanceWeightScale = 1.4f,
                MustBePlayed = true,   // 세 번째 심볼이 여기서만 처음 나온다
            },
            new FloorPlan
            {
                Floor         = 7,
                CoreQuestion  = "내 빌드는 어떤 저항을 더 잘 전력으로 바꾸는가?",
                TeachesRule   = "계약 비교 — 흡수체와 증식체 중 무엇이 내 빌드에 맞는가",
                // 두 계약이 처음으로 **나란히** 놓이는 층. 4층은 흡수체 하나, 6층은 증식체
                // 하나뿐이라 비교가 성립하지 않았다.
                RequiredPower = 1130f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract, ProliferatorContract },
                ResistanceWeightScale = 1.5f,   // 0.75 × 2종
                MustBePlayed = true,   // 두 계약이 나란히 놓이는 유일한 층
            },
            new FloorPlan
            {
                Floor         = 8,
                CoreQuestion  = "무게를 더 지고도 요구 전력을 넘길 수 있는가?",
                TeachesRule   = "적재 압박 — 무게가 요구 전력과 과적 위험을 함께 올린다",
                RequiredPower = 1300f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract, ProliferatorContract },
                ResistanceWeightScale = 1.6f,   // 0.8 × 2종
                // 마지막 적재 기회. 이 층이 가르치는 것이 곧 적재의 대가다.
                OffersBuildReward = true,
            },
            new FloorPlan
            {
                Floor         = 9,
                CoreQuestion  = "이미 올라갈 수 있는데, 한 번 더 돌릴 것인가?",
                TeachesRule   = "푸시 유어 럭 — 확정과 추가 스핀의 기대값 비교",
                RequiredPower = 1480f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { ResistanceContract.None, AbsorberContract, ProliferatorContract },
                ResistanceWeightScale = 1.7f,   // 0.85 × 2종
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
                RequiredPower = 1670f, Spins = 5, SymbolPool = FullPool,
                ContractChoices = new[] { AbsorberContract, ProliferatorContract },
                ResistanceWeightScale = 1.8f,   // 0.9 × 2종
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
