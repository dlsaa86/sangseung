using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Stub for resolving IEffect instances against a generation result.
    /// T-00: all methods are no-ops.
    /// Full context-driven resolution (probability weights, add/multiply chains, etc.)
    /// is planned for T-02+.
    /// </summary>
    public class EffectResolver : MonoBehaviour
    {
        /// <summary>
        /// Applies all active effects to the current generation context.
        /// T-00: no-op — returns input unchanged.
        /// </summary>
        public void ResolveEffects()
        {
            // T-01 no-op stub — call site preserved for T-03 extension.
            // T-03 TODO: accept a GenerationContext, iterate active IEffect list, apply each in order.
        }
    }
}
