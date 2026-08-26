// HHDeals.cs — 인터폰 거래 24종의 실제 효과.
// 원본의 ap:S=>{...} 를 한 건씩 옮겼다. 텍스트는 Data/hh_deals.json 이 정본.
using System;
using System.Collections.Generic;

namespace HeavensHunger
{
    /// <summary>다음 정차까지 이월되는 거래 효과 (원본 S.dx).</summary>
    public sealed class DealCarry
    {
        public float ReqMul = 1f;   // 다음 정차 문턱 배수
        public int Plant;           // 다음 정차 판에 심을 눈
        public int Luck;            // 다음 정차 동안 운
        public int SpinCap;         // 다음 정차 레버 상한 (0 = 제한 없음)
        public void Reset() { ReqMul = 1f; Plant = 0; Luck = 0; SpinCap = 0; }
    }

    public static class HHDeals
    {
        /// <summary>원본에서 tx 가 함수였던 거래의 고정 문구.</summary>
        public static string DynamicText(string id)
        {
            switch (id)
            {
                case "d3": return "묻지 마라 — 화물 하나를 싣는다(무게 3). 지정 층에서 내리면 값을 치른다.";
                case "d9": return "네가 아끼는 살이 어느 건지 안다 — 가장 깊이 새겨진 문양에 두 눈금 더 새겨 주지 (영구 +2W). 대가: 지금 판에 눈 +1.";
                case "d10": return "동전을 전부 걸어라. 이번 레버에 줄이 셋 이상 서면 두 배로 돌려준다.";
                case "x2": return "종루가 통째로 울렸다. 살이 통째로 새겨진다 — 릴 풀의 모든 문양 영구 +1W. 대가 없음.";
                default: return "";
            }
        }

        public static bool Can(HHRun S, string id)
        {
            switch (id)
            {
                case "d2": return S.Aboard.Count > 0;
                case "d3": return S.TotalWeight + 3 <= 8 && S.FloorNow + 30 < HHDial.FinalFloor;
                case "d4": return S.Coins >= 2;
                case "d8": return S.TotalWeight + 2 <= 8;
                case "d9": return S.Pool.Count > 0;
                case "d10": return S.Coins >= 4;
                case "u1": return S.Eyes.Count >= 2;
                default: return true;
            }
        }

        /// <summary>거래 수락. 반환값은 로그 문구.</summary>
        public static string Apply(HHRun S, string id)
        {
            switch (id)
            {
                // ── 일반 10 ──
                case "d1": S.OutMult += 1f; S.Carry.Plant += 2; return "전력 배수 +1 — 다음 정차에 눈 +2";
                case "d2":
                    if (S.Aboard.Count > 0)
                    {
                        var p = S.Aboard[S.Aboard.Count - 1];
                        S.Aboard.RemoveAt(S.Aboard.Count - 1);
                        S.Carry.ReqMul *= 0.7f;
                        return p.Def.Name + "을(를) 목적지 아닌 곳에 내렸다 — 다음 정차 문턱 −30%";
                    }
                    return "";
                case "d3":
                    {
                        int tf = S.FloorNow + 30;
                        S.Cargo.Add(new CargoItem { W = 3, Name = "묻지 마라 (" + tf + "층行)", PickAt = tf, Pay = 8 });
                        return "화물을 실었다 — " + tf + "층에서 값을 받는다";
                    }
                case "d4": S.Coins -= 2; S.Carry.ReqMul *= 0.8f; return "동전 −2 — 다음 정차 문턱 −20%";
                case "d5": S.Coins += 2; S.Carry.SpinCap = 4; return "동전 +2 — 다음 정차 레버 4회";
                case "d6": S.Carry.Luck += 2; S.PlantEye(); return "다음 정차 운 +2 — 지금 판에 눈 +1";
                case "d7": S.OutMult += 0.5f; S.Carry.Plant += 1; return "전력 배수 +0.5 — 다음 정차에 눈 +1";
                case "d8":
                    S.Cargo.Add(new CargoItem { W = 2, Name = "명부에 없는 자", DropNext = true, Pay = 2 });
                    return "명부에 없는 자를 태웠다 (무게 2)";
                case "d9":
                    {
                        PoolEntry best = null;
                        foreach (var e in S.Pool) if (e.BrandW > 0 && (best == null || e.BrandW > best.BrandW)) best = e;
                        if (best == null)
                            foreach (var e in S.Pool)
                                if (best == null || HHResolver.SymVal(new BoardCell { Filled = true, K = e.K, Lv = e.Lv, Ref = e })
                                                  > HHResolver.SymVal(new BoardCell { Filled = true, K = best.K, Lv = best.Lv, Ref = best })) best = e;
                        if (best != null) best.BrandW += 2;
                        S.PlantEye();
                        return best != null ? HHSymbols.Get(best.K).Name + "에 두 눈금 새겼다 (영구 +2W) — 눈 +1" : "";
                    }
                case "d10": S.Bet = new BetSlip { N = S.Coins, Need = 3 }; S.Coins = 0; return "동전을 전부 걸었다 — 줄 3개면 두 배";

                // ── 맑은 종 4 ──
                case "w1": S.OutMult += 1f; return "종이 맑게 겹쳤다 — 전력 배수 +1";
                case "w2": S.Coins += 6; return "종이 맑게 겹쳤다 — 동전 +6";
                case "w3": S.TempLuck += 2; return "종이 맑게 겹쳤다 — 이번 정차 운 +2";
                case "w4": S.Carry.Luck += 3; return "종이 맑게 겹쳤다 — 다음 정차 운 +3";

                // ── 종루 4 ──
                case "x1": S.OutMult *= 1.4f; return "종루가 울렸다 — 전력 배수 ×1.4";
                case "x2": foreach (var e in S.Pool) e.BrandW += 1; return "릴 풀 전체 영구 +1W";
                case "x3": S.Coins += 12; return "종루가 울렸다 — 동전 +12";
                case "x4": S.JumpCapBonus += 15; return "종루가 울렸다 — 상승 한계 +15층 (영구)";

                // ── 붉은 종 4 ──
                case "r1": S.OutMult *= 1.5f; S.PlantEye(); S.PlantEye(); S.PlantEye(); return "전력 배수 ×1.5 — 눈 +3";
                case "r2": S.Carry.ReqMul *= 0.5f; for (int i = 0; i < 4; i++) S.PlantEye(); return "다음 정차 문턱 −50% — 눈 +4";
                case "r3": S.Coins += 4; S.PlantEye(); S.PlantEye(); return "동전 +4 — 눈 +2";
                case "r4": S.OutMult *= 1.3f; S.BonusSpins -= 1; S.Carry.SpinCap = 4; return "전력 배수 ×1.3 (영구) — 레버를 한 번씩 덜 준다";

                // ── 하강 2 ──
                case "u1":
                    for (int k = 0; k < 2 && S.Eyes.Count > 0; k++)
                    {
                        int c = S.Eyes[0]; S.Eyes.RemoveAt(0);
                        if (S.Board[c].IsEye) S.Board[c] = BoardCell.Empty();
                    }
                    S.Carry.ReqMul *= 1.4f;
                    return "눈 −2 — 다음 층 문턱 +40%";
                case "u2": S.OutMult += 1f; S.PlantEye(); S.PlantEye(); return "전력 배수 +1 — 눈 +2";
            }
            return "";
        }

        /// <summary>이번 정차에 제안할 거래를 뽑는다. 종이 울렸으면 등급이 올라간다.</summary>
        public static DealDef Roll(HHRun S, HHRng rng)
        {
            HHContent.EnsureLoaded();
            string kind = "normal";
            if (S.BellRingTier >= 2) kind = "grand";
            else if (S.BellRingTier == 1) kind = "well";
            else if (S.Eyes.Count >= 4) kind = "red";

            var pool = new List<DealDef>();
            foreach (var d in HHContent.Deals)
                if (d.Kind == kind && Can(S, d.Id)) pool.Add(d);
            if (pool.Count == 0)
                foreach (var d in HHContent.Deals)
                    if (d.Kind == "normal" && Can(S, d.Id)) pool.Add(d);
            if (pool.Count == 0) return null;
            return pool[rng.Range(pool.Count)];
        }
    }
}
