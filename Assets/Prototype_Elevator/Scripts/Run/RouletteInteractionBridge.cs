using UnityEngine;
using Ascend.Prototype.Diagnostics;
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
    /// 조작 분담 — 한 물체가 한 가지 뜻만 갖는다:
    ///
    ///   계약 패널   — 계약을 넘겨 본다. 확정하지 않는다.
    ///   실행 레버   — 계약을 확정하고, 이후에는 일반 스핀을 돌린다.
    ///   전력 탱크   — 확정. 층을 끝낸다.
    ///   과수확 레버 — 판돈을 걸고 한 번 더. 요구 전력 100% 전에는 덮개가 닫혀 있다.
    ///
    /// 실행 레버가 추가 스핀을 겸하지 않는 것이 핵심이다(`MASTER_PRD.md` §7).
    /// 겸하면 "확정 vs 과수확"이 같은 물체의 다른 타이밍이 되어, 플레이어가 무엇을
    /// 고르는지 모른 채 당기게 된다. 그건 도박이 아니라 사고다.
    /// </summary>
    public sealed class RouletteInteractionBridge : MonoBehaviour
    {
        // `_run` 은 필수로 표시하지 않는다 — `Awake` 에 GetComponent → FindAnyObjectByType
        // 대체 경로가 있어서 비어 있어도 스스로 채운다. 아래 넷은 대체 경로가 없고,
        // 비면 `if (x != null) 구독` 이 통째로 건너뛰어 **조용히 아무 일도 일어나지 않는다.**
        [SerializeField] private RunSessionBehaviour _run;

        [RequiredReference("실행 레버가 없으면 스핀을 돌릴 수 없어 층이 진행되지 않는다")]
        [SerializeField] private InteractableLever _lever;

        [RequiredReference("계약 패널이 없으면 계약을 고를 수 없다")]
        [SerializeField] private InteractableContractPanel _contractPanel;

        [RequiredReference("전력 탱크가 없으면 전력을 저장할 수 없다")]
        [SerializeField] private InteractablePowerTank _powerTank;

        [RequiredReference("과수확 레버가 없으면 추가 스핀 경로가 사라진다")]
        [SerializeField] private InteractableOverharvestLever _overharvestLever;

        [Tooltip("스핀 결과 연출자. 비어 있으면 결과가 즉시 반영된다(연출 없이도 게임은 돈다).")]
        [SerializeField] private MonoBehaviour _presentationSource;

        private ISpinPresentation _presentation;
        private int _previewIndex;

        // 프롬프트는 매 프레임 같은 문자열을 다시 만들기 쉽다. 세 오브젝트 × 60fps 면
        // 초당 180개의 쓰레기가 된다. 상태 키가 바뀔 때만 문자열을 짓는다.
        private int _leverPromptKey = int.MinValue;
        private int _tankPromptKey = int.MinValue;
        private int _panelPromptKey = int.MinValue;

        /// <summary>계약 패널이 지금 가리키고 있는 선택지. HUD가 읽어서 표시한다.</summary>
        public int PreviewIndex => _previewIndex;

        /// <summary>연출 재생 중이라 입력이 잠긴 상태인가.</summary>
        public bool IsLocked => _presentation != null && _presentation.IsPresenting;

        /// <summary>미리보기 중인 계약. 선택지가 없으면 계약 없음.</summary>
        public ResistanceContract PreviewContract
        {
            get
            {
                FloorSession f = CurrentFloor;
                var choices = f?.Plan.ContractChoices;
                if (choices == null || choices.Length == 0) return ResistanceContract.None;
                return choices[Mathf.Clamp(_previewIndex, 0, choices.Length - 1)];
            }
        }

        private FloorSession CurrentFloor =>
            _run != null && _run.Session != null ? _run.Session.Current : null;

        private void Awake()
        {
            if (_run == null) _run = GetComponent<RunSessionBehaviour>();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();

            _presentation = _presentationSource as ISpinPresentation;
            if (_presentation == null && _presentationSource != null)
            {
                // 조용히 무시하면 "연출이 왜 안 나오지"로 몇 시간을 태운다.
                Debug.LogError($"[상승] {name}: {_presentationSource.GetType().Name}은 " +
                               "ISpinPresentation을 구현하지 않는다. 연출 없이 진행한다.", this);
                _presentationSource = null;
            }
            if (_presentation == null) _presentation = GetComponent<ISpinPresentation>();

            if (_lever != null) _lever.onPulled.AddListener(OnLeverPulled);
            if (_contractPanel != null) _contractPanel.onOpened.AddListener(OnContractPanelPressed);
            if (_powerTank != null) _powerTank.onBanked.AddListener(OnPowerTankPressed);
            if (_overharvestLever != null) _overharvestLever.onPulled.AddListener(OnOverharvestPulled);

            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_lever != null) _lever.onPulled.RemoveListener(OnLeverPulled);
            if (_contractPanel != null) _contractPanel.onOpened.RemoveListener(OnContractPanelPressed);
            if (_powerTank != null) _powerTank.onBanked.RemoveListener(OnPowerTankPressed);
            if (_overharvestLever != null) _overharvestLever.onPulled.RemoveListener(OnOverharvestPulled);
            if (_run != null) _run.RunStarted -= OnRunStarted;
        }

        private void OnRunStarted(RunSession session)
        {
            _previewIndex = 0;
            _presentation?.Clear();
        }

        private void Update()
        {
            // 상호작용 가능 여부는 매 프레임 상태에서 끌어온다. 스텁이 상태를 모르므로
            // 여기서 밀어주지 않으면 항상 눌리는 것처럼 보인다 — 그건 거짓 정보다.
            RunSession run = _run != null ? _run.Session : null;
            FloorSession f = run != null ? run.Current : null;
            bool alive = f != null && !run.IsComplete && !run.IsFailed && !IsLocked;

            UpdateContractPanel(f, alive);
            UpdateExecutionLever(f, alive);
            UpdatePowerTank(f, alive);
            UpdateOverharvestLever(f, alive);
        }

        private void UpdateContractPanel(FloorSession f, bool alive)
        {
            if (_contractPanel == null) return;
            var choices = f?.Plan.ContractChoices;
            bool usable = alive && f.Phase == FloorPhase.ContractSelection &&
                          choices != null && choices.Length > 1;
            _contractPanel.SetCanInteract(usable);

            int key = usable ? 1 + _previewIndex * 2 : 0;
            if (key != _panelPromptKey)
            {
                _panelPromptKey = key;
                _contractPanel.SetPrompt(usable
                    ? $"계약 넘기기 — {PreviewContract.Label}"
                    : "계약 패널");
            }
        }

        private void UpdateExecutionLever(FloorSession f, bool alive)
        {
            if (_lever == null) return;

            // Decision 단계에서는 실행 레버가 죽는다. 그 순간의 선택은 탱크와 과수확 레버
            // 둘뿐이어야 한다 — 세 번째 선택지가 보이면 두 선택의 대등함이 깨진다
            // (visual-criteria B-4.12).
            bool contractStep = alive && f.Phase == FloorPhase.ContractSelection;
            bool spinStep = alive && f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0;
            _lever.SetCanInteract(contractStep || spinStep);

            int spinsRemaining = f != null ? f.SpinsRemaining : 0;
            int key = contractStep ? 1000 + _previewIndex : spinsRemaining;
            if (key != _leverPromptKey)
            {
                _leverPromptKey = key;
                _lever.SetPrompt(contractStep
                    ? $"{PreviewContract.Label} 확정"
                    : $"실행 레버 — 스핀 {spinsRemaining}회 남음");
            }
        }

        private void UpdatePowerTank(FloorSession f, bool alive)
        {
            if (_powerTank == null) return;

            bool canBank = alive && f.Phase == FloorPhase.Decision && f.CanBank;
            // 스핀을 다 쓰고도 요구 전력을 못 넘긴 경우에도 층을 끝낼 방법이 있어야 한다.
            // 없으면 플레이어가 진행 불가 상태에 갇힌다(DoD "진행 불가 상태 없음").
            bool mustResolve = alive && f.Phase == FloorPhase.Decision &&
                               !f.CanBank && f.SpinsRemaining == 0;

            _powerTank.SetCanInteract(canBank || mustResolve);

            int key = canBank ? 100 + (int)f.CurrentBand : mustResolve ? 1 : 0;
            if (key != _tankPromptKey)
            {
                _tankPromptKey = key;
                _powerTank.SetPrompt(canBank
                    ? $"전력 확정 — {f.CurrentBand.DisplayName()}"
                    : mustResolve ? "결과 확인 — 요구 전력 미달" : "전력 탱크");
            }
        }

        private void UpdateOverharvestLever(FloorSession f, bool alive)
        {
            if (_overharvestLever == null) return;

            // 잠금 해제 조건은 오직 "요구 전력 100% 달성"이다(MASTER_PRD §7).
            // 남은 스핀이 없으면 걸 것이 없으므로 잠긴 채로 둔다.
            bool unlocked = alive && f.Phase == FloorPhase.Decision &&
                            f.CanBank && f.SpinsRemaining > 0;
            _overharvestLever.SetUnlocked(unlocked);
        }

        private void OnContractPanelPressed()
        {
            FloorSession f = CurrentFloor;
            var choices = f?.Plan.ContractChoices;
            if (choices == null || choices.Length == 0) return;
            _previewIndex = (_previewIndex + 1) % choices.Length;
        }

        private void OnLeverPulled()
        {
            RunSession run = _run != null ? _run.Session : null;
            FloorSession f = run != null ? run.Current : null;
            if (run == null || f == null || IsLocked) return;

            if (f.Phase == FloorPhase.ContractSelection)
            {
                // 계약 확정과 첫 스핀을 한 번에 하지 않는다. 무엇을 걸었는지 보고 나서 당기게 한다.
                run.SelectContract(_previewIndex);
                return;
            }

            if (f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0)
                DoSpin(run);
        }

        private void OnOverharvestPulled()
        {
            RunSession run = _run != null ? _run.Session : null;
            FloorSession f = run != null ? run.Current : null;
            if (run == null || f == null || IsLocked) return;
            if (f.Phase != FloorPhase.Decision || !f.CanBank || f.SpinsRemaining <= 0) return;

            // 판돈은 스핀 전에 빠진다. 결과를 보고 무를 수 없다는 것이 이 선택의 전부다.
            if (!run.PushYourLuck()) return;
            DoSpin(run);
        }

        private void DoSpin(RunSession run)
        {
            SpinResolution resolution = run.Spin();
            if (resolution.Steps == null) return;   // 상태 게이트에 막힌 호출
            _presentation?.Present(resolution);
        }

        private void OnPowerTankPressed()
        {
            RunSession run = _run != null ? _run.Session : null;
            FloorSession f = run != null ? run.Current : null;
            if (run == null || f == null || IsLocked) return;

            if (f.CanBank) run.Bank();
            else if (f.SpinsRemaining == 0) run.ForceResolve();
            _previewIndex = 0;
        }
    }
}
