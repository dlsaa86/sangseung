using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 양 끝이 고정된 **늘어진 케이블**. 매달린 것과 반대 문제다 — 끝이 못 움직이므로
    /// 반응은 전부 가운데에서 일어난다. 옆으로 쓸리고, 위아래로 출렁이고,
    /// 가속이 끝나면 늘어진 상태로 되돌아온다.
    ///
    /// <see cref="HangingChain"/> 과 왜 나눴는가: 구속 조건이 다르면 눈에 보이는 것이
    /// 다르다. 사슬은 **끝이 가장 크게** 움직이고, 팽팽한 케이블은 **가운데가** 움직인다.
    /// 같은 컴포넌트로 둘 다 하려면 파라미터로 구속을 표현해야 하는데, 그러면
    /// 배선하는 사람이 어느 조합이 무엇인지 알 수 없게 된다.
    ///
    /// 처짐(sag)을 물리로 풀지 않는다. 케이블 길이 보존은 위치 기반 동역학이 필요하고,
    /// 그건 이 화면 크기에서 아무도 알아채지 못하는 정확도에 프레임 예산을 쓰는 일이다.
    /// 여기서는 처짐을 **정적 오프셋**으로 두고, 그 위에 흔들림을 얹는다.
    /// </summary>
    public sealed class CableSway : CabinInertiaReactor
    {
        [Header("가운데 마디 — 이것만 움직인다")]
        [Tooltip("케이블 중앙 트랜스폼. 양 끝 앵커는 건드리지 않는다.")]
        [SerializeField] private Transform _midPoint;

        [Tooltip("추가로 함께 쓸릴 보조 마디들. 중앙에서 멀수록 덜 움직인다.")]
        [SerializeField] private Transform[] _secondary;

        [Header("흔들림")]
        [Tooltip("옆으로 쓸리는 최대 거리(m). 판독성 상한.")]
        [SerializeField, Range(0.01f, 0.6f)] private float _maxLateral = 0.14f;

        [Tooltip("위아래 출렁임 최대 거리(m).")]
        [SerializeField, Range(0.005f, 0.4f)] private float _maxVertical = 0.08f;

        [Tooltip("가로 진동 고유 각진동수(rad/s). 팽팽할수록 크다.")]
        [SerializeField, Range(1.5f, 30f)] private float _lateralOmega = 6.5f;

        [Tooltip("세로 진동 고유 각진동수(rad/s). 보통 가로보다 두 배쯤 빠르다.")]
        [SerializeField, Range(2f, 40f)] private float _verticalOmega = 13f;

        [Tooltip("감쇠비. 0.12 면 오래 출렁이고 0.5 면 금방 죽는다.")]
        [SerializeField, Range(0.03f, 1.2f)] private float _zeta = 0.16f;

        [Tooltip("가속도를 변위로 바꾸는 순응도(m per m/s²).")]
        [SerializeField, Range(0.001f, 0.08f)] private float _compliance = 0.012f;

        [Tooltip("보조 마디가 중앙 대비 갖는 비율. 0.45 면 절반쯤 따라간다.")]
        [SerializeField, Range(0f, 1f)] private float _secondaryRatio = 0.45f;

        private DampedSpring1D _x;
        private DampedSpring1D _z;
        private DampedSpring1D _y;
        private Vector3 _homeMid;
        private Vector3[] _homeSecondary;

        public override bool IsAtRest => _x.IsAtRest() && _z.IsAtRest() && _y.IsAtRest();

        /// <summary>중앙 마디의 현재 변위(m). 진폭 상한 검증이 읽는다.</summary>
        public Vector3 Displacement => new Vector3(_x.Value, _y.Value, _z.Value);

        protected override void CaptureHome()
        {
            if (_midPoint != null) _homeMid = _midPoint.localPosition;
            int n = _secondary != null ? _secondary.Length : 0;
            _homeSecondary = new Vector3[n];
            for (int i = 0; i < n; i++)
                if (_secondary[i] != null) _homeSecondary[i] = _secondary[i].localPosition;
        }

        protected override void RestoreHome()
        {
            _x.Reset(); _z.Reset(); _y.Reset();
            if (_midPoint != null) _midPoint.localPosition = _homeMid;
            if (_secondary == null || _homeSecondary == null) return;
            for (int i = 0; i < _secondary.Length && i < _homeSecondary.Length; i++)
                if (_secondary[i] != null) _secondary[i].localPosition = _homeSecondary[i];
        }

        protected override void Integrate(float dt)
        {
            Vector3 a = LocalAcceleration;
            // 케이블은 가속과 **반대로** 밀린다. 부호를 뒤집으면 "카가 앞으로 나가는데
            // 케이블도 앞으로 나간다"가 되어 관성이 아니라 자석처럼 읽힌다.
            _x.Step(dt, Mathf.Clamp(-a.x * _compliance, -_maxLateral, _maxLateral),
                    _lateralOmega, _zeta, _maxLateral);
            _z.Step(dt, Mathf.Clamp(-a.z * _compliance, -_maxLateral, _maxLateral),
                    _lateralOmega, _zeta, _maxLateral);
            _y.Step(dt, Mathf.Clamp(-a.y * _compliance, -_maxVertical, _maxVertical),
                    _verticalOmega, _zeta, _maxVertical);
        }

        protected override void Apply()
        {
            Vector3 d = new Vector3(_x.Value, _y.Value, _z.Value);

            if (_midPoint != null) _midPoint.localPosition = _homeMid + d;

            if (_secondary == null || _homeSecondary == null) return;
            Vector3 sd = d * _secondaryRatio;
            for (int i = 0; i < _secondary.Length && i < _homeSecondary.Length; i++)
                if (_secondary[i] != null) _secondary[i].localPosition = _homeSecondary[i] + sd;
        }

        public override void AddShock(Vector3 worldImpulse)
        {
            Vector3 local = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldImpulse)
                : worldImpulse;
            _x.AddImpulse(-local.x * _compliance * 8f);
            _z.AddImpulse(-local.z * _compliance * 8f);
            _y.AddImpulse(-local.y * _compliance * 8f);
        }
    }
}
