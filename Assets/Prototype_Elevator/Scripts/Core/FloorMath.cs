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

        public static OverchargeOption BuildMoneyOption(PrototypeConfig config, float surplus)
        {
            if (config == null || config.powerPerExtraFloor <= 0f)
            {
                return new OverchargeOption
                {
                    Mode = OverchargeMode.Money,
                    Label = "돈 +0  (초과 전력 0 전량 변환)"
                };
            }

            return new OverchargeOption
            {
                Mode = OverchargeMode.Money,
                SurplusUsed = surplus,
                FloorsGained = 0,
                MoneyGained = surplus * config.powerToMoneyRatio,
                PowerCarried = 0f,
                Label = $"돈 +{surplus * config.powerToMoneyRatio:F0}  (초과 전력 {surplus:F0} 전량 변환)"
            };
        }

        public static OverchargeOption BuildAscendOption(PrototypeConfig config, float surplus)
        {
            if (config == null || config.powerPerExtraFloor <= 0f)
            {
                return new OverchargeOption
                {
                    Mode = OverchargeMode.Ascend,
                    Label = "추가 상승 +0층  (전력 0 소비, 0 이월)"
                };
            }

            int floorsGained = Mathf.Clamp(
                Mathf.FloorToInt(surplus / config.powerPerExtraFloor),
                0,
                Mathf.Max(0, config.maxExtraFloorsPerAllocation));
            float surplusUsed = floorsGained * config.powerPerExtraFloor;
            float powerCarried = surplus - surplusUsed;

            return new OverchargeOption
            {
                Mode = OverchargeMode.Ascend,
                SurplusUsed = surplusUsed,
                FloorsGained = floorsGained,
                MoneyGained = 0f,
                PowerCarried = powerCarried,
                Label = $"추가 상승 +{floorsGained}층  (전력 {surplusUsed:F0} 소비, {powerCarried:F0} 이월)"
            };
        }
    }
}
