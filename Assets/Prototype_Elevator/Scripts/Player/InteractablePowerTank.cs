using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>Thin scene-facing power tank interaction stub.</summary>
    public sealed class InteractablePowerTank : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool _canInteract = true;
        [SerializeField] public UnityEvent onBanked = new UnityEvent();

        [SerializeField] private string _prompt = "전력 확정";

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
                return;

            onBanked.Invoke();
        }
    }
}
