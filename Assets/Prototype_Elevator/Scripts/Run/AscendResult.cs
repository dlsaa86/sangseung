using System;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>How surplus power is spent after the base ascent is secured.</summary>
    public enum SurplusUse
    {
        Ascend,
        Money,
        Bank,
    }

    /// <summary>A concrete choice for surplus-power allocation.</summary>
    public struct SurplusAllocation
    {
        public SurplusUse Use;
        public float PowerSpent;
        public float PowerBanked;
        public float MoneyGained;
        public int AdditionalFloors;

        public bool IsPartial => PowerBanked > 0f && PowerSpent > 0f;
    }

    /// <summary>
    /// Pure calculation of the ascent and the stop point for a resolved floor.
    /// The caller may spend only part of the excess, preserving the "exact stop"
    /// decision instead of making every high roll an automatic multi-floor ascent.
    /// </summary>
    public sealed class AscendResult
    {
        public const float DefaultPowerPerExtraFloor = 60f;
        public const int DefaultMaxExtraFloors = 3;

        public PowerBand Band { get; }
        public PowerBand ReachedBand => Band;
        public float FinalPower { get; }
        public float RequiredPower { get; }
        public float ExcessPower => Math.Max(0f, FinalPower - RequiredPower);
        public int BaseFloors { get; }
        public int AdditionalFloors { get; }
        public int FloorsAscended => BaseFloors + AdditionalFloors;
        public bool DeviceDamaged { get; }
        public bool RequiresJettison { get; }
        public bool RunEnded { get; }
        public string FailureReason { get; }
        public float PowerPerExtraFloor { get; }
        public int MaxExtraFloors { get; }
        public bool CanAscend => Band.Ascends();

        private AscendResult(PowerBand band, float finalPower, float requiredPower,
            PowerThresholds thresholds, float powerPerExtraFloor, int maxExtraFloors)
        {
            Band = band;
            FinalPower = finalPower;
            RequiredPower = requiredPower;
            PowerPerExtraFloor = powerPerExtraFloor > 0f
                ? powerPerExtraFloor : DefaultPowerPerExtraFloor;
            MaxExtraFloors = Math.Max(0, maxExtraFloors);
            BaseFloors = band.Ascends() ? 1 : 0;
            DeviceDamaged = band == PowerBand.Damaged;
            RequiresJettison = band == PowerBand.Jettison;
            RunEnded = band == PowerBand.Crash;
            FailureReason = RunEnded ? "Crash" :
                (RequiresJettison ? "Jettison required" : string.Empty);

            // The named high bands promise an additional-ascent opportunity, but
            // the player is still allowed to stop short and bank/convert the rest.
            AdditionalFloors = 0;
            if (band == PowerBand.MultiFloor || band == PowerBand.Overharvest ||
                band == PowerBand.Runaway)
            {
                AdditionalFloors = Math.Min(MaxExtraFloors,
                    (int)Math.Floor(ExcessPower / PowerPerExtraFloor));
            }
        }

        public static AscendResult Calculate(float power, float required,
            PowerThresholds thresholds)
        {
            return Calculate(power, required, thresholds,
                DefaultPowerPerExtraFloor, DefaultMaxExtraFloors);
        }

        public static AscendResult Calculate(float power, float required,
            PowerThresholds thresholds, float powerPerExtraFloor, int maxExtraFloors)
        {
            PowerBand band = thresholds.BandFor(power, required);
            return new AscendResult(band, power, required, thresholds,
                powerPerExtraFloor, maxExtraFloors);
        }

        public static AscendResult For(PowerBand band, float power, float required,
            PowerThresholds thresholds)
        {
            // Useful to callers that already have a threshold result, while keeping
            // one implementation of the ascent rules.
            return new AscendResult(band, power, required, thresholds,
                DefaultPowerPerExtraFloor, DefaultMaxExtraFloors);
        }

        /// <summary>
        /// Allocates a requested amount of surplus. Ascend consumes one 60-power
        /// unit per extra floor; any unspent power remains bankable.
        /// </summary>
        public SurplusAllocation AllocateSurplus(SurplusUse use, int requestedExtraFloors = 0,
            float moneyPerPower = 1f)
        {
            float available = ExcessPower;
            if (use == SurplusUse.Bank)
            {
                return new SurplusAllocation
                {
                    Use = use,
                    PowerBanked = available,
                };
            }

            if (use == SurplusUse.Money)
            {
                return new SurplusAllocation
                {
                    Use = use,
                    MoneyGained = available * Math.Max(0f, moneyPerPower),
                    PowerSpent = available,
                };
            }

            int floors = requestedExtraFloors <= 0 ? AdditionalFloors : requestedExtraFloors;
            floors = Math.Max(0, Math.Min(floors, MaxExtraFloors));
            floors = Math.Min(floors, (int)Math.Floor(available / PowerPerExtraFloor));
            float spent = floors * PowerPerExtraFloor;
            return new SurplusAllocation
            {
                Use = use,
                PowerSpent = spent,
                PowerBanked = available - spent,
                AdditionalFloors = floors,
            };
        }

        public SurplusAllocation Allocate(SurplusUse use, int requestedExtraFloors = 0,
            float moneyPerPower = 1f)
        {
            return AllocateSurplus(use, requestedExtraFloors, moneyPerPower);
        }
    }
}
