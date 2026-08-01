using System.Collections.Generic;
using System.Text;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
        /// <summary>
        /// 과적 표시. **기호를 쓰지 않는다.**
        ///
        /// 이전 값은 `"  ⚠ 과적"` 이었고, `⚠`(U+26A0)이 `NanumGothic SDF` 아틀라스에
        /// 없어서 화면에 **두부 글자(□)**로 나갔다. 독립 시각 평가가
        /// `17_accident_recorder` 에서 「무게 320/100 □ 과적」으로 두 번 지적했다.
        ///
        /// 이 문자열은 사고 기록기·HUD·종이 테이프 셋 다 거치므로, 아틀라스에
        /// 확실히 있는 문자만 쓴다 — 대괄호는 ASCII 라 어느 폰트에나 있다.
        /// **기호를 다시 넣고 싶으면 아틀라스에 먼저 넣고 캡처로 확인할 것.**
        /// </summary>
        private const string OverloadMarker = "  [과적]";

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

        /// <summary>허용 중량(짐꾼 보너스 포함). 무게만으로는 과적인지 알 수 없다.</summary>
        public float WeightCapacity { get; private set; }

        /// <summary>
        /// 그 층에서 싣고 있던 승객·부품. `AUTONOMOUS_PROTOTYPE_GOAL.md` §3이 사고 기록기의
        /// 필수 항목으로 지정한 11가지 중 하나인데 빠져 있었다 — 독립 감사가 지목했다.
        ///
        /// 이게 없으면 "왜 요구 전력이 그렇게 높았는가"와 "왜 규칙이 그렇게 굴러갔는가"를
        /// 설명할 수 없다. 무게와 규칙 변경이 전부 적재에서 나오기 때문이다.
        /// </summary>
        public string Loadout { get; private set; }

        /// <summary>적재 상세 — 각 항목의 무게와 효과. 전문 보고에만 쓴다.</summary>
        public string LoadoutDetail { get; private set; }

        public bool Overloaded { get; private set; }
        public PowerBand Band { get; private set; }
        public bool Succeeded { get; private set; }
        public string FailureReason { get; private set; }
        public RiskLevel PeakRisk { get; private set; }
        public string RiskReason { get; private set; }

        /// <summary>화물 포기 구간(70~89%)에서 실제로 잃은 것. 해당 없으면 null.</summary>
        public string Jettison { get; private set; }
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
                                          RiskLevel peakRisk, string riskReason,
                                          string jettison = null)
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
                WeightCapacity = floor.Capacity,
                // 확정 시점 사본을 우선한다. `floor.Loadout` 은 런이 소유한 살아 있는
                // 목록이라, 이 기록이 만들어질 때는 이미 하차·화물 포기가 끝나 있다.
                Loadout = floor.ResolvedLoadoutShort
                          ?? (floor.Loadout != null ? floor.Loadout.DescribeShort() : "없음"),
                LoadoutDetail = floor.ResolvedLoadoutDetail
                                ?? (floor.Loadout != null ? floor.Loadout.Describe() : "적재 없음"),
                Overloaded = floor.IsOverloaded,
                Band = result != null ? result.Band : floor.CurrentBand,
                Succeeded = result != null && result.Succeeded,
                FailureReason = result != null ? result.FailureReason : null,
                PeakRisk = peakRisk,
                RiskReason = riskReason,
                FinalResidual = floor.Residual,
                // 70~89% 로 올랐다면 무언가를 잃었다. 그것을 적지 않으면 화면이
                // "화물 포기"라고만 말하고 무엇이 사라졌는지는 말하지 않는다.
                Jettison = result != null && result.RequiresJettison ? jettison : null,
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
            // `Contract`는 이미 "계약 없음"·"흡수체 계약"처럼 '계약'을 품고 있다.
            // 앞에 또 붙여서 화면에 "계약 계약 없음"이 찍혔다.
            sb.AppendLine($"{Contract}   스핀 {SpinsUsed}/{SpinsAllowed}");
            if (!string.IsNullOrEmpty(Jettison)) sb.AppendLine(Jettison);

            if (ExtraSpinsTaken > 0)
                sb.AppendLine($"과수확 {ExtraSpinsTaken}회 — 판돈 {TotalAnte:F0} 지불, 순손익 {NetProfit:+0;−0;0}");
            else
                sb.AppendLine("과수확 없음 — 달성 시점에 확정");

            sb.AppendLine($"위험 최고 {PeakRisk.DisplayName()}  ({RiskReason})");
            sb.AppendLine(FinalResidual.IsClean ? "잔류 없음" : $"잔류 — {FinalResidual.Describe()}");

            // 무게는 허용치와 함께 적어야 뜻이 생긴다. 33kg 이 위험한지 아닌지는
            // 허용 중량을 봐야 알 수 있고, 그 허용 중량은 짐꾼이 바꾼다.
            sb.AppendLine($"적재 {Loadout}   무게 {CarriedWeight:F0}/{WeightCapacity:F0}" +
                          (Overloaded ? OverloadMarker : string.Empty));

            if (!string.IsNullOrEmpty(FailureReason))
                sb.AppendLine($"원인: {LocalizeFailure(FailureReason)}");
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
            sb.AppendLine("--- 적재 ---");
            sb.AppendLine(LoadoutDetail);
            sb.AppendLine("--- 스핀별 ---");

            foreach (SpinRecord spin in _spins)
            {
                sb.AppendLine($"[{spin.Index + 1}]{(spin.WasOverharvest ? " 과수확" : string.Empty)}");
                sb.Append(spin.Resolution.DescribeCascade());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 실패 사유를 플레이어가 읽는 말로 바꾼다.
        ///
        /// `AscendResult`가 `"Crash"` / `"Jettison required"`를 영어로 하드코딩해 두어
        /// 사고 기록기가 `원인: Crash`를 출력하고 있었다. 나머지 화면이 전부 한국어인데
        /// 실패 원인만 영어라면, 하필 가장 설명이 필요한 순간에 설명이 끊긴다.
        ///
        /// 문자열 매핑으로 두는 이유: `AscendResult`는 순수 C#이고 표시 언어를 몰라야 한다.
        /// 표시 계층에서 바꾸는 것이 맞다.
        /// </summary>
        private static string LocalizeFailure(string reason)
        {
            switch (reason)
            {
                case "Crash":             return "추락 — 요구 전력의 70% 미만";
                case "Jettison required": return "화물 포기 — 요구 전력의 70~89%";
                default:                  return reason;
            }
        }
    }
}
