using System.Text;
using TMPro;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Reads state from RunController every frame and writes it to the TMP HUD.
    /// T-02 additions: per-tube status line (run / brake / STOP+ball) and
    ///                 GenerationTurn context-sensitive stop hints.
    /// </summary>
    public class PrototypeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunController _runController;

        [Tooltip("Multiline TMP text positioned at the top-left — shows all live values.")]
        [SerializeField] private TMP_Text _hudText;

        [Tooltip("Bottom-centre TMP text — keyboard shortcut hints, updated per state.")]
        [SerializeField] private TMP_Text _hintText;

        private readonly StringBuilder _sb      = new StringBuilder(512);
        private readonly StringBuilder _sbTubes = new StringBuilder(128);

        private void Start()
        {
            if (_hintText != null)
                _hintText.text = "[Space] Advance   [R] Reset";
        }

        private void Update()
        {
            if (_runController == null) return;

            RunState currentState = _runController.CurrentState;

            // ── Hint text (context-sensitive) ──
            if (_hintText != null)
            {
                if (currentState == RunState.OverchargeAllocation)
                {
                    _hintText.text = "[Space] Advance   [R] Reset   [1]Money [2]Ascend";
                }
                else if (currentState == RunState.GenerationTurn)
                {
                    // Check tube resolution via Roulette getter
                    bool allStopped = _runController.Roulette != null && _runController.Roulette.AllStopped;
                    _hintText.text = allStopped
                        ? "[Space] Next   [R] Reset"
                        : "[1][2][3] Stop tubes   [R] Reset";
                }
                else
                {
                    _hintText.text = "[Space] Advance   [R] Reset";
                }
            }

            if (_hudText == null) return;

            ElevatorState   state        = _runController.State;
            FloorController floor        = _runController.Floor;
            PrototypeConfig config       = _runController.Config;

            int   currentFloor  = floor  != null ? floor.CurrentFloor  : 0;
            float requiredPower = floor  != null ? floor.RequiredPower  : 0f;
            int   maxTurns      = config != null ? config.generationsPerFloor : 0;

            // Weight row — highlighted red when overloaded
            bool   overloaded  = state.IsOverloaded;
            string weightOpen  = overloaded ? "<color=red>" : string.Empty;
            string weightClose = overloaded ? "</color>"    : string.Empty;

            // ── Roll result line ──
            string rollLine = string.IsNullOrEmpty(state.LastRollSummary)
                ? string.Empty
                : $"\nRoll: {state.LastRollSummary}";

            // ── Resolution result line ──
            string resolutionLine = string.Empty;
            bool inResolutionState = currentState == RunState.PowerResolution
                                  || currentState == RunState.OverchargeAllocation;
            bool hadRecentFail     = !_runController.LastResolutionSuccess
                                  && _runController.LastShortfall > 0f;

            if (inResolutionState)
            {
                resolutionLine = _runController.LastResolutionSuccess
                    ? $"\n<color=green>SUCCESS (surplus +{_runController.Surplus:F1})</color>"
                    : $"\n<color=red>FAIL (short {_runController.LastShortfall:F1})</color>";
            }
            else if (hadRecentFail)
            {
                resolutionLine = $"\n<color=red>FAIL (short {_runController.LastShortfall:F1}) — RETRYING</color>";
            }

            // ── Overcharge allocation hint line ──
            string overchargeLine = string.Empty;
            if (currentState == RunState.OverchargeAllocation)
            {
                string selected = _runController.OverchargeChoice == 1 ? "Ascend" : "Money";
                overchargeLine = $"\n[1] Money  [2] Ascend  (selected: {selected})";
            }

            // ── Tube status line (T-02) ──
            string tubesLine = BuildTubesLine();

            // ── Assemble HUD ──
            _sb.Clear();
            _sb.Append($"State:    {currentState}\n");
            _sb.Append($"Floor:    {currentFloor}   Turn: {state.CurrentTurn}/{maxTurns}\n");
            _sb.Append($"Power:    {state.Power:F1} / Required {requiredPower:F1}\n");
            _sb.Append($"Money:    {state.Money:F1}\n");
            _sb.Append($"{weightOpen}Weight:   {state.Weight:F1} / {state.AllowedWeight:F1}{weightClose}");
            if (!string.IsNullOrEmpty(tubesLine))  _sb.Append(tubesLine);
            if (!string.IsNullOrEmpty(rollLine))   _sb.Append(rollLine);
            if (!string.IsNullOrEmpty(resolutionLine)) _sb.Append(resolutionLine);
            if (!string.IsNullOrEmpty(overchargeLine)) _sb.Append(overchargeLine);

            _hudText.text = _sb.ToString();
        }

        // ── Helpers ──

        private string BuildTubesLine()
        {
            if (_runController == null) return string.Empty;
            RouletteController roulette = _runController.Roulette;
            if (roulette == null) return string.Empty;

            var tubes = roulette.Tubes;
            if (tubes == null || tubes.Count == 0) return string.Empty;

            _sbTubes.Clear();
            _sbTubes.Append("\nTubes:");
            for (int i = 0; i < tubes.Count; i++)
            {
                TubeController tube = tubes[i];
                string status;
                if (tube == null)
                {
                    status = "N/A";
                }
                else if (tube.IsStopped)
                {
                    string ballId = tube.StoppedBall != null ? tube.StoppedBall.id : "?";
                    status = $"STOP {ballId}";
                }
                else if (tube.IsBraking)
                {
                    status = "brake";
                }
                else
                {
                    status = "run";
                }
                _sbTubes.Append($" [{i + 1}:{status}]");
            }
            return _sbTubes.ToString();
        }
    }
}

