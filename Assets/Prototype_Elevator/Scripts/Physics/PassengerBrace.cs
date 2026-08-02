using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 승객이 가속에 맞춰 몸을 버틴다. 매달린 물체와 **반대 부호**로 움직인다.
    ///
    /// 이것이 이 컴포넌트의 전부이자 핵심이다. 죽은 물체는 가속의 반대로 뒤처지지만,
    /// 서 있는 사람은 넘어지지 않으려고 **가속 쪽으로 몸을 기울인다.** 램프와 승객이
    /// 같은 방향으로 기울면 승객이 시체로 읽힌다 — 관성 연출에서 가장 자주 나오는
    /// 틀린 그림이다.
    ///
    /// 그래서 임계 감쇠에 가깝게(ζ ≈ 0.9) 둔다. 사람은 출렁이지 않는다. 오버슛이
    /// 보이면 그건 버티는 게 아니라 휘청이는 것이고, 그 상태가 계속되면 술 취한
    /// 사람이 된다.
    ///
    /// 승객의 **판정**과는 아무 관계가 없다. `PassengerReactionDirector` 가 정하는
    /// 반응 키·자세와 별개 축이고, 이 컴포넌트는 어떤 게임 상태도 읽거나 쓰지 않는다.
    /// </summary>
    public sealed class PassengerBrace : CabinInertiaReactor
    {
        [Header("몸통")]
        [Tooltip("기울일 피벗(보통 허리 또는 상체 루트). 비면 이 오브젝트 자신.")]
        [SerializeField] private Transform _torso;

        [Tooltip("반대 위상으로 살짝 미는 머리. 없어도 된다 — 있으면 훨씬 살아 보인다.")]
        [SerializeField] private Transform _head;

        [Header("버티기")]
        [Tooltip("가속 1 m/s² 당 기울기(도). 사람은 생각보다 조금만 기운다.")]
        [SerializeField, Range(0.05f, 2.5f)] private float _degreesPerAccel = 0.55f;

        [Tooltip("최대 기울기(도). 8도를 넘으면 넘어지는 것으로 읽힌다.")]
        [SerializeField, Range(1f, 15f)] private float _maxTiltDegrees = 7f;

        [Tooltip("반응 속도(rad/s). 사람의 반사는 빠르다.")]
        [SerializeField, Range(3f, 30f)] private float _omega = 11f;

        [Tooltip("감쇠비. 0.9 는 거의 임계 감쇠 — 출렁이지 않는다.")]
        [SerializeField, Range(0.3f, 1.5f)] private float _zeta = 0.92f;

        [Tooltip("머리가 몸통과 반대로 도는 비율. 0.4 면 목이 살아 있다.")]
        [SerializeField, Range(0f, 1f)] private float _headCounter = 0.4f;

        [Header("무릎 — 수직 가속을 흡수한다")]
        [Tooltip("수직 가속에 따라 몸이 내려앉는 최대 거리(m). 0 이면 끈다.")]
        [SerializeField, Range(0f, 0.12f)] private float _maxCrouch = 0.045f;

        private DampedSpring1D _tiltX;
        private DampedSpring1D _tiltZ;
        private DampedSpring1D _crouch;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Quaternion _homeHeadRotation;

        public override bool IsAtRest => _tiltX.IsAtRest() && _tiltZ.IsAtRest() && _crouch.IsAtRest();

        /// <summary>현재 기울기(도). 상한 검증이 읽는다.</summary>
        public Vector2 TiltDegrees => new Vector2(_tiltX.Value, _tiltZ.Value);

        protected override void Awake()
        {
            if (_torso == null) _torso = transform;
            base.Awake();
        }

        protected override void CaptureHome()
        {
            if (_torso == null) _torso = transform;
            _homePosition = _torso.localPosition;
            _homeRotation = _torso.localRotation;
            if (_head != null) _homeHeadRotation = _head.localRotation;
        }

        protected override void RestoreHome()
        {
            _tiltX.Reset();
            _tiltZ.Reset();
            _crouch.Reset();
            if (_torso != null)
            {
                _torso.localPosition = _homePosition;
                _torso.localRotation = _homeRotation;
            }
            if (_head != null) _head.localRotation = _homeHeadRotation;
        }

        protected override void Integrate(float dt)
        {
            Vector3 a = LocalAcceleration;

            // **부호가 매달린 것과 반대다.** 가속 방향으로 몸을 넣어 버틴다.
            float targetX = Mathf.Clamp(-a.z * _degreesPerAccel, -_maxTiltDegrees, _maxTiltDegrees);
            float targetZ = Mathf.Clamp(a.x * _degreesPerAccel, -_maxTiltDegrees, _maxTiltDegrees);

            _tiltX.Step(dt, targetX, _omega, _zeta, _maxTiltDegrees);
            _tiltZ.Step(dt, targetZ, _omega, _zeta, _maxTiltDegrees);

            if (_maxCrouch > 0f)
            {
                // 위로 가속하면 무릎이 접힌다(내려앉는다).
                float target = Mathf.Clamp(-a.y / PendulumState.Gravity, -1f, 1f) * _maxCrouch;
                _crouch.Step(dt, target, _omega * 0.8f, 0.95f, _maxCrouch * 1.5f);
            }
        }

        protected override void Apply()
        {
            if (_torso == null) return;

            _torso.localRotation = _homeRotation * Quaternion.Euler(_tiltX.Value, 0f, _tiltZ.Value);

            if (_maxCrouch > 0f)
            {
                Vector3 p = _homePosition;
                p.y += _crouch.Value;
                _torso.localPosition = p;
            }

            if (_head != null && _headCounter > 0f)
                _head.localRotation = _homeHeadRotation * Quaternion.Euler(
                    -_tiltX.Value * _headCounter, 0f, -_tiltZ.Value * _headCounter);
        }

        public override void AddShock(Vector3 worldImpulse)
        {
            Vector3 local = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldImpulse)
                : worldImpulse;
            _tiltX.AddImpulse(-local.z * _degreesPerAccel * 2f);
            _tiltZ.AddImpulse(local.x * _degreesPerAccel * 2f);
            _crouch.AddImpulse(-Mathf.Abs(local.y) * 0.05f);
        }
    }
}
