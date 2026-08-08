using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 실행 레버. **짧게 = 일반 행동, 길게 = 과수확** (2026-08-08 사용자 결정).
    ///
    /// ## 왜 레버가 하나인가
    ///
    /// 「과수확은 조건이 있는 행동인데 별도 레버로 분리하면 플레이어가 그 조건을 배울
    /// 방법이 없다. 일반 레버로만 동작하다가 조건이 되면 『길게 눌러 과수확』으로 유도하라.」
    ///
    /// 합치기 전 상태는 이지선다가 **아니었다.** 실측하면 `OverharvestLever` 는
    /// 콜라이더가 없어 조준 자체가 불가능했고 `onPulled` 리스너도 0개였다 —
    /// Decision 에서 실제로 누를 수 있는 것은 전력 탱크 하나뿐이었고 과수확은
    /// **도달할 수 없는 선택지**였다. 계약은 `ITapAndHoldInteractable` 에 있다.
    /// </summary>
    public sealed class InteractableLever : MonoBehaviour, ITapAndHoldInteractable
    {
        [SerializeField] private bool _canInteract = true;
        [SerializeField] public UnityEvent onPulled = new UnityEvent();

        /// <summary>
        /// 당길 수 **없는** 상태에서 당기려 했을 때.
        ///
        /// 이것이 없으면 `Interact()` 가 조용히 반환해 **화면에서 아무 일도
        /// 일어나지 않는다.** 플레이어에게 「지금은 안 된다」와 「버튼이 고장났다」는
        /// 구분되지 않는다 — 둘 다 무반응이기 때문이다. 잠긴 레버는 핀에 부딪혀
        /// 튕겨야 왜 안 되는지가 화면 안에 남는다.
        /// </summary>
        [SerializeField] public UnityEvent onBlocked = new UnityEvent();

        [Header("유지 입력 (과수확)")]
        [Tooltip("유지를 완성했을 때. 브리지가 과수확에 연결한다.")]
        [SerializeField] public UnityEvent onHeld = new UnityEvent();

        [Tooltip("유지가 시작됐을 때. 걸쇠 소리·덮개 열림 같은 연출용.")]
        [SerializeField] public UnityEvent onHoldBegan = new UnityEvent();

        [Tooltip("완성 전에 떼거나 조준이 벗어났을 때.")]
        [SerializeField] public UnityEvent onHoldCancelled = new UnityEvent();

        /// <summary>
        /// `MASTER_PRD` §7 규격은 0.7~1.0 초다. 0.2 로 내리면 탭과 구별되지 않고
        /// 2.0 으로 올리면 조작이 굼떠진다. `HoldInputTests` 가 이 대역을 지킨다.
        /// </summary>
        [SerializeField, Range(0.7f, 1.0f)] private float _holdSeconds = 0.85f;

        [SerializeField] private string _prompt = "레버 당기기";

        /// <summary>조준 시 뜨는 문구. 상태에 따라 브리지가 갈아끼운다 —
        /// 상황과 다른 문구가 떠 있으면 그건 거짓 정보다.</summary>
        public string Prompt => _prompt;

        public void SetPrompt(string prompt)
        {
            if (!string.IsNullOrEmpty(prompt)) _prompt = prompt;
        }

        public bool CanInteract => _canInteract;

        public void SetCanInteract(bool canInteract)
        {
            _canInteract = canInteract;
            if (!_canInteract && _holding) OnHoldCancelled();
        }

        // ── 유지 입력 ─────────────────────────────────────────────────────────

        private bool _holdAvailable;
        private bool _holding;

        /// <summary>0~1. 연출이 읽는다 — 레버가 내려가는 정도가 곧 진행 표시다.</summary>
        public float HoldProgress { get; private set; }

        public float HoldSeconds => _holdSeconds;

        /// <summary>
        /// 브리지가 매 프레임 갱신한다. 이 값이 곧 「지금 과수확할 수 있는가」다.
        /// **판정식을 여기서 다시 쓰지 않는다** — 브리지가 `FloorSession.CanTakeExtraSpin`
        /// 을 그대로 넘긴다. 두 벌로 두면 한쪽만 고쳐도 컴파일이 통과한다는 것을
        /// 이 저장소는 이미 배웠다(`RouletteInteractionBridge.UpdateOverharvestLever` 주석).
        /// </summary>
        public void SetHoldAvailable(bool available)
        {
            if (_holdAvailable == available) return;
            _holdAvailable = available;
            if (!available && _holding) OnHoldCancelled();
        }

        public bool HoldAvailable => _canInteract && _holdAvailable;

        public void OnHoldProgress(float normalized)
        {
            if (!_holding)
            {
                _holding = true;
                onHoldBegan.Invoke();
            }
            HoldProgress = Mathf.Clamp01(normalized);
        }

        public void OnHoldCancelled()
        {
            if (!_holding) { HoldProgress = 0f; return; }
            _holding = false;
            HoldProgress = 0f;
            onHoldCancelled.Invoke();
        }

        public void OnHoldCompleted()
        {
            _holding = false;
            HoldProgress = 0f;
            if (!_canInteract) { onBlocked.Invoke(); return; }
            onHeld.Invoke();
        }

        // ── 짧은 행동 ─────────────────────────────────────────────────────────

        public void Interact(GameObject interactor)
        {
            if (!_canInteract)
            {
                onBlocked.Invoke();
                return;
            }

            onPulled.Invoke();
        }
    }
}
