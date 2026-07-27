using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascend.Prototype
{
    /// <summary>
    /// Central controller owning the run state machine and ElevatorState.
    /// T-02: GenerationTurn now drives 3 TubeControllers (spin → player stops → auto-resolve).
    /// Input: UnityEngine.InputSystem.Keyboard.current polling — no .inputactions asset.
    /// </summary>
    public class RunController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private PrototypeConfig _config;

        [Header("References")]
        [SerializeField] private FloorController     _floor;
        [SerializeField] private RouletteController  _roulette;
        [SerializeField] private EffectResolver      _effects;
        [SerializeField] private CombinationResolver _resolver;

        [Header("State (Inspector — live during Play Mode)")]
        [SerializeField] private ElevatorState _state = new ElevatorState();

        // ── Private run state ──
        private RunState _currentState = RunState.FloorArrival;

        private float _surplus;               // surplus power after a successful resolution
        private float _lastShortfall;         // |deficit| when resolution fails
        private bool  _lastResolutionSuccess; // outcome of the most recent PowerResolution
        private int   _overchargeChoice;      // 0 = Money, 1 = Ascend (set via [1]/[2] keys)

        private float _floorStartPower;       // power value at floor entry; used to reset on retry

        private CombinationResolver.CombinationResult _lastCombination;

        // ── T-02: generation turn state ──
        /// <summary>True once all 3 tubes have stopped and the combination has been resolved this turn.</summary>
        private bool _turnResolved;

        // ── Public read-only surface ──

        /// <summary>Active phase of the floor cycle.</summary>
        public RunState CurrentState => _currentState;

        /// <summary>Live runtime data (power, money, weight, turn index).</summary>
        public ElevatorState State => _state;

        /// <summary>Floor tracking and required-power computation.</summary>
        public FloorController Floor => _floor;

        /// <summary>Balance configuration asset.</summary>
        public PrototypeConfig Config => _config;

        /// <summary>The last combination resolved in a generation turn.</summary>
        public CombinationResolver.CombinationResult LastCombination => _lastCombination;

        /// <summary>Surplus power after the most recent successful resolution (positive value).</summary>
        public float Surplus => _surplus;

        /// <summary>0 = Money, 1 = Ascend. Updated via [1]/[2] keys in OverchargeAllocation.</summary>
        public int OverchargeChoice => _overchargeChoice;

        /// <summary>True if the last PowerResolution was a success.</summary>
        public bool LastResolutionSuccess => _lastResolutionSuccess;

        /// <summary>Absolute power deficit when the last resolution failed (0 when success).</summary>
        public float LastShortfall => _lastShortfall;

        /// <summary>T-02: Exposes the RouletteController so UI can read tube states.</summary>
        public RouletteController Roulette => _roulette;

        // ── Unity lifecycle ──

        private void Start()
        {
            ResetRun();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // ── State-gated inputs ──

            if (_currentState == RunState.GenerationTurn)
            {
                // [1]/[2]/[3] — stop individual tubes (only active in GenerationTurn)
                if (keyboard.digit1Key.wasPressedThisFrame)
                    _roulette.StopTube(0);
                else if (keyboard.digit2Key.wasPressedThisFrame)
                    _roulette.StopTube(1);
                else if (keyboard.digit3Key.wasPressedThisFrame)
                    _roulette.StopTube(2);

                // Auto-resolve once all tubes have stopped
                if (!_turnResolved && _roulette.AllStopped)
                    ResolveGenerationTurn();
            }
            else if (_currentState == RunState.OverchargeAllocation)
            {
                // [1] = Money, [2] = Ascend
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    _overchargeChoice = 0;
                    Debug.Log("[상승] OverchargeAllocation: [1] Money selected");
                }
                else if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    _overchargeChoice = 1;
                    Debug.Log("[상승] OverchargeAllocation: [2] Ascend selected");
                }
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
                AdvanceState();

            if (keyboard.rKey.wasPressedThisFrame)
                ResetRun();
        }

        // ── Public actions ──

        /// <summary>
        /// Resets all run data to config defaults and returns to FloorArrival on floor 0.
        /// Re-initialises the RNG seed so the same seed always produces the same stream sequence.
        /// </summary>
        public void ResetRun()
        {
            _state.Initialize(_config);
            _currentState          = RunState.FloorArrival;
            _surplus               = 0f;
            _lastShortfall         = 0f;
            _lastResolutionSuccess = false;
            _overchargeChoice      = 0;
            _lastCombination       = default;
            _floorStartPower       = _config.startingPower;
            _turnResolved          = false;

            _floor.EnterFloor(0);
            _floor.UpdateRequiredPower(_state.IsOverloaded);
            _roulette.InitializeSeed(_config.randomSeed);
            _roulette.ResetTubes();

            Debug.Log($"[상승] Run Reset → FloorArrival (Floor {_floor.CurrentFloor}, Turn 0/{_config.generationsPerFloor}, Seed {_config.randomSeed})");
        }

        /// <summary>
        /// Advances the state machine one step (Space key).
        ///
        /// In GenerationTurn, Space is blocked until all 3 tubes have stopped and
        /// the combination has been resolved (_turnResolved == true).
        ///
        /// Transition table:
        ///   FloorArrival         → PassengerSelection
        ///   PassengerSelection   → GenerationTurn (turn=1) + StartSpin
        ///   GenerationTurn       → GenerationTurn (turn++, new spin) when turn &lt; max
        ///                        → PowerResolution when turn == max
        ///                          → on fail: immediately redirects to PassengerSelection (RetryFloor)
        ///   PowerResolution      → OverchargeAllocation  (only on success)
        ///   OverchargeAllocation → Ascending             (ApplyOverchargeAllocation)
        ///   Ascending            → FloorArrival          (floor++, BankedPower consumed)
        /// </summary>
        public void AdvanceState()
        {
            RunState from = _currentState;
            RunState to;

            switch (_currentState)
            {
                case RunState.FloorArrival:
                    to = RunState.PassengerSelection;
                    break;

                case RunState.PassengerSelection:
                    _state.CurrentTurn = 1;
                    to = RunState.GenerationTurn;
                    break;

                case RunState.GenerationTurn:
                    // Block Space until turn is resolved
                    if (!_turnResolved)
                    {
                        Debug.Log("[상승] GenerationTurn: 아직 세 통관 정지/판정 미완료 — Space 무시");
                        return;
                    }
                    if (_state.CurrentTurn < _config.generationsPerFloor)
                    {
                        _state.CurrentTurn++;
                        to = RunState.GenerationTurn;
                    }
                    else
                    {
                        to = RunState.PowerResolution;
                    }
                    break;

                case RunState.PowerResolution:
                    to = RunState.OverchargeAllocation;
                    break;

                case RunState.OverchargeAllocation:
                    ApplyOverchargeAllocation();
                    to = RunState.Ascending;
                    break;

                case RunState.Ascending:
                    _floorStartPower    = _config.startingPower + _state.BankedPower;
                    _state.Power        = _floorStartPower;
                    _state.BankedPower  = 0f;
                    _state.CurrentTurn  = 0;
                    _floor.EnterFloor(_floor.CurrentFloor + 1);
                    _floor.UpdateRequiredPower(_state.IsOverloaded);
                    to = RunState.FloorArrival;
                    break;

                default:
                    to = _currentState;
                    break;
            }

            _currentState = to;
            Debug.Log($"[상승] {from} -> {to} (Floor {_floor.CurrentFloor}, Turn {_state.CurrentTurn}/{_config.generationsPerFloor})");

            // Post-transition hooks
            if (to == RunState.GenerationTurn)
            {
                StartGenerationTurn();
            }
            else if (to == RunState.PowerResolution)
            {
                PerformPowerResolution();
                // On failure, PerformPowerResolution calls RetryFloor() which overrides _currentState.
            }
        }

        // ── T-02: Generation turn ──

        /// <summary>
        /// Called every time the state machine enters (or re-enters) GenerationTurn.
        /// Resets resolved flag and starts all tubes spinning.
        /// </summary>
        private void StartGenerationTurn()
        {
            _turnResolved = false;
            _roulette.StartSpin();
            Debug.Log($"[상승] StartGenerationTurn — Turn {_state.CurrentTurn}, tubes spinning.");
        }

        /// <summary>
        /// Called automatically when AllStopped becomes true.
        /// Collects results, runs the T-03 hook, resolves the combination, and accumulates power.
        /// </summary>
        private void ResolveGenerationTurn()
        {
            IReadOnlyList<BallDefinition> balls = _roulette.CollectResults();

            // T-03 hook — currently a no-op stub preserved for future effect chain insertion.
            _effects.ResolveEffects();

            _lastCombination            = _resolver.Resolve(balls);
            _state.Power               += _lastCombination.Power;
            _state.LastGenerationPower  = _lastCombination.Power;
            _state.LastRollSummary      = _lastCombination.Summary;
            _turnResolved               = true;

            Debug.Log($"[상승] ResolveGenerationTurn Turn {_state.CurrentTurn}: {_lastCombination.Summary} | Power: {_state.Power:F1} / Required: {_floor.RequiredPower:F1}");
        }

        // ── Power resolution ──

        private void PerformPowerResolution()
        {
            _surplus = _state.Power - _floor.RequiredPower;

            if (_surplus >= 0f)
            {
                _lastResolutionSuccess = true;
                _lastShortfall         = 0f;
                Debug.Log($"[상승] PowerResolution: SUCCESS — Power {_state.Power:F1} >= Required {_floor.RequiredPower:F1} (surplus +{_surplus:F1})");
            }
            else
            {
                _lastResolutionSuccess = false;
                _lastShortfall         = -_surplus;
                Debug.Log($"[상승] PowerResolution: FAIL — Power {_state.Power:F1} < Required {_floor.RequiredPower:F1} (short {_lastShortfall:F1})");
                RetryFloor();
            }
        }

        private void RetryFloor()
        {
            _state.Power       = _floorStartPower;
            _state.CurrentTurn = 0;
            _currentState      = RunState.PassengerSelection;
            Debug.Log($"[상승] Floor Retry → PassengerSelection (Floor {_floor.CurrentFloor}, Power reset to {_state.Power:F1})");
        }

        // ── Overcharge allocation ──

        private void ApplyOverchargeAllocation()
        {
            if (_overchargeChoice == 1)
            {
                _state.BankedPower = _surplus;
                Debug.Log($"[상승] OverchargeAllocation: Ascend — BankedPower = {_surplus:F1}");
            }
            else
            {
                float earned = _surplus * _config.powerToMoneyRatio;
                _state.Money += earned;
                Debug.Log($"[상승] OverchargeAllocation: Money — +{earned:F1} money (surplus={_surplus:F1} x ratio={_config.powerToMoneyRatio})");
            }

            _overchargeChoice = 0;
        }
    }
}

