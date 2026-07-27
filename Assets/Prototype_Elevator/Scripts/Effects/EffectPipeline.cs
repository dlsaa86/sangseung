using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Runs ordered, data-driven effects against a mutable generation context.</summary>
    public class EffectPipeline
    {
        private readonly EffectResolverSettings _settings;
        private readonly IEffectRandom _rng;
        private readonly Dictionary<EffectType, IEffectHandler> _handlers;

        /// <summary>Creates a pipeline and registers the built-in effect handlers.</summary>
        public EffectPipeline(EffectResolverSettings settings, IEffectRandom rng)
        {
            _settings = settings;
            _rng = rng ?? new SystemEffectRandom(0);
            _handlers = new Dictionary<EffectType, IEffectHandler>();
            foreach (Type handlerType in typeof(EffectPipeline).Assembly.GetTypes())
            {
                if (handlerType.IsInterface || handlerType.IsAbstract || !typeof(IEffectHandler).IsAssignableFrom(handlerType))
                    continue;

                var handler = (IEffectHandler)Activator.CreateInstance(handlerType);
                _handlers[handler.Type] = handler;
            }
        }

        /// <summary>Runs the complete generation lifecycle and returns the same context instance.</summary>
        public GenerationContext Run(GenerationContext ctx, IReadOnlyList<EffectDefinition> effects)
        {
            if (ctx == null)
                ctx = new GenerationContext();

            ctx.Log.Clear();
            ctx.PendingProbabilityModifiers.Clear();
            ctx.RepeatRequested = 0;
            ctx.RepeatBonusPower = 0f;
            AddInputLog(ctx);

            var activated = new HashSet<string>();
            int activationCount = 0;
            int maxDepth = _settings != null ? Math.Max(0, _settings.maxRecursionDepth) : 3;
            int maxActivations = _settings != null ? Math.Max(0, _settings.maxTotalActivations) : 64;

            ProcessPass(ctx, effects, 0, true, activated, ref activationCount, maxActivations);
            float basePassPower = ctx.ComputeCurrentPower();
            int repeats = ctx.RepeatRequested;
            int completedRepeats = 0;

            for (int r = 0; r < repeats && r < maxDepth; r++)
            {
                GenerationContext pass = ctx.CloneForRepeat();
                ProcessPass(pass, effects, r + 1, false, activated, ref activationCount, maxActivations);
                ctx.RepeatBonusPower += pass.ComputeCurrentPower();
                ctx.MoneyDelta += pass.MoneyDelta;

                foreach (EffectLogEntry entry in pass.Log)
                {
                    EffectLogEntry copy = entry;
                    copy.Depth = r + 1;
                    copy.Order = ctx.Log.Count;
                    ctx.Log.Add(copy);
                }

                completedRepeats++;
                if (activationCount >= maxActivations)
                    break;
            }

            if (repeats > maxDepth)
                Debug.LogWarning($"[EffectPipeline] EffectPipeline: repeat stopped at max recursion depth {maxDepth}.");

            ctx.FinalPower = basePassPower + ctx.RepeatBonusPower;
            AddFinalLog(ctx, completedRepeats);
            if (IsVerbose)
                Debug.Log($"[EffectPipeline] EffectPipeline: final power {ctx.FinalPower:F2}");
            return ctx;
        }

        private bool IsVerbose => _settings == null || _settings.verboseLogging;

        private void ProcessPass(
            GenerationContext ctx,
            IReadOnlyList<EffectDefinition> effects,
            int depth,
            bool allowRepeatHandler,
            HashSet<string> activated,
            ref int activationCount,
            int maxActivations)
        {
            List<EffectDefinition> ordered = OrderEffects(effects);
            foreach (EffectDefinition def in ordered)
            {
                if (def == null)
                    continue;

                float before = ctx.ComputeCurrentPower();
                bool applied = false;
                string note;

                if (!allowRepeatHandler && def.type == EffectType.Repeat)
                {
                    AddLog(ctx, def, depth, before, before, false, "\uBC18\uBCF5 \uD328\uC2A4\uC5D0\uC11C\uB294 \uC7AC\uBC1C\uB3D9 \uC5C6\uC74C");
                    continue;
                }

                if (!_handlers.ContainsKey(def.type))
                {
                    note = "No handler registered";
                    AddLog(ctx, def, depth, before, before, false, note);
                    continue;
                }

                if (!IsTriggerActive(def.trigger, ctx))
                {
                    note = $"Trigger mismatch: {def.trigger}";
                    AddLog(ctx, def, depth, before, before, false, note);
                    continue;
                }

                if (!IsConditionMet(def, ctx))
                {
                    note = $"Condition failed: {def.condition}";
                    AddLog(ctx, def, depth, before, before, false, note);
                    continue;
                }

                double roll = _rng.NextDouble();
                if (roll >= def.probability)
                {
                    note = $"Probability {def.probability:F2} failed (roll {roll:F2})";
                    AddLog(ctx, def, depth, before, before, false, note);
                    continue;
                }

                string cycleKey = $"{def.id ?? string.Empty}:{depth}";
                if (activated.Contains(cycleKey))
                {
                    note = $"Cycle detected: {cycleKey}";
                    AddLog(ctx, def, depth, before, before, false, note);
                    continue;
                }

                if (activationCount >= maxActivations)
                {
                    note = $"Max total activations reached: {maxActivations}";
                    AddLog(ctx, def, depth, before, before, false, note);
                    Debug.LogWarning($"[EffectPipeline] EffectPipeline: max total activations {maxActivations} reached.");
                    break;
                }

                applied = _handlers[def.type].Apply(def, ctx, _rng, out note);
                float after = ctx.ComputeCurrentPower();
                if (applied)
                {
                    activated.Add(cycleKey);
                    activationCount++;
                }

                AddLog(ctx, def, depth, before, after, applied, note);
                if (IsVerbose)
                    Debug.Log($"[EffectPipeline] Effect {def.id} D{depth}: {before:F2} -> {after:F2} ({note})");
            }
        }

        private List<EffectDefinition> OrderEffects(IReadOnlyList<EffectDefinition> effects)
        {
            var ordered = new List<EffectDefinition>();
            if (effects == null)
                return ordered;

            foreach (EffectType type in (EffectType[])Enum.GetValues(typeof(EffectType)))
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    EffectDefinition candidate = effects[i];
                    if (candidate != null && candidate.type == type)
                        InsertByPriority(ordered, candidate);
                }
            }
            return ordered;
        }

        private static void InsertByPriority(List<EffectDefinition> ordered, EffectDefinition candidate)
        {
            int insertAt = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].type == candidate.type && ordered[i].priority > candidate.priority)
                {
                    insertAt = i;
                    break;
                }
            }
            ordered.Insert(insertAt, candidate);
        }

        private static bool IsTriggerActive(EffectTrigger trigger, GenerationContext ctx)
        {
            return trigger != EffectTrigger.OnOverload || ctx.IsOverloaded;
        }

        private static bool IsConditionMet(EffectDefinition def, GenerationContext ctx)
        {
            switch (def.condition)
            {
                case EffectCondition.Overloaded:
                    return ctx.IsOverloaded;
                case EffectCondition.NotOverloaded:
                    return !ctx.IsOverloaded;
                case EffectCondition.PerfectStop:
                    return ctx.PerfectStop;
                case EffectCondition.ContainsGrade:
                    if (ctx.Balls == null) return false;
                    foreach (BallDefinition ball in ctx.Balls)
                        if (ball != null && ball.grade == def.conditionGrade) return true;
                    return false;
                case EffectCondition.None:
                default:
                    return true;
            }
        }

        private static void AddInputLog(GenerationContext ctx)
        {
            ctx.Log.Add(new EffectLogEntry
            {
                Order = -1,
                Depth = 0,
                EffectId = "__INPUT__",
                EffectName = "Pipeline Input",
                Type = EffectType.Add,
                PowerBefore = ctx.ComputeCurrentPower(),
                PowerAfter = ctx.ComputeCurrentPower(),
                Applied = true,
                Note = $"Balls={ctx.Balls?.Count ?? 0}, Combination={ctx.Combination}"
            });
        }

        private static void AddFinalLog(GenerationContext ctx, int depth)
        {
            ctx.Log.Add(new EffectLogEntry
            {
                Order = ctx.Log.Count,
                Depth = depth,
                EffectId = "__FINAL__",
                EffectName = "Pipeline Final",
                Type = EffectType.Add,
                PowerBefore = ctx.FinalPower,
                PowerAfter = ctx.FinalPower,
                Applied = true,
                Note = $"MoneyDelta={ctx.MoneyDelta:F2}, Balls={ctx.Balls?.Count ?? 0}"
            });
        }

        private static void AddLog(GenerationContext ctx, EffectDefinition def, int depth, float before, float after, bool applied, string note)
        {
            ctx.Log.Add(new EffectLogEntry
            {
                Order = ctx.Log.Count,
                Depth = depth,
                EffectId = def.id,
                EffectName = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName,
                Type = def.type,
                PowerBefore = before,
                PowerAfter = after,
                Applied = applied,
                Note = note
            });
        }
    }
}
