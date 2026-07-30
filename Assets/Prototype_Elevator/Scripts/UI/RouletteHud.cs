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

        private GUIStyle _box;
        private GUIStyle _cell;
        private string _seedField = string.Empty;
        private bool _editingSeed;
        private string _debugNote = string.Empty;

        private void Awake()
        {
            _behaviour = GetComponent<RunSessionBehaviour>();
            _presenter = GetComponent<SpinPresenter>();
            _bridge = GetComponent<RouletteInteractionBridge>();
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
                _debugNote = $"시드 {_behaviour.Seed} 로 재시작.";
            }

            if (k.tKey.wasPressedThisFrame)
            {
                _editingSeed = !_editingSeed;
                if (!_editingSeed) _debugNote = "시드 입력 취소.";
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
            if (history == null || history.Count == 0) { _debugNote = "기록된 스핀이 없다."; return; }

            SpinResolution last = history[history.Count - 1];
            Debug.Log($"[상승] 마지막 스핀 재현 정보\n{last.DescribeCascade()}");
            _debugNote = "마지막 스핀을 콘솔에 기록했다.";
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

            GUILayout.BeginArea(new Rect(12, 12, 420, Screen.height - 24));
            GUILayout.Label(BuildStatus(_behaviour.Session), _box);
            if (_showDebugPanel) DrawDebugPanel();
            GUILayout.EndArea();

            DrawBoard();
        }

        /// <summary>
        /// 디버그 패널 — 시드 재현이 이번 Phase의 통과 조건이라 시드 입력이 UI에 있어야 한다
        /// (`MASTER_PRD.md` §4.1 "디버그 패널, 결정론적 시드, 텔레메트리").
        /// </summary>
        private void DrawDebugPanel()
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>디버그</b>   [F1] 숨기기", _box);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"시드 {_behaviour.Seed}", GUILayout.Width(110));
            if (_editingSeed)
            {
                _seedField = GUILayout.TextField(_seedField, 12, GUILayout.Width(110));
                if (GUILayout.Button("적용", GUILayout.Width(50)))
                {
                    if (int.TryParse(_seedField, out int seed))
                    {
                        _behaviour.ResetRun(seed);
                        _debugNote = $"시드 {seed} 로 재시작.";
                        _editingSeed = false;
                    }
                    else _debugNote = "정수만 받는다.";
                }
            }
            else if (GUILayout.Button("[T] 시드 입력", GUILayout.Width(120)))
            {
                _editingSeed = true;
                _seedField = _behaviour.Seed.ToString();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"모드 {_behaviour.Mode}    " +
                            $"입력잠금 {(_bridge != null && _bridge.IsLocked ? "예(연출중)" : "아니오")}", _box);
            GUILayout.Label("[R] 같은 시드 재시작    [L] 마지막 스핀 로그", _box);
            if (!string.IsNullOrEmpty(_debugNote)) GUILayout.Label($"<i>{_debugNote}</i>", _box);
        }

        private string BuildStatus(RunSession run)
        {
            var sb = new StringBuilder(1024);

            if (run.IsFailed)
            {
                sb.AppendLine($"<b>런 실패</b> — {run.FailureReason}");
                sb.AppendLine($"최고 도달 {run.HighestFloorReached}층");
                return sb.ToString();
            }
            if (run.IsComplete)
            {
                sb.AppendLine($"<b>런 종료</b> — {run.HighestFloorReached}층 도달, 잉여 전력 {run.Money:F0}");
                return sb.ToString();
            }

            FloorSession f = run.Current;
            if (f == null) return "층 없음";

            FloorPlan p = f.Plan;
            sb.AppendLine($"<b>{p.Floor}층</b>  —  {p.CoreQuestion}");
            sb.AppendLine();

            float ratio = f.RequiredPower > 0f ? f.Power / f.RequiredPower : 0f;
            sb.AppendLine($"전력 <b>{f.Power:F0}</b> / 요구 {f.RequiredPower:F0}   ({ratio:P0}, {f.CurrentBand.DisplayName()})");
            sb.AppendLine(ThresholdBar(ratio));
            sb.AppendLine($"남은 스핀 {f.SpinsRemaining} / {p.Spins}");
            sb.AppendLine($"계약: {(f.SelectedContract.IsNone ? "없음" : f.SelectedContract.Label)}");
            sb.AppendLine($"잔류: {f.Residual.Describe()}");
            sb.AppendLine();

            // 연출 중에는 "지금 무엇 때문에 터졌는가" 하나만 강조한다.
            // 한 화면에 모든 숫자를 동시에 띄우지 않는다(MASTER_PRD §6.1).
            if (_presenter != null && _presenter.IsPresenting)
            {
                sb.AppendLine($"<b>연쇄 {_presenter.CurrentDepth}단계</b>");
                if (!string.IsNullOrEmpty(_presenter.CurrentCause))
                    sb.AppendLine(_presenter.CurrentCause);
                return sb.ToString();
            }

            switch (f.Phase)
            {
                case FloorPhase.ContractSelection:
                    sb.AppendLine("<b>계약을 고른다</b>");
                    sb.AppendLine("계약 패널을 눌러 넘기고, 실행 레버로 확정한다.");
                    if (_bridge != null)
                        sb.AppendLine($"  → {_bridge.PreviewContract.Label}\n     {_bridge.PreviewContract.Preview()}");
                    break;

                case FloorPhase.Spinning:
                    sb.AppendLine("<b>실행 레버를 당긴다</b>");
                    break;

                case FloorPhase.Decision:
                    if (f.CanBank && f.SpinsRemaining > 0)
                    {
                        sb.AppendLine("<b>확정할 것인가, 한 번 더 돌릴 것인가</b>");
                        sb.AppendLine($"  전력 탱크 — 확정. 지금 {f.CurrentBand.DisplayName()}");
                        sb.AppendLine($"  과수확 레버 — 판돈 <b>{f.PendingAnte:F0}</b> 을 먼저 잃는다 " +
                                      $"(누적 {f.TotalAnte:F0}, 순손익 {f.NetProfit:+0;−0;0})");
                    }
                    else if (f.CanBank)
                    {
                        sb.AppendLine("<b>스핀 소진</b> — 전력 탱크로 확정한다.");
                    }
                    else
                    {
                        sb.AppendLine("<b>요구 전력 미달</b> — 전력 탱크로 결과를 확인한다.");
                    }
                    break;
            }
            return sb.ToString();
        }

        /// <summary>임계점을 눈금으로 보여준다. 숫자만으로는 "조금만 더"가 안 느껴진다.</summary>
        private static string ThresholdBar(float ratio)
        {
            const int width = 36;
            var sb = new StringBuilder(width + 8);
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
            sb.Append("]  100·130·170·220·300%");
            return sb.ToString();

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
            const float size = 54f;
            float ox = Screen.width - (size * 3f + 32f);
            float oy = 44f;

            GUI.Label(new Rect(ox, oy - 26f, size * 3f, 22f),
                      $"결과판  연쇄 {spin.ChainDepth}{(spin.CascadeCapReached ? " (상한)" : string.Empty)}", _box);

            SpinBoard board = spin.InitialBoard;
            for (int c = 0; c < SpinBoard.Columns; c++)
            {
                for (int r = 0; r < SpinBoard.Rows; r++)
                {
                    SymbolKind kind = board[c, r];
                    Color prev = GUI.color;
                    GUI.color = ColorFor(kind);
                    GUI.Box(new Rect(ox + c * size, oy + r * size, size - 4f, size - 4f),
                            GlyphFor(kind), _cell);
                    GUI.color = prev;
                }
            }

            float y = oy + size * 3f + 8f;
            GUI.Label(new Rect(ox - 130f, y, size * 3f + 130f, 110f),
                      $"정상 영혼 {spin.NormalSoulPower:F0}\n정화 {spin.PurifyPower:F0}\n" +
                      $"잔류 −{spin.Residual.StoredPowerLoss:F0}\n<b>순 전력 {spin.NetPower:+0;−0;0}</b>", _box);
        }

        private static string GlyphFor(SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.NormalSoul:   return "◆";
                case SymbolKind.Absorber:     return "●";
                case SymbolKind.Proliferator: return "▲";
                default:                      return "·";
            }
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
