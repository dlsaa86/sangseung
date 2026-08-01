using UnityEngine;
using Ascend.Prototype.Events;
using Ascend.Prototype.Player;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// "과수확 레버에 손을 올렸다"를 사건으로 만든다.
    ///
    /// `MASTER_PRD.md` §7 은 과수확을 **공간적 사건**으로 요구한다 —
    /// "조명, 음향, 진동, 승객 반응, 기계 상태를 변화시키는". 그 5단계 중 지금까지
    /// 구현된 것은 해금 연출 하나뿐이었고, 나머지(기계음 감소 · 승객 응시 · 정적)는
    /// **당기기 전** 에 일어나야 한다. 즉 "당겼다"가 아니라 "겨눴다"가 신호다.
    ///
    /// 그 신호를 어디서 얻는가: <see cref="CrosshairInteractor"/> 가 매 프레임
    /// 조준 대상을 들고 있다. 레버가 스스로 알리게 하지 않는 이유는 레버가 게임 상태를
    /// 모르는 스텁으로 유지되고 있기 때문이다(`RouletteInteractionBridge` 주석의 분담).
    ///
    /// 잠긴 레버를 겨누는 것은 사건이 아니다. 덮개가 닫혀 있으면 아무 일도 없어야
    /// "풀렸다"가 의미를 갖는다.
    /// </summary>
    public sealed class OverharvestApproachBridge : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private CrosshairInteractor _interactor;
        [SerializeField] private InteractableOverharvestLever _lever;

        [Tooltip("겨눈 상태가 이 시간 이상 이어져야 접근으로 본다. " +
                 "지나가며 스치는 시선까지 사건으로 세면 정적 구간이 계속 껐다 켜진다.")]
        [SerializeField, Min(0f)] private float _dwellSeconds = 0.15f;

        private GameEventBus _bus;
        private RunSession _session;
        private bool _approaching;
        private float _dwell;

        /// <summary>지금 접근 상태로 판정돼 있는가. 오디오의 정적 구간이 이 값을 따라간다.</summary>
        public bool IsApproaching => _approaching;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_interactor == null) _interactor = FindAnyObjectByType<CrosshairInteractor>();
            if (_lever == null) _lever = FindAnyObjectByType<InteractableOverharvestLever>();
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
            _approaching = false;
            _dwell = 0f;
        }

        private void Update()
        {
            if (_bus == null)
            {
                RunSession session = _run != null ? _run.Session : null;
                if (session == null) return;
                _session = session;
                _bus = session.Events;
            }

            bool aimed = _lever != null && _interactor != null &&
                         ReferenceEquals(_interactor.CurrentInteractable, _lever) &&
                         _lever.IsUnlocked;

            if (aimed)
            {
                _dwell += Time.deltaTime;
                if (!_approaching && _dwell >= _dwellSeconds) Enter();
            }
            else
            {
                _dwell = 0f;
                if (_approaching) Exit();
            }
        }

        private void Enter()
        {
            _approaching = true;
            _bus.Publish(GameEventKind.OverharvestApproached,
                _session != null ? _session.CurrentFloor : 0, -1,
                _session != null && _session.Current != null ? _session.Current.ExtraSpinsTaken : 0,
                _session != null && _session.Current != null ? _session.Current.PendingAnte : 0f,
                "과수확 접근");
        }

        private void Exit()
        {
            _approaching = false;
            _bus.Publish(GameEventKind.OverharvestReleased,
                _session != null ? _session.CurrentFloor : 0, -1, 0, 0f, "접근 해제");
        }
    }
}
