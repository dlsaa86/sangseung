using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Data definition for a single ball type.
    /// Contains identity, probability, and base stats only — no movement/physics logic (T-02+).
    /// </summary>
    [CreateAssetMenu(fileName = "BallDefinition", menuName = "Ascend/BallDefinition", order = 1)]
    public class BallDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique string identifier for this ball type (e.g. Ball_01).")]
        public string id;

        [Tooltip("Human-readable name shown in UI.")]
        public string displayName;

        [Tooltip("Rarity grade — affects drop probability and visual presentation.")]
        public BallGrade grade;

        [Header("Probability")]
        [Tooltip("Spawn weight (0–100%). All balls in the database should sum to 100.")]
        [Range(0f, 100f)]
        public float spawnProbability;

        [Header("Stats")]
        [Tooltip("Base power output when this ball is harvested (placeholder value for T-00).")]
        public float baseOutput;

        [Header("Debug")]
        [Tooltip("Color used to tint this ball in the Graybox prototype scene.")]
        public Color debugColor = Color.white;
    }
}
