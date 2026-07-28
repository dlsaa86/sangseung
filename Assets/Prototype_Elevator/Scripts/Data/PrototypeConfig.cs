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

        [Header("Weight → Power")]
        [Tooltip("Required power added per unit of total weight.")]
        public float weightToPowerFactor = 2f;

        [Header("Passengers")]
        public int maxPassengerSlots = 6;
        public int passengerCandidatesPerFloor = 2;

        [Header("Overload Accident (T-06)")]
        [Tooltip("Accident chance added per unit of overweight.")]
        public float accidentChancePerOverweightUnit = 0.06f;

        [Tooltip("Maximum accident chance.")]
        [Range(0f, 1f)] public float maxAccidentChance = 0.75f;

        [Tooltip("Ratio of current power lost when an accident occurs.")]
        [Range(0f, 1f)] public float accidentPowerLossRatio = 0.35f;

        [Header("Economy")]
        [Tooltip("Conversion ratio: excess power → money.")]
        public float powerToMoneyRatio = 1f;

        [Header("Run (T-07)")]
        [Tooltip("이 층에 도달하면 런 성공.")]
        public int targetFloor = 10;

        [Tooltip("한 층에서 허용되는 발전 재시도 횟수. 초과하면 런 실패.")]
        public int maxRetriesPerFloor = 2;

        [Header("Overcharge (T-05)")]
        [Tooltip("추가 상승 1개 층에 필요한 초과 전력.")]
        public float powerPerExtraFloor = 60f;

        [Tooltip("추가 상승으로 한 번에 오를 수 있는 최대 층수.")]
        public int maxExtraFloorsPerAllocation = 3;

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

        [Header("Perfect Stop")]
        [Tooltip("Distance from the harvest-window centre within which a stop counts as perfect.")]
        public float perfectStopTolerance = 0.12f;

        [Header("Stop Accuracy")]
        [Tooltip("Within this distance a stop counts as good; beyond it the stop is a miss.")]
        public float goodStopTolerance = 0.28f;

        [Tooltip("Power multiplier contributed by a perfectly timed tube.")]
        public float perfectStopPowerMultiplier = 1.0f;

        [Tooltip("Power multiplier contributed by a good tube.")]
        public float goodStopPowerMultiplier = 0.72f;

        [Tooltip("Power multiplier contributed by a missed tube. If mistimed stops cost nothing, timing is not a game.")]
        public float missStopPowerMultiplier = 0.40f;

        [Tooltip("Number of ball entries pre-generated in each tube's stream per spin.")]
        public int streamLength = 32;

        [Header("Determinism")]
        [Tooltip("Fixed seed for the ball RNG; identical seeds produce identical roll sequences across runs.")]
        public int randomSeed = 12345;
    }
}
