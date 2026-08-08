using System;
using System.Collections.Generic;

namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 한 저항체 종류가 한 캐스케이드 단계에서 정화된 결과.
    /// 어떤 칸이 어떤 패턴으로 터졌는지 UI가 그대로 하이라이트할 수 있어야 한다.
    /// </summary>
    [Serializable]
    public struct PurifyEvent
    {
        public SymbolKind  Kind;
        public PatternKind Pattern;

        /// <summary>정화된 칸 인덱스. 개수 정화는 보드 전체의 해당 종류 칸이 전부 들어간다.</summary>
        public int[] Cells;

        /// <summary>
        /// **패턴을 이룬** 칸. `Cells`(정화된 전부)와 다르다.
        ///
        /// 직선이면 그 줄의 3칸, 연결이면 덩어리를 이룬 칸, 잭팟이면 9칸이다.
        /// 개수 정화(Scattered)는 모양이 없으므로 비어 있다.
        ///
        /// 이 필드가 없으면 "왜 터졌는가"를 화면이 형태로 표시할 수 없다.
        /// 뷰가 보드를 다시 훑어 직선·덩어리를 찾는 것은 판정의 두 번째 구현이 되므로 금지다
        /// (`TECH_SPEC.md` §5 "UI와 연출은 SpinResult를 소비한다").
        /// </summary>
        public int[] PatternCells;

        /// <summary>직선 패턴일 때 어떤 줄이었는지. 그 외에는 의미 없음.</summary>
        public LineKind Line;

        /// <summary>이 정화가 만든 전력(패턴 배수·계약 보상·연쇄 배수 적용 후).</summary>
        public float Power;

        /// <summary>적용된 패턴 배수. 로그 판독용.</summary>
        public float PatternMultiplier;
    }

    /// <summary>캐스케이드 한 단계. 1단계가 최초 스핀 판정이다.</summary>
    [Serializable]
    public struct CascadeStep
    {
        /// <summary>1부터 시작. 노션 01 "연쇄 단계 예시"의 번호와 같다.</summary>
        public int Depth;

        /// <summary>이 단계 판정 직전의 보드.</summary>
        public SpinBoard BoardBefore;

        /// <summary>제거·재충전이 끝난 뒤의 보드.</summary>
        public SpinBoard BoardAfter;

        /// <summary>이 단계에서 수확된 정상 영혼 개수.</summary>
        public int NormalSoulsHarvested;

        /// <summary>정상 영혼 수확 전력.</summary>
        public float NormalSoulPower;

        /// <summary>이 단계에 일어난 정화들.</summary>
        public PurifyEvent[] Purifies;

        /// <summary>이 단계에 적용된 연쇄 배수.</summary>
        public float ChainMultiplier;

        /// <summary>이 단계가 만든 전력 총합.</summary>
        public float StepPower;
    }

    /// <summary>
    /// 정화되지 않고 다음 스핀으로 넘어가는 위험. 스핀 사이에 유지되는 유일한 상태다.
    /// </summary>
    [Serializable]
    public struct ResidualState
    {
        /// <summary>보드에 남은 흡수체 수.</summary>
        public int AbsorberCount;

        /// <summary>보드에 남은 증식체 수.</summary>
        public int ProliferatorCount;

        /// <summary>흡수체 잔류로 실제 차감된 저장 전력(양수).</summary>
        public float StoredPowerLoss;

        /// <summary>증식체 잔류로 다음 스핀 증식체 가중치에 더해질 값.</summary>
        public float NextProliferatorWeightAdd;

        /// <summary>
        /// 검침원이 **장부에서 지운** 잔류 개수(PD-30). 판에는 그대로 남아 있다.
        /// 0이면 옛 판본이다. 기록에 남기는 이유는 「대가가 왜 이만큼인가」가
        /// 개수만 봐서는 설명되지 않기 때문이다 — 노션 §10 이 요구하는 인과 순서다.
        /// </summary>
        public int ForgivenCount;

        public bool IsClean => AbsorberCount == 0 && ProliferatorCount == 0;

        public static ResidualState Empty => default;

        public string Describe()
        {
            if (IsClean) return "잔류 없음";
            var parts = new List<string>(2);
            if (AbsorberCount > 0)
                parts.Add($"흡수체 {AbsorberCount}개 → 저장 전력 −{StoredPowerLoss:0.#}");
            if (ProliferatorCount > 0)
                parts.Add($"증식체 {ProliferatorCount}개 → 다음 스핀 출현 +{NextProliferatorWeightAdd:0.##}");
            if (ForgivenCount > 0)
                parts.Add($"검침 {ForgivenCount}개 면제");
            return string.Join(" / ", parts);
        }
    }

    /// <summary>
    /// 레버 한 번의 전체 결과. 결정론 재현과 노션 99 "핵심 측정 로그"의 단위이기도 하다.
    /// </summary>
    [Serializable]
    public struct SpinResolution
    {
        /// <summary>이 스핀에 쓰인 시드. 같은 시드 + 같은 규칙이면 같은 결과가 나와야 한다.</summary>
        public int Seed;

        /// <summary>이 스핀을 파생시킨 런 시드. Seed 하나만으로는 어느 런인지 되짚을 수 없다.</summary>
        public int RunSeed;

        /// <summary>층 번호. <see cref="SpinSeed.Derive"/>의 두 번째 좌표.</summary>
        public int Floor;

        /// <summary>해당 층의 몇 번째 스핀인가(0부터). <see cref="SpinSeed.Derive"/>의 세 번째 좌표.</summary>
        public int SpinIndex;

        /// <summary>
        /// 캐스케이드가 하드 캡에 걸려 강제 종료됐는가.
        ///
        /// `MASTER_PRD.md` §6은 "캡 도달 시 오류로 멈추지 말고 명확한 로그를 남긴 뒤 정상
        /// 종료한다"고 요구한다. 플래그 없이 조용히 끝나면 무한 루프 방지가 **동작했는지
        /// 증명할 수 없다** — 자연 종료와 강제 종료가 구분되지 않기 때문이다.
        /// </summary>
        public bool CascadeCapReached;

        /// <summary>최초 3×3 결과.</summary>
        public SpinBoard InitialBoard;

        /// <summary>모든 캐스케이드가 끝난 뒤 보드.</summary>
        public SpinBoard FinalBoard;

        /// <summary>1단계부터 순서대로. Length가 곧 연쇄 길이다.</summary>
        public CascadeStep[] Steps;

        /// <summary>정상 영혼 수확 전력 총합.</summary>
        public float NormalSoulPower;

        /// <summary>정화 전력 총합.</summary>
        public float PurifyPower;

        /// <summary>잔류 저항이 깎기 전의 전력.</summary>
        public float GrossPower;

        /// <summary>잔류 처리 결과.</summary>
        public ResidualState Residual;

        /// <summary>잔류 차감까지 끝난 이 스핀의 순 전력. 음수가 될 수 있다.</summary>
        public float NetPower;

        /// <summary>도달한 최대 연쇄 깊이.</summary>
        public int ChainDepth => Steps != null ? Steps.Length : 0;

        /// <summary>이 스핀에 적용된 계약.</summary>
        public ResistanceContract Contract;

        /// <summary>사람이 읽는 한 줄 요약. 콘솔·HUD 공용.</summary>
        public string Summary()
        {
            int purifies = 0;
            PatternKind best = PatternKind.None;
            if (Steps != null)
            {
                foreach (CascadeStep step in Steps)
                {
                    if (step.Purifies == null) continue;
                    purifies += step.Purifies.Length;
                    foreach (PurifyEvent p in step.Purifies)
                        if (p.Pattern > best) best = p.Pattern;
                }
            }
            return $"{InitialBoard} | 정화 {purifies}회 (최고 {best.DisplayName()}) | 연쇄 {ChainDepth}" +
                   (CascadeCapReached ? "(캡)" : string.Empty) + " | " +
                   $"전력 {NetPower:+0.#;−0.#;0} | {Residual.Describe()}";
        }

        /// <summary>
        /// 재현에 필요한 최소 정보를 한 줄로 직렬화한다. `TECH_SPEC.md` §11의
        /// "SpinResult 직렬화 또는 로그 재현" 요구를 이 한 줄이 담당한다 —
        /// 로그에서 이 줄만 떼어내면 헤드리스로 같은 스핀을 다시 돌릴 수 있어야 한다.
        ///
        /// 형식: `SPIN run=<런시드> floor=<층> idx=<스핀> seed=<스핀시드> init=<초기판> final=<최종판> depth=<연쇄>[cap] net=<순전력>`
        /// </summary>
        public string ToLogLine()
        {
            return $"SPIN run={RunSeed} floor={Floor} idx={SpinIndex} seed={Seed} " +
                   $"init=\"{InitialBoard}\" final=\"{FinalBoard}\" " +
                   $"depth={ChainDepth}{(CascadeCapReached ? "cap" : string.Empty)} " +
                   $"gross={GrossPower:0.##} residual={Residual.StoredPowerLoss:0.##} net={NetPower:0.##}";
        }

        /// <summary>
        /// 캐스케이드 캡 도달 시 남겨야 하는 진단. `TECH_SPEC.md` §9가 요구하는
        /// "시드, 초기 보드, 마지막 보드, 단계별 발동"을 전부 포함한다.
        /// </summary>
        public string DescribeCascade()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.AppendLine(ToLogLine());
            if (Steps == null) return sb.ToString();

            foreach (CascadeStep step in Steps)
            {
                sb.Append($"  [{step.Depth}] ×{step.ChainMultiplier:0.##}  {step.BoardBefore} → {step.BoardAfter}");
                sb.Append($"  영혼 {step.NormalSoulsHarvested}개({step.NormalSoulPower:0.#})");
                if (step.Purifies != null)
                    foreach (PurifyEvent p in step.Purifies)
                        sb.Append($"  {p.Kind.DisplayName()}/{p.Pattern.DisplayName()}×{p.Cells?.Length ?? 0}({p.Power:0.#})");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
