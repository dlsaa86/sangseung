using System;
using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>Serializable int event so a passenger index can be wired in the Inspector.</summary>
    [Serializable]
    public sealed class PassengerBoardRequestedEvent : UnityEvent<int> { }

    /// <summary>Thin scene-facing passenger interaction stub.</summary>
    public sealed class InteractablePassenger : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool _canInteract = true;
        [SerializeField] private int _passengerIndex;
        [SerializeField] public PassengerBoardRequestedEvent onBoardRequested =
            new PassengerBoardRequestedEvent();

        public string Prompt => "탑승 요청";
        public bool CanInteract => _canInteract;
        public int PassengerIndex => _passengerIndex;

        public void SetCanInteract(bool canInteract)
        {
            _canInteract = canInteract;
        }

        public void SetPassengerIndex(int index)
        {
            _passengerIndex = index;
        }

        public void Interact(GameObject interactor)
        {
            if (!_canInteract)
                return;

            onBoardRequested.Invoke(_passengerIndex);
        }
    }
}
