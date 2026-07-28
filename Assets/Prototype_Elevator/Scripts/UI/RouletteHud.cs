using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.UI
{
    /// <summary>
    /// 새 자동 룰렛 루프를 키보드만으로 끝까지 돌려보기 위한 임시 HUD.
    ///
    /// IMGUI를 쓴다. Canvas·TMP·프리팹 참조가 하나도 없어서 씬 배선이 "GameObject 하나 추가"로
    /// 끝나기 때문이다. 씬은 YAML이라 손댈수록 조용히 깨질 여지가 늘어나는데, 지금 확인해야 할
    /// 것은 화면의 완성도가 아니라 "계약 → 스핀 → 정화 → 캐스케이드 → 확정/추가 스핀"이
    /// 실제로 재미있는가다. 최종 UI는 이걸 버리고 다시 만든다.
    /// </summary>
    [RequireComponent(typeof(RunSessionBehaviour))]
    public sealed class RouletteHud : MonoBehaviour
    {
        [SerializeField] private int _fontSize = 14;

        private RunSessionBehaviour _behaviour;
        private SpinResolution _lastSpin;
        private bool _hasSpun;
        private string _message = "계약을 고르고 스핀하라.";

        private GUIStyle _box;
        private GUIStyle _cell;

        private void Awake() => _behaviour = GetComponent<RunSessionBehaviour>();

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || _behaviour == null || _behaviour.Session == null) return;

            RunSession run = _behaviour.Session;

            if (k.rKey.wasPressedThisFrame)
            {
                _behaviour.ResetRun();
                _hasSpun = false;
                _message = "런 재시작.";
                return;
            }

            if (run.IsComplete || run.IsFailed) return;

            FloorSession floor = run.Current;
            if (floor == null) return;

            switch (floor.Phase)
            {
                case FloorPhase.ContractSelection:
                    // 계약이 없는 층은 0번(계약 없음)만 있으므로 Space로도 넘어갈 수 있게 둔다.
                    if (k.digit1Key.wasPressedThisFrame) TrySelect(floor, 0);
                    else if (k.digit2Key.wasPressedThisFrame) TrySelect(floor, 1);
                    else if (k.digit3Key.wasPressedThisFrame) TrySelect(floor, 2);
                    else if (k.spaceKey.wasPressedThisFrame) TrySelect(floor, 0);
                    break;

                case FloorPhase.Spinning:
                    if (k.spaceKey.wasPressedThisFrame) DoSpin(run);
                    break;

                case FloorPhase.Decision:
                    if (k.bKey.wasPressedThisFrame)
                    {
                        // Bank() 하면 런이 다음 층으로 넘어가므로 층 번호를 먼저 잡아둔다.
                        int bankedFloor = floor.Plan.Floor;
                        FloorResult r = run.Bank();
                        _message = r != null
                            ? $"{bankedFloor}층 확정 — {r.Band.DisplayName()} " +
                              $"(전력 {r.FinalPower:F0} / 요구 {r.RequiredPower:F0}, +{r.FloorsAscended}층 상승)"
                            : "확정 실패.";
                        _hasSpun = false;
                    }
                    else if (k.pKey.wasPressedThisFrame)
                    {
                        float ante = floor.PendingAnte;
                        if (run.PushYourLuck())
                        {
                            _message = $"판돈 {ante:F0} 지불 — 한 번 더 돌린다. [Space]";
                        }
                        else _message = "추가 스핀 불가 (남은 스핀 없음).";
                    }
                    else if (k.spaceKey.wasPressedThisFrame) DoSpin(run);
                    break;
            }
        }

        private void TrySelect(FloorSession floor, int index)
        {
            var choices = floor.Plan.ContractChoices;
            if (choices == null || choices.Length == 0)
            {
                if (floor.SelectContract(0)) _message = "계약 없음 — 스핀하라. [Space]";
                return;
            }
            if (index >= choices.Length) return;
            if (floor.SelectContract(index))
                _message = $"{choices[index].Label} 선택 — 스핀하라. [Space]";
        }

        private void DoSpin(RunSession run)
        {
            FloorSession floor = run.Current;
            if (floor == null || floor.SpinsRemaining <= 0) { _message = "남은 스핀 없음. [B] 확정"; return; }

            _lastSpin = run.Spin();
            _hasSpun = true;
            _message = _lastSpin.Summary();

            if (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining == 0)
            {
                FloorResult r = run.ForceResolve();
                if (r != null)
                    _message = $"스핀 소진 — {r.Band.DisplayName()} (전력 {r.FinalPower:F0} / 요구 {r.RequiredPower:F0})";
                _hasSpun = false;
            }
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

            RunSession run = _behaviour.Session;

            GUILayout.BeginArea(new Rect(12, 12, 470, Screen.height - 24));
            GUILayout.Label(BuildStatus(run), _box);
            GUILayout.EndArea();

            if (_hasSpun) DrawBoard(_lastSpin);
        }

        private string BuildStatus(RunSession run)
        {
            var sb = new StringBuilder(1024);

            if (run.IsFailed)
            {
                sb.AppendLine($"<b>런 실패</b> — {run.FailureReason}");
                sb.AppendLine($"최고 도달 {run.HighestFloorReached}층");
                sb.AppendLine();
                sb.AppendLine("[R] 재시작");
                return sb.ToString();
            }
            if (run.IsComplete)
            {
                sb.AppendLine($"<b>런 성공</b> — {run.HighestFloorReached}층 도달, 돈 {run.Money:F0}");
                sb.AppendLine();
                sb.AppendLine("[R] 재시작");
                return sb.ToString();
            }

            FloorSession f = run.Current;
            if (f == null) return "층 없음";

            FloorPlan p = f.Plan;
            sb.AppendLine($"<b>{p.Floor}층</b>  —  {p.CoreQuestion}");
            sb.AppendLine($"<i>{p.TeachesRule}</i>");
            sb.AppendLine();

            float ratio = f.RequiredPower > 0f ? f.Power / f.RequiredPower : 0f;
            sb.AppendLine($"전력 <b>{f.Power:F0}</b> / 요구 {f.RequiredPower:F0}   ({ratio:P0}, {f.CurrentBand.DisplayName()})");
            sb.AppendLine(ThresholdBar(ratio));
            sb.AppendLine($"남은 스핀 {f.SpinsRemaining} / {p.Spins}    무게 {f.CarriedWeight:F0}" +
                          (f.IsOverloaded ? "  <b>[과적]</b>" : string.Empty));
            sb.AppendLine($"계약: {(f.SelectedContract.IsNone ? "없음" : f.SelectedContract.Label)}");
            sb.AppendLine($"잔류: {f.Residual.Describe()}");
            sb.AppendLine();

            switch (f.Phase)
            {
                case FloorPhase.ContractSelection:
                    sb.AppendLine("<b>계약 선택</b>  (걸기 전에 대가까지 공개된다)");
                    var choices = p.ContractChoices;
                    if (choices == null || choices.Length == 0)
                        sb.AppendLine("  [1] 또는 [Space] — 계약 없음");
                    else
                        for (int i = 0; i < choices.Length; i++)
                            sb.AppendLine($"  [{i + 1}] {choices[i].Label}\n      {choices[i].Preview()}");
                    break;

                case FloorPhase.Spinning:
                    sb.AppendLine("[Space] 레버를 당긴다");
                    break;

                case FloorPhase.Decision:
                    sb.AppendLine("<b>확정할 것인가, 한 번 더 돌릴 것인가</b>");
                    sb.AppendLine($"  [B] 전력 확정 — 지금 {f.CurrentBand.DisplayName()}");
                    if (f.SpinsRemaining > 0)
                        sb.AppendLine($"  [P] 추가 스핀 — <b>판돈 {f.PendingAnte:F0}</b> 를 먼저 잃는다 " +
                                      $"(누적 {f.TotalAnte:F0}, 순손익 {f.NetProfit:+0;−0;0})");
                    else
                        sb.AppendLine("  (남은 스핀 없음)");
                    break;
            }

            sb.AppendLine();
            sb.AppendLine($"<b>{_message}</b>");
            sb.AppendLine();
            sb.AppendLine("<i>[R] 런 재시작</i>");
            return sb.ToString();
        }

        /// <summary>임계점을 눈금으로 보여준다. 숫자만으로는 "조금만 더"가 안 느껴진다.</summary>
        private static string ThresholdBar(float ratio)
        {
            const int width = 40;
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

        private void DrawBoard(SpinResolution spin)
        {
            const float size = 62f;
            float ox = Screen.width - (size * 3f + 40f);
            float oy = 40f;

            GUI.Label(new Rect(ox, oy - 24f, size * 3f, 22f),
                      $"결과판  (연쇄 {spin.ChainDepth})", _box);

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
            GUI.Label(new Rect(ox - 120f, y, size * 3f + 120f, 120f),
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
