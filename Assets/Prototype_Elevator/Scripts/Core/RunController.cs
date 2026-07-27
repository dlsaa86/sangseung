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
        [SerializeField] private PassengerManager    _passengers;

        [Header("State (Inspector — live during Play Mode)")]
        [SerializeField] private ElevatorState _state = new ElevatorState();

        // ── Private run state ──
        private RunState _currentState = RunState.FloorArrival;

        private float _surplus;               // surplus power after a successful resolution
        private float _lastShortfall;         // |deficit| when resolution fails
        private bool  _lastResolutionSuccess; // outcome of the most recent PowerResolution
        private int   _overchargeChoice;      // 0 = Money, 1 = Ascend (set via [1]/[2] keys)
        private int   _pendingExtraFloors;

        private RunOutcome _outcome = RunOutcome.InProgress;

        private float _floorStartPower;       // power value at floor entry; used to reset on retry

        private CombinationResolver.CombinationResult _lastCombination;
        private System.Random _accidentRng = new System.Random(0);

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

        public RunOutcome Outcome => _outcome;

        public OverchargeOption MoneyOption { get; private set; }

        public OverchargeOption AscendOption { get; private set; }

        /// <summary>T-02: Exposes the RouletteController so UI can read tube states.</summary>
        public RouletteController Roulette => _roulette;

        /// <summary>T-04: Exposes the PassengerManager so UI can list candidates and boarded passengers.</summary>
        public PassengerManager Passengers => _passengers;

        /// <summary>
        /// Boards the candidate at <paramref name="index"/> and refreshes the load-derived values.
        /// This is the single boarding path — the key handler calls it too, so automated checks
        /// exercise exactly what a key press does.
        /// </summary>
        public bool TryBoardCandidate(int index)
        {
            if (_currentState != RunState.PassengerSelection) return false;
            if (_passengers == null || !_passengers.Board(index)) return false;
            RecalculateLoad();
            return true;
        }

        /// <summary>
        /// Selects how the surplus will be spent (0 = money, 1 = extra ascent).
        /// Same single path the [1]/[2] keys use during OverchargeAllocation.
        /// </summary>
        public bool SetOverchargeChoice(int choice)
        {
            if (_currentState != RunState.OverchargeAllocation) return false;
            _overchargeChoice = choice == 1 ? 1 : 0;
            OverchargeOption picked = _overchargeChoice == 1 ? AscendOption : MoneyOption;
            Debug.Log($"[상승] OverchargeAllocation 선택: {picked.Label}");
            return true;
        }

        /// <summary>T-03: Effect chain log from the most recent generation turn, for UI display.</summary>
        public EffectResolver Effects => _effects;

        // ── Unity lifecycle ──

        private void Start()
        {
            ResetRun();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_outcome != RunOutcome.InProgress)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                    ResetRun();
                return;
            }

            // ── State-gated inputs ──

            if (_currentState == RunState.GenerationTurn)
            {
                // [1]/[2]/[3] — stop individual tubes (only active in GenerationTurn)
                if (keyboard.digit1Key.wasPressedThisFrame && _roulette != null)
                    _roulette.StopTube(0);
                else if (keyboard.digit2Key.wasPressedThisFrame && _roulette != null)
                    _roulette.StopTube(1);
                else if (keyboard.digit3Key.wasPressedThisFrame && _roulette != null)
                    _roulette.StopTube(2);

                // Auto-resolve once all tubes have stopped
                if (!_turnResolved && _roulette != null && _roulette.AllStopped)
                    ResolveGenerationTurn();
            }
            else if (_currentState == RunState.PassengerSelection)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    TryBoardCandidate(0);
                }
                else if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    TryBoardCandidate(1);
                }
                else if (keyboard.digit0Key.wasPressedThisFrame)
                {
                    Debug.Log("[상승] PassengerSelection: no passenger boarded.");
                }
            }
            else if (_currentState == RunState.OverchargeAllocation)
            {
                // [1] = Money, [2] = Ascend
                if (keyboard.digit1Key.wasPressedThisFrame)
                    SetOverchargeChoice(0);
                else if (keyboard.digit2Key.wasPressedThisFrame)
                    SetOverchargeChoice(1);
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
            _outcome = RunOutcome.InProgress;

            if (_state == null)
                _state = new ElevatorState();

            _state.Initialize(_config);
            if (_passengers != null)
            {
                int seed = _config != null ? _config.randomSeed : 0;
                _passengers.InitializeSeed(seed);
                _passengers.ResetPassengers();
            }
            else
            {
                Debug.LogWarning("[상승] RunController.ResetRun(): PassengerManager is missing.");
            }

            _currentState          = RunState.FloorArrival;
            _surplus               = 0f;
            _lastShortfall         = 0f;
            _lastResolutionSuccess = false;
            _overchargeChoice      = 0;
            _pendingExtraFloors    = 0;
            _lastCombination       = default;
            MoneyOption            = default;
            AscendOption           = default;
            _floorStartPower       = _config != null ? _config.startingPower : 0f;
            _turnResolved          = false;
            int randomSeed = _config != null ? _config.randomSeed : 0;
            _accidentRng = new System.Random(unchecked(randomSeed * 17 ^ 0x2ACC));

            if (_floor != null)
                _floor.EnterFloor(0);
            RecalculateLoad();
            if (_passengers != null)
                _passengers.GenerateCandidates(_floor != null ? _floor.CurrentFloor : 0);

            if (_roulette != null)
                _roulette.InitializeSeed(randomSeed);
            if (_effects != null)
                _effects.InitializeSeed(randomSeed);
            if (_roulette != null)
                _roulette.ResetTubes();

            if (_config == null)
                return;

            Debug.Log($"[상승] Run Reset → FloorArrival (Floor {(_floor != null ? _floor.CurrentFloor : 0)}, Turn 0/{_config.generationsPerFloor}, Seed {_config.randomSeed})");
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
            if (_outcome != RunOutcome.InProgress)
                return;

            if (_config == null)
            {
                Debug.LogWarning("[상승] RunController.AdvanceState(): config is missing; state cannot advance.");
                return;
            }

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
                    int climb = 1 + _pendingExtraFloors;
                    _pendingExtraFloors = 0;

                    _floorStartPower = _config.startingPower + _state.BankedPower;
                    _state.Power = _floorStartPower;
                    _state.BankedPower = 0f;
                    _state.CurrentTurn = 0;
                    _state.RetriesThisFloor = 0;

                    int currentFloor = _floor != null ? _floor.CurrentFloor : 0;
                    int next = currentFloor + climb;
                    if (_floor != null)
                        _floor.EnterFloor(next);
                    _state.HighestFloorReached = Mathf.Max(_state.HighestFloorReached, next);

                    if (next >= _config.targetFloor)
                    {
                        SucceedRun();
                        return;
                    }

                    if (_passengers != null)
                        _passengers.GenerateCandidates(next);
                    RecalculateLoad();
                    to = RunState.FloorArrival;
                    break;

                default:
                    to = _currentState;
                    break;
            }

            _currentState = to;

            if (to == RunState.FloorArrival)
            {
                RecalculateLoad();
            }
            Debug.Log($"[상승] {from} -> {to} (Floor {(_floor != null ? _floor.CurrentFloor : 0)}, Turn {_state.CurrentTurn}/{_config.generationsPerFloor})");

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
            if (_roulette != null)
                _roulette.StartSpin();
            Debug.Log($"[상승] StartGenerationTurn — Turn {_state.CurrentTurn}, tubes spinning.");
        }

        /// <summary>
        /// Called automatically when AllStopped becomes true.
        /// Collects results, runs the T-03 hook, resolves the combination, and accumulates power.
        /// </summary>
        private void ResolveGenerationTurn()
        {
            IReadOnlyList<BallDefinition> balls = _roulette != null
                ? _roulette.CollectResults()
                : new List<BallDefinition>();

            // T-03 effect input now includes the actual quality of all three stops.
            bool perfectStop = _roulette != null && _roulette.IsPerfectStop;
            GenerationContext context = _resolver != null
                ? _resolver.BuildContext(
                    balls,
                    _state.IsOverloaded,
                    perfectStop,
                    _state.CurrentTurn,
                    _floor != null ? _floor.CurrentFloor : 0)
                : new GenerationContext
                {
                    Balls = balls != null ? new List<BallDefinition>(balls) : new List<BallDefinition>(),
                    IsOverloaded = _state.IsOverloaded,
                    PerfectStop = perfectStop,
                    TurnIndex = _state.CurrentTurn,
                    FloorIndex = _floor != null ? _floor.CurrentFloor : 0
                };
            context = _effects != null ? _effects.Resolve(context) : context;
            if (_effects == null)
                context.FinalPower = context.ComputeCurrentPower();

            _lastCombination = _resolver != null
                ? _resolver.Resolve(balls)
                : default;
            _state.Power += context.FinalPower;
            _state.LastGenerationPower = context.FinalPower;
            _state.LastRollSummary = $"{_lastCombination.Summary} | Combination: {context.Combination}";
            _state.LastEffectLog = _effects != null ? _effects.BuildLogText() : string.Empty;
            _turnResolved               = true;

            Debug.Log($"[상승] ResolveGenerationTurn Turn {_state.CurrentTurn}: {_lastCombination.Summary} | Power: {_state.Power:F1} / Required: {(_floor != null ? _floor.RequiredPower : 0f):F1}");
        }

        // ── Power resolution ──

        private void PerformPowerResolution()
        {
            _state.LastAccidentOccurred = false;
            _state.LastAccidentLoss     = 0f;
            _state.LastAccidentCause    = string.Empty;

            float chance = FloorMath.ComputeAccidentChance(
                _config, _state.Weight, _state.AllowedWeight);
            _state.AccidentChance = chance;

            if (chance > 0f && _accidentRng.NextDouble() < chance)
            {
                float loss = FloorMath.ComputeAccidentPowerLoss(_config, _state.Power);
                _state.Power -= loss;
                _state.LastAccidentOccurred = true;
                _state.LastAccidentLoss     = loss;
                _state.TotalAccidents++;
                float over = Mathf.Max(0f, _state.Weight - _state.AllowedWeight);
                _state.LastAccidentCause = $"과적 {over:F1} 초과 (확률 {chance:P0}) — 전력 {loss:F1} 손실";
                Debug.LogWarning($"[상승] 과적 사고! {_state.LastAccidentCause}");
            }

            float requiredPower = _floor != null ? _floor.RequiredPower : 0f;
            _surplus = _state.Power - requiredPower;

            if (_surplus >= 0f)
            {
                _lastResolutionSuccess = true;
                _lastShortfall         = 0f;
                MoneyOption = FloorMath.BuildMoneyOption(_config, _surplus);
                AscendOption = FloorMath.BuildAscendOption(_config, _surplus);
                Debug.Log($"[상승] PowerResolution: SUCCESS — Power {_state.Power:F1} >= Required {requiredPower:F1} (surplus +{_surplus:F1})");
            }
            else
            {
                _lastResolutionSuccess = false;
                _lastShortfall         = -_surplus;
                Debug.Log($"[상승] PowerResolution: FAIL — Power {_state.Power:F1} < Required {requiredPower:F1} (short {_lastShortfall:F1})");
                _state.RetriesThisFloor++;
                _state.TotalRetries++;
                if (_state.RetriesThisFloor > _config.maxRetriesPerFloor)
                {
                    FailRun($"{(_floor != null ? _floor.CurrentFloor : 0)}층에서 요구 전력 미달 (재시도 {_config.maxRetriesPerFloor}회 초과)");
                    return;
                }
                RetryFloor();
            }
        }

        /// <summary>Refreshes all load-derived state and the active passenger effects.</summary>
        private void RecalculateLoad()
        {
            float passengerWeight = _passengers != null ? _passengers.TotalWeight : 0f;
            float allowedBonus = _passengers != null ? _passengers.TotalAllowedWeightBonus : 0f;

            _state.Weight = (_config != null ? _config.startingWeight : 0f) + passengerWeight;
            _state.AllowedWeight = (_config != null ? _config.allowedWeight : 0f) + allowedBonus;
            _state.BoardedCount = _passengers != null ? _passengers.Boarded.Count : 0;
            _state.AccidentChance = FloorMath.ComputeAccidentChance(_config, _state.Weight, _state.AllowedWeight);

            if (_floor != null)
                _floor.UpdateRequiredPower(_state.Weight, _state.IsOverloaded);

            if (_effects != null)
                _effects.SetActiveEffects(_passengers != null
                    ? _passengers.ActiveEffects
                    : new List<EffectDefinition>());
        }

        private void RetryFloor()
        {
            _state.Power       = _floorStartPower;
            _state.CurrentTurn = 0;
            _currentState      = RunState.PassengerSelection;
            Debug.Log($"[상승] Floor Retry → PassengerSelection (Floor {(_floor != null ? _floor.CurrentFloor : 0)}, Power reset to {_state.Power:F1})");
        }

        // ── Overcharge allocation ──

        private void ApplyOverchargeAllocation()
        {
            OverchargeOption chosen = (_overchargeChoice == 1) ? AscendOption : MoneyOption;

            if (chosen.Mode == OverchargeMode.Ascend)
            {
                _pendingExtraFloors = chosen.FloorsGained;
                _state.BankedPower = chosen.PowerCarried;
            }
            else
            {
                _pendingExtraFloors = 0;
                _state.Money += chosen.MoneyGained;
                _state.TotalMoneyEarned += chosen.MoneyGained;
                _state.BankedPower = 0f;
            }

            Debug.Log($"[상승] 초과 전력 분배: {chosen.Label}");
            _overchargeChoice = 0;
        }

        private void SucceedRun()
        {
            _outcome = RunOutcome.Success;
            Debug.Log($"[상승] === 런 성공 === {(_floor != null ? _floor.CurrentFloor : _state.HighestFloorReached)}층 도달 / 돈 {_state.Money:F0} / 사고 {_state.TotalAccidents}회 / 재시도 {_state.TotalRetries}회");
        }

        private void FailRun(string reason)
        {
            _outcome = RunOutcome.Failure;
            _state.LastFailureReason = reason;
            Debug.Log($"[상승] === 런 실패 === {reason} / 최고 도달 {_state.HighestFloorReached}층");
        }
    }
}

