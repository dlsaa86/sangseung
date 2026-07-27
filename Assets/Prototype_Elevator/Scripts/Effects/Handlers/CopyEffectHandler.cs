namespace Ascend.Prototype
{
    /// <summary>Copies one ball into the harvested list.</summary>
    public sealed class CopyEffectHandler : IEffectHandler
    {
        public EffectType Type => EffectType.Copy;

        /// <summary>Copies the first matching ball, or the final ball when no target id is supplied.</summary>
        public bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note)
        {
            if (ctx.Balls == null || ctx.Balls.Count == 0)
            {
                note = "No ball to copy";
                return false;
            }

            BallDefinition source = null;
            if (string.IsNullOrEmpty(def.targetBallId))
            {
                source = ctx.Balls[ctx.Balls.Count - 1];
            }
            else
            {
                foreach (BallDefinition ball in ctx.Balls)
                {
                    if (ball != null && ball.id == def.targetBallId)
                    {
                        source = ball;
                        break;
                    }
                }
            }

            if (source == null)
            {
                note = $"Target not found: {def.targetBallId}";
                return false;
            }

            ctx.Balls.Add(source);
            note = $"Copied {source.id} (+{source.baseOutput:F2})";
            return true;
        }
    }
}
