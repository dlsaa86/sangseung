using UnityEngine;
using Ascend.Prototype.Events;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Risk
{
    /// <summary>
    /// 위험 단계가 **바뀐 순간**을 사건으로 내보낸다.
    ///
    /// 왜 <see cref="RiskStateView"/> 안에 넣지 않았나: 그 컴포넌트는 연출 담당이다.
    /// 사건 발행까지 떠맡기면 "연출을 끄면 사운드와 승객 반응도 같이 죽는" 결합이 생긴다.
    ///
    /// 2026-08-05 정정 — **이 주석은 사흘 동안 거짓이었다.** 「`RiskStateView` 는 꺼도
    /// 게임이 도는 순수 연출」이라 적어 놓고, 실제로는 판정 상태(<see cref="RiskEvaluator"/>)가
    /// 그 컴포넌트의 `LateUpdate` 안에 살아서 뷰를 끄면 <see cref="RiskStateView.Level"/> 이
    /// 얼어붙고 사운드·승객 반응·텔레메트리가 통째로 죽었다. `HeroSlicePerfProbe` 가
    /// 실제로 그 뷰를 끈다. 틀린 근거가 남아 있으면 다음 사람이 그걸 믿고 뷰를 끈다.
    ///
    /// 이제는 참이다 — 평가기의 소유자가 <see cref="Run.RunSession"/> 이고, 뷰는
    /// <see cref="Run.RunSession.RiskLevel"/> 을 **읽기만** 한다. 뷰를 꺼도 판정은 돈다.
    /// 판정을 뷰의 프레임 주기에서 떼어낸 이유는 결합만이 아니다 — 히스테리시스 상태는
    /// **입력의 함수가 아니라 입력 궤적의 함수**라, 프레임 단위로 샘플링하면 한 프레임 안에서
    /// 오르내린 중간값을 놓쳐 같은 시드가 다른 사고 기록을 남겼다.
    ///
    /// 폴링인 이유는 <see cref="RiskEvaluator"/> 가 이벤트를 내지 않는 순수 계산기이기
    /// 때문이다. 프레임마다 열거형 하나를 비교하는 비용이므로 할당이 없다.
    /// </summary>
    public sealed class RiskEventBridge : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private RiskStateView _risk;

        private GameEventBus _bus;
        private RunSession _session;
        private RiskLevel _last = RiskLevel.Stable;
        private bool _primed;

        /// <summary>마지막으로 알린 단계. 테스트와 디버그 패널이 읽는다.</summary>
        public RiskLevel LastAnnounced => _last;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
        }

        private void OnRunStarted(RunSession session)
        {
            _session = session;
            _bus = session != null ? session.Events : null;
            _last = RiskLevel.Stable;
            _primed = false;
        }

        private void Update()
        {
            if (_risk == null) return;
            if (_bus == null)
            {
                // 런이 코드로 먼저 만들어졌고 RunStarted 를 놓친 경우를 위한 늦은 결선.
                RunSession session = _run != null ? _run.Session : null;
                if (session == null) return;
                _session = session;
                _bus = session.Events;
            }

            RiskLevel now = _risk.Level;
            if (_primed && now == _last) return;

            RiskLevel previous = _last;
            _last = now;
            _primed = true;

            // 첫 프레임의 Stable 은 "바뀐 것"이 아니다. 그것까지 알리면 모든 런이
            // 시작하자마자 승객 반응과 사운드를 한 번씩 터뜨린다.
            if (previous == now) return;

            _bus.Publish(GameEventKind.RiskLevelChanged,
                _session != null ? _session.CurrentFloor : 0, -1,
                (int)now, _risk.Score, _risk.Reason, previous);
        }
    }
}
