using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 카의 가속도를 관측해 캐빈 안의 반응자들에게 넘겨 주는 **단일 발신원**.
    ///
    /// 왜 관측인가: 카 이동은 `ElevatorGrayboxView.UpdateCarMotion()` 이
    /// <c>_carRoot.localPosition</c> 에 Lerp 로 쓴다. 그 파일은 다른 소유 영역이고,
    /// 무엇보다 **이동의 주인이 하나여야** 카가 두 군데서 밀리지 않는다.
    /// 그래서 여기서는 트랜스폼을 읽기만 하고 가속도를 유도한다.
    /// 나중에 실제 층간 주행이 들어와도 이 컴포넌트는 그대로 동작한다.
    ///
    /// 왜 <c>Rigidbody</c> 가 아닌가: 카는 게임 상태가 결정한 위치로 **끌려간다.**
    /// 힘으로 밀어 도달시키는 물체가 아니다. 여기에 Rigidbody 를 붙이면 층 도착
    /// 위치가 물리 해석기의 수렴 결과가 되고, 그 순간 판정이 물리에 의존한다 —
    /// `TECH_SPEC.md` §7 결정론 요구를 정면으로 깬다.
    ///
    /// 실행 순서 100: 뷰들(`ElevatorGrayboxView`·`RiskStateView`·`CollapseSequence`)이
    /// 전부 기본 순서 0 의 <c>LateUpdate</c> 에서 트랜스폼을 쓴다. 그보다 뒤에서
    /// 읽어야 이번 프레임의 위치를 본다. 앞에서 읽으면 항상 한 프레임 늦은 가속도가
    /// 나오고, 그건 "출발했는데 램프가 나중에 흔들린다"로 보인다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CabinInertiaSource : MonoBehaviour
    {
        [Header("관측 대상")]
        // 자동 대체 경로(자기 자신 관측)가 있으므로 `[RequiredReference]` 를 붙이지 않는다
        // — `ASSUMPTION_LOG` A-20260801-08 의 기준이 정확히 그것이다.
        [Tooltip("카 루트 트랜스폼. 비면 이 오브젝트 자신을 관측한다.")]
        [SerializeField] private Transform _observed;

        [Header("필터 — 판독성이 여기서 결정된다")]
        [Tooltip("저역 통과 차단 주파수(Hz). 낮을수록 잔떨림을 버리고 출발·정지만 남긴다. " +
                 "그레이박스 카는 5Hz 대 럼블을 갖고 있어 필터 없이는 매달린 것이 전부 발작한다.")]
        [SerializeField, Range(0.2f, 12f)] private float _cutoffHz = 2.2f;

        [Tooltip("가속도 크기 상한(m/s²). 한 프레임 튐이 캐빈 전체를 날려 버리는 것을 막는다.")]
        [SerializeField, Range(1f, 60f)] private float _maxMagnitude = 18f;

        [Tooltip("이 값보다 작은 가속도는 0 으로 본다. 부동소수 잔여가 영원히 미세 진동시키는 것을 막는다.")]
        [SerializeField, Range(0f, 0.5f)] private float _deadzone = 0.02f;

        [Header("결정론")]
        [Tooltip("켜면 프레임 시간 대신 고정 스텝을 쓴다. 캡처 하네스가 같은 그림을 내야 할 때 켠다.")]
        [SerializeField] private bool _useFixedStepClock;

        [Tooltip("고정 스텝 폭(초). 캡처 프레임 간격과 맞춘다.")]
        [SerializeField, Range(1f / 240f, 1f / 20f)] private float _fixedStep = 1f / 60f;

        private IPhysicsClock _clock;
        private RealtimePhysicsClock _realtime;
        private FixedStepPhysicsClock _fixed;

        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private Vector3 _filteredAcceleration;
        private Vector3 _externalAcceleration;
        private bool _primed;

        /// <summary>
        /// 이번 프레임의 필터링된 월드 가속도(m/s²). 반응자들이 매 프레임 한 번 읽는다.
        /// **읽기 전용이다** — 여기에 쓴 값이 게임 상태로 되돌아가는 경로는 없다.
        /// </summary>
        public Vector3 Acceleration => _filteredAcceleration;

        /// <summary>필터를 거치지 않은 순간 속도(m/s). 진단·프로브용.</summary>
        public Vector3 Velocity => _velocity;

        /// <summary>이 발신원이 쓰는 시계. 반응자가 같은 시계를 공유해 결정론을 맞춘다.</summary>
        public IPhysicsClock Clock => _clock;

        /// <summary>고정 스텝 모드인가. 캡처 하네스가 확인한다.</summary>
        public bool IsDeterministic => _useFixedStepClock;

        /// <summary>
        /// 바깥에서 가속도를 얹는다(사고 충격·도킹 충격 등). 관측값과 **더해진다.**
        /// 한 프레임만 유효하고 다음 프레임에 지워진다 — 얹은 쪽이 계속 얹어야 한다.
        /// </summary>
        public void PushAcceleration(Vector3 worldAcceleration)
        {
            _externalAcceleration += worldAcceleration;
        }

        /// <summary>
        /// 시계를 갈아 끼운다. 헤드리스 테스트와 캡처 하네스의 결정론 스위치다.
        /// null 이면 인스펙터 설정대로 되돌린다.
        /// </summary>
        public void SetClock(IPhysicsClock clock)
        {
            _clock = clock ?? (_useFixedStepClock ? (IPhysicsClock)_fixed : _realtime);
        }

        /// <summary>상태를 완전히 지운다. 같은 시드를 두 번 재생할 때 부른다.</summary>
        public void ResetState()
        {
            _velocity = Vector3.zero;
            _filteredAcceleration = Vector3.zero;
            _externalAcceleration = Vector3.zero;
            _primed = false;
            _fixed?.Reset();
        }

        private void Awake()
        {
            if (_observed == null) _observed = transform;
            _realtime = new RealtimePhysicsClock();
            _fixed = new FixedStepPhysicsClock(_fixedStep);
            _clock = _useFixedStepClock ? (IPhysicsClock)_fixed : _realtime;
            _lastPosition = _observed.position;
        }

        private void OnEnable()
        {
            if (_observed != null) _lastPosition = _observed.position;
            _primed = false;
        }

        private void LateUpdate()
        {
            if (_observed == null) return;

            float dt = _clock.DeltaTime;
            if (_useFixedStepClock) _fixed.Tick();
            else _realtime.Advance(dt);

            if (dt <= 0f) { _externalAcceleration = Vector3.zero; return; }

            Vector3 p = _observed.position;

            if (!_primed)
            {
                // 첫 프레임에는 가속도를 만들지 않는다. 씬 로드 직후의 위치 점프를
                // 가속도로 읽으면 모든 것이 한 번 크게 튀고, 그 프레임이 캡처에 잡힌다.
                _lastPosition = p;
                _primed = true;
                _externalAcceleration = Vector3.zero;
                return;
            }

            float invDt = 1f / dt;
            Vector3 v = (p - _lastPosition) * invDt;
            Vector3 rawAccel = (v - _velocity) * invDt + _externalAcceleration;

            _lastPosition = p;
            _velocity = v;
            _externalAcceleration = Vector3.zero;

            // 1차 저역 통과. alpha = dt / (dt + 1/(2πfc)).
            float tau = 1f / (2f * Mathf.PI * Mathf.Max(_cutoffHz, 0.01f));
            float alpha = dt / (dt + tau);
            _filteredAcceleration += (rawAccel - _filteredAcceleration) * alpha;

            float sqr = _filteredAcceleration.sqrMagnitude;
            if (sqr > _maxMagnitude * _maxMagnitude)
                _filteredAcceleration *= _maxMagnitude / Mathf.Sqrt(sqr);
            else if (sqr < _deadzone * _deadzone)
                _filteredAcceleration = Vector3.zero;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_fixed != null && !Application.isPlaying) _fixed = new FixedStepPhysicsClock(_fixedStep);
        }
#endif
    }
}
