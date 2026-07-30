using System.Collections.Generic;
using System.Text;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// 사고 기록기. 층이 끝난 뒤 "왜 이렇게 됐는가"를 플레이어가 되짚을 수 있게 만든다.
    ///
    /// `MASTER_PRD.md` §10이 요구하는 것은 결과 요약이 아니라 **설명 가능성**이다.
    /// 그래서 최종 숫자만이 아니라 시드·계약·단계별 발동·잔류·위험 변화를 전부 들고 있다.
    /// 시드가 들어 있으므로 이 기록 하나로 같은 층을 다시 돌려볼 수 있다.
    ///
    /// 순수 C#이다 — Unity 없이 만들고 검증할 수 있어야 텔레메트리로도 쓸 수 있다.
    /// </summary>
    public sealed class FloorRecord
    {
        public int RunSeed { get; private set; }
        public int Floor { get; private set; }
        public string CoreQuestion { get; private set; }
        public string Contract { get; private set; }
        public float RequiredPower { get; private set; }
        public float FinalPower { get; private set; }
        public int SpinsUsed { get; private set; }
        public int SpinsAllowed { get; private set; }
        public int ExtraSpinsTaken { get; private set; }
        public float TotalAnte { get; private set; }
        public float NetProfit { get; private set; }
        public float CarriedWeight { get; private set; }
        public bool Overloaded { get; private set; }
        public PowerBand Band { get; private set; }
        public bool Succeeded { get; private set; }
        public string FailureReason { get; private set; }
        public RiskLevel PeakRisk { get; private set; }
        public string RiskReason { get; private set; }
        public ResidualState FinalResidual { get; private set; }

        /// <summary>스핀별 요약. 각 원소가 그 스핀의 재현 정보와 무엇이 터졌는지를 담는다.</summary>
        public IReadOnlyList<SpinRecord> Spins => _spins;

        private readonly List<SpinRecord> _spins = new List<SpinRecord>();

        public readonly struct SpinRecord
        {
            public readonly int Index;
            public readonly bool WasOverharvest;
            public readonly SpinResolution Resolution;

            public SpinRecord(int index, bool wasOverharvest, in SpinResolution resolution)
            {
                Index = index;
                WasOverharvest = wasOverharvest;
                Resolution = resolution;
            }
        }

        /// <summary>
        /// 끝난 층에서 기록을 만든다. <paramref name="result"/>가 null이면 아직 확정되지
        /// 않은 층이므로 진행 중 스냅샷이 된다.
        /// </summary>
        public static FloorRecord Capture(int runSeed, FloorSession floor, FloorResult result,
                                          RiskLevel peakRisk, string riskReason)
        {
            var record = new FloorRecord
            {
                RunSeed = runSeed,
                Floor = floor.Plan.Floor,
                CoreQuestion = floor.Plan.CoreQuestion,
                Contract = floor.SelectedContract.IsNone ? "계약 없음" : floor.SelectedContract.Label,
                RequiredPower = floor.RequiredPower,
                FinalPower = result != null ? result.FinalPower : floor.Power,
                SpinsUsed = floor.SpinsUsed,
                SpinsAllowed = floor.Plan.Spins,
                ExtraSpinsTaken = floor.ExtraSpinsTaken,
                TotalAnte = floor.TotalAnte,
                NetProfit = floor.NetProfit,
                CarriedWeight = floor.CarriedWeight,
                Overloaded = floor.IsOverloaded,
                Band = result != null ? result.Band : floor.CurrentBand,
                Succeeded = result != null && result.Succeeded,
                FailureReason = result != null ? result.FailureReason : null,
                PeakRisk = peakRisk,
                RiskReason = riskReason,
                FinalResidual = floor.Residual,
            };

            // 추가 스핀은 뒤쪽부터 ExtraSpinsTaken 개다 — 판돈을 걸고 돌린 스핀이 어느 것인지
            // 표시하지 않으면 "왜 전력이 줄었는가"를 설명할 수 없다.
            var history = floor.History;
            int firstExtra = history.Count - floor.ExtraSpinsTaken;
            for (int i = 0; i < history.Count; i++)
                record._spins.Add(new SpinRecord(i, i >= firstExtra, history[i]));

            return record;
        }

        /// <summary>
        /// 플레이어에게 보여주는 요약. 한 화면에 모든 숫자를 쏟지 않고 결과 → 원인 순으로 읽힌다
        /// (`MASTER_PRD.md` §6.1의 정신을 결과 화면에도 적용).
        /// </summary>
        public string Summary()
        {
            var sb = new StringBuilder(512);
            sb.AppendLine($"{Floor}층 — {(Succeeded ? "상승" : "실패")}  ·  {Band.DisplayName()}");
            sb.AppendLine($"전력 {FinalPower:F0} / 요구 {RequiredPower:F0}  ({FinalPower / System.Math.Max(1f, RequiredPower):P0})");
            sb.AppendLine($"계약 {Contract}   스핀 {SpinsUsed}/{SpinsAllowed}");

            if (ExtraSpinsTaken > 0)
                sb.AppendLine($"과수확 {ExtraSpinsTaken}회 — 판돈 {TotalAnte:F0} 지불, 순손익 {NetProfit:+0;−0;0}");
            else
                sb.AppendLine("과수확 없음 — 달성 시점에 확정");

            sb.AppendLine($"위험 최고 {PeakRisk.DisplayName()}  ({RiskReason})");
            sb.AppendLine(FinalResidual.IsClean ? "잔류 없음" : $"잔류 — {FinalResidual.Describe()}");
            if (Overloaded) sb.AppendLine($"과적 — 무게 {CarriedWeight:F0}");
            if (!string.IsNullOrEmpty(FailureReason)) sb.AppendLine($"원인: {FailureReason}");
            return sb.ToString();
        }

        /// <summary>
        /// 재현용 전문. 시드와 단계별 보드가 전부 들어 있어 이 텍스트만으로 같은 층을
        /// 헤드리스로 다시 돌릴 수 있다(`MASTER_PRD.md` §10, `TECH_SPEC.md` §15).
        /// </summary>
        public string FullReport()
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine($"=== 사고 기록기 / 런 시드 {RunSeed} / {Floor}층 ===");
            sb.AppendLine($"핵심 질문: {CoreQuestion}");
            sb.AppendLine(Summary());
            sb.AppendLine("--- 스핀별 ---");

            foreach (SpinRecord spin in _spins)
            {
                sb.AppendLine($"[{spin.Index + 1}]{(spin.WasOverharvest ? " 과수확" : string.Empty)}");
                sb.Append(spin.Resolution.DescribeCascade());
            }
            return sb.ToString();
        }
    }
}
