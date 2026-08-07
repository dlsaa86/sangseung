using UnityEngine;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 로컬 X 로 미는 뷰와 **다른 축으로 저작된 문짝** 사이를 잇는다.
    ///
    /// ## 왜 필요한가
    ///
    /// <see cref="ElevatorGrayboxView"/> 는 문을 <c>localPosition.x</c> 로 민다.
    /// 옛 그레이박스 문은 그 축으로 만들어져 있었다. 블렌더 AD47 캐빈의 문은
    /// 로컬 Y 로 저작됐고 FBX 축 변환(<c>axis_up='Y'</c>)을 거치며 **월드 Z** 가 됐다.
    ///
    /// 보통은 문짝을 회전된 피벗 밑으로 옮겨 축을 맞춘다. 그런데 AD47 캐빈은 FBX
    /// **프리팹 인스턴스**라 그 안의 트랜스폼을 다른 부모로 옮길 수 없다
    /// (「Setting the parent of a transform which resides in a Prefab instance is not
    /// possible」). 프리팹을 풀면 옮길 수 있지만 그러면 FBX 링크가 끊겨 블렌더에서
    /// 다시 구운 메시가 씬에 반영되지 않는다 — 이 프로젝트는 캐빈을 계속 다시 굽는다.
    ///
    /// 그래서 **뷰가 미는 대상은 빈 프록시**로 두고, 이 컴포넌트가 매 프레임 그
    /// 이동량을 읽어 진짜 문짝을 월드 축으로 옮긴다. 뷰도 프리팹도 건드리지 않는다.
    ///
    /// ## 왜 월드 좌표로 쓰나
    ///
    /// 문짝의 부모(<c>CabinAD47</c>)는 Y 180° 로 돌아 있다. 로컬 좌표로 계산하면
    /// 부호가 뒤집혀 「열리는 방향이 반대」가 되고, 그 실수는 캡처로 보기 전까지
    /// 드러나지 않는다. 닫힘 위치를 월드로 기억하고 월드 축으로 더한다.
    /// </summary>
    [ExecuteAlways]
    public sealed class DoorAxisAdapter : MonoBehaviour
    {
        /// <summary>뷰가 실제로 미는 빈 오브젝트. 로컬 X 만 쓴다.</summary>
        [SerializeField] private Transform _driver;

        /// <summary>진짜 문짝. 프리팹 인스턴스 안에 있어도 좌표는 쓸 수 있다.</summary>
        [SerializeField] private Transform _leaf;

        /// <summary>닫힘 상태의 문짝 월드 좌표. 편집 시 <see cref="CaptureClosed"/> 로 채운다.</summary>
        [SerializeField] private Vector3 _closedWorld;

        /// <summary>열리는 방향(월드). 정규화해서 쓴다.</summary>
        [SerializeField] private Vector3 _openDirection = Vector3.forward;

        /// <summary>뷰의 <c>_doorSlide</c> 와 같아야 한다. 다르면 이동량이 어긋난다.</summary>
        [SerializeField, Min(0.01f)] private float _viewSlide = 1f;

        /// <summary>문짝의 실제 편도 이동량(m).</summary>
        [SerializeField, Min(0.01f)] private float _travel = 1f;

        /// <summary>지금 열린 정도 0..1. 검사용.</summary>
        public float Amount { get; private set; }

        public void Configure(Transform driver, Transform leaf, Vector3 openDirection, float viewSlide, float travel)
        {
            _driver = driver;
            _leaf = leaf;
            _openDirection = openDirection.normalized;
            _viewSlide = Mathf.Max(0.01f, viewSlide);
            _travel = Mathf.Max(0.01f, travel);
            CaptureClosed();
        }

        /// <summary>현재 문짝 위치를 「닫힘」으로 기억한다.</summary>
        public void CaptureClosed()
        {
            if (_leaf != null) _closedWorld = _leaf.position;
        }

        private void LateUpdate()
        {
            if (_driver == null || _leaf == null) return;

            // 뷰는 왼쪽에 −slide·amount, 오른쪽에 +slide·amount 를 넣는다.
            // 방향은 `_openDirection` 이 이미 들고 있으므로 크기만 쓴다.
            Amount = Mathf.Clamp01(Mathf.Abs(_driver.localPosition.x) / _viewSlide);
            _leaf.position = _closedWorld + _openDirection * (_travel * Amount);
        }
    }
}
