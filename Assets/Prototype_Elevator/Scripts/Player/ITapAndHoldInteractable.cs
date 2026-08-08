namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 한 물체에서 **짧게 누르기와 길게 누르기가 서로 다른 행동**을 하는 상호작용물.
    ///
    /// ## 왜 새 계약이 필요했나
    ///
    /// <see cref="IHoldInteractable"/> 는 둘 중 하나다 — `HoldSeconds` 가 0 이하면 탭,
    /// 0 보다 크면 **유지만** 되고 탭은 아무 일도 하지 않는다. 그래서 한 레버가
    /// 「탭 = 일반 스핀, 길게 = 과수확」을 할 수 없었다.
    ///
    /// ## 왜 레버를 하나로 합치는가 (2026-08-08 사용자 결정)
    ///
    /// 「과수확은 조건이 있는 행동인데 별도 레버로 분리하면 플레이어가 그 조건을 배울
    /// 방법이 없다. 일반 레버로만 동작하다가 조건이 되면 『길게 눌러 과수확』으로 유도하라.」
    ///
    /// 이것은 `visual-criteria B-4.12`(「Decision 의 선택은 둘이어야 한다. 세 번째가
    /// 보이면 두 선택의 대등함이 깨진다」)와 **충돌하지 않는다.** Decision 의 선택은
    /// 여전히 둘이다 — 탱크를 탭(안전), 레버를 길게(위험).
    ///
    /// 게다가 합치기 전 상태는 이지선다가 아니었다. 실측하면 `OverharvestLever` 는
    /// **콜라이더가 없어 조준 자체가 불가능**했고 `onPulled` 리스너도 0개였다.
    /// 즉 Decision 에서 실제로 누를 수 있는 것은 탱크 하나뿐이었고,
    /// **과수확은 도달할 수 없는 선택지**였다.
    ///
    /// ## 구현자가 지켜야 하는 계약
    ///
    /// - <see cref="HoldAvailable"/> 가 false 면 이 물체는 **평범한 탭 물체처럼** 동작한다.
    ///   유지 진행도 표시도, <see cref="OnHoldCompleted"/> 도 오지 않는다.
    /// - true 면 조작자는 누른 시간을 재고 <see cref="IHoldInteractable.OnHoldProgress"/> 를 부른다.
    ///   · 완성 전에 떼면 → <see cref="IInteractable.Interact"/> (짧은 행동)
    ///   · 완성하면       → <see cref="OnHoldCompleted"/> (긴 행동). `Interact` 는 부르지 않는다.
    /// - 즉 **두 행동은 배타적이다.** 한 번 누름에 둘 다 일어나지 않는다.
    /// - 조준이 벗어나면 <see cref="IHoldInteractable.OnHoldCancelled"/> 만 오고
    ///   짧은 행동도 일어나지 않는다 — 「보다가 딴 데 보면 취소」가 §7 이 요구하는 것이다.
    /// </summary>
    public interface ITapAndHoldInteractable : IHoldInteractable
    {
        /// <summary>
        /// 지금 긴 행동이 제공되는가. false 면 탭 전용으로 동작한다.
        /// 프롬프트도 이 값에 따라 바뀌어야 한다 — 조건을 만족했다는 것을
        /// 플레이어가 화면에서 알 수 있어야 이 설계가 성립한다.
        /// </summary>
        bool HoldAvailable { get; }

        /// <summary>유지를 완성했다. 긴 행동을 실행한다.</summary>
        void OnHoldCompleted();
    }
}
