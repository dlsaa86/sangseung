using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 매달린 표찰·종이 테이프·전표. 가볍고 짧아서 **빠르게 떨고 먼저 멈춘다.**
    ///
    /// 램프와 같은 진자를 쓰지만 파라미터 대역이 다르다. 짧은 줄(≤0.15m)은 주기가
    /// 0.8초 아래로 내려가고, 그 빠른 박자가 긴 케이블의 느린 박자와 겹칠 때
    /// 공간이 여러 무게를 가진 것으로 읽힌다. 전부 같은 속도로 흔들리면
    /// 화면 전체가 하나의 애니메이션 클립처럼 보인다 — 물리로 안 보이는 물리다.
    ///
    /// 종이는 여기에 **펄럭임**이 더 붙는다. 펄럭임은 난수처럼 보여야 하지만
    /// 난수여서는 안 된다(캡처 재현). 정수 해시 기반 결정론적 잡음을 쓴다 —
    /// 같은 시각·같은 시드면 같은 각도가 나온다.
    /// </summary>
    public sealed class DanglingTag : CabinInertiaReactor
    {
        [Header("표찰")]
        [Tooltip("비면 이 오브젝트 자신.")]
        [SerializeField] private Transform _pivot;

        [Header("진자 — 짧고 빠르게")]
        [Tooltip("줄 길이(m). 0.06~0.2 가 표찰 대역이다.")]
        [SerializeField, Range(0.03f, 0.6f)] private float _length = 0.11f;

        [Tooltip("감쇠(1/s). 가벼운 것은 공기 저항이 커서 빨리 멈춘다.")]
        [SerializeField, Range(0.2f, 12f)] private float _damping = 2.6f;

        [Tooltip("최대 각도(도). 표찰은 크게 흔들려도 화면을 가리지 않아 여유가 있다.")]
        [SerializeField, Range(2f, 45f)] private float _maxAngleDegrees = 26f;

        [Header("펄럭임 — 종이일 때만 켠다")]
        [Tooltip("펄럭임 진폭(도). 0 이면 딱딱한 표찰(금속 명찰)이 된다.")]
        [SerializeField, Range(0f, 12f)] private float _flutterDegrees = 3.5f;

        [Tooltip("펄럭임 기본 주파수(Hz).")]
        [SerializeField, Range(0.5f, 12f)] private float _flutterHz = 3.2f;

        [Tooltip("펄럭임 시드. 표찰마다 다른 값을 주면 서로 다른 박자로 떤다.")]
        [SerializeField] private int _flutterSeed = 20260802;

        [Tooltip("완전히 멈춘 상태에서도 남는 펄럭임 비율. **0 이 기본이다** — " +
                 "0 이 아니면 이 표찰은 영원히 움직이고, 그러면 「사고가 끝나고 공간이 " +
                 "정지했다」가 성립하지 않아 복귀 검증이 통과하지 않는다.")]
        [SerializeField, Range(0f, 0.5f)] private float _idleFlutter;

        private PendulumState _pendulum;
        private float _flutterPhase;
        private float _flutterValue;
        private float _seedPhase;
        private Quaternion _homeRotation;

        public override bool IsAtRest => _pendulum.IsAtRest() && _flutterValue == 0f;

        /// <summary>진자 각도(도). 상한 검증이 읽는다.</summary>
        public Vector2 AngleDegrees =>
            new Vector2(_pendulum.AngleX * Mathf.Rad2Deg, _pendulum.AngleZ * Mathf.Rad2Deg);

        /// <summary>펄럭임 각도(도).</summary>
        public float FlutterDegrees => _flutterValue;

        protected override void Awake()
        {
            if (_pivot == null) _pivot = transform;
            // 표찰마다 다른 위상. 같은 프리팹 복제본들이 한 몸처럼 떠는 것을 막는다.
            // 키는 계층 경로에서 뽑으므로 **실행마다 같다** — 인스턴스 ID 는
            // 세션마다 달라져 고정 캡처의 비트 재현을 깨뜨린다(`StableVariationKey` 주석).
            _seedPhase = Hash01(_flutterSeed, StableVariationKey()) * 6.2831853f;
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
            _flutterPhase = 0f;
            _flutterValue = 0f;
            if (_pivot != null) _pivot.localRotation = _homeRotation;
        }

        protected override void Integrate(float dt)
        {
            Vector3 a = LocalAcceleration;
            _pendulum.Step(dt, a.z, -a.x, a.y, _length, _damping, _maxAngleDegrees * Mathf.Deg2Rad);

            if (_flutterDegrees <= 0f) { _flutterValue = 0f; return; }

            // **활동량**이 0 이면 펄럭임도 정확히 0 이다. 곱셈으로 죽이는 이유는
            // 부동소수 잔여가 남지 않게 하기 위해서다 — 감쇠로 줄이면 1e-12 가 영원히
            // 남고, 그러면 「정지했다」가 성립하지 않아 사고 복귀 검증이 실패한다.
            float swing = Mathf.Abs(_pendulum.VelocityX) + Mathf.Abs(_pendulum.VelocityZ);
            float activity = Mathf.Clamp01(a.magnitude * 0.15f + swing * 0.35f);
            float gain = _idleFlutter + (1f - _idleFlutter) * activity;
            if (gain <= 0f) { _flutterValue = 0f; return; }

            _flutterPhase += dt * _flutterHz;
            // 위상을 감아 둔다. 몇 시간 돌아도 float 정밀도가 유지된다.
            if (_flutterPhase > 1024f) _flutterPhase -= 1024f;

            // 두 개의 무리수 비 사인을 겹쳐 주기가 눈에 안 잡히게 한다.
            // 난수가 아니므로 같은 시각이면 항상 같은 값이다 — 캡처가 재현된다.
            float baseWave = Mathf.Sin(_flutterPhase * 6.2831853f)
                           + 0.53f * Mathf.Sin(_flutterPhase * 6.2831853f * 1.618034f + _seedPhase);

            _flutterValue = baseWave * (_flutterDegrees * 0.62f) * gain;
        }

        protected override void Apply()
        {
            if (_pivot == null) return;
            _pivot.localRotation = _homeRotation * Quaternion.Euler(
                _pendulum.AngleX * Mathf.Rad2Deg + _flutterValue,
                0f,
                _pendulum.AngleZ * Mathf.Rad2Deg);
        }

        public override void AddShock(Vector3 worldImpulse)
        {
            Vector3 local = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldImpulse)
                : worldImpulse;
            float invL = 1f / Mathf.Max(_length, 0.03f);
            _pendulum.AddImpulse(local.z * invL, -local.x * invL);
        }
    }
}
