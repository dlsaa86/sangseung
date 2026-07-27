using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Evaluates harvested balls into a combination and its balance inputs.</summary>
    public class CombinationResolver : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private CombinationConfig _config;

        /// <summary>Result of a single 3-ball combination evaluation.</summary>
        [Serializable]
        public struct CombinationResult
        {
            public CombinationType Type;
            public float Power;
            public string Summary;
        }

        /// <summary>
        /// Resolves a list of 3 balls into a CombinationResult using the existing balance rules.
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
                Debug.LogWarning("[상승] CombinationResolver.Resolve: balls null or fewer than 3 - returning None.");
                return new CombinationResult { Type = CombinationType.None, Power = 0f, Summary = "No balls" };
            }

            GenerationContext context = BuildContext(balls, false, false, 0, 0);
            float power = context.ComputeCurrentPower();
            string summary = BuildSummary(balls[0], balls[1], balls[2], context.Combination, power);
            return new CombinationResult { Type = context.Combination, Power = power, Summary = summary };
        }

        /// <summary>Builds effect input values without finalising generation power.</summary>
        public GenerationContext BuildContext(
            IReadOnlyList<BallDefinition> balls,
            bool isOverloaded,
            bool perfectStop,
            int turnIndex,
            int floorIndex)
        {
            var context = new GenerationContext
            {
                Balls = balls != null ? new List<BallDefinition>(balls) : new List<BallDefinition>(),
                IsOverloaded = isOverloaded,
                PerfectStop = perfectStop,
                TurnIndex = turnIndex,
                FloorIndex = floorIndex,
                CombinationMultiplier = 1f
            };

            if (context.Balls.Count >= 3)
            {
                context.Combination = DetermineType(context.Balls[0], context.Balls[1], context.Balls[2]);
            }
            else
            {
                context.Combination = CombinationType.None;
            }

            context.CombinationBaseScore = _config != null
                ? _config.GetBaseScore(context.Combination)
                : 0f;
            context.CombinationMultiplier = _config != null
                ? _config.GetMultiplier(context.Combination)
                : 1f;
            return context;
        }

        public static CombinationType DetermineType(BallDefinition b0, BallDefinition b1, BallDefinition b2)
        {
            if (b0 == null || b1 == null || b2 == null)
                return CombinationType.None;

            if (b0.grade == BallGrade.Legendary || b1.grade == BallGrade.Legendary || b2.grade == BallGrade.Legendary)
                return CombinationType.ContainsLegendary;

            if (b0.id == b1.id && b1.id == b2.id)
                return CombinationType.ThreeOfAKind;

            if ((int)b0.grade < (int)b1.grade && (int)b1.grade < (int)b2.grade)
                return CombinationType.SpecificOrder;

            bool hasCommon = b0.grade == BallGrade.Common || b1.grade == BallGrade.Common || b2.grade == BallGrade.Common;
            bool hasAdvanced = b0.grade == BallGrade.Advanced || b1.grade == BallGrade.Advanced || b2.grade == BallGrade.Advanced;
            bool hasRare = b0.grade == BallGrade.Rare || b1.grade == BallGrade.Rare || b2.grade == BallGrade.Rare;
            if (hasCommon && hasAdvanced && hasRare)
                return CombinationType.CommonAdvancedRare;

            if (b0.grade == BallGrade.Common && b1.grade == BallGrade.Common && b2.grade == BallGrade.Common
                && b0.id != b1.id && b1.id != b2.id && b0.id != b2.id)
                return CombinationType.ThreeDifferentCommon;

            if (b0.grade == b1.grade && b1.grade == b2.grade)
                return CombinationType.ThreeSameGrade;

            return CombinationType.None;
        }

        private static string GradeAbbr(BallGrade grade)
        {
            switch (grade)
            {
                case BallGrade.Common: return "C";
                case BallGrade.Advanced: return "A";
                case BallGrade.Rare: return "R";
                case BallGrade.Legendary: return "L";
                default: return "?";
            }
        }

        private static string BuildSummary(
            BallDefinition b0,
            BallDefinition b1,
            BallDefinition b2,
            CombinationType type,
            float power)
        {
            string s0 = b0 != null ? $"{b0.id}({GradeAbbr(b0.grade)})" : "(null)";
            string s1 = b1 != null ? $"{b1.id}({GradeAbbr(b1.grade)})" : "(null)";
            string s2 = b2 != null ? $"{b2.id}({GradeAbbr(b2.grade)})" : "(null)";
            return $"{s0} {s1} {s2} -> {type} (+{power:F1})";
        }
    }
}
