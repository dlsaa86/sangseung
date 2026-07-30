using System;
using UnityEngine;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 승강장에서 기다리는 후보 하나. 자기 번호만 알고 게임 상태는 모른다 —
    /// 다른 `Interactable*` 스텁과 같은 규약이다(배치와 로직을 따로 작업하기 위해서다).
    ///
    /// 다른 스텁이 `UnityEvent`를 쓰는 것과 달리 여기는 `Action&lt;int&gt;`다.
    /// 후보는 런타임에 생겼다 사라지므로 인스펙터에서 연결할 수 없고,
    /// 어느 번호가 눌렸는지를 함께 넘겨야 한다.
    /// </summary>
    public sealed class InteractableBuildCandidate : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _prompt = "탑승";
        [SerializeField] private bool _canInteract = true;
        [SerializeField] private int _index;

        /// <summary>눌렸을 때 후보 번호와 함께 호출된다. 뷰가 붙인다.</summary>
        public event Action<int> Picked;

        public int Index => _index;
        public string Prompt => _prompt;
        public bool CanInteract => _canInteract;

        public void Configure(int index, string prompt)
        {
            _index = index;
            if (!string.IsNullOrEmpty(prompt)) _prompt = prompt;
        }

        public void SetCanInteract(bool canInteract) => _canInteract = canInteract;

        public void Interact(GameObject interactor)
        {
            if (!_canInteract) return;
            Picked?.Invoke(_index);
        }
    }
}
