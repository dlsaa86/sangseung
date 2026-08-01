using Ascend.Prototype.Risk;

namespace Ascend.Prototype.Audio
{
    /// <summary>
    /// 지속 위험 레이어 두 겹의 목표값 한 벌. 소리를 내지는 않는다.
    /// </summary>
    public readonly struct DangerBedTargets
    {
        /// <summary>저역 베드 볼륨(0~1). 접근성 저주파 배율은 아직 곱해져 있지 않다.</summary>
        public readonly float SubVolume;

        /// <summary>저역 베드 피치 배수. 위험 프로파일의 험 피치를 따라간다.</summary>
        public readonly float SubPitch;

        /// <summary>금속 응력음 볼륨(0~1). 안정 단계에서는 정확히 0 이다.</summary>
        public readonly float StressVolume;

        /// <summary>금속 응력음 피치 배수. 단계가 오를수록 조여진다.</summary>
        public readonly float StressPitch;

        /// <summary>어느 응력 파형을 쓰는가. <see cref="RiskLevel"/> 값이다.</summary>
        public readonly int StressVariant;

        public DangerBedTargets(float subVolume, float subPitch,
                                float stressVolume, float stressPitch, int stressVariant)
        {
            SubVolume = subVolume;
            SubPitch = subPitch;
            StressVolume = stressVolume;
            StressPitch = stressPitch;
            StressVariant = stressVariant;
        }

        public override string ToString() =>
            "sub v=" + SubVolume.ToString("0.###") + " p=" + SubPitch.ToString("0.##") +
            " / stress v=" + StressVolume.ToString("0.###") + " p=" + StressPitch.ToString("0.##") +
            " lv=" + StressVariant;
    }

    /// <summary>
    /// 위험이 **지속적으로** 들리는 방식. UP-RISK-05 이고 근거는 Notion MASTER PRD §8.3 —
    /// "사이렌은 지속 재생하지 않는다. 단계 상승·과수확 해금·레버 결정·사고 순간에만
    /// 강하게 사용한다." 그러면 그 사이 시간을 **무엇이** 채우는가에 답이 있어야 한다.
    /// §8.3 이 지정한 답은 저주파 · 금속 응력음 · 짧은 진동이다.
    ///
    /// **무엇이 빠져 있었나.** 사이렌이 사건 큐로 갇혀 있는 것은 이미 구조로 지켜지고
    /// 있었다(<see cref="AudioCueKind.Siren"/> 주석 · <c>AudioCueTable.TryMapSiren</c> 의 넷).
    /// 그런데 지속음은 `RiskStateView` 의 험 **하나뿐**이었고, 그것은 50/100/150Hz 를 겹친
    /// 고른 톤이라 「저주파」의 절반만 채운다. 금속 응력음은 지속 채널에 아예 없었다 —
    /// <see cref="AudioCueKind.MetalStress"/> 는 단계가 **바뀌는 순간**의 일회성 큐다.
    /// 즉 Critical 로 올라간 뒤 아무 사건도 없는 20초 동안, 소리로는 Stable 과
    /// 험 볼륨 차이밖에 남지 않았다.
    ///
    /// **왜 순수 C# 인가.** 이 항목의 검증 기준이 「오디오 채널 목록 + 무영상 청취」다
    /// (`TOPDOWN_MASTER_BACKLOG.md` UP-RISK-05). 채널 목록은 씬 없이 셀 수 있어야 하고,
    /// 「단계가 오르면 응력음이 세진다」는 귀가 아니라 눈금으로 먼저 확인돼야 한다.
    /// <see cref="SilenceWindow"/> 가 같은 이유로 같은 모양을 하고 있다.
    ///
    /// **값의 출처.** 크기와 형태는 `DangerFeedbackProfile` 의 험 값(단계별
    /// <c>HumVolume</c>/<c>HumPitch</c>)에 `AudioMixProfile` 의 배율이 곱해진 것을 받아
    /// 거기서 파생시킨다 — 두 곳에 절대값을 적으면 위험 단계를 조정할 때 지속 레이어만
    /// 옛 값에 남는다(`AudioMixProfile` 클래스 주석과 같은 이유).
    /// 아래 가중치 세 벌은 프로파일에 **대응 필드가 아직 없어서** 여기 있다. 필요한 필드는
    /// 파일 끝 메모에 적어 두었다.
    /// </summary>
    public static class DangerBed
    {
        /// <summary><see cref="RiskLevel"/> 단계 수. 가중치 배열 길이와 같아야 한다.</summary>
        public const int LevelCount = 4;

        /// <summary>
        /// 저역 베드가 험 볼륨에 곱하는 배수. Stable 에서도 0 이 아니다 —
        /// 엘리베이터는 안정 상태에서도 돌고 있고, 여기가 0 이면 위험이 오를 때
        /// 「소리가 생겼다」로 들려서 그 순간이 사건처럼 읽힌다. 지속층은 **언제나 있고
        /// 두께만 변해야** 한다.
        /// </summary>
        private static readonly float[] SubWeight = { 0.55f, 0.85f, 1.15f, 1.60f };

        /// <summary>
        /// 금속 응력음이 험 볼륨에 곱하는 배수. **Stable 은 정확히 0 이다** —
        /// 안정 상태에서 삐걱이면 그건 안정이 아니고, 응력음이 늘 깔려 있으면
        /// Strain 으로 올라간 것을 귀로 알 수 없다.
        /// </summary>
        private static readonly float[] StressWeight = { 0f, 0.45f, 0.90f, 1.25f };

        /// <summary>
        /// 응력음 피치. 험 피치를 따라가지 **않는다** — 표준 프리셋의 험 피치는
        /// 1.00 → 1.09 → 1.22 → 0.78 로 Collapse 에서 뚝 떨어지는데(`RiskProfile.Collapse`),
        /// 그 형태를 그대로 쓰면 응력음이 Collapse 에서 가장 느슨해진다. 금속은
        /// 부러지기 직전에 가장 조여 있다. 그래서 단계에 대해 단조 증가하는 별도 곡선이다.
        /// </summary>
        private static readonly float[] StressPitchByLevel = { 0.92f, 1.00f, 1.12f, 1.26f };

        /// <summary>피치 하한·상한. <see cref="AudioCueTable"/> 와 같은 이유로 조인다.</summary>
        public const float MinPitch = 0.5f;
        public const float MaxPitch = 2.0f;

        /// <summary>
        /// 지금 단계에서 지속 레이어가 내야 하는 값.
        /// </summary>
        /// <param name="level">현재 위험 단계.</param>
        /// <param name="humVolume">
        /// `AudioMixProfile` × `DangerFeedbackProfile` 이 정한 이 단계의 험 볼륨.
        /// **정적 게인은 곱해져 있지 않아야 한다** — 게인은 아래 <paramref name="gain"/> 이
        /// 받는다. 두 번 곱하면 과수확 정적에서 지속층이 제곱으로 사라져 「음소거됐나」가 된다.
        /// </param>
        /// <param name="humPitch">같은 출처의 험 피치. 저역 베드가 이것을 따라간다.</param>
        /// <param name="subScale">저역 베드 배율(씬에서 조정 가능).</param>
        /// <param name="stressScale">응력음 배율(씬에서 조정 가능).</param>
        /// <param name="gain">과수확 정적 게인(0~1). 0 이면 전부 0 이다.</param>
        public static DangerBedTargets Evaluate(RiskLevel level, float humVolume, float humPitch,
                                                float subScale, float stressScale, float gain)
        {
            int index = IndexOf(level);
            float g = Clamp01(gain);

            // 음수 험 볼륨은 데이터 실수다. 조용히 절댓값을 취하면 「소리가 왜 나지」가 되므로 0 으로 본다.
            float hum = humVolume > 0f ? humVolume : 0f;

            float sub = Clamp01(hum * SubWeight[index] * Positive(subScale) * g);
            float stress = Clamp01(hum * StressWeight[index] * Positive(stressScale) * g);

            float subPitch = Clamp(humPitch <= 0f ? 1f : humPitch, MinPitch, MaxPitch);
            float stressPitch = StressPitchByLevel[index];

            return new DangerBedTargets(sub, subPitch, stress, stressPitch, index);
        }

        /// <summary>이 단계의 저역 가중치. 테스트와 디버그 패널이 「왜 이 값인가」를 물을 때 쓴다.</summary>
        public static float SubWeightAt(RiskLevel level) => SubWeight[IndexOf(level)];

        /// <summary>이 단계의 응력 가중치.</summary>
        public static float StressWeightAt(RiskLevel level) => StressWeight[IndexOf(level)];

        /// <summary>이 단계의 응력음 피치.</summary>
        public static float StressPitchAt(RiskLevel level) => StressPitchByLevel[IndexOf(level)];

        private static int IndexOf(RiskLevel level)
        {
            int index = (int)level;
            if (index < 0) return 0;
            if (index >= LevelCount) return LevelCount - 1;
            return index;
        }

        private static float Positive(float v) => v > 0f ? v : 0f;
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}

// `DangerFeedbackProfile` 에 필요한 필드 (데이터 소유자에게 요청):
//   RiskProfile 에 단계별로 셋이 더 있어야 이 파일의 가중치 배열이 데이터로 내려간다.
//     SubVolume   — 저역 베드 볼륨(0~1). 지금은 HumVolume × SubWeight 로 파생시킨다.
//     StressVolume— 금속 응력음 볼륨(0~1). 지금은 HumVolume × StressWeight.
//     StressPitch — 응력음 피치 배수. 지금은 코드 상수(0.92/1.00/1.12/1.26).
//   `UP-RISK-07` 의 남은 문제 목록에 「§8.4 의 9항목 중 프로파일에 없는 것이 넷」이라고
//   적힌 것과 같은 성격의 부채다. 필드가 생기면 이 파일은 배열 셋을 지우고
//   `RiskProfile` 에서 읽기만 하면 된다 — Evaluate 의 시그니처는 바뀌지 않는다.
