// HHMouseInteract.cs — CloverPit 사용성: 세계의 물체를 마우스로 직접 조작한다.
//
// 레버를 클릭하면 당겨지고, 문을 클릭하면 출발한다. 키보드는 보조로 남는다(HHGame.Update).
// 호버하면 물체가 은은하게 빛나고(커스텀 셰이더 _EmissionColor 오버라이드), 커서 옆에 행동 라벨이 뜬다.
// 하이라이트는 MaterialPropertyBlock 이라 머티리얼 에셋을 건드리지 않는다 — 벗어나면 원상복구.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace HeavensHunger
{
    public class HHMouseInteract : MonoBehaviour
    {
        HHGame _game;
        Camera _cam;
        readonly List<Target> _targets = new List<Target>();
        Target _hover;
        MaterialPropertyBlock _mpb;
        TextMeshProUGUI _hint;
        RectTransform _hintRt;
        Canvas _hintCanvas;

        static readonly Color HoverGlow = new Color(0.30f, 0.24f, 0.14f, 1f); // 은은한 온기 — 블룸에 살짝 걸린다
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        class Target
        {
            public string label;
            public Collider col;
            public Renderer[] rends;
            public System.Action act;
        }

        void Start()
        {
            _game = FindFirstObjectByType<HHGame>();
            _cam = Camera.main;
            _mpb = new MaterialPropertyBlock();

            // 캐빈 전체에서 활성 인스턴스 우선으로 찾는다 — 원본 셸 복원 후 문은 원본, 레버는 복제가 활성이다.
            var cabRoot = GameObject.Find("CabinAD47");
            if (cabRoot == null || _game == null) { enabled = false; return; }
            var root = cabRoot.transform;

            Reg(root, "레버 — 당긴다", new[] { "SM_Lever_Handle.003", "SM_LeverBay" }, () => _game.DoLever());
            Reg(root, "문 — 출발", new[] { "SM_Door_L", "SM_Door_R" }, () => _game.DoDepart());

            BuildHint();
        }

        void Reg(Transform root, string label, string[] names, System.Action act)
        {
            foreach (var n in names)
            {
                Transform tr = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(false))
                    if (t.name == n) { tr = t; break; } // 활성만 (false = 비활성 제외)
                if (tr == null) continue;
                var col = tr.GetComponent<Collider>();
                if (col == null)
                {
                    var mf = tr.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = tr.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        col = mc;
                    }
                    else col = tr.gameObject.AddComponent<BoxCollider>();
                }
                _targets.Add(new Target { label = label, col = col, rends = tr.GetComponentsInChildren<Renderer>(), act = act });
            }
        }

        void BuildHint()
        {
            var go = new GameObject("HH_MouseHint", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            _hintCanvas = go.GetComponent<Canvas>();
            _hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hintCanvas.sortingOrder = 300;
            var t = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            t.transform.SetParent(go.transform, false);
            _hint = t.GetComponent<TextMeshProUGUI>();
            _hint.fontSize = 26;
            _hint.color = new Color(0.95f, 0.90f, 0.75f, 1f);
            _hint.alignment = TextAlignmentOptions.BottomLeft;
            _hint.raycastTarget = false;
            _hint.outlineWidth = 0.25f;
            _hintRt = (RectTransform)t.transform;
            _hintRt.sizeDelta = new Vector2(420, 40);
            _hint.enabled = false;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || _cam == null) return;
            Vector2 mp = mouse.position.ReadValue();

            // 물리용 투명 셸(Col_Wall_*)이 레이를 먹으므로 RaycastAll 로 뻔고,
            // 등록된 타겟 중 가장 가까운 것만 잡는다 — 충돌 셸은 클릭을 막지 않는다.
            Target hit = null;
            var ray = _cam.ScreenPointToRay(mp);
            var hs = Physics.RaycastAll(ray, 40f);
            float best = float.MaxValue;
            foreach (var h in hs)
                foreach (var t in _targets)
                    if (t.col == h.collider && h.distance < best) { best = h.distance; hit = t; }

            if (hit != _hover) { SetGlow(_hover, false); SetGlow(hit, true); _hover = hit; }

            if (_hint != null)
            {
                _hint.enabled = _hover != null;
                if (_hover != null)
                {
                    _hint.text = _hover.label;
                    _hintRt.position = new Vector3(mp.x + 22, mp.y + 6, 0);
                }
            }

            if (_hover != null && mouse.leftButton.wasPressedThisFrame) _hover.act();
        }

        void SetGlow(Target t, bool on)
        {
            if (t == null) return;
            foreach (var r in t.rends)
            {
                if (r == null) continue;
                if (on) { _mpb.Clear(); _mpb.SetColor(EmissionId, HoverGlow); r.SetPropertyBlock(_mpb); }
                else r.SetPropertyBlock(null);
            }
        }
    }
}
