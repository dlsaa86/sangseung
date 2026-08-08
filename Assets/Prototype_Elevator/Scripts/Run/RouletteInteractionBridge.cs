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
    /// ⚠ **위 분담은 현재 구현이지 PRD 규격이 아니다.**
    ///
    /// 직전 판본의 이 주석은 「실행 레버가 추가 스핀을 겸하지 않는 것이 핵심이다
    /// (`MASTER_PRD.md` §7)」라고 적고 있었다. **§7 은 2026-08-02 에 정확히 그 반대로
    /// 확정됐다** — 「물리 레버는 하나다. 일반 스핀과 과수확이 같은 레버의 서로 다른
    /// 걸림점이다」(`MASTER_PRD.md` §7, `DECISION_LOG` 2026-08-02). 즉 그 주석은
    /// 미구현을 **뒤집힌 근거로 정당화**하고 있었다. 근거 없는 구현보다 나쁜 것은
    /// 없는 근거를 인용하는 구현이다.
    ///
    /// 아직 없는 것 (`docs/runtime/PENDING_DECISIONS.md` 로 올린다):
    ///   · 1단/2단 걸림점을 가진 하나의 레버
    ///   · 과수확 실행의 0.7~1.0초 유지 입력
    ///   · 목표 달성 후 1단만 당겼을 때 「스핀하지 않고 선택 정보 재표시」
    ///
    /// 지금의 두 레버는 그 규격의 **플레이스홀더**다. 기능(확정 vs 과수확이 서로 다른
    /// 입력이라는 것)은 성립하고, 물리적 형태만 §7 과 다르다.
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
            // 실행 레버를 **길게** = 과수확 (2026-08-08). 아래 `_overharvestLever` 구독도
            // 남겨 둔다 — 그 레버는 덮개 연출을 계속 담당하고, 언젠가 다시 조준 대상이
            // 되더라도 같은 곳으로 들어온다. 두 입구가 같은 함수를 부르므로 갈라지지 않는다.
            if (_lever != null) _lever.onHeld.AddListener(OnOverharvestPulled);
            if (_contractPanel != null) _contractPanel.onOpened.AddListener(OnContractPanelPressed);
            if (_powerTank != null) _powerTank.onBanked.AddListener(OnPowerTankPressed);
            if (_overharvestLever != null) _overharvestLever.onPulled.AddListener(OnOverharvestPulled);

            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_lever != null) _lever.onPulled.RemoveListener(OnLeverPulled);
            if (_lever != null) _lever.onHeld.RemoveListener(OnOverharvestPulled);
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

            // Decision 단계의 선택은 **둘이어야 한다** — 세 번째 선택지가 보이면 두 선택의
            // 대등함이 깨진다 (visual-criteria B-4.12). 그 둘은 이제 이렇게 갈린다.
            //
            //     탱크를 **짧게**  = 확정 (안전)
            //     레버를 **길게**  = 과수확 (위험)
            //
            // 2026-08-08 사용자 결정으로 과수확을 실행 레버에 합쳤다. B-4.12 는 지켜진다 —
            // 선택은 여전히 둘이고, 세 번째 버튼이 늘어난 것이 아니라 **하나가 줄었다.**
            //
            // ⚠ 합치기 전 상태는 이지선다가 아니었다. `OverharvestLever` 는 콜라이더가
            // 없어 조준 자체가 불가능했고 `onPulled` 리스너도 0개였다 — Decision 에서
            // 누를 수 있는 것은 탱크 하나뿐이었고 **과수확은 도달 불가**였다.
            bool contractStep = alive && f.Phase == FloorPhase.ContractSelection;
            bool spinStep = alive && f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0;
            bool overharvestStep = alive && f.Phase == FloorPhase.Decision && f.CanTakeExtraSpin;
            _lever.SetCanInteract(contractStep || spinStep || overharvestStep);

            // 판정식을 레버 안에서 다시 쓰지 않는다. `CanTakeExtraSpin` 하나가 정본이다 —
            // 두 벌로 두면 한쪽만 고쳐도 컴파일이 통과한다(아래 `UpdateOverharvestLever` 주석).
            _lever.SetHoldAvailable(overharvestStep);

            int spinsRemaining = f != null ? f.SpinsRemaining : 0;
            int key = overharvestStep ? 2000 + Mathf.RoundToInt(f.PendingAnte)
                    : contractStep ? 1000 + _previewIndex
                    : spinsRemaining;
            if (key != _leverPromptKey)
            {
                _leverPromptKey = key;
                // 조건을 만족했다는 것을 **화면에서** 알려야 이 설계가 성립한다.
                // 「길게」라는 몸짓을 배울 곳이 프롬프트뿐이다.
                _lever.SetPrompt(overharvestStep
                    ? $"길게 눌러 과수확 — 판돈 {Mathf.RoundToInt(f.PendingAnte)}"
                    : contractStep
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

            // `CanTakeExtraSpin` 을 그대로 쓴다. 직전 판본은 `f.CanBank && SpinsRemaining > 0`
            // 을 손으로 다시 썼는데, 그 식에는 **`OverharvestProfile` 이 없다** —
            // `UnlockThreshold` 를 1.20 으로 올려도 덮개는 100% 에서 열렸고,
            // `MaxExtraSpins` 를 0 으로 내려도 열렸다. 그리고 열린 레버를 당기면
            // `PushYourLuck` 이 조용히 거부해 **아무 일도 일어나지 않았다.**
            //
            // 판정식을 두 벌로 두면 한쪽만 고쳐도 컴파일이 통과한다.
            // 이 저장소가 `AnteRatioForNextSpin` 에서 이미 배운 것이다(FloorSession 참조).
            bool unlocked = alive && f.Phase == FloorPhase.Decision && f.CanTakeExtraSpin;
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
            {
                DoSpin(run);
                return;
            }

            // Decision 에서 **짧게** 누른 경우. 여기서 조용히 반환하면 「지금은 안 된다」와
            // 「버튼이 고장났다」가 구분되지 않는다 — 둘 다 무반응이기 때문이다
            // (`InteractableLever.onBlocked` 주석이 기록한 그 문제).
            // 레버가 핀에 부딪혀 튕기면 「짧게로는 안 된다」가 손에 남고,
            // 프롬프트의 「길게 눌러」를 읽게 만든다.
            if (f.Phase == FloorPhase.Decision && f.CanTakeExtraSpin && _lever != null)
                _lever.onBlocked.Invoke();
        }

        private void OnOverharvestPulled()
        {
            RunSession run = _run != null ? _run.Session : null;
            FloorSession f = run != null ? run.Current : null;
            if (run == null || f == null || IsLocked) return;
            if (f.Phase != FloorPhase.Decision || !f.CanTakeExtraSpin) return;

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
