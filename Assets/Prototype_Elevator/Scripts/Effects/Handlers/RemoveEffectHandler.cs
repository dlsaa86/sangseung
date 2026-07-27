namespace Ascend.Prototype
{
    /// <summary>Removes one matching ball from the harvested list.</summary>
    public sealed class RemoveEffectHandler : IEffectHandler
    {
        public EffectType Type => EffectType.Remove;

        /// <summary>Removes the first ball whose id matches the definition.</summary>
        public bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note)
        {
            if (ctx.Balls == null)
            {
                note = "No balls";
                return false;
            }

            for (int i = 0; i < ctx.Balls.Count; i++)
            {
                BallDefinition ball = ctx.Balls[i];
                if (ball == null || ball.id != def.targetBallId)
                    continue;

                ctx.Balls.RemoveAt(i);
                note = $"Removed {ball.id} (-{ball.baseOutput:F2})";
                return true;
            }

            note = $"Target not found: {def.targetBallId}";
            return false;
        }
    }
}
