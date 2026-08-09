namespace Ascend.Prototype
{
    /// <summary>
    /// 이번 판에서 **켜고 끄는 기능 스위치.** 밸런스 값이 아니라 범위 결정이다.
    ///
    /// 왜 프로파일이 아니라 여기인가: `OverharvestProfile` 같은 에셋은 「이 기능을 얼마나
    /// 세게」를 담는다. 반면 「이번 판에 이 기능이 있는가」는 `CURRENT_PHASE.md` 가 정하는
    /// **범위**다. 둘을 한 곳에 두면 밸런스를 만지다가 기능이 사라지고, 그 반대도 생긴다.
    ///
    /// 왜 코드를 지우지 않는가: 사용자 지시가 「삭제」가 아니라 **「보류」**였다
    /// (2026-08-09). 과수확은 104개 파일 약 1,300곳에 얽혀 있고, 그중 다수를 곧 있을
    /// 전력 재설계가 다시 건드린다. 지금 지우면 (가) 되돌릴 수 없고 (나) 재설계가 같은
    /// 자리를 두 번 헤집는다. 스위치를 끄면 게임에서는 완전히 사라지되 코드는 살아 있고,
    /// 되살리는 비용이 이 파일 한 줄이 된다.
    /// </summary>
    public static class PrototypeFeatures
    {
        /// <summary>보류 상태의 기본값. 되살리려면 이 상수를 true 로 바꾼다.</summary>
        public const bool OverharvestDefault = false;

        /// <summary>
        /// 과수확(추가 스핀 도박)이 이번 판에 존재하는가.
        ///
        /// 끄면 <see cref="Run.FloorSession.IsOverharvestUnlocked"/> 가 항상 거짓이 되고,
        /// 거기에 매달린 것이 전부 함께 꺼진다 — 추가 스핀 · 과수확 레버 · 해금 이벤트 ·
        /// 단계 연출 타임라인 · 접근 정적 · 승객 응시 · 정산 요약의 과수확 줄.
        ///
        /// **끄는 것이 판정 규칙을 바꾸지 않는다.** 앤티 계산식도 해금 임계도 그대로 있고
        /// 단지 그 경로에 도달하지 않을 뿐이다 — 되살렸을 때 밸런스가 예전 그대로여야 한다.
        ///
        /// 쓰기가 열려 있는 이유는 **보류된 하위 시스템의 단위 테스트가 계속 돌아야 하기
        /// 때문이다.** 기능을 껐다고 테스트를 지우거나 스킵하지 않는다
        /// (`CLAUDE.md` 「실패한 테스트를 삭제, 무시, 조건부 스킵으로 숨기지 않는다」).
        /// 테스트는 <see cref="EnableOverharvest"/> 로 잠깐 켜고 반드시 되돌린다.
        /// </summary>
        public static bool Overharvest { get; set; } = OverharvestDefault;

        /// <summary>전부 기본값으로. 테스트 픽스처가 서로를 오염시키지 않게 한다.</summary>
        public static void ResetToDefaults()
        {
            Overharvest = OverharvestDefault;
        }

        /// <summary>
        /// 과수확을 **범위 안에서만** 켠다. 보류된 하위 시스템을 그대로 검증하기 위한 통로다.
        ///
        /// <code>
        /// using (PrototypeFeatures.EnableOverharvest())
        /// {
        ///     // 과수확이 살아 있던 때와 같은 동작을 검증한다
        /// }
        /// </code>
        ///
        /// <c>using</c> 을 빠뜨리면 그 뒤의 테스트가 「기능이 켜진 채」 돌아 조용히 통과한다 —
        /// 그래서 스코프를 반환한다. 직접 <see cref="Overharvest"/> 에 대입하지 않는다.
        /// </summary>
        public static FeatureScope EnableOverharvest() => new FeatureScope(true);

        /// <summary>범위를 벗어나면 이전 값으로 되돌린다.</summary>
        public readonly struct FeatureScope : System.IDisposable
        {
            private readonly bool _previous;

            internal FeatureScope(bool value)
            {
                _previous = Overharvest;
                Overharvest = value;
            }

            public void Dispose() => Overharvest = _previous;
        }
    }
}
