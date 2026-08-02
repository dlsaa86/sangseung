using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 문이 닫히는 **끝에서 튕기고**, 걸쇠가 걸리는 순간 문틀이 미세하게 떤다.
    ///
    /// 지금 문은 `ElevatorGrayboxView.UpdateDoor()` 의
    /// <c>Mathf.MoveTowards</c> 다 — 등속으로 가서 정확히 멈춘다. 무게가 없는 움직임이고,
    /// 그래서 문이 종이처럼 읽힌다. 무거운 금속 문이 닫힐 때 사람이 읽는 신호는
    /// 두 가지다: 끝에서의 **약한 오버슛**, 그리고 걸쇠가 물리는 **한순간의 떨림**.
    ///
    /// <b>카메라를 흔들지 않는다.</b> `VISUAL_SPEC` §8 이 과도한 카메라 흔들림을 금지하고,
    /// 카메라 셰이크 자체가 `PD-07` 로 사용자 승인 대기다. 여기서 흔드는 것은 문틀과
    /// 문짝뿐이다 — 오브젝트가 떨면 「저것이 무겁다」가 되고, 카메라가 떨면
    /// 「내가 맞았다」가 된다. 둘은 다른 문장이다.
    ///
    /// <b>문의 주인을 뺏지 않는다.</b> `ElevatorGrayboxView` 는 기본 실행 순서 0 의
    /// <c>LateUpdate</c> 에서 문짝 <c>localPosition</c> 을 절대값으로 쓴다. 이 컴포넌트는
    /// 순서 150 이라 그 뒤에 돌면서 **그 값을 읽고 오프셋을 더한다.** 다음 프레임에
    /// 뷰가 다시 깨끗한 값을 쓰므로 누적이 없다. 뷰를 고칠 필요도, 뷰가 이 컴포넌트를
    /// 알 필요도 없다.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class DoorImpact : MonoBehaviour
    {
        [Header("문짝 — 뷰가 모는 트랜스폼을 그대로 넣는다")]
        [Tooltip("왼쪽 문짝.")]
        [SerializeField] private Transform _leafLeft;

        [Tooltip("오른쪽 문짝.")]
        [SerializeField] private Transform _leafRight;

        [Tooltip("문짝이 미끄러지는 축(로컬). 좌우 슬라이드 문이면 X.")]
        [SerializeField] private Vector3 _slideAxis = Vector3.right;

        [Header("걸쇠 — 떠는 것은 여기다")]
        [Tooltip("문틀·걸쇠 하우징. 카메라가 아니다.")]
        [SerializeField] private Transform _frame;

        [Tooltip("걸쇠가 물릴 때 함께 반응할 캐빈 물체들(등·표찰 등).")]
        [SerializeField] private CabinInertiaReactor[] _shakeOnLatch;

        [Header("닫힘 감지")]
        [Tooltip("이 속도 아래로 떨어지면 멈춘 것으로 본다(m/s).")]
        [SerializeField, Range(0.001f, 0.5f)] private float _stopSpeed = 0.05f;

        [Tooltip("이 속도 이상으로 닫히던 중이어야 충격으로 친다(m/s). 살살 닫히면 조용해야 한다.")]
        [SerializeField, Range(0.01f, 2f)] private float _minImpactSpeed = 0.18f;

        [Tooltip("연속 감지를 막는 최소 간격(초).")]
        [SerializeField, Range(0.05f, 2f)] private float _rearmSeconds = 0.4f;

        [Header("오버슛 — 문짝이 끝에서 되튄다")]
        [Tooltip("최대 되튐 거리(m). 5cm 를 넘으면 문이 다시 열린 것처럼 보인다.")]
        [SerializeField, Range(0.002f, 0.06f)] private float _maxOvershoot = 0.018f;

        [Tooltip("되튐 고유 각진동수(rad/s).")]
        [SerializeField, Range(8f, 90f)] private float _overshootOmega = 42f;

        [Tooltip("되튐 감쇠비. 0.25 면 한 번 되튀고 멈춘다.")]
        [SerializeField, Range(0.05f, 1.2f)] private float _overshootZeta = 0.26f;

        [Header("문틀 떨림")]
        [Tooltip("최대 떨림 거리(m). 문틀은 아주 조금만 떤다 — 크면 벽이 무너지는 것으로 읽힌다.")]
        [SerializeField, Range(0.0005f, 0.02f)] private float _maxFrameShudder = 0.004f;

        [Tooltip("떨림 각진동수(rad/s). 높을수록 금속처럼 들린다(보인다).")]
        [SerializeField, Range(20f, 160f)] private float _frameOmega = 96f;

        [Tooltip("떨림 감쇠비.")]
        [SerializeField, Range(0.05f, 1.2f)] private float _frameZeta = 0.35f;

        [Tooltip("걸쇠 충격이 캐빈 물체로 전달되는 세기(m/s).")]
        [SerializeField, Range(0f, 2f)] private float _cabinShock = 0.35f;

        [Header("이벤트")]
        [Tooltip("걸쇠가 물린 순간. 오디오가 붙는다. **판정을 걸지 마라.**")]
        [SerializeField] private UnityEvent onLatched = new UnityEvent();

        private DampedSpring1D _overshoot;
        private DampedSpring1D _frameShudder;
        private PhysicsStepper _stepper;
        private IPhysicsClock _clock;
        private CabinInertiaSource _source;

        private float _lastLeftAxis;
        private float _lastRightAxis;
        private float _closingSpeed;
        private bool _primed;
        private float _simTime;
        private float _rearmAt;
        private Vector3 _frameHome;
        private bool _homeCaptured;

        /// <summary>마지막으로 감지한 충격 속도(m/s). 0 이면 아직 없다.</summary>
        public float LastImpactSpeed { get; private set; }

        /// <summary>지금 되튀는 중인가.</summary>
        public bool IsRinging => !_overshoot.IsAtRest() || !_frameShudder.IsAtRest();

        /// <summary>문짝 오버슛 변위(m). 상한 검증이 읽는다.</summary>
        public float OvershootMeters => _overshoot.Value;

        /// <summary>문틀 떨림 변위(m).</summary>
        public float FrameShudderMeters => _frameShudder.Value;

        /// <summary>배선을 코드로 꽂는다. 조립 스크립트와 헤드리스 테스트의 진입점이다.</summary>
        public void Configure(Transform left, Transform right, Transform frame)
        {
            _leafLeft = left;
            _leafRight = right;
            _frame = frame;
            _homeCaptured = false;
            _primed = false;
            EnsureHome();
        }

        /// <summary>
        /// 바깥에서 걸쇠 충격을 넣는다. 문 트랜스폼을 관측할 수 없는 배선에서 쓴다.
        /// </summary>
        public void Latch(float impactSpeed)
        {
            EnsureHome();
            if (_simTime < _rearmAt) return;
            _rearmAt = _simTime + _rearmSeconds;
            LastImpactSpeed = impactSpeed;

            float k = Mathf.Clamp01(impactSpeed / 1.2f);
            _overshoot.AddImpulse(_maxOvershoot * _overshootOmega * k);
            _frameShudder.AddImpulse(_maxFrameShudder * _frameOmega * k);

            if (_shakeOnLatch != null && _cabinShock > 0f)
            {
                Vector3 shock = transform.TransformDirection(Vector3.forward) * (_cabinShock * k);
                for (int i = 0; i < _shakeOnLatch.Length; i++)
                    if (_shakeOnLatch[i] != null) _shakeOnLatch[i].AddShock(shock);
            }

            onLatched.Invoke();
        }

        /// <summary>상태를 지우고 문틀을 홈으로 돌린다.</summary>
        public void ResetToHome()
        {
            _overshoot.Reset();
            _frameShudder.Reset();
            _stepper.Reset();
            _simTime = 0f;
            _rearmAt = 0f;
            _primed = false;
            _closingSpeed = 0f;
            if (_frame != null) _frame.localPosition = _frameHome;
        }

        /// <summary>헤드리스 테스트 전용 직접 적분.</summary>
        public void StepForTest(float dt, int steps)
        {
            EnsureHome();
            for (int i = 0; i < steps; i++) Integrate(dt);
        }

        /// <summary>시계를 갈아 끼운다.</summary>
        public void SetClock(IPhysicsClock clock) => _clock = clock;

        private void Awake()
        {
            _stepper.Configure(PhysicsStepper.DefaultStep);
            EnsureHome();
            if (_source == null) _source = FindAnyObjectByType<CabinInertiaSource>();
            if (_clock == null) _clock = _source != null ? _source.Clock : new RealtimePhysicsClock();
        }

        private void EnsureHome()
        {
            if (_homeCaptured) return;
            if (_frame != null) _frameHome = _frame.localPosition;
            _homeCaptured = true;
        }

        private void OnDisable() => ResetToHome();

        private void LateUpdate()
        {
            EnsureHome();

            float dt = _clock != null ? _clock.DeltaTime : Time.deltaTime;
            if (dt > 0.1f) dt = 0.1f;

            // 1) 뷰가 이번 프레임에 쓴 **깨끗한** 문짝 위치를 읽는다.
            //    (순서 150 이므로 뷰의 LateUpdate 는 이미 끝났다.)
            Vector3 axis = _slideAxis.sqrMagnitude < 1e-6f ? Vector3.right : _slideAxis.normalized;
            float left = _leafLeft != null ? Vector3.Dot(_leafLeft.localPosition, axis) : 0f;
            float right = _leafRight != null ? Vector3.Dot(_leafRight.localPosition, axis) : 0f;

            if (dt > 0f)
            {
                if (!_primed)
                {
                    _lastLeftAxis = left;
                    _lastRightAxis = right;
                    _primed = true;
                }
                else
                {
                    DetectImpact(left, right, dt);
                }
            }

            // 2) 적분.
            int steps = _stepper.Begin(dt);
            float s = _stepper.Step;
            for (int i = 0; i < steps; i++) Integrate(s);

            // 3) 오프셋을 **더한다.** 다음 프레임에 뷰가 덮어쓰므로 누적되지 않는다.
            if (_overshoot.Value != 0f)
            {
                if (_leafLeft != null) _leafLeft.localPosition -= axis * _overshoot.Value;
                if (_leafRight != null) _leafRight.localPosition += axis * _overshoot.Value;
            }

            if (_frame != null)
            {
                Vector3 p = _frameHome;
                // 떨림은 미끄럼 축에 **수직**으로 준다. 같은 축이면 문짝 오버슛과
                // 구분되지 않아 두 신호가 하나로 뭉갠다.
                p.y += _frameShudder.Value;
                _frame.localPosition = p;
            }
        }

        private void DetectImpact(float left, float right, float dt)
        {
            float invDt = 1f / dt;
            // 닫힘 = 두 문짝이 서로 가까워짐. 열림과 부호가 반대라 열 때는 안 울린다.
            float gapNow = right - left;
            float gapPrev = _lastRightAxis - _lastLeftAxis;
            float closing = (gapPrev - gapNow) * invDt;

            _lastLeftAxis = left;
            _lastRightAxis = right;

            float prevClosing = _closingSpeed;
            _closingSpeed = closing;

            // 「닫히고 있었는데 멈췄다」 — 그 프레임이 걸쇠다.
            if (prevClosing >= _minImpactSpeed && closing < _stopSpeed)
                Latch(prevClosing);
        }

        private void Integrate(float dt)
        {
            _simTime += dt;
            _overshoot.Step(dt, 0f, _overshootOmega, _overshootZeta, _maxOvershoot);
            _frameShudder.Step(dt, 0f, _frameOmega, _frameZeta, _maxFrameShudder);
        }
    }
}
