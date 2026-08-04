using Ascend.Prototype.Run;
using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// Mercy / Hunger 등급 임계치 (`DECISION_LOG` D-20260805-01).
    ///
    /// ## 왜 상수가 아니라 에셋인가
    ///
    /// Notion `MASTER PRD` §6.2 가 스스로 못 박았다 — 「등급 임계치는 밸런스 데이터로
    /// 분리하며 위 수치는 **프로토타입 시작값**이다.」 그리고 `CLAUDE.md` 가 「승인 대기
    /// 비주얼·모션·밸런스는 하나의 최종안으로 잠그지 않는다」를 요구한다.
    ///
    /// 이 저장소는 이미 같은 자리에서 한 번 데였다. `RemainingSpinSettlementProfile` 의
    /// 5%/20% 는 「임시 기본값」이라고 적혀 있었지만 **아무도 그 값이 무엇을 만드는지 재지
    /// 않았고**, 재 보니 과수확 선택률이 91.7% 였다 — 「지금 멈춘다」가 선택이 아니었다.
    /// 아래 값들도 **아직 아무도 재지 않았다.** 시작값일 뿐이다.
    ///
    /// ## 이 값이 무엇을 만드는지 재려면
    ///
    /// Mercy 는 「몇 스핀 남기고 멈췄나」, Hunger 는 「얼마나 더 벌었나」다. 두 축이 겨루려면
    /// **같은 층에서 둘 다 도달 가능**해야 한다. 층당 최대 5스핀에서 요구 전력을 2스핀에
    /// 채우면 남은 3 → Mercy III 이고, 대신 3스핀을 더 돌리면 Mercy 는 0 으로 떨어지지만
    /// Hunger 를 노린다. 어느 한쪽이 언제나 우세하면 이 값이 틀린 것이다.
    /// 판정 기준은 `RemainingSpinSettlementProfile` 과 같은 부류여야 한다 —
    /// **한쪽 선택률이 25~75% 밖으로 나가면 재조정.** 아직 스윕을 돌리지 않았다.
    ///
    /// ## 미배선일 때
    ///
    /// 이 저장소의 규약대로 **조용히 죽지 않는다.** 소비처는 프로파일이 없으면
    /// <see cref="MercyHungerThresholds.Default"/> 를 쓰고 그 사실을 출처와 함께 경고한다.
    /// (`ASSUMPTION_LOG` 의 폴백 출처 명명 규칙 · 자체 검증에 그 항목이 있다.)
    /// </summary>
    [CreateAssetMenu(menuName = "Ascend/Profiles/Mercy Hunger",
                     fileName = "MercyHungerProfile")]
    public sealed class MercyHungerProfile : ScriptableObject
    {
        [Header("Mercy — 첫 달성 시점의 남은 스핀 (Notion §6.2 시작값)")]
        [Tooltip("이 스핀 수 이상 남기고 멈추면 Mercy I.")]
        [Min(0)] [SerializeField] private int _mercySpinsForI = 1;

        [Tooltip("이 스핀 수 이상 남기고 멈추면 Mercy II.")]
        [Min(0)] [SerializeField] private int _mercySpinsForII = 2;

        [Tooltip("이 스핀 수 이상 남기고 멈추면 Mercy III.")]
        [Min(0)] [SerializeField] private int _mercySpinsForIII = 3;

        [Header("Hunger — 요구 전력 대비 초과 비율 (0.25 = 25%)")]
        [Min(0f)] [SerializeField] private float _hungerExcessForI = 0.25f;
        [Min(0f)] [SerializeField] private float _hungerExcessForII = 0.60f;
        [Min(0f)] [SerializeField] private float _hungerExcessForIII = 1.00f;

        public MercyHungerThresholds Thresholds => new MercyHungerThresholds(
            _mercySpinsForI, _mercySpinsForII, _mercySpinsForIII,
            _hungerExcessForI, _hungerExcessForII, _hungerExcessForIII);

        /// <summary>
        /// 프로파일이 없을 때 쓸 값과 **그 출처의 이름**. 문자열을 함께 돌려주는 이유는
        /// 이 저장소가 「어디서 온 값인지 모르는 폴백」으로 여러 번 시간을 버렸기 때문이다.
        /// </summary>
        public static MercyHungerThresholds Fallback(out string source)
        {
            source = nameof(MercyHungerThresholds) + "." + nameof(MercyHungerThresholds.Default);
            return MercyHungerThresholds.Default;
        }

        private void OnValidate()
        {
            // 단조성이 깨지면 등급이 조용히 건너뛴다. 저장하는 순간 잡는다.
            if (_mercySpinsForII < _mercySpinsForI) _mercySpinsForII = _mercySpinsForI;
            if (_mercySpinsForIII < _mercySpinsForII) _mercySpinsForIII = _mercySpinsForII;
            if (_hungerExcessForII < _hungerExcessForI) _hungerExcessForII = _hungerExcessForI;
            if (_hungerExcessForIII < _hungerExcessForII) _hungerExcessForIII = _hungerExcessForII;
        }
    }
}
