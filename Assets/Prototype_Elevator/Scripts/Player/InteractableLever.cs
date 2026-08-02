using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>Thin scene-facing lever interaction stub.</summary>
    public sealed class InteractableLever : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool _canInteract = true;
        [SerializeField] public UnityEvent onPulled = new UnityEvent();

        /// <summary>
        /// 당길 수 **없는** 상태에서 당기려 했을 때.
        ///
        /// 이것이 없으면 `Interact()` 가 조용히 반환해 **화면에서 아무 일도
        /// 일어나지 않는다.** 플레이어에게 「지금은 안 된다」와 「버튼이 고장났다」는
        /// 구분되지 않는다 — 둘 다 무반응이기 때문이다. 잠긴 레버는 핀에 부딪혀
        /// 튕겨야 왜 안 되는지가 화면 안에 남는다.
        /// </summary>
        [SerializeField] public UnityEvent onBlocked = new UnityEvent();

        [SerializeField] private string _prompt = "레버 당기기";

        /// <summary>조준 시 뜨는 문구. 상태에 따라 브리지가 갈아끼운다 —
        /// 상황과 다른 문구가 떠 있으면 그건 거짓 정보다.</summary>
        public string Prompt => _prompt;

        public void SetPrompt(string prompt)
        {
            if (!string.IsNullOrEmpty(prompt)) _prompt = prompt;
        }
        public bool CanInteract => _canInteract;

        public void SetCanInteract(bool canInteract)
        {
            _canInteract = canInteract;
        }

        public void Interact(GameObject interactor)
        {
            if (!_canInteract)
            {
                onBlocked.Invoke();
                return;
            }

            onPulled.Invoke();
        }
    }
}
