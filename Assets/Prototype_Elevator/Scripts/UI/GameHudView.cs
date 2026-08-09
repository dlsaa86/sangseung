using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;
using Ascend.Prototype.View;

namespace Ascend.Prototype.UI
{
    /// <summary>
    /// 화면 UI. IMGUI 디버그 HUD를 대체한다.
    ///
    /// **이전 HUD보다 훨씬 적게 띄우는 것이 이 클래스의 요점이다.**
    /// 옛 HUD는 층·전력·요구·게이지·계약·잔류·스핀·결과판을 한 화면에 전부 쏟았다.
    /// 그건 두 가지를 어긴다:
    ///   · `MASTER_PRD.md` §6.1 "한 화면에 모든 숫자를 동시에 띄우지 않는다.
    ///     큰 배수 하나, 현재 캐스케이드 단계, 핵심 발동 원인을 우선 강조한다."
    ///   · `VISUAL_SPEC.md` §5 "상호작용물은 현대적 HUD처럼 떠 보이면 안 된다."
    ///
    /// 그래서 역할을 나눴다:
    ///   전력·요구·임계점·스핀·잔류·계약 → **벽면 계기판**(`InstrumentPanelView`). 공간이 말한다.
    ///   지금 무엇을 할 수 있는가          → **조준 프롬프트**(`CrosshairView`).
    ///   지금 무엇 때문에 터졌는가        → 여기(연쇄 단계 + 원인 한 줄).
    ///   층이 왜 그렇게 끝났는가          → 여기(런 요약 9종).
    ///
    /// 화면에는 공간이 담을 수 없는 것만 남긴다.
    ///
    /// **한 가지 예외를 뒀다 — 보조 표시(`UP-SPACE-09`).**
    /// 계기판이 월드 공간(`TextMeshPro`)이라, 장치를 등지면 전력과 위험이 **원리적으로**
    /// 화면에서 사라진다(증거 `Captures/TenFloor/23_back_turned_screen.png` — 남는 것은
    /// 「연쇄 0단계」 한 줄뿐). PRD §11 의 무음 관전자 기준이 요구하는 세 채널
    /// (사운드·점등·보조 UI) 중 보조 UI 가 사실상 없었다.
    ///
    /// 그래서 **딱 두 가지만** 화면 공간으로 올린다 — 등을 돌린 관전자가 답해야 하는
    /// 질문이 둘뿐이기 때문이다: 「지금 통과 가능한가」와 「지금 위험한가」.
    ///   · 요구 대비 전력 **비율 하나** (숫자 하나. 전력·요구·스핀·판돈·잔류는 올리지 않는다)
    ///   · 위험 **단계 하나** (숫자가 아니라 낱말 + 점등 개수)
    /// 이것이 `UP-CORE-13`/PRD §6.1 「한 화면에 모든 숫자를 동시에 띄우지 않는다.
    /// 큰 배수 하나, 현재 캐스케이드 단계, 핵심 발동 원인을 우선 강조한다」와 충돌하지 않는
    /// 이유: 화면의 숫자는 여전히 **비율 하나 + 연쇄 단계**뿐이고, 나머지는 낱말과 점등이다.
    /// 계기판이 말하던 값을 옮겨 온 것이 아니라 **그중 하나만** 고른 것이다.
    ///
    /// 할당: 표시값이 바뀔 때만 문자열을 짓고 TMP에는 StringBuilder 오버로드로 넘긴다.
    /// 정착 상태에서 이 컴포넌트는 프레임당 0 B를 목표로 한다.
    /// </summary>
    public sealed class GameHudView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private RouletteInteractionBridge _bridge;
        [SerializeField] private SpinPresenter _presenter;
        [SerializeField] private AccidentRecorder _recorder;
        [SerializeField] private RiskStateView _risk;

        [Tooltip("런 요약 9종의 라벨과 서식. 비어 있으면 코드 기본 서식으로 진행한다.")]
        [SerializeField] private Data.Profiles.RunSummaryTemplate _summaryTemplate;

        [Header("지금 무엇을 할 수 있는가 — 한 줄")]
        [SerializeField] private CanvasGroup _hintGroup;
        [SerializeField] private TextMeshProUGUI _hintText;

        [Header("지금 무엇 때문에 터졌는가 — 연출 중에만")]
        [SerializeField] private CanvasGroup _cascadeGroup;
        [SerializeField] private TextMeshProUGUI _cascadeDepthText;
        [SerializeField] private TextMeshProUGUI _cascadeCauseText;

        [Header("층 결과 — 사고 기록기")]
        [SerializeField] private CanvasGroup _resultGroup;
        [SerializeField] private TextMeshProUGUI _resultTitleText;
        [SerializeField] private TextMeshProUGUI _resultBodyText;

        [Header("등을 돌려도 읽히는 보조 표시 — UP-SPACE-09")]
        [Tooltip("비워 두면 런타임에 만든다. 씬에서 배선하면 그쪽을 그대로 쓴다.")]
        [SerializeField] private CanvasGroup _auxGroup;
        [SerializeField] private TextMeshProUGUI _auxRatioText;
        [SerializeField] private TextMeshProUGUI _auxStateText;
        [Tooltip("위험 단계를 색이 아니라 개수로 말하는 점등. 회색조에서도 구분된다.")]
        [SerializeField] private Image[] _auxRiskLamps;

        [Header("전이")]
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 6f;

        private const int RiskLampCount = 4;

        private readonly StringBuilder _text = new StringBuilder(512);

        private int _hintKey = int.MinValue;
        private int _cascadeKey = int.MinValue;
        private string _lastCause;
        private bool _resultShown;
        private int _resultKey = int.MinValue;
        /// <summary>
        /// 층 이동이 전력을 깎는 쪽. 꽂혀 있으면 **이쪽 잔액**을 그린다.
        /// 비어 있으면 예전처럼 `FloorSession.Power` 로 떨어진다.
        /// </summary>
        [SerializeField] private Ascend.Prototype.Run.RoundSandbox _round;

        private long _auxKey = long.MinValue;
        private bool _resultBodyFitted;

        /// <summary>검증 하네스용 — 각 구역이 실제로 보이는가.</summary>
        public bool HintVisible => _hintGroup != null && _hintGroup.alpha > 0.5f;
        public bool CascadeVisible => _cascadeGroup != null && _cascadeGroup.alpha > 0.5f;
        public bool ResultVisible => _resultGroup != null && _resultGroup.alpha > 0.5f;

        /// <summary>
        /// 보조 표시가 화면에 떠 있는가. `UP-SPACE-09` 의 증거 캡처(`23_back_turned_screen`)를
        /// 다시 찍을 때 이 값이 참이어야 「등을 돌려도 읽힌다」를 주장할 수 있다.
        /// </summary>
        public bool AuxVisible => _auxGroup != null && _auxGroup.alpha > 0.5f;
        public int AuxCharacters => _auxRatioText != null ? _auxRatioText.textInfo.characterCount : 0;

        /// <summary>
        /// 실제로 글자가 배치됐는지. `SetText(StringBuilder)`는 `.text` 게터에 반영되지 않으므로
        /// 문자열이 아니라 **배치된 글자 수**로 확인한다.
        /// </summary>
        public int HintCharacters => _hintText != null ? _hintText.textInfo.characterCount : 0;
        public int CascadeCharacters => _cascadeDepthText != null ? _cascadeDepthText.textInfo.characterCount : 0;
        public int ResultCharacters => _resultBodyText != null ? _resultBodyText.textInfo.characterCount : 0;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_bridge == null) _bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            if (_presenter == null) _presenter = FindAnyObjectByType<SpinPresenter>();
            if (_recorder == null) _recorder = FindAnyObjectByType<AccidentRecorder>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_round == null) _round = FindAnyObjectByType<Ascend.Prototype.Run.RoundSandbox>();

            EnsureAuxReadout();

            SetAlpha(_cascadeGroup, 0f);
            SetAlpha(_resultGroup, 0f);
            SetAlpha(_hintGroup, 1f);
            SetAlpha(_auxGroup, 0f);

            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
        }

        private void OnRunStarted(RunSession session)
        {
            _hintKey = int.MinValue;
            _cascadeKey = int.MinValue;
            _lastCause = null;
            _resultShown = false;
            _resultKey = int.MinValue;
            _auxKey = long.MinValue;
        }

        private void LateUpdate()
        {
            RunSession run = _run != null ? _run.Session : null;
            if (run == null) return;

            bool over = run.IsComplete || run.IsFailed;
            UpdateCascade();
            UpdateHint(run, over);
            UpdateAux(run, over);
            UpdateResult(run, over);
        }

        /// <summary>
        /// 연쇄 단계와 발동 원인. 연출 중에만 떠 있고 끝나면 사라진다 —
        /// 상시 표시하면 "지금 무슨 일이 벌어지는 중"이라는 신호가 죽는다.
        /// </summary>
        private void UpdateCascade()
        {
            bool presenting = _presenter != null && _presenter.IsPresenting;

            // 뜰 때는 즉시, 사라질 때만 페이드한다.
            // 첫 단계의 정화 맥동이 0.55초인데 페이드가 0.17초를 먹으면
            // "무엇 때문에 터졌는가"를 읽을 시간이 그만큼 사라진다.
            if (presenting) SetAlpha(_cascadeGroup, 1f);
            else Fade(_cascadeGroup, 0f);

            if (!presenting) return;

            int depth = _presenter.CurrentDepth;
            string cause = _presenter.CurrentCause;
            if (depth == _cascadeKey && ReferenceEquals(cause, _lastCause)) return;
            _cascadeKey = depth;
            _lastCause = cause;

            _text.Clear();
            _text.Append("연쇄 ").Append(depth).Append("단계");
            Apply(_cascadeDepthText, _text);

            _text.Clear();
            _text.Append(string.IsNullOrEmpty(cause) ? " " : cause);
            Apply(_cascadeCauseText, _text);
        }

        /// <summary>
        /// 지금 할 수 있는 것 한 줄. 숫자는 넣지 않는다 — 숫자는 계기판이 말한다.
        /// </summary>
        private void UpdateHint(RunSession run, bool over)
        {
            bool presenting = _presenter != null && _presenter.IsPresenting;
            Fade(_hintGroup, over || presenting ? 0f : 1f);
            if (over || presenting) return;

            FloorSession floor = run.Current;
            if (floor == null) return;

            int preview = _bridge != null ? _bridge.PreviewIndex : 0;
            int key = (int)floor.Phase * 64
                    + (floor.CanBank ? 32 : 0)
                    + (floor.SpinsRemaining > 0 ? 16 : 0)
                    + preview;
            if (key == _hintKey) return;
            _hintKey = key;

            _text.Clear();
            switch (floor.Phase)
            {
                case FloorPhase.ContractSelection:
                    _text.Append("계약 패널로 고르고, 실행 레버로 확정한다");
                    if (_bridge != null)
                        _text.Append("   —   ").Append(_bridge.PreviewContract.Label);
                    break;

                case FloorPhase.Spinning:
                    _text.Append("실행 레버를 당긴다");
                    break;

                case FloorPhase.Decision:
                    if (floor.CanBank && floor.SpinsRemaining > 0)
                        _text.Append("전력 탱크로 확정하거나, 과수확 레버로 한 번 더");
                    else if (floor.CanBank)
                        _text.Append("스핀 소진 — 전력 탱크로 확정한다");
                    else
                        _text.Append("요구 전력 미달 — 전력 탱크로 결과를 확인한다");
                    break;

                default:
                    _text.Append(' ');
                    break;
            }
            Apply(_hintText, _text);
        }

        /// <summary>
        /// 등을 돌려도 읽히는 두 가지. 「지금 통과 가능한가」와 「지금 위험한가」.
        ///
        /// **연출 중에는 값을 얼리고 표시만 유지한다.** 스핀이 확정되는 프레임에
        /// `floor.Power` 가 먼저 뛰므로, 그대로 흘리면 결과 숫자가 연쇄 연출보다
        /// 앞서 나와 공개를 스포일한다 — 그것이 `UP-FIX-15` 로 이미 열려 있는 결함이다.
        /// 관전자는 연출이 끝난 뒤 비율이 움직이는 것을 본다. 그것이 「전력 변화」다.
        /// </summary>
        private void UpdateAux(RunSession run, bool over)
        {
            FloorSession floor = run.Current;
            bool show = !over && floor != null;
            Fade(_auxGroup, show ? 1f : 0f);
            if (!show) return;

            // 연출 중에는 마지막으로 지은 값을 그대로 둔다.
            if (_presenter != null && _presenter.IsPresenting) return;

            // **999 에서 잘라 놓고 있었다.** 계기판은 1,415% 라고 적는데 화면 위 숫자는
            // 999% 라고 적었다 — 같은 순간의 같은 값이 두 곳에서 다르게 나온다.
            // 9차 독립 판정이 「19 의 999%↔1,415% 그대로」로 두 라운드 연속 지적한 것이 이것이다.
            //
            // 상한이 필요했던 이유는 자릿수가 늘면 글자상자를 넘기 때문이다. 그건 상한이
            // 아니라 **글자 크기**로 풀 문제다. 화면이 자기 자신과 모순되는 것보다
            // 숫자가 조금 작아지는 편이 낫다.
            // 🔴 **퍼센트를 걷어냈다** (2026-08-09 사용자 지시: 「퍼센트가 아니라 숫자로」).
            //
            //    기계 계기판이 이미 `저장 N   필요 M` 을 숫자로 쓴다. 화면 위만 비율이면
            //    플레이어가 두 체계를 머릿속에서 환산해야 하고, 같은 순간의 같은 값이
            //    두 표기로 나뉜다 — 이 계기판이 `999%↔1,415%` 로 이미 한 번 겪은 종류의
            //    분열이다. 표기를 하나로 모으면 그 분열이 원리적으로 안 생긴다.
            //
            //    그리고 요구 전력은 위로 끝이 없다. 분모가 계속 커지면 같은 100% 가
            //    매번 다른 양을 뜻하므로, 비율은 언젠가 반드시 거짓말을 한다.
            // 🔴 **가진 전력 하나만 남긴다** (2026-08-09 사용자 지시:
            //    「다른 거 다 빼고 현재 가지고 있는 전력량만 표기해」).
            //
            //    이 패널은 퍼센트 → `저장/필요` → 지금까지 세 번 줄었다. 매번 「여기에
            //    무엇이 더 필요한가」가 아니라 **「무엇이 없어도 되는가」**가 답이었다.
            //    요구 전력은 기계 계기판이 쓰고, 위험 단계는 소리와 화면 연출이 말한다.
            //    등을 돌려도 읽혀야 하는 것은 하나뿐이다 — 지금 얼마 있는가.
            // 🔴 **실제로 지불하는 전력을 보여준다** (2026-08-09 사용자 보고:
            //    「전력을 써서 층은 올라가는데 전력 소모가 안 되네」).
            //
            //    소모가 안 된 것이 아니라 **다른 전력을 보고 있었다.** 지금 전력이
            //    둘이다 — `FloorSession.Power`(RunSession 이 쌓기만 하고 쓰지 않는다)와
            //    `RoundSession.Power`(층 이동이 실제로 깎는다). 화면이 전자를 그리고
            //    있었으니 아무리 움직여도 숫자가 안 줄었다.
            //
            //    **쓰는 쪽을 그린다.** 지갑을 두 개 놓고 안 쓰는 쪽 잔액을 보여주면
            //    그건 틀린 숫자다. 두 모델이 하나로 합쳐질 때까지의 임시 배선이고,
            //    합쳐지면 이 분기는 사라진다.
            int power = _round != null
                ? Mathf.RoundToInt(_round.Round.Power)
                : Mathf.RoundToInt(floor.Power);
            if (power == _auxKey) return;
            _auxKey = power;

            _text.Clear();
            _text.Append(power);
            Apply(_auxRatioText, _text);

            // 위험 램프 갱신부는 지웠다 — 램프 자체가 없어졌으므로 죽은 코드다.
            // (`_auxRiskLamps` 는 항상 null 이다. 필드는 씬 직렬화 호환을 위해 남긴다.)
        }

        private static readonly Color LampOff = new Color(0.20f, 0.21f, 0.24f, 0.85f);

        private static Color LampOn(int risk)
        {
            switch (risk)
            {
                case 1:  return new Color(0.92f, 0.78f, 0.36f, 1f);
                case 2:  return new Color(0.94f, 0.52f, 0.26f, 1f);
                case 3:  return new Color(0.92f, 0.28f, 0.24f, 1f);
                default: return new Color(0.62f, 0.82f, 0.78f, 1f);
            }
        }

        /// <summary>
        /// 층 결과. `MASTER_PRD.md` §10이 요구하는 것은 점수판이 아니라 **설명**이다 —
        /// 그래서 여기에 뜨는 것은 층 숫자판이 아니라 **런 요약 9종**이다(§10.2 / `UP-REC-02`).
        /// 층 단위 전문은 종이 테이프(`PaperTapePrinterView`)와 콘솔 전문이 맡는다.
        /// </summary>
        private void UpdateResult(RunSession run, bool over)
        {
            Fade(_resultGroup, over ? 1f : 0f);
            if (!over) return;

            // **첫 프레임에 래치하면 안 된다.** 런이 끝난 그 프레임에는 기록기가 아직
            // 마지막 층을 적지 않았을 수 있고, 실제로 그랬다 — "층 실패" 제목 아래에
            // 직전 층의 "전력 535 / 요구 350 (153 %)"가 박혔다. 실패 화면이 성공한
            // 층을 설명하고 있었던 것이다. Gate F 의 유일한 증거 화면인데.
            //
            // 기록 수를 키로 쓴다. 기록이 하나 더 붙으면 다시 짓는다.
            int records = _recorder != null ? _recorder.Records.Count : 0;
            if (records == _resultKey) return;
            _resultKey = records;
            _resultShown = true;

            FloorRecord record = _recorder != null ? _recorder.Latest : null;

            _text.Clear();
            _text.Append(run.IsFailed ? "층 실패" : "층 확정");
            // 어느 층에서 끝났는지는 제목이 들고 간다. 본문 9줄에는 층별 숫자를 넣지 않는다.
            if (record != null) _text.Append("   ").Append(record.Floor).Append('층');
            Apply(_resultTitleText, _text);

            // **런 요약 9종.** 문장은 코드가 아니라 `RunSummaryTemplate` 이 정한다
            // (`MASTER_PRD.md` §10.3 「로컬라이즈 가능한 데이터로 분리」).
            // 값은 `AccidentRecorder` 의 기록 하나에서만 나온다 — 인게임 출력과 디버그가
            // 같은 데이터를 쓴다는 `UP-REC-03` 이 여기서도 성립해야 하기 때문이다.
            string[] lines = RunSummaryBuilder.ComposeLines(
                _summaryTemplate, run, _recorder != null ? _recorder.Records : null);

            _text.Clear();
            for (int i = 0; i < lines.Length; i++) _text.Append(lines[i]).Append('\n');
            _text.Append("\n[R] 같은 시드로 다시");
            Apply(_resultBodyText, _text);
            FitResultBody();
        }

        /// <summary>
        /// 9줄 + 안내 한 줄이 결과판 안에 들어가게 한다.
        ///
        /// 이 화면은 이미 한 번 넘쳤다 — 제목과 본문이 같은 사각형을 공유해 대형
        /// 「층 실패」가 본문을 관통했고, 독립 평가가 `17_accident_recorder` 에서 잡았다.
        /// 줄 수가 늘었으므로 같은 실패를 다시 만들지 않으려면 크기가 내용에 맞아야 한다.
        /// **씬을 고치지 않는다** — 자동 크기는 런타임 속성이라 에셋을 건드리지 않는다.
        /// 한 번만 켠다: 매 프레임 켜면 TMP 가 레이아웃을 계속 다시 잡는다.
        /// </summary>
        private void FitResultBody()
        {
            if (_resultBodyText == null || _resultBodyFitted) return;
            _resultBodyFitted = true;

            float max = _resultBodyText.fontSize > 0f ? _resultBodyText.fontSize : 28f;
            _resultBodyText.fontSizeMax = max;
            _resultBodyText.fontSizeMin = 16f;
            _resultBodyText.enableAutoSizing = true;
        }

        /// <summary>
        /// 보조 표시를 런타임에 세운다. **씬 파일을 고치지 않기 위해서다** —
        /// `.unity` 는 fileID 로 얽힌 YAML 이라 병렬 작업 중에 손대면 머지가 아니라 손상이 된다.
        /// 나중에 씬 소유자가 같은 이름의 오브젝트를 만들어 인스펙터에 물리면 이 코드는 비켜선다.
        ///
        /// 폰트는 **힌트 라벨에서 가져온다.** TMP 기본 폰트(LiberationSans)에는 한글이 없어
        /// 새로 만든 라벨이 통째로 두부 글자가 된다 — `UP-FIX-17` 과 같은 부류의 실패다.
        /// </summary>
        private void EnsureAuxReadout()
        {
            if (_auxGroup != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[상승] GameHudView 가 Canvas 아래에 없다 — " +
                                 "보조 표시를 만들지 못했다(UP-SPACE-09).");
                return;
            }

            // 숫자 하나만 남았다. 판을 그 하나에 맞춰 줄인다 —
            // 빈 자리를 남기면 「무언가 사라졌다」로 읽힌다.
            const float width = 200f;
            const float height = 92f;

            var panel = new GameObject("AuxReadout", typeof(RectTransform), typeof(CanvasGroup));
            panel.transform.SetParent(canvas.transform, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -40f);
            rect.sizeDelta = new Vector2(width, height);

            _auxGroup = panel.GetComponent<CanvasGroup>();
            _auxGroup.alpha = 0f;
            _auxGroup.interactable = false;
            _auxGroup.blocksRaycasts = false;   // 1인칭 조준 클릭을 가로채지 않는다

            // 배경판이 없으면 어두운 벽에서는 읽히고 밝은 통관 앞에서는 사라진다.
            AuxImage(panel.transform, "Backdrop",
                     Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                     new Color(0.05f, 0.05f, 0.06f, 0.72f));

            TMP_FontAsset font = _hintText != null ? _hintText.font : null;

            // 가진 전력 하나. 판 전체를 쓴다 — 아래에 아무것도 오지 않으므로
            // 높이 상대로 잡아도 겹칠 상대가 없다.
            _auxRatioText = AuxLabel(panel.transform, "RatioText", 58f,
                                     TextAlignmentOptions.Left,
                                     new Color(0.97f, 0.94f, 0.84f),
                                     new Vector2(18f, 12f), new Vector2(-18f, -10f), font);
            // 상한을 999 에서 푼 대신 자릿수가 늘면 스스로 줄어든다. 잘리지 않는 것이 먼저다.
            // 「510 / 215」는 「48%」보다 글자가 기니 최소 크기를 조금 더 내린다.
            _auxRatioText.enableAutoSizing = true;
            _auxRatioText.fontSizeMax = 58f;
            _auxRatioText.fontSizeMin = 26f;

            // 🔴 **위험 점등 4칸을 걷어냈다** (2026-08-09 사용자 지시: 「아래 게이지는 제거」).
            //
            //    이 넷은 전력이 아니라 **위험 단계**였다. 그런데 두 사람이 독립적으로
            //    「전력 미니 바」로 읽었다 — 9차 독립 평가자가 한 번, 사용자가 한 번.
            //    직전 판본의 주석이 이미 그것을 「판독 결함」으로 인정하고 간격을 벌려
            //    고쳐 보려 했지만, 같은 오독이 다시 났다. **두 번 실패한 표현이다.**
            //
            //    지워도 잃는 정보가 없다는 것이 결정적이다 — 바로 아래 `_auxStateText`
            //    가 「요구 전력 미달 · 응력」처럼 **같은 위험 단계를 이미 글자로 적는다.**
            //    램프는 처음부터 그 글자의 중복이었고, 중복인 쪽이 오독을 만들고 있었다.
            //    글자는 오독되지 않는다.
            _auxRiskLamps = null;

            // 🔴 상태줄(「요구 전력 미달 · 응력」)도 걷어냈다 — 같은 지시의 「다른 거
            //    다 빼고」에 이것도 든다. 요구 전력은 기계 계기판이 쓰고, 위험 단계는
            //    소리와 연출이 말한다. 여기 남는 것은 숫자 하나뿐이다.
            _auxStateText = null;
        }

        private static TextMeshProUGUI AuxLabel(Transform parent, string name, float size,
                                                TextAlignmentOptions alignment, Color color,
                                                Vector2 offsetMin, Vector2 offsetMax,
                                                TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.text = string.Empty;
            return label;
        }

        private static Image AuxImage(Transform parent, string name,
                                      Vector2 anchorMin, Vector2 anchorMax,
                                      Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void Fade(CanvasGroup group, float target)
        {
            if (group == null) return;
            float alpha = Mathf.MoveTowards(group.alpha, target, Time.deltaTime * _fadeSpeed);
            if (!Mathf.Approximately(alpha, group.alpha)) group.alpha = alpha;
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        /// <summary>TMP의 StringBuilder 오버로드는 중간 string을 만들지 않는다.</summary>
        private static void Apply(TextMeshProUGUI label, StringBuilder value)
        {
            if (label != null) label.SetText(value);
        }
    }
}
