using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Data-driven definition of a passenger that can board the elevator.</summary>
    [CreateAssetMenu(fileName = "PassengerDefinition", menuName = "Ascend/PassengerDefinition")]
    public class PassengerDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public float weight;
        public float allowedWeightBonus;
        public List<EffectDefinition> effects = new List<EffectDefinition>();
        public Color debugColor = Color.white;
    }
}
