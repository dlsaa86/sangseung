using UnityEngine;
using UnityEngine.Events;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 과수확 레버. `MASTER_PRD.md` §7이 "대표 장면"으로 지정한 물체다.
    ///
    /// 일반 실행 레버와 **다른 물체**여야 하는 이유: 확정과 과수확은 서로 반대되는 선택인데
    /// 같은 레버가 상황에 따라 둘 다 하면 플레이어는 자기가 무엇을 고르는지 모른 채 당긴다.
    /// 그건 도박이 아니라 사고다.
    ///
    /// 잠금은 시늉이 아니다. 요구 전력 미달이면 덮개가 닫혀 있고 콜라이더도 꺼져 조준 자체가
    /// 걸리지 않는다(`VISUAL_SPEC.md` §7 "요구 전력 달성 전에는 물리적으로 잠겨 보인다").
    /// 잠긴 채로 눌리는 것처럼 보이면 그건 거짓 정보다.
    ///
    /// 덮개 각도·색·발광은 전부 인스펙터 값이다. 최종 비주얼은 승인 대기 항목이므로
    /// 여기에 잠그지 않는다.
    /// </summary>
    public sealed class InteractableOverharvestLever : MonoBehaviour, IInteractable
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

        /// <summary>요구 전력을 달성해 잠금이 풀렸는가. 브리지가 매 프레임 밀어준다.</summary>
        public bool IsUnlocked => _unlocked;

        /// <summary>덮개가 완전히 열렸는가. 연출·캡처가 "접근 가능해진 순간"을 잡을 때 쓴다.</summary>
        public bool IsCoverOpen => _coverAmount > 0.98f;

        /// <summary>잠금이 방금 풀렸다 — 조명·소리를 집중시킬 한 프레임(`VISUAL_SPEC.md` §7).</summary>
        public event System.Action Unlocked;

        public string Prompt => _unlocked ? "과수확 — 판돈을 걸고 한 번 더" : "잠김 — 요구 전력 미달";

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
            _handleAmount = Mathf.MoveTowards(_handleAmount, 0f, Time.deltaTime * _handleReturnSpeed);

            ApplyCover();
            ApplyHandle();
            ApplyLockLight();

            // 조준이 걸리는 것 자체를 잠근다. 덮개 뒤의 손잡이를 클릭할 수 있으면
            // 잠겨 있다는 표현이 거짓말이 된다.
            if (_grip != null && _grip.enabled != CanInteract) _grip.enabled = CanInteract;

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
