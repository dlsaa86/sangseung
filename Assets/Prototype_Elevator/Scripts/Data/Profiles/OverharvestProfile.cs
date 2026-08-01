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

        /// <summary>
        /// 스냅샷을 뜨면서 **9개 필드를 전부 읽는다.**
        ///
        /// 왜 여기서 검사하는가: 이 저장소가 반복해서 당한 실패는 「만들어졌고 아무도 안
        /// 읽는다」이고, 그 다음이 「읽지만 값을 바꿔도 아무 일도 안 일어난다」이다.
        /// 아직 소비처가 없는 필드(판돈 두 개·해금 임계·감쇠 배율·응시 지연·추가 스핀 상한)는
        /// 그 소비처가 <c>Scripts/Run</c>·<c>Scripts/Player</c>·<c>Scripts/View</c> 에 있어야 해서
        /// 이 파일에서 만들 수 없다. 대신 **자기모순이면 경고가 뜬다** — 값을 망가뜨리면
        /// 런타임에서 실제로 관측되는 변화가 생기고, 그 사실 자체가 「이 에셋은 읽히고 있다」의
        /// 반증 가능한 증거가 된다.
        ///
        /// 확인법: 인스펙터에서 최소 정적을 최대보다 크게 만들거나 추가 스핀 상한을 0으로
        /// 내리면 플레이 시작 시 콘솔에 경고 한 줄이 뜬다. 안 뜨면 배선이 끊긴 것이다.
        /// </summary>
        public static OverharvestSnapshot SnapshotOrDefault(OverharvestProfile profile, string caller)
        {
            if (profile == null)
            {
                Debug.LogWarning($"[상승] OverharvestProfile 이 배선되지 않았다 ({caller}). 코드 기본값으로 진행한다.");
                return DefaultSnapshot;
            }

            OverharvestSnapshot snapshot = profile.Snapshot();
            string problem = snapshot.Validate();
            if (problem != null)
            {
                Debug.LogWarning($"[상승] OverharvestProfile '{profile.name}' 의 값이 자기모순이다 ({caller}): {problem}"
                                 + $"\n  {snapshot.Describe()}");
            }
            return snapshot;
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

        /// <summary>
        /// 9개 값이 서로 모순되는지 본다. 문제가 없으면 null.
        ///
        /// 「범위를 벗어났다」가 아니라 **「이 값이면 기능이 조용히 사라진다」**만 잡는다.
        /// 인스펙터 Range 가 이미 범위는 막아 주지만, 범위 안에서도 과수확 자체가
        /// 없어지는 조합이 있다 — 상한 0, 임계 0, 감쇠 없음이 그것이다.
        /// 그런 고장은 화면에서 「아무 일도 안 일어난다」로만 보이기 때문에 원인을 찾는 데
        /// 가장 오래 걸린다.
        /// </summary>
        public string Validate()
        {
            if (MaxExtraSpins <= 0)
                return $"추가 스핀 상한이 {MaxExtraSpins} 라 과수확 레버가 아무것도 하지 않는다";
            if (AnteRatio <= 0f)
                return $"판돈 비율이 {AnteRatio:0.###} 라 추가 스핀이 공짜다 (PRD §7.2)";
            if (AnteEscalation < 0f)
                return $"판돈 상승률이 음수({AnteEscalation:0.###})라 당길수록 싸진다";
            if (UnlockThreshold <= 0f)
                return $"해금 임계가 {UnlockThreshold:0.###} 라 요구 전력과 무관하게 항상 열린다";
            if (MinSilenceSeconds < 0f)
                return $"정적 최소 길이가 음수({MinSilenceSeconds:0.###}s)다";
            if (MinSilenceSeconds > MaxSilenceSeconds)
                return $"정적 최소 {MinSilenceSeconds:0.###}s 가 최대 {MaxSilenceSeconds:0.###}s 보다 크다";
            if (MaxSilenceSeconds <= 0f)
                return "정적 최대 길이가 0 이라 §7.3 의 정적 구간이 사라진다";
            if (ApproachMachineDuckScale >= 1f)
                return $"접근 감쇠 배율이 {ApproachMachineDuckScale:0.###} 라 접근해도 기계음이 줄지 않는다";
            if (PassengerGazeDelaySeconds < 0f)
                return $"승객 응시 지연이 음수({PassengerGazeDelaySeconds:0.###}s)다";
            if (ResumeFadeSeconds <= 0f)
                return "재개 페이드가 0 이라 기계음이 딱 끊어졌다 돌아온다";
            return null;
        }

        /// <summary>
        /// 9개 값 전부를 한 줄로. 경고에 붙여 「무엇이 들어 있었는지」를 남긴다 —
        /// 경고만 있고 값이 없으면 재현할 수가 없다.
        /// </summary>
        public string Describe()
        {
            return $"앤티 {AnteRatio:0.###}(+{AnteEscalation:0.###}/회) 해금 {UnlockThreshold:0.###} "
                 + $"접근감쇠 {ApproachMachineDuckScale:0.###} 정적 {MinSilenceSeconds:0.###}~{MaxSilenceSeconds:0.###}s "
                 + $"응시지연 {PassengerGazeDelaySeconds:0.###}s 재개 {ResumeFadeSeconds:0.###}s "
                 + $"추가스핀 ≤{MaxExtraSpins}";
        }
    }
}

// 필드별 소비처 대조표 (`UP-POWER-07`). 세어 보라고 만든 표다 — 9 중 3만 살아 있다.
//   1. AnteRatio                 ✗ 없음. `Run/FloorSession` 이 자기 상수를 쓴다(범위 밖 파일).
//   2. AnteEscalation            ✗ 없음. 위와 같음.
//   3. UnlockThreshold           ✗ 없음. `Player/InteractableOverharvestLever._unlocked` 가
//                                   자체 판정한다(범위 밖 파일). `IsUnlocked()` 가 대기 중.
//   4. ApproachMachineDuckScale  ✗ 없음. `Audio/AudioDirector._duckSeconds` 는 시간이고
//                                   이건 배율이라 대체가 아니다(범위 밖 파일).
//   5. MinSilenceSeconds         ✓ `Audio/AudioDirector.Awake`
//   6. MaxSilenceSeconds         ✓ 같음
//   7. PassengerGazeDelaySeconds ✗ 없음. 승객 응시는 `View/`·`Build/` 소유다(범위 밖 파일).
//   8. ResumeFadeSeconds         ✓ `Audio/AudioDirector.Awake`
//   9. MaxExtraSpins             ✗ 없음. `Run/FloorSession` 이 스핀 수를 센다(범위 밖 파일).
//
// ✗ 다섯 개는 **이 파일 안에서 소비처를 만들 수 없다.** 억지로 여기서 읽으면 「읽는 척」이
// 되고, 그건 값을 바꿔도 게임이 그대로인 상태를 한 겹 더 숨길 뿐이다. 대신
// `Validate()`/`Describe()` 가 9개 전부를 실제로 읽고, 값이 기능을 죽이는 조합이면
// 런타임 경고를 낸다 — 그 경고가 「이 에셋이 읽히고 있다」의 반증 가능한 증거다.
//
// 씬 배선 필요:
//   1. `RunSessionBehaviour`(또는 `FloorSession` 생성부)가 `OverharvestProfile` 참조를 받아
//      `anteRatio`/`anteEscalation` 인자로 넘겨야 `UP-POWER-07`의 「값 교체가 코드 수정 없이
//      반영」이 성립한다. 지금은 `FloorSession.DefaultAnteRatio` 상수를 쓴다.
//   2. `Scripts/View/OverharvestUnlockEffect.cs`가 정적 구간·기계음 감쇠·응시 지연·재개 페이드를
//      이 프로파일에서 읽어야 `UP-POWER-06`의 5단계가 데이터로 조정 가능해진다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/OverharvestProfile.asset
