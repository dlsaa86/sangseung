using System.Text;
using TMPro;
using UnityEngine;
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
    ///   층이 왜 그렇게 끝났는가          → 여기(사고 기록기 요약).
    ///
    /// 화면에는 공간이 담을 수 없는 것만 남긴다.
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

        [Header("전이")]
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 6f;

        private readonly StringBuilder _text = new StringBuilder(512);

        private int _hintKey = int.MinValue;
        private int _cascadeKey = int.MinValue;
        private string _lastCause;
        private bool _resultShown;
        private int _resultKey = int.MinValue;

        /// <summary>검증 하네스용 — 각 구역이 실제로 보이는가.</summary>
        public bool HintVisible => _hintGroup != null && _hintGroup.alpha > 0.5f;
        public bool CascadeVisible => _cascadeGroup != null && _cascadeGroup.alpha > 0.5f;
        public bool ResultVisible => _resultGroup != null && _resultGroup.alpha > 0.5f;

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

            SetAlpha(_cascadeGroup, 0f);
            SetAlpha(_resultGroup, 0f);
            SetAlpha(_hintGroup, 1f);

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
        }

        private void LateUpdate()
        {
            RunSession run = _run != null ? _run.Session : null;
            if (run == null) return;

            bool over = run.IsComplete || run.IsFailed;
            UpdateCascade();
            UpdateHint(run, over);
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
        /// 층 결과. `MASTER_PRD.md` §10이 요구하는 것은 점수판이 아니라 **설명**이다 —
        /// 사고 기록기 요약을 그대로 띄우고 재현 시드를 함께 남긴다.
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

            _text.Clear();
            _text.Append(run.IsFailed ? "층 실패" : "층 확정");
            Apply(_resultTitleText, _text);

            _text.Clear();
            FloorRecord record = _recorder != null ? _recorder.Latest : null;
            if (record != null)
            {
                _text.Append(record.Summary());
                // `record.Contract`는 이미 "계약 없음"·"흡수체 계약"처럼 '계약'을
                // 품고 있다. 앞에 또 붙여서 "계약 계약 없음"이 나왔다.
                _text.Append("\n재현: 시드 ").Append(record.RunSeed)
                     .Append(" · ").Append(record.Floor).Append("층 · ").Append(record.Contract);
            }
            else if (!string.IsNullOrEmpty(run.FailureReason))
            {
                _text.Append(run.FailureReason);
            }
            _text.Append("\n\n[R] 같은 시드로 다시");
            Apply(_resultBodyText, _text);
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
