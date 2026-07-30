using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;
using Ascend.Prototype.View;

namespace Ascend.Prototype.UI
{
    /// <summary>
    /// 상태 표시와 디버그 패널.
    ///
    /// **게임 조작은 여기 없다.** 계약 선택·스핀·확정·과수확은 전부 엘리베이터 안의 물체로만
    /// 한다(`CURRENT_PHASE.md` Gate B "디버그 조작 없이 1층을 종료할 수 있다").
    /// 키보드 단축키가 같은 동작을 겸하면 코드 경로가 둘이 되어, 물체 쪽이 망가져도
    /// 키보드로 테스트가 통과해 버린다. 그러면 검증이 검증이 아니다.
    ///
    /// 남아 있는 키는 재현·조사용이다:
    ///   [F1] 디버그 패널 · [R] 같은 시드로 재시작 · [T] 시드 입력 · [L] 마지막 스핀 로그
    ///
    /// IMGUI를 쓴다 — 씬(YAML) 배선이 "GameObject 하나"로 끝나기 때문이다. 최종 UI가 아니다.
    /// </summary>
    [RequireComponent(typeof(RunSessionBehaviour))]
    public sealed class RouletteHud : MonoBehaviour
    {
        [SerializeField] private int _fontSize = 14;
        [SerializeField] private bool _showDebugPanel = true;

        private RunSessionBehaviour _behaviour;
        private SpinPresenter _presenter;
        private RouletteInteractionBridge _bridge;
        private AccidentRecorder _recorder;

        private GUIStyle _box;
        private GUIStyle _cell;
        private string _seedField = string.Empty;
        private bool _editingSeed;
        private string _debugNote = string.Empty;

        // OnGUI 는 프레임마다 Layout·Repaint 로 최소 두 번 불린다. 그 안에서 문자열을
        // 조립하면 같은 문장을 초당 120번 새로 만든다. 이 HUD가 유휴 GC 할당의 89%였다.
        // 그래서 (1) 표시 내용이 바뀔 때만 짓고 (2) GUILayout 대신 고정 Rect + 재사용
        // GUIContent 를 쓴다 — GUILayout 은 호출마다 레이아웃 항목을 할당한다.
        private readonly StringBuilder _builder = new StringBuilder(1024);
        private readonly GUIContent _statusContent = new GUIContent();
        private readonly GUIContent _debugContent = new GUIContent();
        private readonly GUIContent _seedContent = new GUIContent();
        private readonly GUIContent _noteContent = new GUIContent();
        private readonly GUIContent _boardHeader = new GUIContent();
        private readonly GUIContent _boardSummary = new GUIContent();
        private static readonly GUIContent SeedButton = new GUIContent("[T] 시드 입력");
        private static readonly GUIContent ApplyButton = new GUIContent("적용");
        private static readonly GUIContent HelpLine =
            new GUIContent("[R] 같은 시드 재시작    [L] 마지막 스핀 로그    [F1] 디버그 숨기기");

        // 심볼 글리프는 네 종류뿐이다. 매번 문자열→GUIContent 암시 변환을 태우지 않는다.
        private static readonly GUIContent[] CellGlyphs =
        {
            new GUIContent("·"), new GUIContent("◆"), new GUIContent("●"), new GUIContent("▲"),
        };

        private long _statusKey = long.MinValue;
        private string _lastCause;
        private int _debugKey = int.MinValue;
        private int _boardKey = int.MinValue;

        private void Awake()
        {
            _behaviour = GetComponent<RunSessionBehaviour>();
            _presenter = GetComponent<SpinPresenter>();
            _bridge = GetComponent<RouletteInteractionBridge>();
            _recorder = GetComponent<AccidentRecorder>();
            _seedField = _behaviour.Seed.ToString();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || _behaviour == null) return;

            if (k.f1Key.wasPressedThisFrame) _showDebugPanel = !_showDebugPanel;

            if (k.rKey.wasPressedThisFrame && !_editingSeed)
            {
                _behaviour.ResetRun();
                SetNote($"시드 {_behaviour.Seed} 로 재시작.");
            }

            if (k.tKey.wasPressedThisFrame)
            {
                _editingSeed = !_editingSeed;
                if (!_editingSeed) SetNote("시드 입력 취소.");
                else _seedField = _behaviour.Seed.ToString();
            }

            if (k.lKey.wasPressedThisFrame) DumpLastSpin();
        }

        /// <summary>
        /// 마지막 스핀을 로그 한 줄 + 단계별 진단으로 남긴다. 이 줄만 있으면
        /// 헤드리스로 같은 스핀을 재현할 수 있다(`TECH_SPEC.md` §11).
        /// </summary>
        private void DumpLastSpin()
        {
            FloorSession floor = _behaviour.Session != null ? _behaviour.Session.Current : null;
            var history = floor != null ? floor.History : null;
            if (history == null || history.Count == 0) { SetNote("기록된 스핀이 없다."); return; }

            SpinResolution last = history[history.Count - 1];
            Debug.Log($"[상승] 마지막 스핀 재현 정보\n{last.DescribeCascade()}");
            SetNote("마지막 스핀을 콘솔에 기록했다.");
        }

        private void EnsureStyles()
        {
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = _fontSize,
                    richText = true,
                    wordWrap = true,
                };
                _box.normal.textColor = Color.white;
            }
            if (_cell == null)
            {
                _cell = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = _fontSize + 10,
                    fontStyle = FontStyle.Bold,
                };
            }
        }

        private void OnGUI()
        {
            if (_behaviour == null || _behaviour.Session == null) return;
            EnsureStyles();

            RefreshStatus(_behaviour.Session);

            const float x = 12f, width = 420f;
            float height = _box.CalcHeight(_statusContent, width);
            GUI.Label(new Rect(x, 12f, width, height), _statusContent, _box);

            if (_showDebugPanel) DrawDebugPanel(x, 12f + height + 8f, width);
            DrawBoard();
        }

        /// <summary>표시 내용이 실제로 바뀐 프레임에만 문자열을 다시 짓는다.</summary>
        private void RefreshStatus(RunSession run)
        {
            FloorSession f = run.Current;
            bool presenting = _presenter != null && _presenter.IsPresenting;
            string cause = presenting ? _presenter.CurrentCause : null;

            long key = (run.IsFailed ? 1L : 0L)
                     | ((run.IsComplete ? 1L : 0L) << 1)
                     | ((long)(f != null ? f.Plan.Floor : 0) << 2)
                     | ((long)(f != null ? (int)f.Phase : 0) << 8)
                     | ((long)(f != null ? f.SpinsUsed : 0) << 12)
                     | ((long)(f != null ? Mathf.RoundToInt(f.Power) : 0) << 16)
                     | ((long)(f != null && f.CanBank ? 1 : 0) << 40)
                     | ((long)(presenting ? _presenter.CurrentDepth : 0) << 41)
                     | ((long)(_bridge != null ? _bridge.PreviewIndex : 0) << 46);

            if (key == _statusKey && ReferenceEquals(cause, _lastCause)) return;
            _statusKey = key;
            _lastCause = cause;

            BuildStatus(run);
            _statusContent.text = _builder.ToString();
        }

        /// <summary>
        /// 디버그 패널 — 시드 재현이 이번 Phase의 통과 조건이라 시드 입력이 UI에 있어야 한다
        /// (`MASTER_PRD.md` §4.1 "디버그 패널, 결정론적 시드, 텔레메트리").
        /// </summary>
        private void DrawDebugPanel(float x, float y, float width)
        {
            bool locked = _bridge != null && _bridge.IsLocked;
            int key = _behaviour.Seed * 4 + (locked ? 2 : 0) + (_editingSeed ? 1 : 0);
            if (key != _debugKey)
            {
                _debugKey = key;
                _builder.Clear();
                _builder.Append("시드 ").Append(_behaviour.Seed)
                        .Append("    모드 ").Append(_behaviour.Mode)
                        .Append("    입력잠금 ").Append(locked ? "예(연출중)" : "아니오");
                _debugContent.text = _builder.ToString();
                _seedContent.text = _seedField;
            }

            const float row = 22f;
            GUI.Label(new Rect(x, y, width, row), _debugContent, _box);
            y += row + 2f;

            if (_editingSeed)
            {
                _seedField = GUI.TextField(new Rect(x, y, 120f, row), _seedField, 12);
                if (GUI.Button(new Rect(x + 126f, y, 56f, row), ApplyButton))
                {
                    if (int.TryParse(_seedField, out int seed))
                    {
                        _behaviour.ResetRun(seed);
                        SetNote($"시드 {seed} 로 재시작.");
                        _editingSeed = false;
                    }
                    else SetNote("정수만 받는다.");
                }
            }
            else if (GUI.Button(new Rect(x, y, 130f, row), SeedButton))
            {
                _editingSeed = true;
                _seedField = _behaviour.Seed.ToString();
            }
            y += row + 4f;

            GUI.Label(new Rect(x, y, width, row), HelpLine, _box);
            y += row + 2f;

            if (!string.IsNullOrEmpty(_debugNote))
                GUI.Label(new Rect(x, y, width, row), _noteContent, _box);
        }

        private void SetNote(string note)
        {
            _debugNote = note;
            _noteContent.text = note;
        }

        /// <summary>결과를 <see cref="_builder"/>에 남긴다. 문자열을 돌려주지 않는다 — 그게 할당이다.</summary>
        private void BuildStatus(RunSession run)
        {
            var sb = _builder;
            sb.Clear();

            if (run.IsFailed || run.IsComplete)
            {
                sb.AppendLine(run.IsFailed ? "<b>층 실패</b>" : "<b>층 확정</b>");
                if (run.IsFailed && !string.IsNullOrEmpty(run.FailureReason))
                    sb.AppendLine(run.FailureReason);

                // 사고 기록기가 결과 화면의 본문이다. 숫자만 남기면 왜 그렇게 됐는지
                // 설명할 수 없다(MASTER_PRD §10, 경험 완료 조건 "실패 원인을 설명할 수 있음").
                FloorRecord record = _recorder != null ? _recorder.Latest : null;
                if (record != null)
                {
                    sb.AppendLine();
                    sb.AppendLine(record.Summary());
                    sb.Append("<i>재현: 시드 ").Append(record.RunSeed)
                      .Append(" · ").Append(record.Floor).Append("층 · 계약 ")
                      .Append(record.Contract).AppendLine("</i>");
                }
                sb.AppendLine();
                sb.Append("[R] 같은 시드 재시작   [T] 다른 시드");
                return;
            }

            FloorSession f = run.Current;
            if (f == null) { sb.Append("층 없음"); return; }

            FloorPlan p = f.Plan;
            sb.Append("<b>").Append(p.Floor).Append("층</b>  —  ").AppendLine(p.CoreQuestion);
            sb.AppendLine();

            float ratio = f.RequiredPower > 0f ? f.Power / f.RequiredPower : 0f;
            sb.Append("전력 <b>").AppendFormat("{0:F0}", f.Power)
              .Append("</b> / 요구 ").AppendFormat("{0:F0}", f.RequiredPower)
              .Append("   (").AppendFormat("{0:P0}", ratio)
              .Append(", ").Append(f.CurrentBand.DisplayName()).AppendLine(")");
            AppendThresholdBar(sb, ratio);
            sb.Append("남은 스핀 ").Append(f.SpinsRemaining).Append(" / ").AppendLine(p.Spins.ToString());
            sb.Append("계약: ").AppendLine(f.SelectedContract.IsNone ? "없음" : f.SelectedContract.Label);
            sb.Append("잔류: ").AppendLine(f.Residual.Describe());
            sb.AppendLine();

            // 연출 중에는 "지금 무엇 때문에 터졌는가" 하나만 강조한다.
            // 한 화면에 모든 숫자를 동시에 띄우지 않는다(MASTER_PRD §6.1).
            if (_presenter != null && _presenter.IsPresenting)
            {
                sb.Append("<b>연쇄 ").Append(_presenter.CurrentDepth).AppendLine("단계</b>");
                if (!string.IsNullOrEmpty(_presenter.CurrentCause))
                    sb.AppendLine(_presenter.CurrentCause);
                return;
            }

            switch (f.Phase)
            {
                case FloorPhase.ContractSelection:
                    sb.AppendLine("<b>계약을 고른다</b>");
                    sb.AppendLine("계약 패널을 눌러 넘기고, 실행 레버로 확정한다.");
                    if (_bridge != null)
                    {
                        ResistanceContract preview = _bridge.PreviewContract;
                        sb.Append("  → ").AppendLine(preview.Label);
                        sb.Append("     ").AppendLine(preview.Preview());
                    }
                    break;

                case FloorPhase.Spinning:
                    sb.AppendLine("<b>실행 레버를 당긴다</b>");
                    break;

                case FloorPhase.Decision:
                    if (f.CanBank && f.SpinsRemaining > 0)
                    {
                        sb.AppendLine("<b>확정할 것인가, 한 번 더 돌릴 것인가</b>");
                        sb.Append("  전력 탱크 — 확정. 지금 ").AppendLine(f.CurrentBand.DisplayName());
                        sb.Append("  과수확 레버 — 판돈 <b>").AppendFormat("{0:F0}", f.PendingAnte)
                          .Append("</b> 을 먼저 잃는다 (누적 ").AppendFormat("{0:F0}", f.TotalAnte)
                          .Append(", 순손익 ").AppendFormat("{0:+0;−0;0}", f.NetProfit).AppendLine(")");
                    }
                    else if (f.CanBank) sb.AppendLine("<b>스핀 소진</b> — 전력 탱크로 확정한다.");
                    else sb.AppendLine("<b>요구 전력 미달</b> — 전력 탱크로 결과를 확인한다.");
                    break;
            }
        }

        /// <summary>임계점을 눈금으로 보여준다. 숫자만으로는 "조금만 더"가 안 느껴진다.</summary>
        private static void AppendThresholdBar(StringBuilder sb, float ratio)
        {
            const int width = 36;
            int filled = Mathf.Clamp(Mathf.RoundToInt(ratio / 3f * width), 0, width);
            sb.Append('[');
            for (int i = 0; i < width; i++)
            {
                float atRatio = i / (float)width * 3f;
                bool gate = IsNear(atRatio, 1.0f) || IsNear(atRatio, 1.3f)
                         || IsNear(atRatio, 1.7f) || IsNear(atRatio, 2.2f);
                if (i < filled) sb.Append(gate ? '#' : '=');
                else sb.Append(gate ? '|' : '.');
            }
            sb.AppendLine("]  100·130·170·220·300%");

            bool IsNear(float a, float b) => Mathf.Abs(a - b) < (3f / width) * 0.5f;
        }

        /// <summary>
        /// 공간의 결과판을 보조하는 2D 미러. 3×3 구조가 화면에서도 한 번 더 읽히게 한다.
        /// </summary>
        private void DrawBoard()
        {
            FloorSession floor = _behaviour.Session != null ? _behaviour.Session.Current : null;
            var history = floor != null ? floor.History : null;
            if (history == null || history.Count == 0) return;

            SpinResolution spin = history[history.Count - 1];

            int key = history.Count * 32 + floor.Plan.Floor;
            if (key != _boardKey)
            {
                _boardKey = key;
                _builder.Clear();
                _builder.Append("결과판  연쇄 ").Append(spin.ChainDepth);
                if (spin.CascadeCapReached) _builder.Append(" (상한)");
                _boardHeader.text = _builder.ToString();

                _builder.Clear();
                _builder.Append("정상 영혼 ").AppendFormat("{0:F0}", spin.NormalSoulPower)
                        .Append("\n정화 ").AppendFormat("{0:F0}", spin.PurifyPower)
                        .Append("\n잔류 −").AppendFormat("{0:F0}", spin.Residual.StoredPowerLoss)
                        .Append("\n<b>순 전력 ").AppendFormat("{0:+0;−0;0}", spin.NetPower).Append("</b>");
                _boardSummary.text = _builder.ToString();
            }

            const float size = 54f;
            float ox = Screen.width - (size * 3f + 32f);
            float oy = 44f;

            GUI.Label(new Rect(ox, oy - 26f, size * 3f, 22f), _boardHeader, _box);

            SpinBoard board = spin.InitialBoard;
            for (int c = 0; c < SpinBoard.Columns; c++)
            {
                for (int r = 0; r < SpinBoard.Rows; r++)
                {
                    SymbolKind kind = board[c, r];
                    Color prev = GUI.color;
                    GUI.color = ColorFor(kind);
                    GUI.Box(new Rect(ox + c * size, oy + r * size, size - 4f, size - 4f),
                            CellGlyphs[Mathf.Clamp((int)kind, 0, CellGlyphs.Length - 1)], _cell);
                    GUI.color = prev;
                }
            }

            GUI.Label(new Rect(ox - 130f, oy + size * 3f + 8f, size * 3f + 130f, 110f),
                      _boardSummary, _box);
        }

        private static Color ColorFor(SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.NormalSoul:   return new Color(0.65f, 0.9f, 1f);
                case SymbolKind.Absorber:     return new Color(1f, 0.55f, 0.45f);
                case SymbolKind.Proliferator: return new Color(0.7f, 1f, 0.6f);
                default:                      return Color.gray;
            }
        }
    }
}
