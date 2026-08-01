using System;

namespace Ascend.Prototype.Risk
{
    /// <summary>
    /// 위험 단계 산출의 입력. 순수 데이터라 Unity 없이 테스트할 수 있다.
    ///
    /// 무엇이 위험을 만드는가는 `MASTER_PRD.md` §9에서 나온다 — 과적, 잔류 저항, 그리고
    /// 플레이어가 자발적으로 선택한 과수확이다. 셋 다 "플레이어의 선택이 공간에 드러난다"는
    /// 요구(§9 첫 문장)를 만족시키는 축이고, 무작위로 오르내리는 값이 아니다.
    /// </summary>
    public readonly struct RiskInputs
    {
        /// <summary>보드에 남은 흡수체 수. 저장 전력을 직접 깎는다.</summary>
        public readonly int AbsorberResidual;

        /// <summary>보드에 남은 증식체 수. 다음 스핀을 더 위험하게 만든다.</summary>
        public readonly int ProliferatorResidual;

        /// <summary>지금까지 당긴 과수확 횟수. 자발적으로 감수한 위험이다.</summary>
        public readonly int ExtraSpinsTaken;

        /// <summary>과적 상태인가.</summary>
        public readonly bool Overloaded;

        /// <summary>남은 스핀. 0이면 만회할 기회가 없다.</summary>
        public readonly int SpinsRemaining;

        /// <summary>현재 전력 / 요구 전력.</summary>
        public readonly float PowerRatio;

        /// <summary>층이 실패로 확정됐는가. true면 무조건 Collapse다.</summary>
        public readonly bool FloorFailed;

        public RiskInputs(int absorberResidual, int proliferatorResidual, int extraSpinsTaken,
                          bool overloaded, int spinsRemaining, float powerRatio, bool floorFailed)
        {
            AbsorberResidual = Math.Max(0, absorberResidual);
            ProliferatorResidual = Math.Max(0, proliferatorResidual);
            ExtraSpinsTaken = Math.Max(0, extraSpinsTaken);
            Overloaded = overloaded;
            SpinsRemaining = Math.Max(0, spinsRemaining);
            PowerRatio = powerRatio;
            FloorFailed = floorFailed;
        }
    }

    /// <summary>
    /// 위험 단계 산출기. 순수 C#이고 상태는 "직전 단계" 하나뿐이다.
    ///
    /// **히스테리시스가 핵심이다.** `TECH_SPEC.md` §6.4가 진입·이탈 임계값을 따로 요구하는
    /// 이유는 경계에서 단계가 떨리면 조명과 소리가 초당 몇 번씩 뒤집혀 판독성이 무너지기
    /// 때문이다. 그건 긴장이 아니라 고장으로 보인다.
    ///
    /// 임계값은 전부 필드다 — 최종 밸런스는 승인 대기 항목이라 상수로 박지 않는다.
    /// </summary>
    public sealed class RiskEvaluator
    {
        // ── 위험 점수 가중치 ──

        /// <summary>남은 흡수체 1개당 점수. 저장 전력을 직접 깎으므로 즉시 위험이다.</summary>
        public float AbsorberWeight = 1.0f;

        /// <summary>남은 증식체 1개당 점수. 다음 스핀을 더 나쁘게 만드는 지연된 위험이라 조금 높다.</summary>
        public float ProliferatorWeight = 1.2f;

        /// <summary>
        /// 과수확 1회당 점수. **반드시 <see cref="StrainEnter"/> 이상이어야 한다.**
        ///
        /// `MASTER_PRD.md` §7: 과수확은 "단순한 점수 버튼이 아니라 조명, 음향, 진동, 승객
        /// 반응, 기계 상태를 변화시키는 공간적 사건이어야 한다." 한 번 당겼는데 방이 그대로
        /// Stable이면 그 문장이 거짓이 된다. 이 불변식은 테스트로 고정돼 있다.
        /// </summary>
        public float OverharvestWeight = 3.2f;

        /// <summary>과적 상태의 고정 점수.</summary>
        public float OverloadScore = 3.0f;

        /// <summary>스핀을 다 쓰고도 요구 전력에 못 미칠 때의 고정 점수.</summary>
        public float ShortfallScore = 4.0f;

        // ── 진입·이탈 임계값 (이탈이 항상 더 낮다) ──

        public float StrainEnter = 3.0f;
        public float StrainExit = 2.0f;
        public float CriticalEnter = 7.0f;
        public float CriticalExit = 5.5f;

        /// <summary>직전에 산출한 단계. 히스테리시스의 유일한 상태다.</summary>
        public RiskLevel Current { get; private set; } = RiskLevel.Stable;

        /// <summary>마지막으로 계산한 원점수. 디버그 패널이 "왜 위험한가"를 설명할 때 쓴다.</summary>
        public float CurrentScore { get; private set; }

        public void Reset()
        {
            Current = RiskLevel.Stable;
            CurrentScore = 0f;
        }

        /// <summary>원점수. 단계 없이 값만 필요한 곳(계기판 바늘 등)이 쓴다.</summary>
        public float Score(in RiskInputs inputs)
        {
            float score = inputs.AbsorberResidual * AbsorberWeight
                        + inputs.ProliferatorResidual * ProliferatorWeight
                        + inputs.ExtraSpinsTaken * OverharvestWeight;

            if (inputs.Overloaded) score += OverloadScore;
            if (inputs.SpinsRemaining == 0 && inputs.PowerRatio < 1f) score += ShortfallScore;
            return score;
        }

        public RiskLevel Evaluate(in RiskInputs inputs)
        {
            CurrentScore = Score(in inputs);

            // 실패는 점수와 무관하다. 결과가 이미 나왔으면 그게 상태다.
            if (inputs.FloorFailed)
            {
                Current = RiskLevel.Collapse;
                return Current;
            }

            // Collapse는 실패 상태 전용이므로, 실패가 풀리면 점수로 되돌아간다.
            RiskLevel level = Current == RiskLevel.Collapse ? RiskLevel.Critical : Current;

            switch (level)
            {
                case RiskLevel.Stable:
                    if (CurrentScore >= CriticalEnter) level = RiskLevel.Critical;
                    else if (CurrentScore >= StrainEnter) level = RiskLevel.Strain;
                    break;

                case RiskLevel.Strain:
                    if (CurrentScore >= CriticalEnter) level = RiskLevel.Critical;
                    else if (CurrentScore < StrainExit) level = RiskLevel.Stable;
                    break;

                default:   // Critical
                    if (CurrentScore < StrainExit) level = RiskLevel.Stable;
                    else if (CurrentScore < CriticalExit) level = RiskLevel.Strain;
                    break;
            }

            Current = level;
            return Current;
        }

        /// <summary>왜 이 단계인지 한 줄. 숫자만 띄우면 플레이어가 원인을 못 짚는다.</summary>
        public string Explain(in RiskInputs inputs)
        {
            var parts = new System.Collections.Generic.List<string>(4);
            if (inputs.AbsorberResidual > 0) parts.Add($"잔류 흡수체 {inputs.AbsorberResidual}");
            if (inputs.ProliferatorResidual > 0) parts.Add($"잔류 증식체 {inputs.ProliferatorResidual}");
            if (inputs.ExtraSpinsTaken > 0) parts.Add($"과수확 {inputs.ExtraSpinsTaken}회");
            if (inputs.Overloaded) parts.Add("과적");
            if (inputs.SpinsRemaining == 0 && inputs.PowerRatio < 1f) parts.Add("스핀 소진·전력 미달");
            return parts.Count == 0 ? "위험 요인 없음" : string.Join(" · ", parts);
        }
    }
}
