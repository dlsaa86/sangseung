namespace Ascend.Prototype
{
    /// <summary>
    /// Minimal interface stub for ball/combination effects.
    /// Full resolution logic is planned for T-02+.
    /// Implement this interface on concrete effect classes when the effect system is built.
    /// </summary>
    public interface IEffect
    {
        /// <summary>The category of this effect.</summary>
        EffectType Type { get; }
    }
}
