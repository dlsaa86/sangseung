using System;
using System.Collections.Generic;

namespace Ascend.Prototype
{
    /// <summary>One generation turn inside a simulated floor.</summary>
    [Serializable]
    public class SimTurnRecord
    {
        public int    turnIndex;
        public string ball0, ball1, ball2;
        public string grade0, grade1, grade2;
        public bool   perfectStop;
        public float  accuracyMultiplier;
        public string combination;
        public float  powerBeforeEffects;
        public float  powerAfterEffects;
        public float  moneyDelta;
        public string effectLog;
    }

    /// <summary>One floor attempt inside a simulated run.</summary>
    [Serializable]
    public class SimFloorRecord
    {
        public int    floorIndex;
        public string candidatesOffered;
        public string passengerBoarded;
        public float  totalWeight;
        public float  allowedWeight;
        public bool   overloaded;
        public float  accidentChance;
        public bool   accidentOccurred;
        public float  accidentLoss;
        public float  requiredPower;
        public float  finalPower;
        public bool   success;
        public int    retries;
        public float  surplus;
        public string overchargeChoice;
        public int    floorsClimbed;
        public List<SimTurnRecord> turns = new List<SimTurnRecord>();
    }

    /// <summary>One complete simulated run.</summary>
    [Serializable]
    public class SimRunRecord
    {
        public int    runIndex;
        public int    seed;
        public string policyName;
        public string outcome;
        public string failureReason;
        public int    highestFloor;
        public float  finalMoney;
        public int    totalAccidents;
        public int    totalRetries;
        public List<SimFloorRecord> floors = new List<SimFloorRecord>();
    }

    /// <summary>Aggregate result for a batch of simulated runs.</summary>
    [Serializable]
    public class SimBatchResult
    {
        public string generatedAtUtc;
        public int    runCount;
        public int    successCount;
        public float  averageHighestFloor;
        public float  averageMoney;
        public float  averageAccidents;
        public List<SimRunRecord> runs = new List<SimRunRecord>();
    }
}
