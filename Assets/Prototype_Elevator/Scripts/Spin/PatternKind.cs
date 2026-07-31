namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 개수 정화가 성립한 뒤 배치를 검사해 나오는 패턴 등급.
    /// 노션 01 "패턴 보너스" 순서를 그대로 따르며, 값이 클수록 상위 패턴이다.
    /// 초기에는 한 저항체당 가장 높은 패턴 하나만 판정한다(업그레이드로 중복 판정 해금).
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

        /// <summary>4개 이상 직교 연결 — 연쇄 붕괴. 제거 후 빈칸 재추첨으로 캐스케이드가 열린다.</summary>
        Cluster = 3,

        /// <summary>9칸 전부 같은 저항 — 수확 잭팟.</summary>
        FullBoard = 4,
    }

    public static class PatternKinds
    {
        public static string DisplayName(this PatternKind kind)
        {
            switch (kind)
            {
                case PatternKind.Scattered: return "인접 정화";
                case PatternKind.Line:      return "직선";
                case PatternKind.Cluster:   return "연결 붕괴";
                case PatternKind.FullBoard: return "수확 잭팟";
                default:                    return "없음";
            }
        }

        /// <summary>이 패턴이 제거된 칸을 재추첨해 캐스케이드를 여는가.</summary>
        public static bool TriggersRefill(this PatternKind kind)
            => kind == PatternKind.Cluster || kind == PatternKind.FullBoard;
    }
}
