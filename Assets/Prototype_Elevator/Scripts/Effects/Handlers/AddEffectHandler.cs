namespace Ascend.Prototype
{
    /// <summary>Adds a flat amount to the current generation power.</summary>
    public sealed class AddEffectHandler : IEffectHandler
    {
        public EffectType Type => EffectType.Add;

        /// <summary>Accumulates the definition value as a flat bonus.</summary>
        public bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note)
        {
            ctx.FlatBonus += def.value;
            note = $"FlatBonus +{def.value:F2}";
            return true;
        }
    }
}
