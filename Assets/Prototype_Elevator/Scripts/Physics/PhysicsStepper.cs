using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 가변 프레임을 **고정 서브스텝**으로 잘라 주는 누산기.
    ///
    /// 왜 필요한가: 감쇠 스프링과 진자를 프레임 dt 로 그냥 적분하면 dt 가 커지는 순간
    /// 발산한다(반음시적 오일러의 안정 조건 <c>dt &lt; 2/ω</c>). 60fps 에서는 멀쩡하고
    /// 20fps 에서 터지는 종류의 버그라서 개발 중에는 절대 안 잡힌다.
    ///
    /// 동시에 이것이 **결정론의 실체**다. 고정 스텝 시계를 꽂으면 프레임당 정확히
    /// 같은 횟수의 같은 폭 스텝이 돌고, 부동소수 결과가 비트 단위로 재현된다.
    ///
    /// 구조체인 이유: 컴포넌트마다 하나씩 필드로 갖는다. 클래스면 컴포넌트 수만큼
    /// 힙 객체가 생기고, 그것 자체는 1회 할당이라 프레임 예산을 깨지 않지만
    /// 참조 추적 비용을 이유 없이 늘린다.
    /// </summary>
    [System.Serializable]
    public struct PhysicsStepper
    {
        /// <summary>서브스텝 폭(초). 240Hz 는 진자·스프링이 안정적으로 도는 하한이다.</summary>
        public const float DefaultStep = 1f / 240f;

        /// <summary>
        /// 한 프레임에 허용하는 최대 서브스텝 수. 이 상한이 없으면 히칭 한 번이
        /// 수천 스텝을 돌려 그 프레임을 더 크게 히칭시킨다(죽음의 나선).
        /// </summary>
        public const int MaxStepsPerFrame = 8;

        private float _accumulator;
        private float _step;
        private int _remaining;

        /// <summary>서브스텝 폭. 0 이면 <see cref="DefaultStep"/> 로 취급한다.</summary>
        public float Step => _step > 0f ? _step : DefaultStep;

        /// <summary>남은 서브스텝 수. <see cref="Next"/> 가 소모한다.</summary>
        public int Remaining => _remaining;

        /// <summary>
        /// 서브스텝 폭을 정한다. 캡처 하네스가 시계 스텝과 맞추고 싶을 때 부른다.
        /// </summary>
        public void Configure(float step)
        {
            _step = step > 0f ? step : DefaultStep;
        }

        /// <summary>
        /// 프레임 하나의 시간을 넣고, 이번 프레임에 돌릴 서브스텝 수를 받는다.
        /// 남는 시간은 다음 프레임으로 이월된다 — 버리면 슬로모션이 되고,
        /// 그러면 「같은 스텝 수 → 같은 상태」가 프레임률에 흔들린다.
        /// </summary>
        public int Begin(float deltaTime)
        {
            float s = Step;
            if (deltaTime > 0f) _accumulator += deltaTime;

            int steps = 0;
            while (_accumulator >= s && steps < MaxStepsPerFrame)
            {
                _accumulator -= s;
                steps++;
            }

            // 상한에 걸렸다면 이월분을 버린다. 안 버리면 다음 프레임에도 상한에 걸리고,
            // 누산기가 영원히 줄지 않아 시뮬레이션이 실시간보다 뒤처진 채로 고정된다.
            if (steps >= MaxStepsPerFrame && _accumulator > s) _accumulator = 0f;

            _remaining = steps;
            return steps;
        }

        /// <summary>서브스텝을 하나 소모한다. 남아 있으면 true.</summary>
        public bool Next()
        {
            if (_remaining <= 0) return false;
            _remaining--;
            return true;
        }

        /// <summary>이월분과 남은 스텝을 지운다. 시퀀스를 되감을 때 부른다.</summary>
        public void Reset()
        {
            _accumulator = 0f;
            _remaining = 0;
        }
    }

    /// <summary>
    /// 감쇠 스프링 1축. 반음시적(semi-implicit) 오일러로 적분한다.
    ///
    /// 왜 명시적 오일러가 아닌가: 명시적 오일러는 조화 진동에서 **에너지를 만든다.**
    /// 감쇠를 걸어도 진폭이 서서히 커지고, 몇 분 뒤 캡처에서 램프가 천장을 뚫는다.
    /// 반음시적은 같은 비용으로 유계(bounded)다.
    ///
    /// 검증 단정 「에너지를 잃고 정지한다 — 극단 입력에서도」가 이 선택의 근거다.
    /// </summary>
    [System.Serializable]
    public struct DampedSpring1D
    {
        /// <summary>정지로 간주하는 문턱. 이 아래면 **정확히 0** 으로 스냅한다.</summary>
        public const float RestEpsilon = 1e-5f;

        public float Value;
        public float Velocity;

        /// <summary>
        /// <paramref name="target"/> 을 향해 한 스텝 적분한다.
        /// </summary>
        /// <param name="dt">서브스텝 폭. 안정 조건은 <c>dt &lt; 2/ω</c> 이고
        /// <see cref="PhysicsStepper.DefaultStep"/> 은 ω ≤ 480 까지 견딘다.</param>
        /// <param name="omega">고유 각진동수(rad/s). 클수록 빠르게 떨린다.</param>
        /// <param name="zeta">감쇠비. 1 이면 임계 감쇠(오버슛 없음), 0.1 이면 잘 흔들린다.</param>
        /// <param name="maxAbs">진폭 상한. 판독성 제약이 여기 산다 — 물리가 화면을 가리면 실패다.</param>
        public void Step(float dt, float target, float omega, float zeta, float maxAbs)
            => Step(dt, target, omega, zeta, maxAbs, 0f);

        /// <summary>
        /// 쉬는 위치가 0 이 아닌 경우를 위한 판본.
        ///
        /// <b>여기서 한 번 틀렸다.</b> 처음 판본은 상한을 <c>target ± maxAbs</c> 로 걸었다.
        /// 목표가 고정된 곳(문 오버슛·산란)에서는 맞지만, 목표가 **움직이는** 곳
        /// (사슬의 추종, 고리의 비틀림, 케이블의 순응 변위)에서는 상한이 목표를 따라
        /// 함께 움직여 실효 상한이 최대 <b>두 배</b>가 된다. 헤드리스 검사가 사슬
        /// 18° 상한에서 18.39°, 고리 80° 에서 80.27°, 케이블에서 초과를 잡아냈다.
        /// 판독성 상한이 「대체로 지켜지는」 것은 상한이 아니다.
        ///
        /// 그래서 상한은 <paramref name="center"/> 기준의 **절대 한계**다.
        /// 목표가 어디로 가든 값은 이 창을 못 벗어난다.
        /// </summary>
        /// <param name="center">상한을 재는 기준점. 보통 0(쉬는 위치)이다.</param>
        public void Step(float dt, float target, float omega, float zeta, float maxAbs, float center)
        {
            if (dt <= 0f) return;
            if (omega <= 0f) omega = 1f;
            if (zeta < 0f) zeta = 0f;

            float x = Value - target;
            // 반음시적: 속도를 먼저 갱신하고 그 **새 속도**로 위치를 옮긴다.
            float accel = -(omega * omega) * x - (2f * zeta * omega) * Velocity;
            Velocity += accel * dt;
            Value += Velocity * dt;

            // 진폭 상한. 넘으면 속도까지 죽인다 — 위치만 자르면 벽에 붙어 떠는
            // 상태가 되고, 그건 물리가 아니라 고장으로 읽힌다.
            if (maxAbs > 0f)
            {
                float lo = center - maxAbs;
                float hi = center + maxAbs;
                if (Value < lo) { Value = lo; if (Velocity < 0f) Velocity = 0f; }
                else if (Value > hi) { Value = hi; if (Velocity > 0f) Velocity = 0f; }
            }

            // 정확히 0 으로 스냅하는 것이 회귀 방지선이다. `CollapseSequence` 의
            // 「복귀 오차 0.00000」은 잔여 1e-9 도 허용하지 않는다.
            if (Mathf.Abs(Value - target) < RestEpsilon && Mathf.Abs(Velocity) < RestEpsilon)
            {
                Value = target;
                Velocity = 0f;
            }
        }

        /// <summary>임펄스를 넣는다. 레버 당김·문 걸쇠·사고 충격이 이걸로 들어온다.</summary>
        public void AddImpulse(float velocityDelta)
        {
            Velocity += velocityDelta;
        }

        /// <summary>상태를 정확히 초기화한다. 비트 단위로 0 이어야 복귀 오차가 0 이 된다.</summary>
        public void Reset(float value = 0f)
        {
            Value = value;
            Velocity = 0f;
        }

        /// <summary>휴식 중인가. 정지 판정 단정과 스냅 최적화가 함께 읽는다.</summary>
        public bool IsAtRest(float target = 0f)
            => Value == target && Velocity == 0f;

        /// <summary>
        /// 단위 질량 기준 총 역학 에너지. 「발산하지 않는다」를 수치로 확인하는 축이다.
        /// </summary>
        public float Energy(float target, float omega)
        {
            float x = Value - target;
            return 0.5f * (Velocity * Velocity) + 0.5f * (omega * omega) * (x * x);
        }
    }

    /// <summary>
    /// 받침점 가속에 반응하는 2축 진자. 매달린 것 전부(등·고리·표찰·사슬)의 공통 코어다.
    ///
    /// 방정식: <c>θ'' = -(g/L)·sinθ - c·θ' - (a/L)·cosθ</c>
    /// 마지막 항이 이 시스템의 존재 이유다 — **받침점이 위로 가속하면 추는 뒤처진다.**
    /// 엘리베이터가 출발·정지할 때 천장등이 뒤로 밀리는 그 신호가 여기서 나온다.
    ///
    /// <c>Rigidbody</c> + <c>HingeJoint</c> 로도 같은 그림이 나오지만 쓰지 않았다.
    /// 근거는 <see cref="CabinInertiaReactor"/> 의 주석에 모아 두었다.
    /// </summary>
    [System.Serializable]
    public struct PendulumState
    {
        /// <summary>중력 가속도(m/s²). 표현용이므로 실제 값을 쓰되 노출은 하지 않는다.</summary>
        public const float Gravity = 9.81f;

        /// <summary>X축(앞뒤) 각도, 라디안.</summary>
        public float AngleX;
        /// <summary>Z축(좌우) 각도, 라디안.</summary>
        public float AngleZ;

        public float VelocityX;
        public float VelocityZ;

        /// <summary>
        /// 한 서브스텝 적분한다.
        /// </summary>
        /// <param name="dt">서브스텝 폭.</param>
        /// <param name="accelX">받침점의 X 가속(m/s²). 카 로컬 기준.</param>
        /// <param name="accelZ">받침점의 Z 가속.</param>
        /// <param name="accelY">받침점의 Y 가속. 유효 중력을 바꾼다 — 상승 가속 중에는
        /// 추가 무거워져 진동이 빨라지고, 자유낙하에서는 가벼워져 채찍질한다.</param>
        /// <param name="length">줄 길이(m). 짧을수록 빠르게 떤다.</param>
        /// <param name="damping">각속도 감쇠 계수(1/s).</param>
        /// <param name="maxAngle">각도 상한(라디안). 판독성 제약.</param>
        public void Step(float dt, float accelX, float accelZ, float accelY,
                         float length, float damping, float maxAngle)
        {
            if (dt <= 0f) return;
            if (length < 0.05f) length = 0.05f;
            if (damping < 0f) damping = 0f;

            // 유효 중력. 자유낙하(accelY = -9.81)에서 0 이 되어 복원력이 사라지는 것이
            // 물리적으로 옳다. 다만 음수로 뒤집히면 추가 위로 서 버리므로 바닥을 깐다.
            float gEff = Gravity + accelY;
            if (gEff < 0.5f) gEff = 0.5f;

            float invL = 1f / length;

            float aX = -(gEff * invL) * Mathf.Sin(AngleX)
                       - damping * VelocityX
                       - (accelX * invL) * Mathf.Cos(AngleX);
            float aZ = -(gEff * invL) * Mathf.Sin(AngleZ)
                       - damping * VelocityZ
                       - (accelZ * invL) * Mathf.Cos(AngleZ);

            VelocityX += aX * dt;
            VelocityZ += aZ * dt;
            AngleX += VelocityX * dt;
            AngleZ += VelocityZ * dt;

            if (maxAngle > 0f)
            {
                if (AngleX < -maxAngle) { AngleX = -maxAngle; if (VelocityX < 0f) VelocityX = 0f; }
                else if (AngleX > maxAngle) { AngleX = maxAngle; if (VelocityX > 0f) VelocityX = 0f; }

                if (AngleZ < -maxAngle) { AngleZ = -maxAngle; if (VelocityZ < 0f) VelocityZ = 0f; }
                else if (AngleZ > maxAngle) { AngleZ = maxAngle; if (VelocityZ > 0f) VelocityZ = 0f; }
            }

            if (Mathf.Abs(AngleX) < DampedSpring1D.RestEpsilon &&
                Mathf.Abs(VelocityX) < DampedSpring1D.RestEpsilon)
            {
                AngleX = 0f; VelocityX = 0f;
            }
            if (Mathf.Abs(AngleZ) < DampedSpring1D.RestEpsilon &&
                Mathf.Abs(VelocityZ) < DampedSpring1D.RestEpsilon)
            {
                AngleZ = 0f; VelocityZ = 0f;
            }
        }

        /// <summary>임펄스(각속도 증분)를 준다. 사고 순간의 채찍질이 이걸로 들어온다.</summary>
        public void AddImpulse(float velocityX, float velocityZ)
        {
            VelocityX += velocityX;
            VelocityZ += velocityZ;
        }

        public void Reset()
        {
            AngleX = 0f; AngleZ = 0f;
            VelocityX = 0f; VelocityZ = 0f;
        }

        public bool IsAtRest()
            => AngleX == 0f && AngleZ == 0f && VelocityX == 0f && VelocityZ == 0f;

        /// <summary>
        /// 단위 질량·단위 길이 기준 총 에너지. 감쇠가 실제로 에너지를 빼는지
        /// 헤드리스로 확인하는 축이다(발산 회귀 방지).
        /// </summary>
        public float Energy(float length)
        {
            if (length < 0.05f) length = 0.05f;
            float kinetic = 0.5f * length * length *
                            (VelocityX * VelocityX + VelocityZ * VelocityZ);
            float potential = Gravity * length *
                              ((1f - Mathf.Cos(AngleX)) + (1f - Mathf.Cos(AngleZ)));
            return kinetic + potential;
        }
    }
}
