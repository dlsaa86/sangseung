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

        [Tooltip("외곽선이 물체 밖으로 뻗는 총 거리(거리 비례). 선의 바깥 끝을 정한다.")]
        [SerializeField, Range(0.001f, 0.03f)] private float _width = 0.006f;

        [Tooltip("선 자체의 두께. 마스크는 (_width - _thickness) 만큼 부풀어 " +
                 "부품 사이 틈을 메우고, 남는 차이가 곧 선이 된다. " +
                 "_width 보다 크면 안 된다 — 그러면 선이 사라진다.")]
        [SerializeField, Range(0.0005f, 0.01f)] private float _thickness = 0.002f;

        [SerializeField, Range(1f, 30f)] private float _fadeSpeed = 14f;

        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");

        private readonly List<Renderer> _shells = new List<Renderer>();
        private readonly List<GameObject> _owned = new List<GameObject>();
        private object _currentKey;
        private float _weight;
        private MaterialPropertyBlock _block;
        private Material _shellMaterial;
        private Material _maskMaterial;

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

            // 마스크는 없어도 외곽선 자체는 나온다 — 다만 부품마다 테두리가 생긴다.
            // 그래서 경고만 남기고 계속한다.
            Shader mask = Shader.Find("Ascend/OutlineMask");
            if (mask == null)
                Debug.LogWarning("[상승] Ascend/OutlineMask 가 없다. 부품이 여럿인 대상에서 "
                               + "안쪽에도 테두리가 생긴다.");
            else
                _maskMaterial = new Material(mask) { name = "AscendOutlineMask(런타임)" };
        }

        private void OnDestroy()
        {
            Clear();
            if (_shellMaterial != null) Destroy(_shellMaterial);
            if (_maskMaterial != null) Destroy(_maskMaterial);
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
            Transform[] roots = hint != null ? hint.Roots : new[] { comp.transform };
            if (roots == null || roots.Length == 0) return;
            Transform root = null;
            for (int i = 0; i < roots.Length && root == null; i++) root = roots[i];
            if (root == null) return;

            // ── 부품마다 그리지 않고 **대상 전체를 감싸는 상자 하나**로 그린다 ──────
            //
            // 사용자 요구(2026-08-09): 「내부 오브젝트는 외곽선이 안 보여야 해.
            // 말 그대로 외곽선이니까 내부에는 선이 없어야 해, 아무것도.」
            //
            // 부품마다 껍질을 만들면 **부품 사이 단차**에서 선이 새어 나온다. 스텐실
            // 마스크를 부풀려 틈을 메워 봤지만(실측: 안쪽 잔여 2971 화소) 판재 이음매까지는
            // 못 덮었다. 더 부풀리면 이번엔 선이 물체에서 떨어져 뜬다.
            //
            // 상자는 **볼록**이라 안쪽 경계가 애초에 존재하지 않는다. 실루엣의 정밀도를
            // 내주고 「내부에 선이 하나도 없다」를 확실히 얻는 교환이다. 그리고 이 게임의
            // 조작물은 대부분 판·레버·상자꼴이라 손해가 크지 않다.
            Bounds b = default; bool any = false;
            foreach (var rt in roots)
            {
                if (rt == null) continue;
                foreach (var r in rt.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
            }

            // ⚠ **켜진 렌더러가 없으면 콜라이더로 물러난다** (2026-08-09 사용자 지적).
            //
            // 「레버도 선택이 가능한 건데 마우스 올렸을 때 왜 아웃라인 없냐.」
            //
            // 원인은 이 저장소의 이중 소유 구조다 — `InteractableLever` 는
            // `GrayboxWorld/Car/Console/ExecutionLever` 에 붙어 있는데 **그 렌더러는 꺼져
            // 있고**, 화면에 보이는 레버는 `CabinAD47/SOCKET_ElevPanel` 쪽 메시다.
            // 그래서 켜진 렌더러가 하나도 없어 여기서 조용히 반환했다 — 조준은 되는데
            // 외곽선만 없는, 원인을 찾기 어려운 상태가 된다.
            //
            // 조준이 성립한다는 것은 **콜라이더가 있다는 뜻**이므로 그것을 쓴다.
            // 「누를 수 있는 것에는 예외 없이 외곽선이 선다」가 이 기능의 계약이고,
            // 렌더러가 어디 사는지는 그 계약과 무관하다.
            if (!any)
            {
                foreach (var rt in roots)
                {
                    if (rt == null) continue;
                    foreach (var c in rt.GetComponentsInChildren<Collider>(false))
                    {
                        if (!c.enabled) continue;
                        if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
                    }
                }
            }
            if (!any || b.size.sqrMagnitude < 1e-6f) return;

            // 메시 모양으로 따는 대상(레버처럼 형태가 곧 정체성인 것)은 원본 메시를 쓴다.
            // 부품이 적어야 성립한다 — 많으면 이음매마다 선이 샌다(기계가 그랬다).
            if (hint != null && hint.MeshOutline)
            {
                foreach (var rt in roots)
                {
                    if (rt == null) continue;
                    foreach (var mf in rt.GetComponentsInChildren<MeshFilter>(false))
                    {
                        var src = mf.GetComponent<MeshRenderer>();
                        if (src == null || !src.enabled || mf.sharedMesh == null) continue;
                        if (_maskMaterial != null) SpawnAt(mf.transform, mf.sharedMesh, _maskMaterial, "__OutlineMask", false);
                        var m = SpawnAt(mf.transform, mf.sharedMesh, _shellMaterial, "__OutlineShell", true);
                        if (m != null) _shells.Add(m);
                    }
                }
                return;
            }

            Mesh box = BoxMesh();
            if (box == null) return;

            // 마스크(부풀지 않은 상자)와 껍질(부푼 상자) 두 장. 마스크가 상자 안쪽을
            // 스텐실로 막으므로 화면에는 상자 테두리만 남는다 —
            // 마스크가 없으면 상자가 통째로 칠해진다.
            if (_maskMaterial != null) SpawnBox(root, b, box, _maskMaterial, "__OutlineMask", false);
            var mr = SpawnBox(root, b, box, _shellMaterial, "__OutlineShell", true);
            if (mr != null) _shells.Add(mr);
        }

        /// <summary>
        /// 1×1×1 상자 메시. **직접 만들지 않고 Unity 기본 큐브를 빌린다.**
        ///
        /// 처음엔 24 정점을 손으로 짰는데 면마다 기저축을 다르게 잡아 **삼각형 감김이
        /// 뒤죽박죽**이 됐다. `Cull Front` 는 감김으로 앞뒤를 가르므로 어떤 면은 잘리고
        /// 어떤 면은 남아 **외곽선이 통째로 사라졌다**(실측 0 화소). 기본 큐브는 감김도
        /// 법선도 정확하고 면당 정점이 분리돼 있어 껍질 확장에 그대로 맞다.
        /// </summary>
        private static Mesh BoxMesh()
        {
            if (_boxMesh != null) return _boxMesh;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _boxMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
            return _boxMesh;
        }

        private static Mesh _boxMesh;

        /// <summary>원본 메시를 그 자리에 그대로 겹쳐 놓는다 (메시 모양 외곽선용).</summary>
        private MeshRenderer SpawnAt(Transform parent, Mesh mesh, Material material, string name, bool track)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            _owned.Add(go);
            return track ? mr : null;
        }

        /// <summary>
        /// 월드 경계 <paramref name="b"/> 를 감싸는 상자 렌더러를 만든다.
        /// 부모의 스케일을 나눠 줘야 상자가 부모 스케일에 두 번 곱해지지 않는다.
        /// </summary>
        private MeshRenderer SpawnBox(Transform parent, Bounds b, Mesh mesh, Material material, string name, bool track)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(parent, true);
            go.transform.position = b.center;
            go.transform.rotation = Quaternion.identity;   // 경계가 축 정렬이라 회전은 항등
            Vector3 s = parent.lossyScale;
            go.transform.localScale = new Vector3(
                b.size.x / Mathf.Max(1e-5f, Mathf.Abs(s.x)),
                b.size.y / Mathf.Max(1e-5f, Mathf.Abs(s.y)),
                b.size.z / Mathf.Max(1e-5f, Mathf.Abs(s.z)));

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            _owned.Add(go);
            return track ? mr : null;
        }


        private void Apply(bool usable)
        {
            Color c = usable ? _usableColor : _blockedColor;
            float w = Mathf.Clamp01(_weight);
            c.a *= w;

            // 마스크는 선 두께만큼 **덜** 부푼다. 그 차이가 화면에 남는 선이고,
            // 마스크가 부품 사이 틈을 미리 메우므로 안쪽에는 아무것도 남지 않는다.
            float outer = _width * w;
            float inner = Mathf.Max(0f, (_width - _thickness)) * w;
            if (_maskMaterial != null) _maskMaterial.SetFloat(WidthId, inner);

            for (int i = 0; i < _shells.Count; i++)
            {
                var r = _shells[i];
                if (r == null) continue;
                if (_block == null) _block = new MaterialPropertyBlock();   // 도메인 리로드 뒤 null
                r.GetPropertyBlock(_block);
                _block.SetColor(ColorId, c);
                _block.SetFloat(WidthId, outer);
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
