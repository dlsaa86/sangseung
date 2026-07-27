using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Evaluates a list of 3 BallDefinitions into a CombinationResult.
    /// Priority order (highest → lowest):
    ///   ContainsLegendary → ThreeOfAKind → SpecificOrder → CommonAdvancedRare
    ///   → ThreeDifferentCommon → ThreeSameGrade → None.
    /// All balance values are sourced from CombinationConfig (ScriptableObject).
    /// </summary>
    public class CombinationResolver : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private CombinationConfig _config;

        // ── Result type (public so RunController and UI can read it) ──

        /// <summary>Result of a single 3-ball combination evaluation.</summary>
        [Serializable]
        public struct CombinationResult
        {
            public CombinationType Type;
            public float           Power;
            public string          Summary;
        }

        // ── Public API ──

        /// <summary>
        /// Resolves a list of 3 balls into a CombinationResult, applying the
        /// highest-priority matching combination rule.
        /// Returns a zero-power None result if balls is null, has fewer than 3 entries,
        /// or _config is not assigned.
        /// </summary>
        public CombinationResult Resolve(IReadOnlyList<BallDefinition> balls)
        {
            if (_config == null)
            {
                Debug.LogError("[상승] CombinationResolver.Resolve: _config is not assigned!");
                return new CombinationResult { Type = CombinationType.None, Power = 0f, Summary = "Config missing" };
            }

            if (balls == null || balls.Count < 3)
            {
                Debug.LogWarning("[상승] CombinationResolver.Resolve: balls null or fewer than 3 — returning None.");
                return new CombinationResult { Type = CombinationType.None, Power = 0f, Summary = "No balls" };
            }

            BallDefinition b0 = balls[0];
            BallDefinition b1 = balls[1];
            BallDefinition b2 = balls[2];

            float baseOutputSum = (b0 != null ? b0.baseOutput : 0f)
                                + (b1 != null ? b1.baseOutput : 0f)
                                + (b2 != null ? b2.baseOutput : 0f);

            CombinationType type = DetermineType(b0, b1, b2);

            float power = (type == CombinationType.None)
                ? (baseOutputSum + _config.minimumBaseOutput) * 1.0f
                : (baseOutputSum + _config.GetBaseScore(type)) * _config.GetMultiplier(type);

            string summary = BuildSummary(b0, b1, b2, type, power);
            return new CombinationResult { Type = type, Power = power, Summary = summary };
        }

        // ── Priority determination (static; only pure ball data used) ──

        private static CombinationType DetermineType(BallDefinition b0, BallDefinition b1, BallDefinition b2)
        {
            // 1. ContainsLegendary: any Legendary ball present
            if (b0.grade == BallGrade.Legendary || b1.grade == BallGrade.Legendary || b2.grade == BallGrade.Legendary)
                return CombinationType.ContainsLegendary;

            // 2. ThreeOfAKind: identical id for all three
            if (b0.id == b1.id && b1.id == b2.id)
                return CombinationType.ThreeOfAKind;

            // 3. SpecificOrder: strict grade ascending in tube order (Common < Advanced < Rare)
            if ((int)b0.grade < (int)b1.grade && (int)b1.grade < (int)b2.grade)
                return CombinationType.SpecificOrder;

            // 4. CommonAdvancedRare: exactly one Common, one Advanced, one Rare (any arrangement)
            bool hasCommon   = (b0.grade == BallGrade.Common   || b1.grade == BallGrade.Common   || b2.grade == BallGrade.Common);
            bool hasAdvanced = (b0.grade == BallGrade.Advanced || b1.grade == BallGrade.Advanced || b2.grade == BallGrade.Advanced);
            bool hasRare     = (b0.grade == BallGrade.Rare     || b1.grade == BallGrade.Rare     || b2.grade == BallGrade.Rare);
            if (hasCommon && hasAdvanced && hasRare)
                return CombinationType.CommonAdvancedRare;

            // 5. ThreeDifferentCommon: all Common with distinct ids
            //    Checked BEFORE ThreeSameGrade — three distinct Commons are a more
            //    specific pattern than the generic "all same grade" rule (섹션 7).
            if (b0.grade == BallGrade.Common && b1.grade == BallGrade.Common && b2.grade == BallGrade.Common
                && b0.id != b1.id && b1.id != b2.id && b0.id != b2.id)
                return CombinationType.ThreeDifferentCommon;

            // 6. ThreeSameGrade: all three share the same grade
            if (b0.grade == b1.grade && b1.grade == b2.grade)
                return CombinationType.ThreeSameGrade;

            // 7. None
            return CombinationType.None;
        }

        // ── Formatting helpers ──

        private static string GradeAbbr(BallGrade grade)
        {
            switch (grade)
            {
                case BallGrade.Common:    return "C";
                case BallGrade.Advanced:  return "A";
                case BallGrade.Rare:      return "R";
                case BallGrade.Legendary: return "L";
                default:                  return "?";
            }
        }

        private static string BuildSummary(
            BallDefinition b0, BallDefinition b1, BallDefinition b2,
            CombinationType type, float power)
        {
            string s0 = b0 != null ? $"{b0.id}({GradeAbbr(b0.grade)})" : "(null)";
            string s1 = b1 != null ? $"{b1.id}({GradeAbbr(b1.grade)})" : "(null)";
            string s2 = b2 != null ? $"{b2.id}({GradeAbbr(b2.grade)})" : "(null)";
            return $"{s0} {s1} {s2} -> {type} (+{power:F1})";
        }
    }
}
