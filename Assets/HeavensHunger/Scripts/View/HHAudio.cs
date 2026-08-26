// HHAudio.cs — 사운드. 외부 음원 없이 절차적으로 만든다(에셋 의존 0).
// 크레셴도 문법: 작은 줄 → 큰 줄 → 완성형. 팝이 점점 높아지고, 완성형 직전엔 침묵한다.
using System;
using UnityEngine;

namespace HeavensHunger
{
    public class HHAudio : MonoBehaviour
    {
        public static HHAudio I;
        AudioSource _src;
        AudioClip _lever, _reelStop, _bell, _jack, _dud, _coin, _depart;
        readonly AudioClip[] _pops = new AudioClip[6];
        const int SR = 44100;

        void Awake()
        {
            I = this;
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.volume = 0.55f;

            _lever = Clunk("HH_Lever", 0.26f, 110f, 55f, 0.55f);
            _reelStop = Clunk("HH_ReelStop", 0.09f, 240f, 150f, 0.30f);
            _dud = Clunk("HH_Dud", 0.18f, 90f, 62f, 0.22f);
            _coin = Tone("HH_Coin", 0.12f, 1180f, 1560f, 0.22f, 0.004f);
            _depart = Clunk("HH_Depart", 0.55f, 70f, 38f, 0.60f);
            _bell = Bell("HH_Bell", 0.9f, 880f, 0.34f);
            _jack = Chord("HH_Jack", 1.15f, new float[] { 220f, 330f, 440f, 660f }, 0.42f);
            // 줄 팝 — 순서마다 반음씩 올라간다(크레셴도)
            for (int i = 0; i < _pops.Length; i++)
                _pops[i] = Tone("HH_Pop" + i, 0.14f, 330f * Mathf.Pow(1.122f, i), 520f * Mathf.Pow(1.122f, i), 0.26f + i * 0.03f, 0.003f);
        }

        // ── 클립 생성 ──
        static AudioClip Tone(string n, float dur, float f0, float f1, float amp, float atk)
        {
            int len = Mathf.RoundToInt(SR * dur);
            var d = new float[len];
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float f = Mathf.Lerp(f0, f1, t * t);
                ph += 2 * Math.PI * f / SR;
                float env = Mathf.Min(1f, (i / (float)SR) / Mathf.Max(0.0005f, atk)) * Mathf.Pow(1f - t, 2.2f);
                d[i] = (float)Math.Sin(ph) * env * amp;
            }
            var c = AudioClip.Create(n, len, 1, SR, false); c.SetData(d, 0); c.hideFlags = HideFlags.DontSave; return c;
        }

        static AudioClip Clunk(string n, float dur, float f0, float f1, float amp)
        {
            int len = Mathf.RoundToInt(SR * dur);
            var d = new float[len];
            var rnd = new System.Random(7);
            double ph = 0; float lp = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float f = Mathf.Lerp(f0, f1, t);
                ph += 2 * Math.PI * f / SR;
                float noise = (float)(rnd.NextDouble() * 2 - 1);
                lp = Mathf.Lerp(lp, noise, 0.12f);           // 저역 노이즈 = 쇳덩이 감촉
                float env = Mathf.Pow(1f - t, 3.0f);
                d[i] = ((float)Math.Sin(ph) * 0.7f + lp * 0.6f) * env * amp;
            }
            var c = AudioClip.Create(n, len, 1, SR, false); c.SetData(d, 0); c.hideFlags = HideFlags.DontSave; return c;
        }

        static AudioClip Bell(string n, float dur, float f, float amp)
        {
            int len = Mathf.RoundToInt(SR * dur);
            var d = new float[len];
            float[] parts = { 1f, 2.76f, 5.40f, 8.93f };
            float[] w = { 1f, 0.6f, 0.35f, 0.2f };
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float s = 0;
                for (int p = 0; p < parts.Length; p++)
                    s += Mathf.Sin(2 * Mathf.PI * f * parts[p] * (i / (float)SR)) * w[p] * Mathf.Exp(-3.2f * t * (1 + p * 0.7f));
                d[i] = s * amp * 0.35f;
            }
            var c = AudioClip.Create(n, len, 1, SR, false); c.SetData(d, 0); c.hideFlags = HideFlags.DontSave; return c;
        }

        static AudioClip Chord(string n, float dur, float[] freqs, float amp)
        {
            int len = Mathf.RoundToInt(SR * dur);
            var d = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float s = 0;
                for (int p = 0; p < freqs.Length; p++)
                {
                    float delay = p * 0.05f;                       // 아르페지오 — 쌓이며 커진다
                    float tt = (i / (float)SR) - delay;
                    if (tt <= 0) continue;
                    s += Mathf.Sin(2 * Mathf.PI * freqs[p] * tt) * Mathf.Exp(-2.4f * tt);
                }
                d[i] = s * amp * 0.3f * Mathf.Pow(1f - t, 0.6f);
            }
            var c = AudioClip.Create(n, len, 1, SR, false); c.SetData(d, 0); c.hideFlags = HideFlags.DontSave; return c;
        }

        // ── 재생 ──
        void P(AudioClip c, float v = 1f, float pitch = 1f)
        {
            if (c == null || _src == null) return;
            _src.pitch = pitch;
            _src.PlayOneShot(c, v);
        }
        public void Lever() { P(_lever, 1f); }
        public void ReelStop(int i) { P(_reelStop, 0.8f, 1f + i * 0.045f); }
        public void Pop(int i) { P(_pops[Mathf.Clamp(i, 0, _pops.Length - 1)], 0.9f); }
        public void Jackpot() { P(_jack, 1f); }
        public void Bell() { P(_bell, 0.9f); }
        public void Dud() { P(_dud, 0.7f); }
        public void Coin() { P(_coin, 0.6f); }
        public void Depart() { P(_depart, 1f); }
    }
}
