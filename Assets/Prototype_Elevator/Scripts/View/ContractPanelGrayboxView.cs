using System.Text;
using TMPro;
using UnityEngine;
using Ascend.Prototype.Diagnostics;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// UP-DEVICE-07 그레이박스 — 계약 패널의 **물리 오브젝트 쪽 시각 표현**만 맡는다.
    ///
    /// ## 이 스크립트가 하지 않는 것 — 상호작용
    ///
    /// 클릭 감지·선택지 넘기기는 이미 구현돼 있다. 새로 만들지 않는다 —
    ///
    ///   1. `Ascend.Prototype.Player.InteractableContractPanel`
    ///      (`Scripts/Player/InteractableContractPanel.cs:7`)가
    ///      `Ascend.Prototype.Player.IInteractable`
    ///      (`Scripts/Player/IInteractable.cs:8`)을 구현한 상호작용 스텁이다.
    ///      크로스헤어가 이 컴포넌트를 클릭하면 `Interact()`가 `onOpened`
    ///      (`UnityEngine.Events.UnityEvent`)를 발행한다(같은 파일 29~35줄).
    ///   2. `Ascend.Prototype.Run.RouletteInteractionBridge`
    ///      (`Scripts/Run/RouletteInteractionBridge.cs:110`)가 그 `onOpened`를
    ///      `OnContractPanelPressed`에 구독해 두었고, 그 핸들러가
    ///      `_previewIndex = (_previewIndex + 1) % choices.Length`
    ///      (같은 파일 250줄)로 미리보기 칸을 돌린다. 확정은 이 브리지가 아니라
    ///      실행 레버 쪽 핸들러가 `run.SelectContract(_previewIndex)`
    ///      (같은 파일 262줄)로 한다 — "계약 패널은 넘겨 본다, 확정하지 않는다"
    ///      (`RouletteInteractionBridge.cs:16` 조작 분담 표).
    ///
    /// 즉 **씬에 필요한 것은 배선이지 새 스크립트가 아니다.** 이 컴포넌트가 앉을
    /// 물리 오브젝트(콜라이더 있는 벽면 패널)에 `InteractableContractPanel`을
    /// 같이 붙이고, `RouletteInteractionBridge`의 `_contractPanel` 필드에
    /// 그 컴포넌트를 물리면 클릭 → 미리보기 전환까지 이미 동작한다.
    /// 이 뷰는 그렇게 결정된 `PreviewIndex`를 **읽기만** 한다 — 상태를 갖지 않는다.
    ///
    /// ## 색 규칙 — `InstrumentPanelView.ApplyPlaques`와 같은 접근 경로
    ///
    /// `Scripts/View/InstrumentPanelView.cs:681`의 `ApplyPlaques`가 계기판 위
    /// 계약 명판 3장을 같은 데이터(`floor.Plan.ContractChoices` ·
    /// `floor.Phase == FloorPhase.ContractSelection` · `_bridge.PreviewIndex` ·
    /// `floor.SelectedContract`)로 칠한다. 이 컴포넌트는 그 접근 경로를 그대로
    /// 재사용한다 — 데이터가 갈라지면 계기판 명판과 이 벽면 패널이 "다른 계약이
    /// 선택된 것처럼" 보일 수 있고, 그건 이 저장소가 반복해서 겪은
    /// "화면이 스스로와 모순된다" 부류의 결함이다.
    ///
    /// 다만 상태 개수는 줄인다. 명판은 "미리보기 중 · 확정 · 선택 가능(선택 안 됨,
    /// 옅은 흰색 혼합) · 없음" 네 갈래지만, 이 그레이박스는 요청받은 세 갈래
    /// (미리보기 중 · 확정됨 · 미선택)만 구현한다 — "선택 가능" 특수 색조는
    /// 명판의 폴리싱 판단이지 이 패스가 새로 들여올 결정이 아니다.
    /// </summary>
    public sealed class ContractPanelGrayboxView : MonoBehaviour
    {
        [Header("데이터 소스 — 선택적 참조, 비면 씬에서 자동으로 찾는다")]
        [SerializeField] private RunSessionBehaviour _run;

        [Tooltip("미리보기 칸 번호(PreviewIndex)의 출처. 비어 있으면 '미리보기 중' 상태를 " +
                 "구분할 수 없어 항상 미선택/확정 두 갈래로만 보인다 — InstrumentPanelView가 " +
                 "같은 상황에서 내리는 판단과 같다(ApplyPlaques의 `preview = -1` 폴백).")]
        [SerializeField] private RouletteInteractionBridge _bridge;

        [Header("표시 — 선택지 순서대로 3칸 (FloorPlan.ContractChoices의 인덱스와 1:1)")]
        [RequiredReference("칸이 비어 있으면 계약 색이 하나도 안 보인다 — 자동 대체 경로 없음")]
        [SerializeField] private Renderer[] _slots = new Renderer[3];

        [RequiredReference("칸이 비어 있으면 계약 이름이 하나도 안 보인다 — 자동 대체 경로 없음. " +
                            "단 배열 자체가 비어 있을 때만 잡힌다 — 개별 인덱스가 null이면 " +
                            "이 검사로는 못 잡는다(GRAYBOX_NOTES.md 참고).")]
        [SerializeField] private TMP_Text[] _labels = new TMP_Text[3];

        [Header("색 — 미리보기 중 / 확정됨 / 미선택")]
        [Tooltip("기본값은 InstrumentPanelView._plaquePreview와 같다 — 계기판 명판과 " +
                 "이 벽면 패널이 기본 상태에서부터 같은 색 언어를 쓰게 하기 위해서다.")]
        [SerializeField] private Color _previewingColor = new Color(0.95f, 0.80f, 0.35f);

        [Tooltip("기본값은 InstrumentPanelView._plaqueChosen과 같다.")]
        [SerializeField] private Color _confirmedColor = new Color(0.45f, 0.90f, 0.70f);

        [Tooltip("기본값은 InstrumentPanelView._plaqueIdle과 같다. 선택지가 존재하지 않는 " +
                 "칸(예: 계약이 2개뿐인 층의 3번째 칸)도 이 색으로 조용히 앉는다 — " +
                 "ApplyPlaques가 '없음'과 '있지만 미선택'을 같은 색으로 두는 것과 같은 판단이다.")]
        [SerializeField] private Color _unselectedColor = new Color(0.20f, 0.21f, 0.23f);

        // InstrumentPanelView.ApplyPlaques(683~710줄)의 발광값과 맞춘다. 계기판 명판과
        // 이 패널이 "같은 상태인데 밝기만 다르다"로 보이면 그 자체가 또 하나의
        // 화면-자기모순이 된다. 색과 달리 [SerializeField]로 빼지 않는다 — 요청받은
        // 것은 "색"이지 발광 강도가 아니고, 손잡이를 늘릴수록 두 표시가 갈라질 여지도 늘어난다.
        private const float PreviewEmission = 2.2f;
        private const float ConfirmedEmission = 1.4f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _block;
        private readonly StringBuilder _text = new StringBuilder(96);

        // 라벨 텍스트만 캐시한다(색은 문자열을 만들지 않으므로 매 프레임 다시 칠해도
        // 쓰레기가 생기지 않는다 — InstrumentPanelView.ApplyPlaques와 같은 판단).
        // 0 = 그 칸이 비어 있는 상태로 이미 그려져 있다(보초값, 실제 라벨 해시와
        //     우연히 겹칠 수 있으나 InstrumentPanelView.ApplyPlaqueLabel도 같은 규약을
        //     쓴다 — 계약 이름 하나가 해시 0을 내더라도 최악의 경우 한 프레임 여분
        //     갱신이 있을 뿐 틀린 값이 나오지는 않는다).
        // int.MinValue = 아직 아무것도 그리지 않았다.
        //
        // 길이 3 고정이고 `_slots`·`_labels`의 기본 길이를 그대로 따라간다 — 계약 선택지가
        // 실제로 3을 넘는 층이 없다(`PrototypeCurriculum`의 `ContractChoices`는 항상
        // 0~3개, `HeroSlice`가 최대치 3). `InstrumentPanelView._plaqueKeys`도 같은
        // 가정(길이 3 고정, 인스펙터에서 `_slots`/`_labels`를 3보다 늘리면 이 배열과
        // 어긋나 예외가 난다)을 그대로 갖고 있다 — 그 위험을 새로 만든 것이 아니라
        // 이미 있는 접근 경로를 그대로 재사용한 것이다.
        private readonly int[] _labelKeys = { int.MinValue, int.MinValue, int.MinValue };

        private bool _runOverShown;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_bridge == null) _bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            if (_run != null) _run.RunStarted += _ => InvalidateCache();
        }

        private void InvalidateCache()
        {
            for (int i = 0; i < _labelKeys.Length; i++) _labelKeys[i] = int.MinValue;
            _runOverShown = false;
        }

        private void LateUpdate()
        {
            RunSession run = _run != null ? _run.Session : null;
            // 배선이 없으면 손대지 않는다 — InstrumentPanelView·AscentColumnView와 같은 판단.
            if (run == null) return;

            FloorSession floor = run.Current;
            if (floor == null) { ShowRunOver(); return; }
            _runOverShown = false;

            ResistanceContract[] choices = floor.Plan.ContractChoices;
            bool selecting = floor.Phase == FloorPhase.ContractSelection;
            // 다리가 없으면 미리보기 칸을 알 수 없다 — InstrumentPanelView.ApplyPlaques와
            // 같은 폴백(-1)을 쓴다. -1은 어떤 실제 인덱스와도 안 겹치므로 "미리보기 중"
            // 갈래가 그냥 조용히 발동하지 않는다(확정/미선택 두 갈래만 남는다).
            int preview = _bridge != null ? _bridge.PreviewIndex : -1;

            for (int i = 0; i < _slots.Length; i++)
            {
                bool exists = choices != null && i < choices.Length;
                ResistanceContract contract = exists ? choices[i] : ResistanceContract.None;

                Color color = _unselectedColor;
                float emission = 0f;
                if (exists && selecting && i == preview)
                {
                    color = _previewingColor;
                    emission = PreviewEmission;
                }
                else if (exists && !selecting && SameContract(contract, floor.SelectedContract))
                {
                    color = _confirmedColor;
                    emission = ConfirmedEmission;
                }
                // 그 외(존재하지 않음 · 선택 단계인데 미리보기 칸이 아님)는 위에서 미리
                // 깔아 둔 _unselectedColor 그대로 — "미선택"이 요청받은 세 번째 갈래다.

                ApplySlotColor(_slots[i], color, emission);
                ApplyLabel(i, exists, in contract);
            }
        }

        private void ApplySlotColor(Renderer slot, Color color, float emission)
        {
            if (slot == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();   // 도메인 리로드 뒤 null
            slot.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * emission);
            slot.SetPropertyBlock(_block);
        }

        /// <summary>
        /// 칸 하나의 문구. `InstrumentPanelView.ApplyPlaqueLabel`
        /// (`Scripts/View/InstrumentPanelView.cs:718`)과 달리 시너지 줄(`SynergyWith`)은
        /// **뺀다** — 그 문구는 `floor.Loadout`을 필요로 하고, 이 그레이박스는 적재
        /// 의존성을 새로 들이지 않기 위해 일부러 좁힌다. 시너지는 계기판 명판이 이미
        /// 보여주므로 정보 자체가 사라지지는 않는다(중복 여부는 GRAYBOX_NOTES.md 참고).
        /// </summary>
        private void ApplyLabel(int index, bool exists, in ResistanceContract contract)
        {
            if (_labels == null || index >= _labels.Length) return;
            TMP_Text label = _labels[index];
            if (label == null) return;

            if (!exists)
            {
                if (_labelKeys[index] == 0) return;
                _labelKeys[index] = 0;
                label.SetText(string.Empty);
                return;
            }

            int key = contract.Label != null ? contract.Label.GetHashCode() : 1;
            if (_labelKeys[index] == key) return;
            _labelKeys[index] = key;

            _text.Clear();
            _text.Append(contract.Label).AppendLine();
            _text.Append(contract.Preview());
            label.SetText(_text);
        }

        /// <summary>
        /// 런 종료 시 세 칸을 전부 미선택 색·빈 라벨로 되돌린다. `_runOverShown`으로
        /// 한 번만 하는 이유는 `AscentColumnView.ShowRunOver`의 `_runOverShown`과 같다 —
        /// 매 프레임 반복할 이유가 없는 정적 종료 화면이다.
        /// </summary>
        private void ShowRunOver()
        {
            if (_runOverShown) return;
            _runOverShown = true;
            for (int i = 0; i < _slots.Length; i++)
            {
                ApplySlotColor(_slots[i], _unselectedColor, 0f);
                // `ResistanceContract.None`은 프로퍼티(호출마다 새 값)라 변수가 아니다 —
                // `in`을 명시하면 컴파일러가 임시 변수를 만들어 주지 못해 컴파일 오류가 난다.
                // `InstrumentPanelView.ApplyPlaques`가 `ApplyPlaqueLabel(...,
                // exists ? choices[i] : ResistanceContract.None, ...)`을 `in` 없이 호출하는
                // 것과 같은 이유 — `in` 매개변수 호출부의 `in`은 항상 선택이고, 생략하면
                // 컴파일러가 알아서 임시 변수를 만들어 넘긴다.
                ApplyLabel(i, false, ResistanceContract.None);
            }
        }

        /// <summary>
        /// `ResistanceContract`는 값 타입이고 `==`를 오버로드하지 않는다. `Target`과
        /// `Label` 두 필드만 견주는 이유도 `InstrumentPanelView.SameContract`
        /// (`Scripts/View/InstrumentPanelView.cs:754`)와 같다 — 계약 데이터가
        /// `PrototypeCurriculum`의 정적 인스턴스이므로 두 필드의 일치가 곧 "같은 계약"이다.
        /// </summary>
        private static bool SameContract(in ResistanceContract a, in ResistanceContract b)
            => a.Target == b.Target && a.Label == b.Label;
    }
}
