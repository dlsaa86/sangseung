using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// View-only crosshair UI. Layout and object creation stay in the scene; this component only
    /// updates the inspector-provided graphic and prompt text.
    /// </summary>
    public sealed class CrosshairView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Graphic _crosshairGraphic;
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
