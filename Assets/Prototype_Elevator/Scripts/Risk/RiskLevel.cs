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

        /// <summary>
        /// 과적 또는 잔류 저항 증가. 불안한 징후가 보이되 판독성은 지킨다.
        ///
        /// 이름이 `Warning` 이 아니라 `Strain` 인 이유: PRD §8.1 이 그렇게 부른다.
        /// 저장소의 동결 스냅샷이 `Warning` 으로 흘러 있었다(`D-20260801-05`).
        /// **`Warning` 으로 되돌리지 말 것** — 그리고 이름이 비슷한 다른 것들과 헷갈리지 말 것:
        /// `RiskProfile.WarningColor`·`WarningPulseRate`·`WarningEmission` 은 위험 **단계**가
        /// 아니라 경고 **등**이고 `DangerFeedbackProfile.asset` 에 그 이름으로 직렬화돼 있다.
        /// `AudioCueChannel.Warning`·`AudioChannel.Warning` 은 오디오 채널이다.
        /// 열거 멤버는 int 로 직렬화되므로 이 이름만 바꾸는 것은 안전하지만,
        /// 필드 이름을 함께 바꾸면 에셋이 조용히 끊긴다. **일괄 치환 금지.**
        /// </summary>
        Strain = 1,

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
                case RiskLevel.Strain:   return "응력";
                case RiskLevel.Critical: return "위험";
                case RiskLevel.Collapse: return "제어 상실";
                default:                 return "안정";
            }
        }
    }
}
