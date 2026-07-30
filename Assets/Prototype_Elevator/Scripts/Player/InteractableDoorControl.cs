using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 문 개폐 손잡이. 뜻은 하나다 — "적재를 끝내고 출발한다".
    ///
    /// 실행 레버가 이 역할을 겸하지 않는 이유는 `D-20260730-08`과 같다.
    /// 한 물체가 두 뜻을 가지면 플레이어는 무엇을 고르는지 모른 채 당긴다.
    /// </summary>
    public sealed class InteractableDoorControl : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _prompt = "문 닫고 출발";
        [SerializeField] private bool _canInteract;
        [SerializeField] public UnityEvent onDepart = new UnityEvent();

        public string Prompt => _prompt;
        public bool CanInteract => _canInteract;

        public void SetPrompt(string prompt)
        {
            if (!string.IsNullOrEmpty(prompt)) _prompt = prompt;
        }

        public void SetCanInteract(bool canInteract) => _canInteract = canInteract;

        public void Interact(GameObject interactor)
        {
            if (!_canInteract) return;
            onDepart.Invoke();
        }
    }
}
