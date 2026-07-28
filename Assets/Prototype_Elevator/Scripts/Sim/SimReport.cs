using System;
using System.Collections.Generic;
using System.Text;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype
{
    /// <summary>Text report and balance-alert rules for headless simulation output.</summary>
    public static class SimReport
    {
        public static string Format(SimReportData report)
        {
            if (report == null) return "Balance simulation: no data";
            var text = new StringBuilder();
            text.AppendLine("=== Ascend Balance Simulation ===");
            text.AppendLine("runs=" + report.runCount + " seed=" + report.seed);
            foreach (SimPolicyReport policy in report.policies)
            {
                text.AppendLine();
                text.AppendLine("[Policy] " + policy.policyName);
                text.AppendLine("runs=" + policy.runCount + " full-run pass rate=" +
                                Percent(policy.successfulRuns, policy.runCount));
                text.AppendLine("floor pass rates: " + FloorPassRates(policy));
                text.AppendLine("total weight (average per floor)=" + policy.totalWeight.ToString("0.##"));
                text.AppendLine("final power (average run)=" + policy.averageFinalPower.ToString("0.##"));
                text.AppendLine("normal soul base power=" + policy.averageNormalSoulBasePower.ToString("0.##"));
                text.AppendLine("symbol weights=" + SymbolWeights(policy.averageSymbolWeights));
                text.AppendLine("required power by floor=" + Values(policy.averageRequiredPowerByFloor));
                text.AppendLine("final power by floor=" + Values(policy.averageFinalPowerByFloor));
                text.AppendLine("base purification rate/spin=" + Values(policy.basePurificationsByFloor));
                text.AppendLine("line rate/spin=" + Values(policy.linesByFloor));
                text.AppendLine("cluster rate/spin=" + Values(policy.clustersByFloor));
                text.AppendLine("cascade rate/spin=" + Values(policy.cascadesByFloor));
                text.AppendLine("cascade length distribution=" + Distribution(policy.cascadeLengthDistribution));
                text.AppendLine("cascade additional power=" + Values(policy.cascadeAdditionalPowerByFloor));
                text.AppendLine("resistance totals=" + ResistanceTotals(policy.totalResistanceCounts));
                text.AppendLine("absorber residual count=" + Values(policy.absorberResidualsByFloor));
                text.AppendLine("proliferator residual count=" + Values(policy.proliferatorResidualsByFloor));
                text.AppendLine("residual actual cost=" + Values(policy.residualCostsByFloor));
                text.AppendLine("power-band distribution=" + Distribution(policy.powerBandDistribution));
                text.AppendLine("threshold crossing distribution=" +
                                Distribution(policy.thresholdCrossingDistribution));
                text.AppendLine("additional spin choice rate=" + Values(policy.additionalSpinChoicesByFloor));
                text.AppendLine("additional spin success=" + Values(policy.additionalSpinSuccessesByFloor));
                text.AppendLine("additional spin loss=" + Values(policy.additionalSpinLossesByFloor));
                text.AppendLine("average additional-spin ante=" + Values(policy.averageAnteByFloor));
                text.AppendLine("first additional-spin loss rate=" +
                                (policy.firstAdditionalSpinLossRate * 100f).ToString("0.0") + "% (" +
                                policy.firstAdditionalSpinLossCount + "/" + policy.firstAdditionalSpinCount + ")");
                text.AppendLine("contract final power=" + policy.contractAverageFinalPower.ToString("0.##") +
                                " / none final power=" + policy.noneAverageFinalPower.ToString("0.##"));
                text.AppendLine("failed floors=" + FailedFloors(policy));
            }

            text.AppendLine();
            text.AppendLine("[Balance warnings]");
            if (report.balanceWarnings.Count == 0) text.AppendLine("none");
            foreach (string warning in report.balanceWarnings) text.AppendLine("WARNING: " + warning);
            return text.ToString();
        }

        public static void AddBalanceWarnings(SimReportData report)
        {
            report.balanceWarnings.Clear();
            if (report.policies == null || report.policies.Count == 0) return;

            float purificationRate = AverageMetric(report.policies, p => p.basePurificationsByFloor);
            float cascadeRate = AverageMetric(report.policies, p => p.cascadesByFloor);
            if (purificationRate < 0.5f)
                report.balanceWarnings.Add("기본 정화 발생률이 스핀당 0.5회 미만이다 — 억울한 꽝 위험");
            if (purificationRate > 2.0f)
                report.balanceWarnings.Add("기본 정화 발생률이 스핀당 2.0회를 초과한다 — 안전망이 게임을 먹음");
            if (cascadeRate < 0.15f)
                report.balanceWarnings.Add("캐스케이드 발생률이 스핀당 0.15회 미만이다 — 큰 도파민 부족");

            for (int floor = 0; floor < 10; floor++)
            {
                bool allHigh = true;
                bool allLow = true;
                foreach (SimPolicyReport policy in report.policies)
                {
                    float rate = policy.floorAttempts[floor] == 0 ? 0f :
                        (float)policy.floorPasses[floor] / policy.floorAttempts[floor];
                    allHigh &= rate > 0.95f;
                    allLow &= rate < 0.20f;
                }
                if (allHigh) report.balanceWarnings.Add((floor + 1) + "층: 세 정책 모두 통과율 95% 초과 — 긴장 없음");
                if (allLow) report.balanceWarnings.Add((floor + 1) + "층: 세 정책 모두 통과율 20% 미만 — 벽");
            }

            SimPolicyReport cautious = Find(report, "Cautious");
            SimPolicyReport greedy = Find(report, "Greedy");
            if (cautious != null && greedy != null && greedy.averageFinalPower < cautious.averageFinalPower)
                report.balanceWarnings.Add("Greedy 최종 전력 기대값이 Cautious보다 낮다 — 푸시 유어 럭이 함정");

            foreach (SimPolicyReport policy in report.policies)
            {
                if (policy.contractRuns == 0 || policy.noneAverageFinalPower == 0f) continue;
                float difference = Math.Abs(policy.contractAverageFinalPower - policy.noneAverageFinalPower) /
                                   Math.Abs(policy.noneAverageFinalPower);
                if (difference < 0.05f)
                    report.balanceWarnings.Add(policy.policyName + ": 계약과 None의 최종 전력 차이가 5% 미만 — 계약이 무의미");
            }
        }

        private static SimPolicyReport Find(SimReportData report, string name)
        {
            foreach (SimPolicyReport policy in report.policies)
                if (policy.policyName == name) return policy;
            return null;
        }

        private static float AverageMetric(List<SimPolicyReport> reports, Func<SimPolicyReport, float[]> selector)
        {
            float sum = 0f;
            int count = 0;
            foreach (SimPolicyReport report in reports)
            {
                float[] values = selector(report);
                foreach (float value in values)
                {
                    sum += value;
                    count++;
                }
            }
            return sum / Math.Max(1, count);
        }

        private static string FloorPassRates(SimPolicyReport report)
        {
            var values = new string[report.floorPasses.Length];
            for (int i = 0; i < values.Length; i++)
                values[i] = Percent(report.floorPasses[i], report.floorAttempts[i]);
            return string.Join(", ", values);
        }

        private static string FailedFloors(SimPolicyReport report)
        {
            var counts = new Dictionary<int, int>();
            foreach (SimRunRecord run in report.runs)
                if (!run.succeeded) Increment(counts, run.failedFloor);
            return Distribution(counts);
        }

        private static string Values(float[] values)
        {
            if (values == null) return "-";
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = values[i].ToString("0.##");
            return "[" + string.Join(", ", result) + "]";
        }

        private static string Distribution<T>(Dictionary<T, int> values)
        {
            if (values == null || values.Count == 0) return "-";
            var result = new List<string>();
            foreach (KeyValuePair<T, int> pair in values) result.Add(pair.Key + ":" + pair.Value);
            return string.Join(", ", result.ToArray());
        }

        private static string ResistanceTotals(Dictionary<SymbolKind, int> values)
        {
            var result = new List<string>();
            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds)
            {
                int count;
                values.TryGetValue(kind, out count);
                result.Add(kind.DisplayName() + ":" + count);
            }
            return string.Join(", ", result.ToArray());
        }

        private static string SymbolWeights(Dictionary<SymbolKind, float> values)
        {
            var result = new List<string>();
            SymbolKind[] allKinds = { SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.Proliferator };
            foreach (SymbolKind kind in allKinds)
            {
                float weight;
                values.TryGetValue(kind, out weight);
                result.Add(kind.DisplayName() + ":" + weight.ToString("0.##"));
            }
            return string.Join(", ", result.ToArray());
        }

        private static string Percent(int numerator, int denominator)
        {
            if (denominator <= 0) return "0%";
            return (100f * numerator / denominator).ToString("0.0") + "%";
        }

        private static void Increment<T>(Dictionary<T, int> dictionary, T key)
        {
            int value;
            dictionary.TryGetValue(key, out value);
            dictionary[key] = value + 1;
        }
    }
}
