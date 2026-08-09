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

        [Header("등장·퇴장 전환")]
        [Tooltip("구슬이 나타나고 사라지는 데 걸리는 시간(초). SetActive로 즉시 켜고 끄면 " +
            "팝인/팝아웃처럼 보인다 — 이 값만큼 스케일을 0↔1로 물려 부드럽게 만든다.")]
        [SerializeField, Min(0.01f)] private float _transitionDuration = 0.12f;

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

            /// <summary>목표 표시 상태. true면 결국 다 보여야 한다(Progress가 1을 향해 간다).</summary>
            public bool TargetOn;

            /// <summary>
            /// 등장/퇴장 전환 진행도. 0 = 완전히 숨김(스케일 0, 곧 비활성화) ·
            /// 1 = 완전히 보임. <see cref="ApplyHighlights"/> 에서 하이라이트 배율과
            /// **곱**해져 최종 localScale이 된다.
            /// </summary>
            public float Progress;
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
            EnsureSlots();
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
                    // 캐싱 시점의 실제 활성 상태를 그대로 시작 목표로 삼는다. 여기서
                    // 무조건 false(꺼짐)로 시작하면, 씬에 미리 켜진 채로 저작된 자식이
                    // 있어도 SetCell이 "달라진 게 없다"고 오판해 절대 끄지 않는다.
                    bool activeNow = child.gameObject.activeSelf;
                    slots[c] = new SymbolSlot
                    {
                        Child = child,
                        BaseScale = child.localScale,
                        // 비활성 자식도 포함한다 — 심볼 하위 전체가 부모와 함께 켜지고 꺼진다.
                        Renderers = child.GetComponentsInChildren<Renderer>(true),
                        TargetOn = activeNow,
                        Progress = activeNow ? 1f : 0f,
                    };
                }
                _slots[i] = slots;
            }
        }

        /// <summary>
        /// `_slots` 가 비어 있으면 채운다.
        ///
        /// 평소엔 `Awake` 가 먼저 채워 둔다. 그런데 `ShowBoard` 는 Play 모드 없이도
        /// 호출 가능해야 하는 공개 API이고(그 메서드의 주석 참조), 에디트 모드에서는
        /// `Awake` 자체가 불리지 않는다(`ExecuteAlways` 가 아니므로). `EyeLevelCapture.cs`
        /// 처럼 씬을 열자마자 Play 모드 없이 `ShowBoard` 를 부르는 에디터 툴이 실제로
        /// 있어서, 여기서 지연 초기화를 해 주지 않으면 `_slots` 가 null 인 채로
        /// 아무 일도 못 하고 조용히 아무것도 안 그린다.
        /// </summary>
        private void EnsureSlots()
        {
            if (_slots == null) CacheSlots();
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// **진단 전용 게이트.** 0 = 정상 · 1 = 전환·하이라이트(`ApplyTransitions`·
        /// `ApplyHighlights`)만 건너뜀 · 2 = `Update` 전체 건너뜀.
        ///
        /// 소거 측정이 이 컴포넌트를 1,638 B 의 전부로 지목했는데, `ApplyHighlights` 를
        /// 세 가지 방법으로 고쳐도 수치가 안 움직였다. 컴포넌트 단위로는 더 못 좁힌다.
        /// **안을 갈라서 재야 한다** — 어느 쪽이 쓰는지 추측하지 않고.
        /// 기본값 0 이라 켜지 않으면 아무 영향이 없다.
        ///
        /// 2026-08-09: 구슬 등장/퇴장 전환(`ApplyTransitions`)을 추가하면서 레벨 1의
        /// 대상에 포함시켰다 — 둘 다 "매 프레임 도는 시각 폴리싱"이라는 같은 범주라
        /// 따로 쪼갤 이유가 없었다. 필요해지면 그때 레벨을 하나 더 만든다.
        /// </summary>
        public static int DiagnosticSkip;
#endif

        private void Update()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (DiagnosticSkip >= 2) return;
            if (DiagnosticSkip < 1) ApplyHighlights(ApplyTransitions());
#else
            ApplyHighlights(ApplyTransitions());
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

            // 🔴 **`FinalBoard` 에서 `InitialBoard` 로 바꿨다** (2026-08-09 사용자 지시:
            //    「슬롯 돌아가고 나서 바로 사라져서 어색하다. 다음 슬롯 돌리기 전까지는
            //    계속 남아 있어야 할 것 같다」).
            //
            //    사라지는 것은 결함이 아니라 **설계였다.** 캐스케이드는 맞은 심볼을
            //    지우는 것이고, `FinalBoard` 는 그 뒤의 판이라 잘 맞을수록 판이 비었다.
            //    「크게 이겼다」와 「아무것도 없다」가 같은 그림이 되는 것이 문제다 —
            //    성과가 클수록 화면이 허전해지는 표시는 거꾸로 된 피드백이다.
            //
            //    그래서 쉬는 동안에는 **떨어진 판**을 그대로 둔다. 무엇이 나왔는지가
            //    다음 레버를 당길 때까지 남는다.
            //
            // ⚠ 대신 **캐스케이드로 무엇이 사라졌는지가 화면에서 빠진다.** 그 정보는
            //   모듈이 밝아지는 연출(사용자 요청)이 대신 져야 한다 — 그것이 붙기
            //   전까지는 「어느 칸이 전력이 됐는가」를 판에서 읽을 수 없다.
            _cleared = false;   // 판을 그렸으니 다음엔 다시 비울 수 있어야 한다
            ShowBoard(floor.History[spins - 1].InitialBoard);
        }

        /// <summary>
        /// 주어진 판을 그대로 표시한다. 에디터 캡처와 테스트가 Play 모드 없이
        /// 결과판을 세울 수 있어야 해서 공개한다 — Update는 에디트 모드에서 돌지 않는다.
        ///
        /// 그래서 <see cref="SetCell"/> 은 `Application.isPlaying` 이 거짓이면 등장/퇴장
        /// 전환을 건너뛰고 즉시 최종 모습으로 스냅한다 — Update가 안 도는 호출자에게
        /// "다음 프레임을 기다리면 마저 그려진다"는 약속을 할 수 없기 때문이다.
        /// </summary>
        public void ShowBoard(SpinBoard board)
        {
            int salt = BoardSalt(board);
            for (int i = 0; i < SpinBoard.Cells && i < _cells.Length; i++)
                SetCell(i, board[i], VariantOf(salt, i));
        }

        /// <summary>
        /// 같은 종에 얼굴이 여럿일 때 어느 것을 켤지 정하는 값. **판 전체에서 유도한다.**
        ///
        /// ⚠ `Random` 을 쓰지 않는다. 이 저장소는 고정 시드로 같은 그림이 다시 나오는 것을
        ///   회귀 판정의 근거로 쓴다(`Captures/baseline.txt`). 뷰가 프레임마다 다른 얼굴을
        ///   고르면 같은 판을 두 번 그려도 캡처가 달라져 그 근거가 사라진다.
        ///   판이 같으면 얼굴도 같고, 판이 바뀌면 얼굴도 바뀐다.
        /// </summary>
        private static int BoardSalt(SpinBoard board)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < SpinBoard.Cells; i++) h = h * 31 + (int)board[i];
                return h;
            }
        }

        /// <summary>
        /// ⚠ 혼합이 약하면 한 판에서 같은 얼굴이 몰린다 — 첫 판본(`salt ^ index*φ` +
        ///   1단 시프트)은 아홉 칸 중 넷이 같은 얼굴로 나왔고 사과는 한 번도 안 나왔다.
        ///   칸 아홉은 표본이 작아서 눈사태 성질이 나쁘면 그대로 보인다.
        ///   murmur3 의 최종 혼합(finalizer)을 쓴다.
        /// </summary>
        private static int VariantOf(int salt, int index)
        {
            unchecked
            {
                uint h = (uint)salt + 0x9E3779B9u * (uint)(index + 1);
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
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
                SetCell(i, SymbolKind.Empty);
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
        /// 등장/퇴장 스케일 전환을 매 프레임 목표(Progress 0 또는 1)로 수렴시킨다.
        ///
        /// `localScale` 대입은 여기서 하지 않는다 — `ApplyHighlights` 한 곳에서만 쓴다.
        /// 두 메서드가 같은 `Transform.localScale`을 따로 쓰면 나중에 실행되는 쪽이
        /// 앞선 쪽 결과를 덮어써서, 정화 맥동 중에 전환이 겹치면 둘 중 하나가 사라져
        /// 보인다. 대신 "이번 프레임에 뭔가 실제로 움직였는가"만 돌려주고,
        /// `ApplyHighlights` 의 "달라진 게 없으면 건너뛴다" 게이트에 그 결과를 끼워
        /// 넣는다 — 하이라이트 값 자체는 그대로여도 전환 때문에 다시 칠해야 할 수
        /// 있기 때문이다.
        ///
        /// 0 B 확인: 배열(`_slots`, `SymbolSlot[]`)만 인덱스 `for`로 돈다. `foreach`도
        /// 컬렉션 인터페이스도 없으니 열거자 할당이 생길 자리가 없다. `Mathf.MoveTowards`
        /// 는 구조체 인자만 받는 정적 함수라 할당하지 않는다.
        /// </summary>
        private bool ApplyTransitions()
        {
            if (_slots == null) return false;

            float step = Time.deltaTime / _transitionDuration;
            if (step <= 0f) return false; // 일시정지 등 — 이번 프레임엔 진행할 시간이 없다

            bool changed = false;
            for (int i = 0; i < _slots.Length; i++)
            {
                SymbolSlot[] slots = _slots[i];
                for (int s = 0; s < slots.Length; s++)
                {
                    SymbolSlot slot = slots[s];
                    if (slot.Child == null) continue;

                    float target = slot.TargetOn ? 1f : 0f;
                    if (slot.Progress == target) continue;

                    slot.Progress = Mathf.MoveTowards(slot.Progress, target, step);
                    slots[s] = slot;
                    changed = true;

                    // 완전히 줄어든 순간에야 끈다 — "꺼진다"가 아니라 "줄어들다가
                    // 사라진다"로 보이게 하는 것이 이 전환의 목적이다.
                    if (slot.Progress <= 0f && !slot.TargetOn)
                        slot.Child.gameObject.SetActive(false);
                }
            }
            return changed;
        }

        /// <summary>
        /// 정화 점등과 등장/퇴장 전환을 칸에 함께 바른다.
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
        ///
        /// <paramref name="transitioning"/>: 등장/퇴장 전환(<see cref="ApplyTransitions"/>)이
        /// 이번 프레임에 뭔가를 움직였는가. 하이라이트 값 자체는 안 바뀌었어도 전환이
        /// 진행 중이면 최종 스케일이 달라지므로, 그 경우엔 건너뛰지 않는다.
        /// </summary>
        private void ApplyHighlights(bool transitioning)
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
                if (!changed && !transitioning) return;
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

                    // 저작된 스케일에 하이라이트 배율과 전환 진행도를 **곱**한다. 셋 중
                    // 하나라도 대입(=)으로 덮으면 나머지의 효과가 사라진다 — 전환만
                    // 곱하면 정화 맥동이 죽고, 하이라이트만 곱하면 등장 애니메이션이
                    // 매 프레임 원래 크기로 스냅해 버린다.
                    slot.Child.localScale = slot.BaseScale * scale * slot.Progress;

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

        /// <summary>
        /// 칸 하나를 지정한 심볼로 맞춘다 — **목표만 정한다.**
        ///
        /// 예전엔 여기서 곧바로 `SetActive` 로 켜고 껐다. 그게 사용자가 지적한
        /// "룰렛 돌릴 때 구슬이 새로 생기고 사라지는 게 어색해"의 원인이었다 —
        /// 팝인/팝아웃.
        ///
        /// 이제 켜지는 슬롯만 여기서 즉시 `SetActive(true)` 하고, 실제로 자라나는 건
        /// `Update` 의 `ApplyTransitions` 가 시간에 걸쳐 한다. 꺼지는 슬롯은 `SetActive`
        /// 조차 여기서 안 부른다 — 스케일이 0 까지 줄어든 **뒤**에 `ApplyTransitions`
        /// 가 끈다. 즉시 끄면 예전과 똑같이 팝아웃한다.
        ///
        /// 단, `Application.isPlaying` 이 거짓이면(에디터 캡처 등) 전환을 기다려 줄
        /// `Update` 자체가 돌지 않으므로 트랜지션을 건너뛰고 즉시 최종 모습으로
        /// 스냅한다 — `ShowBoard` 의 계약("Update 없이도 결과판을 세울 수 있다")을
        /// 지키기 위해서다.
        ///
        /// **고정 캡처 중에도 스냅한다** (`Time.captureDeltaTime > 0`).
        /// `TenFloorCaptureRig` 은 `ShowBoard` 뒤 `WaitFrames(3)` 만 기다리고 찍는데,
        /// `captureDeltaTime` 이 1/60 이라 3 프레임은 0.05 초다 — 0.12 초 전환의 42% 다.
        /// 즉 기준 이미지에 **덜 자란 구슬**이 박힌다.
        ///
        /// `captureDeltaTime` 이 결정론을 세우므로 그 값 자체는 매번 같고, 그래서
        /// 캡처가 흔들리지는 **않는다** — 흔들리는 게 아니라 **일관되게 틀린다.**
        /// 재현되는 오류가 재현되지 않는 오류보다 낫지만, 기준 이미지는 정착한 판이어야
        /// 한다. 대기 프레임을 늘려 맞추는 방법도 있었지만 그러면 전환 시간을 바꿀 때마다
        /// 캡처 하네스를 따라 고쳐야 한다 — 두 곳이 조용히 어긋날 자리를 만드는 셈이다.
        ///
        /// 대가: 전환 자체를 캡처로 남길 수 없다. 지금은 그럴 이유가 없다.
        ///
        /// 0 B 확인: `_slots[index]` 배열만 인덱스 `for`로 돈다. `child.name`(→
        /// `KindOf`)은 실제로 판이 바뀔 때만 부른다 — 이 메서드 자체가 `Update`에서
        /// 매 프레임이 아니라 스핀/층이 바뀐 순간에만 호출된다(호출부인 `Update` 참고).
        /// </summary>
        private void SetCell(int index, SymbolKind kind) => SetCell(index, kind, 0);

        /// <summary>
        /// <paramref name="variant"/> — 한 종에 **얼굴이 여럿일 때** 어느 것을 켤지.
        ///
        /// 🔴 2026-08-09, 사용자 지시 「만든 오브젝트 전부 룰렛에 돌아가게」로 칸마다
        ///    자식이 3개에서 **9개**가 됐다(정상 영혼의 얼굴이 사과 포함 일곱).
        ///    예전 코드는 「종이 같으면 켠다」였는데 그대로 두면 **한 칸에 일곱이 겹쳐**
        ///    뜬다. 같은 종의 자식 중 하나만 골라야 한다.
        ///
        /// ⚠ 고르는 값은 판에서 유도한다(<see cref="BoardSalt"/>). 난수를 쓰면 같은 판을
        ///   두 번 그릴 때 얼굴이 달라져 고정 캡처 회귀 판정의 근거가 사라진다.
        /// </summary>
        private void SetCell(int index, SymbolKind kind, int variant)
        {
            EnsureSlots();
            if (_slots == null || index < 0 || index >= _slots.Length) return;

            bool animate = Application.isPlaying && Time.captureDeltaTime <= 0f;
            SymbolSlot[] slots = _slots[index];

            // 이 종의 얼굴이 몇 개인지 먼저 센다. 하나뿐이면(저항체 둘) 예전과 같다.
            int faces = 0;
            if (kind != SymbolKind.Empty)
            {
                for (int s = 0; s < slots.Length; s++)
                    if (slots[s].Child != null && KindOf(slots[s].Child.name) == kind) faces++;
            }
            int pick = faces > 1 ? variant % faces : 0;
            int seen = 0;

            for (int s = 0; s < slots.Length; s++)
            {
                SymbolSlot slot = slots[s];
                if (slot.Child == null) continue;

                SymbolKind childKind = KindOf(slot.Child.name);
                bool on = childKind != SymbolKind.Empty && childKind == kind;
                if (on)
                {
                    on = seen == pick;
                    seen++;
                }
                // 이미 그 목표를 향해 있다 — 다시 만지면(예: 연출자가 같은 판을 반복
                // 밀어 넣을 때) 진행 중이던 전환이 처음부터 다시 시작돼 버린다.
                if (slot.TargetOn == on) continue;

                slot.TargetOn = on;

                if (!animate)
                {
                    // Update가 돌지 않는 호출 — 다음 프레임을 기다릴 수 없으니 트랜지션
                    // 없이 바로 최종 모습으로 스냅한다(예전 SetCell과 같은 결과).
                    slot.Progress = on ? 1f : 0f;
                    slot.Child.gameObject.SetActive(on);
                    float amount = index < _highlight.Length ? Quantize(_highlight[index]) : 0f;
                    float highlightScale = Mathf.Lerp(1f, _highlightScale, amount);
                    slot.Child.localScale = slot.BaseScale * highlightScale * slot.Progress;
                }
                else if (on)
                {
                    // 켜질 때: 즉시 보이게 하고, 실제로 자라나는 건 ApplyTransitions가 한다.
                    // Progress를 0으로 되돌리는 즉시 스케일도 0으로 맞춰 둔다 — 안 그러면
                    // 이번 프레임엔 아직 ApplyHighlights가 다시 돌지 않아서, 이 자식이
                    // 마지막으로 칠해졌을 때의(어쩌면 원래 크기인) 스케일이 한 프레임
                    // 그대로 노출된다 — 자라나기 전에 잠깐 원래 크기로 튀어 보이는 것이다.
                    slot.Child.gameObject.SetActive(true);
                    slot.Progress = 0f;
                    slot.Child.localScale = Vector3.zero;
                }
                // else (꺼질 때, animate): 여기서는 SetActive(false)를 부르지 않는다.
                // Progress도 건드리지 않는다 — 지금 값(대개 1에 가까움)에서부터
                // ApplyTransitions가 0까지 줄이고, 0에 닿는 순간 SetActive(false)한다.

                slots[s] = slot;
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
                // 정상 영혼의 **다른 얼굴들** (뼈·심장·어금니·새싹·뇌·손뼈).
                // 판정은 늘어나지 않는다 — 화면에만 아홉이 돈다.
                // 접두사 규약은 `AscendSlotSymbolSwap.SoulVariants` 와 한 쌍이다.
                default:
                    return childName != null && childName.StartsWith("Sym_Soul_")
                        ? SymbolKind.NormalSoul
                        : SymbolKind.Empty;
            }
        }
    }
}
