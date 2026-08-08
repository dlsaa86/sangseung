using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.Player
{
    /// <summary>
    /// 조준한 상호작용물의 **외곽선을 세운다.** 모든 조작 가능한 오브젝트에 적용된다.
    ///
    /// ## 왜 (2026-08-08 사용자 지시)
    ///
    /// 「조작 가능한 오브젝트에 마우스를 올리면 외곽선으로 선택할 수 있음을 알리면 좋겠어.
    /// 이건 **모든 오브젝트**에 해당하는 거임.」
    ///
    /// 1인칭에서 「이건 누를 수 있다」를 알리는 방법은 셋뿐이다 — 문구, 커서 모양, 물체 자체.
    /// 앞의 둘은 화면 중앙에 있고 물체는 화면 어디에나 있다. **물체가 스스로 말해야**
    /// 시선이 문구와 물체 사이를 오가지 않는다.
    ///
    /// ## 림에서 껍질로 바꾼 이유 (같은 날, 실측 뒤)
    ///
    /// 첫 판본은 `Ascend/Stylized` 의 `_RimStrength` 를 올렸다. 그 셰이더에 이미 실루엣
    /// 림이 있으니 그것이 곧 외곽선이라고 봤다. **그런데 실제로는 대부분 동작하지 않았다** —
    /// 상호작용물 6개 중 5개가 림을 가진 렌더러 0개였고(`URP/Lit` 이라 프로퍼티 자체가 없다),
    /// 발광 폴백으로 물러났지만 「물체가 조금 밝아진다」는 외곽선이 아니다.
    /// 재질을 전부 `Ascend/Stylized` 로 옮기면 되지만 그건 **그림이 바뀌는 아트 결정**이다.
    ///
    /// 그래서 원본 재질을 아예 건드리지 않는 방법으로 옮겼다. 같은 메시를 법선 방향으로
    /// 부풀려 앞면을 잘라 그리면(`Ascend/OutlineShell`) 실루엣 바깥에만 테두리가 남는다.
    /// **어떤 셰이더를 쓰는 물체든 똑같이 동작하고, 끄면 흔적이 0 이다.**
    ///
    /// ## 껍질은 조준 대상이 아니다
    ///
    /// 콜라이더를 붙이지 않는다. 붙이면 껍질이 원본보다 크므로 **껍질이 조준을 가로채고**,
    /// 그러면 조준→껍질 생성→조준 대상 변경→껍질 파괴가 매 프레임 반복된다.
    /// </summary>
    public sealed class InteractableHighlighter : MonoBehaviour
    {
        [SerializeField] private CrosshairInteractor _interactor;

        [Tooltip("누를 수 있을 때의 외곽선 색.")]
        [SerializeField] private Color _usableColor = new Color(1f, 0.86f, 0.45f, 1f);

        [Tooltip("보이지만 지금은 누를 수 **없을** 때. 색으로 구분해 「왜 안 되는지」를 묻게 만든다.")]
        [SerializeField] private Color _blockedColor = new Color(0.85f, 0.35f, 0.28f, 1f);

        [Tooltip("외곽선 폭(화면 비례). 거리와 무관하게 같은 두께로 보인다.")]
        [SerializeField, Range(0f, 0.05f)] private float _width = 0.012f;

        [SerializeField, Range(1f, 30f)] private float _fadeSpeed = 14f;

        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");

        private readonly List<Renderer> _shells = new List<Renderer>();
        private readonly List<GameObject> _owned = new List<GameObject>();
        private object _currentKey;
        private float _weight;
        private MaterialPropertyBlock _block;
        private Material _shellMaterial;

        /// <summary>지금 외곽선이 서 있는 렌더러 수. 헤드리스 점검과 진단이 읽는다.</summary>
        public int ShellCount => _shells.Count;

        private void Awake()
        {
            if (_interactor == null) _interactor = GetComponentInParent<CrosshairInteractor>();
            _block = new MaterialPropertyBlock();

            Shader shader = Shader.Find("Ascend/OutlineShell");
            if (shader == null)
            {
                Debug.LogWarning("[상승] Ascend/OutlineShell 셰이더가 없다. 외곽선을 그리지 않는다.");
                return;
            }
            _shellMaterial = new Material(shader) { name = "AscendOutlineShell(런타임)" };
        }

        private void OnDestroy()
        {
            Clear();
            if (_shellMaterial != null) Destroy(_shellMaterial);
        }

        private void LateUpdate()
        {
            if (_interactor == null || _shellMaterial == null) return;

            IInteractable target = _interactor.CurrentInteractable;
            object key = target as Object;

            if (!ReferenceEquals(key, _currentKey))
            {
                Clear();
                _currentKey = key;
                _weight = 0f;
                Build(target);
            }

            bool usable = target != null && target.CanInteract;
            float goal = target == null ? 0f : 1f;
            _weight = Mathf.MoveTowards(_weight, goal, _fadeSpeed * Time.deltaTime);

            Apply(usable);
        }

        private void OnDisable() => Clear();

        /// <summary>
        /// 대상의 메시마다 껍질을 만든다. `InteractableHighlightTarget` 이 있으면
        /// 그것이 가리키는 루트를, 없으면 자기 자신을 쓴다 — 조준 상자와 보이는 물체가
        /// 다른 경우가 있기 때문이다.
        /// </summary>
        private void Build(IInteractable target)
        {
            var comp = target as Component;
            if (comp == null) return;

            var hint = comp.GetComponent<InteractableHighlightTarget>();
            Transform root = hint != null ? hint.Root : comp.transform;
            if (root == null) return;

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var srcRenderer = mf.GetComponent<MeshRenderer>();
                if (srcRenderer == null || !srcRenderer.enabled) continue;

                var go = new GameObject("__OutlineShell");
                go.hideFlags = HideFlags.HideAndDontSave;   // 씬에 저장되지 않는다
                go.transform.SetParent(mf.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _shellMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                // 라이트 프로브를 끄는 이유: 껍질은 무광 단색이라 어차피 조명을 안 받는데,
                // 켜 두면 프로브 보간 비용만 든다.
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                _owned.Add(go);
                _shells.Add(mr);
            }
        }

        private void Apply(bool usable)
        {
            Color c = usable ? _usableColor : _blockedColor;
            c.a *= Mathf.Clamp01(_weight);

            for (int i = 0; i < _shells.Count; i++)
            {
                var r = _shells[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(ColorId, c);
                _block.SetFloat(WidthId, _width * Mathf.Clamp01(_weight));
                r.SetPropertyBlock(_block);
            }
        }

        /// <summary>껍질을 전부 없앤다. 남겨 두면 조준을 벗어난 물체가 계속 테두리를 두른다.</summary>
        private void Clear()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] == null) continue;
                if (Application.isPlaying) Destroy(_owned[i]);
                else DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
            _shells.Clear();
        }
    }
}
