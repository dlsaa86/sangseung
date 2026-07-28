using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>Thin scene-facing contract panel interaction stub.</summary>
    public sealed class InteractableContractPanel : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool _canInteract = true;
        [SerializeField] public UnityEvent onOpened = new UnityEvent();

        public string Prompt => "계약 선택";
        public bool CanInteract => _canInteract;

        public void SetCanInteract(bool canInteract)
        {
            _canInteract = canInteract;
        }

        public void Interact(GameObject interactor)
        {
            if (!_canInteract)
                return;

            onOpened.Invoke();
        }
    }
}
