using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 실행 레버의 **유일한 소유자**. 피벗 트랜스폼에 각도를 쓰는 것은 이 컴포넌트뿐이다.
    ///
    /// ## 왜 상태 기계인가
    ///
    /// 레버 하나에 필요한 동작이 여덟이다 — 잠김·대기·준비·당김·걸림·처리·완료·복귀.
    /// 이것을 코루틴 여럿으로 만들면 **두 코루틴이 같은 트랜스폼에 동시에 각도를 쓴다.**
    /// 그 결과는 「가끔 떨린다」이고, 떨림은 재현이 어려워 원인 추적이 사실상 불가능하다.
    /// 이 저장소는 같은 이유로 조명에서 한 번 당했다(`RiskLightingView` 주석).
    ///
    /// 그래서 상태는 하나, 시간축도 하나, 트랜스폼에 쓰는 지점도 하나다.
    ///
    /// ## 왜 `LeverPhysics` 를 대신하는가
    ///
    /// `LeverPhysics` 는 감쇠 스프링으로 각도를 적분한다 — 반동 자체는 옳지만
    /// **당김을 시작시키는 코드가 어디에도 없었다.** 실측으로 `Pull()` 의 런타임
    /// 호출자가 0 개였다. 즉 레버는 지금까지 한 번도 움직인 적이 없다.
    /// 게다가 잠긴 상태에서 `InteractableLever.Interact()` 는 조용히 반환해서
    /// **아무 일도 일어나지 않았다** — 플레이어에게는 「버튼이 고장났다」로 읽힌다.
    ///
    /// ## 프레임률 독립
    ///
    /// 모든 구간이 「경과 시간 / 구간 길이」로 진행한다. 프레임이 튀어도 각도가
    /// 튀지 않고, 60fps 와 30fps 의 총 소요 시간이 같다. 테스트는 <see cref="Step"/>
    /// 를 고정 dt 로 돌려 이것을 검증한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeverStateMachine : MonoBehaviour
    {
        public enum State
        {
            /// <summary>잠겨 있다. 입력을 받으면 핀에 부딪히고 되돌아온다.</summary>
            Locked,
            /// <summary>평상시. 입력을 받을 수 있다.</summary>
            Idle,
            /// <summary>당길 수 있다고 강조된 상태. 각도는 Idle 과 같다.</summary>
            Ready,
            /// <summary>당기는 중.</summary>
            Pulling,
            /// <summary>바닥에 걸렸다. 여기서 장치가 반응한다.</summary>
            Latched,
            /// <summary>장치가 처리 중. 레버는 걸린 자세를 유지한다.</summary>
            Processing,
            /// <summary>처리 완료. 잠깐 머문다.</summary>
            Completed,
            /// <summary>원위치로 돌아가는 중.</summary>
            Resetting,

            /// <summary>
            /// **유지 입력 중.** 각도가 시간이 아니라 `InteractableLever.HoldProgress` 를 따른다.
            ///
            /// `IHoldInteractable` 문서가 요구하는 것이다 — 「진행도는 상호작용물이 연출로
            /// 갚아야 한다. 레버가 실제로 내려가는 것이 진행 표시이고, 그것이 §7 의
            /// 『강한 금속 걸쇠 피드백』의 시각 절반이다.」
            ///
            /// ⚠ 열거자 **끝에** 넣었다. 중간에 끼우면 직렬화된 값이 밀려 씬의 상태가
            /// 다른 상태로 바뀐다.
            /// </summary>
            Holding,
        }

        [Header("피벗")]
        [SerializeField] private Transform _pivot;
        /// <summary>회전축. 기본은 X — 레버는 수직 슬롯을 따라 앞뒤로 움직인다.</summary>
        [SerializeField] private Vector3 _axis = Vector3.right;
        [SerializeField, Range(10f, 90f)] private float _swingDegrees = 55f;

        [Header("당김 (합계 0.5~0.7초)")]
        /// <summary>초기 저항. 이 구간에서 각도가 거의 변하지 않아 「무겁다」가 읽힌다.</summary>
        [SerializeField, Range(0.02f, 0.20f)] private float _resistance = 0.08f;
        [SerializeField, Range(0.20f, 0.80f)] private float _travel = 0.42f;
        /// <summary>바닥을 지나쳤다가 되돌아오는 각도. 도달이 아니라 **반동**이 타격감이다.</summary>
        [SerializeField, Range(0f, 6f)] private float _overshoot = 2.0f;
        [SerializeField, Range(0.04f, 0.30f)] private float _settle = 0.12f;

        [Header("걸림 이후")]
        /// <summary>걸린 뒤 장치가 반응하기까지. 0 이면 원인과 결과가 붙어 인과가 안 읽힌다.</summary>
        [SerializeField, Range(0.04f, 0.30f)] private float _deviceReactDelay = 0.11f;
        [SerializeField, Range(0.2f, 3f)] private float _processing = 1.00f;
        [SerializeField, Range(0.05f, 1f)] private float _completedHold = 0.25f;
        [SerializeField, Range(0.30f, 1.20f)] private float _resetDuration = 0.55f;

        [Header("잠김 (합계 0.3~0.5초)")]
        /// <summary>핀에 닿기까지 허용되는 각도. 명세 요구 2~4도.</summary>
        [SerializeField, Range(1f, 6f)] private float _lockedTravelDegrees = 3.2f;
        [SerializeField, Range(0.04f, 0.20f)] private float _lockedApproach = 0.09f;
        [SerializeField, Range(0.10f, 0.30f)] private float _lockedBounce = 0.16f;
        [SerializeField, Range(0.05f, 0.30f)] private float _lockedReturn = 0.13f;

        [Header("이벤트")]
        /// <summary>걸린 뒤 <see cref="_deviceReactDelay"/> 만큼 지나 발동한다.
        /// 장치 타격(`MachineImpactView`)과 릴(`SoulReelView`)을 여기에 건다.</summary>
        [SerializeField] private UnityEvent onLatched = new UnityEvent();
        /// <summary>잠긴 채 당겨 핀에 부딪힌 순간. 경고등 1회 점멸을 여기에 건다.</summary>
        [SerializeField] private UnityEvent onLockBlocked = new UnityEvent();
        /// <summary>완전히 원위치로 돌아온 순간.</summary>
        [SerializeField] private UnityEvent onReturned = new UnityEvent();

        private Quaternion _home;
        private State _state = State.Idle;
        private float _age;
        private float _angle;
        private bool _latchFired;
        private bool _blockFired;
        private bool _homeCaptured;

        public State Current => _state;
        public float AngleDegrees => _angle;
        /// <summary>지금 입력을 받을 수 있는가. 스팸 방지의 근거이기도 하다.</summary>
        public bool AcceptsInput => _state == State.Idle || _state == State.Ready || _state == State.Locked;

        [Header("유지 입력 연출")]
        /// <summary>
        /// 진행도를 읽어 올 레버. 비우면 부모에서 찾는다.
        ///
        /// **View 가 Model 을 읽는 방향이다.** 반대로 `InteractableLever` 가 이 클래스를
        /// 참조하면 입력 계층이 연출을 알게 되고, 그러면 연출을 바꿀 때마다 입력을 고쳐야 한다.
        /// </summary>
        [SerializeField] private Player.InteractableLever _holdSource;

        private void Awake()
        {
            CaptureHome();
            if (_holdSource == null) _holdSource = FindHoldSource();
        }

        private void OnEnable()
        {
            if (_holdSource == null) _holdSource = FindHoldSource();
            if (_holdSource == null) return;
            _holdSource.onHoldBegan.AddListener(BeginHold);
            _holdSource.onHoldCancelled.AddListener(AbortHold);
            _holdSource.onHeld.AddListener(LatchFromHold);
        }

        private void OnDisable()
        {
            if (_holdSource == null) return;
            _holdSource.onHoldBegan.RemoveListener(BeginHold);
            _holdSource.onHoldCancelled.RemoveListener(AbortHold);
            _holdSource.onHeld.RemoveListener(LatchFromHold);
        }

        /// <summary>
        /// 진행도의 출처를 찾는다.
        ///
        /// ⚠ **부모에서 찾으면 안 된다.** 실측(2026-08-08)하면 이 저장소의 씬에서
        /// `LeverStateMachine` 은 `Console/ExecutionLeverBase/ExecutionLeverHandle` 에 있고
        /// `InteractableLever` 는 `Console/ExecutionLever` 에 있다 — **형제 서브트리**다.
        /// 둘은 계층이 아니라 UnityEvent 로 이어져 있다. `GetComponentInParent` 는 null 을
        /// 돌려주고, 그러면 유지 연출이 조용히 안 나온다.
        ///
        /// 그래서 씬에서 명시 배선하는 것이 정본이고, 이 탐색은 마지막 안전망일 뿐이다.
        /// 레버가 여러 개가 되면 이 폴백은 더 이상 옳지 않으므로 그때 경고가 뜨게 해 둔다.
        /// </summary>
        private Player.InteractableLever FindHoldSource()
        {
            var own = GetComponentInParent<Player.InteractableLever>();
            if (own != null) return own;

            var found = FindObjectsByType<Player.InteractableLever>(FindObjectsSortMode.None);
            if (found == null || found.Length == 0) return null;
            if (found.Length > 1)
                Debug.LogWarning($"[상승] {name}: InteractableLever 가 {found.Length} 개다 — " +
                                 "_holdSource 를 씬에서 명시 배선해야 한다. 지금은 첫 번째를 쓴다.");
            return found[0];
        }

        /// <summary>유지가 시작됐다. 각도를 진행도에 맡긴다.</summary>
        public void BeginHold()
        {
            CaptureHome();
            if (_state == State.Locked) { Blocked(); return; }
            if (_state != State.Idle && _state != State.Ready) return;
            Enter(State.Holding);
        }

        /// <summary>완성 전에 놓았다. 되돌아온다 — 걸리지 않으므로 `onLatched` 는 안 나간다.</summary>
        public void AbortHold()
        {
            if (_state != State.Holding) return;
            Enter(State.Resetting);
        }

        /// <summary>
        /// 유지를 완성했다. **`Pulling` 을 거치지 않고 바로 걸린다** —
        /// 레버는 이미 내려가 있으므로 당김 애니메이션을 다시 재생하면 튄다.
        /// </summary>
        public void LatchFromHold()
        {
            CaptureHome();
            if (_state != State.Holding) return;
            Enter(State.Latched);
        }

        private void CaptureHome()
        {
            if (_homeCaptured) return;
            if (_pivot == null) _pivot = transform;
            _home = _pivot.localRotation;
            _homeCaptured = true;
        }

        // ── 입력 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 당긴다. **받을 수 없는 상태면 조용히 무시한다** — 연타해도 진행 중인
        /// 동작을 처음으로 되돌리지 않는다. 되돌리면 연타가 애니메이션을
        /// 영원히 리셋해 아무것도 완료되지 않는다.
        /// </summary>
        public void Pull()
        {
            CaptureHome();
            if (_state == State.Locked) { Blocked(); return; }
            if (_state != State.Idle && _state != State.Ready) return;
            Enter(State.Pulling);
        }

        /// <summary>잠긴 채로 당겼다. 핀에 부딪히는 짧은 반동을 낸다.</summary>
        public void Blocked()
        {
            CaptureHome();
            if (_state != State.Locked) return;
            // 이미 튕기는 중이면 처음부터 다시 시작하지 않는다 — 연타 시 각도가
            // 계속 0 으로 리셋되어 **아예 움직이지 않는 것처럼** 보인다.
            if (_age < _lockedApproach + _lockedBounce + _lockedReturn) return;
            _age = 0f;
            _blockFired = false;
        }

        /// <summary>잠금 상태를 바꾼다. 진행 중인 동작은 끊지 않는다.</summary>
        public void SetLocked(bool locked)
        {
            CaptureHome();
            if (locked)
            {
                if (_state == State.Idle || _state == State.Ready) Enter(State.Locked);
            }
            else if (_state == State.Locked)
            {
                Enter(State.Idle);
            }
        }

        public void SetReady(bool ready)
        {
            if (ready && _state == State.Idle) Enter(State.Ready);
            else if (!ready && _state == State.Ready) Enter(State.Idle);
        }

        /// <summary>지금 상태와 무관하게 원위치로 되돌린다(층 전환 등).</summary>
        public void ForceReset()
        {
            CaptureHome();
            if (_state == State.Idle || _state == State.Locked) return;
            Enter(State.Resetting);
        }

        // ── 진행 ────────────────────────────────────────────────────────────

        private void Update() => Step(Time.deltaTime);

        /// <summary>
        /// 한 스텝. **테스트가 고정 dt 로 직접 부른다** — 애니메이션 타이밍은
        /// 「보기에 괜찮다」가 아니라 초 단위로 검증할 수 있어야 한다.
        /// </summary>
        public void Step(float dt)
        {
            CaptureHome();
            if (dt <= 0f) return;
            _age += dt;

            switch (_state)
            {
                case State.Locked:   StepLocked();  break;
                case State.Pulling:  StepPulling(); break;
                case State.Latched:
                    _angle = _swingDegrees;
                    if (!_latchFired && _age >= _deviceReactDelay)
                    {
                        _latchFired = true;
                        onLatched.Invoke();
                        Enter(State.Processing);
                    }
                    break;
                case State.Processing:
                    _angle = _swingDegrees;
                    if (_age >= _processing) Enter(State.Completed);
                    break;
                case State.Completed:
                    _angle = _swingDegrees;
                    if (_age >= _completedHold) Enter(State.Resetting);
                    break;
                case State.Resetting:
                    StepResetting();
                    break;
                case State.Holding:
                    StepHolding();
                    break;
                default:
                    _angle = 0f;
                    break;
            }

            Apply();
        }

        /// <summary>
        /// 유지 중. **각도가 시간이 아니라 진행도를 따른다.** 손을 멈추면 레버도 멈춘다 —
        /// 그것이 「내가 누르고 있는 만큼 내려간다」를 손에 남기는 유일한 방법이다.
        ///
        /// 저항 구간을 진행도에도 준다. 초반 <see cref="_resistance"/> 비율에서 각도가
        /// 거의 변하지 않아 「무겁다」가 읽히고, 그 뒤로 열린다. 시간 기반 `StepPulling`
        /// 과 같은 감각을 쓰되 축만 시간 → 진행도로 바꾼 것이다.
        ///
        /// ⚠ 여기서 걸림(`Latched`)으로 스스로 넘어가지 않는다. 완성 판정은 입력 계층이
        /// 하고(`CrosshairInteractor`), 그 결과가 `LatchFromHold()` 로 들어온다.
        /// 두 곳에서 판정하면 진행도 1 과 완성이 갈라진다.
        /// </summary>
        private void StepHolding()
        {
            if (_holdSource == null) { _angle = 0f; return; }

            float p = Mathf.Clamp01(_holdSource.HoldProgress);
            float r = Mathf.Clamp(_resistance / Mathf.Max(0.0001f, _resistance + _travel), 0.05f, 0.45f);

            float shaped;
            if (p <= r)
            {
                // 저항 구간 — 눌러도 거의 안 움직인다. 전체 각도의 8% 까지만.
                shaped = 0.08f * (p / r);
            }
            else
            {
                float t = (p - r) / Mathf.Max(0.0001f, 1f - r);
                shaped = 0.08f + 0.92f * EaseOut(t);
            }

            _angle = _swingDegrees * shaped;
        }

        private void StepLocked()
        {
            float a = _lockedApproach, b = _lockedBounce, c = _lockedReturn;
            float lim = _lockedTravelDegrees;

            if (_age < a)
            {
                // 핀을 향해 간다. 감속 없이 곧게 — 막힐 것이기 때문이다.
                _angle = lim * (_age / a);
            }
            else if (_age < a + b)
            {
                // 핀에 닿았다. 여기서만 이벤트가 나간다 — 접근 중에 울리면
                // 「부딪히기 전에 소리가 난다」가 된다.
                if (!_blockFired) { _blockFired = true; onLockBlocked.Invoke(); }
                // 감쇠 진동 두 번. 쇳덩이가 쇳덩이에 부딪힌 느낌은 한 번이 아니다.
                float t = (_age - a) / b;
                _angle = lim * (1f - t * 0.55f) * Mathf.Cos(t * Mathf.PI * 3.4f) * Mathf.Exp(-3.2f * t);
                _angle = Mathf.Clamp(_angle, -lim * 0.35f, lim);
            }
            else if (_age < a + b + c)
            {
                float t = (_age - a - b) / c;
                _angle = Mathf.LerpUnclamped(_angle, 0f, EaseOut(t));
            }
            else
            {
                _angle = 0f;
            }
        }

        private void StepPulling()
        {
            float r = _resistance, tr = _travel, s = _settle;

            if (_age < r)
            {
                // 초기 저항 — 전체의 6% 만 움직인다. 「무겁다」는 속도가 아니라
                // **처음에 잘 안 움직인다**로 읽힌다.
                _angle = _swingDegrees * 0.06f * (_age / r);
            }
            else if (_age < r + tr)
            {
                float t = (_age - r) / tr;
                // 가속 후 감속. 등속이면 기계가 아니라 슬라이더로 보인다.
                float e = t < 0.35f
                    ? 0.5f * Mathf.Pow(t / 0.35f, 2f) * 0.7f
                    : 1f - Mathf.Pow(1f - t, 2.2f);
                _angle = Mathf.Lerp(_swingDegrees * 0.06f, _swingDegrees + _overshoot, e);
            }
            else if (_age < r + tr + s)
            {
                // 오버슈트에서 되돌아와 걸린다.
                float t = (_age - r - tr) / s;
                _angle = Mathf.Lerp(_swingDegrees + _overshoot, _swingDegrees, EaseOut(t));
            }
            else
            {
                _angle = _swingDegrees;
                Enter(State.Latched);
            }
        }

        private void StepResetting()
        {
            float t = Mathf.Clamp01(_age / _resetDuration);
            // 돌아올 때는 오버슈트가 없다. 스프링이 밀어 올리는 것이지
            // 사람이 던지는 것이 아니다.
            _angle = Mathf.Lerp(_swingDegrees, 0f, EaseOut(t));
            if (t >= 1f)
            {
                _angle = 0f;
                onReturned.Invoke();
                Enter(State.Idle);
            }
        }

        /// <summary>
        /// **헤드리스 검사 전용.** 상태를 직접 세운다.
        ///
        /// 왜 필요한가: `CustomsLockView` 의 자동 해제는 레버 상태의 **전이**를 본다.
        /// 그 전이를 재현하려면 `Latched → Resetting → Idle` 을 만들 수 있어야 하는데,
        /// 정상 경로로는 초 단위 시간과 입력이 필요해 순수 검사에서 재현할 수 없다.
        /// 그 재현 수단이 없어서 소유권 결함이 여덟 개 검사를 통과했다.
        ///
        /// 게임 코드에서 부르지 않는다 — 부르면 이벤트가 건너뛰어진다.
        /// </summary>
        public void ForceState(State next) => Enter(next);

        private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        private void Enter(State next)
        {
            _state = next;
            _age = 0f;
            if (next == State.Pulling) _latchFired = false;
            if (next == State.Locked) { _blockFired = false; _age = 999f; }
        }

        private void Apply()
        {
            if (_pivot == null) return;
            _pivot.localRotation = _home * Quaternion.AngleAxis(_angle, _axis.sqrMagnitude < 1e-6f ? Vector3.right : _axis.normalized);
        }

        /// <summary>테스트·조립기용. 인스펙터 배선 없이 축과 피벗을 지정한다.</summary>
        public void Configure(Transform pivot, Vector3 axis, float swingDegrees)
        {
            _pivot = pivot;
            _axis = axis;
            _swingDegrees = swingDegrees;
            _homeCaptured = false;
            CaptureHome();
        }
    }
}
