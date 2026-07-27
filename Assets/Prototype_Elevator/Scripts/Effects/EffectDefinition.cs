using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Data-driven definition of one generation effect.</summary>
    [CreateAssetMenu(fileName = "EffectDefinition", menuName = "Ascend/EffectDefinition")]
    public class EffectDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public EffectType type;
        [Range(0, 100)] public int priority;
        public EffectTrigger trigger;
        public EffectCondition condition;
        public BallGrade conditionGrade;
        [Range(0f, 1f)] public float probability = 1f;
        public float value;
        public string targetBallId;
        public string resultBallId;
        [Range(1, 5)] public int repeatCount = 1;
        [TextArea] public string description;
    }
}
