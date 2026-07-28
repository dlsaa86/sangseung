using System;
using System.Collections.Generic;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype
{
    /// <summary>One spin's complete balance measurement.</summary>
    [Serializable]
    public sealed class SimSpinRecord
    {
        public int spinIndex;
        public SymbolKind[] cells = new SymbolKind[SpinBoard.Cells];
        public Dictionary<SymbolKind, float> symbolWeights = new Dictionary<SymbolKind, float>();
        public float normalSoulBasePower;
        public float normalSoulPower;
        public int normalSouls;
        public Dictionary<SymbolKind, int> resistanceCounts = new Dictionary<SymbolKind, int>();
        public int basePurifyCount;
        public int lineCount;
        public int clusterCount;
        public int cascadeLength;
        public float cascadeAdditionalPower;
        public int absorberResidualCount;
        public int proliferatorResidualCount;
        public float absorberResidualCost;
        public float proliferatorResidualCost;
        public float grossPower;
        public float netPower;
        public float cumulativePower;
        public bool wasAdditionalSpin;
        public bool isFirstAdditionalSpin;
        public float additionalSpinAnte;
        public float additionalSpinNetAfterAnte;
        public bool additionalSpinSucceeded;
        public bool additionalSpinLost;
        public SpinResolution resolution;
    }

    /// <summary>One floor's measurements and final threshold result.</summary>
    [Serializable]
    public sealed class SimFloorRecord
    {
        public int floor;
        public int floorIndex;
        public float requiredPower;
        public ResistanceContract selectedContract;
        public List<SimSpinRecord> spins = new List<SimSpinRecord>();
        public float totalWeight;
        public float finalPower;
        public PowerBand finalBand;
        public bool passed;
        public int additionalSpinChoices;
        public int additionalSpinDecisions;
        public int additionalSpinSuccesses;
        public int additionalSpinLosses;
        public int firstAdditionalSpinChoices;
        public int firstAdditionalSpinSuccesses;
        public int firstAdditionalSpinLosses;
        public float totalAnte;
        public int thresholdCrossings;
        public string failureReason;
        public float allowedWeight;
        public bool overloaded;
        public bool accidentOccurred;
        public float accidentChance;
        public float accidentLoss;
        public string candidatesOffered;
        public string passengerBoarded;
        public bool success;
        public int retries;
        public float surplus;
        public string overchargeChoice;
        public int floorsClimbed;
        public List<SimTurnRecord> turns = new List<SimTurnRecord>();
    }

    /// <summary>One complete ten-floor run.</summary>
    [Serializable]
    public sealed class SimRunRecord
    {
        public int runIndex;
        public int seed;
        public string policyName;
        public bool succeeded;
        public int failedFloor;
        public List<SimFloorRecord> floors = new List<SimFloorRecord>();
        public float finalPower;
        public string outcome;
        public string failureReason;
        public int highestFloor;
        public float finalMoney;
        public int totalAccidents;
        public int totalRetries;
    }

    /// <summary>Compatibility row for the retired timing-based editor CSV.</summary>
    [Serializable]
    public sealed class SimTurnRecord
    {
        public int turnIndex;
        public string ball0, ball1, ball2;
        public string grade0, grade1, grade2;
        public bool perfectStop;
        public float accuracyMultiplier;
        public string combination;
        public float powerBeforeEffects;
        public float powerAfterEffects;
        public float moneyDelta;
        public string effectLog;
    }

    /// <summary>Compatibility container for the retired editor batch runner.</summary>
    [Serializable]
    public sealed class SimBatchResult
    {
        public string generatedAtUtc;
        public int runCount;
        public int successCount;
        public float averageHighestFloor;
        public float averageMoney;
        public float averageAccidents;
        public List<SimRunRecord> runs = new List<SimRunRecord>();
    }

    /// <summary>Aggregate policy result. Raw runs are retained for drill-down reports.</summary>
    [Serializable]
    public sealed class SimPolicyReport
    {
        public string policyName;
        public int runCount;
        public int successfulRuns;
        public List<SimRunRecord> runs = new List<SimRunRecord>();
        public int[] floorPasses = new int[10];
        public int[] floorAttempts = new int[10];
        public float[] averageFinalPowerByFloor = new float[10];
        public float[] averageRequiredPowerByFloor = new float[10];
        public float[] basePurificationsByFloor = new float[10];
        public float[] linesByFloor = new float[10];
        public float[] clustersByFloor = new float[10];
        public float[] cascadesByFloor = new float[10];
        public float[] cascadeAdditionalPowerByFloor = new float[10];
        public float[] absorberResidualsByFloor = new float[10];
        public float[] proliferatorResidualsByFloor = new float[10];
        public float[] residualCostsByFloor = new float[10];
        public float[] additionalSpinChoicesByFloor = new float[10];
        public float[] additionalSpinDecisionsByFloor = new float[10];
        public float[] additionalSpinSuccessesByFloor = new float[10];
        public float[] additionalSpinLossesByFloor = new float[10];
        public float[] averageAnteByFloor = new float[10];
        public Dictionary<int, int> cascadeLengthDistribution = new Dictionary<int, int>();
        public Dictionary<PowerBand, int> powerBandDistribution = new Dictionary<PowerBand, int>();
        public Dictionary<int, int> thresholdCrossingDistribution = new Dictionary<int, int>();
        public Dictionary<SymbolKind, int> totalResistanceCounts = new Dictionary<SymbolKind, int>();
        public Dictionary<SymbolKind, float> averageSymbolWeights = new Dictionary<SymbolKind, float>();
        public int symbolWeightSamples;
        public float totalWeight;
        public float averageFinalPower;
        public float averageNormalSoulBasePower;
        public int contractRuns;
        public float contractAverageFinalPower;
        public float noneAverageFinalPower;
        public int firstAdditionalSpinCount;
        public int firstAdditionalSpinLossCount;
        public float firstAdditionalSpinLossRate;
    }

    [Serializable]
    public sealed class SimReportData
    {
        public int runCount;
        public int seed;
        public List<SimPolicyReport> policies = new List<SimPolicyReport>();
        public List<string> balanceWarnings = new List<string>();
    }
}
