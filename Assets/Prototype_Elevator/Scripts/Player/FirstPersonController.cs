using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// Small first-person controller for the elevator graybox.
    ///
    /// Cursor capture is opt-in at startup and is also released when focus is lost. This keeps
    /// editor and automated capture sessions recoverable with Escape or by clicking elsewhere.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Camera _viewCamera;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _moveSpeed = 2.2f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField, Range(0f, 89f)] private float _slopeLimit = 45f;
        [SerializeField, Min(0f)] private float _stepOffset = 0.3f;

        [Header("Look")]
        [SerializeField, Min(0f)] private float _mouseSensitivity = 0.08f;
        [SerializeField, Range(1f, 89f)] private float _verticalLookLimit = 85f;
        [SerializeField, Min(0f)] private float _eyeHeight = 1.6f;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursorOnStart;

        private float _verticalVelocity;
        private float _pitch;

        /// <summary>Camera used for look and interaction raycasts.</summary>
        public Camera ViewCamera => _viewCamera;

        /// <summary>True when this controller currently owns the mouse cursor.</summary>
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Whether the required CharacterController reference is available.</summary>
        public bool HasCharacterController => _characterController != null;

        private void Awake()
        {
            if (_characterController == null)
                _characterController = GetComponent<CharacterController>();

            if (_viewCamera == null)
                _viewCamera = GetComponentInChildren<Camera>();

            if (_characterController != null)
            {
                _characterController.slopeLimit = _slopeLimit;
                _characterController.stepOffset = _stepOffset;
            }

            ApplyEyeHeight();
        }

        private void Start()
        {
            // Never force the cursor into a locked state unless the scene explicitly requests it.
            SetCursorLocked(_lockCursorOnStart);
        }

        private void Update()
        {
            HandleCursorInput();
            HandleLook();
            HandleMovement();
        }

        private void HandleCursorInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsCursorLocked)
                SetCursorLocked(true);
        }

        private void HandleLook()
        {
            if (!IsCursorLocked || _viewCamera == null)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            transform.Rotate(Vector3.up, delta.x * _mouseSensitivity, Space.World);

            _pitch = Mathf.Clamp(_pitch - delta.y * _mouseSensitivity, -_verticalLookLimit, _verticalLookLimit);
            _viewCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            if (_characterController == null)
                return;

            Keyboard keyboard = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                input = new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                input = Vector2.ClampMagnitude(input, 1f);
            }

            Vector3 planarDirection = transform.right * input.x + transform.forward * input.y;
            planarDirection.y = 0f;
            if (planarDirection.sqrMagnitude > 1f)
                planarDirection.Normalize();

            if (_characterController.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += _gravity * Time.deltaTime;

            Vector3 velocity = planarDirection * _moveSpeed;
            velocity.y = _verticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);
        }

        private void ApplyEyeHeight()
        {
            if (_viewCamera == null)
                return;

            Vector3 localPosition = _viewCamera.transform.localPosition;
            localPosition.y = _eyeHeight;
            _viewCamera.transform.localPosition = localPosition;
        }

        /// <summary>Allows a scene or pause controller to explicitly capture/release the cursor.</summary>
        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SetCursorLocked(false);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }
    }
}
