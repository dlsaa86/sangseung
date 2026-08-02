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

        /// <summary>
        /// 남은 스핀 **운행 효율 정산**으로 얻은 돈 (`T-05` 2026-08-02 결정).
        ///
        /// 과수확을 한 번이라도 선택한 층에서는 **항상 0** 이다 — 그 결정이
        /// 「그 층의 정산 권리 소멸」이기 때문이고, 그것이 두 선택을 겨루게 만드는 축이다.
        /// </summary>
        public float SettlementMoney { get; }

        /// <summary>정산에 쓰인 남은 스핀 수. 0 이면 정산이 없었다.</summary>
        public int SettledSpins { get; }

        /// <summary>정산이 층당 상한에 걸렸는가. UI 가 「상한 도달」을 적을 근거다.</summary>
        public bool SettlementCapped { get; }

        internal FloorResult(AscendResult ascent, float totalAnte = 0f,
            int extraSpinsTaken = 0, float extraSpinNetPower = 0f, float netProfit = 0f,
            float settlementMoney = 0f, int settledSpins = 0, bool settlementCapped = false)
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
            SettlementMoney = settlementMoney;
            SettledSpins = settledSpins;
            SettlementCapped = settlementCapped;
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
