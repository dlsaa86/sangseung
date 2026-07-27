using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// ScriptableObject holding per-combination balance values (baseScore and multiplier).
    /// All values are Inspector-adjustable — no hardcoded balance in gameplay scripts.
    /// Power formula: (ball baseOutput sum + baseScore) × multiplier.
    /// </summary>
    [CreateAssetMenu(fileName = "CombinationConfig", menuName = "Ascend/CombinationConfig", order = 3)]
    public class CombinationConfig : ScriptableObject
    {
        [Header("ThreeOfAKind")]
        [Tooltip("Bonus power added to ball baseOutput sum.")]
        public float threeOfAKindBaseScore = 20f;
        [Tooltip("Multiplier applied to (baseOutput sum + baseScore).")]
        public float threeOfAKindMultiplier = 2.0f;

        [Header("ThreeSameGrade")]
        public float threeSameGradeBaseScore = 12f;
        public float threeSameGradeMultiplier = 1.6f;

        [Header("ThreeDifferentCommon")]
        public float threeDifferentCommonBaseScore = 6f;
        public float threeDifferentCommonMultiplier = 1.2f;

        [Header("CommonAdvancedRare")]
        public float commonAdvancedRareBaseScore = 10f;
        public float commonAdvancedRareMultiplier = 1.5f;

        [Header("SpecificOrder")]
        public float specificOrderBaseScore = 8f;
        public float specificOrderMultiplier = 1.4f;

        [Header("ContainsLegendary")]
        public float containsLegendaryBaseScore = 30f;
        public float containsLegendaryMultiplier = 2.5f;

        [Header("None (Minimum Base Output)")]
        [Tooltip("Added to ball baseOutput sum when no combo matches. Multiplier is fixed at 1.0.")]
        public float minimumBaseOutput = 5f;

        /// <summary>Returns the baseScore for the given combination type.</summary>
        public float GetBaseScore(CombinationType type)
        {
            switch (type)
            {
                case CombinationType.ThreeOfAKind:         return threeOfAKindBaseScore;
                case CombinationType.ThreeSameGrade:       return threeSameGradeBaseScore;
                case CombinationType.ThreeDifferentCommon: return threeDifferentCommonBaseScore;
                case CombinationType.CommonAdvancedRare:   return commonAdvancedRareBaseScore;
                case CombinationType.SpecificOrder:        return specificOrderBaseScore;
                case CombinationType.ContainsLegendary:    return containsLegendaryBaseScore;
                case CombinationType.None:
                default:                                    return minimumBaseOutput;
            }
        }

        /// <summary>Returns the power multiplier for the given combination type.</summary>
        public float GetMultiplier(CombinationType type)
        {
            switch (type)
            {
                case CombinationType.ThreeOfAKind:         return threeOfAKindMultiplier;
                case CombinationType.ThreeSameGrade:       return threeSameGradeMultiplier;
                case CombinationType.ThreeDifferentCommon: return threeDifferentCommonMultiplier;
                case CombinationType.CommonAdvancedRare:   return commonAdvancedRareMultiplier;
                case CombinationType.SpecificOrder:        return specificOrderMultiplier;
                case CombinationType.ContainsLegendary:    return containsLegendaryMultiplier;
                case CombinationType.None:
                default:                                    return 1.0f;
            }
        }
    }
}
