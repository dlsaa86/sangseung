namespace Ascend.Prototype
{
    /// <summary>Applies one effect category to a generation context.</summary>
    public interface IEffectHandler
    {
        /// <summary>The effect category handled by this instance.</summary>
        EffectType Type { get; }

        /// <summary>Applies the definition and returns true when it changed or recorded context.</summary>
        bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note);
    }
}
