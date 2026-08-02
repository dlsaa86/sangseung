using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 관성에 반응하는 캐빈 오브젝트의 공통 뼈대.
    ///
    /// ─────────────────────────────────────────────────────────────────────────
    /// **왜 이 계층 전체가 <c>Rigidbody</c> 를 쓰지 않는가** — 세 가지 이유이고,
    /// 셋 다 이 저장소에 이미 기록된 요구에서 나온다. 취향이 아니다.
    ///
    /// 1. **결정론.** PhysX 는 프레임률·솔버 반복 수·접촉 순서에 따라 결과가 다르다.
    ///    `Captures/baseline.txt` 는 기기에서 **비트 동일한** 렌더 결과를 요구하고,
    ///    캡처 하네스는 두 번 돌면 같은 그림을 내야 한다. `FixedUpdate` 위의
    ///    강체 시뮬레이션은 그 요구를 만족시키지 못한다. 직접 적분은 만족시킨다 —
    ///    <see cref="FixedStepPhysicsClock"/> 를 꽂으면 float 연산 순서가 고정된다.
    ///
    /// 2. **할당 0.** `UP-TECH-04` 가 「매 프레임 0 B」로 VERIFIED 다. Rigidbody 를
    ///    쓰는 순간 <c>OnCollisionEnter(Collision)</c> 계열이 <c>Collision</c> 객체를
    ///    만들고(`ReuseCollisionCallbacks` 로 줄지만 0 이 되지는 않는다),
    ///    <c>Physics.OverlapSphere</c> 는 배열을 만든다. 여기서는 콜백이 아예 없다.
    ///
    /// 3. **판정과의 분리.** Rigidbody 는 트랜스폼의 **주인**이 된다. 카 이동은
    ///    `ElevatorGrayboxView` 가, 사고 낙하는 `CollapseSequence` 가 이미 소유한다.
    ///    두 주인이 같은 트랜스폼을 쓰면 「복귀 오차 0.00000」이 즉시 깨진다.
    ///    여기서는 항상 <c>홈 + 오프셋</c> 으로 **절대값을 다시 쓰므로** 누적이 없고,
    ///    오프셋이 정확히 0 으로 스냅되면 홈이 비트 단위로 복원된다.
    ///
    /// **그럼 Rigidbody 가 옳았을 곳은 어디인가**(정직하게 적는다):
    /// 사고 순간 화물이 실제로 굴러 흩어지는 그림 — 다물체 접촉·마찰·회전 축 전환은
    /// 손으로 적분하면 값이 안 나온다. 그 하나를 위해 위 셋을 포기할 가치가 없어서
    /// <see cref="LooseCargo"/> 는 **미끄러짐만** 한다(굴리지 않는다). 이는 지시서의
    /// 「작게. 굴러다니면 안 된다」와도 같은 결론이다. 진짜 텀블링이 필요해지면
    /// **사고 연출 전용으로 그 순간에만** Rigidbody 를 켜고, 캡처 결정론이 필요한
    /// 구간에서는 꺼야 한다 — 그건 별도 결정 항목이지 이 작업의 기본값이 아니다.
    /// ─────────────────────────────────────────────────────────────────────────
    ///
    /// 실행 순서 150: 발신원(100)이 이번 프레임 가속도를 낸 **뒤**,
    /// <see cref="CollapsePhysics"/>(200)가 사고 오프셋을 얹기 **전**이다.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public abstract class CabinInertiaReactor : MonoBehaviour
    {
        [Header("관성 발신원")]
        [Tooltip("비면 씬에서 하나 찾는다. 못 찾으면 이 반응자는 조용히 쉰다 — 오류가 아니다.")]
        [SerializeField] protected CabinInertiaSource _source;

        [Header("반응 강도")]
        [Tooltip("가속도에 곱하는 배율. 0 이면 반응하지 않는다. " +
                 "**음수를 허용하는 이유**: 매다는 방향이 뒤집힌 채 배선되면 흔들림이 " +
                 "반대로 읽히는데, 그때 씬에서 부모를 180° 돌리는 것보다 여기서 부호를 " +
                 "뒤집는 편이 안전하다 — 부모 회전은 홈 자세와 그림자까지 바꾼다.")]
        [SerializeField, Range(-3f, 3f)] protected float _responseScale = 1f;

        [Header("서브스텝")]
        [Tooltip("적분 서브스텝 폭(초). 작을수록 안정적이고 비싸다. 240Hz 가 기본이다.")]
        [SerializeField, Range(1f / 480f, 1f / 60f)] private float _subStep = PhysicsStepper.DefaultStep;

        private PhysicsStepper _stepper;
        private bool _homeCaptured;

        /// <summary>이번 프레임의 카 로컬 가속도. 파생 클래스가 <see cref="Integrate"/> 에서 쓴다.</summary>
        protected Vector3 LocalAcceleration { get; private set; }

        /// <summary>시뮬레이션이 흘린 총 시간(초). 결정론적 잡음 위상에 쓴다.</summary>
        protected float SimTime { get; private set; }

        /// <summary>지금 발신원이 붙어 있는가. 진단 프로브가 읽는다.</summary>
        public bool HasSource => _source != null;

        /// <summary>
        /// 이 반응자가 지금 정지 상태인가. 「발산하지 않는다」와
        /// 「사고 복귀 오차 0」의 검사 지점이다.
        /// </summary>
        public abstract bool IsAtRest { get; }

        /// <summary>고정 스텝 하나를 적분한다. 여기서 <b>절대 할당하지 않는다.</b></summary>
        protected abstract void Integrate(float dt);

        /// <summary>적분 결과를 트랜스폼에 쓴다. 항상 <c>홈 + 오프셋</c> 절대값으로 쓴다.</summary>
        protected abstract void Apply();

        /// <summary>홈(기준 자세)을 기록한다. 한 번만 불린다.</summary>
        protected abstract void CaptureHome();

        /// <summary>홈으로 비트 단위 복원하고 상태를 0 으로 지운다.</summary>
        protected abstract void RestoreHome();

        /// <summary>바깥에서 충격을 준다. 사고·문 걸쇠·레버가 부른다.</summary>
        public virtual void AddShock(Vector3 worldImpulse) { }

        /// <summary>상태를 지우고 홈으로 돌린다. 같은 시드 재생·사고 종료가 부른다.</summary>
        public void ResetToHome()
        {
            _stepper.Reset();
            SimTime = 0f;
            LocalAcceleration = Vector3.zero;
            RestoreHome();
        }

        /// <summary>
        /// 시계를 갈아 끼우지 않고 **직접** N 스텝 돌린다. 헤드리스 테스트 전용이며,
        /// 씬 없이 결정론·안정성·할당 0 을 확인하는 경로다.
        /// </summary>
        public void StepForTest(float dt, Vector3 localAcceleration, int steps)
        {
            EnsureHome();
            LocalAcceleration = localAcceleration;
            for (int i = 0; i < steps; i++)
            {
                Integrate(dt);
                SimTime += dt;
            }
        }

        /// <summary>발신원을 코드로 꽂는다. 조립 스크립트와 헤드리스 테스트가 쓴다.</summary>
        public void SetSource(CabinInertiaSource source) => _source = source;

        /// <summary>
        /// 홈을 다시 잡는다. 대상 트랜스폼을 코드로 바꿔 끼운 직후에만 부른다 —
        /// 흔들리는 도중에 부르면 기울어진 자세가 새 기준이 되어 영구히 남는다.
        /// </summary>
        protected void ForceRecaptureHome()
        {
            _homeCaptured = false;
            EnsureHome();
        }

        protected virtual void Awake()
        {
            _stepper.Configure(_subStep);
            EnsureHome();
            if (_source == null) _source = FindAnyObjectByType<CabinInertiaSource>();
        }

        protected virtual void OnDisable()
        {
            // 꺼진 채로 기울어진 자세가 남으면 그 프레임이 캡처에 잡힌다.
            ResetToHome();
        }

        private void EnsureHome()
        {
            if (_homeCaptured) return;
            CaptureHome();
            _homeCaptured = true;
        }

        private void LateUpdate()
        {
            EnsureHome();

            float dt;
            if (_source != null)
            {
                dt = _source.Clock.DeltaTime;
                Vector3 world = _source.Acceleration * _responseScale;
                // 카는 회전하지 않지만, 반응자가 회전한 채 붙어 있을 수 있다.
                // 로컬로 옮겨야 "앞으로 밀렸다"가 그 오브젝트 기준으로 맞는다.
                LocalAcceleration = transform.parent != null
                    ? transform.parent.InverseTransformDirection(world)
                    : world;
            }
            else
            {
                dt = Time.deltaTime;
                if (dt > 0.1f) dt = 0.1f;
                LocalAcceleration = Vector3.zero;
            }

            int steps = _stepper.Begin(dt);
            float s = _stepper.Step;
            for (int i = 0; i < steps; i++)
            {
                Integrate(s);
                SimTime += s;
            }

            Apply();
        }

        /// <summary>
        /// 결정론적 잡음. <c>UnityEngine.Random</c> 을 쓰지 않는 이유는 그것이
        /// **전역 상태**라서, 판정 RNG 와 같은 스트림을 소비하면 시드 재현이 깨지기
        /// 때문이다(`TECH_SPEC.md` §7 「연출용 난수는 판정용 RNG 와 분리한다」).
        /// 여기서는 정수 해시라 스트림 자체가 없다.
        /// </summary>
        protected static float Hash01(int seed, int index)
        {
            uint h = (uint)(seed * 374761393 + index * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) * (1f / 0xFFFFFF);
        }

        /// <summary>-1 ~ 1 의 결정론적 잡음.</summary>
        protected static float HashSigned(int seed, int index) => Hash01(seed, index) * 2f - 1f;

        /// <summary>
        /// 같은 종류의 반응자들이 한 몸처럼 떠는 것을 막는 **인스턴스별 변주 키**.
        /// 계층 경로(이름 + 형제 인덱스)에서 뽑으므로 **실행마다 같다.**
        ///
        /// **`GetInstanceID()` 를 쓰면 안 된다.** 두 가지 이유가 있고 둘 다 치명적이다.
        ///
        /// ① Unity 6000.5 에서 `[Obsolete(error: true)]` 가 됐다 — `CS0619` 로
        ///    **컴파일 자체가 실패한다.** 이 프로젝트는 asmdef 이 없어 파일 하나가
        ///    전체 어셈블리를 막는다(`CLAUDE.md` 탑다운 규칙 4).
        /// ② 그보다 나쁜 것은 **인스턴스 ID 가 실행마다 다르다**는 것이다.
        ///    그것으로 위상을 뽑으면 같은 시드로 돌려도 매 세션 다른 그림이 나온다.
        ///    이 저장소의 고정 캡처는 `machineFingerprint` 로 **비트 단위 재현**을
        ///    요구하므로(`CLAUDE.md` 검증 절), 연출 위상이 세션마다 흔들리면
        ///    베이스라인 비교가 통째로 무의미해진다.
        ///    처음 판본의 주석은 「시드를 고정하면 재현은 유지된다」고 적고 있었는데
        ///    **그것이 사실이 아니었다** — 고정된 것은 시드뿐이고 인덱스가 흔들렸다.
        ///
        /// `string.GetHashCode()` 도 쓰지 않는다 — .NET Core 에서 **프로세스마다
        /// 무작위화**되므로 같은 이름이 같은 값을 주지 않는다. FNV-1a 를 직접 돈다.
        /// </summary>
        protected int StableVariationKey()
        {
            unchecked
            {
                uint h = 2166136261u;
                for (Transform t = transform; t != null; t = t.parent)
                {
                    string n = t.name;
                    for (int i = 0; i < n.Length; i++)
                    {
                        h ^= n[i];
                        h *= 16777619u;
                    }
                    h ^= (uint)t.GetSiblingIndex();
                    h *= 16777619u;
                }
                return (int)(h & 0xFFFF);
            }
        }
    }
}
