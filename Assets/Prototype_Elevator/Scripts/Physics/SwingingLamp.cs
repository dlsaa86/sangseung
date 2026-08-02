using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 천장등이 카의 가감속에 뒤처져 흔들린다. 「엘리베이터에 타고 있다」를 만드는
    /// 가장 값싼 신호이고, 지금 이 프로젝트에 완전히 없는 것이다.
    ///
    /// **그림자가 함께 움직인다** — 별도 코드가 필요 없다. 이 컴포넌트를 붙인
    /// 피벗의 **자식**에 <c>Light</c> 를 두면 광원이 같이 회전하고, URP 의 실시간
    /// 그림자가 그 결과를 그린다. 반대로 광원을 피벗 밖에 두면 등만 흔들리고
    /// 그림자는 굳어 있어 「가짜」로 읽힌다 — 배선에서 가장 틀리기 쉬운 지점이다.
    ///
    /// 진자 방정식을 쓴다(<see cref="PendulumState"/>). 감쇠 스프링으로도 비슷하게
    /// 보이지만, 진자는 <b>줄 길이</b>가 주기를 정하기 때문에 짧은 표찰과 긴 케이블이
    /// 같은 가속에 **서로 다른 박자**로 반응한다. 그 불일치가 공간을 물리적으로 만든다.
    /// </summary>
    public sealed class SwingingLamp : CabinInertiaReactor
    {
        [Header("매달린 지점")]
        [Tooltip("회전시킬 피벗. 비면 이 오브젝트 자신. 광원과 등갓을 이 아래에 둔다.")]
        [SerializeField] private Transform _pivot;

        [Header("진자")]
        [Tooltip("줄 길이(m). 길수록 느리게 흔들린다. 확대된 캐빈(천장 5.5m)에서는 0.6~1.2 가 읽힌다.")]
        [SerializeField, Range(0.1f, 3f)] private float _length = 0.8f;

        [Tooltip("감쇠(1/s). 낮으면 오래 흔들린다. 0.6 이면 3~4초 안에 잦아든다.")]
        [SerializeField, Range(0.05f, 6f)] private float _damping = 0.9f;

        [Tooltip("최대 흔들림 각도(도). 판독성 상한 — `VISUAL_SPEC` §8. " +
                 "14도를 넘으면 등이 시야를 쓸고 지나가 계기를 읽을 수 없다.")]
        [SerializeField, Range(1f, 25f)] private float _maxAngleDegrees = 12f;

        [Header("케이블 늘어남 — 있으면 더 무겁게 읽힌다")]
        [Tooltip("수직 가속에 따라 아래위로 미세하게 늘어나는 양(m). 0 이면 끈다.")]
        [SerializeField, Range(0f, 0.12f)] private float _stretch = 0.03f;

        [Tooltip("늘어남의 고유 각진동수(rad/s).")]
        [SerializeField, Range(4f, 40f)] private float _stretchOmega = 16f;

        private PendulumState _pendulum;
        private DampedSpring1D _drop;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public override bool IsAtRest => _pendulum.IsAtRest() && _drop.IsAtRest();

        /// <summary>현재 흔들림 각도(도). 프로브와 테스트가 진폭 상한을 확인한다.</summary>
        public Vector2 AngleDegrees =>
            new Vector2(_pendulum.AngleX * Mathf.Rad2Deg, _pendulum.AngleZ * Mathf.Rad2Deg);

        /// <summary>진자의 총 에너지. 「발산하지 않는다」의 관측축.</summary>
        public float Energy => _pendulum.Energy(_length);

        protected override void Awake()
        {
            if (_pivot == null) _pivot = transform;
            base.Awake();
        }

        protected override void CaptureHome()
        {
            if (_pivot == null) _pivot = transform;
            _homePosition = _pivot.localPosition;
            _homeRotation = _pivot.localRotation;
        }

        protected override void RestoreHome()
        {
            _pendulum.Reset();
            _drop.Reset();
            if (_pivot == null) return;
            _pivot.localPosition = _homePosition;
            _pivot.localRotation = _homeRotation;
        }

        protected override void Integrate(float dt)
        {
            Vector3 a = LocalAcceleration;
            _pendulum.Step(dt, a.z, -a.x, a.y, _length, _damping,
                           _maxAngleDegrees * Mathf.Deg2Rad);

            if (_stretch > 0f)
            {
                // 위로 가속하면 케이블이 늘어난다(등이 내려간다). 부호가 뒤집히면
                // 「올라가는데 등이 떠오른다」가 되어 즉시 틀린 것으로 읽힌다.
                float target = Mathf.Clamp(-a.y / PendulumState.Gravity, -1f, 1f) * _stretch;
                _drop.Step(dt, target, _stretchOmega, 0.55f, _stretch * 1.6f);
            }
        }

        protected override void Apply()
        {
            if (_pivot == null) return;

            // 홈에 **더한다.** 누적하지 않는다 — 매 프레임 절대값을 다시 쓰므로
            // 오프셋이 0 이 되는 순간 홈이 비트 단위로 복원된다.
            _pivot.localRotation = _homeRotation * Quaternion.Euler(
                _pendulum.AngleX * Mathf.Rad2Deg, 0f, _pendulum.AngleZ * Mathf.Rad2Deg);

            if (_stretch > 0f)
            {
                Vector3 p = _homePosition;
                p.y += _drop.Value;
                _pivot.localPosition = p;
            }
        }

        public override void AddShock(Vector3 worldImpulse)
        {
            Vector3 local = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldImpulse)
                : worldImpulse;
            _pendulum.AddImpulse(local.z / Mathf.Max(_length, 0.05f),
                                 -local.x / Mathf.Max(_length, 0.05f));
            _drop.AddImpulse(local.y * 0.2f);
        }
    }
}
