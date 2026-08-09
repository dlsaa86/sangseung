using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 바닥에 놓인 화물이 가감속에 **미끄러진다.** 굴리지 않는다.
    ///
    /// 왜 미끄러짐만인가: 진짜 텀블링은 접촉·마찰·관성 텐서가 필요하고 그건 강체
    /// 해석기의 일이다. 그런데 강체를 쓰면 결정론과 할당 0 이 동시에 깨진다
    /// (`CabinInertiaReactor` 주석의 세 가지 이유). 그리고 굴러다니는 화물은
    /// 판독성에도 나쁘다 — 캐빈 바닥은 플레이어가 이동하는 공간이고, 거기서
    /// 예측 불가능하게 움직이는 물체는 조작을 방해한다(`.claude/visual-criteria.md` B-5).
    ///
    /// 정지 마찰을 **데드존**으로 표현한다. 이것이 이 컴포넌트의 핵심이다 —
    /// 데드존이 없으면 화물이 아주 작은 가속에도 끊임없이 미끄러져 「살아 있는
    /// 상자」가 되고, 그건 물리가 아니라 버그로 읽힌다. 문턱을 넘을 때만 움직이고,
    /// 멈추면 **그 자리에 머무른다**(홈으로 돌아가지 않는다).
    /// </summary>
    public sealed class LooseCargo : CabinInertiaReactor
    {
        [Header("미끄러질 대상")]
        [Tooltip("비면 이 오브젝트 자신.")]
        [SerializeField] private Transform _body;

        [Header("마찰 — 쿨롱 모형")]
        // **여기서 한 번 틀렸다.** 처음 판본은 「가속 전달률 0.22」와 「운동 마찰 감속
        // 6.5 m/s²」를 따로 두었는데, 그러면 최대 구동력(0.22 × 18 = 3.96)이 마찰
        // (6.5)보다 작아 **상자가 어떤 입력에도 절대 움직이지 않았다.** 검사는
        // 「상한을 넘지 않는다」를 물었으므로 그대로 통과했을 것이다 — 죽은 컴포넌트가
        // 초록으로 보이는 전형적인 모양이다. 그래서 두 계수를 같은 단위(마찰계수)로
        // 묶었다. 이제 μs > μk 라는 물리적 관계가 값 자체에 보인다.
        [Tooltip("정지 마찰계수 μs. 이 값 × 유효중력(m/s²) 보다 작은 가속에는 꿈쩍도 하지 않는다. " +
                 "0.33 이면 약 3.2 m/s² 이 문턱이다.")]
        [SerializeField, Range(0.02f, 1.2f)] private float _staticMu = 0.33f;

        [Tooltip("운동 마찰계수 μk. **반드시 μs 보다 작다** — 크면 한 번 움직인 상자가 " +
                 "즉시 얼어붙어 미끄러짐이 관측되지 않는다.")]
        [SerializeField, Range(0.01f, 1.2f)] private float _kineticMu = 0.27f;

        [Header("범위 — 화물이 캐빈을 떠나면 안 된다")]
        [Tooltip("홈에서 벗어날 수 있는 최대 거리(m). 확대된 캐빈(바닥 4배)에서도 0.4 를 넘기지 않는다.")]
        [SerializeField, Range(0.02f, 0.8f)] private float _maxDrift = 0.22f;

        [Header("기울기 — 미끄러질 때 살짝 기운다")]
        [Tooltip("최대 기울기(도). 0 이면 끈다.")]
        [SerializeField, Range(0f, 14f)] private float _maxTiltDegrees = 5f;

        private Vector2 _offset;      // x, z 평면 변위
        private Vector2 _velocity;
        private DampedSpring1D _tiltX;
        private DampedSpring1D _tiltZ;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private bool _sliding;

        public override bool IsAtRest =>
            _velocity == Vector2.zero && _tiltX.IsAtRest() && _tiltZ.IsAtRest();

        /// <summary>홈에서 벗어난 거리(m). 상한 검증이 읽는다.</summary>
        public float DriftDistance => _offset.magnitude;

        /// <summary>지금 미끄러지는 중인가. 오디오가 붙을 지점이다.</summary>
        public bool IsSliding => _sliding;

        protected override void Awake()
        {
            if (_body == null) _body = transform;
            base.Awake();
        }

        protected override void CaptureHome()
        {
            if (_body == null) _body = transform;
            _homePosition = _body.localPosition;
            _homeRotation = _body.localRotation;
        }

        protected override void RestoreHome()
        {
            _offset = Vector2.zero;
            _velocity = Vector2.zero;
            _tiltX.Reset();
            _tiltZ.Reset();
            _sliding = false;
            if (_body == null) return;
            _body.localPosition = _homePosition;
            _body.localRotation = _homeRotation;
        }

        protected override void Integrate(float dt)
        {
            Vector3 a = LocalAcceleration;
            Vector2 planar = new Vector2(a.x, a.z);

            // 수직 가속이 아래로 크면 접촉력이 줄어 마찰도 약해진다 — 자유낙하 중에
            // 화물이 뜨는 그 느낌이 여기서 나온다. 위로 가속하면 반대로 눌러 붙는다.
            // 0 이하로 내려가지 않게 바닥을 깐다(음의 마찰은 물리가 아니다).
            float gEff = Mathf.Max(0.5f, PendulumState.Gravity + a.y);
            float staticLimit = _staticMu * gEff;
            float kineticDecel = Mathf.Min(_kineticMu, _staticMu) * gEff;

            float mag = planar.magnitude;
            bool moving = _velocity.sqrMagnitude > 1e-8f;
            _sliding = moving || mag > staticLimit;

            if (_sliding)
            {
                // 관성: 카가 +x 로 가속하면 화물은 카 기준 -x 로 밀린다.
                _velocity -= planar * dt;

                // 쿨롱 운동 마찰: 속도 반대 방향으로 일정 감속. 0 을 지나쳐 뒤로
                // 가지 않게 자른다 — 자르지 않으면 마찰이 물체를 되밀어 떤다.
                float speed = _velocity.magnitude;
                if (speed > 0f)
                {
                    float next = speed - kineticDecel * dt;
                    if (next <= 0f) { _velocity = Vector2.zero; _sliding = mag > staticLimit; }
                    else _velocity *= next / speed;
                }

                _offset += _velocity * dt;
            }

            // 범위 제한. 벽에 닿으면 그 방향 속도를 죽인다.
            float d = _offset.magnitude;
            if (d > _maxDrift)
            {
                _offset *= _maxDrift / d;
                _velocity = Vector2.zero;
            }

            if (_maxTiltDegrees > 0f)
            {
                float maxRad = _maxTiltDegrees * Mathf.Deg2Rad;
                _tiltX.Step(dt, Mathf.Clamp(a.z * 0.012f, -maxRad, maxRad), 14f, 0.7f, maxRad);
                _tiltZ.Step(dt, Mathf.Clamp(-a.x * 0.012f, -maxRad, maxRad), 14f, 0.7f, maxRad);
            }

            if (_velocity.sqrMagnitude < 1e-10f) _velocity = Vector2.zero;
        }

        protected override void Apply()
        {
            if (_body == null) return;

            Vector3 p = _homePosition;
            p.x += _offset.x;
            p.z += _offset.y;
            _body.localPosition = p;

            if (_maxTiltDegrees > 0f)
                _body.localRotation = _homeRotation * Quaternion.Euler(
                    _tiltX.Value * Mathf.Rad2Deg, 0f, _tiltZ.Value * Mathf.Rad2Deg);
        }

        public override void AddShock(Vector3 worldImpulse)
        {
            Vector3 local = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldImpulse)
                : worldImpulse;
            _velocity += new Vector2(-local.x, -local.z);
            _sliding = true;
        }
    }
}
