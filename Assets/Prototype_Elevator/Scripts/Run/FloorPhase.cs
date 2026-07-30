namespace Ascend.Prototype.Run
{
    /// <summary>State of the decision loop for one floor.</summary>
    public enum FloorPhase
    {
        ContractSelection,
        Spinning,
        Decision,
        Resolved,

        /// <summary>
        /// 문이 열려 있고 승객·부품을 실을 수 있는 단계. `OffersBuildReward` 층에서만 나온다.
        ///
        /// 계약보다 **앞**이다. 무엇을 실었는지에 따라 요구 전력과 허용 중량이 바뀌고,
        /// 그 값을 보고 나서 계약을 골라야 순서가 성립한다. 반대로 두면 계약을 정한 뒤에
        /// 요구 전력이 움직여 플레이어가 방금 한 선택의 근거가 사라진다.
        ///
        /// 열거형 끝에 붙인 이유는 앞의 네 값에 직렬화된 인스펙터 값이 물려 있기 때문이다.
        /// </summary>
        Boarding,
    }
}
