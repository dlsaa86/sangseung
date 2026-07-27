using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Pure balance formulas shared by the floor controller and simulations.</summary>
    public static class FloorMath
    {
        public static float ComputeRequiredPower(PrototypeConfig config, int floor, float totalWeight, bool isOverloaded)
        {
            if (config == null)
                return 0f;

            float required = config.baseRequiredPower
                           + floor * config.requiredPowerGrowthPerFloor
                           + totalWeight * config.weightToPowerFactor;
            return required * (isOverloaded ? config.overloadRequiredPowerMultiplier : 1f);
        }

        public static float ComputeAccidentChance(PrototypeConfig config, float weight, float allowedWeight)
        {
            if (config == null)
                return 0f;

            float overweight = Mathf.Max(0f, weight - allowedWeight);
            float chance = overweight * config.accidentChancePerOverweightUnit;
            return Mathf.Clamp01(Mathf.Min(config.maxAccidentChance, chance));
        }

        public static float ComputeAccidentPowerLoss(PrototypeConfig config, float currentPower)
        {
            return config == null ? 0f : currentPower * config.accidentPowerLossRatio;
        }

        public static int ComputeExtraFloors(PrototypeConfig config, float surplus)
        {
            if (config == null || config.baseRequiredPower <= 0f || surplus <= 0f)
                return 0;

            return Mathf.FloorToInt(surplus / config.baseRequiredPower);
        }

        public static float ComputeMoneyFromSurplus(PrototypeConfig config, float surplus)
        {
            if (config == null || surplus <= 0f)
                return 0f;

            return surplus * config.powerToMoneyRatio;
        }
    }
}
