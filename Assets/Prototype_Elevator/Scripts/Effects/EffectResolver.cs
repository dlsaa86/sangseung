using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Thin Unity adapter around the pure C# effect pipeline.</summary>
    public class EffectResolver : MonoBehaviour
    {
        [SerializeField] private EffectResolverSettings _settings;
        [SerializeField] private List<EffectDefinition> _globalEffects = new List<EffectDefinition>();

        private List<EffectDefinition> _activeEffects;
        private bool _hasActiveEffects;
        private IEffectRandom _random = new SystemEffectRandom(0);
        private IReadOnlyList<EffectLogEntry> _lastLog = new List<EffectLogEntry>();

        /// <summary>Returns the log from the most recent resolution.</summary>
        public IReadOnlyList<EffectLogEntry> LastLog => _lastLog;

        /// <summary>Initialises the effect random source for a reproducible run.</summary>
        public void InitializeSeed(int seed)
        {
            _random = new SystemEffectRandom(unchecked(seed * 397 ^ 0x5EED));
        }

        /// <summary>Replaces global effects with effects injected by a later system.</summary>
        public void SetActiveEffects(IReadOnlyList<EffectDefinition> effects)
        {
            _activeEffects = effects != null
                ? new List<EffectDefinition>(effects)
                : new List<EffectDefinition>();
            _hasActiveEffects = true;
        }

        /// <summary>Runs the configured effect chain and returns the resolved context.</summary>
        public GenerationContext Resolve(GenerationContext ctx)
        {
            IReadOnlyList<EffectDefinition> effects = _hasActiveEffects ? _activeEffects : _globalEffects;
            var pipeline = new EffectPipeline(_settings, _random);
            GenerationContext result = pipeline.Run(ctx, effects);
            _lastLog = new List<EffectLogEntry>(result.Log);
            return result;
        }

        /// <summary>Builds a multi-line UI representation of the most recent effect log.</summary>
        public string BuildLogText()
        {
            if (_lastLog == null || _lastLog.Count == 0)
                return string.Empty;

            var lines = new List<string>(_lastLog.Count);
            foreach (EffectLogEntry entry in _lastLog)
                lines.Add(entry.ToDisplayString());
            return string.Join("\n", lines);
        }

        /// <summary>Preserved for older call sites that do not yet provide a context.</summary>
        public void ResolveEffects()
        {
        }
    }
}
