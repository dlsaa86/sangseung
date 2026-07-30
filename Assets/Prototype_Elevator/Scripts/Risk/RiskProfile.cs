using System;
using UnityEngine;

namespace Ascend.Prototype.Risk
{
    /// <summary>
    /// 위험 단계 하나가 공간에 만드는 변화. `TECH_SPEC.md` §6.4의 RiskProfile 항목이다.
    ///
    /// 값이 전부 필드인 이유: 공포 표현 강도는 **승인 대기 항목**이다
    /// (`MASTER_PRD.md` §1, `VISUAL_SPEC.md` §11). 최종안 하나로 코드에 잠그면
    /// 나중에 바꾸는 데 코드 수정이 필요해진다.
    /// </summary>
    [Serializable]
    public struct RiskProfile
    {
        [Header("조명")]
        [Tooltip("실내등 밝기 배율. Stable을 1로 본다.")]
        public float LightIntensity;

        [Tooltip("실내등 색. 위험할수록 따뜻하고 탁해진다 — 색 하나에만 의존하지 않도록 밝기도 함께 움직인다.")]
        public Color LightColor;

        [Tooltip("초당 깜빡임 횟수. 0이면 깜빡이지 않는다.")]
        public float FlickerRate;

        [Tooltip("깜빡임 깊이(0~1). 1이면 완전히 꺼진다 — 결과 판독을 죽이므로 쓰지 않는다.")]
        [Range(0f, 1f)] public float FlickerDepth;

        [Header("경고등")]
        public Color WarningColor;
        [Tooltip("경고등 맥동 속도. 0이면 켜진 채 고정.")]
        public float WarningPulseRate;
        [Tooltip("경고등 최대 발광. 0이면 꺼진 것으로 본다.")]
        public float WarningEmission;

        [Header("진동")]
        [Tooltip("천장등이 흔들리는 폭(미터). 화면이 아니라 물체가 흔들린다.")]
        public float SwayAmplitude;
        public float SwayRate;

        [Tooltip("카메라 흔들림 폭(미터). VISUAL_SPEC §8이 과도한 흔들림을 금지하므로 작게 유지한다.")]
        public float CameraShake;

        [Header("소리")]
        [Tooltip("기계 험 음량(0~1).")]
        [Range(0f, 1f)] public float HumVolume;
        [Tooltip("기계 험 피치 배율. 높을수록 조여진 느낌.")]
        public float HumPitch;

        /// <summary>
        /// 4단계 기본 프리셋 3종. `VISUAL_SPEC.md` §11이 "2~3개의 교체 가능한 프리셋"을 요구한다.
        /// </summary>
        public static RiskProfile[] Preset(RiskIntensity intensity)
        {
            // Stable은 세 프리셋이 같다 — 기준 상태는 판독성이 최우선이라 흔들 이유가 없다.
            RiskProfile stable = new RiskProfile
            {
                LightIntensity = 1f, LightColor = new Color(0.85f, 0.87f, 0.92f),
                FlickerRate = 0f, FlickerDepth = 0f,
                WarningColor = new Color(0.18f, 0.19f, 0.21f), WarningPulseRate = 0f, WarningEmission = 0f,
                SwayAmplitude = 0f, SwayRate = 0f, CameraShake = 0f,
                HumVolume = 0.10f, HumPitch = 1.0f,
            };

            switch (intensity)
            {
                case RiskIntensity.Restrained:
                    return new[]
                    {
                        stable,
                        new RiskProfile
                        {
                            LightIntensity = 0.86f, LightColor = new Color(0.88f, 0.80f, 0.68f),
                            FlickerRate = 1.4f, FlickerDepth = 0.10f,
                            WarningColor = new Color(0.95f, 0.66f, 0.22f), WarningPulseRate = 1.1f, WarningEmission = 1.1f,
                            SwayAmplitude = 0.004f, SwayRate = 1.6f, CameraShake = 0f,
                            HumVolume = 0.16f, HumPitch = 1.06f,
                        },
                        new RiskProfile
                        {
                            LightIntensity = 0.70f, LightColor = new Color(0.92f, 0.62f, 0.44f),
                            FlickerRate = 3.2f, FlickerDepth = 0.20f,
                            WarningColor = new Color(1f, 0.42f, 0.26f), WarningPulseRate = 2.6f, WarningEmission = 2.2f,
                            SwayAmplitude = 0.010f, SwayRate = 3.0f, CameraShake = 0.0015f,
                            HumVolume = 0.26f, HumPitch = 1.16f,
                        },
                        Collapse(0.44f, 0.24f, 3.0f, 0.014f, 0.003f, 0.30f),
                    };

                case RiskIntensity.Heavy:
                    return new[]
                    {
                        stable,
                        new RiskProfile
                        {
                            LightIntensity = 0.74f, LightColor = new Color(0.90f, 0.74f, 0.55f),
                            FlickerRate = 3.0f, FlickerDepth = 0.24f,
                            WarningColor = new Color(1f, 0.62f, 0.18f), WarningPulseRate = 2.0f, WarningEmission = 1.9f,
                            SwayAmplitude = 0.012f, SwayRate = 2.6f, CameraShake = 0.0012f,
                            HumVolume = 0.26f, HumPitch = 1.12f,
                        },
                        new RiskProfile
                        {
                            LightIntensity = 0.50f, LightColor = new Color(0.96f, 0.46f, 0.30f),
                            FlickerRate = 6.5f, FlickerDepth = 0.42f,
                            WarningColor = new Color(1f, 0.26f, 0.18f), WarningPulseRate = 4.4f, WarningEmission = 3.6f,
                            SwayAmplitude = 0.028f, SwayRate = 5.0f, CameraShake = 0.0045f,
                            HumVolume = 0.44f, HumPitch = 1.30f,
                        },
                        Collapse(0.28f, 0.50f, 6.0f, 0.038f, 0.008f, 0.52f),
                    };

                default:   // Standard
                    return new[]
                    {
                        stable,
                        new RiskProfile
                        {
                            LightIntensity = 0.80f, LightColor = new Color(0.89f, 0.77f, 0.62f),
                            FlickerRate = 2.2f, FlickerDepth = 0.16f,
                            WarningColor = new Color(0.98f, 0.64f, 0.20f), WarningPulseRate = 1.6f, WarningEmission = 1.5f,
                            SwayAmplitude = 0.008f, SwayRate = 2.2f, CameraShake = 0.0008f,
                            HumVolume = 0.20f, HumPitch = 1.09f,
                        },
                        new RiskProfile
                        {
                            LightIntensity = 0.58f, LightColor = new Color(0.94f, 0.52f, 0.34f),
                            FlickerRate = 4.8f, FlickerDepth = 0.30f,
                            WarningColor = new Color(1f, 0.32f, 0.22f), WarningPulseRate = 3.4f, WarningEmission = 2.9f,
                            SwayAmplitude = 0.018f, SwayRate = 4.0f, CameraShake = 0.0026f,
                            HumVolume = 0.34f, HumPitch = 1.22f,
                        },
                        Collapse(0.34f, 0.38f, 4.5f, 0.026f, 0.005f, 0.42f),
                    };
            }
        }

        private static RiskProfile Collapse(float intensity, float flickerDepth, float flickerRate,
                                            float sway, float shake, float hum)
        {
            // 암전으로 정보를 숨기지 않는다(VISUAL_SPEC §6 Collapse). 어두워지되 결과는 보인다.
            return new RiskProfile
            {
                LightIntensity = intensity, LightColor = new Color(0.86f, 0.34f, 0.28f),
                FlickerRate = flickerRate, FlickerDepth = flickerDepth,
                WarningColor = new Color(1f, 0.18f, 0.14f), WarningPulseRate = 5.5f, WarningEmission = 4.0f,
                SwayAmplitude = sway, SwayRate = 5.5f, CameraShake = shake,
                HumVolume = hum, HumPitch = 0.78f,
            };
        }
    }

    /// <summary>공포 표현 강도 프리셋. 사용자 승인 전까지 하나로 잠그지 않는다.</summary>
    public enum RiskIntensity
    {
        /// <summary>가장 절제된 안. 멀미·섬광 피로가 걱정될 때.</summary>
        Restrained = 0,

        /// <summary>기본안.</summary>
        Standard = 1,

        /// <summary>가장 강한 안. 붕괴 직전의 압박을 최대로.</summary>
        Heavy = 2,
    }
}
