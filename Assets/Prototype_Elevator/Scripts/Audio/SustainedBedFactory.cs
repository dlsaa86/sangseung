using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.Audio
{
    /// <summary>
    /// 지속 위험 레이어 두 겹을 굽는다 — 저역 베드와 금속 응력음(UP-RISK-05).
    /// 오디오 파일을 쓰지 않는 이유는 <see cref="ProceduralClipFactory"/> 와 같다
    /// (라이선스가 불명확한 파일을 추가하지 않는다, `CLAUDE.md`).
    ///
    /// **왜 <see cref="ProceduralClipFactory"/> 와 한 파일이 아닌가.** 그쪽의 계약은
    /// "한 번 울리고 끝난다"이고, 그 계약이 Notion MASTER PRD §8.3 의 「사이렌은 지속
    /// 재생하지 않는다」를 **구조로** 지키는 장치다 — 그 파일이 만드는 클립은 1초를 넘지
    /// 못하고 양끝이 0 으로 페이드되어 루프로 깔 물건 자체가 없다
    /// (<see cref="AudioCueKind.Siren"/> 주석). 이음매 없이 도는 클립을 그 파일에 넣는
    /// 순간 그 보장은 파일 안에서 자기 자신과 모순된다. 그래서 여기가 따로 있다.
    ///
    /// **이것은 험이 아니다.** 위험 단계 험(고른 50/100/150Hz 톤)의 소유자는
    /// `Scripts/Risk/RiskStateView.cs` 이고 그대로 둔다 — 소유자가 둘이면 같은 험이
    /// 두 겹으로 깔린다. 여기서 만드는 것은 그 아래(30~50Hz 대의 저역)와
    /// 그 위(응력 삐걱임)라서 음역이 겹치지 않는다.
    ///
    /// **모든 파형이 루프 안전하다.** 부분음의 주파수는 전부 정수 Hz 이고 변조 주파수는
    /// 0.5Hz 의 배수라, 클립 길이 <see cref="LoopSeconds"/>=2초 동안 정확히 정수 주기를 돈다.
    /// 잡음층은 같은 길이의 주기 잡음을 **두 번 통과**시켜 필터 상태를 정상 상태로 데운 뒤
    /// 두 번째 통과분만 남긴다 — 이음매에서 상태가 튀지 않는다.
    /// 그래서 <c>ProceduralClipFactory.Polish</c> 같은 양끝 페이드를 쓰지 않는다.
    /// 페이드가 있으면 2초마다 볼륨이 꺼졌다 켜지는 맥동이 된다.
    /// </summary>
    public static class SustainedBedFactory
    {
        /// <summary>
        /// 44100 이 아닌 이유: 이 두 층의 내용이 전부 3kHz 아래다. 절반으로 구우면
        /// 상주 메모리도 절반이고, 나이키스트(11kHz)는 필요한 대역의 세 배가 넘는다.
        /// </summary>
        public const int SampleRate = 22050;

        /// <summary>
        /// 루프 한 바퀴. 짧으면 반복이 귀에 잡히고, 길면 상주 메모리가 는다.
        /// 2초는 `RiskStateView.BuildHumClip` 이 이미 쓰는 길이라 두 층의 반복 주기가 어긋난다
        /// (같으면 둘이 한 덩어리로 들린다).
        /// </summary>
        public const float LoopSeconds = 2f;

        /// <summary>단계 수. <see cref="DangerBed.LevelCount"/> 와 같아야 한다.</summary>
        public const int LevelCount = DangerBed.LevelCount;

        // key = (층 종류 << 8) | 단계. 층 종류 0 저역 / 1 응력.
        private static readonly Dictionary<int, AudioClip> Cache = new Dictionary<int, AudioClip>(8);

        /// <summary>
        /// 단계별 저역 베드 기본 주파수. 험(50/100/150Hz)보다 **아래**에 둔다 —
        /// 겹치면 두 소리가 한 덩어리로 뭉쳐서 「험이 커졌다」로만 들리고,
        /// §8.3 이 요구하는 저주파가 따로 생겼다는 사실이 사라진다.
        /// 단계가 깊을수록 낮다. 낮은 소리는 크기가 아니라 **무게**로 읽힌다.
        /// </summary>
        private static readonly int[] SubFundamental = { 43, 39, 35, 31 };

        /// <summary>저역 베드의 느린 맥동 깊이. 깊을수록 「숨쉬는」 느낌이 강해진다.</summary>
        private static readonly float[] SubBreathDepth = { 0.06f, 0.14f, 0.24f, 0.36f };

        /// <summary>
        /// 응력음 기본 주파수. 단계가 오르면 조여지다가 Collapse 에서 **내려간다** —
        /// 금속은 항복하는 순간 음정이 떨어진다. 재생 피치(<see cref="DangerBed"/>)는
        /// 단조 증가하므로 둘이 곱해져 Collapse 는 「낮은데 팽팽한」 소리가 된다.
        /// </summary>
        private static readonly int[] StressFundamental = { 150, 168, 186, 132 };

        /// <summary>위상 변조 깊이(라디안). 삐걱임의 정체다 — 음정이 미세하게 흔들린다.</summary>
        private static readonly float[] StressCreakDepth = { 0.05f, 0.15f, 0.35f, 0.55f };

        /// <summary>삐걱임의 속도(Hz). 0.5Hz 배수여야 루프가 이어진다.</summary>
        private static readonly float[] StressCreakRate = { 0.5f, 1.5f, 2.5f, 3.5f };

        /// <summary>고역 잡음(금속이 쓸리는 소리) 중심 주파수와 크기.</summary>
        private static readonly int[] StressNoiseHz = { 900, 1300, 1900, 2600 };
        private static readonly float[] StressNoiseAmp = { 0.03f, 0.09f, 0.17f, 0.24f };

        /// <summary>저역 베드 클립. 실패하면 <c>null</c>과 함께 이유를 남긴다.</summary>
        public static AudioClip Sub(int level) => Get(0, level);

        /// <summary>금속 응력음 클립.</summary>
        public static AudioClip Stress(int level) => Get(1, level);

        /// <summary>
        /// 여덟 클립을 미리 굽는다. 첫 사용 순간에 굽으면 그 프레임이 길어지는데,
        /// 하필 그 순간이 위험 단계가 오른 프레임이라 성능 캡처에 그대로 찍힌다.
        /// </summary>
        public static void Prewarm()
        {
            for (int level = 0; level < LevelCount; level++)
            {
                Sub(level);
                Stress(level);
            }
        }

        /// <summary>캐시를 버린다. 도메인 리로드 이후 파괴된 클립을 붙들고 있지 않기 위한 통로.</summary>
        public static void ClearCache() => Cache.Clear();

        /// <summary>지금 캐시에 들어 있는 클립 수. 테스트·디버그 패널이 읽는다.</summary>
        public static int CachedCount => Cache.Count;

        private static AudioClip Get(int layer, int level)
        {
            int l = Clamp(level, 0, LevelCount - 1);
            int key = (layer << 8) | l;

            AudioClip cached;
            // Unity 의 null 비교가 파괴된 객체까지 걸러 준다 — 플레이 모드를 빠져나오면
            // 런타임에 만든 클립이 파괴되는데 정적 캐시는 그 사실을 모른다.
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            int count = (int)(SampleRate * LoopSeconds);
            var samples = new float[count];

            if (layer == 0) SynthesizeSub(samples, count, l);
            else SynthesizeStress(samples, count, l);

            Normalize(samples, count, 0.85f);

            string name = (layer == 0 ? "Bed_Sub_" : "Bed_Stress_") + l;
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            Cache[key] = clip;
            return clip;
        }

        // ── 합성 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 저역 베드 — 기본음과 두 배음, 그리고 아주 느린 맥동 하나.
        /// 배음을 넣는 이유는 순수 30~40Hz 가 노트북 스피커에서 통째로 사라지기 때문이다.
        /// 배음이 있으면 작은 스피커에서도 「낮은 것이 있다」가 전달된다.
        /// </summary>
        private static void SynthesizeSub(float[] buf, int count, int level)
        {
            int f0 = SubFundamental[level];
            float depth = SubBreathDepth[level];

            // 맥동은 루프 한 바퀴에 정확히 한 번(0.5Hz). 이음매가 맥동의 골에 오므로
            // 반복 주기가 귀에 덜 잡힌다.
            const float breathHz = 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float breath = 1f + depth * Sin(breathHz * t);

                float v = Sin(f0 * t) * 1.00f
                        + Sin(f0 * 2 * t) * 0.42f
                        + Sin(f0 * 3 * t) * 0.18f;

                buf[i] += v * breath;
            }
        }

        /// <summary>
        /// 금속 응력음 — 비조화 부분음 셋에 위상 변조를 걸고 고역 잡음을 얹는다.
        ///
        /// 위상 변조가 삐걱임의 정체다. 진폭만 흔들면 「소리가 커졌다 작아졌다」로 들리고,
        /// 음정이 미세하게 흔들려야 「무언가 버티고 있다」가 된다.
        /// 정수 반송파 × 0.5Hz 배수 변조는 루프 한 바퀴에서 위상이 정확히 제자리로 온다.
        /// </summary>
        private static void SynthesizeStress(float[] buf, int count, int level)
        {
            int f0 = StressFundamental[level];
            float creak = StressCreakDepth[level];
            float rate = StressCreakRate[level];

            // 비조화 배음비. 정수배만 쓰면 악기 소리가 되고 금속으로 들리지 않는다
            // (`ProceduralClipFactory.MetalRatios` 와 같은 이유). 정수 Hz 로 반올림하는 것은
            // 루프 안전성 때문이다 — 비율이 조금 어긋나도 비조화성은 남는다.
            int f1 = Round(f0 * 2.76f);
            int f2 = Round(f0 * 5.40f);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float wobble = creak * Sin(rate * t);

                float v = SinPhase(f0 * t, wobble) * 1.00f
                        + SinPhase(f1 * t, wobble * 1.6f) * 0.38f
                        + SinPhase(f2 * t, wobble * 2.3f) * 0.16f;

                // 진폭도 함께 흔든다. 위상만 흔들면 음정 변화만 남아 신디사이저처럼 들린다.
                buf[i] += v * (1f + 0.30f * creak * Sin(rate * 0.5f * t));
            }

            AddLoopBandNoise(buf, count, StressNoiseHz[level], 2.5f,
                             StressNoiseAmp[level], 4801 + level * 97);
        }

        /// <summary>
        /// 루프 안전한 대역 잡음. 잡음 버퍼 자체가 클립 길이와 같은 주기를 가지므로,
        /// 같은 버퍼를 **두 번** 통과시키고 두 번째 통과분만 남기면 필터 상태가
        /// 이음매에서 이어진다. 한 번만 통과시키면 시작 부분에 필터가 데워지는
        /// 과도 구간이 남고, 그게 2초마다 「퍽」 하고 들린다.
        /// </summary>
        private static void AddLoopBandNoise(float[] buf, int count, float centerHz,
                                             float resonance, float amp, int seed)
        {
            if (amp <= 0f) return;

            var noise = new float[count];
            var rng = new Lcg(seed);
            for (int i = 0; i < count; i++) noise[i] = rng.Bipolar();

            var svf = new Svf(centerHz, resonance);
            for (int i = 0; i < count; i++) svf.Band(noise[i]);        // 데우는 통과 — 버린다
            for (int i = 0; i < count; i++) buf[i] += svf.Band(noise[i]) * amp;
        }

        /// <summary>
        /// 최대 진폭을 맞춘다. **양끝 페이드는 하지 않는다** — 루프이기 때문이다.
        /// 페이드를 넣으면 2초마다 소리가 꺼졌다 켜지는 맥동이 되고, 그 맥동은
        /// 「지속층」이 아니라 「반복 재생」으로 들린다.
        /// </summary>
        private static void Normalize(float[] buf, int count, float peakTarget)
        {
            float peak = 0f;
            for (int i = 0; i < count; i++)
            {
                float a = buf[i] < 0f ? -buf[i] : buf[i];
                if (a > peak) peak = a;
            }
            if (peak <= 0.0001f) return;

            float gain = peakTarget / peak;
            for (int i = 0; i < count; i++) buf[i] *= gain;
        }

        // ── 부품 ─────────────────────────────────────────────────────────────

        private static float Sin(float cycles) => Mathf.Sin(2f * Mathf.PI * cycles);

        private static float SinPhase(float cycles, float phaseOffset)
            => Mathf.Sin(2f * Mathf.PI * cycles + phaseOffset);

        private static int Round(float v) => (int)(v + 0.5f);
        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>
        /// 선형 합동 난수. <c>ProceduralClipFactory</c> 에 같은 이름의 것이 있고 **합치지
        /// 않았다** — 그쪽 <c>Svf</c> 는 44100Hz 를 상수로 물고 있어서 합치려면 샘플레이트를
        /// 인자로 빼는 리팩터가 먼저다. 이 두 부품은 각각 20줄이고, 그 리팩터는 큐 열여섯
        /// 종류의 파형을 전부 다시 굽게 만든다. Pass 1 에서 치를 값이 아니다.
        /// </summary>
        private struct Lcg
        {
            private uint _state;

            public Lcg(int seed)
            {
                _state = seed == 0 ? 2463534242u : (uint)seed;
            }

            public float Unit()
            {
                _state = _state * 1664525u + 1013904223u;
                return (_state >> 8) * (1f / 16777216f);
            }

            public float Bipolar() => Unit() * 2f - 1f;
        }

        /// <summary>Chamberlin 상태 변수 필터의 밴드패스 출력. 위와 같은 이유로 따로 있다.</summary>
        private struct Svf
        {
            private readonly float _f;
            private readonly float _q;
            private float _low;
            private float _band;

            public Svf(float cutoffHz, float resonance)
            {
                float hz = cutoffHz < 20f ? 20f
                         : (cutoffHz > SampleRate * 0.24f ? SampleRate * 0.24f : cutoffHz);
                _f = 2f * Mathf.Sin(Mathf.PI * hz / SampleRate);
                _q = 1f / (resonance < 0.5f ? 0.5f : resonance);
                _low = 0f;
                _band = 0f;
            }

            public float Band(float input)
            {
                float high = input - _low - _q * _band;
                _band += _f * high;
                _low += _f * _band;
                return _band;
            }
        }
    }
}
