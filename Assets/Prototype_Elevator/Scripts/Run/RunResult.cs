using System.Collections.Generic;

namespace Ascend.Prototype.Run
{
    /// <summary>Read-only run-level snapshot exposed by RunSession.</summary>
    public sealed class RunResult
    {
        internal RunResult(bool succeeded, bool complete, bool failed, int highestFloor,
            float money, float carriedWeight, string failureReason,
            IReadOnlyList<FloorResult> floors)
        {
            Succeeded = succeeded;
            IsComplete = complete;
            IsFailed = failed;
            HighestFloorReached = highestFloor;
            Money = money;
            CarriedWeight = carriedWeight;
            FailureReason = failureReason;
            Floors = floors;
        }

        public bool Succeeded { get; }
        public bool IsComplete { get; }
        public bool IsFailed { get; }
        public int HighestFloorReached { get; }
        public float Money { get; }
        public float CarriedWeight { get; }
        public string FailureReason { get; }
        public IReadOnlyList<FloorResult> Floors { get; }
    }
}
