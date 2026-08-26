// HHRng.cs — 상승 원본과 동일한 결정론적 난수 (xmur3 + mulberry32)
// 같은 시드 → 같은 판. 밸런스 측정과 게임이 같은 난수를 쓴다는 원칙을 유지하기 위해 원문 그대로 이식.
using System;

namespace HeavensHunger
{
    public class HHRng
    {
        uint _a;

        public HHRng(string seed) { _a = Xmur3(seed); }
        public HHRng(uint state) { _a = state; }

        static uint Xmur3(string str)
        {
            uint h = 1779033703u ^ (uint)str.Length;
            for (int i = 0; i < str.Length; i++)
            {
                h = Mul(h ^ str[i], 3432918353u);
                h = (h << 13) | (h >> 19);
            }
            h = Mul(h ^ (h >> 16), 2246822507u);
            h = Mul(h ^ (h >> 13), 3266489909u);
            h ^= h >> 16;
            return h;
        }

        static uint Mul(uint a, uint b)
        {
            // JS Math.imul 등가
            unchecked { return (uint)((int)a * (int)b); }
        }

        /// <summary>[0,1) — mulberry32. 시뮬레이션은 다른 난수를 쓰므로 virtual.</summary>
        public virtual double NextDouble()
        {
            unchecked
            {
                _a = _a + 0x6D2B79F5u;
                uint t = _a;
                t = Mul(t ^ (t >> 15), 1u | t);
                t = (t + Mul(t ^ (t >> 7), 61u | t)) ^ t;
                return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296.0;
            }
        }

        public float Value { get { return (float)NextDouble(); } }

        /// <summary>[0,n) 정수.</summary>
        public int Range(int n) { return n <= 0 ? 0 : (int)(NextDouble() * n); }

        public bool Chance(double p) { return NextDouble() < p; }

        public uint State { get { return _a; } set { _a = value; } }
    }
}
