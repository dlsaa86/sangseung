using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Central balance configuration for the Ascend prototype.
    /// All numeric design values live here — no hardcoding in gameplay scripts.
    /// </summary>
    [CreateAssetMenu(fileName = "PrototypeConfig", menuName = "Ascend/PrototypeConfig", order = 0)]
    public class PrototypeConfig : ScriptableObject
    {
        [Header("Floor Settings")]
        [Tooltip("Number of GenerationTurn states allowed per floor.")]
        public int generationsPerFloor = 3;

        [Header("Power")]
        [Tooltip("Base required power to ascend on floor 0.")]
        public float baseRequiredPower = 100f;

        [Tooltip("Additional required power added per floor number.")]
        public float requiredPowerGrowthPerFloor = 25f;

        [Header("Weight / Overload")]
        [Tooltip("Maximum passenger/cargo weight before overload penalty applies.")]
        public float allowedWeight = 100f;

        [Tooltip("Multiplier applied to required power when the elevator is overloaded.")]
        public float overloadRequiredPowerMultiplier = 1.5f;

        [Header("Economy")]
        [Tooltip("Conversion ratio: excess power → money.")]
        public float powerToMoneyRatio = 1f;

        [Header("Starting Values")]
        [Tooltip("Power at the start of a new run.")]
        public float startingPower = 0f;

        [Tooltip("Money at the start of a new run.")]
        public float startingMoney = 0f;

        [Tooltip("Weight at the start of a new run.")]
        public float startingWeight = 0f;

        [Header("Ball Settings")]
        [Tooltip("Speed at which balls fall through the tubes.")]
        public float ballMoveSpeed = 5f;

        [Tooltip("Gap between consecutive balls inside a tube.")]
        public float ballSpacing = 1f;

        [Tooltip("Delay (seconds) after the stop button is pressed before the ball halts. Keep at 0 so the ball nearest the harvest line at the moment of the press is the one that gets aligned.")]
        public float brakeDelay = 0f;

        [Tooltip("Seconds spent easing the nearest ball onto the harvest line after a stop. 0 snaps instantly.")]
        public float snapDuration = 0.12f;

        [Header("Tube Visuals")]
        [Tooltip("Number of ball spheres visible per tube at any time.")]
        public int visibleBallsPerTube = 7;

        [Tooltip("Total vertical height of the tube's scroll cycle (world units).")]
        public float tubeHeight = 6f;

        [Tooltip("Y offset of the harvest window marker relative to the tube's local origin.")]
        public float harvestWindowOffset = -1.5f;

        [Tooltip("Number of ball entries pre-generated in each tube's stream per spin.")]
        public int streamLength = 32;

        [Header("Determinism")]
        [Tooltip("Fixed seed for the ball RNG; identical seeds produce identical roll sequences across runs.")]
        public int randomSeed = 12345;
    }
}
