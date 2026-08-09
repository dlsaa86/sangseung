using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// **2단 조작.** 기계를 조준하면 실루엣이 살아나고, 누르면 카메라가 기계에 고정된다.
    ///
    /// ## 왜 (2026-08-08 사용자 지시)
    ///
    /// 「레버를 보려고 카메라를 돌리면 구슬 돌아가는 게 잘 안 보임. 이게 불편하니까
    /// 2단 구조로 가자. 맨 처음에 기계에 마우스를 가져다대면 기계 주변에 아웃라인으로
    /// 하일라이트 표시를 하고, 기계를 누르면 카메라가 기계에 고정되고 레버를 마우스로
    /// 눌러도 시야는 기계 고정. 더 이상 할 수 있는 액션이 없거나 사용자가 나가기 등
    /// 액션을 취하면 그때 원래대로 돌아오게.」
    ///
    /// 1인칭에서 **조작 대상과 판독 대상이 다른 방향에 있으면** 둘 중 하나를 잃는다.
    /// 실측: 스폰 지점에서 레버는 정면에서 16.6° 벗어나 있고 눈높이보다 60 cm 아래다.
    /// 레버를 조준하면 구슬 통관이 시야 위로 밀린다.
    ///
    /// ## 두 모드는 손끝에서 구분된다
    ///
    ///     자유 시선 — 커서 잠김 · 화면 중앙 조준 · 카메라가 돈다
    ///     기계 집중 — 커서 풀림 · **마우스 위치** 조준 · 카메라 고정
    ///
    /// 카메라를 고정한 채 화면 중앙으로만 조준하면 화면 중앙의 물체 하나만 누를 수 있어
    /// 「레버를 마우스로 눌러도」가 성립하지 않는다. 그래서 집중 중에는
    /// <see cref="CrosshairInteractor.UsePointer"/> 를 켠다.
    ///
    /// ## 나가기를 넷 다 넣는 이유
    ///
    /// 사용자가 「뭐가 편할지 모르겠어, 비슷한 사례로 사용성 확인 필요」라고 유보했다.
    /// 콘솔·단말 집중 UX 의 관례는 **중복 제공**이다 — 하나만 두면 플레이어는 반드시
    /// 다른 것을 먼저 시도하고, 그때 갇힌 느낌을 받는다.
    ///
    ///     Esc      「빠져나오기」의 가장 강한 관례
    ///     우클릭    조준·집중 해제의 FPS 관례. 손이 이미 마우스에 있다
    ///     WASD     배우지 않아도 되는 유일한 조작 — 물러나면 풀린다
    ///     자동      할 일이 없으면 붙잡아 두지 않는다 (기본 꺼짐, 아래 참조)
    ///
    /// ## 외곽선은 이 클래스가 하지 않는다
    ///
    /// 처음에는 여기서 기계만 빛냈지만, 사용자가 「이건 **모든 오브젝트**에 해당하는 거임」
    /// 이라고 범위를 넓혔다(2026-08-08). 그래서 <see cref="InteractableHighlighter"/> 로
    /// 옮겼고, 기계는 <see cref="InteractableHighlightTarget"/> 으로 빛낼 범위를 가리킨다.
    /// 두 곳에서 칠하면 같은 `MaterialPropertyBlock` 을 서로 덮어써 깜빡인다.
    /// </summary>
    public sealed class MachineFocusController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Camera _camera;
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private CrosshairInteractor _interactor;
        [SerializeField] private InteractableMachineFocus _focus;

        [Tooltip("집중했을 때 화면에 담을 범위. 비우면 _focus 의 콜라이더를 쓴다.")]
        [SerializeField] private Collider _frameBounds;

        /// <summary>
        /// 범위에 **함께** 담을 것들. 사용자 지시 (2026-08-09):
        /// 「버튼 누르면 기계 위에 층수까지 한눈에 보이게」.
        ///
        /// ⚠ 콜라이더를 키워서 해결하지 않는다. `_frameBounds` 는 조준 상자를 겸하므로
        ///   (`SetFocusColliderEnabled` 참조) 키우면 **기계 위 빈 공간을 클릭해도
        ///   기계가 잡힌다.** 화면에 담을 범위와 손으로 짚는 범위는 다른 것이고,
        ///   여기서 합집합을 뜨면 둘을 분리한 채로 목적을 이룬다.
        ///
        /// 비어 있으면 종전 동작 그대로다 — 배선 안 해도 깨지지 않는다.
        /// </summary>
        [Tooltip("범위에 함께 담을 렌더러. 기계 위 층수 표시가 여기 들어간다.")]
        [SerializeField] private Renderer[] _alsoFrame;

        // ⚠ 하일라이트는 이 클래스가 하지 않는다. `InteractableHighlighter` 가 **모든**
        // 상호작용물에 대해 처리하고(2026-08-08 사용자 지시: 「이건 모든 오브젝트에
        // 해당하는 거임」), 기계는 `InteractableHighlightTarget` 으로 빛낼 범위를 가리킨다.
        // 여기서 또 칠하면 두 곳이 같은 `MaterialPropertyBlock` 을 덮어써 깜빡인다.

        [Header("카메라")]
        [Tooltip("비우면 _frameBounds 를 화면에 담는 자리를 계산한다.")]
        [SerializeField] private Transform _focusAnchor;
        [SerializeField, Range(0.1f, 1.0f)] private float _enterDuration = 0.35f;
        [SerializeField, Range(0.1f, 1.0f)] private float _exitDuration = 0.28f;
        [Tooltip("계산한 거리에 곱하는 여유. 1 이면 범위가 화면에 꽉 찬다.")]
        [SerializeField, Range(1.0f, 2.0f)] private float _framingMargin = 1.18f;

        [Header("나가기")]
        [SerializeField] private bool _exitOnEscape = true;
        [SerializeField] private bool _exitOnRightClick = true;
        [SerializeField] private bool _exitOnMove = true;
        [Tooltip("누를 것이 하나도 없을 때 이 시간이 지나면 자동으로 나온다. " +
                 "0 이면 끔 — 플레이테스트로 정하기 전에는 사람이 나가게 둔다.")]
        [SerializeField, Range(0f, 6f)] private float _idleExitSeconds = 0f;

        // ── 상태 ─────────────────────────────────────────────────────────────

        public bool IsFocused { get; private set; }

        private Vector3 _restPos;
        private Quaternion _restRot;
        private Vector3 _goalPos;
        private Quaternion _goalRot;
        private float _blend;                   // 0 = 원위치, 1 = 집중
        private float _blendSpeed;
        private bool _blending;
        private float _idleFor;

        private Bounds _cachedBounds;
        private bool _boundsCached;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            if (_player == null) _player = GetComponentInParent<FirstPersonController>();
            if (_interactor == null) _interactor = GetComponentInParent<CrosshairInteractor>();
        }

        private void OnEnable()
        {
            if (_focus != null) _focus.onFocusRequested.AddListener(Enter);
        }

        private void OnDisable()
        {
            if (_focus != null) _focus.onFocusRequested.RemoveListener(Enter);
        }

        private void Update()
        {
            UpdateExitInput();
            UpdateBlend();
        }

        private void UpdateExitInput()
        {
            if (!IsFocused) { _idleFor = 0f; return; }

            var kb = Keyboard.current;
            var ms = Mouse.current;

            if (_exitOnEscape && kb != null && kb.escapeKey.wasPressedThisFrame) { Exit(); return; }
            if (_exitOnRightClick && ms != null && ms.rightButton.wasPressedThisFrame) { Exit(); return; }

            if (_exitOnMove && kb != null)
            {
                bool moved = kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed;
                if (moved) { Exit(); return; }
            }

            if (_idleExitSeconds > 0f)
            {
                // 「누를 것이 하나도 없다」의 판정. 조준 중인 것이 없거나 있어도 못 누르면 쉬는 것으로 본다.
                var cur = _interactor != null ? _interactor.CurrentInteractable : null;
                bool anything = cur != null && cur.CanInteract;
                _idleFor = anything ? 0f : _idleFor + Time.deltaTime;
                if (_idleFor >= _idleExitSeconds) Exit();
            }
        }

        // ── 들어가기 / 나오기 ────────────────────────────────────────────────

        public void Enter()
        {
            if (IsFocused || _camera == null) return;
            IsFocused = true;

            _restPos = _camera.transform.position;
            _restRot = _camera.transform.rotation;
            ComputeGoal(out _goalPos, out _goalRot);

            _blending = true;
            _blendSpeed = 1f / Mathf.Max(0.01f, _enterDuration);

            // 시선과 이동을 멈춘다. 컴포넌트를 끄는 것이 가장 확실하다 —
            // 개별 축을 막으면 나중에 축이 늘 때마다 여기도 고쳐야 한다.
            if (_player != null) _player.enabled = false;

            // 커서를 풀어 마우스로 가리키게 한다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_interactor != null) _interactor.UsePointer = true;

            // 집중 중에는 기계 자체를 다시 누를 수 없다 — 같은 자리로 재보간되면 화면이 튄다.
            if (_focus != null) _focus.SetCanInteract(false);

            // 🔴 **콜라이더를 반드시 끈다.** `CanInteract=false` 만으로는 부족하다 —
            // `CrosshairInteractor.FindTarget` 은 `IInteractable` 로 해석되는 **가장 가까운**
            // 적중을 고르지, 누를 수 있는지는 그 뒤에 본다. 기계를 감싼 큰 상자가 앞에 있으면
            // 그 뒤의 레버·탱크·계약 패널이 전부 가려져 **집중해 놓고 아무것도 못 누른다.**
            SetFocusColliderEnabled(false);
            _idleFor = 0f;
        }

        public void Exit()
        {
            if (!IsFocused) return;
            IsFocused = false;

            _blending = true;
            _blendSpeed = 1f / Mathf.Max(0.01f, _exitDuration);

            if (_interactor != null) _interactor.UsePointer = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_focus != null) _focus.SetCanInteract(true);
            SetFocusColliderEnabled(true);
            _idleFor = 0f;
            // 플레이어 컴포넌트는 **보간이 끝난 뒤** 켠다 (UpdateBlend). 지금 켜면
            // 카메라가 되돌아오는 중에 시선 입력이 섞여 화면이 흔들린다.
        }

        /// <summary>
        /// 기계를 감싼 조준 상자를 켜고 끈다. 집중 중에는 꺼야 뒤의 것들을 누를 수 있다.
        /// `_frameBounds` 가 같은 콜라이더일 수 있으므로 둘 다 다룬다.
        /// </summary>
        private void SetFocusColliderEnabled(bool on)
        {
            if (_focus != null)
            {
                var c = _focus.GetComponent<Collider>();
                if (c != null) c.enabled = on;
            }
            if (_frameBounds != null) _frameBounds.enabled = on;
        }

        private void ComputeGoal(out Vector3 pos, out Quaternion rot)
        {
            if (_focusAnchor != null)
            {
                pos = _focusAnchor.position;
                rot = _focusAnchor.rotation;
                return;
            }

            // ⚠ **꺼진 콜라이더의 `bounds` 는 크기 0 이다.** 집중 중에는 콜라이더를 끄므로,
            // 어떤 이유로든 꺼진 상태에서 여기 들어오면 범위가 0 이 되어 카메라가 기계
            // **한가운데**로 들어간다(벽 안에서 보게 된다). 그래서 한 번 잰 값을 캐시한다.
            if (!_boundsCached)
            {
                Collider c = _frameBounds != null ? _frameBounds
                           : _focus != null ? _focus.GetComponent<Collider>() : null;
                if (c == null || c.bounds.size.sqrMagnitude < 1e-6f)
                {
                    pos = _restPos; rot = _restRot; return;
                }
                _cachedBounds = c.bounds;

                // 기계 위 층수 표시를 같이 담는다. **캐시 안에서 합친다** — 밖에서
                // 매번 합치면 집중 중에 층수가 바뀔 때 화각이 흔들린다.
                for (int i = 0; _alsoFrame != null && i < _alsoFrame.Length; i++)
                {
                    Renderer extra = _alsoFrame[i];
                    // 꺼진 렌더러의 bounds 는 신뢰할 수 없다. `_frameBounds` 가 꺼졌을 때
                    // 범위가 0 이 되던 것과 같은 함정이라 여기서도 막는다.
                    if (extra != null && extra.enabled && extra.gameObject.activeInHierarchy)
                        _cachedBounds.Encapsulate(extra.bounds);
                }
                _boundsCached = true;
            }

            Bounds b = _cachedBounds;

            // 플레이어가 서 있는 쪽에서 본다 — 패널의 법선을 몰라도 되고,
            // 뒤로 돌아가 벽 안에서 보는 사고가 원리적으로 생기지 않는다.
            Vector3 away = _restPos - b.center;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -_camera.transform.forward;
            away.Normalize();

            // 세로로 담을 거리. 가로가 더 넓으면 가로 기준으로 물러난다.
            float halfV = Mathf.Max(0.05f, b.extents.y);
            float halfH = Mathf.Max(0.05f, Mathf.Max(b.extents.x, b.extents.z));
            float tanV = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(0.1f, _camera.aspect);
            float dist = Mathf.Max(halfV / tanV, halfH / tanH) * _framingMargin;

            // 눈높이를 범위 중심에 맞춘다 — 구슬 통관과 레버가 **한 화면**에 들어오게 하는
            // 것이 이 기능의 목적이고, 중심을 벗어나면 그 목적이 깨진다.
            pos = b.center + away * dist;
            rot = Quaternion.LookRotation((b.center - pos).normalized, Vector3.up);
        }

        private void UpdateBlend()
        {
            if (!_blending || _camera == null) return;

            float target = IsFocused ? 1f : 0f;
            _blend = Mathf.MoveTowards(_blend, target, _blendSpeed * Time.deltaTime);
            float e = _blend * _blend * (3f - 2f * _blend);   // smoothstep

            if (IsFocused)
            {
                _camera.transform.position = Vector3.Lerp(_restPos, _goalPos, e);
                _camera.transform.rotation = Quaternion.Slerp(_restRot, _goalRot, e);
            }
            else
            {
                _camera.transform.position = Vector3.Lerp(_restPos, _goalPos, e);
                _camera.transform.rotation = Quaternion.Slerp(_restRot, _goalRot, e);
            }

            if (Mathf.Approximately(_blend, target))
            {
                _blending = false;
                // 완전히 돌아온 뒤에 플레이어를 켠다.
                if (!IsFocused && _player != null) _player.enabled = true;
            }
        }

    }
}
