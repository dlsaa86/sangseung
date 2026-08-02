using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 과적 규칙의 수치 3종 (`UP-TECH-09` ⑤). 허용 중량·무게당 요구 전력·과적 배수다.
    ///
    /// ## 왜 `PrototypeConfig` 를 쓰지 않는가 — 두 값은 어긋난 게 아니라 **단위가 다르다**
    ///
    /// 백로그는 이 항목을 「같은 값이 두 벌로 갈라져 있다」로 적었고, 그래서 「그냥 하나로
    /// 합치면 되는 가장 싼 항목」으로 분류했다. **실측은 그렇지 않다.**
    ///
    /// | 출처 | 허용 중량 | 읽는 쪽 |
    /// |---|---|---|
    /// | `PrototypeConfig.asset` | **8** | `ElevatorState.Reset` → `RunController` (T-04·T-06 경로) |
    /// | `FloorSession.AllowedWeight` | **100** | 10층 런 (`RunSession` → `FloorSession`) |
    ///
    /// 100 → 8 은 사고가 아니라 `682bbd0`(「T-04, T-06: 승객 5종·무게 선택과 과적 사고」)의
    /// **의도적 변경**이다. 그 시절 무게는 승객 수에 가까운 단위였고 상한 8이 맞았다.
    /// 지금의 10층 경로는 kg 에 가까운 단위로 돌아간다 — 짐꾼 보너스를 더한 130 이
    /// 테스트와 캡처 리그 전반에 박혀 있다(`무게 120/130`, `218/130`).
    ///
    /// 그래서 두 숫자를 하나로 합치면 **허용 중량이 100에서 8로 떨어지고 전 층이 즉시
    /// 과적이 된다.** 요구 전력에 1.5배가 상시로 걸려 10층 밸런스가 통째로 무너진다.
    /// 백로그가 「가장 싸다」고 적은 작업이 실제로는 밸런스를 파괴하는 작업이었다.
    ///
    /// 결론: 라이브 경로의 수치는 **자기 프로파일**을 갖는다. `PrototypeConfig` 의
    /// `allowedWeight: 8` 은 레거시 경로의 값으로 그 자리에 남긴다 — 그 경로를 지우는 것은
    /// `UP-TEST-11`(미사용 레거시 정리)의 일이지 이 항목의 일이 아니다.
    ///
    /// ## 코드 프리셋이 현재 상수와 같은 이유
    ///
    /// 에셋이 없으면 <see cref="DefaultSnapshot"/> 으로 폴백하고, 그 값은
    /// `FloorSession` 이 지금 쓰는 상수와 **같은 수**다. 따라서 이 파일이 들어와도
    /// 동작은 한 자리도 바뀌지 않는다. 바뀌는 것은 「값이 어디서 왔는가」를 물을 수
    /// 있게 된다는 것뿐이고, 그것이 이 항목이 요구한 전부다.
    /// </summary>
    [CreateAssetMenu(fileName = "WeightProfile",
                     menuName = "Ascend/Profiles/Weight", order = 102)]
    public sealed class WeightProfile : ScriptableObject
    {
        /// <summary>`UP-TECH-09` ⑤ 가 요구하는 항목 수. 대조표가 셀 수 있어야 한다.</summary>
        public const int RequiredFieldCount = 3;

        [Header("과적 (PRD §14.1 ⑤)")]
        [Tooltip("1. 짐꾼 계열 보너스를 더하기 전 기본 허용 중량. 이 값을 넘으면 과적이다. " +
                 "레거시 PrototypeConfig 의 allowedWeight(=8) 와 단위가 다르다 — 저쪽은 승객 수 단위다.")]
        [Min(0f)] [SerializeField] private float _allowedWeight = DefaultAllowedWeight;

        [Tooltip("2. 무게 1단위가 요구 전력에 더하는 양. 0이면 무게가 요구 전력을 전혀 밀지 않아 " +
                 "적재 선택이 무의미해진다.")]
        [Min(0f)] [SerializeField] private float _weightPowerFactor = DefaultWeightPowerFactor;

        [Tooltip("3. 과적일 때 요구 전력에 곱하는 배수. 1.0 이하면 과적에 벌칙이 없어져 " +
                 "과적 경고·사고 연출이 전부 의미를 잃는다.")]
        [Min(0f)] [SerializeField] private float _overloadRequiredPowerMultiplier = DefaultOverloadRequiredPowerMultiplier;

        // ── 코드 기본값 ────────────────────────────────────────────────────────
        // `Run.FloorSession` 의 동명 상수와 **같은 수**여야 한다. 다르면 에셋이 없는
        // 경로와 있는 경로가 서로 다른 게임이 된다. 테스트가 이 일치를 대조한다.
        public const float DefaultAllowedWeight = 100f;
        public const float DefaultWeightPowerFactor = 2f;
        public const float DefaultOverloadRequiredPowerMultiplier = 1.5f;

        public float AllowedWeight => _allowedWeight;
        public float WeightPowerFactor => _weightPowerFactor;
        public float OverloadRequiredPowerMultiplier => _overloadRequiredPowerMultiplier;

        public WeightSnapshot Snapshot()
        {
            return new WeightSnapshot(_allowedWeight, _weightPowerFactor,
                _overloadRequiredPowerMultiplier, name);
        }

        public static WeightSnapshot DefaultSnapshot
        {
            get
            {
                return new WeightSnapshot(DefaultAllowedWeight, DefaultWeightPowerFactor,
                    DefaultOverloadRequiredPowerMultiplier, WeightSnapshot.CodePresetName);
            }
        }

        /// <summary>
        /// 스냅샷을 뜨면서 **3개 필드를 전부 읽는다.**
        ///
        /// 이 저장소가 반복해서 당한 실패는 「만들어졌고 아무도 안 읽는다」와
        /// 「읽지만 값을 바꿔도 아무 일도 안 일어난다」이다. 후자는 값 비교 테스트로
        /// 잡히지 않는다 — 에셋의 기본값이 코드 프리셋과 같으면 어느 쪽을 읽든 통과한다.
        ///
        /// 그래서 두 가지를 남긴다. ① <see cref="WeightSnapshot.SourceName"/> 이
        /// 폴백일 때 「코드 프리셋」이라고 말한다. ② 자기모순이면 경고가 뜬다.
        ///
        /// 확인법: 인스펙터에서 과적 배수를 1.0 으로 내리거나 허용 중량을 0으로 만들면
        /// 플레이 시작 시 콘솔에 경고 한 줄이 뜬다. 안 뜨면 배선이 끊긴 것이다.
        /// </summary>
        public static WeightSnapshot SnapshotOrDefault(WeightProfile profile, string caller)
        {
            if (profile == null)
            {
                // **경고를 띄우지 않는다.** 이 프로파일은 아직 에셋이 없는 것이 정상이고
                // (배선은 씬 소유자의 일이다), 폴백해도 밸런스가 같다. 대신 출처가
                // 「코드 프리셋」으로 남아 하네스가 그 사실을 읽을 수 있다.
                return DefaultSnapshot;
            }

            WeightSnapshot snapshot = profile.Snapshot();
            string problem = snapshot.Validate();
            if (problem != null)
            {
                Debug.LogWarning($"[상승] WeightProfile '{profile.name}' 의 값이 자기모순이다 ({caller}): {problem}"
                                 + $"\n  {snapshot.Describe()}");
            }
            return snapshot;
        }
    }

    /// <summary>
    /// 과적 규칙 3종의 값 사본. 에셋 없이도 판정이 돌아야 하므로 순수 구조체다 —
    /// `FloorSession` 이 `UnityEngine`·`ScriptableObject` 에 의존하지 않는다는 제약을
    /// 이 구조체가 지킨다. 에셋을 든 쪽과 안 든 쪽이 **같은 구조체**를 넘기고,
    /// 판정식은 <see cref="RequiredPowerFor"/> 한 곳에만 있다.
    /// </summary>
    public readonly struct WeightSnapshot
    {
        /// <summary>에셋 없이 코드 값으로 돌 때의 출처 이름. 하네스가 이 문자열을 본다.</summary>
        public const string CodePresetName = "코드 프리셋";

        public readonly float AllowedWeight;
        public readonly float WeightPowerFactor;
        public readonly float OverloadRequiredPowerMultiplier;

        /// <summary>값이 어디서 왔는가. 에셋 이름이거나 <see cref="CodePresetName"/>.</summary>
        public readonly string SourceName;

        public WeightSnapshot(float allowedWeight, float weightPowerFactor,
            float overloadRequiredPowerMultiplier, string sourceName)
        {
            AllowedWeight = allowedWeight;
            WeightPowerFactor = weightPowerFactor;
            OverloadRequiredPowerMultiplier = overloadRequiredPowerMultiplier;
            SourceName = string.IsNullOrEmpty(sourceName) ? CodePresetName : sourceName;
        }

        /// <summary>에셋에서 온 값인가. 거짓이면 코드 프리셋으로 돌고 있다.</summary>
        public bool FromAsset => SourceName != CodePresetName;

        /// <summary>기본 허용 중량 + 짐꾼 보너스. 이 값을 넘으면 과적이다.</summary>
        public float CapacityWith(float bonus) => AllowedWeight + bonus;

        /// <summary>
        /// 이 층의 요구 전력. **과적 판정이 있는 유일한 자리다.**
        ///
        /// 직전 판본은 `FloorSession.ComputeRequiredPower` 의 `private static` 이었고,
        /// 상수 두 개를 직접 읽었다. 그 상태에서는 값을 데이터로 옮겨도 판정식이
        /// 옛 상수를 계속 보므로 「에셋을 바꿔도 게임이 그대로」가 유지된다.
        /// 식과 값이 같이 움직여야 한다.
        /// </summary>
        public float RequiredPowerFor(float basePower, float weight, float capacity)
        {
            float required = basePower + weight * WeightPowerFactor;
            if (weight > capacity)
                required *= OverloadRequiredPowerMultiplier;
            return required;
        }

        /// <summary>
        /// 3개 값이 서로 모순되는지 본다. 문제가 없으면 null.
        ///
        /// 「범위를 벗어났다」가 아니라 **「이 값이면 기능이 조용히 사라진다」**만 잡는다.
        /// 인스펙터 `Min` 이 음수는 이미 막지만, 0 이상 안에서도 과적이라는 개념 자체가
        /// 없어지는 조합이 있다.
        /// </summary>
        public string Validate()
        {
            if (AllowedWeight <= 0f)
                return $"허용 중량이 {AllowedWeight} 이라 첫 적재부터 항상 과적이다";
            if (OverloadRequiredPowerMultiplier <= 1f)
                return $"과적 배수가 {OverloadRequiredPowerMultiplier} 이라 과적에 벌칙이 없다 — 경고·사고 연출이 의미를 잃는다";
            if (WeightPowerFactor <= 0f)
                return $"무게당 요구 전력이 {WeightPowerFactor} 이라 무엇을 실어도 요구 전력이 그대로다 — 적재 선택이 무의미해진다";
            return null;
        }

        public string Describe()
        {
            return $"허용 중량 {AllowedWeight:0.##} · 무게당 요구 전력 {WeightPowerFactor:0.##}"
                 + $" · 과적 배수 {OverloadRequiredPowerMultiplier:0.##} (출처 {SourceName})";
        }
    }
}

// 씬 배선 필요:
//   `RunSessionBehaviour` 의 `_weightProfile` 슬롯에 아래 에셋을 물린다. 배선하지 않아도
//   코드 프리셋으로 **같은 밸런스로** 돌기 때문에 화면에서는 차이가 없다 — 그래서
//   「배선했는가」는 눈으로 못 본다. `RunSessionBehaviour.WeightSource` 가 「코드 프리셋」이
//   아닌 에셋 이름을 찍는지로 확인한다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/WeightProfile.asset
//   (Create ▸ Ascend ▸ Profiles ▸ Weight). 생성 직후 값은 코드 프리셋과 같으므로
//   만들기만 해서는 밸런스가 바뀌지 않는다.
// **하지 말 것**: `PrototypeConfig.asset` 의 `allowedWeight`(=8)를 여기 옮기지 않는다.
//   그쪽은 승객 수 단위인 레거시 경로의 값이고 여기는 kg 단위다. 자세한 근거는 이 파일
//   맨 위 주석에 있다.
