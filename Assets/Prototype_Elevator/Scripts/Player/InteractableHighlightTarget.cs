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

        [Tooltip("보이는 것이 여러 곳에 흩어져 있을 때. 하나라도 채우면 이쪽이 이긴다.")]
        [SerializeField] private Transform[] _roots;

        public Transform Root => _root != null ? _root : transform;

        /// <summary>
        /// 빛낼 범위 전부. **여러 개가 필요한 이유는 이 저장소의 이중 소유 구조 때문이다.**
        ///
        /// `InteractableLever` 는 `GrayboxWorld/Car/Console/ExecutionLever` 에 붙어 있는데
        /// 그 렌더러는 꺼져 있고, 화면에 보이는 레버는 `CabinAD47/SOCKET_ElevPanel` 아래
        /// `SM_Lever_Handle.001` · `.003` · `SM_LeverBay` **세 조각**이다. 셋은 FBX 프리팹
        /// 인스턴스의 자식이라 **한 부모 밑으로 묶을 수 없다**(기존 자식 재배치 불가).
        ///
        /// 그래서 루트를 하나로 제한하면 「보이는 것을 가리킬 방법이 없는」 상호작용물이
        /// 생긴다. 조준 콜라이더로 물러나면 되지만, 레버의 경우 그 콜라이더가 보이는
        /// 메시에서 **800 mm 떨어진 24 mm 상자**라 허공에 테두리가 뜬다.
        /// </summary>
        public Transform[] Roots
        {
            get
            {
                if (_roots != null && _roots.Length > 0)
                {
                    bool anyValid = false;
                    for (int i = 0; i < _roots.Length; i++) if (_roots[i] != null) { anyValid = true; break; }
                    if (anyValid) return _roots;
                }
                return new[] { Root };
            }
        }
    }
}
