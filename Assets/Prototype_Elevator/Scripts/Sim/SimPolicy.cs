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

        [Tooltip("통관 하나를 완벽 정지시킬 확률. 사람의 조작 숙련도를 대신하는 가정값이다.")]
        [Range(0f, 1f)] public float perfectStopChance = 0.25f;

        [Tooltip("완벽이 아닐 때 '양호'에 들 확률. 나머지는 빗나감이 된다.")]
        [Range(0f, 1f)] public float goodStopChance = 0.35f;

        [Tooltip("낙하 중인 구슬을 읽고 원하는 구슬을 노려 잡는 데 성공할 확률. " +
                 "정지 타이밍은 '언제 멈출지'뿐 아니라 '무엇을 잡을지'도 정하므로, " +
                 "이걸 빼면 숙련자의 실력이 절반만 모델링된다.")]
        [Range(0f, 1f)] public float ballAimChance = 0f;

        [Tooltip("조준에 성공했을 때 후보로 훑어보는 구슬 수. 통관에 보이는 범위를 뜻한다.")]
        [Range(1, 6)] public int ballAimWindow = 3;

        [Tooltip("초과 전력을 추가 상승에 쓸 확률. 나머지는 돈으로 바꾼다.")]
        [Range(0f, 1f)] public float ascendChance = 0.5f;

        public static SimPolicy Light() => new SimPolicy
        {
            name = "경량형", boardChance = 0.35f, weightCeilingRatio = 0.8f,
            perfectStopChance = 0.35f, goodStopChance = 0.40f, ascendChance = 0.3f, ballAimChance = 0.30f
        };

        public static SimPolicy Balanced() => new SimPolicy
        {
            name = "균형형", boardChance = 0.60f, weightCeilingRatio = 1.0f,
            perfectStopChance = 0.35f, goodStopChance = 0.40f, ascendChance = 0.5f, ballAimChance = 0.30f
        };

        public static SimPolicy Overload() => new SimPolicy
        {
            name = "과적형", boardChance = 0.90f, weightCeilingRatio = 1.6f,
            perfectStopChance = 0.35f, goodStopChance = 0.40f, ascendChance = 0.7f, ballAimChance = 0.30f
        };

        /// <summary>
        /// Presses at random moments — the "mash the button" baseline. With ballSpacing 1 and the
        /// default tolerances a uniformly random press lands perfect ~24% and good ~32% of the time.
        /// If this policy clears the run, timing is not carrying any weight.
        /// </summary>
        public static SimPolicy Masher() => new SimPolicy
        {
            name = "막누르기", boardChance = 0.60f, weightCeilingRatio = 1.0f,
            perfectStopChance = 0.24f, goodStopChance = 0.32f, ascendChance = 0.5f, ballAimChance = 0.00f
        };

        /// <summary>Near-optimal timing, for the upper bound of what skill can buy.</summary>
        public static SimPolicy Expert() => new SimPolicy
        {
            name = "숙련형", boardChance = 0.60f, weightCeilingRatio = 1.0f,
            perfectStopChance = 0.80f, goodStopChance = 0.17f, ascendChance = 0.5f, ballAimChance = 0.65f
        };
    }
}
