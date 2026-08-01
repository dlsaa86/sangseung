using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Ascend.Prototype.Diagnostics;
using Ascend.Prototype.Perf;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// Finds and activates interactables in the centre of the view. The sphere cast deliberately
    /// gives the player a forgiving aim volume instead of requiring pixel-perfect clicks.
    ///
    /// 하이라이트 경로는 **풀 기반**이다. `docs/runtime/GC_ALLOC_ANALYSIS.md` §7 이
    /// 저장소 전체에서 `ObjectPool` 이 답인 지점을 하나로 좁혔고, 그것이 여기다 —
    /// 조준 대상이 바뀔 때마다 렌더러당 `MaterialPropertyBlock` 2개와 상태 객체 1개가
    /// 새로 생겼다. 조준은 걸어 다니는 내내 계속 바뀌므로 이 경로는 사실상 상시 도는
    /// 경로이고, 그래서 프레임 할당으로 잡힌다(`UP-TECH-05`·`UP-TECH-06`).
    /// </summary>
    public sealed class CrosshairInteractor : MonoBehaviour
    {
        [Header("References")]
        // `_viewCamera` 는 필수로 표시하지 않는다 — `Update` 의 `Camera.main` 대체 경로가
        // **검사기보다 늦게** 돈다(검사기는 `AfterSceneLoad`, 즉 첫 `Update` 앞이다).
        // 표시하면 `Camera.main` 으로 잘 도는 구성을 결함으로 보고하게 되고,
        // 그런 보고가 섞이면 진짜 결선 실패가 소음에 묻힌다.
        [SerializeField] private Camera _viewCamera;

        [RequiredReference("CrosshairView 가 없으면 조준점·프롬프트가 통째로 죽는다 — 상호작용은 되는데 무엇을 겨누는지 화면에 아무 표시가 없다")]
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

        /// <summary>
        /// 원본 블록 풀의 예열·상한. 예열은 「대상 하나가 대개 이 정도 렌더러를 갖는다」이고,
        /// 상한은 「큰 대상 하나를 겨눴다고 풀이 영구히 부풀지 않는다」이다.
        /// 모자라면 풀이 더 만들 뿐이라 동작이 달라지지 않는다.
        /// </summary>
        private const int HighlightBlockPrewarm = 8;
        private const int HighlightBlockMaxSize = 32;

        // 델리게이트를 static 으로 캐시한다. 인스턴스마다 새 클로저를 만들면
        // 풀을 넣어 줄인 할당을 풀 자신이 도로 만든다.
        private static readonly Func<MaterialPropertyBlock> CreateBlock = () => new MaterialPropertyBlock();

        /// <summary>
        /// **반환할 때 비운다.** 이 한 줄이 풀 도입의 유일한 위험을 막는다 —
        /// `MaterialPropertyBlock` 을 비우지 않고 재사용하면 이전에 겨눴던 물체의
        /// 색이 다음 물체에 그대로 실린다. 그 증상은 「가끔 엉뚱한 게 초록으로 빛난다」로
        /// 나타나 원인 추적이 어렵다. 「풀 안의 것은 비어 있다」를 불변식으로 세워 둔다.
        /// </summary>
        private static readonly Action<MaterialPropertyBlock> ClearBlock = block => block.Clear();

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly List<RendererState> _highlightedRenderers = new List<RendererState>();

        /// <summary>
        /// `GetComponentsInChildren` 이 배열을 새로 만들지 않도록 받는 버퍼.
        /// 조준 대상이 바뀔 때마다 불리므로 배열 한 개가 매번 쌓였다.
        /// </summary>
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>(16);

        /// <summary>
        /// 원래 블록 보관용 풀. 수명이 「하이라이트가 걸려 있는 동안」이라 동시에 여러 개가
        /// 밖에 나가 있고, 개수가 대상마다 다르다 — 풀이 답인 형태다.
        /// </summary>
        private ObjectPool<MaterialPropertyBlock> _blockPool;

        /// <summary>
        /// 값을 밀어 넣는 데만 쓰는 작업용 블록. **풀로 만들지 않는다** —
        /// 한 번에 하나만 필요하고 `SetPropertyBlock` 이 값을 복사해 가므로,
        /// 필드 하나가 풀보다 단순하면서 할당도 같은 0 이다.
        /// 근거 없이 풀을 늘리면 반환 누락이라는 실패 경로만 생긴다.
        /// </summary>
        private MaterialPropertyBlock _workingBlock;

        private IInteractable _currentInteractable;
        private bool _currentCanInteract;

        /// <summary>Object currently under the crosshair, if any.</summary>
        public IInteractable CurrentInteractable => _currentInteractable;

        /// <summary>Serialized camera reference, used by the setup validator.</summary>
        public bool HasCameraReference => _viewCamera != null;

        /// <summary>Serialized UI reference, used by the setup validator.</summary>
        public bool HasViewReference => _view != null;

        /// <summary>지금 하이라이트가 걸려 있는 렌더러 수. 검증이 회수 누락을 볼 때 읽는다.</summary>
        public int HighlightedRendererCount => _highlightedRenderers.Count;

        /// <summary>
        /// 풀이 실제로 새로 만든 블록 수. 조준 대상을 여러 번 바꿔도 이 값이 **멈춰야**
        /// 풀이 일하고 있는 것이다. 계속 오르면 상한이 실사용보다 작다.
        /// </summary>
        public int HighlightBlocksCreated => _blockPool != null ? _blockPool.CreatedCount : 0;

        /// <summary>창고에서 꺼내 다시 쓴 횟수. 0 이면 풀을 넣은 의미가 없다.</summary>
        public int HighlightBlocksReused => _blockPool != null ? _blockPool.ReusedCount : 0;

        /// <summary>이중 반환 횟수. 0 이 아니면 하이라이트 소유권이 어긋났다는 뜻이다.</summary>
        public int HighlightDoubleReleaseCount => _blockPool != null ? _blockPool.DoubleReleaseCount : 0;

        /// <summary>한 줄 요약. 성능 보고서와 PlayMode 검증이 그대로 찍는다.</summary>
        public string HighlightPoolStats => _blockPool != null ? _blockPool.Stats() : "풀 미초기화";

        /// <summary>
        /// 하이라이트를 걸어 둔 렌더러 하나와 그 원래 블록.
        ///
        /// **왜 여기는 풀이 아니라 struct 인가:** 이 값의 수명은 `_highlightedRenderers`
        /// 와 정확히 같아서 개별 소유권이 없다. `List&lt;T&gt;` 는 struct 원소를 자기
        /// 배열에 담으므로 항목당 할당이 이미 0 이고, 풀로 바꾸면 얻는 것 없이
        /// 「반환을 빠뜨리면 샌다」는 실패 경로만 새로 생긴다.
        /// </summary>
        private readonly struct RendererState
        {
            public readonly Renderer Renderer;
            public readonly MaterialPropertyBlock OriginalBlock;

            public RendererState(Renderer renderer, MaterialPropertyBlock originalBlock)
            {
                Renderer = renderer;
                OriginalBlock = originalBlock;
            }
        }

        private void Awake()
        {
            if (_viewCamera == null)
                _viewCamera = GetComponentInParent<Camera>();

            if (_viewCamera == null)
                _viewCamera = GetComponentInChildren<Camera>();

            _workingBlock = new MaterialPropertyBlock();

            // 예열을 생성자에 맡기지 않는다. 맡기면 예열 중에 나는 경고가 `OnWarning` 이
            // 연결되기 전에 발생해 조용히 사라진다 — `ComponentPool` 이 같은 이유로
            // 같은 순서를 쓴다. 지금은 예열(8)이 상한(32)보다 작아 경고가 날 수 없지만,
            // 상수를 고치는 사람이 그 사실을 알 필요가 없어야 한다.
            _blockPool = new ObjectPool<MaterialPropertyBlock>(
                CreateBlock, null, ClearBlock, 0, HighlightBlockMaxSize);
            _blockPool.OnWarning = OnPoolWarning;
            _blockPool.Prewarm(HighlightBlockPrewarm);
        }

        /// <summary>풀의 경고는 전부 소유권 사고다. 삼키지 않는다.</summary>
        private void OnPoolWarning(string message)
        {
            Debug.LogWarning("[상승] 조준 하이라이트 " + message, this);
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

        /// <summary>
        /// 상호작용물 조회. `GetComponentsInParent`(복수형)는 호출마다 **배열을 새로 만든다.**
        /// 이 함수는 매 프레임 스피어캐스트 히트마다 불리므로, 그 배열이 프레임당 최대 16개씩
        /// 쌓였다. 인터페이스도 단수형 조회가 되므로 배열이 필요 없다.
        /// </summary>
        private static IInteractable FindInteractable(Collider collider)
        {
            return collider.GetComponentInParent<IInteractable>(true);
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

        /// <summary>
        /// 하이라이트를 건다. 결과는 이전과 같고 **할당만 사라졌다** —
        /// 렌더러 배열은 재사용 버퍼로, 원본 블록은 풀로, 작업 블록은 필드 하나로 바뀌었다.
        ///
        /// 작업 블록을 렌더러마다 돌려 쓸 수 있는 이유: `Renderer.SetPropertyBlock` 은
        /// 값을 네이티브 쪽으로 **복사**한다. 참조를 붙들지 않으므로 다음 렌더러에
        /// 같은 블록을 다시 채워도 앞의 렌더러가 따라 바뀌지 않는다.
        /// 대신 채우기 전에 반드시 `Clear()` 한다 — 앞 대상의 값이 남으면 안 된다.
        /// </summary>
        private void ApplyHighlight(IInteractable target, bool canInteract)
        {
            Component component = target as Component;
            if (component == null)
                return;

            component.GetComponentsInChildren(true, _rendererBuffer);
            Color highlightColor = canInteract ? _availableHighlight : _unavailableHighlight;
            float blend = Mathf.Clamp01(_highlightBlend);

            for (int i = 0; i < _rendererBuffer.Count; i++)
            {
                Renderer renderer = _rendererBuffer[i];
                if (renderer == null)
                    continue;

                MaterialPropertyBlock original = _blockPool.Get();
                original.Clear();
                renderer.GetPropertyBlock(original);

                _workingBlock.Clear();
                renderer.GetPropertyBlock(_workingBlock);

                Material material = renderer.sharedMaterial;
                if (material != null)
                {
                    SetBlendedColor(material, _workingBlock, "_BaseColor", highlightColor, blend);
                    SetBlendedColor(material, _workingBlock, "_Color", highlightColor, blend);
                    if (material.HasProperty("_EmissionColor"))
                        _workingBlock.SetColor("_EmissionColor", highlightColor * (canInteract ? 0.35f : 0.08f));
                }

                renderer.SetPropertyBlock(_workingBlock);
                _highlightedRenderers.Add(new RendererState(renderer, original));
            }

            // 버퍼를 비워 둔다. 들고 있으면 렌더러가 파괴돼도 참조가 남는다.
            _rendererBuffer.Clear();
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

        /// <summary>
        /// 하이라이트를 되돌린다. 되돌리는 것과 **반환하는 것은 별개**다 —
        /// 렌더러가 그새 파괴됐어도 블록은 풀로 돌려보내야 한다. 여기서 새면
        /// 풀이 계속 새로 만들고, 풀을 넣은 의미가 조용히 사라진다.
        /// </summary>
        private void ClearHighlight()
        {
            for (int i = 0; i < _highlightedRenderers.Count; i++)
            {
                RendererState state = _highlightedRenderers[i];
                if (state.Renderer != null)
                    state.Renderer.SetPropertyBlock(state.OriginalBlock);

                if (_blockPool != null && state.OriginalBlock != null)
                    _blockPool.Release(state.OriginalBlock);
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
