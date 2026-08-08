using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 기계 전체를 하나의 조준 대상으로 만든다. 누르면 카메라가 기계에 고정된다.
    ///
    /// ## 왜 필요한가 (2026-08-08 사용자 지시)
    ///
    /// 「레버를 보려고 카메라를 돌리면 구슬 돌아가는 게 잘 안 보임. 이게 불편하니까
    /// 2단 구조로 가자 — 기계에 마우스를 가져다대면 아웃라인 하일라이트, 누르면 카메라가
    /// 기계에 고정되고 레버를 눌러도 시야는 기계 고정.」
    ///
    /// 1인칭에서 **조작 대상과 판독 대상이 서로 다른 방향에 있으면** 둘 중 하나를 반드시
    /// 잃는다. 실측하면 스폰 지점에서 레버는 정면에서 16.6° 벗어나 있고 눈높이보다
    /// 60 cm 아래다(카메라 y=1.70, 레버 y=1.10). 레버를 조준하면 구슬 통관이 시야
    /// 위쪽으로 밀려난다. 카메라를 고정하면 두 대상이 한 화면에 들어온다.
    ///
    /// ## 이 클래스가 하지 않는 것
    ///
    /// 카메라를 직접 움직이지 않는다. <see cref="onFocusRequested"/> 만 쏘고
    /// `MachineFocusController` 가 실제 이동·입력 잠금·나가기를 담당한다.
    /// 조준 대상과 카메라 연출을 한 클래스에 두면 나가기 조작을 바꿀 때마다
    /// 상호작용물을 고쳐야 한다.
    /// </summary>
    public sealed class InteractableMachineFocus : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _prompt = "기계 조작";
        [SerializeField] private bool _canInteract = true;

        [Tooltip("누르면 발동. MachineFocusController 가 구독한다.")]
        [SerializeField] public UnityEvent onFocusRequested = new UnityEvent();

        public string Prompt => _prompt;
        public bool CanInteract => _canInteract;

        public void SetPrompt(string prompt)
        {
            if (!string.IsNullOrEmpty(prompt)) _prompt = prompt;
        }

        /// <summary>
        /// 이미 집중 중이면 false 로 내린다 — 집중 상태에서 기계를 또 누르면
        /// 카메라가 같은 자리로 다시 보간되어 화면이 튄다.
        /// </summary>
        public void SetCanInteract(bool canInteract) => _canInteract = canInteract;

        public void Interact(GameObject interactor) => onFocusRequested.Invoke();
    }
}
