using System;
using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// **달성률(전력 ÷ 요구 전력)을 천장등에 싣는 값.** (`P-20260804-05` 권장안 B)
    ///
    /// ## 왜 이 파일이 생겼나
    ///
    /// 4차 독립 평가의 **유일한 2점 항목**이 「등을 돌리면 전력을 알 수 없다」였다.
    /// 여섯 고정 포즈 중 기계 벽을 등진 둘(`C_toward_gate`·`E_contract_wall`)에
    /// 전력·요구·층이 **화면에 존재하지 않는다** —
    /// `visual-criteria` B-5 #15(핵심 결과가 특정 위치에서만 보이는가) 위반이고
    /// B-3 #8(등을 돌려도 전력을 아는가)과 같은 실패다.
    ///
    /// 선택지 넷 중 채택된 것이 **B — 환경 자체가 전력을 말한다**이다.
    /// 새 오브젝트를 0개 만들고, 이미 <see cref="Risk.RiskStateView"/>가 잡고 있는
    /// 조명 채널에 얹는다. `MASTER_PRD.md` §9 「게임 상태를 공간 전체로 표현」과
    /// 같은 방향이고, HUD 를 늘리지 않으므로 `UP-FIX-89`(정보 위계 역전)를
    /// 악화시키지 않는다.
    ///
    /// ## 왜 이 값이 `DangerFeedbackProfile` 에 들어가지 않았나
    ///
    /// 그쪽은 「**위험**해 보이는 방법」이고 이쪽은 「**전력**이 얼마나 찼는가」다.
    /// 한 에셋에 두면 「연출이 약해 보인다」는 이유로 위험 사다리를 만지게 된다 —
    /// `RiskThresholdProfile` 이 `DangerFeedbackProfile` 에서 갈라져 나온 것과 같은 이유다.
    /// 두 채널은 화면에서 **서로 다투므로** 소유자도 달라야 우선순위를 말할 수 있다.
    ///
    /// ## 중립점이 r = 1.0 인 것이 이 설계의 안전장치다
    ///
    /// <see cref="Tint"/>·<see cref="IntensityScale"/>는 **r = 1 에서 정확히 항등**이다
    /// (색 보정 0, 배율 1). 「요구를 정확히 채운 상태」가 곧 지금까지의 화면이라는 뜻이고,
    /// 그래서 런이 없는 저장된 씬(<c>RiskInputs</c>가 `ratio = 1f` 로 읽는 상태)에서
    /// 고정 캡처 A~F 의 조명이 **한 화소도 바뀌지 않는다.**
    /// `VISUAL_SPEC` §12 대역 수치와 좌벽 ΔL 회귀 감시선을 건드리지 않고 채널을
    /// 하나 더 여는 유일한 방법이다.
    ///
    /// ## 색상 계열을 바꾸지 않는다
    ///
    /// 부족은 **차갑고 어둡게**, 초과는 **붉고 밝게** 간다. 초록·청록으로 가지 않는 이유는
    /// `VISUAL_SPEC` §5 가 「붉은색만 강조색」을 못 박았고, `C_toward_gate` 가 §12 대역
    /// 집계(A/C/D)에 들어 있어 색상 계열을 벗어나면 g/r·b/r 이 대역 밖으로 나가기 때문이다.
    /// 물리적으로도 맞는다 — 전원이 모자란 필라멘트는 어둡고 푸르딩딩해지고,
    /// 과전류가 걸린 필라멘트는 붉게 달아오른다.
    /// </summary>
    [CreateAssetMenu(fileName = "PowerAmbienceProfile",
                     menuName = "Ascend/Profiles/Power Ambience", order = 103)]
    public sealed class PowerAmbienceProfile : ScriptableObject
    {
        [Tooltip("어느 코드 프리셋에서 찍어 왔는가. 값을 손으로 고친 뒤에도 출처를 알 수 있게 남긴다.")]
        [SerializeField] private PowerAmbienceIntensity _sourcePreset = PowerAmbienceIntensity.Standard;

        [Tooltip("사람이 읽는 프리셋 이름. 승인 비교 자료에 그대로 실린다(PRD §14 Phase 6).")]
        [SerializeField] private string _presetName = "표준";

        [SerializeField] private PowerAmbience _values = PowerAmbience.Preset(PowerAmbienceIntensity.Standard);

        public PowerAmbienceIntensity SourcePreset => _sourcePreset;
        public string PresetName => _presetName;
        public PowerAmbience Values => _values;

        public void Reset() { ApplyPreset(PowerAmbienceIntensity.Standard); }

        /// <summary>코드 프리셋을 이 에셋에 찍어 넣는다. 프리셋 비교(PRD §14 Phase 6)의 진입점.</summary>
        public void ApplyPreset(PowerAmbienceIntensity intensity)
        {
            _sourcePreset = intensity;
            _presetName = PresetDisplayName(intensity);
            _values = PowerAmbience.Preset(intensity);
        }

        public static string PresetDisplayName(PowerAmbienceIntensity intensity)
        {
            switch (intensity)
            {
                case PowerAmbienceIntensity.Restrained: return "절제";
                case PowerAmbienceIntensity.Heavy:      return "강함";
                default:                                return "표준";
            }
        }

        /// <summary>에셋이 없으면 코드 프리셋으로 간다 — 배선이 빠져도 채널이 죽지 않는다.</summary>
        public static PowerAmbience ValuesOrDefault(PowerAmbienceProfile profile,
                                                    PowerAmbienceIntensity fallback, out string source)
        {
            if (profile != null)
            {
                source = profile.name;
                return profile.Values;
            }
            source = "코드 프리셋 " + fallback;
            return PowerAmbience.Preset(fallback);
        }
    }

    /// <summary>
    /// 달성률 → 조명 한 벌. 필드로 두는 이유는 <see cref="Risk.RiskProfile"/>과 같다 —
    /// 승인 대기 항목을 최종안 하나로 코드에 잠그지 않는다 (`VISUAL_SPEC` §11).
    /// </summary>
    [Serializable]
    public struct PowerAmbience
    {
        [Header("부족 (r = 0)")]
        [Tooltip("전력이 0 일 때 천장등이 끌려가는 색. 굶은 필라멘트 — 차갑고 탁하다.")]
        public Color StarvedColor;

        [Tooltip("전력이 0 일 때의 실내등 밝기 배율. 1 이면 부족이 어둡게 읽히지 않는다.")]
        [Min(0f)] public float StarvedIntensity;

        [Header("초과 (r = HotRatio)")]
        [Tooltip("과수확 구간에서 천장등이 끌려가는 색. 달아오른 필라멘트 — 붉고 밝다. " +
                 "붉은색은 VISUAL_SPEC §5 가 허용한 유일한 강조색이다.")]
        public Color OverdrivenColor;

        [Tooltip("초과 구간 상단의 실내등 밝기 배율. 1 보다 커야 「넘쳤다」가 밝기로 읽힌다.")]
        [Min(0f)] public float OverdrivenIntensity;

        [Tooltip("이 달성률에서 초과 색·밝기가 최대가 된다. 게이지 눈금 표(300%)와 같은 축이다.")]
        [Min(1.01f)] public float HotRatio;

        [Header("권한")]
        [Tooltip("위험이 Stable 일 때 이 채널이 등 색을 끌어당기는 최대 비율. " +
                 "1 이면 전력이 등 색을 통째로 정하고 위험 색이 지워진다 — 그건 우선순위 역전이다.")]
        [Range(0f, 1f)] public float ColorWeight;

        [Tooltip("위험이 오를수록 이 채널의 권한이 줄어드는 정도. 1 이면 Collapse 에서 " +
                 "전력 채널이 완전히 사라지고 등은 100% 위험 조명이 된다.")]
        [Range(0f, 1f)] public float RiskYield;

        [Header("임계점 돌파")]
        [Tooltip("요구 전력(100%)을 넘는 순간 등이 튀는 크기. 0 이면 점등하지 않는다. " +
                 "정지 캡처에는 잡히지 않지만 노션 03 이 「점등」을 명시했다.")]
        [Min(0f)] public float BreachFlash;

        [Tooltip("그 점등이 잦아드는 시간(초).")]
        [Min(0.01f)] public float BreachFlashDecay;

        /// <summary>
        /// **r = 1 에서 항등**인 색 보정. 부족·초과 양쪽으로만 벗어난다.
        /// 돌려주는 것은 「어디로 끌어당길 색」과 「얼마나」다 — 여기서 곱하지 않는 이유는
        /// 위험 권한을 곱하는 쪽이 <see cref="Risk.RiskStateView"/> 이기 때문이다.
        /// </summary>
        public void Tint(float ratio, out Color target, out float weight)
        {
            if (ratio < 1f)
            {
                target = StarvedColor;
                weight = ColorWeight * Mathf.Clamp01(1f - Mathf.Clamp01(ratio));
            }
            else
            {
                target = OverdrivenColor;
                float span = Mathf.Max(0.01f, HotRatio - 1f);
                weight = ColorWeight * Mathf.Clamp01((ratio - 1f) / span);
            }
        }

        /// <summary>**r = 1 에서 정확히 1.0** 인 밝기 배율.</summary>
        public float IntensityScale(float ratio)
        {
            if (ratio < 1f)
                return Mathf.Lerp(StarvedIntensity, 1f, Mathf.Clamp01(ratio));
            float span = Mathf.Max(0.01f, HotRatio - 1f);
            return Mathf.Lerp(1f, OverdrivenIntensity, Mathf.Clamp01((ratio - 1f) / span));
        }

        /// <summary>
        /// 위험 단계가 이 채널에서 가져가는 권한(0~1). 1 이면 전력 채널이 아무 일도 하지 않는다.
        /// **위험 조명이 우선**이라는 요구가 이 한 줄이다.
        /// </summary>
        public float AuthorityFor(float riskT) => Mathf.Clamp01(1f - RiskYield * Mathf.Clamp01(riskT));

        /// <summary>
        /// 프리셋 3종. `VISUAL_SPEC.md` §11 이 「2~3개의 교체 가능한 프리셋」을 요구한다.
        /// 승인 대기 항목이므로 하나로 잠그지 않는다.
        /// </summary>
        public static PowerAmbience Preset(PowerAmbienceIntensity intensity)
        {
            switch (intensity)
            {
                // 등이 거의 색만 바뀌고 밝기는 조금만 움직인다. 어두운 방을 더 어둡게
                // 만드는 것이 판독성을 해칠 수 있다는 우려에 대한 안전한 쪽 안이다.
                case PowerAmbienceIntensity.Restrained:
                    return new PowerAmbience
                    {
                        StarvedColor = new Color(0.66f, 0.72f, 0.86f),
                        StarvedIntensity = 0.78f,
                        OverdrivenColor = new Color(1.00f, 0.56f, 0.34f),
                        OverdrivenIntensity = 1.18f,
                        HotRatio = 2.20f,
                        ColorWeight = 0.45f,
                        RiskYield = 1.00f,
                        BreachFlash = 0.35f,
                        BreachFlashDecay = 0.45f,
                    };

                // 방 전체가 확실히 갈린다. 등을 돌린 화각에서 **정지 이미지로** 구분되는
                // 것이 이 항목의 통과 조건이라, 기본값을 여기에 두지 않고 Standard 를 쓴다.
                case PowerAmbienceIntensity.Heavy:
                    return new PowerAmbience
                    {
                        StarvedColor = new Color(0.48f, 0.58f, 0.86f),
                        StarvedIntensity = 0.40f,
                        OverdrivenColor = new Color(1.00f, 0.30f, 0.14f),
                        OverdrivenIntensity = 1.75f,
                        HotRatio = 2.20f,
                        ColorWeight = 0.85f,
                        RiskYield = 1.00f,
                        BreachFlash = 0.90f,
                        BreachFlashDecay = 0.55f,
                    };

                default:   // Standard
                    return new PowerAmbience
                    {
                        // 굶은 필라멘트. 청록이 아니라 **청회색**이다 — 색상 계열을
                        // 벗어나면 §12 의 g/r·b/r 이 대역 밖으로 나간다(C 가 집계에 있다).
                        StarvedColor = new Color(0.56f, 0.64f, 0.84f),
                        StarvedIntensity = 0.55f,
                        // 달아오른 필라멘트. 붉은색은 §5 가 허용한 유일한 강조색이다.
                        OverdrivenColor = new Color(1.00f, 0.40f, 0.20f),
                        OverdrivenIntensity = 1.45f,
                        // 게이지 눈금의 최대(300%)보다 낮게 둔다 — 300% 에서야 최대가 되면
                        // 실제 플레이 대부분에서 채널이 거의 안 움직인다.
                        HotRatio = 2.20f,
                        ColorWeight = 0.70f,
                        // Collapse 에서 0 이 된다. 위험 조명이 이긴다.
                        RiskYield = 1.00f,
                        BreachFlash = 0.60f,
                        BreachFlashDecay = 0.50f,
                    };
            }
        }
    }

    /// <summary>전력 환경 연출 강도. <see cref="Risk.RiskIntensity"/>와 **따로** 둔다.</summary>
    public enum PowerAmbienceIntensity
    {
        /// <summary>색 위주. 밝기는 조금만 움직인다.</summary>
        Restrained = 0,

        /// <summary>기본안.</summary>
        Standard = 1,

        /// <summary>방 전체가 확실히 갈린다.</summary>
        Heavy = 2,
    }
}
