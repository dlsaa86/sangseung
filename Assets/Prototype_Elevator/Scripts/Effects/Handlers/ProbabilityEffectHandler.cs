namespace Ascend.Prototype
{
    /// <summary>Queues a probability modifier for a roulette consumer.</summary>
    public sealed class ProbabilityEffectHandler : IEffectHandler
    {
        public EffectType Type => EffectType.Probability;

        /// <summary>Records the modifier without changing generation power directly.</summary>
        public bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note)
        {
            ctx.PendingProbabilityModifiers.Add(def);
            note = $"Queued probability modifier x{def.value:F2}";
            return true;
        }
    }
}
