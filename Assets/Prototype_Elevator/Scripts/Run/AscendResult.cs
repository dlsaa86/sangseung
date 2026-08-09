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

        /// <summary>
        /// 건너뛴 층 하나당 나오는 돈 (2026-08-09 사용자 결정: 「층당 4골드로 하자 우선은」).
        ///
        /// ⚠ **이것은 전력을 돈으로 바꾼 것이 아니다.** 층에 쓴 전력
        /// (<see cref="PowerPerExtraFloor"/> × 층수)은 그대로 소모되고, 그와 **별개로**
        /// 건너뛴 층수에 비례한 보상이 나온다. 그래서 「추가 층 전력이 돈으로 중복
        /// 지급되지 않는다」는 기존 규칙이 그대로 성립한다 — 지급 근거가 전력이 아니라
        /// **층수**이기 때문이다. 둘을 헷갈리면 초과 전력을 넣을수록 돈이 선형으로
        /// 늘어나 「층을 건너뛴다」가 아니라 「전력을 환전한다」가 된다.
        /// </summary>
        public const float DefaultGoldPerSkippedFloor = 4f;

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
            // `Jettison`은 이제 실패가 아니다 — 대가를 치르고 오르는 구간이다.
            // 실패 사유에 남겨 두면 성공한 층의 기록에 "화물 포기"가 사유로 찍힌다.
            FailureReason = RunEnded ? "Crash" : string.Empty;

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
            float moneyPerPower = 1f, float goldPerSkippedFloor = DefaultGoldPerSkippedFloor)
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

            // 🔴 2026-08-09 — **층 상승과 돈은 더 이상 택일이 아니다.** 사용자 지시:
            // 「초과하는 전력을 넣는 경우 한 번에 더 많은 층을 올라갈 수 있게 된다.
            //   이때 스킵된 층수만큼 돈이 추가로 나온다」.
            // 직전 판본은 `SurplusUse` 셋 중 하나만 골라야 해서, 층을 오르면 돈이 0 이었다.
            return new SurplusAllocation
            {
                Use = use,
                PowerSpent = spent,
                PowerBanked = available - spent,
                AdditionalFloors = floors,
                MoneyGained = floors * Math.Max(0f, goldPerSkippedFloor),
            };
        }

        public SurplusAllocation Allocate(SurplusUse use, int requestedExtraFloors = 0,
            float moneyPerPower = 1f, float goldPerSkippedFloor = DefaultGoldPerSkippedFloor)
        {
            return AllocateSurplus(use, requestedExtraFloors, moneyPerPower, goldPerSkippedFloor);
        }
    }
}
