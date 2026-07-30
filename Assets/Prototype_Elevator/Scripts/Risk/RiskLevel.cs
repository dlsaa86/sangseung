namespace Ascend.Prototype.Risk
{
    /// <summary>
    /// 공간이 표현해야 하는 위험 단계. `MASTER_PRD.md` §9의 4단계를 그대로 쓴다.
    ///
    /// 이 값은 UI 숫자가 아니라 **엘리베이터 전체의 상태**다. 조명·소리·진동·경고등이
    /// 전부 여기에서 갈라져 나오며, 특정 감각 채널 하나에만 의존하지 않는다(§9 마지막 문장).
    /// </summary>
    public enum RiskLevel
    {
        /// <summary>정상 운용. 판독성이 가장 높은 기준 상태 — 다른 단계는 여기서부터 나빠진다.</summary>
        Stable = 0,

        /// <summary>과적 또는 잔류 저항 증가. 불안한 징후가 보이되 판독성은 지킨다.</summary>
        Warning = 1,

        /// <summary>사고 직전. 붕괴 직전처럼 느껴지되 핵심 결과는 여전히 읽혀야 한다.</summary>
        Critical = 2,

        /// <summary>제어 상실. 층이 실패로 끝난 상태.</summary>
        Collapse = 3,
    }

    public static class RiskLevels
    {
        public static string DisplayName(this RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Warning:  return "경고";
                case RiskLevel.Critical: return "위험";
                case RiskLevel.Collapse: return "제어 상실";
                default:                 return "안정";
            }
        }
    }
}
