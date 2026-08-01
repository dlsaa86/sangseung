namespace Ascend.Prototype.Audio
{
    /// <summary>
    /// 한 번 울리고 끝나는 소리 하나. 지속음(위험 단계 험)은 여기 없다 —
    /// 그건 <c>Scripts/Risk/RiskStateView.cs</c>가 이미 소유하고 있고,
    /// 소유자가 둘이면 같은 험이 두 겹으로 깔린다.
    ///
    /// 왜 사건 목록(<see cref="Events.GameEventKind"/>)과 따로 두는가:
    /// 사건은 "무슨 일이 일어났나"이고 큐는 "어떤 소리가 나야 하나"다. 둘은 1:1이 아니다 —
    /// <c>PurifyLine</c>과 <c>PurifyCluster</c>는 서로 다른 사건이지만 같은 계열의 소리이고,
    /// <c>SpinStarted</c>와 <c>PowerBanked</c>는 다른 사건인데 둘 다 금속 타격이다.
    /// 사건 목록에 소리를 직접 매달면 밸런스 변경이 사운드를 흔들고 그 반대도 마찬가지가 된다.
    ///
    /// 앞의 10종이 `TOPDOWN_MASTER_BACKLOG.md` UP-AUD-02가 지정한 룰렛 사운드 10종이다
    /// (출처 N08 §16.4 — 레버 / 칸 공개 / 영혼 수확 / 정화 / 직선 / 연결 / 캐스케이드 단계 /
    /// 임계점 / 잔류 피해 / 확정). 그 뒤는 룰렛 밖의 큐다.
    ///
    /// 값을 명시적으로 고정한다. 캐시 키와 로그에 숫자로 들어가므로 중간에 끼워 넣으면
    /// 과거 기록의 뜻이 바뀐다.
    /// </summary>
    public enum AudioCueKind
    {
        None = 0,

        // ── UP-AUD-02: 룰렛 사운드 10종 ────────────────────────────────────────
        /// <summary>실행 레버를 당겼다. 무거운 금속 타격.</summary>
        LeverPull = 1,

        /// <summary>통관 하나가 멈춰 결과가 보였다. 짧은 유리질 스윕.</summary>
        ColumnReveal = 2,

        /// <summary>정상 영혼을 거뒀다. 맑고 짧은 종.</summary>
        SoulHarvest = 3,

        /// <summary>인접 3개 기본 정화. 정화 3종 중 가장 짧고 높다.</summary>
        PurifyScattered = 4,

        /// <summary>직선 정화. 개수 정화보다 길고 낮다.</summary>
        PurifyLine = 5,

        /// <summary>직교 연결 정화. 셋 중 가장 길고 낮다 — 가장 큰 사건이기 때문이다.</summary>
        PurifyCluster = 6,

        /// <summary>캐스케이드 한 단계. 깊이만큼 피치가 오른다 — 깊이를 귀로 셀 수 있어야 한다.</summary>
        CascadeStep = 7,

        /// <summary>전력 임계점 통과. 두 음의 상승 인터벌.</summary>
        ThresholdCrossed = 8,

        /// <summary>잔류 저항이 저장 전력을 깎았다. 저역 하강 톤.</summary>
        ResidualDamage = 9,

        /// <summary>전력 확정(브레이크). 레버와 같은 계열의 금속 타격이되 더 짧고 단호하다.</summary>
        PowerBanked = 10,

        // ── 룰렛 밖의 큐 ──────────────────────────────────────────────────────
        /// <summary>요구 전력 100% 달성으로 과수확 잠금이 풀렸다(`MASTER_PRD.md` §7).</summary>
        OverharvestUnlock = 20,

        /// <summary>과수확 레버를 끝까지 당겼다. 정적을 깨고 전부가 재개되는 순간이다.</summary>
        OverharvestPull = 21,

        /// <summary>Collapse 시작. 저주파 임펄스 + 노이즈 테일.</summary>
        CollapseImpact = 22,

        /// <summary>승객의 비언어 음성(UP-AUD-04, `MASTER_PRD.md` §9.3). 승객마다 피치가 다르다.</summary>
        PassengerVoice = 23,

        /// <summary>
        /// 위험 단계가 바뀌는 순간의 금속 응력음.
        ///
        /// 목록에 명시된 14종 밖이지만 필요하다. `MASTER_PRD.md` §9는 단계 변화가
        /// 소리에서도 읽히기를 요구하는데, <c>RiskStateView</c>가 가진 것은 **지속음**이라
        /// 단계가 바뀌는 그 한 순간을 짚어 주지 못한다. 험의 볼륨/피치가 천천히 섞이는 동안
        /// "방금 바뀌었다"는 신호가 없으면 무영상 청취 테스트(§18 Phase 4)에서 전이 시점을
        /// 짚을 수 없다. 지속음을 복제하지 않으면서 전이만 표시하는 최소 추가다.
        /// </summary>
        MetalStress = 24,

        /// <summary>
        /// 경보 사이렌.
        ///
        /// **지속음이 아니다.** Notion MASTER PRD §8.3 이 못박는다 —
        /// "사이렌은 지속 재생하지 않는다. 단계 상승·과수확 해금·레버 결정·사고 순간에만
        /// 강하게 사용한다." (`VISUAL_BIBLE.md` 금지 목록 16번이 「지속 재생되는 사이렌」이다.)
        ///
        /// 그 원칙을 주석이 아니라 **구조**로 지킨다. 사이렌을 이 열거에 넣는 순간
        /// 사이렌은 <see cref="AudioCueKind"/> 의 계약을 물려받는다 — 한 번 울리고 끝나며,
        /// <c>ProceduralClipFactory.MaxSeconds</c> 가 1초를 넘지 못하게 하고,
        /// <c>AudioSource.loop</c> 가 false 인 원샷으로만 나간다. 루프로 만들 자리가 없다.
        ///
        /// 발동 사건은 <c>AudioCueTable.TryMapSiren</c> 넷뿐이고, 변형 번호가 그 넷을 가른다
        /// (0 단계 상승 / 1 과수확 해금 / 2 레버 결정 / 3 사고). 왜 울렸는지 귀로 갈리지
        /// 않으면 "경보가 켜졌다"는 정보밖에 남지 않는다.
        /// </summary>
        Siren = 25,
    }
}
