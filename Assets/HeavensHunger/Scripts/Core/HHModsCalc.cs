// HHModsCalc.cs — 원본 recomputeMods 등가. 보유 설비 + 탑승 승객 + 융합분을 하나의 숫자 뭉치로 접는다.
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HeavensHunger
{
    public static class HHModsCalc
    {
        /// <summary>보유 설비/승객에서 mods 를 접는다. famPer(계열 N개당)까지 반영.</summary>
        public static ModBag Compute(List<PartDef> parts, List<AboardPassenger> aboard, float totalWeight)
        {
            var M = new ModBag();
            var famAcc = new ModBag();          // famPer 원본(계열 1개당 값)
            var famCount = new Dictionary<string, int>();

            foreach (var p in parts)
            {
                if (p == null) continue;
                if (!string.IsNullOrEmpty(p.Family))
                    famCount[p.Family] = (famCount.ContainsKey(p.Family) ? famCount[p.Family] : 0) + 1;
                M.selfW += p.W;
                M.AddFrom(p.M, famAcc);
            }
            foreach (var a in aboard)
            {
                if (a == null || a.Def == null) continue;
                M.AddFrom(a.Def.M, famAcc);
                if (a.Fused) M.AddFrom(a.Def.FuseM, famAcc);
            }

            // famPer: "같은 계열 부품 N개당 +v" — 원본 famPerCap 6 · famPerScale 1
            int famTotal = 0;
            foreach (var kv in famCount) famTotal += kv.Value;
            int famUnits = famTotal > 6 ? 6 : famTotal;   // DIAL.famPerCap = 6
            if (famUnits > 0)
            {
                M.luck += famAcc.luck * famUnits;
                M.outAdd += famAcc.outAdd * famUnits;
                M.eyeMult += famAcc.eyeMult * famUnits;
                M.svLo += famAcc.svLo * famUnits;
                M.svHi += famAcc.svHi * famUnits;
                M.lvH += famAcc.lvH * famUnits;
                M.lvV += famAcc.lvV * famUnits;
                M.lvD += famAcc.lvD * famUnits;
                M.pick += famAcc.pick * famUnits;
            }

            // 무게 비례
            if (M.svPerW != 0)
                for (int i = 0; i < M.sv.Length; i++) M.sv[i] += M.svPerW * totalWeight;

            // 저/고배율 일괄 보정을 문양별로 편다
            for (int i = 0; i < M.sv.Length; i++)
            {
                var k = (SymKind)i;
                M.sv[i] += HHResolver.IsOrgan(k) ? M.svHi : M.svLo;
            }
            return M;
        }

        /// <summary>출발 복리(confirmMul)의 재료값.</summary>
        public static float ConfirmSrc(HHRun S, string src)
        {
            switch (src)
            {
                case "used": return S.SpinsUsed;
                case "deliv": return S.Delivered.Count;
                case "weight": return S.TotalWeight;
                case "eyes": return S.Eyes.Count;
                case "lines": return S.StopLines;
                default: return 0;
            }
        }
    }
}
