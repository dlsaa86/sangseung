using System.Collections.Generic;

namespace Ascend.Prototype
{
    /// <summary>
    /// Weighted ball draw shared by the simulator.
    /// Mirrors RouletteController.DrawWeighted exactly so simulated streams match play-mode streams
    /// for the same seed — if the two ever diverge, simulation results stop describing the game.
    /// </summary>
    public class BallDrawer
    {
        private readonly BallDatabase _database;
        private readonly System.Random _rng;

        public BallDrawer(BallDatabase database, System.Random rng)
        {
            _database = database;
            _rng = rng ?? new System.Random(0);
        }

        /// <summary>Draws one ball, or null when the database is empty or has no positive weights.</summary>
        public BallDefinition Draw()
        {
            if (_database == null || _database.balls == null || _database.balls.Count == 0)
                return null;

            float totalWeight = 0f;
            foreach (BallDefinition ball in _database.balls)
                if (ball != null) totalWeight += ball.spawnProbability;

            if (totalWeight <= 0f) return null;

            double roll = _rng.NextDouble() * totalWeight;
            float cumulative = 0f;
            BallDefinition selected = _database.balls[_database.balls.Count - 1];

            foreach (BallDefinition ball in _database.balls)
            {
                if (ball == null) continue;
                cumulative += ball.spawnProbability;
                if (roll < cumulative)
                {
                    selected = ball;
                    break;
                }
            }
            return selected;
        }

        /// <summary>Draws <paramref name="count"/> balls in sequence.</summary>
        public List<BallDefinition> DrawMany(int count)
        {
            var list = new List<BallDefinition>(count);
            for (int i = 0; i < count; i++) list.Add(Draw());
            return list;
        }

        /// <summary>Sums every registered ball's spawn probability. Used by the probability self-test.</summary>
        public static float SumProbabilities(BallDatabase database)
        {
            if (database == null || database.balls == null) return 0f;
            float sum = 0f;
            foreach (BallDefinition ball in database.balls)
                if (ball != null) sum += ball.spawnProbability;
            return sum;
        }
    }
}
