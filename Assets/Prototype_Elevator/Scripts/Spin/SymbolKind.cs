namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 3×3 보드 한 칸에 들어가는 심볼. 프로토타입 고정안(노션 01/99)은 정상 영혼 1종 +
    /// 저항체 2종만 사용한다. 확장 시 저항체는 <see cref="ResistanceKinds"/> 뒤에 추가한다.
    /// </summary>
    public enum SymbolKind
    {
        /// <summary>제거된 뒤 아직 충전되지 않은 빈칸. 캐스케이드 중간 상태에서만 존재한다.</summary>
        Empty = 0,

        /// <summary>정상 영혼. 개수만큼 기본 전력을 준다. 위치·패턴과 무관하게 즉시 수확된다.</summary>
        NormalSoul = 1,

        /// <summary>흡수체. 정화되지 않으면 저장 전력을 깎는다.</summary>
        Absorber = 2,

        /// <summary>증식체. 정화되지 않으면 다음 스핀의 자기 출현 가중치를 올린다.</summary>
        Proliferator = 3,
    }

    public static class SymbolKinds
    {
        /// <summary>프로토타입에서 사용하는 저항체 목록. 판정 순서는 이 배열 순서를 따른다.</summary>
        public static readonly SymbolKind[] ResistanceKinds =
        {
            SymbolKind.Absorber,
            SymbolKind.Proliferator,
        };

        public static bool IsResistance(this SymbolKind kind)
            => kind == SymbolKind.Absorber || kind == SymbolKind.Proliferator;

        public static bool IsFilled(this SymbolKind kind)
            => kind != SymbolKind.Empty;

        /// <summary>UI·로그용 한글 이름.</summary>
        public static string DisplayName(this SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.NormalSoul:   return "정상 영혼";
                case SymbolKind.Absorber:     return "흡수체";
                case SymbolKind.Proliferator: return "증식체";
                default:                      return "빈칸";
            }
        }
    }
}
