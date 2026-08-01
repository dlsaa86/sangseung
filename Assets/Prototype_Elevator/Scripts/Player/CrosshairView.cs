using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ascend.Prototype.Diagnostics;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// View-only crosshair UI. Layout and object creation stay in the scene; this component only
    /// updates the inspector-provided graphic and prompt text.
    /// </summary>
    public sealed class CrosshairView : MonoBehaviour
    {
        // 둘 다 대체 경로가 없다. `SetTarget` 이 `if (x != null)` 로 감싸고 있어서
        // 비어 있으면 예외도 로그도 없이 **아무 일도 일어나지 않는다** — 조준해도
        // 조준점 색이 안 변하고 프롬프트가 안 뜬다. 정확히 「조용한 실패」다.
        [Header("References")]
        [RequiredReference("조준점 Graphic 이 없으면 상호작용 가능 여부가 색으로 전달되지 않는다")]
        [SerializeField] private Graphic _crosshairGraphic;

        [RequiredReference("프롬프트 TMP_Text 가 없으면 '무엇을 할 수 있는가'가 화면에 나오지 않는다")]
        [SerializeField] private TMP_Text _promptText;

        [Header("Colours")]
        [SerializeField] private Color _idleColor = Color.white;
        [SerializeField] private Color _availableColor = new Color(0.35f, 1f, 0.65f, 1f);
        [SerializeField] private Color _unavailableColor = new Color(0.65f, 0.65f, 0.65f, 1f);

        public bool HasCrosshairGraphic => _crosshairGraphic != null;
        public bool HasPromptText => _promptText != null;

        private void Awake()
        {
            SetTarget(null, false);
        }

        /// <summary>Updates the prompt and visual state for the current aim target.</summary>
        public void SetTarget(IInteractable target, bool canInteract)
        {
            if (_promptText != null)
                _promptText.text = target != null ? target.Prompt : string.Empty;

            if (_crosshairGraphic != null)
                _crosshairGraphic.color = target == null
                    ? _idleColor
                    : (canInteract ? _availableColor : _unavailableColor);
        }

        /// <summary>Clears the current target without requiring a scene reference.</summary>
        public void ClearTarget()
        {
            SetTarget(null, false);
        }
    }
}
