using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 천장 크레인 고리. 진자로 흔들리면서 **자기 축으로도 돈다.**
    ///
    /// 비틀림 축이 이 컴포넌트가 램프와 다른 이유다. 매달린 고리는 흔들림이 잦아든
    /// 뒤에도 한참을 천천히 돌고, 그 잔여 회전이 「방금 무슨 일이 있었다」를 가장
    /// 오래 남긴다. 진자만 있으면 몇 초 만에 완전히 죽은 물체가 된다.
    ///
    /// 비틀림은 복원력이 거의 없는 감쇠 운동이다 — 줄이 꼬였다 풀리는 것뿐이라
    /// 스프링이 아니라 **아주 약한 복원 + 낮은 감쇠**로 둔다. 복원을 0 으로 두면
    /// 영원히 도는데, 그건 배경 애니메이션이지 반응이 아니다.
    /// </summary>
    public sealed class SwayingHook : CabinInertiaReactor
    {
        [Header("고리")]
        [Tooltip("비면 이 오브젝트 자신.")]
        [SerializeField] private Transform _pivot;

        [Header("진자")]
        [SerializeField, Range(0.1f, 3f)] private float _length = 0.65f;
        [SerializeField, Range(0.05f, 6f)] private float _damping = 0.75f;

        [Tooltip("최대 흔들림 각도(도). 판독성 상한.")]
        [SerializeField, Range(1f, 30f)] private float _maxAngleDegrees = 15f;

        [Header("비틀림 — 이 축이 고리를 고리답게 만든다")]
        [Tooltip("비틀림 고유 각진동수(rad/s). 작을수록 천천히 되돌아온다.")]
        [SerializeField, Range(0.2f, 8f)] private float _twistOmega = 1.4f;

        [Tooltip("비틀림 감쇠비. 0.08 이면 몇 바퀴 돌다 잦아든다.")]
        [SerializeField, Range(0.02f, 1f)] private float _twistZeta = 0.09f;

        [Tooltip("최대 비틀림 각도(도).")]
        [SerializeField, Range(5f, 180f)] private float _maxTwistDegrees = 80f;

        [Tooltip("가로 가속이 비틀림으로 새는 비율. 고리가 완전 대칭이 아니라서 생긴다.")]
        [SerializeField, Range(0f, 1f)] private float _twistCoupling = 0.35f;

        [Tooltip("비틀림 비대칭 시드. 같은 시드는 같은 방향으로 돈다 — 캡처 재현용이다.")]
        [SerializeField] private int _twistSeed = 20260802;

        private PendulumState _pendulum;
        private DampedSpring1D _twist;
        private Quaternion _homeRotation;
        private float _bias;

        public override bool IsAtRest => _pendulum.IsAtRest() && _twist.IsAtRest();

        /// <summary>비틀림 각도(도). 상한 검증이 읽는다.</summary>
        public float TwistDegrees => _twist.Value * Mathf.Rad2Deg;

        /// <summary>흔들림 각도(도).</summary>
        public Vector2 SwingDegrees =>
            new Vector2(_pendulum.AngleX * Mathf.Rad2Deg, _pendulum.AngleZ * Mathf.Rad2Deg);

        protected override void Awake()
        {
            if (_pivot == null) _pivot = transform;
            // 비대칭은 결정론적으로 만든다. `UnityEngine.Random` 은 전역 스트림이라
            // 판정 RNG 와 섞이면 시드 재현이 깨진다(TECH_SPEC §7).
            _bias = HashSigned(_twistSeed, StableVariationKey());
            if (Mathf.Abs(_bias) < 0.2f) _bias = _bias >= 0f ? 0.2f : -0.2f;
            base.Awake();
        }

        protected override void CaptureHome()
        {
            if (_pivot == null) _pivot = transform;
            _homeRotation = _pivot.localRotation;
        }

        protected override void RestoreHome()
        {
            _pendulum.Reset();
            _twist.Reset();
            if (_pivot != null) _pivot.localRotation = _homeRotation;
        }

        protected override void Integrate(float dt)
        {
            Vector3 a = LocalAcceleration;
            _pendulum.Step(dt, a.z, -a.x, a.y, _length, _damping, _maxAngleDegrees * Mathf.Deg2Rad);

            float maxTwist = _maxTwistDegrees * Mathf.Deg2Rad;
            // 가로 가속의 크기가 비틀림을 몰고, 방향은 이 고리 고유의 비대칭이 정한다.
            float drive = (Mathf.Abs(a.x) + Mathf.Abs(a.z)) * _twistCoupling * _bias * 0.03f;
            _twist.Step(dt, Mathf.Clamp(drive, -maxTwist, maxTwist),
                        _twistOmega, _twistZeta, maxTwist);
        }

        protected override void Apply()
        {
            if (_pivot == null) return;
            _pivot.localRotation = _homeRotation * Quaternion.Euler(
                _pendulum.AngleX * Mathf.Rad2Deg,
                _twist.Value * Mathf.Rad2Deg,
                _pendulum.AngleZ * Mathf.Rad2Deg);
        }

        public override void AddShock(Vector3 worldImpulse)
        {
            Vector3 local = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldImpulse)
                : worldImpulse;
            float invL = 1f / Mathf.Max(_length, 0.05f);
            _pendulum.AddImpulse(local.z * invL, -local.x * invL);
            _twist.AddImpulse(local.magnitude * _twistCoupling * _bias * 0.6f);
        }
    }
}
