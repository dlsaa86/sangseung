using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascend.Prototype
{
    /// <summary>
    /// Read-only HUD. Reads RunController every frame and writes two TMP texts.
    ///
    /// This class must never mutate game state — the only input it handles is the [P] toggle for
    /// the probability table, which is purely local view state. Everything else is display.
    /// </summary>
    public class PrototypeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunController _runController;

        [Tooltip("Multiline TMP text at the top-left — all live values.")]
        [SerializeField] private TMP_Text _hudText;

        [Tooltip("Bottom-centre TMP text — keyboard hints for the current state.")]
        [SerializeField] private TMP_Text _hintText;

        [Tooltip("Used for the [P] ball probability table. Table is skipped when null.")]
        [SerializeField] private BallDatabase _ballDatabase;

        // Grade colours differ in hue AND lightness so they stay readable for colour-blind
        // players and on washed-out displays.
        private const string ColCommon    = "#B8B8B8";
        private const string ColAdvanced  = "#5FC7F5";
        private const string ColRare      = "#B681F0";
        private const string ColLegendary = "#F2A03D";
        private const string ColGood      = "#6FD46F";
        private const string ColBad       = "#F05B5B";
        private const string ColWarn      = "#F2D24B";
        private const string ColDim       = "#8A8A8A";

        private const int EffectLogMaxLines = 8;

        // Reused every frame — Update() must not allocate.
        private readonly StringBuilder _sb = new StringBuilder(2048);
        private readonly StringBuilder _tmp = new StringBuilder(256);

        private bool _showProbabilityTable = true;
        private string _cachedProbabilityTable;

        private void Update()
        {
            HandleLocalInput();
            if (_runController == null) return;

            BuildHint();
            BuildHud();
        }

        // ── Local view-only input ──

        private void HandleLocalInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.pKey.wasPressedThisFrame)
                _showProbabilityTable = !_showProbabilityTable;
        }

        // ── Hint line ──

        private void BuildHint()
        {
            if (_hintText == null) return;

            if (_runController.Outcome != RunOutcome.InProgress)
            {
                _hintText.text = "[R] 다시 시작";
                return;
            }

            switch (_runController.CurrentState)
            {
                case RunState.PassengerSelection:
                    _hintText.text = "[1][2] 승객 탑승   [0] 건너뛰기   [Space] 진행   [P] 확률표   [R] 재시작";
                    break;
                case RunState.GenerationTurn:
                    bool allStopped = _runController.Roulette != null && _runController.Roulette.AllStopped;
                    _hintText.text = allStopped
                        ? "[Space] 다음 턴   [P] 확률표   [R] 재시작"
                        : "[1][2][3] 통관 정지   [P] 확률표   [R] 재시작";
                    break;
                case RunState.OverchargeAllocation:
                    _hintText.text = "[1] 돈   [2] 추가 상승   [Space] 확정   [P] 확률표   [R] 재시작";
                    break;
                default:
                    _hintText.text = "[Space] 진행   [P] 확률표   [R] 재시작";
                    break;
            }
        }

        // ── HUD ──

        private void BuildHud()
        {
            if (_hudText == null) return;

            ElevatorState state = _runController.State;
            if (state == null) { _hudText.text = "(state 없음)"; return; }

            PrototypeConfig config = _runController.Config;
            FloorController floor = _runController.Floor;
            RunOutcome outcome = _runController.Outcome;

            _sb.Clear();

            // ── Run finished: show only the result ──
            if (outcome != RunOutcome.InProgress)
            {
                bool win = outcome == RunOutcome.Success;
                _sb.Append("<size=150%><color=").Append(win ? ColGood : ColBad).Append(">=== 런 ")
                   .Append(win ? "성공" : "실패").Append(" ===</color></size>\n\n");

                if (!win && !string.IsNullOrEmpty(state.LastFailureReason))
                    _sb.Append("<color=").Append(ColBad).Append('>').Append(state.LastFailureReason).Append("</color>\n\n");

                _sb.Append("최고 도달 층 ").Append(state.HighestFloorReached)
                   .Append("   돈 ").Append(state.Money.ToString("F0"))
                   .Append("   사고 ").Append(state.TotalAccidents).Append('회')
                   .Append("   재시도 ").Append(state.TotalRetries).Append("회\n");
                _sb.Append("승객 ").Append(DescribeBoarded()).Append('\n');
                _sb.Append("\n<color=").Append(ColWarn).Append(">[R] 다시 시작</color>");
                _hudText.text = _sb.ToString();
                return;
            }

            int currentFloor = floor != null ? floor.CurrentFloor : 0;
            float required = floor != null ? floor.RequiredPower : 0f;
            int maxTurns = config != null ? config.generationsPerFloor : 0;
            int targetFloor = config != null ? config.targetFloor : 0;

            // ── Core status ──
            _sb.Append("층  ").Append(currentFloor).Append(" / ").Append(targetFloor)
               .Append("       상태 ").Append(_runController.CurrentState)
               .Append("   턴 ").Append(state.CurrentTurn).Append('/').Append(maxTurns).Append('\n');

            bool powerOk = state.Power >= required;
            _sb.Append("전력  <color=").Append(powerOk ? ColGood : ColBad).Append('>')
               .Append(state.Power.ToString("F0")).Append("</color> / ").Append(required.ToString("F0"))
               .Append("  ").Append(Bar(state.Power, required)).Append('\n');

            _sb.Append("돈    ").Append(state.Money.ToString("F0")).Append('\n');

            // ── Weight / overload ──
            bool overloaded = state.IsOverloaded;
            _sb.Append("무게  ");
            if (overloaded)
            {
                _sb.Append("<color=").Append(ColBad).Append('>')
                   .Append(state.Weight.ToString("F0")).Append(" / ").Append(state.AllowedWeight.ToString("F0"))
                   .Append("   ⚠ 과적 +").Append((state.Weight - state.AllowedWeight).ToString("F0"))
                   .Append("</color>\n");
            }
            else
            {
                _sb.Append(state.Weight.ToString("F0")).Append(" / ").Append(state.AllowedWeight.ToString("F0")).Append('\n');
            }

            // Accident chance must be visible BEFORE the accident happens (T-06 requirement).
            float chance = state.AccidentChance;
            string chanceCol = chance <= 0f ? ColGood : (chance >= 0.4f ? ColBad : ColWarn);
            _sb.Append("사고 확률  <color=").Append(chanceCol).Append('>')
               .Append((chance * 100f).ToString("F0")).Append("%</color>\n");

            _sb.Append("승객  ").Append(DescribeBoarded()).Append('\n');

            // ── Tubes: the button-to-tube mapping has to be unmistakable ──
            AppendTubes();

            // ── State-specific blocks ──
            switch (_runController.CurrentState)
            {
                case RunState.FloorArrival:
                case RunState.PassengerSelection:
                    AppendCandidates();
                    break;

                case RunState.GenerationTurn:
                    AppendRollAndEffects(state);
                    break;

                case RunState.PowerResolution:
                    AppendResolution(state);
                    AppendRollAndEffects(state);
                    break;

                case RunState.OverchargeAllocation:
                    AppendResolution(state);
                    AppendOvercharge();
                    break;
            }

            if (_showProbabilityTable)
                _sb.Append(GetProbabilityTable());

            _hudText.text = _sb.ToString();
        }

        // ── Sections ──

        private void AppendTubes()
        {
            RouletteController roulette = _runController.Roulette;
            if (roulette == null) return;
            IReadOnlyList<TubeController> tubes = roulette.Tubes;
            if (tubes == null || tubes.Count == 0) return;

            PrototypeConfig config = _runController.Config;
            float tolerance = config != null ? config.perfectStopTolerance : 0f;

            _sb.Append("\n통관\n");
            for (int i = 0; i < tubes.Count; i++)
            {
                TubeController tube = tubes[i];
                _sb.Append("  [").Append(i + 1).Append("] 통관").Append((char)('A' + i)).Append("  ");

                if (tube == null) { _sb.Append("<color=").Append(ColDim).Append(">없음</color>\n"); continue; }

                if (tube.IsStopped)
                {
                    BallDefinition ball = tube.StoppedBall;
                    _sb.Append("◆ 정지 ");
                    if (ball != null)
                    {
                        _sb.Append("<color=").Append(GradeColor(ball.grade)).Append('>')
                           .Append(ball.id).Append(' ').Append(GradeName(ball.grade)).Append("</color>");
                    }
                    else _sb.Append('?');

                    // No timing verdict here on purpose. Press precision decides WHICH ball you
                    // take, nothing more — showing a grade for it only invited the player to
                    // chase a number and made the whole thing tiring.
                }
                else if (tube.IsBraking) _sb.Append("<color=").Append(ColWarn).Append(">● 제동중</color>");
                else                     _sb.Append("<color=").Append(ColDim).Append(">● 낙하중</color>");

                _sb.Append('\n');
            }
        }

        private void AppendCandidates()
        {
            PassengerManager passengers = _runController.Passengers;
            if (passengers == null) return;
            IReadOnlyList<PassengerDefinition> candidates = passengers.Candidates;

            _sb.Append("\n승객 후보\n");
            if (candidates == null || candidates.Count == 0)
            {
                _sb.Append("  <color=").Append(ColDim).Append(">없음</color>\n");
                return;
            }

            PrototypeConfig config = _runController.Config;
            ElevatorState state = _runController.State;
            FloorController floor = _runController.Floor;

            for (int i = 0; i < candidates.Count; i++)
            {
                PassengerDefinition p = candidates[i];
                if (p == null) continue;
                _sb.Append("  [").Append(i + 1).Append("] ")
                   .Append(string.IsNullOrEmpty(p.displayName) ? p.id : p.displayName)
                   .Append("   무게 ").Append(p.weight.ToString("F0"))
                   .Append("   ").Append(p.description);

                // Show what boarding would cost in required power — the trade-off has to be legible
                // at the moment of choosing, not after.
                if (config != null && floor != null && state != null)
                {
                    float newWeight = state.Weight + p.weight;
                    float newAllowed = state.AllowedWeight + p.allowedWeightBonus;
                    float newRequired = FloorMath.ComputeRequiredPower(
                        config, floor.CurrentFloor, newWeight, newWeight > newAllowed);
                    float delta = newRequired - floor.RequiredPower;
                    _sb.Append("  <color=").Append(delta > 0f ? ColWarn : ColGood).Append(">→ 요구 ")
                       .Append(newRequired.ToString("F0")).Append(" (").Append(delta >= 0f ? "+" : "")
                       .Append(delta.ToString("F0")).Append(")</color>");
                }
                _sb.Append('\n');
            }
            _sb.Append("  [0] 아무도 태우지 않음\n");
        }

        private void AppendRollAndEffects(ElevatorState state)
        {
            // Stop accuracy and timing bias used to be reported here. Both are gone on purpose:
            // grading the press turned a judgement game into a reflex test and made players
            // stare instead of think. The press picks a ball; that is all it does.

            if (!string.IsNullOrEmpty(state.LastRollSummary))
                _sb.Append("조합  ").Append(state.LastRollSummary).Append('\n');

            EffectResolver effects = _runController.Effects;
            if (effects == null) return;
            IReadOnlyList<EffectLogEntry> log = effects.LastLog;
            if (log == null || log.Count == 0) return;

            _sb.Append("연쇄 효과\n");
            int start = Mathf.Max(0, log.Count - EffectLogMaxLines);
            if (start > 0)
                _sb.Append("  <color=").Append(ColDim).Append(">... 앞 ").Append(start).Append("줄 생략</color>\n");
            for (int i = start; i < log.Count; i++)
            {
                EffectLogEntry e = log[i];
                _sb.Append("  <color=").Append(e.Applied ? ColGood : ColDim).Append('>')
                   .Append(e.ToDisplayString()).Append("</color>\n");
            }
        }

        private void AppendResolution(ElevatorState state)
        {
            if (state.LastAccidentOccurred && !string.IsNullOrEmpty(state.LastAccidentCause))
            {
                _sb.Append("\n<color=").Append(ColBad).Append(">⚠ ")
                   .Append(state.LastAccidentCause).Append("</color>\n");
            }

            if (_runController.LastResolutionSuccess)
            {
                _sb.Append("<color=").Append(ColGood).Append(">성공  초과 전력 +")
                   .Append(_runController.Surplus.ToString("F0")).Append("</color>\n");
            }
            else if (_runController.LastShortfall > 0f)
            {
                _sb.Append("<color=").Append(ColBad).Append(">실패  전력 부족 ")
                   .Append(_runController.LastShortfall.ToString("F0"))
                   .Append("   (재시도 ").Append(state.RetriesThisFloor).Append('/')
                   .Append(_runController.Config != null ? _runController.Config.maxRetriesPerFloor : 0)
                   .Append(")</color>\n");
            }
        }

        private void AppendOvercharge()
        {
            OverchargeOption money = _runController.MoneyOption;
            OverchargeOption ascend = _runController.AscendOption;
            bool ascendSelected = _runController.OverchargeChoice == 1;

            _sb.Append("\n초과 전력 ").Append(_runController.Surplus.ToString("F0")).Append(" 을 어떻게 쓸까?\n");
            _sb.Append("  [1] ").Append(money.Label);
            if (!ascendSelected) _sb.Append("   <color=").Append(ColWarn).Append(">← 선택됨</color>");
            _sb.Append('\n');
            _sb.Append("  [2] ").Append(ascend.Label);
            if (ascendSelected) _sb.Append("   <color=").Append(ColWarn).Append(">← 선택됨</color>");
            _sb.Append('\n');
        }

        // ── Helpers ──

        private string DescribeBoarded()
        {
            PassengerManager passengers = _runController.Passengers;
            PrototypeConfig config = _runController.Config;
            int slots = config != null ? config.maxPassengerSlots : 0;
            if (passengers == null) return "없음";

            IReadOnlyList<PassengerDefinition> boarded = passengers.Boarded;
            if (boarded == null || boarded.Count == 0) return "없음 (0/" + slots + ")";

            _tmp.Clear();
            for (int i = 0; i < boarded.Count; i++)
            {
                if (i > 0) _tmp.Append(", ");
                PassengerDefinition p = boarded[i];
                _tmp.Append(p == null ? "?" : (string.IsNullOrEmpty(p.displayName) ? p.id : p.displayName));
            }
            _tmp.Append(" (").Append(boarded.Count).Append('/').Append(slots).Append(')');
            return _tmp.ToString();
        }

        private static string Bar(float value, float target)
        {
            if (target <= 0f) return string.Empty;
            float ratio = Mathf.Clamp01(value / target);
            int filled = Mathf.RoundToInt(ratio * 10f);
            var chars = new char[12];
            chars[0] = '[';
            for (int i = 0; i < 10; i++) chars[i + 1] = i < filled ? '■' : '□';
            chars[11] = ']';
            return new string(chars) + " " + (ratio * 100f).ToString("F0") + "%";
        }

        private static string GradeColor(BallGrade grade)
        {
            switch (grade)
            {
                case BallGrade.Common:    return ColCommon;
                case BallGrade.Advanced:  return ColAdvanced;
                case BallGrade.Rare:      return ColRare;
                case BallGrade.Legendary: return ColLegendary;
                default:                  return ColDim;
            }
        }

        private static string GradeName(BallGrade grade)
        {
            switch (grade)
            {
                case BallGrade.Common:    return "일반";
                case BallGrade.Advanced:  return "고급";
                case BallGrade.Rare:      return "희귀";
                case BallGrade.Legendary: return "전설";
                default:                  return "?";
            }
        }

        /// <summary>
        /// Built once and cached — the database never changes at runtime, and rebuilding a
        /// 9-row table every frame would allocate for nothing.
        /// </summary>
        private string GetProbabilityTable()
        {
            if (_cachedProbabilityTable != null) return _cachedProbabilityTable;
            if (_ballDatabase == null || _ballDatabase.balls == null) return string.Empty;

            var sb = new StringBuilder(512);
            float sum = 0f;
            foreach (BallDefinition b in _ballDatabase.balls) if (b != null) sum += b.spawnProbability;

            sb.Append("\n구슬 등장 확률 (합계 ").Append(sum.ToString("F0")).Append("%)   [P] 토글\n");
            for (int grade = 0; grade <= 3; grade++)
            {
                foreach (BallDefinition b in _ballDatabase.balls)
                {
                    if (b == null || (int)b.grade != grade) continue;
                    sb.Append("  <color=").Append(GradeColor(b.grade)).Append('>')
                      .Append(GradeName(b.grade)).Append("  ").Append(b.id)
                      .Append("  ").Append(b.spawnProbability.ToString("F0")).Append('%')
                      .Append("  출력 ").Append(b.baseOutput.ToString("F0")).Append("</color>\n");
                }
            }
            _cachedProbabilityTable = sb.ToString();
            return _cachedProbabilityTable;
        }
    }
}
