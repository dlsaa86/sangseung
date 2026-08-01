namespace Ascend.Prototype.Events
{
    /// <summary>
    /// 런 안에서 "일어난 일" 하나. 텔레메트리·사운드·승객 반응·위험 연출이 전부
    /// 이 목록 하나를 보고 갈라진다.
    ///
    /// 왜 공용 목록이 필요한가: 이 넷은 같은 사건에 반응해야 하는데 서로 다른
    /// 층위에 산다(순수 C# 판정 / MonoBehaviour 연출). 각자 판정 코드를 다시
    /// 읽으면 "정화가 일어났다"의 정의가 네 벌 생기고, 그중 하나만 고쳐도
    /// 채널 사이가 어긋난다. `MASTER_PRD.md` §9 마지막 문장이 금지하는 상태다 —
    /// "각 상태는 조명, 사운드, 기계 진동, 파티클, 계기판, 승객 행동에서
    /// 동기화되어야 한다."
    ///
    /// 값을 명시적으로 고정한다. 텔레메트리 파일에 숫자로 들어가므로 중간에
    /// 항목을 끼워 넣으면 과거 로그의 뜻이 바뀐다.
    /// </summary>
    public enum GameEventKind
    {
        None = 0,

        // ── 층 흐름 ───────────────────────────────────────────────────────────
        /// <summary>층이 시작됐다. Floor = 층 번호.</summary>
        FloorStarted = 1,

        /// <summary>적재 단계에서 하나를 실었다. Text = 항목 라벨, FloatValue = 무게.</summary>
        ItemBoarded = 2,

        /// <summary>문을 닫아 적재를 끝냈다. FloatValue = 총 적재 무게.</summary>
        BoardingFinished = 3,

        /// <summary>계약을 확정했다. Text = 계약 라벨, IntValue = 선택 인덱스.</summary>
        ContractSelected = 4,

        // ── 스핀 판정 ─────────────────────────────────────────────────────────
        /// <summary>레버를 당겨 스핀이 시작됐다. SpinIndex = 이 층의 몇 번째인가.</summary>
        SpinStarted = 10,

        /// <summary>결과판의 한 열이 공개됐다. IntValue = 열 인덱스(0~2).</summary>
        ColumnRevealed = 11,

        /// <summary>정상 영혼을 수확했다. IntValue = 개수, FloatValue = 전력.</summary>
        NormalSoulHarvested = 12,

        /// <summary>개수 정화(인접 3개 이상). IntValue = 칸 수, FloatValue = 전력.</summary>
        PurifyScattered = 13,

        /// <summary>직선 정화. IntValue = 칸 수, FloatValue = 전력.</summary>
        PurifyLine = 14,

        /// <summary>직교 연결 정화. IntValue = 칸 수, FloatValue = 전력.</summary>
        PurifyCluster = 15,

        /// <summary>캐스케이드 한 단계가 끝났다. IntValue = 깊이(1부터), FloatValue = 단계 전력.</summary>
        CascadeStep = 16,

        /// <summary>캐스케이드가 하드 캡에 걸려 강제 종료됐다. IntValue = 캡 값.</summary>
        CascadeCapReached = 17,

        /// <summary>잔류 저항이 저장 전력을 깎았다. FloatValue = 깎인 양(양수).</summary>
        ResidualDamage = 18,

        /// <summary>스핀 하나가 끝났다. Payload = <c>SpinResolution</c>.</summary>
        SpinResolved = 19,

        // ── 전력과 과수확 ─────────────────────────────────────────────────────
        /// <summary>전력 임계점을 넘었다. IntValue = 넘은 퍼센트(100·170·300 등).</summary>
        PowerThresholdCrossed = 30,

        /// <summary>요구 전력 100% 달성으로 과수확 잠금이 풀렸다.</summary>
        OverharvestUnlocked = 31,

        /// <summary>플레이어가 과수확 레버를 조준했다(아직 당기지 않았다).</summary>
        OverharvestApproached = 32,

        /// <summary>조준을 거뒀다. 접근 연출을 되돌린다.</summary>
        OverharvestReleased = 33,

        /// <summary>과수확 레버를 당겼다. FloatValue = 건 판돈.</summary>
        OverharvestPulled = 34,

        /// <summary>추가 스핀을 실제로 시작했다. IntValue = 몇 번째 추가 스핀인가.</summary>
        ExtraSpinTaken = 35,

        /// <summary>전력을 확정했다(브레이크). FloatValue = 확정 전력.</summary>
        PowerBanked = 36,

        // ── 위험과 결과 ───────────────────────────────────────────────────────
        /// <summary>위험 단계가 바뀌었다. IntValue = 새 <c>RiskLevel</c>, Payload = 이전 단계.</summary>
        RiskLevelChanged = 50,

        /// <summary>Collapse 연출이 시작됐다. 층 실패가 확정된 순간이다.</summary>
        CollapseBegan = 51,

        /// <summary>층이 끝났다. IntValue = 오른 층수, Text = 결과 요약.</summary>
        FloorResolved = 52,

        /// <summary>런이 끝났다. IntValue = 도달 최고 층, Text = 종료 원인.</summary>
        RunEnded = 53,

        /// <summary>화물 포기 구간(70~89%)이 무언가를 빼앗았다. Text = 잃은 것.</summary>
        JettisonPaid = 54,
    }
}
