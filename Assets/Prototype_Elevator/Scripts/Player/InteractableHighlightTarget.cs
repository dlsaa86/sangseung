using UnityEngine;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 이 상호작용물을 조준했을 때 **무엇을 빛낼지** 지정한다. 없으면 자기 자신을 빛낸다.
    ///
    /// ## 왜 필요한가
    ///
    /// 조준 상자와 보이는 물체가 같지 않은 경우가 있다.
    ///   · `MachineFocusTarget` — 콜라이더만 있는 빈 오브젝트. 빛낼 것은 `SOCKET_ElevPanel` 이다
    ///   · `ExecutionLever`      — 조준은 자식 `AimProxy_Exec` 가 받지만 빛낼 것은 레버 몸체다
    ///
    /// 이것이 없으면 「조준되는 것」과 「빛나는 것」이 갈라져, 아무것도 안 빛나거나
    /// 엉뚱한 것이 빛난다.
    /// </summary>
    public sealed class InteractableHighlightTarget : MonoBehaviour
    {
        [Tooltip("빛낼 범위의 루트. 비우면 이 오브젝트 자신.")]
        [SerializeField] private Transform _root;

        public Transform Root => _root != null ? _root : transform;
    }
}
