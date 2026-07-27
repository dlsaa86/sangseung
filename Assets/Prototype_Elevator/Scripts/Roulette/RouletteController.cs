using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// T-02: Orchestrates 3 TubeControllers with seeded ball streams.
    /// StartSpin() builds streams from the seeded RNG and starts each tube scrolling.
    /// StopTube(int) requests a brake on the chosen tube.
    /// AllStopped becomes true when every tube has finalized its stop.
    /// CollectResults() returns the 3 stopped balls (contract unchanged).
    /// </summary>
    public class RouletteController : MonoBehaviour
    {
        [Header("Tubes")]
        [SerializeField] private TubeController[] _tubes;

        [Header("Ball Data")]
        [SerializeField] private BallDatabase _database;

        [Header("Config (for streamLength)")]
        [SerializeField] private PrototypeConfig _config;

        private System.Random _rng;

        // ── Seed / RNG ──

        /// <summary>
        /// Initialises the internal RNG with the given seed for reproducible streams.
        /// Must be called once per run (RunController.ResetRun).
        /// </summary>
        public void InitializeSeed(int seed)
        {
            _rng = new System.Random(seed);
        }

        // ── Weighted draw helpers ──

        private BallDefinition DrawWeighted()
        {
            if (_database == null || _database.balls == null || _database.balls.Count == 0)
                return null;

            float totalWeight = 0f;
            foreach (BallDefinition ball in _database.balls)
                if (ball != null) totalWeight += ball.spawnProbability;

            if (totalWeight <= 0f) return null;

            double roll      = _rng.NextDouble() * totalWeight;
            float  cumulative = 0f;
            BallDefinition selected = _database.balls[_database.balls.Count - 1];

            foreach (BallDefinition ball in _database.balls)
            {
                if (ball == null) continue;
                cumulative += ball.spawnProbability;
                if (roll < cumulative)
                {
                    selected = ball;
                    break;
                }
            }
            return selected;
        }

        private List<BallDefinition> BuildStream(int length)
        {
            var stream = new List<BallDefinition>(length);
            for (int i = 0; i < length; i++)
                stream.Add(DrawWeighted());
            return stream;
        }

        // ── Public API ──

        /// <summary>
        /// Generates seeded streams for each tube and starts them scrolling.
        /// RNG must be initialised first (InitializeSeed).
        /// </summary>
        public void StartSpin()
        {
            if (_rng == null)
            {
                Debug.LogWarning("[상승] RouletteController.StartSpin(): RNG not initialised — using seed 0.");
                InitializeSeed(0);
            }

            int length = (_config != null) ? _config.streamLength : 32;

            if (_tubes == null) return;

            for (int i = 0; i < _tubes.Length; i++)
            {
                if (_tubes[i] == null) continue;
                List<BallDefinition> stream = BuildStream(length);
                _tubes[i].SetStream(stream);
                _tubes[i].StartScroll();
            }

            Debug.Log($"[상승] RouletteController.StartSpin() — {_tubes.Length} tubes spinning (streamLength={length})");
        }

        /// <summary>
        /// Requests a brake on tube at index (0-based). Ignored if out of range.
        /// </summary>
        public void StopTube(int index)
        {
            if (_tubes == null || index < 0 || index >= _tubes.Length) return;
            if (_tubes[index] == null) return;
            _tubes[index].RequestStop();
            Debug.Log($"[상승] RouletteController.StopTube({index}) requested");
        }

        /// <summary>True when every tube has stopped (IsStopped).</summary>
        public bool AllStopped
        {
            get
            {
                if (_tubes == null || _tubes.Length == 0) return false;
                foreach (var tube in _tubes)
                {
                    if (tube == null) continue;
                    if (!tube.IsStopped) return false;
                }
                return true;
            }
        }

        /// <summary>True when every configured tube stopped within the perfect-stop tolerance.</summary>
        public bool IsPerfectStop
        {
            get
            {
                if (_config == null || _tubes == null || _tubes.Length == 0)
                    return false;

                foreach (TubeController tube in _tubes)
                {
                    if (tube == null || !tube.IsStopped)
                        return false;
                    if (tube.LastStopDistance > _config.perfectStopTolerance)
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Returns the stopped balls from all tubes in order.
        /// Contract unchanged from T-01 (IReadOnlyList&lt;BallDefinition&gt;).
        /// </summary>
        public IReadOnlyList<BallDefinition> CollectResults()
        {
            var results = new List<BallDefinition>();
            if (_tubes == null) return results;
            foreach (var tube in _tubes)
            {
                results.Add(tube != null ? tube.StoppedBall : null);
            }
            return results;
        }

        /// <summary>Resets all tubes to their initial state.</summary>
        public void ResetTubes()
        {
            if (_tubes == null) return;
            foreach (var tube in _tubes)
            {
                if (tube != null) tube.ResetTube();
            }
        }

        /// <summary>Read-only access to tubes (for UI status display).</summary>
        public IReadOnlyList<TubeController> Tubes
        {
            get { return _tubes; }
        }
    }
}

