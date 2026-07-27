namespace Ascend.Prototype
{
    /// <summary>Random source used by effects so runs can be deterministic.</summary>
    public interface IEffectRandom
    {
        /// <summary>Returns a value in the half-open interval [0, 1).</summary>
        double NextDouble();
    }

    /// <summary>System.Random adapter for runtime effect resolution.</summary>
    public sealed class SystemEffectRandom : IEffectRandom
    {
        private readonly System.Random _random;

        /// <summary>Creates a seeded random source.</summary>
        public SystemEffectRandom(int seed)
        {
            _random = new System.Random(seed);
        }

        /// <summary>Returns the next deterministic random value.</summary>
        public double NextDouble()
        {
            return _random.NextDouble();
        }
    }

    /// <summary>Fixed random source intended for deterministic tests.</summary>
    public sealed class FixedEffectRandom : IEffectRandom
    {
        private readonly double _value;

        /// <summary>Creates a source that always returns the supplied value.</summary>
        public FixedEffectRandom(double value)
        {
            _value = value;
        }

        /// <summary>Returns the configured fixed value.</summary>
        public double NextDouble()
        {
            return _value;
        }
    }
}
