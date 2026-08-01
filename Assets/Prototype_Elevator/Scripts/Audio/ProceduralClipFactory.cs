using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.Audio
{
    /// <summary>
    /// 큐 하나를 <see cref="AudioClip"/> 하나로 굽는다. **오디오 파일을 쓰지 않는다.**
    ///
    /// 왜 절차 생성인가: 유료 에셋과 라이선스가 불명확한 파일을 추가하지 않는다는 규칙
    /// (`CLAUDE.md`) 때문이다. `RiskStateView.BuildHumClip`이 같은 이유로 이미 험을
    /// 코드로 만들고 있고, 이건 그 방식을 룰렛 사운드 전체로 넓힌 것이다.
    ///
    /// 최종 사운드가 아니다. 목표는 하나뿐이다 — `MASTER_PRD.md` §18 Phase 4의
    /// "오디오만 듣는 테스트에서 위험 단계와 사건을 구분할 수 있는가"를 통과하는 것.
    /// 좋게 들리는 것이 아니라 **서로 다르게 들리는 것**이 기준이다. 그래서 종류마다
    /// 파형 자체를 바꾼다 — 같은 사인파의 피치만 바꾸면 열 종류가 한 종류로 들린다.
    ///
    /// 난수는 <see cref="Lcg"/>로 직접 돌린다. <c>UnityEngine.Random</c>은 전역 상태라
    /// 굽는 순서가 바뀌면 소리가 바뀌고, 그러면 캡처 회귀가 오디오 때문에 흔들린다.
    /// </summary>
    public static class ProceduralClipFactory
    {
        public const int SampleRate = 44100;

        /// <summary>어떤 큐도 이 길이를 넘지 않는다. 생성 비용과 상주 메모리를 위한 상한이다.</summary>
        public const float MaxSeconds = 1f;

        // (kind, variant) → 구운 클립. 같은 큐를 층마다 다시 굽지 않는다.
        private static readonly Dictionary<int, AudioClip> Cache = new Dictionary<int, AudioClip>(32);

        // 비조화 배음비. 정수배만 쓰면 악기 소리가 되고 금속으로 들리지 않는다.
        private static readonly float[] MetalRatios = { 1f, 2.76f, 5.40f, 8.93f };
        private static readonly float[] MetalAmps = { 1f, 0.52f, 0.30f, 0.16f };

        /// <summary>
        /// 큐 하나를 굽거나 캐시에서 꺼낸다. 실패하면 <c>null</c>과 함께 이유를 남긴다 —
        /// 소리가 안 나는 것과 소리가 없는 것은 다른 문제이고, 로그가 없으면 구분이 안 된다.
        /// </summary>
        public static AudioClip Bake(AudioCueKind kind, int variant)
        {
            if (kind == AudioCueKind.None)
            {
                Debug.LogWarning("[상승] 오디오 큐 None 을 구우려 했다 — 매핑 표에 빠진 사건이 있다");
                return null;
            }

            int v = NormalizeVariant(kind, variant);
            int key = ((int)kind << 8) | v;

            AudioClip cached;
            // Unity의 null 비교가 파괴된 객체까지 걸러 준다. 플레이 모드를 빠져나오면
            // 런타임에 만든 클립이 파괴되는데 정적 캐시는 그 사실을 모른다.
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            float seconds = LengthOf(kind);
            if (seconds > MaxSeconds) seconds = MaxSeconds;
            int count = (int)(SampleRate * seconds);
            if (count < 64) count = 64;

            var samples = new float[count];
            Synthesize(kind, v, samples, count);
            Polish(samples, count);

            AudioClip clip = AudioClip.Create("Cue_" + kind + "_" + v, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            Cache[key] = clip;
            return clip;
        }

        /// <summary>
        /// 쓰일 큐를 미리 전부 구워 둔다. 첫 사용 순간에 굽으면 그 프레임만 길어지는데,
        /// 하필 그 순간이 레버를 당긴 순간이라 성능 캡처에 그대로 찍힌다.
        /// </summary>
        public static void Prewarm()
        {
            Bake(AudioCueKind.LeverPull, 0);
            for (int c = 0; c < 3; c++) Bake(AudioCueKind.ColumnReveal, c);
            Bake(AudioCueKind.SoulHarvest, 0);
            Bake(AudioCueKind.PurifyScattered, 0);
            Bake(AudioCueKind.PurifyLine, 0);
            Bake(AudioCueKind.PurifyCluster, 0);
            Bake(AudioCueKind.CascadeStep, 0);
            for (int t = 0; t < 3; t++) Bake(AudioCueKind.ThresholdCrossed, t);
            Bake(AudioCueKind.ResidualDamage, 0);
            Bake(AudioCueKind.PowerBanked, 0);
            Bake(AudioCueKind.OverharvestUnlock, 0);
            Bake(AudioCueKind.OverharvestPull, 0);
            Bake(AudioCueKind.CollapseImpact, 0);
            for (int p = 0; p < 4; p++) Bake(AudioCueKind.PassengerVoice, p);
            for (int r = 0; r < 4; r++) Bake(AudioCueKind.MetalStress, r);
            for (int s = 0; s < 4; s++) Bake(AudioCueKind.Siren, s);
        }

        /// <summary>캐시를 버린다. 도메인 리로드 이후 파괴된 클립을 붙들고 있지 않기 위한 통로.</summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }

        /// <summary>지금 캐시에 들어 있는 클립 수. 테스트·디버그 패널이 읽는다.</summary>
        public static int CachedCount { get { return Cache.Count; } }

        // ── 길이와 변형 ───────────────────────────────────────────────────────

        /// <summary>
        /// 큐마다의 길이. 정화 3종은 개수 &lt; 직선 &lt; 연결 순으로 길어진다 —
        /// 사건의 크기가 곧 소리의 길이여야 `MASTER_PRD.md` §6.1의 판독 순서가 귀로도 선다.
        /// </summary>
        public static float LengthOf(AudioCueKind kind)
        {
            switch (kind)
            {
                case AudioCueKind.LeverPull:         return 0.45f;
                case AudioCueKind.ColumnReveal:      return 0.18f;
                case AudioCueKind.SoulHarvest:       return 0.22f;
                case AudioCueKind.PurifyScattered:   return 0.30f;
                case AudioCueKind.PurifyLine:        return 0.45f;
                case AudioCueKind.PurifyCluster:     return 0.70f;
                case AudioCueKind.CascadeStep:       return 0.14f;
                case AudioCueKind.ThresholdCrossed:  return 0.50f;
                case AudioCueKind.ResidualDamage:    return 0.55f;
                case AudioCueKind.PowerBanked:       return 0.50f;
                case AudioCueKind.OverharvestUnlock: return 0.60f;
                case AudioCueKind.OverharvestPull:   return 0.80f;
                case AudioCueKind.CollapseImpact:    return 0.95f;
                case AudioCueKind.PassengerVoice:    return 0.35f;
                case AudioCueKind.MetalStress:       return 0.60f;
                // 사이렌은 이 하네스가 만드는 가장 긴 소리다. 그래도 1초를 넘기지 않는다 —
                // 넘길 자리가 없는 것이 「지속 재생하지 않는다」(PRD §8.3)의 구조적 보장이다.
                case AudioCueKind.Siren:             return 0.90f;
                default:                             return 0.25f;
            }
        }

        /// <summary>변형 번호를 큐가 실제로 쓰는 범위로 조인다. 캐시가 무한히 늘지 않게 하는 장치다.</summary>
        public static int NormalizeVariant(AudioCueKind kind, int variant)
        {
            switch (kind)
            {
                case AudioCueKind.ColumnReveal:     return Clamp(variant, 0, 2);
                case AudioCueKind.ThresholdCrossed: return Clamp(variant, 0, 2);
                case AudioCueKind.PassengerVoice:   return Clamp(variant, 0, 7);
                case AudioCueKind.MetalStress:      return Clamp(variant, 0, 3);
                case AudioCueKind.Siren:            return Clamp(variant, 0, 3);
                default:                            return 0;
            }
        }

        // ── 합성 ─────────────────────────────────────────────────────────────

        private static void Synthesize(AudioCueKind kind, int variant, float[] buf, int count)
        {
            // 시드를 큐 종류에서 뽑는다. 같은 큐는 언제 구워도 같은 파형이어야
            // 캡처 회귀가 오디오 때문에 흔들리지 않는다.
            var rng = new Lcg(9001 + (int)kind * 131 + variant * 17);

            switch (kind)
            {
                case AudioCueKind.LeverPull:
                    // 무겁고 낮은 타격 + 기계가 물리는 잡음. 이 층에서 가장 큰 물리적 동작이다.
                    AddMetallic(buf, count, 178f, 9f, 1f);
                    AddNoiseTransient(buf, count, 0.018f, 0.55f, ref rng);
                    AddBandNoise(buf, count, 320f, 2.4f, 26f, 0.35f, ref rng);
                    break;

                case AudioCueKind.PowerBanked:
                    // 확정. 같은 금속이되 더 높고 짧다 — 여는 소리와 닫는 소리가 구분돼야 한다.
                    AddMetallic(buf, count, 330f, 15f, 1f);
                    AddNoiseTransient(buf, count, 0.010f, 0.4f, ref rng);
                    break;

                case AudioCueKind.ColumnReveal:
                    // 통관이 멈추며 유리질이 스치는 짧은 스윕. 열마다 목적지가 다르다.
                    AddSweep(buf, count, 780f + variant * 120f, 1450f + variant * 160f, 24f, 0.8f);
                    AddNoiseTransient(buf, count, 0.004f, 0.25f, ref rng);
                    break;

                case AudioCueKind.SoulHarvest:
                    // 맑은 종. 저항체 소리(잡음 계열)와 정면으로 다른 음색이어야
                    // §6.1 판독 순서 1번("정상 영혼과 저항체 구분")이 귀로 선다.
                    AddBell(buf, count, 690f, 13f, 1f);
                    break;

                case AudioCueKind.PurifyScattered:
                    AddPurify(buf, count, 520f, 2200f, 14f, ref rng);
                    break;

                case AudioCueKind.PurifyLine:
                    AddPurify(buf, count, 380f, 1500f, 9f, ref rng);
                    break;

                case AudioCueKind.PurifyCluster:
                    AddPurify(buf, count, 240f, 900f, 6f, ref rng);
                    // 연결 정화만 아래에 한 옥타브를 더 깐다. 셋 중 가장 큰 사건이다.
                    AddTone(buf, count, 120f, 5f, 0.5f);
                    break;

                case AudioCueKind.CascadeStep:
                    // 짧고 마른 톤. 재생 피치가 단계마다 반음씩 오른다(AudioCueTable).
                    AddTone(buf, count, 740f, 26f, 1f);
                    AddTone(buf, count, 2220f, 34f, 0.18f);
                    break;

                case AudioCueKind.ThresholdCrossed:
                    AddThreshold(buf, count, variant);
                    break;

                case AudioCueKind.ResidualDamage:
                    // 하강. 이득이 오르는 소리라면 손해는 내려가는 소리여야 한다.
                    AddSweep(buf, count, 230f, 68f, 3.4f, 1f);
                    AddSweep(buf, count, 460f, 136f, 5.0f, 0.35f);
                    AddBandNoise(buf, count, 180f, 1.6f, 6f, 0.22f, ref rng);
                    break;

                case AudioCueKind.OverharvestUnlock:
                    // 덮개가 풀리는 걸쇠 + 위로 열리는 톤. `MASTER_PRD.md` §7 "짧은 경고 연출".
                    AddMetallic(buf, count, 250f, 18f, 0.8f);
                    AddSweep(buf, count, 300f, 520f, 4.5f, 0.7f);
                    AddNoiseTransient(buf, count, 0.012f, 0.35f, ref rng);
                    break;

                case AudioCueKind.OverharvestPull:
                    // 정적을 깨는 소리. 무겁게 물리고 전부가 다시 돌기 시작한다(§7.3 5단계).
                    AddMetallic(buf, count, 110f, 5.5f, 1f);
                    AddNoiseTransient(buf, count, 0.030f, 0.7f, ref rng);
                    AddSweep(buf, count, 90f, 240f, 2.4f, 0.5f);
                    AddBandNoise(buf, count, 700f, 1.4f, 5f, 0.3f, ref rng);
                    break;

                case AudioCueKind.CollapseImpact:
                    // 저주파 임펄스 + 노이즈 테일. 층 실패가 확정된 순간이라 가장 크고 길다.
                    AddSweep(buf, count, 96f, 28f, 5.5f, 1f);
                    AddNoiseTransient(buf, count, 0.045f, 1f, ref rng);
                    AddBandNoise(buf, count, 140f, 1.2f, 3.2f, 0.45f, ref rng);
                    AddMetallic(buf, count, 84f, 3.2f, 0.55f);
                    break;

                case AudioCueKind.PassengerVoice:
                    AddVowel(buf, count, 142f, variant);
                    break;

                case AudioCueKind.MetalStress:
                    AddStress(buf, count, variant, ref rng);
                    break;

                case AudioCueKind.Siren:
                    AddSiren(buf, count, variant, ref rng);
                    break;

                default:
                    AddTone(buf, count, 440f, 12f, 0.6f);
                    break;
            }
        }

        /// <summary>정화 공통 — 대역 잡음 버스트 + 톤. 톤이 낮고 감쇠가 느릴수록 큰 정화다.</summary>
        private static void AddPurify(float[] buf, int count, float toneHz, float noiseHz,
                                      float decay, ref Lcg rng)
        {
            AddTone(buf, count, toneHz, decay, 0.85f);
            AddTone(buf, count, toneHz * 1.5f, decay * 1.3f, 0.3f);   // 완전5도 — 해소감
            AddBandNoise(buf, count, noiseHz, 2.0f, decay * 1.8f, 0.6f, ref rng);
        }

        /// <summary>임계점 — 두 음의 상승 인터벌. 위 임계점일수록 간격이 넓다.</summary>
        private static void AddThreshold(float[] buf, int count, int tier)
        {
            // 5도 → 옥타브 → 옥타브+5도. 세 임계점(100·170·300%)이 서로 다른 사건으로 들려야 한다.
            float second = tier == 0 ? 1.5f : (tier == 1 ? 2f : 3f);
            int firstLen = (int)(count * 0.45f);
            int secondStart = (int)(count * 0.38f);

            AddNoteAt(buf, count, 0, firstLen, 466f, 11f, 0.8f);
            AddNoteAt(buf, count, secondStart, count - secondStart, 466f * second, 8f, 0.9f);
        }

        /// <summary>위험 단계 전이의 금속 응력음. 단계가 깊을수록 거칠고 불규칙하다.</summary>
        private static void AddStress(float[] buf, int count, int level, ref Lcg rng)
        {
            float rough = 0.15f + 0.28f * level;
            AddMetallic(buf, count, 132f - level * 12f, 4.2f, 0.85f);

            // 삐걱거림 — 진폭이 느리게 흔들리는 저역 톤. 규칙적인 사인 하나면 알람으로 들린다.
            float baseHz = 196f - level * 18f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float wobble = 1f + rough * Mathf.Sin(2f * Mathf.PI * (5.5f + level * 2.3f) * t);
                float env = Mathf.Exp(-t * 3.6f);
                buf[i] += Mathf.Sin(2f * Mathf.PI * baseHz * t * wobble) * env * 0.5f;
            }

            if (level >= 2) AddBandNoise(buf, count, 2400f, 3.2f, 7f, 0.22f * level, ref rng);
        }

        /// <summary>
        /// 경보 사이렌(UP-RISK-05). 두 음 사이를 오가는 울림 + 확성기 잡음.
        ///
        /// **한 발이다.** 포락선이 끝에서 확실히 0으로 내려가고 길이가
        /// <see cref="MaxSeconds"/> 안에 갇힌다 — Notion MASTER PRD §8.3 의
        /// "사이렌은 지속 재생하지 않는다"를 소리 자체가 지키게 만든 것이다.
        /// 루프로 깔고 싶어도 깔 물건이 없다.
        ///
        /// 왜 순수 사인이 아닌가: 매끈한 사인 두 음의 교대는 게임 UI 알림처럼 들린다.
        /// 옥타브 배음과 대역 잡음을 얹어야 "기계에 달린 나팔"이 된다.
        /// <see cref="AddStress"/>(저역 삐걱거림)와 겹치는 음역을 피해야
        /// 무영상 청취에서 응력음과 사이렌이 한 소리로 뭉치지 않는다.
        /// </summary>
        private static void AddSiren(float[] buf, int count, int reason, ref Lcg rng)
        {
            // 넷을 서로 다르게 만든다 — 왜 울렸는지가 소리에 없으면 "경보"라는 사실만 남는다.
            //   0 단계 상승 / 1 과수확 해금 / 2 레버 결정 / 3 사고
            float lowHz = 470f + reason * 55f;
            float highHz = 930f + reason * 130f;
            float wailHz = 1.7f + reason * 0.55f;

            float seconds = count / (float)SampleRate;
            if (seconds <= 0.0001f) return;

            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;

                // 0↔1 을 오가되 끝이 둥근 곡선. 톱니로 오가면 방향이 바뀌는 지점에서 딱 소리가 난다.
                float wail = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * wailHz * t);
                float hz = lowHz + (highHz - lowHz) * wail;

                // 주파수가 변하는 사인은 위상을 적분해야 한다(AddSweep 과 같은 이유).
                phase += 2f * Mathf.PI * hz / SampleRate;

                float open = Mathf.Clamp01(t / 0.05f);
                float close = Mathf.Clamp01((seconds - t) / (seconds * 0.55f));
                float env = open * close;

                buf[i] += (Mathf.Sin(phase) * 0.80f + Mathf.Sin(phase * 2f) * 0.22f) * env;
            }

            AddBandNoise(buf, count, 1500f, 2.2f, 3.0f, 0.12f, ref rng);
        }

        /// <summary>
        /// 비언어 음성. 포먼트 두 개짜리 짧은 모음이다(UP-AUD-04).
        /// 말을 만들지 않는 이유는 `MASTER_PRD.md` §4.2가 완성형 대화를 명시적으로 제외하기
        /// 때문이다 — 반쯤 만든 대사는 없는 것보다 나쁘다.
        /// </summary>
        private static void AddVowel(float[] buf, int count, float f0, int variant)
        {
            // 변형마다 모음 색을 바꾼다. 개인차는 재생 피치가 만든다(AudioCueTable.PassengerVoice).
            float f1 = 520f + 45f * (variant % 4);
            float f2 = 980f + 190f * (variant % 4) + (variant >= 4 ? 140f : 0f);

            var band1 = new Svf(f1, 7f);
            var band2 = new Svf(f2, 9f);
            float seconds = count / (float)SampleRate;
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;

                // 성대 진동을 톱니로 근사한다. 사인은 배음이 없어 포먼트가 걸릴 게 없다.
                phase += f0 / SampleRate;
                if (phase >= 1f) phase -= 1f;
                float glottal = phase * 2f - 1f;

                // 사람의 짧은 발성 포락선 — 빠르게 열리고 천천히 닫힌다.
                float open = Mathf.Clamp01(t / (seconds * 0.14f));
                float close = Mathf.Clamp01((seconds - t) / (seconds * 0.45f));
                float env = open * close;

                buf[i] += (band1.Band(glottal) * 1f + band2.Band(glottal) * 0.55f) * env;
            }
        }

        // ── 파형 조각 ────────────────────────────────────────────────────────

        private static void AddMetallic(float[] buf, int count, float baseHz, float decay, float amp)
        {
            for (int p = 0; p < MetalRatios.Length; p++)
            {
                float hz = baseHz * MetalRatios[p];
                if (hz > SampleRate * 0.45f) continue;
                float partialAmp = amp * MetalAmps[p];
                float partialDecay = decay * (1f + p * 0.55f);   // 고배음이 먼저 죽는다
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)SampleRate;
                    buf[i] += Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * partialDecay) * partialAmp;
                }
            }
        }

        private static void AddBell(float[] buf, int count, float baseHz, float decay, float amp)
        {
            // 거의 조화롭되 살짝 어긋난 배음. 완전히 조화로우면 신디사이저처럼 들린다.
            AddTone(buf, count, baseHz, decay, amp);
            AddTone(buf, count, baseHz * 2.01f, decay * 1.4f, amp * 0.45f);
            AddTone(buf, count, baseHz * 3.02f, decay * 1.9f, amp * 0.22f);
        }

        private static void AddTone(float[] buf, int count, float hz, float decay, float amp)
        {
            if (hz > SampleRate * 0.45f) return;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                buf[i] += Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * decay) * amp;
            }
        }

        private static void AddNoteAt(float[] buf, int count, int start, int length,
                                      float hz, float decay, float amp)
        {
            if (start >= count) return;
            int end = start + length;
            if (end > count) end = count;
            for (int i = start; i < end; i++)
            {
                float t = (i - start) / (float)SampleRate;
                float value = Mathf.Sin(2f * Mathf.PI * hz * t) * 0.8f
                            + Mathf.Sin(2f * Mathf.PI * hz * 2f * t) * 0.2f;
                buf[i] += value * Mathf.Exp(-t * decay) * amp;
            }
        }

        /// <summary>주파수가 변하는 사인. 위상을 적분해야 한다 — t에 직접 곱하면 끊긴다.</summary>
        private static void AddSweep(float[] buf, int count, float fromHz, float toHz,
                                     float decay, float amp)
        {
            float phase = 0f;
            float inv = 1f / count;
            for (int i = 0; i < count; i++)
            {
                float k = i * inv;
                float hz = fromHz + (toHz - fromHz) * k;
                phase += 2f * Mathf.PI * hz / SampleRate;
                float t = i / (float)SampleRate;
                buf[i] += Mathf.Sin(phase) * Mathf.Exp(-t * decay) * amp;
            }
        }

        private static void AddNoiseTransient(float[] buf, int count, float seconds, float amp, ref Lcg rng)
        {
            int n = (int)(seconds * SampleRate);
            if (n > count) n = count;
            for (int i = 0; i < n; i++)
            {
                float k = 1f - i / (float)n;
                buf[i] += rng.Bipolar() * k * k * amp;
            }
        }

        private static void AddBandNoise(float[] buf, int count, float centerHz, float resonance,
                                         float decay, float amp, ref Lcg rng)
        {
            var svf = new Svf(centerHz, resonance);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                buf[i] += svf.Band(rng.Bipolar()) * Mathf.Exp(-t * decay) * amp;
            }
        }

        /// <summary>
        /// 정규화 + 양끝 페이드. 페이드가 없으면 클립 경계에서 딱 소리가 나는데,
        /// 그 소리는 큐마다 똑같아서 열 종류가 전부 "딱"으로 들리기 시작한다.
        /// </summary>
        private static void Polish(float[] buf, int count)
        {
            float peak = 0f;
            for (int i = 0; i < count; i++)
            {
                float a = buf[i] < 0f ? -buf[i] : buf[i];
                if (a > peak) peak = a;
            }
            if (peak > 0.0001f)
            {
                float gain = 0.9f / peak;
                for (int i = 0; i < count; i++) buf[i] *= gain;
            }

            int fadeIn = SampleRate / 1000;          // 1 ms
            int fadeOut = SampleRate * 8 / 1000;     // 8 ms
            if (fadeIn > count) fadeIn = count;
            if (fadeOut > count) fadeOut = count;

            for (int i = 0; i < fadeIn; i++) buf[i] *= i / (float)fadeIn;
            for (int i = 0; i < fadeOut; i++) buf[count - 1 - i] *= i / (float)fadeOut;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        // ── 부품 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 선형 합동 난수. <c>UnityEngine.Random</c>을 쓰지 않는 이유는 그것이 전역 상태이기
        /// 때문이다 — 다른 시스템이 시드를 건드리면 여기서 굽는 소리가 조용히 달라진다.
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

        /// <summary>
        /// Chamberlin 상태 변수 필터의 밴드패스 출력. 대역 잡음과 포먼트를 같은 부품으로 만든다.
        /// 계수 안정 범위를 벗어나면 발산하므로 컷오프를 샘플레이트의 1/4 아래로 조인다.
        /// </summary>
        private struct Svf
        {
            private readonly float _f;
            private readonly float _q;
            private float _low;
            private float _band;

            public Svf(float cutoffHz, float resonance)
            {
                float hz = cutoffHz < 20f ? 20f : (cutoffHz > SampleRate * 0.24f ? SampleRate * 0.24f : cutoffHz);
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
