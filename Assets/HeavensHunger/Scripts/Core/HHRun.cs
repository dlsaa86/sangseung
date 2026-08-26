// HHRun.cs — 런 상태 + 레버/출발/상점/승객/설비/거래/연쇄.
// coreB doSpin·arrive 이식 + 원본 mods 파이프(승객 23 · 설비 151 · 거래 24 · 연쇄) 복원.
//   레버 = 충전식 탱크(상한 5) · 당첨 레버 +1닢 · 출발 시 남긴 레버 누진(1·2·3·5·8)
//   점프 = 상한(5 + 층×0.15 + 부품) × 겹침(floor(전력/문턱), 최대 4)
using System;
using System.Collections.Generic;

namespace HeavensHunger
{
    public sealed class ActiveItemSlot
    {
        public string Id;
        public int Chg;
        public int MaxChg;
    }

    public sealed class SpinReport
    {
        public LeverResult R;
        public int Power;
        public float EyePow;
        public int BellAdd;
        public int GaugeNeed;
        public List<int> BellCells = new List<int>();
        public List<int> BellPopped = new List<int>();
        public int CoinsGained;
        public int LuckUsed;
        public bool BellRing;
        public string GrindLog;
        public readonly List<string> ChainLog = new List<string>();
    }

    public sealed class HHRun
    {
        public string Seed;
        public HHRng Rng;

        // ── 진행 ──
        public int StopIdx;
        public int FloorNow = 1;
        public int Power;
        public int SpinsUsed;
        public int BonusSpins;
        public int Coins;
        public bool Dead;
        public bool Finished;
        public int StopLines;              // 이 정차에서 세운 줄 수 (복리 재료)
        public float OutMult = 1f;         // 영구 전력 배수
        public int JumpCapBonus;           // 거래로 얻은 상승 한계

        // ── 판 ──
        public BoardCell[] Board = new BoardCell[HHDial.Cells];
        public List<int> Eyes = new List<int>();

        // ── 릴 풀 / 장치 / 아이템 ──
        public List<PoolEntry> Pool = new List<PoolEntry>();
        public List<PoolEntry> Devices = new List<PoolEntry>();
        public Dictionary<string, int> ItemStacks = new Dictionary<string, int>();
        public List<ActiveItemSlot> Actives = new List<ActiveItemSlot>();
        public int SwapLeft;
        public int TempLuck;
        public int MissStreak;

        // ── 명부 / 설비 / 화물 ──
        public readonly List<AboardPassenger> Aboard = new List<AboardPassenger>();
        public readonly List<PartDef> OwnedParts = new List<PartDef>();
        public readonly List<CargoItem> Cargo = new List<CargoItem>();
        public readonly HashSet<string> Delivered = new HashSet<string>();
        public readonly HashSet<string> Killed = new HashSet<string>();
        public ModBag Mods = new ModBag();
        public const int AboardCap = 4;
        public const int PartHoldCap = 8;

        // ── 거래 / 내기 ──
        public DealCarry Carry = new DealCarry();
        public BetSlip Bet;
        public StopOffers Offers = new StopOffers();

        // ── 종 ──
        public int BellGauge;
        public int GaugeRings;
        public int BellRingTier;
        public int BellsTotal;

        // ── 상점 진열 ──
        public List<SymKind?> SymShop = new List<SymKind?>();
        public List<string> ItemShop = new List<string>();
        public List<string> PartShop = new List<string>();
        int _shopStop = -1;
        int _uHStop = -1;
        int _lastPaxStop = -99;
        int _brandStop = -1;
        int _brandLeft;
        bool _sgunShown;
        float _chainPowPct;

        public SpinReport Last;
        public List<BadgeSlot> LastBadges = new List<BadgeSlot>();
        public List<int> LastBellCells = new List<int>();
        public readonly List<string> Log = new List<string>();

        public HHRun(string seed)
        {
            HHContent.EnsureLoaded();
            Seed = seed;
            Rng = new HHRng(seed);
            // 설계자 확정 2026-08-25: 처음부터 7종 — 어금니4·뼈3·귀2·혀1·심장1·뇌1·폐1 (13개)
            AddPool(SymKind.TOOTH, 4); AddPool(SymKind.BONE, 3); AddPool(SymKind.EAR, 2);
            AddPool(SymKind.TONGUE, 1); AddPool(SymKind.HEART, 1); AddPool(SymKind.BRAIN, 1); AddPool(SymKind.LUNG, 1);
            Devices.Add(new PoolEntry(SymKind.CAP));
            for (int i = 0; i < Board.Length; i++) Board[i] = BoardCell.Empty();
            RecomputeMods();
            RefreshShop(true);
        }

        void AddPool(SymKind k, int n) { for (int i = 0; i < n; i++) Pool.Add(new PoolEntry(k)); }
        void L(string s) { if (string.IsNullOrEmpty(s)) return; Log.Add(s); if (Log.Count > 250) Log.RemoveAt(0); }
        public void LogLine(string s) { L(s); }

        // ── 파생값 ──
        public float TotalWeight
        {
            get
            {
                float w = Mods.selfW;
                foreach (var a in Aboard) w += a.W;
                foreach (var c in Cargo) w += c.W;
                return w;
            }
        }

        public void RecomputeMods()
        {
            Mods = HHModsCalc.Compute(OwnedParts, Aboard, 0f);      // 1패스: selfW 확보
            Mods = HHModsCalc.Compute(OwnedParts, Aboard, TotalWeight);
        }

        public int TargetPower { get { return HHDial.TargetOfStop(StopIdx, FloorNow); } }

        /// <summary>실효 문턱 = 목표 × (1 + 0.05×무게세×무게) × 거래이월 × 부품문턱배수.</summary>
        public int EffReq
        {
            get
            {
                float wc = 0.05f * Mods.wcMul;
                float v = TargetPower * (1f + wc * TotalWeight) * Carry.ReqMul * Mods.reqMul;
                return (int)Math.Round(v);
            }
        }

        public int LeverLimit
        {
            get
            {
                int L2 = HHDial.LeverTank + BonusSpins + (int)Mods.spinCapD;
                if (Carry.SpinCap > 0) L2 = Math.Min(L2, Carry.SpinCap);
                L2 = Math.Max(2, L2);
                return Math.Min(L2, SpinsUsed + HHDial.LeverTank);
            }
        }
        public int LeversLeft { get { return Math.Max(0, LeverLimit - SpinsUsed); } }

        /// <summary>눈 배수. 설계자 지시(2026-08-25)로 부품 기여는 기본 꺼져 있다 — HHDial.EyeMultFromMods.</summary>
        public float EyeMultBase
        {
            get
            {
                float m = HHDial.EyeBase + (HHDial.EyeMultFromMods ? Mods.eyeMult : 0f);
                return Math.Min(3f, Math.Max(1f, m));
            }
        }

        public int JumpCap
        {
            get
            {
                float baseCap = HHDial.JumpCapBase + Mods.jumpCap + JumpCapBonus;
                float rel = (float)Math.Floor(Math.Max(1, FloorNow) * HHDial.JumpCapRel);
                return Math.Max(1, (int)Math.Round((baseCap + rel) * Mods.jumpCapMul));
            }
        }
        public int Wraps
        {
            get
            {
                double r = Math.Max(0, Power) / (double)Math.Max(1, EffReq);
                return Math.Max(1, Math.Min(HHDial.WrapMax, (int)Math.Floor(r)));
            }
        }
        public int JumpPreview { get { return JumpCap * Wraps; } }
        public bool CanDepart { get { return Power >= EffReq; } }
        public int GaugeNeed
        {
            get { return Math.Max(1, Math.Min(HHDial.GaugeNeedMax, HHDial.GaugeNeed + GaugeRings + (int)Mods.gaugeNeedD)); }
        }

        // ── 아이템 파생 ──
        public float ItemM1
        {
            get
            {
                float m = 0;
                foreach (var it in HHItems.All)
                { int n; if (it.M1Add != 0 && ItemStacks.TryGetValue(it.Id, out n)) m += it.M1Add * n; }
                return m;
            }
        }
        public int ItemLuck
        {
            get
            {
                int l = 0;
                foreach (var it in HHItems.All)
                { int n; if (it.LuckAdd != 0 && ItemStacks.TryGetValue(it.Id, out n)) l += it.LuckAdd * n; }
                return l;
            }
        }

        public Dictionary<SymKind, float> DrawWeights()
        {
            var m = new Dictionary<SymKind, float>();
            var cnt = new Dictionary<SymKind, int>();
            foreach (var e in Pool)
            {
                if (!cnt.ContainsKey(e.K)) cnt[e.K] = 0;
                cnt[e.K]++;
                if (!m.ContainsKey(e.K)) m[e.K] = 1f;
            }
            foreach (var it in HHItems.All)
            {
                int n;
                if (it.IsRate && it.RateTarget.HasValue && ItemStacks.TryGetValue(it.Id, out n) && m.ContainsKey(it.RateTarget.Value))
                    m[it.RateTarget.Value] *= (float)Math.Pow(1.4, n);
            }
            if (SwapLeft > 0 && m.Count > 1)
            {
                SymKind hi = default(SymKind), lo = default(SymKind); bool first = true;
                foreach (var kv in m)
                {
                    float share = cnt[kv.Key] * kv.Value;
                    if (first) { hi = lo = kv.Key; first = false; continue; }
                    if (share > cnt[hi] * m[hi]) hi = kv.Key;
                    if (share < cnt[lo] * m[lo]) lo = kv.Key;
                }
                if (!hi.Equals(lo))
                {
                    float whi = m[hi], wlo = m[lo];
                    m[hi] = cnt[lo] * wlo / Math.Max(1, cnt[hi]);
                    m[lo] = cnt[hi] * whi / Math.Max(1, cnt[lo]);
                }
            }
            return m;
        }

        public Dictionary<SymKind, float> DrawProbabilities()
        {
            var w = DrawWeights();
            var eff = new Dictionary<SymKind, float>();
            float tot = 0;
            foreach (var e in Pool)
            {
                float ww; if (!w.TryGetValue(e.K, out ww)) ww = 1f;
                if (!eff.ContainsKey(e.K)) eff[e.K] = 0;
                eff[e.K] += ww; tot += ww;
            }
            var o = new Dictionary<SymKind, float>();
            foreach (var kv in eff) o[kv.Key] = tot > 0 ? kv.Value / tot : 0;
            return o;
        }

        int CalcLuck()
        {
            int ov = 0;
            if (StopIdx <= HHDial.IlsStops)
                if (Array.IndexOf(HHDial.IlsPlan, SpinsUsed + 1) >= 0) ov = HHDial.IlsLuck;
            if (MissStreak >= HHDial.PityFrom)
                ov = Math.Max(ov, HHDial.PityStep * (MissStreak - HHDial.PityFrom + 1));
            ov = Math.Min(4, ov);
            int lowOnly = ov >= 1 ? (int)Mods.luckLow : 0;
            return Math.Min(9, ov + TempLuck + ItemLuck + (int)Mods.luck + lowOnly + Carry.Luck);
        }

        public void PlantEye()
        {
            if (Eyes.Count >= HHDial.EyeMaxN) return;
            var free = new List<int>();
            for (int q = 0; q < HHDial.Cells; q++) if (!Eyes.Contains(q)) free.Add(q);
            if (free.Count <= 1) return;
            int c = free[Rng.Range(free.Count)];
            Eyes.Add(c);
            if (Board[c].Filled) Board[c] = BoardCell.Eye();
        }

        // ── 연쇄 ──
        void FireChain(string evt, SpinReport rep)
        {
            var chains = new List<ChainDef>();
            foreach (var p in OwnedParts) if (p.Ch != null && p.Ch.On == evt) chains.Add(p.Ch);
            foreach (var a in Aboard) if (a.Def.Ch != null && a.Def.Ch.On == evt) chains.Add(a.Def.Ch);
            foreach (var ch in chains)
            {
                switch (ch.Fx)
                {
                    case "purge":
                        if (Eyes.Count > 0)
                        {
                            int c = Eyes[0]; Eyes.RemoveAt(0);
                            if (Board[c].IsEye) Board[c] = BoardCell.Empty();
                            if (rep != null) rep.ChainLog.Add("연쇄 — 눈 하나가 지워졌다");
                        }
                        break;
                    case "plant": PlantEye(); if (rep != null) rep.ChainLog.Add("연쇄 — 눈이 하나 심겼다"); break;
                    case "out": OutMult += ch.V; if (rep != null) rep.ChainLog.Add("연쇄 — 전력 배수 +" + ch.V.ToString("0.##")); break;
                    case "pow": _chainPowPct += ch.V; if (rep != null) rep.ChainLog.Add("연쇄 — 이번 판 전력 +" + (ch.V * 100).ToString("0") + "%"); break;
                    case "bellg": BellGauge += (int)ch.V; if (rep != null) rep.ChainLog.Add("연쇄 — 벨 +" + ch.V); break;
                    case "spin": BonusSpins += (int)ch.V; if (rep != null) rep.ChainLog.Add("연쇄 — 레버 +" + ch.V); break;
                    case "tick": Coins += (int)ch.V; if (rep != null) rep.ChainLog.Add("연쇄 — 동전 +" + ch.V); break;
                    case "luck": TempLuck += (int)ch.V; if (rep != null) rep.ChainLog.Add("연쇄 — 운 +" + ch.V); break;
                    case "notch":
                        {
                            PoolEntry best = null;
                            foreach (var e in Pool) if (best == null || e.BrandW < best.BrandW) best = e;
                            if (best != null) { best.BrandW += (int)ch.V; if (rep != null) rep.ChainLog.Add("연쇄 — " + HHSymbols.Get(best.K).Name + " 새김 +" + ch.V); }
                            break;
                        }
                    default: break;
                }
            }
        }

        // ── 레버 ──
        public SpinReport PullLever()
        {
            if (Dead || Finished) return null;
            if (SpinsUsed >= LeverLimit) return null;
            _chainPowPct = 0f;

            // 눈 출현 (강설) — 부품/승객의 eyeW 가 확률을 민다
            if (FloorNow >= HHDial.EyeFromFloor && Eyes.Count < HHDial.EyeMaxN)
            {
                double p = HHDial.SnowBase + HHDial.SnowStep * Math.Max(0, StopIdx - 3) + Mods.eyeW * 0.5;
                p = Math.Max(0, Math.Min(HHDial.SnowMax, p));
                if (Rng.NextDouble() < p) { PlantEye(); FireChain("eyeBorn", null); }
            }

            bool eyeKeep = EyeMultBase >= HHDial.GrindGateAt;
            int luck = CalcLuck();

            if (ItemStacks.ContainsKey("uH") && _uHStop != StopIdx)
            { _uHStop = StopIdx; BonusSpins++; L("🎩 기관사의 습관 — 첫 레버를 되감았다 (+1)"); }

            var opt = new LeverOptions
            {
                EyeKeep = eyeKeep,
                Devices = Devices,
                Luck = luck,
                DrawW = DrawWeights(),
                ItemM1 = ItemM1,
                LvH = Mods.lvH,
                LvV = Mods.lvV,
                LvD = Mods.lvD,
                SymBonus = Mods.sv,
                SvHiX = Mods.svHiX
            };
            int eyesBefore = Eyes.Count;
            var R = HHResolver.ResolveLever(Pool, Eyes, Rng, opt);
            var rep = new SpinReport { R = R };

            if (SwapLeft > 0) { SwapLeft--; if (SwapLeft == 0) L("🔀 확률 반전이 풀렸다"); }

            for (int i = 0; i < R.LidN && Eyes.Count > 0; i++)
            {
                int c = Eyes[0]; Eyes.RemoveAt(0);
                if (R.Board[c].IsEye) R.Board[c] = BoardCell.Empty();
            }
            if (R.LidN > 0) L("🫦 눈꺼풀 — 눈을 감겼다");
            if (R.BurnEyes > 0) L("♨️ 소각로 — 눈 " + R.BurnEyes + "개를 태워 +" + R.BurnW.ToString("0.0") + "W");

            int eyesGone = Math.Max(0, eyesBefore - Eyes.Count);
            for (int g = 0; g < eyesGone; g++)
            {
                if (Mods.onEyeGoneOut != 0) OutMult += Mods.onEyeGoneOut;
                FireChain("eyeGone", rep);
            }

            // 낙인 — 층당 mods.brand 회, 터진 살 중 가장 값진 것에 찍는다
            if (_brandStop != StopIdx) { _brandStop = StopIdx; _brandLeft = (int)Mods.brand; }
            if (_brandLeft > 0 && R.Events.Count > 0)
            {
                PoolEntry best = null; float bv = -1; string bn = "";
                foreach (var ev in R.Events)
                    foreach (var c in ev.Cells)
                    {
                        var cell = R.Board[c];
                        if (!cell.Filled || cell.IsEye || cell.Ref == null || !HHSymbols.IsFlesh(cell.K)) continue;
                        float vv = HHResolver.SymVal(cell);
                        if (vv > bv) { bv = vv; best = cell.Ref; bn = HHSymbols.Get(cell.K).Name; }
                    }
                if (best != null) { best.BrandW += 1; _brandLeft--; L("🔥 낙인 — 터진 " + bn + "에 새겼다 · 영구 +1W"); }
            }

            // 분쇄기
            if (R.GrindDev != null)
            {
                var flesh = Pool.FindAll(x => HHSymbols.IsFlesh(x.K));
                if (flesh.Count > 4)
                {
                    var low = flesh[0];
                    for (int f = 1; f < flesh.Count; f++)
                        if (HHSymbols.Get(flesh[f].K).Val * HHResolver.Sc2(flesh[f].Lv) < HHSymbols.Get(low.K).Val * HHResolver.Sc2(low.Lv)) low = flesh[f];
                    Pool.Remove(low);
                    R.GrindDev.Stacks++;
                    rep.GrindLog = "⚙️ 분쇄기가 " + HHSymbols.Get(low.K).Name + "을(를) 삼켰다 — 영구 +2W";
                    L(rep.GrindLog);
                }
            }

            if (R.Bursts > 0) FireChain("line", rep); else FireChain("blank", rep);

            float eyePow = (float)Math.Pow(EyeMultBase, Eyes.Count);
            float outEff = OutMult + Mods.outAdd + Mods.outPerW * TotalWeight;
            if (outEff < 0.1f) outEff = 0.1f;
            int power = (int)Math.Round(R.TotalBase * R.LineMulAll * R.M1 * R.CoreM * eyePow
                                        * outEff * Mods.outMulX * (1f + _chainPowPct));

            Board = R.Board;
            LastBadges = R.Badges;
            SpinsUsed++;
            TempLuck = 0;
            StopLines += R.Bursts;

            // 경제
            int coins = 0;
            if (R.Bursts > 0) coins += 1 + (Eyes.Count > 0 ? (int)Mods.onJackTick : 0);
            else coins += (int)Mods.onBlankTick;
            Coins += coins;

            MissStreak = (power >= EffReq * HHDial.MissFrac) ? 0 : MissStreak + 1;

            // 종 게이지 — 벨 뱃지 단일 경로 (부품 badgeAdd 가 확률을 민다)
            float bellRate = Math.Max(0f, HHDial.BellBadgeRate + Mods.badgeAdd);
            var devCells = new HashSet<int>();
            foreach (var b in R.Badges) devCells.Add(b.Cell);
            LastBellCells.Clear();
            for (int i = 0; i < HHDial.Cells; i++)
            {
                var c = R.Board[i];
                if (!c.Filled || c.IsEye) continue;
                if (devCells.Contains(i)) continue;
                if (Rng.NextDouble() < bellRate) LastBellCells.Add(i);
            }
            var winCells = new HashSet<int>();
            foreach (var ev in R.Events) foreach (var c in ev.Cells) winCells.Add(c);
            int add = 0;
            foreach (var bc in LastBellCells) if (winCells.Contains(bc)) { add++; rep.BellPopped.Add(bc); }

            rep.BellAdd = add;
            rep.GaugeNeed = GaugeNeed;
            BellGauge += add;
            bool ring = false;
            if (add >= 4) { ring = true; BellRingTier = 2; }
            else if (BellGauge >= GaugeNeed)
            {
                ring = true; GaugeRings++;
                BellRingTier = Math.Min(2, Math.Max(0, BellGauge - GaugeNeed) + Math.Max(0, GaugeRings - 1) / HHDial.GaugeTierEvery);
            }
            if (ring) { BellsTotal++; BellGauge = 0; FireChain("bell", rep); }

            rep.Power = power;
            rep.EyePow = eyePow;
            rep.CoinsGained = coins;
            rep.LuckUsed = luck;
            rep.BellRing = ring;
            rep.BellCells = new List<int>(LastBellCells);

            // 종의 내기 정산
            if (Bet != null)
            {
                bool won = R.Bursts >= Bet.Need;
                if (won) { Coins += Bet.N * 2; L("인터폰 내기 승리 — 동전 +" + (Bet.N * 2)); }
                else L("인터폰 내기 패배 — 건물이 판돈을 삼켰다");
                Bet = null;
            }

            Power += power;
            Last = rep;

            if (R.Bursts > 0)
            {
                var names = new List<string>();
                foreach (var e in R.Events) names.Add(e.Name);
                L("⚡ +" + power + "W — " + string.Join("·", names.ToArray()) + (R.CoreM > 1 ? " · 코어 점화" : ""));
            }
            else L("꽝 — 줄이 서지 않았다");
            foreach (var c in rep.ChainLog) L(c);

            if (SpinsUsed >= LeverLimit && Power < EffReq) { Dead = true; L("케이블이 끊겼다 — 문턱을 못 넘겼다"); }
            return rep;
        }

        // ── 출발 ──
        public int Depart()
        {
            if (!CanDepart) return 0;
            int spare = HHDial.SpareCoins(LeversLeft);
            Coins += spare + (int)Mods.arriveTick;

            // 출발 복리 — 부품/승객이 각자 다른 재료를 쌓는다
            if (!string.IsNullOrEmpty(Mods.confirmMulSrc) && Mods.confirmMulV != 0)
            {
                float src = HHModsCalc.ConfirmSrc(this, Mods.confirmMulSrc);
                float mul = 1f + Mods.confirmMulV * src;
                if (mul > 1f) { OutMult *= mul; L("복리 — 전력 배수 ×" + mul.ToString("0.00") + " (" + Mods.confirmMulSrc + " " + src.ToString("0") + ")"); }
            }

            int jump = Math.Max(1, Math.Min(JumpPreview, Math.Max(1, HHDial.FinalFloor - FloorNow)));
            FloorNow = Math.Min(HHDial.FinalFloor, FloorNow + jump);
            L("출발 — +" + jump + "층 → " + FloorNow + "층 · 남긴 레버 " + LeversLeft + "개 = " + spare + "닢");

            StopIdx++;
            Power = 0;
            SpinsUsed = 0;
            BonusSpins = 0;
            StopLines = 0;
            for (int i = 0; i < Board.Length; i++) Board[i] = BoardCell.Empty();
            LastBadges.Clear(); LastBellCells.Clear(); Last = null;

            // 지난 정차의 거래 이월을 소진하고, 심을 눈을 심는다
            int plant = Carry.Plant;
            Carry.Reset();
            for (int i = 0; i < plant; i++) PlantEye();

            // 도착 효과
            for (int i = 0; i < (int)Mods.purgeOnArrive && Eyes.Count > 0; i++) { Eyes.RemoveAt(0); FireChain("eyeGone", null); }
            for (int i = 0; i < (int)Mods.plantOnArrive; i++) PlantEye();

            // 승객 인도
            for (int i = Aboard.Count - 1; i >= 0; i--)
            {
                var a = Aboard[i];
                if (!a.Def.IsQuest || a.Def.Dest <= 0) continue;
                if (FloorNow >= a.Def.Dest)
                {
                    Aboard.RemoveAt(i);
                    Delivered.Add(a.Def.Id);
                    int pay = a.Def.Pay + (int)Mods.onDeliverTick;
                    Coins += pay;
                    L("인도 — " + a.Def.Name + " (" + a.Def.Dest + "층) · 동전 +" + pay);
                    FireChain("deliv", null);
                }
            }
            // 화물 하차
            for (int i = Cargo.Count - 1; i >= 0; i--)
            {
                var c = Cargo[i];
                if (c.DropNext || (c.PickAt > 0 && FloorNow >= c.PickAt))
                {
                    Cargo.RemoveAt(i);
                    Coins += c.Pay;
                    L("하차 — " + c.Name + " · 동전 +" + c.Pay);
                }
            }

            foreach (var a in Actives) a.Chg = Math.Min(a.MaxChg, a.Chg + 1);

            RecomputeMods();
            if (FloorNow >= HHDial.FinalFloor) { Finished = true; L("7734층 — 완주"); }
            RefreshShop(true);
            RollOffers();
            return jump;
        }

        // ── 정차 제안: 문 앞의 사람 · 인터폰 ──
        public void RollOffers()
        {
            Offers = new StopOffers();
            // 승객: 3층부터 · 명부 4명 상한 · 쿨다운 2정차 · 정차 3·9 보장 · 가뭄 ×2.2
            if (FloorNow >= 3 && Aboard.Count < AboardCap)
            {
                int since = StopIdx - _lastPaxStop;
                double pr = 0.18;
                if (StopIdx == 3 || StopIdx == 9) pr = 1.0;
                else if (since <= 2) pr = 0;
                else if (since >= 8) pr = Math.Min(1.0, pr * 2.2);
                if (pr > 0 && Rng.NextDouble() < pr)
                {
                    var pool = new List<PassengerDef>();
                    foreach (var p in HHContent.Roster)
                    {
                        if (Delivered.Contains(p.Id) || Killed.Contains(p.Id)) continue;
                        if (Aboard.Exists(a => a.Def.Id == p.Id)) continue;
                        if (FloorNow < 9 && p.Gen != 1) continue;          // 2기는 9층부터
                        if (p.Dest != 0 && p.Dest <= FloorNow) continue;   // 이미 지난 목적지는 안 태운다
                        pool.Add(p);
                    }
                    if (pool.Count > 0) { Offers.Passenger = pool[Rng.Range(pool.Count)]; _lastPaxStop = StopIdx; }
                }
            }
            // 인터폰: 원본 phoneRate 0.4
            if (Rng.NextDouble() < 0.4) Offers.Deal = HHDeals.Roll(this, Rng);
        }

        public bool BoardPassenger()
        {
            if (Offers.Passenger == null || Aboard.Count >= AboardCap) return false;
            var a = new AboardPassenger { Def = Offers.Passenger, BoardedAtFloor = FloorNow };
            Aboard.Add(a);
            L("탑승 — " + a.Def.Name + " (무게 " + a.W + ") · " + a.Def.Fx);
            Offers.Passenger = null;
            Offers.PassengerAnswered = true;
            RecomputeMods();
            if (Aboard.Count >= AboardCap) FireChain("full", null);
            return true;
        }
        public void RefusePassenger()
        {
            if (Offers.Passenger == null) return;
            L("보냈다 — " + Offers.Passenger.Name);
            Offers.Passenger = null;
            Offers.PassengerAnswered = true;
        }

        public bool AcceptDeal()
        {
            if (Offers.Deal == null || Offers.DealTaken) return false;
            if (!HHDeals.Can(this, Offers.Deal.Id)) return false;
            string msg = HHDeals.Apply(this, Offers.Deal.Id);
            L("인터폰 — " + msg);
            Offers.DealTaken = true;
            RecomputeMods();
            return true;
        }
        public void RefuseDeal()
        {
            if (Offers.Deal == null) return;
            L("인터폰 — 끊었다");
            Offers.Deal = null;
        }

        // ── 상점 ──
        int Phase { get { return StopIdx <= 3 ? 0 : StopIdx <= 6 ? 1 : 2; } }

        public void RefreshShop(bool force)
        {
            if (!force && _shopStop == StopIdx) return;
            _shopStop = StopIdx;
            RollSymShop();
            RollItemShop();
            RollPartShop();
        }

        public void RollSymShop()
        {
            var pool = new List<SymKind>();
            foreach (var d in HHSymbols.All)
            {
                if (d.Kind == SymKind.FURN || d.Kind == SymKind.LID)
                { if (Eyes.Count > 0 || StopIdx >= 2) pool.Add(d.Kind); continue; }
                if (d.W[Phase] > 0) pool.Add(d.Kind);
            }
            SymShop.Clear();
            for (int n = 0; n < 3; n++)
            {
                float tot = 0;
                foreach (var k in pool) tot += HHSymbols.Get(k).W[Phase];
                double r = Rng.NextDouble() * tot;
                SymKind pick = pool[0];
                foreach (var k in pool) { r -= HHSymbols.Get(k).W[Phase]; if (r < 0) { pick = k; break; } }
                SymShop.Add(pick);
            }
        }

        public void RollItemShop()
        {
            var cand = new List<KeyValuePair<ItemDef, float>>();
            foreach (var it in HHItems.All)
            {
                int owned; ItemStacks.TryGetValue(it.Id, out owned);
                if (it.Tier == ItemTier.Unique && owned > 0) continue;
                float w = it.Tier == ItemTier.Unique ? 2f : it.Tier == ItemTier.Active ? 4f : 8f;
                if (it.IsRate && owned > 0) w *= (float)Math.Pow(0.55, owned);
                cand.Add(new KeyValuePair<ItemDef, float>(it, w));
            }
            ItemShop.Clear();
            for (int n = 0; n < 2 && cand.Count > 0; n++)
            {
                float tw = 0; foreach (var c in cand) tw += c.Value;
                double r = Rng.NextDouble() * tw;
                var pick = cand[cand.Count - 1];
                foreach (var c in cand) { r -= c.Value; if (r < 0) { pick = c; break; } }
                ItemShop.Add(pick.Key.Id);
                cand.Remove(pick);
            }
        }

        /// <summary>설비 진열 — 원본 genShopStock: 4칸 · 등급 가중 [.32,.38,.22,.08] · 보유 계열 60% · 융합 대기 계열 60%.</summary>
        public void RollPartShop()
        {
            HHContent.EnsureLoaded();
            PartShop.Clear();
            var owned = new HashSet<string>(); foreach (var p in OwnedParts) owned.Add(p.Id);
            var un = HHContent.Parts.FindAll(p => !owned.Contains(p.Id));
            if (un.Count == 0) return;

            // 산탄총 — 승객을 물리는 유일한 수단이라 4층 이후 첫 상점에 반드시 진열
            if (FloorNow >= 4 && !_sgunShown && !owned.Contains("sgun"))
            { PartShop.Add("sgun"); _sgunShown = true; }

            // 융합 대기 계열의 최저가 부품 60%
            var wantFams = new List<string>();
            foreach (var a in Aboard) if (!a.Fused && !string.IsNullOrEmpty(a.Def.FuseFamily)) wantFams.Add(a.Def.FuseFamily);
            if (wantFams.Count > 0 && Rng.NextDouble() < 0.6)
            {
                string ff = wantFams[Rng.Range(wantFams.Count)];
                PartDef cheap = null;
                foreach (var p in un) if (p.Family == ff && !PartShop.Contains(p.Id)) if (cheap == null || p.Cost < cheap.Cost) cheap = p;
                if (cheap != null) PartShop.Add(cheap.Id);
            }
            // 보유 계열 60%
            var ownedFams = new HashSet<string>(); foreach (var p in OwnedParts) if (!string.IsNullOrEmpty(p.Family)) ownedFams.Add(p.Family);
            if (ownedFams.Count > 0 && Rng.NextDouble() < 0.6)
            {
                var famPool = un.FindAll(p => ownedFams.Contains(p.Family) && !PartShop.Contains(p.Id));
                if (famPool.Count > 0) PartShop.Add(famPool[Rng.Range(famPool.Count)].Id);
            }
            int guard = 0;
            while (PartShop.Count < 4 && guard++ < 400)
            {
                var pool = un.FindAll(p => !PartShop.Contains(p.Id));
                if (pool.Count == 0) break;
                float tot = 0;
                foreach (var p in pool) tot += TierW(p.Cost);
                double r = Rng.NextDouble() * tot;
                var pick = pool[0];
                foreach (var p in pool) { r -= TierW(p.Cost); if (r < 0) { pick = p; break; } }
                PartShop.Add(pick.Id);
            }
        }
        static float TierW(int c) { return c == 1 ? .32f : c == 2 ? .38f : c == 3 ? .22f : c == 4 ? .08f : .1f; }

        public bool BuyPart(string id)
        {
            var d = HHContent.Part(id);
            if (d == null || Coins < d.Cost) return false;
            if (OwnedParts.Count >= PartHoldCap) { L("설비를 더 실을 수 없다 (" + PartHoldCap + "개)"); return false; }
            if (OwnedParts.Exists(p => p.Id == id)) return false;
            Coins -= d.Cost;
            OwnedParts.Add(d);
            PartShop.Remove(id);
            L("설비 — " + d.Name + " (" + d.Cost + "닢) · " + d.Fx);
            // 융합: 이 계열을 기다리던 승객이 있으면 터진다
            foreach (var a in Aboard)
                if (!a.Fused && a.Def.FuseFamily == d.Family)
                { a.Fused = true; L("융합! " + a.Def.Name + " × " + d.Family + " — " + a.Def.FuseFx); FireChain("fuse", null); break; }
            RecomputeMods();
            return true;
        }

        public bool SellPart(string id)
        {
            var p = OwnedParts.Find(x => x.Id == id);
            if (p == null) return false;
            OwnedParts.Remove(p);
            Coins += Math.Max(1, p.Cost / 2);     // 원본 partSellPct 0.5
            L("되팔았다 — " + p.Name);
            RecomputeMods();
            return true;
        }

        public bool BuySymbol(int shopIdx)
        {
            if (shopIdx < 0 || shopIdx >= SymShop.Count || !SymShop[shopIdx].HasValue) return false;
            var k = SymShop[shopIdx].Value;
            var d = HHSymbols.Get(k);
            if (Coins < d.Price) return false;
            Coins -= d.Price;
            SymShop[shopIdx] = null;
            if (d.Family == SymFamily.Device) { Devices.Add(new PoolEntry(k)); L("구매 — " + d.Name + " · 장치로 장착"); }
            else { Pool.Add(new PoolEntry(k)); L("구매 — " + d.Name + " · 릴에 들어갔다"); }
            Coins += (int)Mods.buyTick;
            return true;
        }

        public bool BuyItem(string id)
        {
            var it = HHItems.Get(id);
            if (it == null || Coins < it.Cost) return false;
            if (it.Tier == ItemTier.Active)
            {
                var owned = Actives.Find(a => a.Id == id);
                if (owned != null)
                {
                    if (owned.MaxChg >= 4) return false;
                    Coins -= it.Cost; owned.MaxChg++; owned.Chg = Math.Min(owned.MaxChg, owned.Chg + 1);
                    L("⬆ 업그레이드 — " + it.Name + " 충전 상한 " + owned.MaxChg);
                }
                else
                {
                    if (Actives.Count >= 2) return false;
                    Coins -= it.Cost;
                    Actives.Add(new ActiveItemSlot { Id = id, Chg = it.Charge, MaxChg = it.Charge });
                    L("구매 — " + it.Name + " (액티브 장착)");
                }
            }
            else
            {
                int have; ItemStacks.TryGetValue(id, out have);
                if (it.Tier == ItemTier.Unique && have > 0) return false;
                Coins -= it.Cost;
                ItemStacks[id] = have + 1;
                L("구매 — " + it.Name + (it.Tier == ItemTier.Common ? " ×" + ItemStacks[id] : ""));
            }
            ItemShop.Remove(id);
            return true;
        }

        public bool UseActive(int slot)
        {
            if (slot < 0 || slot >= Actives.Count) return false;
            var a = Actives[slot];
            if (a.Chg <= 0) return false;
            var it = HHItems.Get(a.Id);
            a.Chg--;
            if (it.Act == ActiveKind.Swap) { SwapLeft = 3; L("🔀 확률 반전 — 3레버 동안 최고↔최저 문양이 자리를 바꾼다"); }
            else if (it.Act == ActiveKind.Surge) { TempLuck += 4; L("🎚 과부하 — 다음 레버 운 +4"); }
            else if (it.Act == ActiveKind.Lever)
            {
                if (LeverLimit - SpinsUsed >= HHDial.LeverTank) { a.Chg++; L("레버가 이미 가득 찼다"); return false; }
                BonusSpins++; L("🕹 예비 레버 태엽 — 레버 +1 충전");
            }
            return true;
        }

        public bool Merge(SymKind k, int lv)
        {
            var L2 = HHSymbols.Get(k).Family == SymFamily.Device ? Devices : Pool;
            var same = L2.FindAll(x => x.K == k && x.Lv == lv);
            if (same.Count < 3 || lv >= 3) return false;
            for (int i = 0; i < 3; i++) L2.Remove(same[i]);
            L2.Add(new PoolEntry(k, lv + 1));
            L("⚙ 합성! " + HHSymbols.Get(k).Name + " Lv" + (lv + 1) + " — 효과 ×2");
            return true;
        }

        public bool RemoveSymbol(SymKind k, int lv)
        {
            if (Coins < HHDial.RemoveCost) return false;
            var L2 = HHSymbols.Get(k).Family == SymFamily.Device ? Devices : Pool;
            var tg = L2.FindAll(x => x.K == k && x.Lv == lv);
            if (tg.Count == 0) return false;
            if (L2 == Pool && Pool.Count <= 6) return false;
            Coins -= HHDial.RemoveCost;
            L2.Remove(tg[tg.Count - 1]);
            L("제거 — " + HHSymbols.Get(k).Name);
            return true;
        }

        public bool RerollShop()
        {
            if (Coins < HHDial.RerollCost) return false;
            Coins -= HHDial.RerollCost;
            RollSymShop();
            RollPartShop();
            return true;
        }
    }
}
