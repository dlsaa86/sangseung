using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 과수확 레버. `MASTER_PRD.md` §7이 "대표 장면"으로 지정한 물체다.
    ///
    /// ⚠ **이 물체는 §7 규격의 플레이스홀더다.**
    ///
    /// 직전 판본의 이 주석은 「일반 실행 레버와 **다른 물체**여야 하는 이유」를 적고
    /// `MASTER_PRD` §7 을 근거로 들었다. **§7 은 2026-08-02 에 정확히 그 반대로
    /// 확정됐다** — 「물리 레버는 하나다. 일반 스핀과 과수확이 같은 레버의 서로 다른
    /// 걸림점이다」. 미구현을 뒤집힌 근거로 정당화하고 있었다.
    ///
    /// 레버를 하나로 합치는 것은 씬 구조 변경이라 사용자 결정으로 올렸다
    /// (`PENDING_DECISIONS` P-20260804-01, 권장안 C).
    ///
    /// **다만 §7 의 핵심은 레버가 하나냐 둘이냐가 아니라 「몸짓」이다** —
    /// 「과수확은 두 번째 걸림점을 넘는 0.7~1.0초 **유지 입력**으로 실행한다.
    /// 한 번 당기는 것으로는 실행되지 않는다 — 그 유지 시간이 자발적 선택의 몸짓이다.」
    /// 그 절반은 물체를 합치지 않고도 구현할 수 있고, 그래서 구현했다
    /// (<see cref="IHoldInteractable"/>).
    ///
    /// 잠금은 시늉이 아니다. 요구 전력 미달이면 덮개가 닫혀 있고 콜라이더도 꺼져 조준 자체가
    /// 걸리지 않는다(`VISUAL_SPEC.md` §7 "요구 전력 달성 전에는 물리적으로 잠겨 보인다").
    /// 잠긴 채로 눌리는 것처럼 보이면 그건 거짓 정보다.
    ///
    /// 덮개 각도·색·발광은 전부 인스펙터 값이다. 최종 비주얼은 승인 대기 항목이므로
    /// 여기에 잠그지 않는다.
    /// </summary>
    public sealed class InteractableOverharvestLever : MonoBehaviour, IHoldInteractable
    {
        [Header("Interaction")]
        [SerializeField] private Collider _grip;
        [SerializeField] public UnityEvent onPulled = new UnityEvent();

        [Header("Cover — 잠금의 물리적 표현")]
        [SerializeField] private Transform _coverPivot;
        [SerializeField] private float _closedAngle;
        [SerializeField] private float _openAngle = -105f;
        [SerializeField, Min(0.1f)] private float _coverSpeed = 3.5f;

        [Header("Lock light — 붉은색 하나로 위험을 표현하지 않는다 (VISUAL_SPEC §7)")]
        [SerializeField] private Renderer _lockLight;
        [SerializeField] private Color _lockedColor = new Color(0.16f, 0.16f, 0.18f);
        [SerializeField] private Color _armedColor = new Color(1f, 0.72f, 0.15f);
        [SerializeField, Min(0f)] private float _armedEmission = 2.4f;

        [Header("Hold — §7 「0.7~1.0초 유지 입력」")]
        [Tooltip("실행까지 버튼을 유지해야 하는 시간(초). MASTER_PRD §7 규격은 0.7~1.0 이다. " +
                 "0 으로 두면 즉시 실행되어 §7 의 「몸짓」이 사라진다.")]
        [SerializeField, Range(0f, 2f)] private float _holdSeconds = 0.85f;

        [Header("Handle")]
        [SerializeField] private Transform _handlePivot;
        [SerializeField] private float _handleRestAngle = 18f;
        [SerializeField] private float _handlePulledAngle = -52f;
        [SerializeField, Min(0.1f)] private float _handleReturnSpeed = 2.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _block;
        private bool _unlocked;
        private float _coverAmount;      // 0 = 닫힘, 1 = 열림
        private float _handleAmount;     // 0 = 원위치, 1 = 당겨짐
        private bool _wasUnlocked;

        /// <summary>
        /// 유지 중인가. 참이면 손잡이가 **스스로 돌아가지 않는다** —
        /// 진행도가 곧 손잡이 각도이고, 그것이 §7 이 요구하는 시각 피드백이다.
        /// </summary>
        private bool _holding;

        /// <summary>직전 프레임의 유지 진행도. 취소 연출이 어디서부터 되돌아올지 정한다.</summary>
        private float _holdProgress;

        /// <summary>요구 전력을 달성해 잠금이 풀렸는가. 브리지가 매 프레임 밀어준다.</summary>
        public bool IsUnlocked => _unlocked;

        /// <summary>덮개가 완전히 열렸는가. 연출·캡처가 "접근 가능해진 순간"을 잡을 때 쓴다.</summary>
        public bool IsCoverOpen => _coverAmount > 0.98f;

        /// <summary>잠금이 방금 풀렸다 — 조명·소리를 집중시킬 한 프레임(`VISUAL_SPEC.md` §7).</summary>
        public event System.Action Unlocked;

        public string Prompt => _unlocked
            ? (_holdSeconds > 0f ? "과수확 — 길게 눌러 강행" : "과수확 — 판돈을 걸고 한 번 더")
            : "잠김 — 요구 전력 미달";

        /// <summary>유지 진행도 0~1. HUD 가 링·게이지로 그릴 근거다.</summary>
        public float HoldProgress => _holdProgress;

        public float HoldSeconds => _holdSeconds;

        /// <summary>
        /// 진행도가 곧 손잡이 각도다. 별도 게이지를 띄우지 않는 이유 —
        /// §7 은 「강한 금속 걸쇠 피드백」을 요구하지 UI 링을 요구하지 않는다.
        /// 레버가 실제로 내려가는 것이 가장 읽히는 진행 표시다.
        /// </summary>
        public void OnHoldProgress(float normalized)
        {
            _holding = true;
            _holdProgress = Mathf.Clamp01(normalized);
            _handleAmount = _holdProgress;
            // **같은 프레임에 바른다.** `Update` 에 맡기면 입력과 각도 사이에 한 프레임이
            // 끼고, 그 한 프레임이 「손잡이가 손을 따라온다」는 감각을 지운다.
            // (검사도 이 즉시성에 기댄다 — EditMode 에서는 `Update` 가 돌지 않는다.)
            ApplyHandle();
        }

        /// <summary>
        /// 손을 뗐다. 손잡이가 **되돌아간다** — 그 되돌아감이 「아직 안 걸었다」를 말한다.
        /// 진행도만 0 으로 만들고 각도를 그대로 두면 걸린 것처럼 보인다.
        /// </summary>
        public void OnHoldCancelled()
        {
            _holding = false;
            _holdProgress = 0f;
        }

        // 덮개가 열리는 중에는 아직 못 당긴다. 잠금 해제가 사건으로 보여야 하는데
        // 해제와 동시에 눌리면 그 사건이 한 프레임 만에 사라진다.
        public bool CanInteract => _unlocked && IsCoverOpen;

        /// <summary>브리지가 게임 상태에서 끌어와 밀어준다. 레버는 상태를 스스로 알지 못한다.</summary>
        public void SetUnlocked(bool unlocked)
        {
            if (_unlocked == unlocked) return;
            _unlocked = unlocked;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            _holding = false;
            _holdProgress = 0f;
            _handleAmount = 1f;
            onPulled.Invoke();
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _coverAmount = 0f;
            _wasUnlocked = false;
            ApplyCover();
            ApplyLockLight();
            if (_grip != null) _grip.enabled = false;
        }

        private void Update()
        {
            float target = _unlocked ? 1f : 0f;
            _coverAmount = Mathf.MoveTowards(_coverAmount, target, Time.deltaTime * _coverSpeed);
            // 유지 중에는 손잡이가 스스로 돌아가지 않는다. 돌아가면 진행도와 각도가
            // 어긋나 「얼마나 더 눌러야 하는가」를 손잡이로 읽을 수 없게 된다.
            if (!_holding)
                _handleAmount = Mathf.MoveTowards(_handleAmount, 0f, Time.deltaTime * _handleReturnSpeed);

            ApplyCover();
            ApplyHandle();
            ApplyLockLight();

            // 조준이 걸리는 것 자체를 잠근다. 덮개 뒤의 손잡이를 클릭할 수 있으면
            // 잠겨 있다는 표현이 거짓말이 된다.
            if (_grip != null && _grip.enabled != CanInteract) _grip.enabled = CanInteract;

            // 잠금이 풀렸다가 다시 잠기면 유지도 무효다. 안 그러면 잠긴 레버가
            // 진행도를 들고 있다가 다음 해제 순간에 그 자리에서 이어진다.
            if (!CanInteract && _holding) OnHoldCancelled();

            if (_unlocked && !_wasUnlocked)
            {
                _wasUnlocked = true;
                Unlocked?.Invoke();
            }
            else if (!_unlocked && _wasUnlocked)
            {
                _wasUnlocked = false;
            }
        }

        private void ApplyCover()
        {
            if (_coverPivot == null) return;
            float angle = Mathf.Lerp(_closedAngle, _openAngle, Smooth(_coverAmount));
            _coverPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        private void ApplyHandle()
        {
            if (_handlePivot == null) return;
            float angle = Mathf.Lerp(_handleRestAngle, _handlePulledAngle, Smooth(_handleAmount));
            _handlePivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        private void ApplyLockLight()
        {
            if (_lockLight == null) return;

            // 🔴 **널 가드.** `_block` 은 직렬화되지 않는 필드라 **도메인 리로드 뒤
            //    null 이 된다** — 컴포넌트는 이미 `Awake` 를 지났으므로 다시 만들
            //    기회가 없고, `Update` 는 계속 돈다. 그래서 `GetPropertyBlock(null)` 이
            //    매 프레임 던졌다.
            //
            //    실측: 에디터 로그가 25,000 → 712,000 줄로 불어 있었고 그중
            //    **`ArgumentNullException` 이 6,732 건**이었다. 예외가 여기서 던져지니
            //    그 아래 두 줄(`SetColor` · `SetPropertyBlock`)이 실행되지 않아
            //    **잠금등이 옛 색에 얼어붙는다** — 사용자가 「레버에 예전에 고쳤던 빛
            //    표현이 여전히 남아 있다」고 본 것이 이것이다. 고친 값이 안 먹은 게
            //    아니라 **적용하는 줄에 닿지 못하고 있었다.**
            if (_block == null) _block = new MaterialPropertyBlock();

            // 잠김/해제를 색만으로 구분하지 않는다 — 발광 여부가 함께 바뀐다.
            // 회색조로 봐도 켜짐/꺼짐이 남아야 한다(visual-criteria B-2.5의 정신).
            Color tint = Color.Lerp(_lockedColor, _armedColor, _coverAmount);
            _lockLight.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, tint);
            _block.SetColor(EmissionColorId, _armedColor * (_armedEmission * _coverAmount));
            _lockLight.SetPropertyBlock(_block);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
