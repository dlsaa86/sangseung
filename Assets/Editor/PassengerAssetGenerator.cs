using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype.Editor
{
    /// <summary>Creates the five starter passenger definitions without overwriting existing assets.</summary>
    public static class PassengerAssetGenerator
    {
        private const string DataFolder = "Assets/Prototype_Elevator/Data";
        private const string PassengersFolder = "Assets/Prototype_Elevator/Data/Passengers";
        private const string EffectsFolder = "Assets/Prototype_Elevator/Data/Effects";

        private struct PassengerSpec
        {
            public string Id;
            public string DisplayName;
            public float Weight;
            public float AllowedWeightBonus;
            public string EffectId;
            public string Description;
        }

        [MenuItem("Ascend/Generate Passenger Assets")]
        public static void Generate()
        {
            EnsurePassengersFolder();

            PassengerSpec[] specs =
            {
                new PassengerSpec
                {
                    Id = "PSG_TECHNICIAN",
                    DisplayName = "\uAE30\uC220\uC790",
                    Weight = 2f,
                    EffectId = "EFF_TECHNICIAN_ADD",
                    Description = "\uBC1C\uC804\uB9C8\uB2E4 \uC804\uB825 +2"
                },
                new PassengerSpec
                {
                    Id = "PSG_TRANSFORMER",
                    DisplayName = "\uBCC0\uC555\uAE30 \uAE30\uC0AC",
                    Weight = 4f,
                    EffectId = "EFF_TRANSFORMER_MUL",
                    Description = "\uCD5C\uC885 \uC804\uB825 \u00D72"
                },
                new PassengerSpec
                {
                    Id = "PSG_GAMBLER",
                    DisplayName = "\uB3C4\uBC15\uC0AC",
                    Weight = 3f,
                    EffectId = "EFF_GAMBLER_REPEAT",
                    Description = "\uC644\uBCBD \uC815\uC9C0 \uC2DC \uD6A8\uACFC \uC7AC\uBC1C\uB3D9"
                },
                new PassengerSpec
                {
                    Id = "PSG_PORTER",
                    DisplayName = "\uC9D0\uAFBC",
                    Weight = 2f,
                    AllowedWeightBonus = 5f,
                    Description = "\uD5C8\uC6A9 \uC911\uB7C9 +5"
                },
                new PassengerSpec
                {
                    Id = "PSG_ZEALOT",
                    DisplayName = "\uACFC\uC801 \uAD11\uC2E0\uB3C4",
                    Weight = 6f,
                    EffectId = "EFF_ZEALOT_OVERLOAD_MUL",
                    Description = "\uACFC\uC801 \uC0C1\uD0DC\uC5D0\uC11C \uC804\uB825 \u00D72"
                }
            };

            foreach (PassengerSpec spec in specs)
            {
                string passengerPath = $"{PassengersFolder}/{spec.Id}.asset";
                if (AssetDatabase.LoadAssetAtPath<PassengerDefinition>(passengerPath) != null)
                {
                    Debug.Log($"[\uC0C1\uC2B9] Passenger asset exists, skipped: {passengerPath}");
                    continue;
                }

                var definition = ScriptableObject.CreateInstance<PassengerDefinition>();
                definition.id = spec.Id;
                definition.displayName = spec.DisplayName;
                definition.description = spec.Description;
                definition.weight = spec.Weight;
                definition.allowedWeightBonus = spec.AllowedWeightBonus;
                definition.effects = new List<EffectDefinition>();

                if (!string.IsNullOrEmpty(spec.EffectId))
                {
                    string effectPath = $"{EffectsFolder}/{spec.EffectId}.asset";
                    EffectDefinition effect = AssetDatabase.LoadAssetAtPath<EffectDefinition>(effectPath);
                    if (effect != null)
                    {
                        definition.effects.Add(effect);
                    }
                    else
                    {
                        Debug.LogWarning($"[\uC0C1\uC2B9] Passenger effect asset missing, left empty: {effectPath}");
                    }
                }

                AssetDatabase.CreateAsset(definition, passengerPath);
                Debug.Log($"[\uC0C1\uC2B9] Passenger asset created: {passengerPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsurePassengersFolder()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets/Prototype_Elevator", "Data");

            if (!AssetDatabase.IsValidFolder(PassengersFolder))
                AssetDatabase.CreateFolder(DataFolder, "Passengers");
        }
    }
}
