using System;
using System.Collections.Generic;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype
{
    public enum SimPolicyKind
    {
        Cautious,
        Greedy,
        Balanced,
    }

    /// <summary>Deterministic virtual player policy used by the balance simulator.</summary>
    [Serializable]
    public sealed class SimPolicy
    {
        public SimPolicyKind kind;
        public string name;

        private SimPolicy(SimPolicyKind kind, string name)
        {
            this.kind = kind;
            this.name = name;
        }

        public static SimPolicy Cautious() => new SimPolicy(SimPolicyKind.Cautious, "Cautious");
        public static SimPolicy Greedy() => new SimPolicy(SimPolicyKind.Greedy, "Greedy");
        public static SimPolicy Balanced() => new SimPolicy(SimPolicyKind.Balanced, "균형형");

        // Legacy editor probes remain source-compatible while the new report uses the three
        // explicit policies above. They are mapped to the closest current behavior.
        public static SimPolicy Light() => new SimPolicy(SimPolicyKind.Cautious, "경량형");
        public static SimPolicy Overload() => new SimPolicy(SimPolicyKind.Greedy, "과적형");
        public static SimPolicy Masher() => new SimPolicy(SimPolicyKind.Cautious, "막누르기");
        public static SimPolicy Expert() => new SimPolicy(SimPolicyKind.Greedy, "숙련형");

        public static IReadOnlyList<SimPolicy> Defaults => new[]
        {
            new SimPolicy(SimPolicyKind.Cautious, "Cautious"),
            new SimPolicy(SimPolicyKind.Greedy, "Greedy"),
            new SimPolicy(SimPolicyKind.Balanced, "Balanced"),
        };

        public ResistanceContract ChooseContract(in FloorPlan floor, in ResidualState residual)
        {
            if (kind == SimPolicyKind.Cautious)
            {
                if (floor.ContractChoices == null || floor.ContractChoices.Length == 0)
                    return ResistanceContract.None;
                foreach (ResistanceContract choice in floor.ContractChoices)
                    if (choice.IsNone) return ResistanceContract.None;
                // A floor with no None option forces a contract; preserve cautious behavior
                // by taking the first offered choice rather than inventing an unavailable None.
                return floor.ContractChoices[0];
            }
            if (floor.ContractChoices == null || floor.ContractChoices.Length == 0)
                return ResistanceContract.None;

            ResistanceContract best = ResistanceContract.None;
            float bestScore = float.MinValue;
            foreach (ResistanceContract choice in floor.ContractChoices)
            {
                if (choice.IsNone || !Contains(floor.SymbolPool, choice.Target)) continue;
                if (kind == SimPolicyKind.Balanced && !residual.IsClean &&
                    residual.AbsorberCount + residual.ProliferatorCount > 1) continue;

                float score = choice.AppearanceMultiplier * choice.PurifyRewardMultiplier +
                              choice.PatternBonusAdd * 0.25f - choice.ResidualPenaltyMultiplier * 0.1f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = choice;
                }
            }
            return best;
        }

        public bool ShouldTakeAdditionalSpin(float power, float requiredPower, int spinsRemaining)
        {
            if (power < requiredPower) return true;
            if (spinsRemaining <= 0) return false;
            switch (kind)
            {
                case SimPolicyKind.Greedy:
                    return true;
                case SimPolicyKind.Balanced:
                    return power < requiredPower * 1.30f && spinsRemaining >= 2;
                default:
                    return false;
            }
        }

        private static bool Contains(SymbolKind[] pool, SymbolKind target)
        {
            if (pool == null) return false;
            foreach (SymbolKind kind in pool) if (kind == target) return true;
            return false;
        }
    }
}
