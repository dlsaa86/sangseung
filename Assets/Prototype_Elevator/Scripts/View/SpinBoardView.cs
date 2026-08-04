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
    ///   정상 영혼 — 매끈한 구 하나, 지름 0.168 (가장 크다)
    ///   흡수체   — 뾰족한 다면체 하나, 스파이크 끝 지름 0.124
    ///   증식체   — 작은 덩어리 **셋**, 조각 지름 0.058 (가장 작다)
    /// 색 외 차이가 넷이다 — 형태 종류 · 크기 · **개수** · 표면 각짐.
    /// 형상 정의는 `SymbolShapeFactory` 한 곳에만 있다(`UP-FIX-82`).
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
        /// 캐시가 필요한 이유: 하이라이트가 저작된 스케일을 모른 채 스케일을 1로
        /// 덮어쓰면 심볼이 부풀어 결과판을 통째로 가린다. 실제로 첫 캡처에서 그렇게 나왔다.
        /// 지금은 조각들이 전부 스케일 1 로 저작되지만(크기는 메시에 구워져 있다)
        /// 핵은 0.52 다 — 「전부 1 이니까 캐시가 필요 없다」는 참이 아니다.
        /// </summary>
        private struct SymbolSlot
        {
            public Transform Child;
            public Vector3 BaseScale;

            /// <summary>
            /// 이 심볼이 쓰는 렌더러 **전부**. 하나가 아니다.
            ///
            /// 🔴 예전엔 `child.GetComponent&lt;Renderer&gt;()` 하나였고, 그것이
            /// `UP-FIX-82` 의 새 형상에서 조용히 깨졌다 — 증식체는 최상위에 렌더러가
            /// 없는 **빈 부모 + 조각 셋**이고, 정상 영혼·흡수체도 껍질 안에 핵을 갖는다.
            /// 하나만 잡으면 증식체는 정화 점등이 통째로 사라지고(부모 렌더러 null)
            /// 나머지 둘은 핵만 안 빛난다. 「점등이 왜 어떤 칸에서만 안 보이지」는
            /// 원인 추적이 가장 오래 걸리는 종류의 결함이라, 계약을 배열로 바꾼다.
            /// </summary>
            public Renderer[] Renderers;
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
                        // 비활성 자식도 포함한다 — 심볼 하위 전체가 부모와 함께 켜지고 꺼진다.
                        Renderers = child.GetComponentsInChildren<Renderer>(true),
                    };
                }
                _slots[i] = slots;
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// **진단 전용 게이트.** 0 = 정상 · 1 = `ApplyHighlights` 만 건너뜀 ·
        /// 2 = `Update` 전체 건너뜀.
        ///
        /// 소거 측정이 이 컴포넌트를 1,638 B 의 전부로 지목했는데, `ApplyHighlights` 를
        /// 세 가지 방법으로 고쳐도 수치가 안 움직였다. 컴포넌트 단위로는 더 못 좁힌다.
        /// **안을 갈라서 재야 한다** — 어느 쪽이 쓰는지 추측하지 않고.
        /// 기본값 0 이라 켜지 않으면 아무 영향이 없다.
        /// </summary>
        public static int DiagnosticSkip;
#endif

        private void Update()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (DiagnosticSkip >= 2) return;
            if (DiagnosticSkip < 1) ApplyHighlights();
#else
            ApplyHighlights();
#endif
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
            _cleared = false;   // 판을 그렸으니 다음엔 다시 비울 수 있어야 한다
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

        /// <summary>이미 비어 있는가. 같은 일을 매 프레임 다시 하지 않기 위한 것이다.</summary>
        private bool _cleared;

        public void ClearAll()
        {
            // **이미 비었으면 아무것도 하지 않는다.**
            //
            // 이것이 `UP-TECH-05`(워밍업 후 매 프레임 0 B)를 혼자서 위반하고 있었다.
            // 빌드 소거 측정이 `SpinBoardView` 를 1,638 B 전부로 지목했고,
            // 그 안을 다시 갈라 재니 `ApplyHighlights` 의 기여는 **0** 이었다 —
            // 세 번의 수정이 전부 엉뚱한 함수를 고치고 있었다. 남은 건 이 경로다.
            //
            // 왜 매 프레임 도는가: `Update` 는 층 세션이 없거나 스핀이 0 이면 여기로 온다.
            // 그런데 이 함수가 끝에서 `_lastSpinCount = -1` 로 되돌려 **다음 프레임의
            // 「바뀐 게 없으면 건너뛴다」 검사를 반드시 실패시킨다.** 스스로 자기를 다시
            // 부르게 만드는 구조였다.
            //
            // 비용의 정체는 `SetCell` 의 `foreach (Transform child in cell)` 안에서
            // 읽는 `child.name` 이다. Unity 의 `Object.name` 게터는 **호출마다 새 문자열을
            // 만든다.** 9칸 × 자식 × 약 50 B 가 1,638 B 와 맞는다.
            if (_cleared) return;

            ClearHighlights();
            for (int i = 0; i < _cells.Length; i++)
                SetCell(_cells[i], SymbolKind.Empty);
            _lastSpinCount = -1;
            _lastFloor = -1;
            _cleared = true;
        }

        /// <summary>
        /// 하이라이트를 크기와 발광 **양쪽**에 건다. 발광만 쓰면 회색조에서 사라지고,
        /// 크기만 쓰면 밝은 장면에서 묻힌다(visual-criteria B-2.6).
        /// </summary>
        /// <summary>
        /// 점등 세기를 **32 단계로 끊는다.**
        ///
        /// 감쇠는 연속이라 매 프레임 값이 조금씩 달라지고, 그러면 「바뀌었으니 다시 칠한다」가
        /// 매 프레임 참이 되어 `SetPropertyBlock` 27번이 계속 돈다 — 그것이 1,638 B 다.
        /// 값을 끊으면 **대부분의 프레임에서 직전과 같은 값**이 나오고 통째로 건너뛴다.
        ///
        /// 32 단계는 눈에 안 보인다. 점등은 0→1 을 0.55 초에 지나가므로 한 단계가
        /// 약 17 ms 이고, 그건 프레임 하나 남짓이다. 화면은 그대로고 쓰기만 사라진다.
        /// </summary>
        private static float Quantize(float amount) => Mathf.Round(amount * 32f) / 32f;

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
                    if (Mathf.Abs(Quantize(_highlight[i]) - _highlightApplied[i]) > 0.0001f) { changed = true; break; }
                if (!changed) return;
            }
            else _highlightApplied = new float[_highlight.Length];

            for (int i = 0; i < _highlight.Length; i++) _highlightApplied[i] = Quantize(_highlight[i]);

            for (int i = 0; i < _slots.Length; i++)
            {
                float amount = Quantize(_highlight[i]);
                float scale = Mathf.Lerp(1f, _highlightScale, amount);

                SymbolSlot[] slots = _slots[i];
                for (int s = 0; s < slots.Length; s++)
                {
                    SymbolSlot slot = slots[s];
                    if (slot.Child == null || !slot.Child.gameObject.activeSelf) continue;

                    // 저작된 스케일에 **곱한다**. 덮어쓰지 않는다.
                    slot.Child.localScale = slot.BaseScale * scale;

                    if (slot.Renderers == null) continue;
                    // **`GetPropertyBlock` 을 부르지 않는다.** 그것이 할당의 실제 출처다.
                    //
                    // 처음엔 「매 프레임 무조건 도는 것」이 원인인 줄 알고 값이 안 바뀌면
                    // 건너뛰게 했다. 그것만으로는 부족했다 — 유휴 국면에서는 0 B 가 됐지만
                    // **정화 연출이 실제로 도는 동안에는 1,638 B 가 그대로**였다.
                    // 즉 원인은 「얼마나 자주 부르는가」가 아니라 **부르는 것 자체**였다.
                    //
                    // 이 슬롯에 블록을 쓰는 곳은 여기뿐이고 우리가 쓰는 프로퍼티도
                    // `_EmissionColor` 하나다. 기존 값을 읽어 올 이유가 없다 —
                    // 읽어 오는 그 호출이 매번 새 저장소를 만든다.
                    _block.SetColor(EmissionColorId, _purifyEmission * (_purifyEmissionStrength * amount));
                    Renderer[] rs = slot.Renderers;
                    for (int k = 0; k < rs.Length; k++)
                        if (rs[k] != null) rs[k].SetPropertyBlock(_block);
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
