using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Converts a ball definition or a portion of current power.</summary>
    public sealed class ConvertEffectHandler : IEffectHandler
    {
        public EffectType Type => EffectType.Convert;

        /// <summary>Replaces the first matching ball, or moves power to money when no result id exists.</summary>
        public bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note)
        {
            if (ctx.Balls == null)
                ctx.Balls = new List<BallDefinition>();

            if (string.IsNullOrEmpty(def.resultBallId))
            {
                float powerMoved = ctx.ComputeCurrentPower() * def.value;
                ctx.MoneyDelta += powerMoved;
                ctx.FlatBonus -= powerMoved;
                note = $"Power -> Money +{powerMoved:F2}";
                return true;
            }

            for (int i = 0; i < ctx.Balls.Count; i++)
            {
                BallDefinition source = ctx.Balls[i];
                if (source == null || source.id != def.targetBallId)
                    continue;

                BallDefinition result = FindResultBall(ctx.Balls, def.resultBallId);
                if (result == null)
                    result = CreateFallbackResult(source, def.resultBallId);

                ctx.Balls[i] = result;
                note = $"{source.id} -> {result.id}";
                return true;
            }

            note = $"Target not found: {def.targetBallId}";
            return false;
        }

        private static BallDefinition FindResultBall(List<BallDefinition> balls, string resultId)
        {
            foreach (BallDefinition ball in balls)
                if (ball != null && ball.id == resultId)
                    return ball;

            BallDefinition[] loaded = Resources.FindObjectsOfTypeAll<BallDefinition>();
            foreach (BallDefinition ball in loaded)
                if (ball != null && ball.id == resultId)
                    return ball;

            return null;
        }

        private static BallDefinition CreateFallbackResult(BallDefinition source, string resultId)
        {
            BallDefinition result = ScriptableObject.CreateInstance<BallDefinition>();
            result.id = resultId;
            result.displayName = resultId;
            result.grade = source.grade;
            result.spawnProbability = source.spawnProbability;
            result.baseOutput = source.baseOutput;
            result.debugColor = source.debugColor;
            return result;
        }
    }
}
