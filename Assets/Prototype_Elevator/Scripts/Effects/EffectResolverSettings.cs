using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Safety and diagnostics settings for the effect pipeline.</summary>
    [CreateAssetMenu(fileName = "EffectResolverSettings", menuName = "Ascend/EffectResolverSettings")]
    public class EffectResolverSettings : ScriptableObject
    {
        public int maxRecursionDepth = 3;
        public int maxTotalActivations = 64;
        public bool verboseLogging = true;
    }
}
