using UnityEngine;
using UnityEngine.InputSystem;
using Ascend.Prototype.Diagnostics;

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
        // 둘 다 `Awake` 에 대체 경로가 있는데도 필수로 표시한다. 검사기는 씬 로드 직후
        // (`AfterSceneLoad` — Awake 뒤, Start 앞)에 돌므로, 여기서 비어 있다는 것은
        // **대체 경로까지 실패했다**는 뜻이지 「인스펙터를 안 물렸다」가 아니다.
        // 그 상태의 증상이 조용하다는 것이 표시하는 이유다 — 컨트롤러가 없으면 걷지
        // 못하고 카메라가 없으면 시점이 돌지 않는데, 어느 쪽도 오류를 내지 않는다.
        // (옛 `PlayerSetupValidator` 의 에디터 메뉴가 보던 것이 정확히 이 둘이었다.
        //  `UP-TEST-11` 웨이브 1에서 그 파일을 지웠고, 검사는 여기로 옮겨져 살아 있다 —
        //  `WiringDiagnosticsTests.TestPlayerComponentsStayMarked` 가 그 말뚝이다.)
        [Header("References")]
        [RequiredReference("CharacterController 가 없으면 이동 입력이 통째로 무시된다 — 걸을 수 없다")]
        [SerializeField] private CharacterController _characterController;

        [RequiredReference("카메라가 없으면 시점 회전이 조용히 멈춘다 — 마우스가 아무것도 하지 않는다")]
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

            // 컨트롤러가 꺼져 있는 프레임에는 움직이지 않는다.
            //
            // 왜 필요한가: 캡처 리그(`TenFloorCaptureRig`)와 증거 레코더는 플레이어를
            // 텔레포트시키려고 `CharacterController.enabled = false` 로 콜라이더를 잠깐
            // 끈다. 그런데 이 컴포넌트는 계속 활성이라 그 사이에도 `Update()` 가 돌고
            // `Move()` 를 불러 「Move called on inactive controller」 오류를 냈다.
            // 누수가 아니라 **두 컴포넌트의 Update 순서 경합**이다 — 끈 쪽이 다시 켜도
            // 꺼져 있던 프레임의 호출은 이미 오류로 기록된다.
            //
            // Pass 3 이 콘솔 오류 0 을 요구하므로 이 오류 하나가 캡처 세트 전체를
            // 판정 근거에서 탈락시킨다. 꺼진 컨트롤러를 미는 것은 어차피 무의미하므로
            // 조기 반환이 옳은 자리다 — 끄는 쪽을 고치면 다음에 끄는 코드가 또 밟는다.
            if (!_characterController.enabled)
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

        /// <summary>
        /// 눈높이를 세운다. **카메라가 이 컴포넌트의 직계 자식일 때만** 카메라를 직접 올린다.
        ///
        /// 왜 조건이 붙었나: 원래 이 함수는 카메라의 부모가 플레이어 루트라고 가정하고
        /// 로컬 y 를 `_eyeHeight` 로 덮었다. 그런데 `Head`(이미 눈높이에 있다) 아래에
        /// `CameraRig` 를 끼우자 눈높이가 **두 번** 더해져 카메라가 3.22m 로 올라갔다 —
        /// 천장이 3.2m 인 칸에서 시점이 천장 위였다.
        ///
        /// 그 결함이 통과한 이유가 더 중요하다. PlayMode 단정 394건 중 **카메라 위치를
        /// 보는 것이 하나도 없었고**, 캡처 하네스는 자기 카메라를 따로 만들어 쓰므로
        /// 그림도 멀쩡히 나왔으며, 트랜스폼이 엉뚱한 곳에 있는 것은 콘솔 오류가 아니다.
        /// 그래서 `TenFloorAutoPilot` 에 눈높이 단정을 함께 넣었다 — 이 수정보다
        /// 그 단정이 오래 간다.
        ///
        /// 계층이 눈높이를 소유하는 경우(`Player/Head/CameraRig/Camera`)에는 카메라의
        /// 로컬 위치를 **0 으로 정리만** 한다. `HumanScaleLayout` 이 `Head` 를
        /// `EyeHeight` 에 두므로 그것이 유일한 소유자여야 한다.
        /// 씬에 남아 있던 (-0.088, 0.98, -0.596) 은 `EyeLevelCapture` 가 마지막 샷
        /// 자리에 두고 간 잔재이며, 정리하지 않으면 그대로 굳는다.
        /// </summary>
        private void ApplyEyeHeight()
        {
            if (_viewCamera == null)
                return;

            Transform cam = _viewCamera.transform;

            if (cam.parent == transform)
            {
                Vector3 localPosition = cam.localPosition;
                localPosition.y = _eyeHeight;
                cam.localPosition = localPosition;
                return;
            }

            // 중간 리그가 있다 — 눈높이는 계층이 소유한다. 카메라는 리그 원점에 둔다.
            if (cam.localPosition != Vector3.zero) cam.localPosition = Vector3.zero;
        }

        /// <summary>
        /// 카메라의 월드 눈높이. 검증 하네스가 "시점이 사람 키에 있는가"를 물을 때 읽는다.
        /// 플레이어 루트를 기준으로 재므로 층이 바뀌어도 의미가 같다.
        /// </summary>
        public float EyeHeightAboveRoot =>
            _viewCamera != null ? _viewCamera.transform.position.y - transform.position.y : 0f;

        /// <summary>인스펙터에 설정된 목표 눈높이. 단정이 기대값으로 쓴다.</summary>
        public float ConfiguredEyeHeight => _eyeHeight;

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
