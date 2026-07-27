using System;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Runtime data snapshot for a single run (aka RunData).
    /// Pure C# class — no MonoBehaviour. Owned and serialized by RunController.
    /// </summary>
    [Serializable]
    public class ElevatorState
    {
        [Header("Resources")]
        [Tooltip("Accumulated power generated this floor.")]
        public float Power;

        [Tooltip("Money carried into this floor.")]
        public float Money;

        [Tooltip("Total passenger/cargo weight currently loaded.")]
        public float Weight;

        [Tooltip("Maximum weight allowed before overload penalty.")]
        public float AllowedWeight;

        [Header("Passenger / Accident")]
        [Tooltip("Current visible probability of an overload accident, from 0 to 1.")]
        public float AccidentChance;

        public bool LastAccidentOccurred;
        public float LastAccidentLoss;
        public string LastAccidentCause;
        public int BoardedCount;

        [Header("Turn Tracking")]
        [Tooltip("Current generation turn index within the floor (1-indexed, 0 = before first turn).")]
        public int CurrentTurn;

        [Header("Generation Results")]
        [Tooltip("Surplus power banked from the previous floor's Ascend overcharge choice. Applied to next-floor starting power.")]
        public float BankedPower;

        [Tooltip("Summary string from the last ball roll (e.g. 'Ball_04(A) Ball_02(C) Ball_07(R) -> CommonAdvancedRare (+34.0)').")]
        public string LastRollSummary;

        [Tooltip("Power gained in the most recent generation turn.")]
        public float LastGenerationPower;

        [Tooltip("Effect pipeline log from the most recent generation turn.")]
        public string LastEffectLog;

        [Header("Run Tracking")]
        public int RetriesThisFloor;
        public int TotalRetries;
        public int HighestFloorReached;
        public float TotalMoneyEarned;
        public int TotalAccidents;
        public string LastFailureReason;

        /// <summary>True when Weight exceeds AllowedWeight.</summary>
        public bool IsOverloaded => Weight > AllowedWeight;

        /// <summary>Resets all fields to their initial values from the given config.</summary>
        public void Initialize(PrototypeConfig config)
        {
            Power              = config != null ? config.startingPower : 0f;
            Money              = config != null ? config.startingMoney : 0f;
            Weight             = config != null ? config.startingWeight : 0f;
            AllowedWeight      = config != null ? config.allowedWeight : 0f;
            AccidentChance     = 0f;
            LastAccidentOccurred = false;
            LastAccidentLoss    = 0f;
            LastAccidentCause   = string.Empty;
            BoardedCount        = 0;
            CurrentTurn        = 0;
            BankedPower        = 0f;
            LastRollSummary    = string.Empty;
            LastGenerationPower = 0f;
            LastEffectLog      = string.Empty;
            RetriesThisFloor   = 0;
            TotalRetries       = 0;
            HighestFloorReached = 0;
            TotalMoneyEarned    = 0f;
            TotalAccidents     = 0;
            LastFailureReason   = string.Empty;
        }
    }
}
