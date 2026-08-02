using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 층별 밸런스 곡선 (`UP-TECH-09` ④ 층별 요구 전력). 10층 각각의 요구 전력·스핀 수·
    /// 저항 총량 배율 셋을 담는다.
    ///
    /// ## 왜 「근거 주석이 끊긴다」를 이유로 미루지 않았나
    ///
    /// 이 항목을 미뤄 둔 이유는 `FloorPlan._tenFloors` 의 각 숫자에 **근거가 주석으로
    /// 붙어 있다**는 것이었다 — 「2층 저항 배율 1.6 은 기본 밀도에서 직선이 스핀당
    /// 0.08회라 5스핀이면 기대 0.4회, 즉 셋 중 둘이 직선을 가르치는 층에서 직선을
    /// 못 본다」 같은 것. 인스펙터로 옮기면 그 문장이 사라진다.
    ///
    /// 해결은 **옮기지 않는 것**이다. `_tenFloors` 는 근거와 함께 **코드 프리셋**으로
    /// 그대로 남고, 이 에셋은 그 위에 얹히는 **덮어쓰기 경로**다. 에셋이 없으면 곡선은
    /// 한 자리도 바뀌지 않는다. <see cref="FloorCurriculumProfile.Reset"/> 이 프리셋에서
    /// 값을 읽어 채우므로, 편집자는 빈 배열이 아니라 현재 곡선을 보고 시작한다.
    ///
    /// 이 저장소의 다른 프로파일이 전부 같은 모양이다 — 코드가 프리셋을 들고, 에셋은
    /// 교체 경로다. ⑪ 에서 배운 것도 같다: 에셋이 **대체**가 아니라 **얹히는 것**이어야
    /// 코드가 이름 붙은 기본을 계속 알 수 있다.
    ///
    /// ## 무엇이 여기 없는가
    ///
    /// 층별 심볼 풀·계약 선택지·빌드 보상 여부는 **커리큘럼의 구조**지 밸런스 다이얼이
    /// 아니다. 3층에서 계약 선택지를 없애면 난이도가 바뀌는 게 아니라 4층이 가르칠 것을
    /// 잃는다. `SpinBalanceProfile` 이 연쇄 하드 캡을 뺀 것과 같은 구분이다.
    /// </summary>
    [CreateAssetMenu(fileName = "FloorCurriculumProfile",
                     menuName = "Ascend/Profiles/FloorCurriculum", order = 105)]
    public sealed class FloorCurriculumProfile : ScriptableObject
    {
        /// <summary>10층 커리큘럼. 배열 길이가 이보다 짧으면 그 층은 프리셋으로 폴백한다.</summary>
        public const int FloorCount = 10;

        [Header("층별 요구 전력 (PRD §14.1 ④)")]
        [Tooltip("1층부터 10층까지의 요구 전력. 길이가 10이 아니면 통째로 무시하고 " +
                 "코드 프리셋으로 돌아간다 — 일부만 맞는 배열은 어느 층이 데이터에서 왔는지 " +
                 "알 수 없게 만든다.")]
        [SerializeField] private float[] _requiredPower = System.Array.Empty<float>();

        [Tooltip("층별 스핀 수. 0 이하인 칸은 그 층만 프리셋으로 폴백한다 — " +
                 "스핀 0은 층을 시작조차 못 하게 만든다.")]
        [SerializeField] private int[] _spins = System.Array.Empty<int>();

        [Tooltip("층별 저항 총량 배율. 0 이하인 칸은 그 층만 프리셋으로 폴백한다 " +
                 "(FloorPlan 은 0 을 「명시 안 함」으로 읽는다).")]
        [SerializeField] private float[] _resistanceWeightScale = System.Array.Empty<float>();

        public FloorCurriculumSnapshot Snapshot()
        {
            return new FloorCurriculumSnapshot(_requiredPower, _spins, _resistanceWeightScale, name);
        }

        /// <summary>
        /// 에셋 없이 도는 경로. **빈 배열**이라 모든 층이 프리셋을 그대로 쓴다 —
        /// 「덮어쓰지 않는다」가 기본값이어야 에셋이 없는 것과 있는 것이 같은 게임이 된다.
        /// </summary>
        public static FloorCurriculumSnapshot DefaultSnapshot =>
            new FloorCurriculumSnapshot(null, null, null, FloorCurriculumSnapshot.CodePresetName);

        public static FloorCurriculumSnapshot SnapshotOrDefault(FloorCurriculumProfile profile, string caller)
        {
            if (profile == null) return DefaultSnapshot;

            FloorCurriculumSnapshot snapshot = profile.Snapshot();
            string problem = snapshot.Validate();
            if (problem != null)
            {
                Debug.LogWarning($"[상승] FloorCurriculumProfile '{profile.name}' 의 값이 자기모순이다 ({caller}): {problem}"
                                 + $"\n  {snapshot.Describe()}");
            }
            return snapshot;
        }

        /// <summary>
        /// 현재 코드 곡선을 그대로 읽어 채운다. Unity가 에셋 생성·인스펙터 Reset에서 부른다.
        ///
        /// **빈 배열로 두지 않는 이유**는 `PassengerReactionSet.Reset` 과 같다 —
        /// 「이벤트별 교체 가능」은 편집자가 빈 칸 열 개를 손으로 만드는 것이 아니라
        /// 이미 있는 값을 고치는 것이어야 한다. 그리고 빈 에셋은 폴백 덕분에 조용히
        /// 동작하므로 「아직 안 채웠다」를 아무도 눈치채지 못한다.
        /// </summary>
        public void Reset()
        {
            var power = new float[FloorCount];
            var spins = new int[FloorCount];
            var scale = new float[FloorCount];
            for (int i = 0; i < FloorCount; i++)
            {
                Ascend.Prototype.Spin.FloorPlan plan = Ascend.Prototype.Spin.PrototypeCurriculum.For(i + 1);
                power[i] = plan.RequiredPower;
                spins[i] = plan.Spins;
                scale[i] = plan.ResistanceWeightScale;
            }
            _requiredPower = power;
            _spins = spins;
            _resistanceWeightScale = scale;
        }
    }

    /// <summary>
    /// 층별 곡선의 값 사본. `FloorPlan`·`TenFloorSource` 는 순수 C# 이라
    /// `ScriptableObject` 를 알면 안 된다 — 이 구조체가 그 경계다.
    ///
    /// **덮어쓰기지 대체가 아니다.** 비어 있는 축은 프리셋을 그대로 통과시킨다.
    /// </summary>
    public readonly struct FloorCurriculumSnapshot
    {
        public const string CodePresetName = "코드 프리셋";

        private readonly float[] _requiredPower;
        private readonly int[] _spins;
        private readonly float[] _resistanceWeightScale;

        public readonly string SourceName;

        public FloorCurriculumSnapshot(float[] requiredPower, int[] spins,
            float[] resistanceWeightScale, string sourceName)
        {
            _requiredPower = requiredPower;
            _spins = spins;
            _resistanceWeightScale = resistanceWeightScale;
            SourceName = string.IsNullOrEmpty(sourceName) ? CodePresetName : sourceName;
        }

        public bool FromAsset => SourceName != CodePresetName;

        /// <summary>어느 축이라도 실제로 덮어쓰는가. 전부 비어 있으면 거짓.</summary>
        public bool OverridesAnything =>
            Usable(_requiredPower) || Usable(_spins) || Usable(_resistanceWeightScale);

        private static bool Usable(float[] a) => a != null && a.Length == FloorCurriculumProfile.FloorCount;
        private static bool Usable(int[] a) => a != null && a.Length == FloorCurriculumProfile.FloorCount;

        /// <summary>
        /// 층 계획에 곡선을 덮어쓴다. **비어 있는 축은 건드리지 않는다.**
        ///
        /// 길이가 10이 아니면 그 축을 통째로 무시한다 — 일부만 맞는 배열을 부분 적용하면
        /// 어느 층이 데이터에서 왔는지 알 수 없게 되고, 그 상태가 이 항목이 막으려는
        /// 바로 그것이다.
        /// </summary>
        public Ascend.Prototype.Spin.FloorPlan Apply(Ascend.Prototype.Spin.FloorPlan plan)
        {
            int index = plan.Floor - 1;
            if (index < 0 || index >= FloorCurriculumProfile.FloorCount) return plan;

            if (Usable(_requiredPower) && _requiredPower[index] > 0f)
                plan.RequiredPower = _requiredPower[index];
            // 스핀 0은 층을 시작조차 못 하게 만든다(`FloorSession` 이 예외를 던진다).
            // 그래서 0 이하는 「이 칸은 안 채웠다」로 읽는다.
            if (Usable(_spins) && _spins[index] > 0)
                plan.Spins = _spins[index];
            // `FloorPlan` 은 0 을 「명시 안 함」으로 읽어 저항 종류 수를 배율로 쓴다.
            // 그 뜻을 여기서도 지킨다.
            if (Usable(_resistanceWeightScale) && _resistanceWeightScale[index] > 0f)
                plan.ResistanceWeightScale = _resistanceWeightScale[index];
            return plan;
        }

        /// <summary>
        /// 「이 값이면 층이 성립하지 않는다」만 잡는다. 문제가 없으면 null.
        /// 빈 배열은 문제가 아니다 — 덮어쓰지 않겠다는 뜻이다.
        /// </summary>
        public string Validate()
        {
            if (_requiredPower != null && _requiredPower.Length != 0
                && _requiredPower.Length != FloorCurriculumProfile.FloorCount)
                return $"요구 전력 배열이 {_requiredPower.Length}칸이다 — 10칸이거나 비어 있어야 한다";
            if (_spins != null && _spins.Length != 0
                && _spins.Length != FloorCurriculumProfile.FloorCount)
                return $"스핀 배열이 {_spins.Length}칸이다 — 10칸이거나 비어 있어야 한다";
            if (_resistanceWeightScale != null && _resistanceWeightScale.Length != 0
                && _resistanceWeightScale.Length != FloorCurriculumProfile.FloorCount)
                return $"저항 배율 배열이 {_resistanceWeightScale.Length}칸이다 — 10칸이거나 비어 있어야 한다";

            if (Usable(_requiredPower))
            {
                for (int i = 0; i < _requiredPower.Length; i++)
                    if (_requiredPower[i] < 0f)
                        return $"{i + 1}층 요구 전력이 음수({_requiredPower[i]})다";
            }
            return null;
        }

        public string Describe()
        {
            string power = Usable(_requiredPower)
                ? string.Join("/", System.Array.ConvertAll(_requiredPower, v => v.ToString("0")))
                : "(프리셋)";
            return $"요구 전력 {power} · 덮어쓰는 축 {(OverridesAnything ? "있음" : "없음")} (출처 {SourceName})";
        }
    }
}

// 씬 배선 필요:
//   `RunSessionBehaviour` 의 `_floorCurriculum` 슬롯에 아래 에셋을 물린다. 배선하지
//   않아도 코드 곡선으로 그대로 돌기 때문에 화면에서는 차이가 없다 —
//   `RunSessionBehaviour.FloorCurriculumSource` 가 「코드 프리셋」이 아닌 에셋 이름을
//   찍는지로 확인한다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/FloorCurriculumProfile.asset
//   (Create ▸ Ascend ▸ Profiles ▸ FloorCurriculum). 생성 시 Reset() 이 현재 곡선을
//   그대로 채우므로 만들기만 해서는 밸런스가 바뀌지 않는다.
// **하지 말 것**: 층별 심볼 풀·계약 선택지·빌드 보상 여부를 이 에셋에 추가하지 않는다.
//   그건 커리큘럼의 구조이지 밸런스 다이얼이 아니다 — 3층에서 계약 선택지를 없애면
//   난이도가 바뀌는 게 아니라 4층이 가르칠 것을 잃는다.
