using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 남은 스핀을 **운행 효율 정산**으로 바꾸는 수치 (`T-05` 2026-08-02 결정).
    ///
    /// ## 이 규칙이 없으면 무엇이 망가지나
    ///
    /// 직전까지 남은 스핀은 **그냥 사라졌다.** 목표 전력을 채우고 확정하면 안 쓴
    /// 스핀의 가치가 0 이었고, 그래서 「지금 멈춘다」가 전략이 되지 못했다 —
    /// 항상 과수확으로 한 번 더 굴리는 것이 유일하게 합리적인 선택이었다.
    /// 선택지가 하나뿐인 것은 선택지가 아니다.
    ///
    /// 정산이 붙으면 두 값이 실제로 겨룬다 —
    ///   · **안전**: 남은 스핀 × 5% 를 확정 수익으로
    ///   · **과수확**: 그 권리를 버리고 더 큰 초과 전력을 노린다
    ///
    /// ## 정산은 스핀이 아니다
    ///
    /// **캐스케이드·임계점·승객·부품 효과를 발동하지 않는다** (`T-05`).
    /// 정산이 그것들을 건드리면 「안 돌린 스핀」이 「돌린 스핀」과 같아져
    /// 위 대비가 사라지고, 결정론 재현에도 새 난수 소비처가 생긴다.
    /// 이 클래스는 **곱셈 하나**만 한다.
    ///
    /// ## 값은 2026-08-04 에 5%/20% → **15%/60%** 로 올랐다 (실측)
    ///
    /// `T-05` 의 5%/20% 는 스스로 「임시 기본값」이라고 적은 값이었고, 그 값이
    /// 실제로 무엇을 만드는지 아무도 재지 않았다. 재 보니 **기대값 최적 플레이가
    /// 과수확을 91.7% 고른다** — 즉 「지금 멈춘다」가 여전히 선택이 아니었다.
    /// 이 클래스의 첫 문단이 막으려던 바로 그 상태다.
    ///
    /// 후보 7개를 시드 300개로 훑어 골랐다 (`docs/runtime/BALANCE_SOLVE.md`).
    ///
    /// | 회당 | 상한 | 과수확 선택률 |
    /// |---|---|---|
    /// | 0.05 | 0.20 | 91.7%  ← 옛 기본값. 선택이 아니다 |
    /// | 0.09 | 0.36 | 79.6% |
    /// | 0.13 | 0.52 | 55.6% |
    /// | **0.15** | **0.60** | **47.4%**  ← 채택 |
    /// | 0.18 | 0.72 | 39.0% |
    ///
    /// 판정 기준은 **과수확 선택률 25~75%** 다. Notion §7.3 의 「추가 스핀 기대값의
    /// 30~40%」는 「추가 스핀 기대값」이 한 번인지 남은 전부인지 문서가 말하지 않아
    /// 두 해석이 3~4배 차이 난다 — 그 값 하나로는 고를 수 없다.
    ///
    /// ## ⚠ 상한 60% 가 위 문단의 경고를 넘는다
    ///
    /// `SettlementPower` 의 주석이 「50% 는 그 자체로 한 층을 더 오르는 양」이라고
    /// 경고한다. 60% 는 그 위다. **다만 정산의 지급처는 전력이 아니라 돈이다**
    /// (`RunSession.FloorAscent.SettlementMoney`) — 상승에 쓰이지 않으므로
    /// 그 경고가 말한 사고(안 돌린 것이 돌린 것보다 낫다)는 일어나지 않는다.
    /// 대신 **소지금 경제가 부풀 수 있다.** 상점이 붙으면 다시 재야 한다.
    /// → `ASSUMPTION_LOG` A-20260804-10
    /// </summary>
    [CreateAssetMenu(menuName = "Ascend/Profiles/Remaining Spin Settlement",
                     fileName = "RemainingSpinSettlementProfile")]
    public sealed class RemainingSpinSettlementProfile : ScriptableObject
    {
        [Header("정산 (T-05 임시 기본값)")]
        [Tooltip("남은 스핀 1회당 요구 전력의 몇 배를 정산하는가. T-05 기본값 0.05 (=5%).")]
        [SerializeField, Range(0f, 0.3f)] private float _perSpinRatio = 0.25f;

        [Tooltip("층당 정산 상한. 요구 전력의 몇 배인가. T-05 기본값 0.20 (=20%).")]
        [SerializeField, Range(0f, 1f)] private float _floorCapRatio = 1.00f;

        [Tooltip("정산 전력을 돈으로 바꾸는 비율. 1 이면 전력 1 = 돈 1.")]
        [SerializeField, Min(0f)] private float _moneyPerPower = 1f;

        public float PerSpinRatio => _perSpinRatio;
        public float FloorCapRatio => _floorCapRatio;
        public float MoneyPerPower => _moneyPerPower;

        /// <summary>코드 프리셋. 에셋이 배선되지 않아도 규칙이 사라지지 않는다.</summary>
        public static RemainingSpinSettlementSnapshot DefaultSnapshot =>
            new RemainingSpinSettlementSnapshot(0.25f, 1.00f, 1f, "코드 프리셋");

        public RemainingSpinSettlementSnapshot Snapshot =>
            new RemainingSpinSettlementSnapshot(_perSpinRatio, _floorCapRatio, _moneyPerPower, name);

        /// <summary>
        /// 에셋이 있으면 그 값을, 없으면 코드 프리셋을 돌려주고 **어느 쪽인지 남긴다.**
        /// 「배선했다」와 「그 값이 쓰였다」는 다르고, 이 저장소는 그 구분을 흐렸다가
        /// 프로파일 8종이 여덟 세션 동안 죽어 있는 것을 못 봤다.
        /// </summary>
        public static RemainingSpinSettlementSnapshot SnapshotOrDefault(
            RemainingSpinSettlementProfile profile, string caller)
        {
            if (profile != null) return profile.Snapshot;
            Debug.LogWarning($"[상승] RemainingSpinSettlementProfile 이 배선되지 않았다 ({caller}). 코드 프리셋으로 진행한다.");
            return DefaultSnapshot;
        }
    }

    /// <summary>
    /// 값 사본. `ScriptableObject` 참조를 런타임 판정 경로에 들고 다니지 않는다 —
    /// 씬을 여는 순간 네이티브 객체가 파괴되는 사고를 이 저장소가 겪었다
    /// (`CLAUDE.md` 「OpenScene 은 참조되지 않은 ScriptableObject 를 파괴한다」).
    /// </summary>
    public readonly struct RemainingSpinSettlementSnapshot
    {
        public readonly float PerSpinRatio;
        public readonly float FloorCapRatio;
        public readonly float MoneyPerPower;
        public readonly string Source;

        public RemainingSpinSettlementSnapshot(float perSpinRatio, float floorCapRatio,
                                               float moneyPerPower, string source)
        {
            PerSpinRatio = Mathf.Max(0f, perSpinRatio);
            FloorCapRatio = Mathf.Max(0f, floorCapRatio);
            MoneyPerPower = Mathf.Max(0f, moneyPerPower);
            Source = source;
        }

        /// <summary>
        /// 남은 스핀 <paramref name="spinsRemaining"/> 회를 정산 전력으로 바꾼다.
        ///
        /// 상한이 **층당**이라는 것이 요점이다 — 스핀 수가 많은 층에서 정산만으로
        /// 요구 전력을 다시 채우는 일이 없어야 한다. 5% × 10회 = 50% 는 그 자체로
        /// 한 층을 더 오르는 양이고, 그러면 「안 돌린 것」이 「돌린 것」보다 낫다.
        /// </summary>
        public float SettlementPower(int spinsRemaining, float requiredPower)
        {
            if (spinsRemaining <= 0 || requiredPower <= 0f) return 0f;
            float raw = requiredPower * PerSpinRatio * spinsRemaining;
            float cap = requiredPower * FloorCapRatio;
            return Mathf.Min(raw, cap);
        }

        /// <summary>정산 전력을 돈으로. `T-05` 「초기 변환처: 돈 또는 장치 수리」의 앞쪽.</summary>
        public float SettlementMoney(int spinsRemaining, float requiredPower)
            => SettlementPower(spinsRemaining, requiredPower) * MoneyPerPower;

        /// <summary>상한에 걸렸는가. UI 가 「상한 도달」을 표시할 근거다.</summary>
        public bool IsCapped(int spinsRemaining, float requiredPower)
        {
            if (spinsRemaining <= 0 || requiredPower <= 0f) return false;
            return requiredPower * PerSpinRatio * spinsRemaining > requiredPower * FloorCapRatio + 0.0001f;
        }

        /// <summary>상한에 닿는 최소 스핀 수. 「몇 회부터 더 남겨도 소용없는가」.</summary>
        public int SpinsToCap =>
            PerSpinRatio <= 0f ? int.MaxValue : Mathf.CeilToInt(FloorCapRatio / PerSpinRatio);
    }
}
