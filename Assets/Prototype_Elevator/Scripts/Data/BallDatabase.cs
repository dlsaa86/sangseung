using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Master list of all BallDefinitions in the game.
    /// Provides probability-sum validation; no gameplay logic.
    /// </summary>
    [CreateAssetMenu(fileName = "BallDatabase", menuName = "Ascend/BallDatabase", order = 2)]
    public class BallDatabase : ScriptableObject
    {
        [Tooltip("All registered ball definitions. Probabilities should sum to 100.")]
        public List<BallDefinition> balls = new List<BallDefinition>();

        /// <summary>
        /// Validates that the spawn probabilities of all registered balls sum to approximately 100.
        /// Logs the result and returns true when the sum is within 0.01 of 100.
        /// </summary>
        public bool ValidateProbabilities()
        {
            float sum = 0f;
            foreach (var ball in balls)
            {
                if (ball != null)
                    sum += ball.spawnProbability;
            }

            bool valid = Mathf.Abs(sum - 100f) < 0.01f;
            if (valid)
                Debug.Log($"[Ascend] BallDatabase probability sum: {sum:F2} ✓ (valid, expected ~100)");
            else
                Debug.LogWarning($"[Ascend] BallDatabase probability sum: {sum:F2} ✗ (expected ~100)");

            return valid;
        }
    }
}
