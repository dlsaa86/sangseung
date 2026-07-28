using System.Collections.Generic;

namespace Ascend.Prototype
{
    /// <summary>Mutable state passed through one data-driven generation pipeline.</summary>
    public class GenerationContext
    {
        public List<BallDefinition> Balls = new List<BallDefinition>();
        public CombinationType Combination;
        /// <summary>Always derived from Balls so ball-list mutations can never desync the sum.</summary>
        public float BaseOutputSum
        {
            get
            {
                float sum = 0f;
                if (Balls != null)
                    for (int i = 0; i < Balls.Count; i++)
                        if (Balls[i] != null) sum += Balls[i].baseOutput;
                return sum;
            }
        }
        public float CombinationBaseScore;
        public float CombinationMultiplier = 1f;

        /// <summary>
        /// How well the player timed the three stops (see RouletteController.AccuracyMultiplier).
        /// Applied to the final power so precision is worth something.
        /// </summary>
        public float AccuracyMultiplier = 1f;

        public float FlatBonus;
        public float MultiplierBonus = 1f;
        public float MoneyDelta;

        public bool IsOverloaded;
        public bool PerfectStop;
        public int TurnIndex;
        public int FloorIndex;

        public float FinalPower;
        public float RepeatBonusPower;
        public readonly List<EffectLogEntry> Log = new List<EffectLogEntry>();
        public readonly List<EffectDefinition> PendingProbabilityModifiers = new List<EffectDefinition>();
        public int RepeatRequested;

        /// <summary>Computes power from the current mutable context values.</summary>
        public float ComputeCurrentPower()
        {
            return (BaseOutputSum + CombinationBaseScore + FlatBonus)
                   * CombinationMultiplier * MultiplierBonus * AccuracyMultiplier;
        }

        /// <summary>Creates an independent context for one additional repeat power pass.</summary>
        public GenerationContext CloneForRepeat()
        {
            var clone = new GenerationContext
            {
                Balls = Balls != null ? new List<BallDefinition>(Balls) : new List<BallDefinition>(),
                Combination = Combination,
                CombinationBaseScore = CombinationBaseScore,
                CombinationMultiplier = CombinationMultiplier,
                AccuracyMultiplier = AccuracyMultiplier,
                IsOverloaded = IsOverloaded,
                PerfectStop = PerfectStop,
                TurnIndex = TurnIndex,
                FloorIndex = FloorIndex,
                FlatBonus = 0f,
                MultiplierBonus = 1f,
                MoneyDelta = 0f,
                RepeatBonusPower = 0f,
                RepeatRequested = 0,
                FinalPower = 0f
            };

            return clone;
        }
    }
}
