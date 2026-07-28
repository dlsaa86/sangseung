using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>Thin scene-facing lever interaction stub.</summary>
    public sealed class InteractableLever : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool _canInteract = true;
        [SerializeField] public UnityEvent onPulled = new UnityEvent();

        public string Prompt => "레버 당기기";
        public bool CanInteract => _canInteract;

        public void SetCanInteract(bool canInteract)
        {
            _canInteract = canInteract;
        }

        public void Interact(GameObject interactor)
        {
            if (!_canInteract)
                return;

            onPulled.Invoke();
        }
    }
}
