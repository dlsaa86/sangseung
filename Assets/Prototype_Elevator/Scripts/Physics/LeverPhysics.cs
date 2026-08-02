using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 레버 손잡이가 **관성을 갖고** 내려갔다가 되돌아온다.
    ///
    /// 왜 이것이 필요한가: `UP-FIX-03` 「과수확 레버가 '당김'을 형상으로 전달하지
    /// 못한다」가 열려 있다. 지금 `InteractableOverharvestLever.ApplyHandle()` 은
    /// <c>Mathf.Lerp(rest, pulled, smoothstep(amount))</c> 다. Lerp 는 **끝에서 멈춘다** —
    /// 당김의 증거는 끝점이 아니라 <b>끝을 지나쳤다가 되돌아오는 것</b>에 있다.
    /// 사람이 「당겼다」를 읽는 신호는 도달이 아니라 반동이다.
    ///
    /// 감쇠 스프링으로 힌지 각도를 적분한다. 임펄스를 받으면 목표를 지나쳐 내려갔다가
    /// 몇 번 튀고 원위치로 돌아온다.
    ///
    /// <b>기존 레버를 대체하지 않는다.</b> `InteractableOverharvestLever` 는 자기
    /// <c>_handlePivot</c> 을 계속 몬다. 그래서 이 컴포넌트는 **다른 피벗**(손잡이 아래의
    /// 반동 전용 자식, 또는 그 레버가 몰지 않는 별도 손잡이)에 붙인다. 같은 트랜스폼에
    /// 두 주인을 두면 둘이 매 프레임 싸우고, 그건 물리가 아니라 떨림으로 보인다.
    /// 배선 지침은 인수인계 문서에 있다.
    ///
    /// <c>HingeJoint</c> 를 쓰지 않은 이유: 조인트는 <c>Rigidbody</c> 를 요구하고,
    /// 그러면 이 손잡이의 위치가 <c>FixedUpdate</c> 의 해석 결과가 된다. 캡처 하네스가
    /// 「레버를 당긴 직후」를 두 번 잡으면 두 번 다른 각도가 나온다. 그건 회귀 판정을
    /// 불가능하게 만든다 — 1축 힌지 하나에 그 대가를 치를 이유가 없다.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class LeverPhysics : MonoBehaviour
    {
        [Header("힌지")]
        [Tooltip("회전시킬 피벗. 비면 이 오브젝트 자신.")]
        [SerializeField] private Transform _pivot;

        [Tooltip("회전 축(로컬). 기본은 X — 앞뒤로 당기는 레버다.")]
        [SerializeField] private Vector3 _axis = Vector3.right;

        [Header("스프링")]
        [Tooltip("쉬는 각도(도). 홈 회전에 이만큼 더한 자세가 평상시다.")]
        [SerializeField, Range(-90f, 90f)] private float _restAngle;

        [Tooltip("고유 각진동수(rad/s). 클수록 딱딱하고 빠르게 되돌아온다.")]
        [SerializeField, Range(3f, 45f)] private float _omega = 15f;

        [Tooltip("감쇠비. 0.18 이면 두세 번 튀고 멈춘다. 1 이면 오버슛이 사라져 Lerp 와 같아진다.")]
        [SerializeField, Range(0.02f, 1.2f)] private float _zeta = 0.19f;

        [Tooltip("최대 각변위(도). 손잡이가 하우징을 뚫고 들어가지 않게 한다.")]
        [SerializeField, Range(5f, 120f)] private float _maxSwingDegrees = 62f;

        [Header("당김")]
        [Tooltip("한 번 당길 때 넣는 각속도(도/s). 클수록 세게 내려간다.")]
        [SerializeField, Range(30f, 900f)] private float _pullImpulse = 340f;

        [Tooltip("당김 방향. -1 이면 아래로, +1 이면 위로.")]
        [SerializeField] private float _pullSign = -1f;

        [Tooltip("당김 직후 손잡이가 붙잡혀 있는 시간(초). 0 이면 즉시 되돌아온다.")]
        [SerializeField, Range(0f, 0.6f)] private float _holdSeconds = 0.12f;

        [Header("반동을 전달받을 곳 — 있으면 레버가 무겁게 읽힌다")]
        [Tooltip("당길 때 함께 흔들릴 하우징·패널. 없어도 된다.")]
        [SerializeField] private CabinInertiaReactor[] _shakeOnPull;

        [Tooltip("하우징에 전달하는 충격 세기(m/s).")]
        [SerializeField, Range(0f, 3f)] private float _housingShock = 0.55f;

        [Header("이벤트")]
        [Tooltip("손잡이가 최저점에 닿은 순간. 오디오의 '철컥'이 여기 붙는다. " +
                 "**게임 판정을 여기에 걸지 마라** — 물리가 판정을 몰면 결정론이 깨진다.")]
        [SerializeField] private UnityEvent onBottomedOut = new UnityEvent();

        private DampedSpring1D _angle;
        private Quaternion _homeRotation;
        private PhysicsStepper _stepper;
        private IPhysicsClock _clock;
        private CabinInertiaSource _source;
        private float _holdUntil;
        private float _simTime;
        private bool _wasNearBottom;
        private bool _homeCaptured;

        /// <summary>현재 각변위(도). 쉬는 각도 기준의 상대값이다.</summary>
        public float AngleDegrees => _angle.Value;

        /// <summary>정지했는가. 헤드리스 안정성 단정이 읽는다.</summary>
        public bool IsAtRest => _angle.IsAtRest(_restAngle);

        /// <summary>
        /// 레버를 당긴다. `InteractableOverharvestLever.onPulled` 를 인스펙터에서
        /// 여기에 연결한다 — 코드 의존이 아니라 배선이라 소유 경로를 넘지 않는다.
        /// </summary>
        public void Pull()
        {
            EnsureHome();
            _angle.AddImpulse(_pullImpulse * Mathf.Sign(_pullSign == 0f ? -1f : _pullSign));
            _holdUntil = _simTime + _holdSeconds;

            if (_shakeOnPull == null || _housingShock <= 0f) return;
            Vector3 shock = transform.TransformDirection(Vector3.down) * _housingShock;
            for (int i = 0; i < _shakeOnPull.Length; i++)
                if (_shakeOnPull[i] != null) _shakeOnPull[i].AddShock(shock);
        }

        /// <summary>세기를 지정해 당긴다. 과수확처럼 「더 세게」가 의미를 갖는 경우에 쓴다.</summary>
        public void PullWithStrength(float strength01)
        {
            EnsureHome();
            float k = Mathf.Clamp01(strength01);
            _angle.AddImpulse(_pullImpulse * k * Mathf.Sign(_pullSign == 0f ? -1f : _pullSign));
            _holdUntil = _simTime + _holdSeconds * k;
        }

        /// <summary>상태를 지우고 쉬는 자세로 되돌린다.</summary>
        public void ResetToHome()
        {
            _angle.Reset(_restAngle);
            _stepper.Reset();
            _simTime = 0f;
            _holdUntil = 0f;
            _wasNearBottom = false;
            if (_pivot != null) _pivot.localRotation = _homeRotation * AxisRotation(_restAngle);
        }

        /// <summary>헤드리스 테스트 전용 직접 적분. 씬 없이 안정성·결정론을 확인한다.</summary>
        public void StepForTest(float dt, int steps)
        {
            EnsureHome();
            for (int i = 0; i < steps; i++) Integrate(dt);
        }

        /// <summary>시계를 갈아 끼운다. 캡처 하네스의 결정론 스위치.</summary>
        public void SetClock(IPhysicsClock clock) => _clock = clock;

        private void Awake()
        {
            if (_pivot == null) _pivot = transform;
            _stepper.Configure(PhysicsStepper.DefaultStep);
            EnsureHome();
            if (_source == null) _source = FindAnyObjectByType<CabinInertiaSource>();
            if (_clock == null) _clock = _source != null ? _source.Clock : new RealtimePhysicsClock();
        }

        private void EnsureHome()
        {
            if (_homeCaptured) return;
            if (_pivot == null) _pivot = transform;
            _homeRotation = _pivot.localRotation;
            _angle.Reset(_restAngle);
            _homeCaptured = true;
        }

        private void OnDisable() => ResetToHome();

        private void LateUpdate()
        {
            EnsureHome();

            float dt = _clock != null ? _clock.DeltaTime : Time.deltaTime;
            if (dt > 0.1f) dt = 0.1f;

            int steps = _stepper.Begin(dt);
            float s = _stepper.Step;
            for (int i = 0; i < steps; i++) Integrate(s);

            if (_pivot != null) _pivot.localRotation = _homeRotation * AxisRotation(_angle.Value);
        }

        private void Integrate(float dt)
        {
            _simTime += dt;

            if (_simTime < _holdUntil)
            {
                // 붙잡힌 구간: 복원력을 끄고 감쇠만 남긴다. 이것이 「손이 아직 레버에
                // 있다」이고, 이게 없으면 당기자마자 손을 뗀 것처럼 튀어 오른다.
                _angle.Velocity -= _angle.Velocity * Mathf.Min(1f, 9f * dt);
                _angle.Value += _angle.Velocity * dt;
                float lo = _restAngle - _maxSwingDegrees;
                float hi = _restAngle + _maxSwingDegrees;
                if (_angle.Value < lo) { _angle.Value = lo; _angle.Velocity = 0f; }
                else if (_angle.Value > hi) { _angle.Value = hi; _angle.Velocity = 0f; }
            }
            else
            {
                // 상한 기준점이 0 이 아니라 **쉬는 각도**다. 레버의 쉬는 자세는
                // 보통 0 이 아니고(하우징 안에서 살짝 세워져 있다), 그때 절대 0 을
                // 기준으로 자르면 쉬는 자세 자체가 상한에 걸린다.
                _angle.Step(dt, _restAngle, _omega, _zeta, _maxSwingDegrees, _restAngle);
            }

            // 최저점 도달을 한 번만 알린다. 매 프레임 부르면 오디오가 갈린다.
            bool nearBottom = Mathf.Abs(_angle.Value - _restAngle) > _maxSwingDegrees * 0.85f;
            if (nearBottom && !_wasNearBottom) onBottomedOut.Invoke();
            _wasNearBottom = nearBottom;
        }

        private Quaternion AxisRotation(float degrees)
        {
            Vector3 axis = _axis.sqrMagnitude < 1e-6f ? Vector3.right : _axis;
            return Quaternion.AngleAxis(degrees, axis.normalized);
        }
    }
}
