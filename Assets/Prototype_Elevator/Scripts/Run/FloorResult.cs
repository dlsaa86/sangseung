using System;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>Immutable summary of a floor's final decision.</summary>
    public sealed class FloorResult
    {
        public PowerBand Band { get; }
        public PowerBand ReachedBand => Band;
        public float FinalPower { get; }
        public float RequiredPower { get; }
        public float ExcessPower => Math.Max(0f, FinalPower - RequiredPower);
        public int FloorsAscended { get; }
        public int AscentFloors => FloorsAscended;
        public string FailureReason { get; }
        public bool Succeeded => Band.Ascends();
        public bool CanContinueRun => Succeeded && !RunEnded;
        public bool DeviceDamaged { get; }
        public bool RequiresJettison { get; }
        public bool RunEnded { get; }
        public AscendResult Ascent { get; }
        public float TotalAnte { get; }
        public float TotalStakedPower => TotalAnte;
        public int ExtraSpinsTaken { get; }
        public float ExtraSpinNetPower { get; }
        public float NetProfit { get; }
        public float PushYourLuckNetProfit => NetProfit;

        internal FloorResult(AscendResult ascent, float totalAnte = 0f,
            int extraSpinsTaken = 0, float extraSpinNetPower = 0f, float netProfit = 0f)
        {
            if (ascent == null) throw new ArgumentNullException(nameof(ascent));
            Ascent = ascent;
            Band = ascent.Band;
            FinalPower = ascent.FinalPower;
            RequiredPower = ascent.RequiredPower;
            FloorsAscended = ascent.FloorsAscended;
            FailureReason = ascent.FailureReason;
            DeviceDamaged = ascent.DeviceDamaged;
            RequiresJettison = ascent.RequiresJettison;
            RunEnded = ascent.RunEnded;
            TotalAnte = totalAnte;
            ExtraSpinsTaken = extraSpinsTaken;
            ExtraSpinNetPower = extraSpinNetPower;
            NetProfit = netProfit;
        }

        public SurplusAllocation AllocateSurplus(SurplusUse use,
            int requestedExtraFloors = 0, float moneyPerPower = 1f)
        {
            return Ascent.AllocateSurplus(use, requestedExtraFloors, moneyPerPower);
        }

        public override string ToString()
        {
            return $"{Band} / power {FinalPower:0.##}/{RequiredPower:0.##} / " +
                   $"+{FloorsAscended} floor(s)" +
                   (string.IsNullOrEmpty(FailureReason) ? string.Empty : $" / {FailureReason}");
        }
    }
}
