using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 과수확 레버의 수치 9종. `MASTER_PRD.md` §7이 요구하는 것은 "점수 버튼"이 아니라
    /// **공간적 사건**이고, 그 사건의 길이·감쇠·판돈이 지금은 세 파일에 흩어져 있다
    /// (`FloorSession`의 앤티 상수, `OverchargeOption`, 연출 코드의 시간값) — `UP-POWER-07`.
    ///
    /// 흩어진 상태의 실제 비용: 정적 구간을 0.5초로 바꾸려면 어느 파일을 고쳐야 하는지
    /// 아무도 모르고, 고친 뒤 판돈 계산이 같이 움직였는지도 알 수 없다.
    /// `MASTER_PRD.md` §1 「가변 요소는 데이터 또는 프로파일로 분리한다」가 막는 상태다.
    ///
    /// 앤티 계산식은 <see cref="Ascend.Prototype.Run.FloorSession"/>과 **같아야 한다.**
    /// 여기서 다시 구현한 것이 아니라 같은 식을 옮겨 적은 것이고, 테스트가 두 값을 대조한다.
    /// </summary>
    [CreateAssetMenu(fileName = "OverharvestProfile",
                     menuName = "Ascend/Profiles/Overharvest", order = 101)]
    public sealed class OverharvestProfile : ScriptableObject
    {
        /// <summary>`UP-POWER-07`이 요구하는 항목 수. 대조표가 셀 수 있어야 한다.</summary>
        public const int RequiredFieldCount = 9;

        [Header("판돈 (PRD §7.2)")]
        [Tooltip("1. 첫 추가 스핀에 거는 현재 전력의 비율.")]
        [Range(0f, 1f)] [SerializeField] private float _anteRatio = DefaultAnteRatio;

        [Tooltip("2. 당길 때마다 판돈 비율이 오르는 정도. 0.35면 2회째에 1.35배가 된다.")]
        [Range(0f, 2f)] [SerializeField] private float _anteEscalation = DefaultAnteEscalation;

        [Tooltip("3. 잠금이 풀리는 달성률. 1.00 = 요구 전력 100% (PRD §7.1).")]
        [Range(0f, 2f)] [SerializeField] private float _unlockThreshold = DefaultUnlockThreshold;

        [Header("접근 연출 (PRD §7.3의 5단계)")]
        [Tooltip("4. 레버에 접근했을 때 기계음에 곱하는 배율. 0.35면 35%로 줄어든다 — 0은 접근 순간부터 완전 무음이라 정적 구간과 구분되지 않는다.")]
        [Range(0f, 1f)] [SerializeField] private float _approachMachineDuckScale = DefaultApproachMachineDuckScale;

        [Tooltip("5. 정적 구간 최소 길이(초).")]
        [Range(0f, 3f)] [SerializeField] private float _minSilenceSeconds = DefaultMinSilenceSeconds;

        [Tooltip("6. 정적 구간 최대 길이(초). 이보다 길면 플레이어가 「멈췄나?」로 읽는다.")]
        [Range(0f, 3f)] [SerializeField] private float _maxSilenceSeconds = DefaultMaxSilenceSeconds;

        [Tooltip("7. 접근 후 승객이 플레이어를 응시하기까지의 지연(초). 즉시 돌아보면 스크립트로 보인다.")]
        [Range(0f, 2f)] [SerializeField] private float _passengerGazeDelaySeconds = DefaultPassengerGazeDelaySeconds;

        [Tooltip("8. 정적 이후 기계음이 원래 크기로 돌아오는 시간(초).")]
        [Range(0f, 3f)] [SerializeField] private float _resumeFadeSeconds = DefaultResumeFadeSeconds;

        [Header("상한")]
        [Tooltip("9. 한 층에서 허용하는 추가 스핀 수. 실효 상한은 남은 스핀과의 최솟값이다.")]
        [Range(0, 8)] [SerializeField] private int _maxExtraSpins = DefaultMaxExtraSpins;

        // ── 코드 기본값 ────────────────────────────────────────────────────────
        // 앤티 두 값은 `FloorSession.DefaultAnteRatio`/`DefaultAnteEscalation`과 같은 수다.
        // 에셋이 생겨도 밸런스가 조용히 바뀌지 않도록 현재 값을 그대로 가져왔다.
        public const float DefaultAnteRatio = 0.12f;
        public const float DefaultAnteEscalation = 0.35f;
        public const float DefaultUnlockThreshold = 1.00f;
        public const float DefaultApproachMachineDuckScale = 0.35f;
        public const float DefaultMinSilenceSeconds = 0.3f;
        public const float DefaultMaxSilenceSeconds = 0.7f;
        public const float DefaultPassengerGazeDelaySeconds = 0.18f;
        public const float DefaultResumeFadeSeconds = 0.25f;

        /// <summary>PRD §4.1 「한 층 최대 5회 스핀」에서 첫 스핀을 뺀 값.</summary>
        public const int DefaultMaxExtraSpins = 4;

        public float AnteRatio => _anteRatio;
        public float AnteEscalation => _anteEscalation;
        public float UnlockThreshold => _unlockThreshold;
        public float ApproachMachineDuckScale => _approachMachineDuckScale;
        public float MinSilenceSeconds => _minSilenceSeconds;
        public float MaxSilenceSeconds => _maxSilenceSeconds;
        public float PassengerGazeDelaySeconds => _passengerGazeDelaySeconds;
        public float ResumeFadeSeconds => _resumeFadeSeconds;
        public int MaxExtraSpins => _maxExtraSpins;

        public void Reset()
        {
            _anteRatio = DefaultAnteRatio;
            _anteEscalation = DefaultAnteEscalation;
            _unlockThreshold = DefaultUnlockThreshold;
            _approachMachineDuckScale = DefaultApproachMachineDuckScale;
            _minSilenceSeconds = DefaultMinSilenceSeconds;
            _maxSilenceSeconds = DefaultMaxSilenceSeconds;
            _passengerGazeDelaySeconds = DefaultPassengerGazeDelaySeconds;
            _resumeFadeSeconds = DefaultResumeFadeSeconds;
            _maxExtraSpins = DefaultMaxExtraSpins;
        }

        public OverharvestSnapshot Snapshot()
        {
            return new OverharvestSnapshot(_anteRatio, _anteEscalation, _unlockThreshold,
                _approachMachineDuckScale, _minSilenceSeconds, _maxSilenceSeconds,
                _passengerGazeDelaySeconds, _resumeFadeSeconds, _maxExtraSpins);
        }

        public static OverharvestSnapshot DefaultSnapshot
        {
            get
            {
                return new OverharvestSnapshot(DefaultAnteRatio, DefaultAnteEscalation,
                    DefaultUnlockThreshold, DefaultApproachMachineDuckScale,
                    DefaultMinSilenceSeconds, DefaultMaxSilenceSeconds,
                    DefaultPassengerGazeDelaySeconds, DefaultResumeFadeSeconds,
                    DefaultMaxExtraSpins);
            }
        }

        public static OverharvestSnapshot SnapshotOrDefault(OverharvestProfile profile, string caller)
        {
            if (profile != null) return profile.Snapshot();
            Debug.LogWarning($"[상승] OverharvestProfile 이 배선되지 않았다 ({caller}). 코드 기본값으로 진행한다.");
            return DefaultSnapshot;
        }

        // 자주 쓰는 헬퍼는 스냅샷으로 위임한다. 에셋을 든 쪽과 안 든 쪽이 같은 답을 내야 한다.
        public float ClampedSilenceSeconds(float seconds) => Snapshot().ClampedSilenceSeconds(seconds);
        public float AnteRatioForPull(int extraSpinsAlreadyTaken) => Snapshot().AnteRatioForPull(extraSpinsAlreadyTaken);
    }

    /// <summary>과수확 수치의 값 사본. 에셋 없이도 연출·판정이 돌아야 한다.</summary>
    public readonly struct OverharvestSnapshot
    {
        public readonly float AnteRatio;
        public readonly float AnteEscalation;
        public readonly float UnlockThreshold;
        public readonly float ApproachMachineDuckScale;
        public readonly float MinSilenceSeconds;
        public readonly float MaxSilenceSeconds;
        public readonly float PassengerGazeDelaySeconds;
        public readonly float ResumeFadeSeconds;
        public readonly int MaxExtraSpins;

        public OverharvestSnapshot(float anteRatio, float anteEscalation, float unlockThreshold,
            float approachMachineDuckScale, float minSilenceSeconds, float maxSilenceSeconds,
            float passengerGazeDelaySeconds, float resumeFadeSeconds, int maxExtraSpins)
        {
            AnteRatio = anteRatio;
            AnteEscalation = anteEscalation;
            UnlockThreshold = unlockThreshold;
            ApproachMachineDuckScale = approachMachineDuckScale;
            MinSilenceSeconds = minSilenceSeconds;
            MaxSilenceSeconds = maxSilenceSeconds;
            PassengerGazeDelaySeconds = passengerGazeDelaySeconds;
            ResumeFadeSeconds = resumeFadeSeconds;
            MaxExtraSpins = maxExtraSpins;
        }

        /// <summary>
        /// 정적 구간 길이를 허용 범위로 조인다. 인스펙터에서 최소 &gt; 최대로 뒤집혀 있어도
        /// 무한 정적이 되지 않도록 순서를 먼저 바로잡는다 — 잘못된 데이터가 게임을
        /// 멈춰 세우는 것이 이 게임에서 가장 알아채기 어려운 고장이다.
        /// </summary>
        public float ClampedSilenceSeconds(float seconds)
        {
            float low = MinSilenceSeconds <= MaxSilenceSeconds ? MinSilenceSeconds : MaxSilenceSeconds;
            float high = MinSilenceSeconds <= MaxSilenceSeconds ? MaxSilenceSeconds : MinSilenceSeconds;
            if (low < 0f) low = 0f;
            if (high < low) high = low;
            if (seconds < low) return low;
            if (seconds > high) return high;
            return seconds;
        }

        /// <summary>
        /// 시드에서 정적 구간 길이를 뽑는다. `UnityEngine.Random`을 쓰지 않는 이유는
        /// 같은 시드가 같은 연출 길이를 내야 캡처가 재현되기 때문이다(`TECH_SPEC.md` §14).
        /// </summary>
        public float SilenceSecondsFor(int seed)
        {
            var random = new System.Random(seed);
            float t = (float)random.NextDouble();
            float low = MinSilenceSeconds <= MaxSilenceSeconds ? MinSilenceSeconds : MaxSilenceSeconds;
            float high = MinSilenceSeconds <= MaxSilenceSeconds ? MaxSilenceSeconds : MinSilenceSeconds;
            return low + (high - low) * t;
        }

        /// <summary>
        /// 다음 당김에 적용되는 판돈 비율. <c>FloorSession.AnteRatioForNextSpin</c>과 같은 식이다.
        /// </summary>
        public float AnteRatioForPull(int extraSpinsAlreadyTaken)
        {
            int taken = extraSpinsAlreadyTaken > 0 ? extraSpinsAlreadyTaken : 0;
            return AnteRatio * (1f + AnteEscalation * taken);
        }

        /// <summary>실제로 빠져나가는 전력. 판돈은 선택 시점에 지불된다.</summary>
        public float AnteFor(float currentPower, int extraSpinsAlreadyTaken)
        {
            float power = currentPower > 0f ? currentPower : 0f;
            return power * AnteRatioForPull(extraSpinsAlreadyTaken);
        }

        /// <summary>잠금이 풀렸는가. 달성률(현재/요구)로 판정한다.</summary>
        public bool IsUnlocked(float currentPower, float requiredPower)
        {
            if (requiredPower <= 0.0001f) return true;
            return currentPower / requiredPower >= UnlockThreshold;
        }

        /// <summary>남은 스핀과 프로파일 상한 중 작은 쪽. 둘 다 지켜야 한다.</summary>
        public int EffectiveExtraSpinLimit(int spinsRemaining)
        {
            int remaining = spinsRemaining > 0 ? spinsRemaining : 0;
            return MaxExtraSpins < remaining ? MaxExtraSpins : remaining;
        }
    }
}

// 씬 배선 필요:
//   1. `RunSessionBehaviour`(또는 `FloorSession` 생성부)가 `OverharvestProfile` 참조를 받아
//      `anteRatio`/`anteEscalation` 인자로 넘겨야 `UP-POWER-07`의 「값 교체가 코드 수정 없이
//      반영」이 성립한다. 지금은 `FloorSession.DefaultAnteRatio` 상수를 쓴다.
//   2. `Scripts/View/OverharvestUnlockEffect.cs`가 정적 구간·기계음 감쇠·응시 지연·재개 페이드를
//      이 프로파일에서 읽어야 `UP-POWER-06`의 5단계가 데이터로 조정 가능해진다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/OverharvestProfile.asset
