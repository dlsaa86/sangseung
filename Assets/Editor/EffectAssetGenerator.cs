using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype.Editor
{
    /// <summary>Creates the starter effect definitions and resolver settings asset.</summary>
    public static class EffectAssetGenerator
    {
        private const string EffectsFolder = "Assets/Prototype_Elevator/Data/Effects";
        private const string SettingsPath = "Assets/Prototype_Elevator/Data/EffectResolverSettings.asset";

        private struct EffectSpec
        {
            public string Id;
            public string DisplayName;
            public EffectType Type;
            public EffectTrigger Trigger;
            public EffectCondition Condition;
            public float Value;
            public BallGrade ConditionGrade;
            public int RepeatCount;
        }

        /// <summary>Generates missing starter effect assets without overwriting existing assets.</summary>
        [MenuItem("Ascend/Generate Effect Assets")]
        public static void Generate()
        {
            Directory.CreateDirectory(EffectsFolder);
            Directory.CreateDirectory("Assets/Prototype_Elevator/Data");
            AssetDatabase.Refresh();

            EffectSpec[] specs =
            {
                new EffectSpec { Id = "EFF_TECHNICIAN_ADD", DisplayName = "\uAE30\uC220\uC790 \uBC1C\uC804 \uBCF4\uB108\uC2A4", Type = EffectType.Add, Trigger = EffectTrigger.OnGeneration, Condition = EffectCondition.None, Value = 2f, RepeatCount = 1 },
                new EffectSpec { Id = "EFF_TRANSFORMER_MUL", DisplayName = "\uBCC0\uC555\uAE30 \uAE30\uC0AC \uC99D\uD3ED", Type = EffectType.Multiply, Trigger = EffectTrigger.OnFinal, Condition = EffectCondition.None, Value = 2f, RepeatCount = 1 },
                new EffectSpec { Id = "EFF_GAMBLER_REPEAT", DisplayName = "\uB3C4\uBC15\uC0AC \uC7AC\uBC1C\uB3D9", Type = EffectType.Repeat, Trigger = EffectTrigger.OnFinal, Condition = EffectCondition.PerfectStop, Value = 0f, RepeatCount = 1 },
                new EffectSpec { Id = "EFF_ZEALOT_OVERLOAD_MUL", DisplayName = "\uACFC\uC801 \uAD11\uC2E0\uB3C4", Type = EffectType.Multiply, Trigger = EffectTrigger.OnFinal, Condition = EffectCondition.Overloaded, Value = 2f, RepeatCount = 1 },
                new EffectSpec { Id = "EFF_TEST_LEGENDARY_ADD", DisplayName = "\uC804\uC124 \uAC00\uC0B0(\uD14C\uC2A4\uD2B8)", Type = EffectType.Add, Trigger = EffectTrigger.OnCombination, Condition = EffectCondition.ContainsGrade, ConditionGrade = BallGrade.Legendary, Value = 15f, RepeatCount = 1 }
            };

            foreach (EffectSpec spec in specs)
            {
                string path = $"{EffectsFolder}/{spec.Id}.asset";
                if (AssetDatabase.LoadAssetAtPath<EffectDefinition>(path) != null)
                {
                    Debug.Log($"[\uC0C1\uC2B9] Effect asset exists, skipped: {path}");
                    continue;
                }

                var definition = ScriptableObject.CreateInstance<EffectDefinition>();
                definition.id = spec.Id;
                definition.displayName = spec.DisplayName;
                definition.type = spec.Type;
                definition.trigger = spec.Trigger;
                definition.condition = spec.Condition;
                definition.conditionGrade = spec.ConditionGrade;
                definition.value = spec.Value;
                definition.repeatCount = spec.RepeatCount;
                definition.probability = 1f;
                AssetDatabase.CreateAsset(definition, path);
                Debug.Log($"[\uC0C1\uC2B9] Effect asset created: {path}");
            }

            if (AssetDatabase.LoadAssetAtPath<EffectResolverSettings>(SettingsPath) == null)
            {
                var settings = ScriptableObject.CreateInstance<EffectResolverSettings>();
                settings.maxRecursionDepth = 3;
                settings.maxTotalActivations = 64;
                settings.verboseLogging = true;
                AssetDatabase.CreateAsset(settings, SettingsPath);
                Debug.Log($"[\uC0C1\uC2B9] Effect resolver settings created: {SettingsPath}");
            }
            else
            {
                Debug.Log($"[\uC0C1\uC2B9] Effect resolver settings exists, skipped: {SettingsPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
