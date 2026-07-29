using UnityEngine;
using Ascend.Prototype.Player;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// 엘리베이터 안의 물리 오브젝트를 런 상태에 연결한다.
    ///
    /// Player/ 의 Interactable* 스텁은 일부러 게임 상태를 모른다(UnityEvent만 노출한다).
    /// 그래야 씬 배치와 게임 로직을 따로 작업할 수 있다. 그 둘을 잇는 곳이 여기 한 군데다.
    ///
    /// 조작 분담:
    ///   계약 패널 — 누를 때마다 다음 계약을 미리보기로 넘긴다. 확정하지 않는다.
    ///   레버       — 미리보기 중이던 계약을 확정하고 스핀한다. 이미 계약했으면 그냥 스핀한다.
    ///   전력 탱크  — 전력을 확정하고 다음 층으로 간다.
    /// 고르는 곳과 되돌릴 수 없는 곳을 분리했다. 한 오브젝트가 선택과 확정을 겸하면
    /// 잘못 눌렀을 때 되돌릴 방법이 없다.
    /// </summary>
    public sealed class RouletteInteractionBridge : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private InteractableLever _lever;
        [SerializeField] private InteractableContractPanel _contractPanel;
        [SerializeField] private InteractablePowerTank _powerTank;

        private int _previewIndex;

        /// <summary>계약 패널이 지금 가리키고 있는 선택지. HUD가 읽어서 표시한다.</summary>
        public int PreviewIndex => _previewIndex;

        /// <summary>미리보기 중인 계약. 선택지가 없으면 계약 없음.</summary>
        public ResistanceContract PreviewContract
        {
            get
            {
                FloorSession f = _run != null && _run.Session != null ? _run.Session.Current : null;
                var choices = f?.Plan.ContractChoices;
                if (choices == null || choices.Length == 0) return ResistanceContract.None;
                return choices[Mathf.Clamp(_previewIndex, 0, choices.Length - 1)];
            }
        }

        private void Awake()
        {
            if (_run == null) _run = GetComponent<RunSessionBehaviour>();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();

            if (_lever != null) _lever.onPulled.AddListener(OnLeverPulled);
            if (_contractPanel != null) _contractPanel.onOpened.AddListener(OnContractPanelPressed);
            if (_powerTank != null) _powerTank.onBanked.AddListener(OnPowerTankPressed);
        }

        private void OnDestroy()
        {
            if (_lever != null) _lever.onPulled.RemoveListener(OnLeverPulled);
            if (_contractPanel != null) _contractPanel.onOpened.RemoveListener(OnContractPanelPressed);
            if (_powerTank != null) _powerTank.onBanked.RemoveListener(OnPowerTankPressed);
        }

        private void Update()
        {
            // 상호작용 가능 여부는 매 프레임 상태에서 끌어온다. 스텁이 상태를 모르므로
            // 여기서 밀어주지 않으면 항상 눌리는 것처럼 보인다 — 그건 거짓 정보다.
            FloorSession f = _run != null && _run.Session != null ? _run.Session.Current : null;
            bool alive = f != null && _run.Session != null && !_run.Session.IsComplete && !_run.Session.IsFailed;

            if (_contractPanel != null)
            {
                var choices = f?.Plan.ContractChoices;
                _contractPanel.SetCanInteract(
                    alive && f.Phase == FloorPhase.ContractSelection && choices != null && choices.Length > 1);
            }

            if (_lever != null)
            {
                _lever.SetCanInteract(alive &&
                    (f.Phase == FloorPhase.ContractSelection ||
                     (f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0) ||
                     (f.Phase == FloorPhase.Decision && f.SpinsRemaining > 0)));
            }

            if (_powerTank != null)
                _powerTank.SetCanInteract(alive && f.Phase == FloorPhase.Decision && f.CanBank);
        }

        private void OnContractPanelPressed()
        {
            FloorSession f = _run?.Session?.Current;
            var choices = f?.Plan.ContractChoices;
            if (choices == null || choices.Length == 0) return;
            _previewIndex = (_previewIndex + 1) % choices.Length;
        }

        private void OnLeverPulled()
        {
            RunSession run = _run?.Session;
            FloorSession f = run?.Current;
            if (run == null || f == null) return;

            if (f.Phase == FloorPhase.ContractSelection)
            {
                run.SelectContract(_previewIndex);
                _previewIndex = 0;
                return;   // 계약 확정과 첫 스핀을 한 번에 하지 않는다. 계약을 보고 나서 당기게 한다.
            }

            if (f.Phase == FloorPhase.Decision)
            {
                // 확정 가능한 상태에서 레버를 당기는 것은 "한 번 더"라는 뜻이다.
                // 판돈이 붙으므로 PushYourLuck을 거쳐야 한다.
                if (f.CanBank && !run.PushYourLuck()) return;
            }

            if (f.SpinsRemaining > 0) run.Spin();

            if (f.Phase == FloorPhase.Spinning && f.SpinsRemaining == 0)
                run.ForceResolve();
        }

        private void OnPowerTankPressed()
        {
            RunSession run = _run?.Session;
            if (run?.Current == null) return;
            if (run.Current.CanBank) run.Bank();
            _previewIndex = 0;
        }
    }
}
