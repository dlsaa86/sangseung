namespace Ascend.Prototype
{
    /// <summary>Multiplies the accumulated generation power.</summary>
    public sealed class MultiplyEffectHandler : IEffectHandler
    {
        public EffectType Type => EffectType.Multiply;

        /// <summary>Accumulates the definition value as a multiplier.</summary>
        public bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note)
        {
            ctx.MultiplierBonus *= def.value;
            note = $"MultiplierBonus x{def.value:F2}";
            return true;
        }
    }
}
