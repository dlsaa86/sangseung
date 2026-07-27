namespace Ascend.Prototype
{
    /// <summary>Requests another complete pipeline pass.</summary>
    public sealed class RepeatEffectHandler : IEffectHandler
    {
        public EffectType Type => EffectType.Repeat;

        /// <summary>Accumulates the requested repeat count for the pipeline.</summary>
        public bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note)
        {
            int count = def.repeatCount < 1 ? 1 : def.repeatCount;
            ctx.RepeatRequested += count;
            note = $"Repeat requested x{count}";
            return true;
        }
    }
}
