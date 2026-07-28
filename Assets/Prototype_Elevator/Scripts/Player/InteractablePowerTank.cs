using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>Thin scene-facing power tank interaction stub.</summary>
    public sealed class InteractablePowerTank : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool _canInteract = true;
        [SerializeField] public UnityEvent onBanked = new UnityEvent();

        public string Prompt => "전력 확정";
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
