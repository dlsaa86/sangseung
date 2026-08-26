// HHResolver.cs — coreB 레버 해석기 (RULESET 3.0)
// 원문: sangseung_proto.html §coreB · findLines / resolveLever
// "레버 = 전 칸 추첨 → 직선 3연속 지불 → 판은 그대로. 장치는 뱃지로 붙어 당첨 줄에 들면 발동."
// 판정 규칙은 유닛 검증된 것이므로 손대지 않는다.
using System;
using System.Collections.Generic;

namespace HeavensHunger
{
    /// <summary>릴 풀 한 장. ref 동일성이 중요하다(합성/낙인/포식 스택이 여기 붙는다).</summary>
    public sealed class PoolEntry
    {
        public SymKind K;
        public int Lv = 1;
        public int Stacks;    // 분쇄기 포식 스택 (+2W/스택)
        public int BrandW;    // 낙인 (영구 +1W씩)
        public PoolEntry(SymKind k, int lv = 1) { K = k; Lv = lv; }
        public PoolEntry Clone() { return new PoolEntry(K, Lv) { Stacks = Stacks, BrandW = BrandW }; }
    }

    /// <summary>판 위의 한 칸. Eye면 눈(지형 벽).</summary>
    public struct BoardCell
    {
        public bool IsEye;
        public bool Filled;
        public SymKind K;
        public int Lv;
        public PoolEntry Ref;
        public static BoardCell Eye() { return new BoardCell { IsEye = true, Filled = true }; }
        public static BoardCell Empty() { return new BoardCell(); }
        public bool IsFlesh { get { return Filled && !IsEye && HHSymbols.IsFlesh(K); } }
    }

    public sealed class LineHit
    {
        public string Name;
        public SymKind K;
        public int[] Cells;
        public int Len;
        public bool Zig;          // 꺾인 줄 5칸 완성형 = 잭팟
        public float Value;       // 이 줄이 낸 W (변압기 반영 후)
        public List<BadgeSlot> Badges = new List<BadgeSlot>();
    }

    public sealed class BadgeSlot
    {
        public int Cell;
        public PoolEntry Dev;
        public bool Done;   // 레버당 1회 발동 플래그
        public bool Hit;
    }

    public sealed class LeverOptions
    {
        public bool EyeKeep;                      // 눈 배수 1.5+ 빌드 → 줄이 눈을 갈지 않는다
        public List<PoolEntry> Devices = new List<PoolEntry>();
        public int Luck;
        public float LvH, LvV, LvD;               // 부품/승객이 주는 줄값 보정
        public Dictionary<SymKind, float> DrawW;  // 아이템 가중
        public float[] SymBonus;                  // symbol value bonus per kind (7)
        public float SvHiX = 1f;                  // organ (HEART/BRAIN/LUNG) value multiplier
        public float ItemM1;                      // 아이템 레버 배율 합연산
    }

    public sealed class LeverResult
    {
        public BoardCell[] Board;
        public List<LineHit> Events = new List<LineHit>();
        public List<BadgeSlot> Badges = new List<BadgeSlot>();
        public float TotalBase;
        public float M1 = 1f;
        public float CoreM = 1f;
        public int CoreOn;
        public int Bursts;          // 성립한 줄 수
        public int Grinds;          // 당첨 줄이 갈아낸 눈
        public int BurnEyes;        // 소각로가 태운 눈
        public int BurnN;           // 소각 용량(레벨 합)
        public float BurnW;
        public int LidN;            // 눈꺼풀 감김 수
        public PoolEntry GrindDev;
        public float LineMulAll = 1f;
    }

    public static class HHResolver
    {
        public const int C = HHDial.BoardCols;   // 5
        public const int R = HHDial.BoardRows;   // 3
        public const int N = HHDial.Cells;       // 15

        // ── 페이라인: 고전 9라인 (열마다 행 인덱스) ──
        static readonly int[][] PaylineRows = new int[][]
        {
            new[]{1,1,1,1,1},  // 중단
            new[]{0,0,0,0,0},  // 상단
            new[]{2,2,2,2,2},  // 하단
            new[]{0,1,2,1,0},  // 브이
            new[]{2,1,0,1,2},  // 산
            new[]{1,0,0,0,1},  // 지붕
            new[]{1,2,2,2,1},  // 골짜기
            new[]{0,0,1,2,2},  // 내리막
            new[]{2,2,1,0,0},  // 오르막
        };
        public static readonly string[] LineNames = { "중단", "상단", "하단", "브이", "산", "지붕", "골짜기", "내리막", "오르막" };

        public static readonly int[][] Paylines = BuildPaylines();
        static int[][] BuildPaylines()
        {
            var o = new int[PaylineRows.Length][];
            for (int i = 0; i < PaylineRows.Length; i++)
            {
                o[i] = new int[C];
                for (int c = 0; c < C; c++) o[i][c] = PaylineRows[i][c] * C + c;
            }
            return o;
        }

        // 대각 6방향 (3연속)
        public static readonly int[][] Diags =
        {
            new[]{0,6,12}, new[]{1,7,13}, new[]{2,8,14},
            new[]{10,6,2}, new[]{11,7,3}, new[]{12,8,4}
        };

        /// <summary>4연속 ×2 · 5연속 ×4.</summary>
        public static float LenMul(int n) { return n >= 5 ? 4f : n == 4 ? 2f : 1f; }
        /// <summary>합성 레벨 배수 2^(lv-1).</summary>
        public static float Sc2(int lv) { return (float)Math.Pow(2, Math.Max(1, lv) - 1); }

        public static int[] AdjOf(int i)
        {
            int r = i / C, c = i % C;
            var a = new List<int>(4);
            if (r > 0) a.Add(i - C);
            if (r < R - 1) a.Add(i + C);
            if (c > 0) a.Add(i - 1);
            if (c < C - 1) a.Add(i + 1);
            return a.ToArray();
        }

        /// <summary>칸의 실효 W = 기본값 × 2^(합성-1) + 포식 스택×2 + 낙인.</summary>
        public static bool IsOrgan(SymKind k)
        {
            return k == SymKind.HEART || k == SymKind.BRAIN || k == SymKind.LUNG;
        }

        public static float SymVal(BoardCell cell)
        {
            if (!cell.Filled || cell.IsEye) return 0;
            var d = HHSymbols.Get(cell.K);
            float v = d.Val * Sc2(cell.Lv);
            if (cell.Ref != null) { v += 2 * cell.Ref.Stacks; v += cell.Ref.BrandW; }
            return v;
        }

        static BoardCell DrawCell(List<PoolEntry> pool, HHRng rng, Dictionary<SymKind, float> wm)
        {
            PoolEntry e;
            if (wm == null || wm.Count == 0)
            {
                e = pool[rng.Range(pool.Count)];
            }
            else
            {
                float tot = 0;
                for (int i = 0; i < pool.Count; i++) { float w; tot += wm.TryGetValue(pool[i].K, out w) ? w : 1f; }
                double r = rng.NextDouble() * tot;
                e = pool[pool.Count - 1];
                for (int i = 0; i < pool.Count; i++)
                {
                    float w; r -= wm.TryGetValue(pool[i].K, out w) ? w : 1f;
                    if (r < 0) { e = pool[i]; break; }
                }
            }
            return new BoardCell { Filled = true, IsEye = false, K = e.K, Lv = e.Lv, Ref = e };
        }

        /// <summary>
        /// 기본 = 직선 3연속(가로 행내 어디든 · 세로 5열 · 대각 6방향)
        /// 희귀 = 꺾인 줄 6종 5칸 완성 → 잭팟
        /// 중복 정책: 같은 줄 하위 조각 흡수(5연속=1회) · 다른 모양 간 중복 지불
        /// </summary>
        public static List<LineHit> FindLines(BoardCell[] board)
        {
            var hits = new List<LineHit>();
            Func<int, SymKind?> flesh = (i) =>
            {
                var c = board[i];
                return c.IsFlesh ? (SymKind?)c.K : null;
            };

            // 가로 3줄 — 행 안에서 연속 런을 찾는다 (하위 조각 흡수)
            for (int li = 0; li < 3; li++)
            {
                var P = Paylines[li];
                int col = 0;
                while (col < C)
                {
                    var k0 = flesh(P[col]);
                    if (k0 == null) { col++; continue; }
                    var run = new List<int> { P[col] };
                    int c2 = col + 1;
                    while (c2 < C && flesh(P[c2]) == k0) { run.Add(P[c2]); c2++; }
                    if (run.Count >= 3)
                        hits.Add(new LineHit { Name = LineNames[li] + "줄", K = k0.Value, Cells = run.ToArray(), Len = run.Count, Zig = false });
                    col = c2;
                }
            }
            // 세로 5열
            for (int vc = 0; vc < C; vc++)
            {
                var vk = flesh(vc);
                if (vk != null && flesh(vc + C) == vk && flesh(vc + 2 * C) == vk)
                    hits.Add(new LineHit { Name = "세로줄", K = vk.Value, Cells = new[] { vc, vc + C, vc + 2 * C }, Len = 3, Zig = false });
            }
            // 대각 6방향
            foreach (var D in Diags)
            {
                var dk = flesh(D[0]);
                if (dk != null && flesh(D[1]) == dk && flesh(D[2]) == dk)
                    hits.Add(new LineHit { Name = "대각줄", K = dk.Value, Cells = (int[])D.Clone(), Len = 3, Zig = false });
            }
            // 꺾인 줄 완성형 = 잭팟
            for (int lj = 3; lj < Paylines.Length; lj++)
            {
                var Q = Paylines[lj];
                var k2 = flesh(Q[0]);
                bool ok = k2 != null;
                for (int c3 = 1; c3 < C && ok; c3++) if (flesh(Q[c3]) != k2) ok = false;
                if (ok)
                    hits.Add(new LineHit { Name = LineNames[lj] + " 완성", K = k2.Value, Cells = (int[])Q.Clone(), Len = 5, Zig = true });
            }
            return hits;
        }

        /// <summary>레버 한 번. 순수 함수 — 게임과 측정이 같이 쓴다.</summary>
        public static LeverResult ResolveLever(List<PoolEntry> pool, List<int> eyes, HHRng rng, LeverOptions opt)
        {
            opt = opt ?? new LeverOptions();
            var res = new LeverResult();
            var eyeSet = new HashSet<int>(eyes);

            var board = new BoardCell[N];
            for (int i = 0; i < N; i++)
                board[i] = eyeSet.Contains(i) ? BoardCell.Eye() : DrawCell(pool, rng, opt.DrawW);

            // ── 운: 가로 2연속의 세 번째 칸에 판 위 같은 문양을 끌어와 앉힌다 ──
            int LK = Math.Min(9, Math.Max(0, opt.Luck));
            for (int lk = 0; lk < LK; lk++)
            {
                if (rng.NextDouble() >= HHDial.LuckSeatP) continue;
                bool seated = false;
                for (int lr = 0; lr < 3 && !seated; lr++)
                {
                    var PR = Paylines[lr];
                    for (int lc = 0; lc < 3 && !seated; lc++)
                    {
                        int aC = PR[lc], bC = PR[lc + 1], tC = PR[lc + 2];
                        var cA = board[aC]; if (!cA.IsFlesh) continue;
                        var cB = board[bC]; if (!cB.Filled || cB.IsEye || cB.K != cA.K) continue;
                        var cT = board[tC]; if (cT.IsEye) continue;
                        if (cT.Filled && cT.K == cA.K) continue;
                        for (int dn = 0; dn < N && !seated; dn++)
                        {
                            if (dn == aC || dn == bC || dn == tC) continue;
                            var cD = board[dn];
                            if (!cD.Filled || cD.IsEye || cD.K != cA.K) continue;
                            board[tC] = cD; board[dn] = cT; seated = true;
                        }
                    }
                }
                if (!seated) break;
            }

            // ── 뱃지 부착: 보유 장치마다 35%로 심볼 칸에 붙는다 ──
            var badges = res.Badges;
            for (int d = 0; d < opt.Devices.Count; d++)
            {
                if (rng.NextDouble() >= HHDial.DeviceBadgeRate) continue;
                var free = new List<int>();
                for (int i = 0; i < N; i++)
                {
                    if (eyeSet.Contains(i)) continue;
                    bool taken = false;
                    for (int b = 0; b < badges.Count; b++) if (badges[b].Cell == i) { taken = true; break; }
                    if (!taken) free.Add(i);
                }
                if (free.Count > 0)
                    badges.Add(new BadgeSlot { Cell = free[rng.Range(free.Count)], Dev = opt.Devices[d] });
            }

            var hits = FindLines(board);
            float m1 = 1f + opt.ItemM1;
            int ampTrig = 0, burnN = 0, lidN = 0;
            var coreTrig = new List<PoolEntry>();

            foreach (var h in hits)
            {
                float v = 0;
                for (int x = 0; x < h.Cells.Length; x++)
                {
                    var bc = board[h.Cells[x]];
                    float sv = SymVal(bc);
                    if (opt.SymBonus != null && bc.Filled && !bc.IsEye)
                    {
                        int ki = (int)bc.K;
                        if (ki >= 0 && ki < opt.SymBonus.Length) sv += opt.SymBonus[ki];
                    }
                    if (opt.SvHiX != 1f && bc.Filled && !bc.IsEye && IsOrgan(bc.K)) sv *= opt.SvHiX;
                    v += sv;
                }
                v += (h.Zig || h.Name == "대각줄") ? opt.LvD : (h.Name == "세로줄" ? opt.LvV : opt.LvH);
                v *= LenMul(h.Len);

                float lineTr = 1f;
                foreach (var b in badges)
                {
                    if (Array.IndexOf(h.Cells, b.Cell) < 0) continue;
                    h.Badges.Add(b);
                    var dk = b.Dev.K; float sc = Sc2(b.Dev.Lv);
                    if (dk == SymKind.TRANS) lineTr = Math.Max(lineTr, 1f + 0.3f * sc);   // 변압기만 줄마다
                    else if (!b.Done)                                                     // 나머지는 레버당 1회
                    {
                        if (dk == SymKind.CAP) m1 += 0.5f * sc;
                        else if (dk == SymKind.AMP) ampTrig += b.Dev.Lv;
                        else if (dk == SymKind.CORE) coreTrig.Add(b.Dev);
                        else if (dk == SymKind.FURN) burnN += (int)sc;
                        else if (dk == SymKind.LID) lidN += b.Dev.Lv;
                        else if (dk == SymKind.GRIND) { res.GrindDev = b.Dev; v += 2 * b.Dev.Stacks; }
                        b.Done = true;
                    }
                    b.Hit = true;
                }

                h.Value = v * lineTr;
                res.TotalBase += h.Value;
                res.Events.Add(h);

                // 당첨 줄 인접의 눈이 갈린다
                if (!opt.EyeKeep)
                {
                    for (int x = 0; x < h.Cells.Length; x++)
                    {
                        foreach (int j in AdjOf(h.Cells[x]))
                        {
                            if (board[j].IsEye)
                            {
                                board[j] = BoardCell.Empty();
                                res.Grinds++;
                                eyes.Remove(j);
                                eyeSet.Remove(j);
                            }
                        }
                    }
                }
            }

            if (ampTrig >= 2) m1 += 0.5f * ampTrig;
            else if (ampTrig > 0) m1 += 0.2f * ampTrig;

            // 소각로: 눈을 태워 전력으로 (제거 + 전환. 폭발합에 가산되므로 코어 임계에도 든다)
            int burned = 0;
            while (burned < burnN && eyes.Count > 0)
            {
                int bE = eyes[0]; eyes.RemoveAt(0);
                if (board[bE].IsEye) board[bE] = BoardCell.Empty();
                eyeSet.Remove(bE); burned++;
            }
            if (burned > 0) { res.BurnEyes = burned; res.BurnW = HHDial.FurnaceW * burned; res.TotalBase += res.BurnW; }

            res.LineMulAll = hits.Count >= 2 ? (1f + HHDial.MultiLineStep * (hits.Count - 1)) : 1f;

            foreach (var cd in coreTrig)
                if (res.TotalBase >= HHDial.CoreThreshold) { res.CoreM *= 2f + 0.5f * (cd.Lv - 1); res.CoreOn++; }

            res.Board = board;
            res.M1 = m1;
            res.Bursts = hits.Count;
            res.BurnN = burnN;
            res.LidN = lidN;
            return res;
        }
    }
}
