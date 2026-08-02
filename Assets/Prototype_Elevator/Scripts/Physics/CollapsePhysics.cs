using UnityEngine;
using Ascend.Prototype.Risk;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 사고(Collapse) 순간의 **물리 반응**. 낙하 자체는 만들지 않는다.
    ///
    /// <b>`CollapseSequence` 를 재작성하지 않는다.</b> 그쪽은 이미 네 박자
    /// (암전 → 파열음 → 급강하 → 불규칙 재점등)를 소유하고 있고, 실측 낙하량
    /// (lampRig/tank/sign 0.5794m · camRig 0.4138m)과 <b>복귀 오차 0.00000</b> 이
    /// 회귀 방지선으로 기록돼 있다. 여기서는 그 낙하 **위에** 반응을 얹는다.
    ///
    /// 얹는 방식이 이 컴포넌트의 설계 전부다. 세 갈래로 나뉜다.
    ///
    /// 1. <b>합성 가속도</b> — 자유낙하를 <see cref="CabinInertiaSource"/> 에 밀어 넣는다.
    ///    그러면 매달린 것 전부가 <i>이미 있는 진자 방정식으로</i> 반응한다.
    ///    낙하 중에는 유효 중력이 0 에 가까워져 복원력이 사라지고(추가 뜬다),
    ///    바닥을 치는 순간 큰 양의 가속이 들어와 채찍질한다. 별도 연출 코드가 없다 —
    ///    물리가 낙하를 「알아서」 그리는 것이 이 구조를 쓰는 이유다.
    ///
    /// 2. <b>충격 임펄스</b> — 착지 박자에 반응자들에게 직접 임펄스를 준다.
    ///    합성 가속만으로는 방향이 전부 수직이라 그림이 단조롭다. 임펄스에
    ///    결정론적 가로 성분을 섞어 흩어지게 만든다.
    ///
    /// 3. <b>산란</b> — 자기 소유 트랜스폼(파편·공구·서류)을 튀게 한다.
    ///    이 트랜스폼들은 `CollapseSequence` 의 `_dropTargets` 와 <b>겹치면 안 된다.</b>
    ///    겹치면 두 주인이 같은 값을 쓰고, 그 순간 복귀 오차 0 이 깨진다.
    ///    배선 지침이 인수인계 문서에 있다.
    ///
    /// <b>복귀 오차 0 을 어떻게 보장하는가</b>: 연출이 끝나는 프레임에
    /// 산란 대상과 반응자를 <b>무조건</b> 홈으로 되돌린다(감쇠가 충분히 줄었기를
    /// 기대하지 않는다). 재점등 구간이 1.8초라 그때쯤 진폭은 이미 0 에 가깝고,
    /// 그래서 이 강제 복귀는 눈에 보이지 않는다. 「보이지 않으므로 안전하다」가
    /// 아니라 「무조건 복원하므로 안전하고, 마침 보이지도 않는다」가 순서다.
    ///
    /// 실행 순서 200: `CollapseSequence`(기본 0)와 반응자들(150) 뒤다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class CollapsePhysics : MonoBehaviour
    {
        [Header("사고 연출 — 읽기만 한다")]
        [Tooltip("비면 씬에서 찾는다. 이 컴포넌트는 연출의 상태를 읽을 뿐 바꾸지 않는다.")]
        [SerializeField] private CollapseSequence _sequence;

        [Tooltip("합성 가속도를 받을 관성 발신원. 비면 씬에서 찾는다.")]
        [SerializeField] private CabinInertiaSource _source;

        [Header("반응할 대상")]
        [Tooltip("낙하·착지에 채찍질할 반응자들(등·사슬·고리·표찰·승객).")]
        [SerializeField] private CabinInertiaReactor[] _reactors;

        [Tooltip("산란시킬 자기 소유 트랜스폼. **`CollapseSequence` 의 낙하 대상과 겹치지 않게 한다.**")]
        [SerializeField] private Transform[] _scatterTargets;

        [Header("박자 — CollapseSequence 와 같은 값을 넣는다")]
        [Tooltip("암전 지속(초). 이 구간에는 아직 떨어지지 않는다.")]
        [SerializeField, Min(0f)] private float _blackoutSeconds = 0.55f;

        [Tooltip("급강하 지속(초).")]
        [SerializeField, Min(0.05f)] private float _dropSeconds = 0.9f;

        [Tooltip("낙하 구간 중 자유낙하가 차지하는 비율. `CollapseSequence` 의 0.55 와 맞춘다.")]
        [SerializeField, Range(0.1f, 0.9f)] private float _freeFallFraction = 0.55f;

        [Header("합성 가속도")]
        [Tooltip("자유낙하 중 유효 중력이 얼마나 사라지는가. 1 이면 완전 무중력.")]
        [SerializeField, Range(0f, 1f)] private float _weightlessness = 0.85f;

        [Tooltip("착지 충격 가속도(m/s²). 이 값이 매달린 것들의 채찍질 크기를 정한다.")]
        [SerializeField, Range(2f, 40f)] private float _landingAcceleration = 22f;

        [Tooltip("착지 충격이 유지되는 시간(초). 짧을수록 딱딱한 충돌로 읽힌다.")]
        [SerializeField, Range(0.02f, 0.5f)] private float _landingSeconds = 0.11f;

        [Header("임펄스")]
        [Tooltip("착지 순간 반응자에게 주는 임펄스 크기(m/s).")]
        [SerializeField, Range(0f, 6f)] private float _reactorImpulse = 1.9f;

        [Tooltip("임펄스의 가로 성분 비율. 0 이면 전부 수직이라 그림이 단조롭다.")]
        [SerializeField, Range(0f, 1f)] private float _lateralMix = 0.55f;

        [Header("산란")]
        [Tooltip("산란 최대 거리(m). 판독성 상한 — 파편이 계기를 가리면 안 된다.")]
        [SerializeField, Range(0.01f, 0.6f)] private float _maxScatter = 0.16f;

        [Tooltip("산란 고유 각진동수(rad/s).")]
        [SerializeField, Range(2f, 40f)] private float _scatterOmega = 9f;

        [Tooltip("산란 감쇠비. 낮으면 오래 튄다.")]
        [SerializeField, Range(0.05f, 1.2f)] private float _scatterZeta = 0.22f;

        [Tooltip("산란 방향 시드. 같은 시드는 같은 방향으로 흩어진다 — 캡처 재현용이다.")]
        [SerializeField] private int _scatterSeed = 20260802;

        private DampedSpring1D[] _scatterX;
        private DampedSpring1D[] _scatterY;
        private DampedSpring1D[] _scatterZ;
        private Vector3[] _scatterHome;
        private PhysicsStepper _stepper;

        private bool _wasPlaying;
        private bool _landedThisEvent;
        private float _peakDisplacement;
        private bool _homeCaptured;
        private bool _dirty;

        /// <summary>지금 사고 물리가 돌고 있는가.</summary>
        public bool IsActive => _sequence != null && _sequence.IsPlaying;

        /// <summary>이번 사고에서 관측된 최대 산란 변위(m). 「연속 프레임에서 관측 가능」의 증거.</summary>
        public float PeakDisplacement => _peakDisplacement;

        /// <summary>착지 충격이 이미 발생했는가.</summary>
        public bool HasLanded => _landedThisEvent;

        /// <summary>산란 대상 수. 배선 진단이 읽는다.</summary>
        public int ScatterCount => _scatterTargets != null ? _scatterTargets.Length : 0;

        /// <summary>
        /// 배선을 코드로 꽂는다. 재실행 가능한 조립 스크립트와 헤드리스 테스트의 진입점이다.
        /// <paramref name="scatterTargets"/> 에 `CollapseSequence` 의 낙하 대상을 넣지 마라 —
        /// 주인이 둘이 되는 순간 복귀 오차 0 이 깨진다.
        /// </summary>
        public void Configure(CollapseSequence sequence, CabinInertiaSource source,
                              CabinInertiaReactor[] reactors, Transform[] scatterTargets)
        {
            if (sequence != null) _sequence = sequence;
            if (source != null) _source = source;
            if (reactors != null) _reactors = reactors;
            if (scatterTargets != null)
            {
                _scatterTargets = scatterTargets;
                _homeCaptured = false;
            }
            EnsureHome();
        }

        /// <summary>
        /// 착지 임펄스를 강제로 발사한다. 사고 연출 없이 반응만 확인할 때
        /// (헤드리스 테스트·연출 디버그) 쓴다.
        /// </summary>
        public void FireLandingForTest()
        {
            EnsureHome();
            _landedThisEvent = true;
            FireLandingImpulses();
        }

        /// <summary>헤드리스 테스트 전용 산란 적분.</summary>
        public void StepScatterForTest(float dt, int steps)
        {
            EnsureHome();
            for (int i = 0; i < steps; i++) IntegrateScatter(dt);
            ApplyScatter();
        }

        /// <summary>
        /// 모든 상태를 지우고 산란 대상과 반응자를 <b>정확히</b> 홈으로 되돌린다.
        /// 복귀 오차 0 의 실체가 이 메서드다.
        /// </summary>
        public void RestoreAll()
        {
            EnsureHome();

            if (_scatterX != null)
                for (int i = 0; i < _scatterX.Length; i++)
                {
                    _scatterX[i].Reset();
                    _scatterY[i].Reset();
                    _scatterZ[i].Reset();
                }

            if (_scatterTargets != null && _scatterHome != null)
                for (int i = 0; i < _scatterTargets.Length && i < _scatterHome.Length; i++)
                    if (_scatterTargets[i] != null) _scatterTargets[i].localPosition = _scatterHome[i];

            if (_reactors != null)
                for (int i = 0; i < _reactors.Length; i++)
                    if (_reactors[i] != null) _reactors[i].ResetToHome();

            _stepper.Reset();
            _landedThisEvent = false;
            _dirty = false;
        }

        /// <summary>
        /// 사고 경과 시각에 해당하는 합성 수직 가속도(m/s²)를 낸다.
        /// 순수 함수라서 씬 없이 검증할 수 있다 — 「낙하 중에는 음수(가벼워짐),
        /// 착지에는 큰 양수(때림)」가 이 함수의 계약이다.
        /// </summary>
        public float SyntheticVerticalAcceleration(float elapsed)
        {
            float dropStart = _blackoutSeconds;
            float dropEnd = dropStart + _dropSeconds;
            if (elapsed < dropStart || elapsed >= dropEnd + _landingSeconds) return 0f;

            float impactAt = dropStart + _dropSeconds * _freeFallFraction;

            if (elapsed < impactAt)
                return -PendulumState.Gravity * _weightlessness;

            if (elapsed < impactAt + _landingSeconds)
            {
                // 충격은 순간적으로 최대였다가 선형으로 죽는다. 계단 함수로 두면
                // 서브스텝 위치에 따라 충격을 통째로 놓치는 프레임이 생긴다.
                float k = 1f - (elapsed - impactAt) / _landingSeconds;
                return _landingAcceleration * k;
            }

            // 착지 후 낙하 구간의 나머지는 감쇠 진동 구간이다. `CollapseSequence` 가
            // 그 구간을 이미 그리므로 여기서는 가속을 더하지 않는다.
            return 0f;
        }

        private void Awake()
        {
            if (_sequence == null) _sequence = FindAnyObjectByType<CollapseSequence>();
            if (_source == null) _source = FindAnyObjectByType<CabinInertiaSource>();
            _stepper.Configure(PhysicsStepper.DefaultStep);
            EnsureHome();
        }

        private void OnDisable() => RestoreAll();

        private void EnsureHome()
        {
            if (_homeCaptured) return;
            int n = _scatterTargets != null ? _scatterTargets.Length : 0;
            _scatterHome = new Vector3[n];
            _scatterX = new DampedSpring1D[n];
            _scatterY = new DampedSpring1D[n];
            _scatterZ = new DampedSpring1D[n];
            for (int i = 0; i < n; i++)
                if (_scatterTargets[i] != null) _scatterHome[i] = _scatterTargets[i].localPosition;
            _homeCaptured = true;
        }

        private void LateUpdate()
        {
            EnsureHome();

            bool playing = _sequence != null && _sequence.IsPlaying;

            if (playing && !_wasPlaying)
            {
                _peakDisplacement = 0f;
                _landedThisEvent = false;
            }

            if (!playing)
            {
                if (_wasPlaying)
                {
                    // 연출이 끝난 프레임. **무조건** 복원한다 — 감쇠가 충분히 줄었기를
                    // 기대하지 않는다. 이 한 줄이 「복귀 오차 0.00000」의 보증이다.
                    RestoreAll();
                }
                _wasPlaying = false;
                return;
            }

            _wasPlaying = true;

            float elapsed = _sequence.Elapsed;
            float verticalAccel = SyntheticVerticalAcceleration(elapsed);

            // 1) 합성 가속도를 발신원에 밀어 넣는다. 매달린 것 전부가 여기에 반응한다.
            //    발신원은 매 프레임 외부 가속을 지우므로 계속 밀어야 한다.
            if (_source != null && verticalAccel != 0f)
                _source.PushAcceleration(new Vector3(0f, verticalAccel, 0f));

            // 2) 착지 임펄스는 이벤트당 한 번.
            float impactAt = _blackoutSeconds + _dropSeconds * _freeFallFraction;
            if (!_landedThisEvent && elapsed >= impactAt)
            {
                _landedThisEvent = true;
                FireLandingImpulses();
            }

            // 3) 산란 적분.
            float dt = _source != null ? _source.Clock.DeltaTime : Time.deltaTime;
            if (dt > 0.1f) dt = 0.1f;
            int steps = _stepper.Begin(dt);
            float s = _stepper.Step;
            for (int i = 0; i < steps; i++) IntegrateScatter(s);

            ApplyScatter();
        }

        private void FireLandingImpulses()
        {
            if (_reactors != null && _reactorImpulse > 0f)
            {
                for (int i = 0; i < _reactors.Length; i++)
                {
                    CabinInertiaReactor r = _reactors[i];
                    if (r == null) continue;
                    // 방향은 결정론적 해시로 정한다. 난수를 쓰면 캡처가 재현되지 않고,
                    // 전부 같은 방향이면 흩어지는 것으로 안 읽힌다.
                    float ax = HashSigned(_scatterSeed, i * 3 + 0) * _lateralMix;
                    float az = HashSigned(_scatterSeed, i * 3 + 1) * _lateralMix;
                    r.AddShock(new Vector3(ax, 1f, az).normalized * _reactorImpulse);
                }
            }

            if (_scatterX == null) return;
            for (int i = 0; i < _scatterX.Length; i++)
            {
                float sx = HashSigned(_scatterSeed, i * 5 + 0);
                float sy = Hash01(_scatterSeed, i * 5 + 1);
                float sz = HashSigned(_scatterSeed, i * 5 + 2);
                float v = _maxScatter * _scatterOmega;
                _scatterX[i].AddImpulse(sx * v);
                _scatterY[i].AddImpulse(sy * v * 0.7f);
                _scatterZ[i].AddImpulse(sz * v);
            }
            _dirty = true;
        }

        private void IntegrateScatter(float dt)
        {
            if (_scatterX == null) return;
            for (int i = 0; i < _scatterX.Length; i++)
            {
                _scatterX[i].Step(dt, 0f, _scatterOmega, _scatterZeta, _maxScatter);
                _scatterY[i].Step(dt, 0f, _scatterOmega, _scatterZeta, _maxScatter);
                _scatterZ[i].Step(dt, 0f, _scatterOmega, _scatterZeta, _maxScatter);
            }
        }

        private void ApplyScatter()
        {
            if (_scatterTargets == null || _scatterHome == null || _scatterX == null) return;
            if (!_dirty) return;

            bool anyMoving = false;
            for (int i = 0; i < _scatterTargets.Length && i < _scatterHome.Length; i++)
            {
                Transform t = _scatterTargets[i];
                if (t == null) continue;

                float x = _scatterX[i].Value;
                float y = _scatterY[i].Value;
                float z = _scatterZ[i].Value;

                Vector3 p = _scatterHome[i];
                p.x += x; p.y += y; p.z += z;
                t.localPosition = p;

                float d = Mathf.Sqrt(x * x + y * y + z * z);
                if (d > _peakDisplacement) _peakDisplacement = d;
                if (d > 0f) anyMoving = true;
            }

            // 전부 정확히 0 이 되면 마지막으로 홈을 한 번 더 쓰고 손을 뗀다.
            // 「쓰지 않는 것」이 복귀 오차 0 의 나머지 절반이다.
            if (!anyMoving) _dirty = false;
        }

        private static float Hash01(int seed, int index)
        {
            uint h = (uint)(seed * 374761393 + index * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) * (1f / 0xFFFFFF);
        }

        private static float HashSigned(int seed, int index) => Hash01(seed, index) * 2f - 1f;
    }
}
