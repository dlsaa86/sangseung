using System.Collections.Generic;
using Ascend.Prototype.Events;
using Ascend.Prototype.Risk;

namespace Ascend.Prototype.Npc
{
    /// <summary>
    /// 승객이 반응해야 하는 사건. `MASTER_PRD.md` §9.2가 세는 10종에 결과(사고·성공)를
    /// 더한 11종이다.
    ///
    /// <see cref="GameEventKind"/>와 따로 두는 이유: 사건 목록은 게임이 발행하는 **모든**
    /// 것이고(층 시작·열 공개·전력 확정까지), 그중 승객이 몸으로 반응해야 하는 것은
    /// 일부다. 승객 쪽이 사건 목록 전체를 switch 하면 새 사건이 하나 늘 때마다
    /// "여기서도 반응해야 하나"를 다시 판단해야 하고, PRD가 고정한 10종이라는 수가
    /// 코드 어디에도 남지 않는다.
    ///
    /// 값을 명시적으로 고정한다 — 반응 발동 로그(UP-NPC-02 검증 항목)에 숫자로 들어가므로
    /// 중간에 끼워 넣으면 과거 로그의 뜻이 바뀐다. <see cref="GameEventKind"/>와 같은 규약이다.
    /// </summary>
    public enum PassengerReactionEvent
    {
        None = 0,

        /// <summary>§9.2 「계약 선택」 — 무엇을 싣고 오를지가 정해진 순간.</summary>
        ContractChosen = 1,

        /// <summary>§9.2 「기본 정화」 — 가장 흔한 성공. 반응이 작아야 나머지가 커 보인다.</summary>
        BasicPurify = 2,

        /// <summary>§9.2 「5연쇄」 — 캐스케이드가 깊어져 판이 스스로 굴러가는 순간.</summary>
        FiveChain = 3,

        /// <summary>§9.2 「임계점 1」 — 요구 전력 100%. 살아서 내릴 수 있게 됐다.</summary>
        Threshold100 = 4,

        /// <summary>§9.2 「임계점 2」 — 170%. 욕심을 낼 근거가 생겼다.</summary>
        Threshold170 = 5,

        /// <summary>§9.2 「임계점 3」 — 300%. 이 층에서 볼 수 있는 최대치에 가깝다.</summary>
        Threshold300 = 6,

        /// <summary>§9.2 「과수확 해금」 — 레버의 덮개가 열렸다(§7).</summary>
        OverharvestUnlocked = 7,

        /// <summary>§9.2 「과수확 접근」 — 아직 당기지 않았다. 이 층에서 가장 긴 정적이다.</summary>
        OverharvestApproach = 8,

        /// <summary>§9.2 「추가 스핀」 — 레버를 당겨 실제로 한 번 더 돌린다.</summary>
        ExtraSpin = 9,

        /// <summary>§9.2 「Critical 진입」과 「Collapse 직전」. 둘 다 "이제 곧 터진다"는 같은 몸짓이다.</summary>
        CollapseImminent = 10,

        /// <summary>§9.2 「사고·성공」 — 층이 끝나 결과가 확정됐다.</summary>
        AccidentOrSuccess = 11,
    }

    /// <summary>
    /// 사건 목록 → 반응 사건 변환. **이 모듈의 입력 계약이 전부 여기 있다.**
    ///
    /// 순수 함수로 두는 이유: 이 판정이 틀리면 승객은 조용하거나 엉뚱하게 움직이는데,
    /// 둘 다 화면을 봐서는 "반응 정의가 비었나 / 변환이 틀렸나"를 구분할 수 없다.
    /// 씬 없이 테스트할 수 있어야 원인이 갈린다(`TECH_SPEC.md` §2).
    /// </summary>
    public static class PassengerReactionEvents
    {
        private static readonly PassengerReactionEvent[] AllValues =
        {
            PassengerReactionEvent.ContractChosen,
            PassengerReactionEvent.BasicPurify,
            PassengerReactionEvent.FiveChain,
            PassengerReactionEvent.Threshold100,
            PassengerReactionEvent.Threshold170,
            PassengerReactionEvent.Threshold300,
            PassengerReactionEvent.OverharvestUnlocked,
            PassengerReactionEvent.OverharvestApproach,
            PassengerReactionEvent.ExtraSpin,
            PassengerReactionEvent.CollapseImminent,
            PassengerReactionEvent.AccidentOrSuccess,
        };

        /// <summary><see cref="PassengerReactionEvent.None"/>을 뺀 전부. 순서는 PRD §9.2의 나열 순서다.</summary>
        public static IReadOnlyList<PassengerReactionEvent> All => AllValues;

        /// <summary>
        /// 「5연쇄」로 세는 최소 깊이. `FloorSession`이 캐스케이드 단계마다
        /// <see cref="GameEventKind.CascadeStep"/>을 발행하므로 4단계에서는 반응하지 않고
        /// 5단계째에 한 번만 걸린다.
        /// </summary>
        public const int FiveChainDepth = 5;

        /// <summary>
        /// 승객이 반응해야 하는 사건인가. 아니면 <c>false</c>와 <see cref="PassengerReactionEvent.None"/>.
        ///
        /// 반응하지 않는 사건이 훨씬 많다는 점이 중요하다 — 열이 공개될 때마다 승객이
        /// 움찔하면 정작 5연쇄와 과수확이 묻힌다(§9 "특정 감각 채널 하나에만 의존하지 않는다"의
        /// 반대편 실패: 채널이 항상 켜져 있어 아무 정보도 싣지 못하는 상태).
        /// </summary>
        public static bool TryMap(in GameEvent e, out PassengerReactionEvent reaction)
        {
            switch (e.Kind)
            {
                case GameEventKind.ContractSelected:
                    reaction = PassengerReactionEvent.ContractChosen;
                    return true;

                // 「기본 정화」는 개수 정화만이다. 직선·연결은 PRD §9.2의 10종에 없고,
                // 여기에 끼워 넣으면 "기본"이라는 말이 뜻을 잃는다. 별도 반응이 필요하다면
                // 목록을 늘리는 결정이 먼저다.
                case GameEventKind.PurifyScattered:
                    reaction = PassengerReactionEvent.BasicPurify;
                    return true;

                case GameEventKind.CascadeStep:
                    if (e.IntValue >= FiveChainDepth)
                    {
                        reaction = PassengerReactionEvent.FiveChain;
                        return true;
                    }
                    break;

                case GameEventKind.PowerThresholdCrossed:
                    // 세 임계점이 서로 다른 반응으로 갈려야 한다. 하나로 뭉치면
                    // 100%(안도)와 300%(경외)가 같은 몸짓이 되어 §6.1 판독 순서가 무너진다.
                    switch (e.IntValue)
                    {
                        case 100: reaction = PassengerReactionEvent.Threshold100; return true;
                        case 170: reaction = PassengerReactionEvent.Threshold170; return true;
                        case 300: reaction = PassengerReactionEvent.Threshold300; return true;
                    }
                    break;

                case GameEventKind.OverharvestUnlocked:
                    reaction = PassengerReactionEvent.OverharvestUnlocked;
                    return true;

                case GameEventKind.OverharvestApproached:
                    reaction = PassengerReactionEvent.OverharvestApproach;
                    return true;

                case GameEventKind.ExtraSpinTaken:
                    reaction = PassengerReactionEvent.ExtraSpin;
                    return true;

                case GameEventKind.RiskLevelChanged:
                    // IntValue는 새 `RiskLevel`이다(`GameEventKind` 주석). Critical 이상만
                    // 잡는다 — Warning 마다 비명을 지르면 Critical 이 강해지지 않는다.
                    if (e.IntValue >= (int)RiskLevel.Critical)
                    {
                        reaction = PassengerReactionEvent.CollapseImminent;
                        return true;
                    }
                    break;

                case GameEventKind.CollapseBegan:
                    reaction = PassengerReactionEvent.CollapseImminent;
                    return true;

                // 층 결과와 런 종료를 같은 반응으로 묶는다. 승객에게는 둘 다
                // "이번 상승이 어떻게 끝났는가" 하나다.
                case GameEventKind.FloorResolved:
                case GameEventKind.RunEnded:
                    reaction = PassengerReactionEvent.AccidentOrSuccess;
                    return true;
            }

            reaction = PassengerReactionEvent.None;
            return false;
        }

        /// <summary>로그와 디버그 패널이 쓰는 한국어 이름. 반응 발동 로그가 UP-NPC-02의 검증 증거다.</summary>
        public static string DisplayName(this PassengerReactionEvent reaction)
        {
            switch (reaction)
            {
                case PassengerReactionEvent.ContractChosen:      return "계약 선택";
                case PassengerReactionEvent.BasicPurify:         return "기본 정화";
                case PassengerReactionEvent.FiveChain:           return "5연쇄";
                case PassengerReactionEvent.Threshold100:        return "임계점 100%";
                case PassengerReactionEvent.Threshold170:        return "임계점 170%";
                case PassengerReactionEvent.Threshold300:        return "임계점 300%";
                case PassengerReactionEvent.OverharvestUnlocked: return "과수확 해금";
                case PassengerReactionEvent.OverharvestApproach: return "과수확 접근";
                case PassengerReactionEvent.ExtraSpin:           return "추가 스핀";
                case PassengerReactionEvent.CollapseImminent:    return "붕괴 직전";
                case PassengerReactionEvent.AccidentOrSuccess:   return "사고·성공";
                default:                                         return "없음";
            }
        }
    }
}
