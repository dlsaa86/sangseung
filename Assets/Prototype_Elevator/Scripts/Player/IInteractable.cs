using UnityEngine;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// Contract for objects that can be used from the first-person crosshair.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short text shown while the crosshair is on this object.</summary>
        string Prompt { get; }

        /// <summary>Whether a click should currently invoke <see cref="Interact"/>.</summary>
        bool CanInteract { get; }

        /// <summary>Called when the object is clicked by an interactor.</summary>
        void Interact(GameObject interactor);
    }
}
