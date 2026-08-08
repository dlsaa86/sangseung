namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 개수 정화가 성립한 뒤 배치를 검사해 나오는 패턴 등급.
    /// 노션 01 "패턴 보너스" 순서를 그대로 따르며, 값이 클수록 상위 패턴이다.
    /// 초기에는 한 저항체당 가장 높은 패턴 하나만 판정한다(업그레이드로 중복 판정 해금).
    ///
    /// 🔴 **2026-08-09 — Duo·Cross 추가, 값 재배치** (`PLAN_BUILD_DEPENDENCY.md` §C-2·C-7 1단).
    /// 기존 넷(Scattered·Line·Cluster·FullBoard)은 종류 하나만 보고 판정했다. Duo·Cross는
    /// **두 저항체의 상대 배치**를 본다 — 사용자가 요청한 "구슬 2종류가 어떤 모양으로 나와야
    /// 점수를 더 준다"가 이 둘이다. 배수 크기 순으로 다시 매겼다(Scattered 1.0 < Line 2.0 <
    /// Duo 2.5 < Cluster 3.0 < Cross 5.0 < FullBoard 10.0, 값은 `SpinRuleSet`) — 그래서
    /// `Cluster`가 3→4, `FullBoard`가 4→6으로 밀렸다.
    ///
    /// 이 재배치가 안전한 이유: 이 enum은 **직렬화되지 않는다.** `PurifyEvent.Pattern`
    /// 하나에만 쓰이고, 그 구조체는 스핀 하나가 끝나면 사라지는 런타임 값이지
    /// `.asset`/`.prefab`에 저장되는 값이 아니다(검증: 저장소 전체에서 `PatternKind`를
    /// 참조하는 `.asset`/`.unity`/`.prefab`이 없다). 이 enum을 소비하는 다른 파일들
    /// (`View/SpinPresenter.cs`, `View/PurifyMarkerLayout.cs`, `Run/FloorSession.cs`)도
    /// 전부 **이름으로** `switch`하지 정수값으로 비교하지 않으므로 값이 밀려도 그 스위치들은
    /// 그대로 맞게 컴파일된다 — 다만 그 세 파일 다 `default:` 안전 분기가 있어도 Duo·Cross를
    /// **모른다.** 크래시는 안 나지만(확인함) 예전 패턴처럼 전용 처리는 못 받는다.
    /// 이건 UI 쪽 파일이라 이 티켓 권한 밖이다 — `docs/runtime/PATTERN_IMPL_NOTES.md`에 남겼다.
    /// </summary>
    public enum PatternKind
    {
        /// <summary>정화 자체가 성립하지 않음.</summary>
        None = 0,

        /// <summary>
        /// 최소 개수가 **붙어 있음** — 기본 정화, 배수 없음.
        ///
        /// 예전에는 판 어디에 흩어져 있든 개수만 넘으면 성립했다("개수 안전망").
        /// 지금은 서로 닿아 있어야 한다. 직선도 아니고 4칸도 안 되는 모양 —
        /// V자, 작은 ㄴ자 — 이 여기 걸린다.
        /// `RequireAdjacencyToPurify` 를 끄면 예전 의미로 돌아간다.
        /// </summary>
        Scattered = 1,

        /// <summary>가로·세로·대각선 직선 3개 — 정화 배수와 승객 발동.</summary>
        Line = 2,

        /// <summary>
        /// **쌍** — 한 종류의 연결 덩어리(2칸 이상)가 **다른 저항체와 인접**하면 성립한다.
        /// 배수는 Line보다 높고 Cluster보다 낮다(`SpinRuleSet.DuoMultiplier`, 기본 2.5).
        ///
        /// 최소 크기가 2인 이유: Scattered(기본 최소 3)보다도 낮은 문턱에서 "다른 종류
        /// 옆에 있다"만으로 보상하는 것이 이 패턴의 핵심이다 — 실측(10000판 표본,
        /// `PATTERN_IMPL_NOTES.md`)으로 전체 판정 중 약 17.8%를 차지해 가장 자주 뜬다.
        /// 사용자가 요청한 "레버만 당겨도" 자주 걸리는 낮은 문턱 보상이 이 자리다.
        /// </summary>
        Duo = 3,

        /// <summary>
        /// 4개 이상 직교 연결 — 연쇄 붕괴. 제거 후 빈칸 재추첨으로 캐스케이드가 열린다.
        /// (2026-08-09 이전에는 값이 3이었다. 재배치 이유는 이 enum 상단 주석 참조.)
        /// </summary>
        Cluster = 4,

        /// <summary>
        /// **십자** — 한 종류가 중심 칸을 뺀 직교 4방향(바퀴)을 전부 채우고, 중심 칸이
        /// **다른 저항체**일 때 성립한다. 모서리 4칸은 무엇이든 상관없다.
        /// 배수는 Cluster보다 높고 FullBoard보다 낮다(`SpinRuleSet.CrossMultiplier`, 기본 5.0).
        ///
        /// 실측(10000판 표본, 게임 근사 가중치)으로 발생률이 **약 0.02~0.04%** 였다 —
        /// 계획 문서(`PLAN_BUILD_DEPENDENCY.md` §C-2)의 "추정 3%"보다 훨씬 희귀하다.
        /// 우연히 걸리는 패턴이 아니라 승객·부품으로 중심을 고정해야 노리는 패턴에
        /// 가깝다 — §C-4가 예고한 `CrossPatternMode`(2단)가 그 역할이다.
        /// </summary>
        Cross = 5,

        /// <summary>
        /// 9칸 전부 같은 저항 — 수확 잭팟.
        /// (2026-08-09 이전에는 값이 4였다. 재배치 이유는 이 enum 상단 주석 참조.)
        /// </summary>
        FullBoard = 6,
    }

    public static class PatternKinds
    {
        public static string DisplayName(this PatternKind kind)
        {
            switch (kind)
            {
                case PatternKind.Scattered: return "인접 정화";
                case PatternKind.Line:      return "직선";
                case PatternKind.Duo:       return "쌍";
                case PatternKind.Cluster:   return "연결 붕괴";
                case PatternKind.Cross:     return "십자";
                case PatternKind.FullBoard: return "수확 잭팟";
                default:                    return "없음";
            }
        }

        /// <summary>
        /// 이 패턴이 제거된 칸을 재추첨해 캐스케이드를 여는가.
        ///
        /// Cross를 포함한 이유: 바퀴 4칸이 Cluster와 같은 최소 크기(4)이고 배수도 더 커서
        /// "판이 한 번 더 무너질 기회"를 살 자격이 Cluster와 같다고 봤다. Duo는 뺐다 —
        /// 최소 2칸으로 Line·Scattered와 같은 "작은" 등급이라 재충전까지 열어 주면
        /// 발생 빈도(약 17.8%)를 감안할 때 캐스케이드가 지나치게 자주 열린다.
        /// </summary>
        public static bool TriggersRefill(this PatternKind kind)
            => kind == PatternKind.Cluster || kind == PatternKind.FullBoard || kind == PatternKind.Cross;
    }
}
