using UnityEngine;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 한 번 누르는 것으로는 실행되지 않고 **버튼을 유지해야** 실행되는 상호작용물.
    ///
    /// ## 왜 별도 인터페이스인가
    ///
    /// `MASTER_PRD.md` §7 은 과수확에만 유지 입력을 요구한다 —
    /// 「실수로 발생하지 않도록 약 `0.7~1.0초` 유지 입력과 강한 금속 걸쇠 피드백」.
    /// 그 문장의 요점은 오입력 방지가 아니라 **몸짓**이다. 위험을 자발적으로 고르는
    /// 순간이 이 게임의 대표 장면이고(§2.3 감정 곡선의 정점), 한 번의 클릭으로는
    /// 그 「자발성」이 손에 남지 않는다.
    ///
    /// 그래서 <see cref="IInteractable"/> 에 유지 시간을 넣지 않았다. 계약 패널·
    /// 전력 탱크·문은 즉시 반응해야 하고, 거기에 유지를 붙이면 조작이 굼떠진다.
    /// **유지가 필요한 것은 되돌릴 수 없는 선택 하나뿐이다.**
    ///
    /// ## 구현자가 지켜야 하는 계약
    ///
    /// - <see cref="HoldSeconds"/> 가 0 이하면 유지 없이 즉시 실행된다(일반 클릭과 같다).
    /// - <see cref="OnHoldProgress"/> 는 **매 프레임** 불린다. 0 → 1 로 오르며,
    ///   1 에 닿는 프레임에 <see cref="IInteractable.Interact"/> 가 불린다.
    /// - 도중에 손을 떼거나 조준이 벗어나면 <see cref="OnHoldCancelled"/> 가 불린다.
    ///   **취소는 반드시 옴을 보장한다** — 진행 중 상태로 얼어붙는 물체가 없어야 한다.
    /// - 진행도는 상호작용물이 **연출로 갚아야 한다.** 레버가 실제로 내려가는 것이
    ///   진행 표시이고, 그것이 §7 이 말하는 「강한 금속 걸쇠 피드백」의 시각 절반이다.
    /// </summary>
    public interface IHoldInteractable : IInteractable
    {
        /// <summary>
        /// 실행까지 유지해야 하는 시간(초). 0 이하면 즉시 실행.
        /// `MASTER_PRD.md` §7 규격은 0.7~1.0 이다.
        /// </summary>
        float HoldSeconds { get; }

        /// <summary>진행도 0~1. 매 프레임 불린다.</summary>
        void OnHoldProgress(float normalized);

        /// <summary>실행 전에 손을 떼거나 조준이 벗어났다. 진행도는 버려진다.</summary>
        void OnHoldCancelled();
    }
}
