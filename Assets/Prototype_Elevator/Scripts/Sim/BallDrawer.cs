using System;
using System.Collections.Generic;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype
{
    /// <summary>Small headless weighted sampler retained for simulation-side experiments.</summary>
    public sealed class BallDrawer
    {
        private readonly IReadOnlyDictionary<SymbolKind, float> _weights;
        private readonly Random _random;

        public BallDrawer(IReadOnlyDictionary<SymbolKind, float> weights, Random random)
        {
            _weights = weights;
            _random = random ?? new Random(0);
        }

        public SymbolKind Draw()
        {
            if (_weights == null || _weights.Count == 0) return SymbolKind.Empty;
            float total = 0f;
            foreach (KeyValuePair<SymbolKind, float> pair in _weights)
                total += (float)Math.Max(0d, pair.Value);
            if (total <= 0f) return SymbolKind.Empty;

            double roll = _random.NextDouble() * total;
            foreach (KeyValuePair<SymbolKind, float> pair in _weights)
            {
                float weight = (float)Math.Max(0d, pair.Value);
                if (roll < weight) return pair.Key;
                roll -= weight;
            }
            return SymbolKind.Empty;
        }

        public List<SymbolKind> DrawMany(int count)
        {
            int safeCount = Math.Max(0, count);
            var result = new List<SymbolKind>(safeCount);
            for (int i = 0; i < safeCount; i++) result.Add(Draw());
            return result;
        }

        /// <summary>Retired editor self-test compatibility; read the legacy asset shape by reflection.</summary>
        public static float SumProbabilities<T>(T database)
        {
            if (database == null) return 0f;
            var ballsField = database.GetType().GetField("balls");
            var balls = ballsField != null ? ballsField.GetValue(database) as System.Collections.IEnumerable : null;
            if (balls == null) return 0f;
            float total = 0f;
            foreach (object ball in balls)
            {
                if (ball == null) continue;
                var probability = ball.GetType().GetField("spawnProbability");
                if (probability != null) total += Convert.ToSingle(probability.GetValue(ball));
            }
            return total;
        }
    }
}
