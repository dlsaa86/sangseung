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

        /// <summary>True when Weight exceeds AllowedWeight.</summary>
        public bool IsOverloaded => Weight > AllowedWeight;

        /// <summary>Resets all fields to their initial values from the given config.</summary>
        public void Initialize(PrototypeConfig config)
        {
            Power              = config.startingPower;
            Money              = config.startingMoney;
            Weight             = config.startingWeight;
            AllowedWeight      = config.allowedWeight;
            CurrentTurn        = 0;
            BankedPower        = 0f;
            LastRollSummary    = string.Empty;
            LastGenerationPower = 0f;
            LastEffectLog      = string.Empty;
        }
    }
}
