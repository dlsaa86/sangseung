// HHSim.cs — 원본 __v2.simLever 와 동일 조건 몬테카를로.
// 목적: JS coreB 엔진과 C# 이식본이 같은 게임인지 수치로 확인한다(읽어서 비교하지 않는다).
// 원본과 같은 LCG(seed*1103515245+12345)를 쓴다 — 난수까지 같으면 비교가 깨끗하다.
using System;
using System.Collections.Generic;
using System.Text;

namespace HeavensHunger
{
    /// <summary>
    /// 원본 simLever 의 LCG — JS 의미론까지 그대로 재현한다.
    /// JS: seed=(seed*1103515245+12345)&amp;0x7fffffff
    /// seed*1103515245 는 최대 ~2^61 로 2^53 을 넘어서 **double 정밀도가 깨진다**.
    /// long 으로 정확하게 계산하면 원본과 다른 수열이 나온다 — 비교가 깨져서
    /// 일부러 double 산술 + ToInt32 를 흥내낸다. 게임 본체는 mulberry32 라 영향 없음.
    /// </summary>
    public sealed class LcgRng
    {
        double _s;
        public LcgRng(int seed) { _s = seed; }
        public double NextDouble()
        {
            double t = _s * 1103515245.0 + 12345.0;   // JS 와 동일한 IEEE754 binary64 결과
            t = Math.Truncate(t) % 4294967296.0;       // ToUint32
            if (t < 0) t += 4294967296.0;
            uint u = (uint)t;
            _s = u & 0x7fffffff;
            return _s / 2147483648.0;
        }
    }

    // HHResolver 가 HHRng 을 받으므로 LCG 를 HHRng 인터페이스로 감싸는 대신
    // 시뮬용으로 HHRng 을 상속 없이 쓰기 위해 별도 진입점을 둔다.
    public sealed class SimResult
    {
        public double Ev, Sd, Max, PZig, ZigAvgVal;
        public double P15, P30, P45, P60, Med;
        public int N;
        public override string ToString()
        {
            return string.Format("ev={0:F1} sd={1:F1} max={2:F0} pZig={3:F1}% zigAvg={4:F0} p15={5:F0} p30={6:F0} p45={7:F0} p60={8:F0} med={9:F0} n={10}",
                Ev, Sd, Max, PZig, ZigAvgVal, P15, P30, P45, P60, Med, N);
        }
    }

    public static class HHSim
    {
        /// <summary>"TOOTH:4,BONE:3,EAR:2" 또는 "HEART@2:3" 형식 파싱 — 원본 simLever 와 동일.</summary>
        public static List<PoolEntry> ParseSpec(string spec)
        {
            var o = new List<PoolEntry>();
            if (string.IsNullOrEmpty(spec)) return o;
            foreach (var kv in spec.Split(','))
            {
                var pp = kv.Trim().Split(':');
                if (pp.Length < 2) continue;
                string kk = pp[0]; int lv = 1;
                if (kk.IndexOf('@') >= 0) { var qq = kk.Split('@'); kk = qq[0]; lv = int.Parse(qq[1]); }
                int n = int.Parse(pp[1]);
                var kind = (SymKind)Enum.Parse(typeof(SymKind), kk);
                for (int m = 0; m < n; m++) o.Add(new PoolEntry(kind, lv));
            }
            return o;
        }

        /// <summary>
        /// 원본 __v2.simLever(poolSpec, eyeN, n, seed, 1, eyeKeep, devSpec, luck) 등가.
        /// 전력 = totalBase × lineMulAll × m1 × coreM  (눈 배수/outMult 제외 — 원본과 동일 범위)
        /// </summary>
        public static SimResult SimLever(string poolSpec, int eyeN, int n, int seed,
                                         bool eyeKeep = false, string devSpec = "", int luck = 0)
        {
            var pool = ParseSpec(poolSpec);
            var devs = ParseSpec(devSpec);
            var rng = new LcgRng(seed == 0 ? 12345 : seed);

            double tot = 0, tot2 = 0, mx = 0, zigValSum = 0;
            int zigLev = 0, zigTot = 0, levN = 0;
            var sums = new List<double>(n);

            for (int t = 0; t < n; t++)
            {
                // 눈은 트라이얼마다 무작위 산포 (열 몰림 병리 방지 — 원본 함정 3번)
                var eyes = new List<int>();
                while (eyes.Count < eyeN)
                {
                    int c = (int)(rng.NextDouble() * HHDial.Cells);
                    if (!eyes.Contains(c)) eyes.Add(c);
                }
                var eyesCopy = new List<int>(eyes);
                var opt = new LeverOptions { EyeKeep = eyeKeep, Devices = devs, Luck = luck };
                var R = ResolveWithLcg(pool, eyesCopy, rng, opt);
                double pw = R.TotalBase * R.LineMulAll * R.M1 * R.CoreM;
                levN++;
                int zh = 0;
                foreach (var e in R.Events) if (e.Zig) { zh++; zigValSum += e.Value; }
                if (zh > 0) zigLev++;
                zigTot += zh;
                tot += pw; tot2 += pw * pw; if (pw > mx) mx = pw;
                sums.Add(pw);

                // 뱃지 done 플래그는 매 레버 새로 만들어지므로 초기화 불필요.
                // 다만 GRIND 스택은 시뮬에서 누적시키지 않는다(원본과 동일하게 stacks=0 유지).
                foreach (var d in devs) d.Stacks = 0;
            }
            sums.Sort();
            Func<double, double> q = x => sums[Math.Min(sums.Count - 1, (int)(sums.Count * x))];
            return new SimResult
            {
                Ev = tot / n,
                Sd = Math.Sqrt(Math.Max(0, tot2 / n - (tot / n) * (tot / n))),
                Max = mx,
                PZig = zigLev / (double)Math.Max(1, levN) * 100.0,
                ZigAvgVal = zigTot > 0 ? zigValSum / zigTot : 0,
                P15 = q(0.15), P30 = q(0.30), P45 = q(0.45), P60 = q(0.60), Med = q(0.5),
                N = n
            };
        }

        // HHResolver 는 HHRng 을 받으므로, LCG 를 쓰려면 얇은 어댑터가 필요하다.
        // 여기서는 HHResolver 의 로직을 재사용하기 위해 HHRng 을 상속 대신 델리게이트로 감싼다.
        static LeverResult ResolveWithLcg(List<PoolEntry> pool, List<int> eyes, LcgRng rng, LeverOptions opt)
        {
            var adapter = new LcgHHRng(rng);
            return HHResolver.ResolveLever(pool, eyes, adapter, opt);
        }

        sealed class LcgHHRng : HHRng
        {
            readonly LcgRng _l;
            public LcgHHRng(LcgRng l) : base(1u) { _l = l; }
            public override double NextDouble() { return _l.NextDouble(); }
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            const string start = "TOOTH:4,BONE:3,EAR:2,TONGUE:1,HEART:1,BRAIN:1,LUNG:1";
            sb.AppendLine("===== HH SIM (C# 이식본) =====");
            sb.AppendLine("시작 풀 · 눈 0  : " + SimLever(start, 0, 20000, 7331));
            sb.AppendLine("시작 풀 · 눈 3  : " + SimLever(start, 3, 20000, 7331));
            sb.AppendLine("시작 풀 · 눈 5  : " + SimLever(start, 5, 20000, 7331));
            sb.AppendLine("모노 뼈13 · 눈0 : " + SimLever("BONE:13", 0, 20000, 7331));
            sb.AppendLine("순도60% 뼈8/기타5·눈0 : " + SimLever("BONE:8,TOOTH:2,EAR:1,HEART:1,BRAIN:1", 0, 20000, 7331));
            return sb.ToString();
        }
    }
}
