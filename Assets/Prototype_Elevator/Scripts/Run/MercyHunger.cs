using System;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// **절제의 성과.** 적은 스핀으로 요구 전력을 채우고 **즉시 멈춘** 정도다.
    /// 등급은 「첫 달성 시점의 남은 스핀」으로 고정된다 — 그 뒤에 더 돌려도 이미 정해진
    /// 값이 내려가지 않는다는 뜻이 아니다. 더 돌리면 <see cref="MercyHunger"/> 가
    /// **소비된 스핀만큼 낮춰서** 다시 매긴다(<see cref="MercyHunger.MercyFor"/> 참조).
    /// </summary>
    public enum MercyGrade { None = 0, I = 1, II = 2, III = 3 }

    /// <summary>
    /// **탐욕의 성과.** 요구 전력을 채운 뒤 남은 스핀으로 만든 초과 전력의 비율이다.
    /// </summary>
    public enum HungerGrade { None = 0, I = 1, II = 2, III = 3 }

    /// <summary>
    /// Notion `MASTER PRD` §6 의 두 성과 축을 **순수 함수**로 계산한다
    /// (`DECISION_LOG` D-20260805-01 / Notion `D-20260804-07`).
    ///
    /// ## 왜 두 축인가
    ///
    /// PRD §6.1 이 목적을 직접 적는다 — 「잘한 플레이는 보상해야 하지만 순수한 강화만
    /// 지급하면 숙련자가 더 강해져 이후 층이 쉬워진다.」 그래서 성과를 **절제(Mercy)** 와
    /// **탐욕(Hunger)** 으로 나누고, 좋은 플레이가 자동 보상이 아니라 **더 강하고 어려운
    /// 선택**을 열게 한다.
    ///
    /// ## 이 클래스가 하지 않는 것
    ///
    /// **보상을 고르지 않는다.** 등급만 낸다. Mercy Cache · Hunger Cache · 정비 중
    /// 무엇을 주는가는 상위 계층의 일이다(`D-20260804-08`). 등급 계산과 보상 지급을 한
    /// 곳에 두면 밸런스를 만질 때마다 지급 코드가 함께 흔들린다.
    ///
    /// **`UnityEngine` 에 의존하지 않는다.** `MASTER_PRD` §9.1 이 「핵심 판정은 순수 C#
    /// 서비스로 분리해 EditMode 테스트 가능하게 한다」를 요구한다.
    ///
    /// ## 임계치는 상수가 아니다
    ///
    /// Notion 이 「등급 임계치는 밸런스 데이터로 분리하며 위 수치는 프로토타입 시작값이다」
    /// 라고 직접 적었다. 그래서 <see cref="MercyHungerThresholds"/> 를 인자로 받고,
    /// 기본값은 그 「시작값」일 뿐이다.
    /// </summary>
    public static class MercyHunger
    {
        /// <summary>
        /// 절제 등급. <paramref name="spinsRemainingAtFirstReach"/> 는 요구 전력을
        /// **처음** 채운 순간의 남은 스핀이고, <paramref name="extraSpinsTaken"/> 은
        /// 그 뒤에 더 돌린 횟수다.
        ///
        /// 🔴 **더 돌리면 Mercy 가 내려간다.** PRD §6.2 가 「스핀을 사용할수록 Mercy
        /// 기회는 낮아지고」라고 적는다. 즉 Mercy 는 「첫 달성 시점」에 **고정된 상한**이고
        /// 실제 등급은 거기서 소비한 만큼 내려간 값이다. 이 한 줄이 없으면
        /// 「과수확해도 Mercy 를 그대로 챙긴다」가 되어 §6.2 의 저울이 무너진다 —
        /// 브레이크를 걸 이유가 사라진다.
        ///
        /// 요구 전력을 끝내 못 채웠으면(<c>spinsRemainingAtFirstReach &lt; 0</c>)
        /// <see cref="MercyGrade.None"/> 이다. 「달성하지 못한 절제」는 절제가 아니다.
        /// </summary>
        public static MercyGrade MercyFor(int spinsRemainingAtFirstReach, int extraSpinsTaken,
                                          in MercyHungerThresholds t)
        {
            if (spinsRemainingAtFirstReach < 0) return MercyGrade.None;

            int effective = spinsRemainingAtFirstReach - Math.Max(0, extraSpinsTaken);
            if (effective >= t.MercySpinsForIII) return MercyGrade.III;
            if (effective >= t.MercySpinsForII) return MercyGrade.II;
            if (effective >= t.MercySpinsForI) return MercyGrade.I;
            return MercyGrade.None;
        }

        /// <summary>
        /// 탐욕 등급. 초과 전력 비율 = (최종 전력 − 요구 전력) / 요구 전력.
        ///
        /// 요구 전력이 0 이하면 비율이 정의되지 않으므로 <see cref="HungerGrade.None"/> 이다
        /// (0 으로 나누어 무한대를 III 로 만드는 것이 이 저장소가 실제로 겪은 부류의 사고다).
        /// 요구에 못 미치면 초과가 음수이므로 역시 <see cref="HungerGrade.None"/> 이다.
        /// </summary>
        public static HungerGrade HungerFor(float finalPower, float requiredPower,
                                            in MercyHungerThresholds t)
        {
            if (requiredPower <= 0f) return HungerGrade.None;
            float excess = (finalPower - requiredPower) / requiredPower;
            if (excess <= 0f) return HungerGrade.None;

            if (excess >= t.HungerExcessForIII) return HungerGrade.III;
            if (excess >= t.HungerExcessForII) return HungerGrade.II;
            if (excess >= t.HungerExcessForI) return HungerGrade.I;
            return HungerGrade.None;
        }

        /// <summary>초과 전력 비율 그 자체. 계기와 사고 기록기가 등급 대신 이 값을 쓴다.</summary>
        public static float ExcessRatio(float finalPower, float requiredPower)
            => requiredPower <= 0f ? 0f : Math.Max(0f, (finalPower - requiredPower) / requiredPower);

        /// <summary>표시용 이름. `Mercy II` 처럼 등급이 곧 라벨이다.</summary>
        public static string DisplayName(this MercyGrade g)
            => g == MercyGrade.None ? "—" : "Mercy " + Roman((int)g);

        /// <summary>표시용 이름.</summary>
        public static string DisplayName(this HungerGrade g)
            => g == HungerGrade.None ? "—" : "Hunger " + Roman((int)g);

        private static string Roman(int n) => n == 3 ? "III" : n == 2 ? "II" : n == 1 ? "I" : "—";
    }

    /// <summary>
    /// 두 축의 임계치. **값이 아니라 프리셋이다** — Notion §6.2 가 「위 수치는 프로토타입
    /// 시작값」이라고 명시했다.
    ///
    /// 구조체로 두는 이유는 순수 계산 계층이 `ScriptableObject` 를 알 필요가 없기
    /// 때문이다. Unity 쪽 프로파일(`Data.Profiles.MercyHungerProfile`)이 이 구조체를
    /// 만들어 넘긴다 — 규칙과 표현의 분리(`MASTER_PRD` §9.1).
    /// </summary>
    public readonly struct MercyHungerThresholds
    {
        public readonly int MercySpinsForI;
        public readonly int MercySpinsForII;
        public readonly int MercySpinsForIII;
        public readonly float HungerExcessForI;
        public readonly float HungerExcessForII;
        public readonly float HungerExcessForIII;

        public MercyHungerThresholds(int mercyI, int mercyII, int mercyIII,
                                     float hungerI, float hungerII, float hungerIII)
        {
            MercySpinsForI = mercyI;
            MercySpinsForII = mercyII;
            MercySpinsForIII = mercyIII;
            HungerExcessForI = hungerI;
            HungerExcessForII = hungerII;
            HungerExcessForIII = hungerIII;
        }

        /// <summary>
        /// Notion §6.2 의 프로토타입 시작값 그대로.
        /// 남은 1/2/3 → Mercy I/II/III · 초과 25%/60%/100% → Hunger I/II/III.
        /// </summary>
        public static MercyHungerThresholds Default
            => new MercyHungerThresholds(1, 2, 3, 0.25f, 0.60f, 1.00f);

        /// <summary>
        /// 단조성 검사. 임계치가 뒤집히면 「II 인데 III 는 아닌」 구간이 사라져
        /// 등급이 조용히 건너뛴다. 프로파일이 저장될 때마다 이걸 통과해야 한다.
        /// </summary>
        public bool IsMonotonic
            => MercySpinsForI <= MercySpinsForII && MercySpinsForII <= MercySpinsForIII
               && HungerExcessForI <= HungerExcessForII && HungerExcessForII <= HungerExcessForIII;
    }
}
