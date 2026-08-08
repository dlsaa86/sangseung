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
    /// ## 새 렌더 패스를 만들지 않는다
    ///
    /// URP 에 기본 아웃라인이 없고 이 저장소에는 렌더러 피처도 아웃라인 셰이더도 없다.
    /// 그런데 `Ascend/Stylized` 에 이미 **실루엣 림 항**이 있다 — 「실루엣을 살짝 세운다.
    /// 『큰 실루엣』이 락의 첫 항목이고, 무지 머티리얼끼리 겹치면 경계가 사라진다.」
    /// 그 `_RimStrength` 를 올리는 것이 곧 외곽선이다. 림은 시선 방향과 법선의 각도로
    /// 계산되므로 **정확히 실루엣 가장자리**에서 가장 밝다 — 아웃라인이 하는 일과 같다.
    ///
    /// 그 셰이더가 아닌 렌더러는 `_EmissionColor` 로 물러난다. 둘 다 없으면 건너뛴다 —
    /// 억지로 머티리얼을 바꾸면 조준할 때마다 재질이 달라진다.
    ///
    /// ## 원래 값을 반드시 되돌린다
    ///
    /// `MaterialPropertyBlock` 은 렌더러별 덮어쓰기라 공유 머티리얼을 건드리지 않는다.
    /// 그래도 **원래 값을 기억해 두고 조준을 벗어날 때 되돌린다** — 안 되돌리면
    /// 한 번 조준한 물체가 영원히 빛나고, 그건 「선택 가능」이라는 신호를 죽인다.
    /// </summary>
    public sealed class InteractableHighlighter : MonoBehaviour
    {
        [SerializeField] private CrosshairInteractor _interactor;

        [Tooltip("조준했을 때의 림 세기. 0.25 안팎이 기본값이라 0.8 이면 확실히 구분된다.")]
        [SerializeField, Range(0f, 1f)] private float _rimHighlight = 0.85f;

        [Tooltip("누를 수 **없는** 상태일 때. 「보이지만 지금은 안 된다」를 약하게 알린다.")]
        [SerializeField, Range(0f, 1f)] private float _rimDisabled = 0.45f;

        [SerializeField, Range(1f, 20f)] private float _fadeSpeed = 9f;

        [Tooltip("림이 없는 셰이더에서 대신 쓸 발광 배수.")]
        [SerializeField, Range(1f, 4f)] private float _emissionBoost = 2.2f;

        private static readonly int RimId = Shader.PropertyToID("_RimStrength");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private sealed class Entry
        {
            public Renderer Renderer;
            public bool HasRim;
            public float BaseRim;
            public bool HasEmission;
            public Color BaseEmission;
        }

        private readonly List<Entry> _current = new List<Entry>();
        private object _currentKey;
        private float _weight;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            if (_interactor == null) _interactor = GetComponentInParent<CrosshairInteractor>();
            _block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (_interactor == null) return;

            IInteractable target = _interactor.CurrentInteractable;
            object key = target as Object;

            if (!ReferenceEquals(key, _currentKey))
            {
                // 대상이 바뀌었다 — 옛 대상을 **완전히 되돌리고** 새로 모은다.
                Restore();
                _currentKey = key;
                _weight = 0f;
                Collect(target);
            }

            bool usable = target != null && target.CanInteract;
            float goal = target == null ? 0f : (usable ? _rimHighlight : _rimDisabled);
            _weight = Mathf.MoveTowards(_weight, goal, _fadeSpeed * Time.deltaTime * Mathf.Max(goal, 0.2f));

            Apply();
        }

        private void OnDisable() => Restore();

        /// <summary>
        /// 빛낼 렌더러를 모으고 **원래 값을 기억한다.**
        /// `InteractableHighlightTarget` 이 있으면 그것이 가리키는 곳을, 없으면 자기 자신을 쓴다.
        /// </summary>
        private void Collect(IInteractable target)
        {
            _current.Clear();
            var comp = target as Component;
            if (comp == null) return;

            var hint = comp.GetComponent<InteractableHighlightTarget>();
            Transform root = hint != null ? hint.Root : comp.transform;
            if (root == null) return;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null) continue;

                var e = new Entry { Renderer = r };
                if (m.HasProperty(RimId)) { e.HasRim = true; e.BaseRim = m.GetFloat(RimId); }
                else if (m.HasProperty(EmissionId)) { e.HasEmission = true; e.BaseEmission = m.GetColor(EmissionId); }
                else continue;   // 억지로 바꾸지 않는다

                _current.Add(e);
            }
        }

        private void Apply()
        {
            for (int i = 0; i < _current.Count; i++)
            {
                var e = _current[i];
                if (e.Renderer == null) continue;
                e.Renderer.GetPropertyBlock(_block);
                if (e.HasRim)
                    _block.SetFloat(RimId, Mathf.Max(e.BaseRim, _weight));
                else if (e.HasEmission)
                    _block.SetColor(EmissionId,
                        Color.Lerp(e.BaseEmission, e.BaseEmission * _emissionBoost, Mathf.Clamp01(_weight)));
                e.Renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>원래 값으로 되돌린다. 안 되돌리면 한 번 조준한 물체가 영원히 빛난다.</summary>
        private void Restore()
        {
            for (int i = 0; i < _current.Count; i++)
            {
                var e = _current[i];
                if (e.Renderer == null) continue;
                e.Renderer.GetPropertyBlock(_block);
                if (e.HasRim) _block.SetFloat(RimId, e.BaseRim);
                else if (e.HasEmission) _block.SetColor(EmissionId, e.BaseEmission);
                e.Renderer.SetPropertyBlock(_block);
            }
            _current.Clear();
        }
    }
}
