using UnityEngine;
using Ascend.Prototype.Risk;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 위험 4단계가 공간에 만드는 변화를 담는 에셋. `MASTER_PRD.md` §9의 4단계와
    /// `VISUAL_SPEC.md` §11의 「2~3개의 교체 가능한 프리셋」이 근거다 — `UP-RISK-07`.
    ///
    /// <see cref="RiskProfile"/> 구조체는 이미 있고 프리셋 3종도 코드에 있다.
    /// **여기서 값을 다시 쓰지 않는다** — 두 벌이 되면 어느 쪽이 진짜인지 알 수 없고,
    /// 실제로 Warning 색을 두 번의 독립 감사 끝에 고친 이력이 `RiskProfile.cs` 주석에 있다.
    /// 그 수정이 이 에셋에 반영되지 않으면 감사에서 얻은 것을 그대로 잃는다.
    /// 그래서 이 클래스가 하는 일은 하나다: <see cref="ApplyPreset"/>로 코드 프리셋을
    /// 에셋에 **찍어 넣고**, 그 뒤부터는 인스펙터가 원본이 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "DangerFeedbackProfile",
                     menuName = "Ascend/Profiles/Danger Feedback", order = 102)]
    public sealed class DangerFeedbackProfile : ScriptableObject
    {
        /// <summary><see cref="RiskLevel"/>의 단계 수. 배열 길이가 이것과 달라지면 폴백한다.</summary>
        public const int LevelCount = 4;

        [Tooltip("어느 코드 프리셋에서 찍어 왔는가. 값을 손으로 고친 뒤에도 출처를 알 수 있게 남긴다.")]
        [SerializeField] private RiskIntensity _sourcePreset = RiskIntensity.Standard;

        [Tooltip("사람이 읽는 프리셋 이름. 승인 비교 자료에 그대로 실린다(PRD §14 Phase 6).")]
        [SerializeField] private string _presetName = "표준";

        [Tooltip("Stable / Warning / Critical / Collapse 순서. 순서가 RiskLevel 값과 같아야 한다.")]
        [SerializeField] private RiskProfile[] _levels = RiskProfile.Preset(RiskIntensity.Standard);

        public RiskIntensity SourcePreset => _sourcePreset;
        public string PresetName => _presetName;

        /// <summary>인스펙터에서 갓 만든 에셋에 표준 프리셋을 찍어 넣는다. Unity가 호출한다.</summary>
        public void Reset()
        {
            ApplyPreset(RiskIntensity.Standard);
        }

        /// <summary>
        /// 코드 프리셋의 값을 이 에셋에 찍어 넣는다. 프리셋 비교(PRD §14 Phase 6)의 진입점이다.
        /// 프리셋을 바꿔 가며 캡처하려면 이 한 줄만 호출하면 된다.
        /// </summary>
        public void ApplyPreset(RiskIntensity intensity)
        {
            _sourcePreset = intensity;
            _presetName = PresetDisplayName(intensity);
            _levels = RiskProfile.Preset(intensity);
        }

        public static string PresetDisplayName(RiskIntensity intensity)
        {
            switch (intensity)
            {
                case RiskIntensity.Restrained: return "절제";
                case RiskIntensity.Heavy:      return "강함";
                default:                       return "표준";
            }
        }

        /// <summary>
        /// 단계별 프로파일. 배열이 비었거나 길이가 어긋나면 코드 기본값으로 돌아간다 —
        /// 조용히 기본값(0)을 돌려주면 조명이 꺼진 채로 게임이 진행되고, 그것이 데이터
        /// 문제인지 연출 문제인지 화면만 봐서는 구분되지 않는다. 그래서 경고를 남긴다.
        /// </summary>
        public RiskProfile For(RiskLevel level)
        {
            if (_levels == null || _levels.Length < LevelCount)
            {
                Debug.LogWarning($"[상승] DangerFeedbackProfile '{name}' 의 단계 배열이 " +
                                 $"{(_levels == null ? "null" : _levels.Length.ToString())} 개다 " +
                                 $"({LevelCount} 개 필요). 코드 프리셋 {_sourcePreset} 으로 폴백한다.");
                return RiskProfile.Preset(_sourcePreset)[IndexOf(level)];
            }
            return _levels[IndexOf(level)];
        }

        private static int IndexOf(RiskLevel level)
        {
            int index = (int)level;
            if (index < 0) return 0;
            if (index >= LevelCount) return LevelCount - 1;
            return index;
        }

        public DangerFeedbackSnapshot Snapshot()
        {
            var levels = new RiskProfile[LevelCount];
            for (int i = 0; i < LevelCount; i++) levels[i] = For((RiskLevel)i);
            return new DangerFeedbackSnapshot(_sourcePreset, _presetName, levels);
        }

        /// <summary>에셋이 없을 때의 값. 표준 프리셋이 곧 코드 기본값이다.</summary>
        public static DangerFeedbackSnapshot DefaultSnapshot
        {
            get { return SnapshotOf(RiskIntensity.Standard); }
        }

        public static DangerFeedbackSnapshot SnapshotOf(RiskIntensity intensity)
        {
            return new DangerFeedbackSnapshot(intensity, PresetDisplayName(intensity),
                                              RiskProfile.Preset(intensity));
        }

        public static DangerFeedbackSnapshot SnapshotOrDefault(DangerFeedbackProfile profile, string caller)
        {
            if (profile != null) return profile.Snapshot();
            Debug.LogWarning($"[상승] DangerFeedbackProfile 이 배선되지 않았다 ({caller}). " +
                             "코드 프리셋 Standard 로 진행한다.");
            return DefaultSnapshot;
        }
    }

    /// <summary>위험 연출 값의 사본. 연출 코드가 에셋 유무와 무관하게 같은 API를 쓴다.</summary>
    public readonly struct DangerFeedbackSnapshot
    {
        public readonly RiskIntensity SourcePreset;
        public readonly string PresetName;

        private readonly RiskProfile[] _levels;

        public DangerFeedbackSnapshot(RiskIntensity sourcePreset, string presetName, RiskProfile[] levels)
        {
            SourcePreset = sourcePreset;
            PresetName = presetName;
            _levels = levels;
        }

        public RiskProfile For(RiskLevel level)
        {
            RiskProfile[] levels = _levels;
            if (levels == null || levels.Length < DangerFeedbackProfile.LevelCount)
                levels = RiskProfile.Preset(SourcePreset);

            int index = (int)level;
            if (index < 0) index = 0;
            if (index >= levels.Length) index = levels.Length - 1;
            return levels[index];
        }
    }
}

// 씬 배선 필요:
//   `Scripts/Risk/RiskStateView.cs`가 `RiskProfile.Preset(...)` 를 직접 부르는 대신
//   `DangerFeedbackProfile` 참조를 받아 `For(level)` 을 쓰도록 바꿔야 `UP-RISK-07` 의
//   「인스펙터 교체 가능」이 성립한다. 그 파일은 다른 소유 영역이므로 여기서 고치지 않았다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/DangerFeedbackProfile.asset
//   프리셋 비교(PD-07 / PRD Phase 6)를 위해 절제·표준·강함 3개를 각각 만들어 두는 것이 낫다.
