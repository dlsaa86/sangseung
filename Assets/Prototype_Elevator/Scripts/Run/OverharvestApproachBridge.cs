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
        private bool _forcedAim;
        private string _blockReason = "아직 한 프레임도 돌지 않았다";

        /// <summary>지금 접근 상태로 판정돼 있는가. 오디오의 정적 구간이 이 값을 따라간다.</summary>
        public bool IsApproaching => _approaching;

        /// <summary>1단계가 발화한 횟수. 0 이면 §7.3 의 1~4단계는 이 런에서 한 번도 없었다.</summary>
        public int ApproachCount { get; private set; }

        /// <summary>조준을 거둔 횟수.</summary>
        public int ReleaseCount { get; private set; }

        /// <summary>지금 조준이 성립해 있는가(강제 조준 포함).</summary>
        public bool IsAimingAtLever => _forcedAim || IsRealAim;

        /// <summary>사람이 실제로 겨누고 있는가. 강제 조준은 세지 않는다.</summary>
        public bool IsRealAim =>
            _lever != null && _interactor != null &&
            ReferenceEquals(_interactor.CurrentInteractable, _lever);

        /// <summary>조준이 이어진 시간. 이 값이 <see cref="DwellThresholdSeconds"/> 를 넘으면 1단계다.</summary>
        public float DwellSeconds => _dwell;

        /// <summary>1단계로 인정하는 조준 지속 시간.</summary>
        public float DwellThresholdSeconds => _dwellSeconds;

        /// <summary>배선 진단 — 조준기를 찾았는가.</summary>
        public bool HasInteractor => _interactor != null;

        /// <summary>배선 진단 — 레버를 찾았는가.</summary>
        public bool HasLever => _lever != null;

        /// <summary>
        /// **왜 1단계가 안 뜨는가.** 조준이 성립하면 null.
        ///
        /// 이 속성이 없어서 지난 감사가 「1단계가 어떤 런에서도 발화한 적이 없다」까지만
        /// 알아내고 원인을 짚지 못했다. 실패한 조건이 무엇인지 남기지 않으면
        /// 「배선이 빠졌다」와 「겨누지 않았다」와 「잠겨 있다」가 화면에서 똑같이 보인다.
        /// </summary>
        public string LastBlockReason => _blockReason;

        /// <summary>
        /// 조준을 흉내 낸다. **하네스 전용 진입점이다.**
        ///
        /// 왜 필요한가: `TenFloorAutoPilot` 은 레버를 겨누지 않고
        /// <c>InteractableOverharvestLever.Interact()</c> 를 직접 부른다. 그래서
        /// <see cref="CrosshairInteractor.CurrentInteractable"/> 이 한 번도 레버가 되지 않고,
        /// dwell 이 성립하지 않고, §7.3 의 1~4단계가 통째로 죽은 채로 모든 검사가 통과했다.
        /// 브리지 자체는 멀쩡하다 — 사람이 겨누면 지금도 뜬다.
        ///
        /// **dwell 은 면제하지 않는다.** 호출자가 <c>true</c> 로 둔 채 0.15초를 실제로
        /// 흘려보내야 1단계가 뜬다. 면제하면 「0.15초가 지켜지는가」를 검증하는 경로가
        /// 사라지고, 그 상수는 다시 아무도 안 읽는 값이 된다.
        ///
        /// 잠긴 레버에는 걸리지 않는다 — 잠긴 레버를 겨누는 것은 사건이 아니다.
        /// </summary>
        public void SimulateAim(bool aiming) => _forcedAim = aiming;

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

        /// <summary>
        /// 접근 상태로 꺼지면 아무도 해제를 발행하지 않는다 — 오디오의 정적 구간과
        /// 연출의 감쇠가 영원히 걸린 채로 남는다. 빌린 것은 돌려준다.
        /// </summary>
        private void OnDisable()
        {
            _forcedAim = false;
            _dwell = 0f;
            if (_approaching && _bus != null) Exit();
            else _approaching = false;
        }

        private void OnRunStarted(RunSession session)
        {
            _session = session;
            _bus = session != null ? session.Events : null;
            _approaching = false;
            _dwell = 0f;

            // 카운터는 런 단위다. 넘겨받으면 "지난 런에서 한 번 떴으니 됐다"가 되어
            // 이번 런에서 1단계가 죽은 것을 덮는다.
            ApproachCount = 0;
            ReleaseCount = 0;
        }

        private void Update()
        {
            if (_bus == null)
            {
                RunSession session = _run != null ? _run.Session : null;
                if (session != null)
                {
                    _session = session;
                    _bus = session.Events;
                }
            }

            // dwell 은 버스와 무관하게 센다. 버스가 아직 없다는 이유로 여기서 빠져나가면
            // 「겨눴는데 아무 일도 없다」와 「겨누지 않았다」가 같은 모습이 된다.
            bool aimed = ComputeAim();

            if (aimed)
            {
                // 오디오의 정적 구간과 같은 시계를 쓴다(§7.3 은 0.3~0.7초를 못박는다).
                _dwell += Time.unscaledDeltaTime;
                if (!_approaching && _dwell >= _dwellSeconds)
                {
                    if (_bus != null) Enter();
                    else _blockReason = "사건 버스 미연결 — 런이 아직 시작되지 않았다";
                }
            }
            else
            {
                _dwell = 0f;
                if (_approaching) Exit();
            }
        }

        /// <summary>조준 판정과 **실패 사유**를 함께 낸다.</summary>
        private bool ComputeAim()
        {
            if (_lever == null)
            {
                _blockReason = "InteractableOverharvestLever 참조 없음";
                return false;
            }

            if (!_lever.IsUnlocked)
            {
                _blockReason = "레버 잠김 — 요구 전력 미달. 잠긴 레버 조준은 사건이 아니다";
                return false;
            }

            if (_forcedAim)
            {
                _blockReason = null;
                return true;
            }

            if (_interactor == null)
            {
                _blockReason = "CrosshairInteractor 참조 없음";
                return false;
            }

            if (!ReferenceEquals(_interactor.CurrentInteractable, _lever))
            {
                _blockReason = "조준 대상이 레버가 아니다 — 하네스는 레버를 겨누지 않고 " +
                               "Interact() 를 직접 부른다. SimulateAim(true) 로 조준을 만들어야 한다";
                return false;
            }

            _blockReason = null;
            return true;
        }

        private void Enter()
        {
            _approaching = true;
            ApproachCount++;
            _bus.Publish(GameEventKind.OverharvestApproached,
                _session != null ? _session.CurrentFloor : 0, -1,
                _session != null && _session.Current != null ? _session.Current.ExtraSpinsTaken : 0,
                _session != null && _session.Current != null ? _session.Current.PendingAnte : 0f,
                "과수확 접근");
        }

        private void Exit()
        {
            _approaching = false;
            ReleaseCount++;
            if (_bus == null) return;
            _bus.Publish(GameEventKind.OverharvestReleased,
                _session != null ? _session.CurrentFloor : 0, -1, 0, 0f, "접근 해제");
        }
    }
}
