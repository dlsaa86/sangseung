using System;

namespace Ascend.Prototype
{
    /// <summary>One human-readable record from an effect pipeline run.</summary>
    [Serializable]
    public struct EffectLogEntry
    {
        public int Order;
        public int Depth;
        public string EffectId;
        public string EffectName;
        public EffectType Type;
        public float PowerBefore;
        public float PowerAfter;
        public bool Applied;
        public string Note;

        /// <summary>Formats the entry for a compact UI log line.</summary>
        public string ToDisplayString()
        {
            string state = Applied ? "적용" : "스킵";
            return $"[{Order}] D{Depth} {EffectName} ({Type}) {state}: {PowerBefore:F2} -> {PowerAfter:F2} {Note}";
        }
    }
}
