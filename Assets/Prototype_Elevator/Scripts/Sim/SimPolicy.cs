using System;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Describes how the simulated player behaves for one batch of runs.
    /// Kept as data so different build directions can be compared without code changes.
    /// </summary>
    [Serializable]
    public class SimPolicy
    {
        public string name = "균형형";

        [Tooltip("후보 승객을 태울 확률 (0~1).")]
        [Range(0f, 1f)] public float boardChance = 0.6f;

        [Tooltip("총무게가 허용 중량의 이 비율을 넘으면 더 태우지 않는다.")]
        public float weightCeilingRatio = 1.0f;

        [Tooltip("세 통관을 완벽 정지시킬 확률. 사람의 조작 숙련도를 대신하는 가정값이다.")]
        [Range(0f, 1f)] public float perfectStopChance = 0.25f;

        [Tooltip("초과 전력을 추가 상승에 쓸 확률. 나머지는 돈으로 바꾼다.")]
        [Range(0f, 1f)] public float ascendChance = 0.5f;

        public static SimPolicy Light() => new SimPolicy
        {
            name = "경량형", boardChance = 0.35f, weightCeilingRatio = 0.8f,
            perfectStopChance = 0.25f, ascendChance = 0.3f
        };

        public static SimPolicy Balanced() => new SimPolicy
        {
            name = "균형형", boardChance = 0.60f, weightCeilingRatio = 1.0f,
            perfectStopChance = 0.25f, ascendChance = 0.5f
        };

        public static SimPolicy Overload() => new SimPolicy
        {
            name = "과적형", boardChance = 0.90f, weightCeilingRatio = 1.6f,
            perfectStopChance = 0.25f, ascendChance = 0.7f
        };
    }
}
