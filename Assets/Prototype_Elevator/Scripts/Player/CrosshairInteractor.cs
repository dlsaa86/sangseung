using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// Finds and activates interactables in the centre of the view. The sphere cast deliberately
    /// gives the player a forgiving aim volume instead of requiring pixel-perfect clicks.
    /// </summary>
    public sealed class CrosshairInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _viewCamera;
        [SerializeField] private CrosshairView _view;

        [Header("Aim")]
        [SerializeField] private LayerMask _interactionLayers = ~0;
        [SerializeField, Min(0.01f)] private float _sphereCastRadius = 0.18f;
        [SerializeField, Min(0.1f)] private float _interactionDistance = 5f;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Runtime highlight")]
        [SerializeField] private Color _availableHighlight = new Color(0.25f, 1f, 0.55f, 1f);
        [SerializeField] private Color _unavailableHighlight = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField, Range(0f, 1f)] private float _highlightBlend = 0.65f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly List<RendererState> _highlightedRenderers = new List<RendererState>();

        private IInteractable _currentInteractable;
        private bool _currentCanInteract;

        /// <summary>Object currently under the crosshair, if any.</summary>
        public IInteractable CurrentInteractable => _currentInteractable;

        /// <summary>Serialized camera reference, used by the setup validator.</summary>
        public bool HasCameraReference => _viewCamera != null;

        /// <summary>Serialized UI reference, used by the setup validator.</summary>
        public bool HasViewReference => _view != null;

        private sealed class RendererState
        {
            public Renderer Renderer;
            public MaterialPropertyBlock OriginalBlock;
        }

        private void Awake()
        {
            if (_viewCamera == null)
                _viewCamera = GetComponentInParent<Camera>();

            if (_viewCamera == null)
                _viewCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (_viewCamera == null)
                _viewCamera = Camera.main;

            IInteractable target = FindTarget();
            SetCurrentTarget(target);

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                _currentInteractable != null && _currentInteractable.CanInteract)
            {
                _currentInteractable.Interact(gameObject);
            }
        }

        private IInteractable FindTarget()
        {
            if (_viewCamera == null)
                return null;

            Ray ray = new Ray(_viewCamera.transform.position, _viewCamera.transform.forward);
            int hitCount = Physics.SphereCastNonAlloc(
                ray,
                Mathf.Max(0.01f, _sphereCastRadius),
                _hits,
                Mathf.Max(0.1f, _interactionDistance),
                _interactionLayers,
                _triggerInteraction);

            float closestDistance = float.PositiveInfinity;
            IInteractable closest = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null || hit.distance >= closestDistance)
                    continue;

                IInteractable candidate = FindInteractable(hit.collider);
                if (candidate == null)
                    continue;

                closestDistance = hit.distance;
                closest = candidate;
            }

            return closest;
        }

        private static IInteractable FindInteractable(Collider collider)
        {
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInteractable interactable)
                    return interactable;
            }

            return null;
        }

        private void SetCurrentTarget(IInteractable target)
        {
            bool canInteract = target != null && target.CanInteract;
            if (ReferenceEquals(target, _currentInteractable) && canInteract == _currentCanInteract)
                return;

            ClearHighlight();
            _currentInteractable = target;
            _currentCanInteract = canInteract;

            if (target != null)
                ApplyHighlight(target, canInteract);

            if (_view != null)
                _view.SetTarget(target, canInteract);
        }

        private void ApplyHighlight(IInteractable target, bool canInteract)
        {
            Component component = target as Component;
            if (component == null)
                return;

            Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true);
            Color highlightColor = canInteract ? _availableHighlight : _unavailableHighlight;
            float blend = Mathf.Clamp01(_highlightBlend);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                MaterialPropertyBlock original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);

                Material material = renderer.sharedMaterial;
                if (material != null)
                {
                    SetBlendedColor(material, block, "_BaseColor", highlightColor, blend);
                    SetBlendedColor(material, block, "_Color", highlightColor, blend);
                    if (material.HasProperty("_EmissionColor"))
                        block.SetColor("_EmissionColor", highlightColor * (canInteract ? 0.35f : 0.08f));
                }

                renderer.SetPropertyBlock(block);
                _highlightedRenderers.Add(new RendererState
                {
                    Renderer = renderer,
                    OriginalBlock = original
                });
            }
        }

        private static void SetBlendedColor(
            Material material,
            MaterialPropertyBlock block,
            string propertyName,
            Color highlightColor,
            float blend)
        {
            if (!material.HasProperty(propertyName))
                return;

            Color source = material.GetColor(propertyName);
            if (block.HasColor(propertyName))
                source = block.GetColor(propertyName);
            block.SetColor(propertyName, Color.Lerp(source, highlightColor, blend));
        }

        private void ClearHighlight()
        {
            for (int i = 0; i < _highlightedRenderers.Count; i++)
            {
                RendererState state = _highlightedRenderers[i];
                if (state.Renderer != null)
                    state.Renderer.SetPropertyBlock(state.OriginalBlock);
            }

            _highlightedRenderers.Clear();
        }

        private void OnDisable()
        {
            ClearHighlight();
            _currentInteractable = null;
            _currentCanInteract = false;
            if (_view != null)
                _view.ClearTarget();
        }
    }
}
