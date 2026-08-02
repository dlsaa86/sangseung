using UnityEngine;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 3×3 결과판을 실제 공간에 표시한다.
    ///
    /// 여기가 없으면 게임이 화면에 존재하지 않는다. 판정 엔진과 밸런스가 아무리 정확해도
    /// 플레이어가 보는 것은 빈 판때기 세 장이다.
    ///
    /// 심볼은 색이 아니라 **형태**로 구분한다. 시각 기준 B-2.5가 "회색조로 바꿨을 때
    /// 구분이 사라지면 실패"라고 요구하고, 이 씬은 지금 사실상 무채색이라 색에 기대면
    /// 아무것도 구분되지 않는다.
    ///   정상 영혼 — 구
    ///   흡수체   — 정육면체
    ///   증식체   — 캡슐(세로로 긴 알약)
    /// 실루엣이 셋 다 다르다.
    ///
    /// 갱신 주체가 둘이다:
    ///   · <see cref="SpinPresenter"/>가 붙으면 연출자가 단계별로 밀어 넣는다.
    ///   · 없으면 이 클래스가 최종 보드를 스스로 따라간다(연출 없이도 게임은 돌아야 한다).
    /// </summary>
    public sealed class SpinBoardView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;

        /// <summary>9칸. SpinBoard.Index(column, row) 순서를 그대로 따른다.</summary>
        [SerializeField] private Transform[] _cells = new Transform[SpinBoard.Cells];

        [Header("정화 하이라이트")]
        [Tooltip("맥동 최대 배율.")]
        [SerializeField, Min(1f)] private float _highlightScale = 1.35f;
        /// <summary>
        /// 정화 점등 색.
        ///
        /// **한 번 차가운 청록 `(0.62, 0.82, 0.78)` × 2 로 바꿨다가 되돌렸다 (2026-08-02).**
        /// 이유는 「`21` 의 금색 화소를 줄인다」였는데 **그 전제가 틀렸다** —
        /// 바꾼 뒤에도 금색이 36,716 → 36,716 으로 화소 하나까지 같았고,
        /// 실제 출처는 `RiskStateView` 가 런타임에 거는 **천장등 발광**이었다
        /// (`PD-22`). 정화 점등은 그 장에 아예 없다.
        ///
        /// 근거가 사라진 변경을 남겨 두면 다음 시각 판정이 **무엇 때문에 달라졌는지**
        /// 를 못 가른다. 그래서 원래 값으로 되돌린다. 팔레트 락 관점의 재검토는
        /// 별개 항목으로 다루고, 그때는 목적과 측정을 먼저 맞춘다.
        /// </summary>
        [SerializeField] private Color _purifyEmission = new Color(1f, 0.86f, 0.55f);
        [SerializeField, Min(0f)] private float _purifyEmissionStrength = 3f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>칸별 하이라이트 세기(0~1). 연출자가 프레임마다 갱신한다.</summary>
        private readonly float[] _highlight = new float[SpinBoard.Cells];

        /// <summary>
        /// 칸별 심볼 자식과 **씬에서 저작된 원래 스케일**.
        ///
        /// 캐시가 필요한 이유: 심볼들은 저마다 다른 크기로 배치돼 있다(구 0.17 / 정육면체 0.16 /
        /// 캡슐 0.15 — 실루엣 대비를 위한 값이다). 하이라이트가 이걸 모른 채 스케일을 1로
        /// 덮어쓰면 심볼이 6배로 부풀어 결과판을 통째로 가린다. 실제로 첫 캡처에서 그렇게 나왔다.
        /// </summary>
        private struct SymbolSlot
        {
            public Transform Child;
            public Vector3 BaseScale;
            public Renderer Renderer;
        }

        private SymbolSlot[][] _slots;
        private MaterialPropertyBlock _block;
        private int _lastSpinCount = -1;
        private int _lastFloor = -1;

        /// <summary>연출자가 붙어 있으면 이 뷰는 스스로 보드를 바꾸지 않는다.</summary>
        public bool DrivenExternally { get; set; }

        /// <summary>칸 하나의 Transform. 연출자가 패턴 마커를 놓을 좌표를 얻는다.</summary>
        public Transform CellTransform(int index)
            => index >= 0 && index < _cells.Length ? _cells[index] : null;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            CacheSlots();
            ClearAll();
        }

        private void CacheSlots()
        {
            _slots = new SymbolSlot[_cells.Length][];
            for (int i = 0; i < _cells.Length; i++)
            {
                Transform cell = _cells[i];
                if (cell == null) { _slots[i] = System.Array.Empty<SymbolSlot>(); continue; }

                var slots = new SymbolSlot[cell.childCount];
                for (int c = 0; c < cell.childCount; c++)
                {
                    Transform child = cell.GetChild(c);
                    slots[c] = new SymbolSlot
                    {
                        Child = child,
                        BaseScale = child.localScale,
                        Renderer = child.GetComponent<Renderer>(),
                    };
                }
                _slots[i] = slots;
            }
        }

        private void Update()
        {
            ApplyHighlights();
            if (DrivenExternally) return;

            FloorSession floor = _run != null && _run.Session != null ? _run.Session.Current : null;
            if (floor == null) { ClearAll(); return; }

            int spins = floor.History != null ? floor.History.Count : 0;
            int floorNo = floor.Plan.Floor;

            // 스핀이 늘었거나 층이 바뀐 순간에만 갱신한다. 매 프레임 9칸을 다시 그리면
            // 아무것도 안 바뀌었는데 GetComponent가 계속 돈다.
            if (spins == _lastSpinCount && floorNo == _lastFloor) return;
            _lastSpinCount = spins;
            _lastFloor = floorNo;

            if (spins == 0) { ClearAll(); return; }

            // 캐스케이드까지 끝난 뒤의 판을 보여준다. 비워진 칸은 "정화됐다"는 뜻이고,
            // 남아 있는 저항체가 곧 다음 스핀으로 넘어갈 위험이다.
            ShowBoard(floor.History[spins - 1].FinalBoard);
        }

        /// <summary>
        /// 주어진 판을 그대로 표시한다. 에디터 캡처와 테스트가 Play 모드 없이
        /// 결과판을 세울 수 있어야 해서 공개한다 — Update는 에디트 모드에서 돌지 않는다.
        /// </summary>
        public void ShowBoard(SpinBoard board)
        {
            for (int i = 0; i < SpinBoard.Cells && i < _cells.Length; i++)
                SetCell(_cells[i], board[i]);
        }

        /// <summary>칸 하나의 하이라이트 세기(0~1). 연출자가 맥동을 만든다.</summary>
        public void SetHighlight(int index, float amount)
        {
            if (index < 0 || index >= _highlight.Length) return;
            _highlight[index] = Mathf.Clamp01(amount);
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < _highlight.Length; i++) _highlight[i] = 0f;
        }

        public void ClearAll()
        {
            ClearHighlights();
            for (int i = 0; i < _cells.Length; i++)
                SetCell(_cells[i], SymbolKind.Empty);
            _lastSpinCount = -1;
            _lastFloor = -1;
        }

        /// <summary>
        /// 하이라이트를 크기와 발광 **양쪽**에 건다. 발광만 쓰면 회색조에서 사라지고,
        /// 크기만 쓰면 밝은 장면에서 묻힌다(visual-criteria B-2.6).
        /// </summary>
        /// <summary>직전에 실제로 칠한 하이라이트 값. 같은 값을 다시 쓰지 않기 위한 것이다.</summary>
        private float[] _highlightApplied;

        /// <summary>
        /// 정화 점등을 칸에 바른다.
        ///
        /// **값이 바뀐 칸만 바른다.** 이전에는 매 프레임 27개 슬롯 전부에
        /// `GetPropertyBlock`/`SetPropertyBlock` 을 걸었고, 그것이 **`UP-TECH-05`
        /// (워밍업 후 매 프레임 0 B)를 혼자서 위반하고 있었다.**
        ///
        /// 빌드 소거 측정이 지목했다 — 36개 컴포넌트 중 이것 하나가 **1,638 B/프레임
        /// 전부**였다. 27 슬롯 × 약 60 B 가 그 수와 맞는다. 정적으로 찾으려 했을 때는
        /// 못 찾았다(Update 를 가진 파일이 48개다). **끄고 재서** 찾았다.
        ///
        /// 하이라이트는 정화 연출 중에만 움직이고 나머지 시간에는 전부 0 이다.
        /// 즉 대부분의 프레임에서 이 27번의 왕복은 **같은 값을 다시 쓰는 것**이었다.
        /// </summary>
        private void ApplyHighlights()
        {
            if (_slots == null) return;

            // 바뀐 것이 없으면 통째로 건너뛴다. 부동소수 비교에 허용오차를 두는 이유는
            // 감쇠가 0 에 점근하면서 마지막 몇 프레임이 1e-8 씩 달라지기 때문이다 —
            // 그걸 「바뀌었다」로 세면 연출이 끝난 뒤에도 매 프레임 칠하게 된다.
            if (_highlightApplied != null && _highlightApplied.Length == _highlight.Length)
            {
                bool changed = false;
                for (int i = 0; i < _highlight.Length; i++)
                    if (Mathf.Abs(_highlight[i] - _highlightApplied[i]) > 0.0005f) { changed = true; break; }
                if (!changed) return;
            }
            else _highlightApplied = new float[_highlight.Length];

            for (int i = 0; i < _highlight.Length; i++) _highlightApplied[i] = _highlight[i];

            for (int i = 0; i < _slots.Length; i++)
            {
                float amount = _highlight[i];
                float scale = Mathf.Lerp(1f, _highlightScale, amount);

                SymbolSlot[] slots = _slots[i];
                for (int s = 0; s < slots.Length; s++)
                {
                    SymbolSlot slot = slots[s];
                    if (slot.Child == null || !slot.Child.gameObject.activeSelf) continue;

                    // 저작된 스케일에 **곱한다**. 덮어쓰지 않는다.
                    slot.Child.localScale = slot.BaseScale * scale;

                    if (slot.Renderer == null) continue;
                    slot.Renderer.GetPropertyBlock(_block);
                    _block.SetColor(EmissionColorId, _purifyEmission * (_purifyEmissionStrength * amount));
                    slot.Renderer.SetPropertyBlock(_block);
                }
            }
        }

        private static void SetCell(Transform cell, SymbolKind kind)
        {
            if (cell == null) return;
            foreach (Transform child in cell)
            {
                SymbolKind childKind = KindOf(child.name);
                bool on = childKind != SymbolKind.Empty && childKind == kind;
                if (child.gameObject.activeSelf != on) child.gameObject.SetActive(on);
            }
        }

        /// <summary>자식 오브젝트 이름으로 어떤 심볼인지 정한다. 배치 스크립트와 규약이 같아야 한다.</summary>
        public static SymbolKind KindOf(string childName)
        {
            switch (childName)
            {
                case "Sym_NormalSoul":   return SymbolKind.NormalSoul;
                case "Sym_Absorber":     return SymbolKind.Absorber;
                case "Sym_Proliferator": return SymbolKind.Proliferator;
                default:                 return SymbolKind.Empty;
            }
        }
    }
}
