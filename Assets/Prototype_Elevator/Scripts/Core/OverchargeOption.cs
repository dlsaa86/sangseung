using System;

namespace Ascend.Prototype
{
    public enum OverchargeMode
    {
        Money,
        Ascend
    }

    [Serializable]
    public struct OverchargeOption
    {
        public OverchargeMode Mode;
        public float SurplusUsed;
        public int FloorsGained;
        public float MoneyGained;
        public float PowerCarried;
        public string Label;
    }
}
