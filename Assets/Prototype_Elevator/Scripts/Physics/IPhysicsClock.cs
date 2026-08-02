using System;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 표현 계층 물리가 시간을 읽는 **유일한** 창구.
    ///
    /// 왜 <c>Time.deltaTime</c> 을 직접 읽지 않는가: 이 저장소의 결정론 요구
    /// (`TECH_SPEC.md` §7, `Captures/baseline.txt` 의 기기 종속 비트 동일성)는
    /// 「같은 시드 → 같은 그림」이다. 프레임률에 묶인 적분은 캡처 하네스가 두 번 돌 때
    /// 두 번 다른 그림을 낸다. 그래서 시계를 **주입**하고, 캡처·테스트는 고정 스텝
    /// 시계를 꽂는다. 승객 반응 라우터가 <c>Func&lt;float&gt;</c> 를 주입받는 것과
    /// 같은 이유이고 같은 관례다(`ASSUMPTION_LOG` A-20260801-07 「교체 지점」).
    ///
    /// 인터페이스 호출은 **프레임당 컴포넌트당 한 번**만 일어난다. 고정 서브스텝
    /// 루프 안에서는 float 하나만 돈다 — 가상 호출을 내부 루프에 넣으면 그 자체가
    /// 비용이고, 구조체를 인터페이스에 담으면 박싱이 곧 할당이 된다(`UP-TECH-04` 0 B).
    /// </summary>
    public interface IPhysicsClock
    {
        /// <summary>이번 프레임에 흘려야 할 시간(초). 음수를 돌려주지 않는다.</summary>
        float DeltaTime { get; }

        /// <summary>시계가 켠 이후 흐른 총 시간(초). 결정론적 잡음의 위상에 쓴다.</summary>
        float ElapsedTime { get; }
    }

    /// <summary>
    /// 평상시 실행용 시계. <c>Time.deltaTime</c> 을 그대로 흘린다.
    ///
    /// 상한이 있는 이유: 도메인 리로드·씬 로드 직후 첫 프레임의 deltaTime 은 수 초에
    /// 달할 수 있고, 그대로 적분하면 진자가 한 프레임에 한 바퀴를 돌아 **터진 것처럼
    /// 보인다.** 물리가 화면을 망가뜨리는 가장 흔한 경로다.
    /// </summary>
    public sealed class RealtimePhysicsClock : IPhysicsClock
    {
        private readonly float _maxDelta;
        private float _elapsed;

        public RealtimePhysicsClock(float maxDelta = 0.1f)
        {
            _maxDelta = maxDelta > 0f ? maxDelta : 0.1f;
        }

        public float DeltaTime
        {
            get
            {
                float dt = UnityEngine.Time.deltaTime;
                if (dt < 0f) dt = 0f;
                if (dt > _maxDelta) dt = _maxDelta;
                return dt;
            }
        }

        public float ElapsedTime => _elapsed;

        /// <summary>
        /// 프레임 하나가 끝날 때 누산한다. <see cref="DeltaTime"/> 이 프로퍼티라서
        /// 여러 번 읽혀도 시간이 두 번 흐르면 안 되므로, 전진은 명시적으로 한다.
        /// </summary>
        public void Advance(float dt) => _elapsed += dt;
    }

    /// <summary>
    /// 결정론 스위치. 프레임률·머신·에디터 상태와 무관하게 **정확히 같은 시간**을 흘린다.
    ///
    /// 캡처 하네스와 헤드리스 테스트가 이것을 꽂는다. 「같은 입력 시퀀스 → 같은 상태
    /// (부동소수 비트 동일)」이 성립하는 근거가 이 클래스다 — 여기서 나가는 dt 는
    /// 항상 같은 float 리터럴이고, 뒤의 적분기는 그 값 외에 시간을 읽는 경로가 없다.
    /// </summary>
    public sealed class FixedStepPhysicsClock : IPhysicsClock
    {
        private readonly float _step;
        private float _elapsed;
        private int _stepIndex;

        public FixedStepPhysicsClock(float step = 1f / 60f)
        {
            if (step <= 0f)
                throw new ArgumentOutOfRangeException(nameof(step),
                    "스텝이 0 이하면 시간이 흐르지 않는다 — 결정론이 아니라 정지다.");
            _step = step;
        }

        /// <summary>고정 스텝 폭. 캡처 하네스가 프레임 간격을 맞출 때 읽는다.</summary>
        public float Step => _step;

        /// <summary>지금까지 흘린 스텝 수. 재현 검증이 "몇 스텝째 상태"를 지정할 때 쓴다.</summary>
        public int StepIndex => _stepIndex;

        public float DeltaTime => _step;

        public float ElapsedTime => _elapsed;

        /// <summary>한 스텝 전진시킨다. 부르지 않으면 시간이 멈춰 있다.</summary>
        public void Tick()
        {
            _stepIndex++;
            // 누산이 아니라 곱으로 낸다. 누산은 스텝 수가 늘수록 오차가 쌓여
            // "같은 스텝 수 → 같은 시각"이 깨진다.
            _elapsed = _step * _stepIndex;
        }

        /// <summary>시각을 0 으로 되돌린다. 같은 시드를 두 번 재생할 때 부른다.</summary>
        public void Reset()
        {
            _stepIndex = 0;
            _elapsed = 0f;
        }
    }
}
