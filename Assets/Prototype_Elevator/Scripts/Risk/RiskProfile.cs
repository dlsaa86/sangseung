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
                        // Warning 이 Stable 과 구분되지 않는다는 지적을 두 번의 독립
                        // 감사에서 받았다. "글자를 가리면 차이가 지름 50px 램프 색
                        // 하나뿐"이라고 했다.
                        //
                        // 원래 값은 (0.89, 0.77, 0.62) / 밝기 0.80 이었다. Stable 의
                        // (0.85, 0.87, 0.92) 와 색상 거리가 좁은데 **밝기까지 낮아서**
                        // 둘이 서로를 상쇄했다 — 조금 누렇고 조금 어두운 것은 그냥
                        // "조금 어두운 같은 방"으로 보인다.
                        //
                        // 흔들림·깜빡임은 정지 화면에서 증거가 되지 못한다. 고정 캡처로
                        // 판정하는 이상 정지 상태에서 작동하는 채널은 색이다. 채도를
                        // 올려 Stable(차가운 회청) → Warning(호박) → Critical(적등) 이
                        // 세 걸음으로 벌어지게 한다. Critical(0.94, 0.52, 0.34)과도
                        // 색상이 겹치지 않는다.
                        new RiskProfile
                        {
                            LightIntensity = 0.84f, LightColor = new Color(0.96f, 0.72f, 0.38f),
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

    /// <summary>
    /// 위험 단계를 **명도축(V)** 으로도 내리는 앰비언트 사다리.
    ///
    /// 왜 따로 있는가 — 7차 독립 판정이 앰비언트 틴트를 채택하면서 동시에 이렇게 적었다:
    /// 「가장 약한 인접 쌍은 **Strain↔Critical** 이다. 둘 다 따뜻한 갈색 방이고 **밝기 차가
    /// 없다.** 「기준점으로부터의 거리」는 인접 구분 가능성을 재지 못한다 — 색상만 움직이고
    /// 명도가 그대로면 거리가 벌어져도 사람 눈에는 같은 밴드다.」
    ///
    /// 실제로 그랬다. `RiskStateView.ApplyAmbient` 가 쓰던 식은
    /// <c>Lerp(원래앰비언트, LightColor, 0.55)</c> 하나였고, 여기에 <see cref="RiskProfile.LightIntensity"/>
    /// 는 **한 번도 들어가지 않았다.** 씬 앰비언트 (0.26, 0.27, 0.31) 와 표준 프리셋으로
    /// 계산한 그 경로의 V 사다리는
    ///
    ///   Stable 0.6455 → Strain 0.6450 → Critical 0.6340 → Collapse 0.5900
    ///
    /// 이다. Stable↔Strain 간격이 **0.0005**, Strain↔Critical 이 **0.0110** — 색상축만
    /// 움직이고 명도축은 사실상 정지해 있었다. 단계 구분이 색맹·축소본·회색조에서 전부
    /// 무너지는 이유가 이것이다.
    ///
    /// **여기서 새 값을 만들지 않는다.** 명도는 이미 데이터에 있다 —
    /// <see cref="RiskProfile.LightIntensity"/> 는 세 프리셋 모두에서 단조 하강한다
    /// (표준 1.00 / 0.84 / 0.58 / 0.34). 그 축이 지금까지 실내등 하나에만 닿고 방에는
    /// 닿지 않았을 뿐이다. `DangerFeedbackProfile` 에 필드를 새로 넣지 않는 이유이기도 하다 —
    /// 구조체에 필드를 늘리면 이미 찍혀 있는 `.asset` 의 배열이 그 필드를 0으로 읽는다.
    /// </summary>
    public static class RiskAmbientLadder
    {
        /// <summary><see cref="RiskLevel"/> 단계 수.</summary>
        public const int LevelCount = 4;

        /// <summary>
        /// 인접 단계의 **명도차 하한**. 「색거리」가 아니라 명도차다.
        ///
        /// 이 저장소는 앰비언트 도입 때 「색거리 6.1 → 13.4, 두 배」를 개선 근거로 적었다가
        /// 인접 쌍을 하나도 못 갈랐다(백로그 §5.1). 두 배가 된 것은 **기준점(Stable)으로부터의
        /// 거리**였고, 사람이 보는 것은 **옆 단계와의 차이**다. 그래서 지표를 바꾼다.
        /// </summary>
        public const float MinValueStep = 0.08f;

        /// <summary>
        /// 아무리 눌러도 이 아래로는 내리지 않는다. `VISUAL_SPEC §6` 이 Collapse 에서
        /// 암전으로 정보를 숨기는 것을 금지한다 — 어두워지되 결과는 보여야 한다.
        /// </summary>
        public const float HardFloor = 0.04f;

        /// <summary>HSV 의 V. 최대 성분이다.</summary>
        public static float ValueOf(Color color)
        {
            return Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        }

        /// <summary>
        /// 색상(H)·채도(S)를 그대로 두고 명도(V)만 바꾼다.
        ///
        /// RGB 를 균일 배수하는 것이 곧 HSV 의 V 교체다 — H 는 성분 **순서와 차이의 비**로,
        /// S 는 <c>1 − min/max</c> 로 정의되므로 둘 다 균일 스케일에 불변이다.
        /// <c>Color.RGBToHSV</c>/<c>HSVToRGB</c> 왕복보다 정확하고(부동소수 왕복 오차가 없다)
        /// HDR 색에서도 클램프되지 않는다. 그 불변성은 테스트가 `Color.RGBToHSV` 로 확인한다.
        /// </summary>
        public static Color WithValue(Color color, float value)
        {
            float current = ValueOf(color);
            if (current <= 0.0001f) return new Color(value, value, value, color.a);
            float k = value / current;
            return new Color(color.r * k, color.g * k, color.b * k, color.a);
        }

        /// <summary>
        /// 사다리의 천장 = **지금의 Stable 앰비언트 명도 그대로**.
        ///
        /// Stable 을 움직이지 않는 것이 중요하다. 7차 판정이 채택한 것은 Stable 기준의
        /// 색조 진행이고, 기준점을 같이 밀면 그 승인이 무효가 된다. 여기서 바꾸는 것은
        /// **Stable 아래 세 단계가 얼마나 어두워지는가** 하나다.
        /// </summary>
        public static float CeilingFor(Color originalAmbient, RiskProfile stable, float ambientBlend)
        {
            Color tinted = Color.Lerp(originalAmbient, stable.LightColor, Mathf.Clamp01(ambientBlend));
            return ValueOf(tinted);
        }

        /// <summary>
        /// 천장이 4단계 × <see cref="MinValueStep"/> 를 담을 만큼 높은가.
        /// 낮으면 <see cref="Build"/> 가 하한에 걸려 간격 보장을 못 지킨다 —
        /// 조용히 못 지키는 대신 부르는 쪽이 경고를 남기라고 노출한다.
        /// </summary>
        public static bool BandIsSufficient(float ceiling)
        {
            return ceiling >= HardFloor + MinValueStep * (LevelCount - 1);
        }

        /// <summary>
        /// 단계별 앰비언트 명도를 만든다.
        ///
        /// 1) 원시 값은 <see cref="RiskProfile.LightIntensity"/> 를 Stable 로 정규화해
        ///    <c>[ceiling·floorRatio, ceiling]</c> 밴드에 선형으로 얹는다. Stable 은 천장에 정확히 붙는다.
        /// 2) 그 다음 **단조 하강 + 최소 간격을 강제한다.** 데이터가 뒤집혀 있어도 화면은
        ///    뒤집히지 않는다 — 승인 대기 프리셋을 사람이 손으로 고치는 에셋이라
        ///    「값이 그렇게 들어와서」가 변명이 되면 안 된다.
        /// </summary>
        /// <param name="levels">Stable→Collapse 순 4단계. 짧으면 마지막 항목을 반복한다.</param>
        /// <param name="ceiling">Stable 의 앰비언트 명도. <see cref="CeilingFor"/>.</param>
        /// <param name="floorRatio">LightIntensity 가 0 일 때의 명도 / 천장 비율.</param>
        /// <param name="result">길이 <see cref="LevelCount"/> 이상인 출력 버퍼.</param>
        public static void Build(RiskProfile[] levels, float ceiling, float floorRatio, float[] result)
        {
            if (result == null || result.Length < LevelCount) return;

            ceiling = Mathf.Max(ceiling, HardFloor);
            floorRatio = Mathf.Clamp01(floorRatio);

            float anchor = levels != null && levels.Length > 0 ? levels[0].LightIntensity : 1f;
            if (anchor <= 0.0001f) anchor = 1f;

            for (int i = 0; i < LevelCount; i++)
            {
                float intensity = anchor;
                if (levels != null && levels.Length > 0)
                    intensity = levels[Mathf.Min(i, levels.Length - 1)].LightIntensity;

                float normalized = Mathf.Clamp01(intensity / anchor);
                result[i] = ceiling * (floorRatio + (1f - floorRatio) * normalized);
            }

            for (int i = 1; i < LevelCount; i++)
            {
                float cap = result[i - 1] - MinValueStep;
                if (result[i] > cap) result[i] = cap;
                if (result[i] < HardFloor) result[i] = HardFloor;
            }
        }

        /// <summary>배열을 새로 만드는 편의 오버로드. 테스트와 1회성 조회용이다.</summary>
        public static float[] Build(RiskProfile[] levels, float ceiling, float floorRatio)
        {
            var result = new float[LevelCount];
            Build(levels, ceiling, floorRatio, result);
            return result;
        }

        /// <summary>
        /// 인접 쌍 명도차의 **최솟값**. 음수면 어딘가에서 단계가 역전됐다는 뜻이다.
        /// 「기준점으로부터의 거리」 대신 이 값을 본다.
        /// </summary>
        public static float MinAdjacentStep(float[] ladder)
        {
            if (ladder == null || ladder.Length < 2) return 0f;
            float min = float.MaxValue;
            for (int i = 1; i < ladder.Length; i++)
                min = Mathf.Min(min, ladder[i - 1] - ladder[i]);
            return min;
        }

        /// <summary>
        /// 옛 경로 — 색상만 움직이던 앰비언트의 명도 사다리. **회귀 증인으로만 쓴다.**
        /// 새 사다리가 이것보다 나아졌다는 것을 테스트가 수치로 비교한다.
        /// </summary>
        public static float[] HueOnlyLadder(Color originalAmbient, RiskProfile[] levels, float ambientBlend)
        {
            var result = new float[LevelCount];
            float blend = Mathf.Clamp01(ambientBlend);
            for (int i = 0; i < LevelCount; i++)
            {
                RiskProfile p = levels != null && levels.Length > 0
                    ? levels[Mathf.Min(i, levels.Length - 1)]
                    : default;
                result[i] = ValueOf(Color.Lerp(originalAmbient, p.LightColor, blend));
            }
            return result;
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
