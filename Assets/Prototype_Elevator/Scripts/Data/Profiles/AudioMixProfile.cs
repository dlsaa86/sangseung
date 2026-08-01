using UnityEngine;
using Ascend.Prototype.Risk;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 소리가 나뉘는 다섯 갈래. `MASTER_PRD.md` §9가 "특정 감각 채널 하나에만 의존하지
    /// 않는다"고 못박는데, 소리 안에서도 갈래가 나뉘어 있지 않으면 과수확 정적에서
    /// **전부 같이 사라진다** — 그러면 정적이 연출이 아니라 고장으로 읽힌다.
    /// </summary>
    public enum AudioChannel
    {
        /// <summary>전체 배수. 옵션 메뉴의 마스터 볼륨이다.</summary>
        Master = 0,

        /// <summary>기계 험, 케이블, 모터. 상시 깔리는 바닥.</summary>
        Machine = 1,

        /// <summary>정화·캐스케이드·임계점 같은 사건음.</summary>
        Event = 2,

        /// <summary>승객의 비언어 반응과 발소리.</summary>
        Passenger = 3,

        /// <summary>경고음·사이렌. 접근성 옵션이 따로 끌 수 있어야 한다.</summary>
        Warning = 4,
    }

    /// <summary>
    /// 채널별 기본 볼륨과 과수확 정적 시의 감쇠 배율. `UP-AUD-05`.
    ///
    /// 위험 단계별 험 값을 여기 **다시 적지 않는다.** 그 값은
    /// <see cref="DangerFeedbackProfile"/>이 들고 있고, 여기 있는 것은 그 위에 곱하는
    /// 배율뿐이다. 두 곳에 절대값을 적으면 위험 단계를 조정할 때 소리만 옛 값에 남아
    /// `MASTER_PRD.md` §9의 "조명·사운드·진동이 동기화되어야 한다"가 조용히 깨진다.
    /// 기본 배율이 전부 1인 것은 실수가 아니라 "형태는 위험 프로파일이 정한다"는 선언이다.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioMixProfile",
                     menuName = "Ascend/Profiles/Audio Mix", order = 104)]
    public sealed class AudioMixProfile : ScriptableObject
    {
        [Header("기본 볼륨")]
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = DefaultMasterVolume;
        [Range(0f, 1f)] [SerializeField] private float _machineVolume = DefaultMachineVolume;
        [Range(0f, 1f)] [SerializeField] private float _eventVolume = DefaultEventVolume;
        [Range(0f, 1f)] [SerializeField] private float _passengerVolume = DefaultPassengerVolume;
        [Range(0f, 1f)] [SerializeField] private float _warningVolume = DefaultWarningVolume;

        [Header("과수확 정적 시 감쇠 배율 (PRD §7.3)")]
        [Tooltip("마스터는 건드리지 않는다. 마스터를 줄이면 플레이어 자신의 발소리까지 사라져 「음소거됐나」로 읽힌다.")]
        [Range(0f, 1f)] [SerializeField] private float _masterDuck = DefaultMasterDuck;

        [Tooltip("기계음. 정적의 주인공이라 가장 깊게 내린다.")]
        [Range(0f, 1f)] [SerializeField] private float _machineDuck = DefaultMachineDuck;

        [Range(0f, 1f)] [SerializeField] private float _eventDuck = DefaultEventDuck;

        [Tooltip("승객. 완전히 지우지 않는다 — 정적 구간에 남는 유일한 소리가 숨소리여야 「보고 있다」가 전달된다.")]
        [Range(0f, 1f)] [SerializeField] private float _passengerDuck = DefaultPassengerDuck;

        [Range(0f, 1f)] [SerializeField] private float _warningDuck = DefaultWarningDuck;

        [Header("위험 단계별 험 배율 (DangerFeedbackProfile 값 위에 곱한다)")]
        [Tooltip("Stable / Warning / Critical / Collapse 순서.")]
        [SerializeField] private float[] _humVolumeScale = { 1f, 1f, 1f, 1f };

        [SerializeField] private float[] _humPitchScale = { 1f, 1f, 1f, 1f };

        public const float DefaultMasterVolume = 1.00f;
        public const float DefaultMachineVolume = 0.55f;
        public const float DefaultEventVolume = 0.80f;
        public const float DefaultPassengerVolume = 0.70f;
        public const float DefaultWarningVolume = 0.75f;

        public const float DefaultMasterDuck = 1.00f;
        public const float DefaultMachineDuck = 0.05f;
        public const float DefaultEventDuck = 0.10f;
        public const float DefaultPassengerDuck = 0.35f;
        public const float DefaultWarningDuck = 0.15f;

        public void Reset()
        {
            _masterVolume = DefaultMasterVolume;
            _machineVolume = DefaultMachineVolume;
            _eventVolume = DefaultEventVolume;
            _passengerVolume = DefaultPassengerVolume;
            _warningVolume = DefaultWarningVolume;

            _masterDuck = DefaultMasterDuck;
            _machineDuck = DefaultMachineDuck;
            _eventDuck = DefaultEventDuck;
            _passengerDuck = DefaultPassengerDuck;
            _warningDuck = DefaultWarningDuck;

            _humVolumeScale = new[] { 1f, 1f, 1f, 1f };
            _humPitchScale = new[] { 1f, 1f, 1f, 1f };
        }

        public AudioMixSnapshot Snapshot()
        {
            return new AudioMixSnapshot(
                _masterVolume, _machineVolume, _eventVolume, _passengerVolume, _warningVolume,
                _masterDuck, _machineDuck, _eventDuck, _passengerDuck, _warningDuck,
                Scale(_humVolumeScale, 0), Scale(_humVolumeScale, 1), Scale(_humVolumeScale, 2), Scale(_humVolumeScale, 3),
                Scale(_humPitchScale, 0), Scale(_humPitchScale, 1), Scale(_humPitchScale, 2), Scale(_humPitchScale, 3));
        }

        /// <summary>배열이 짧으면 1을 돌려준다. 배수의 중립값은 0이 아니라 1이다 — 0이면 무음이 된다.</summary>
        private static float Scale(float[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length) return 1f;
            return values[index];
        }

        public static AudioMixSnapshot DefaultSnapshot
        {
            get
            {
                return new AudioMixSnapshot(
                    DefaultMasterVolume, DefaultMachineVolume, DefaultEventVolume,
                    DefaultPassengerVolume, DefaultWarningVolume,
                    DefaultMasterDuck, DefaultMachineDuck, DefaultEventDuck,
                    DefaultPassengerDuck, DefaultWarningDuck,
                    1f, 1f, 1f, 1f,
                    1f, 1f, 1f, 1f);
            }
        }

        public static AudioMixSnapshot SnapshotOrDefault(AudioMixProfile profile, string caller)
        {
            if (profile != null) return profile.Snapshot();
            Debug.LogWarning($"[상승] AudioMixProfile 이 배선되지 않았다 ({caller}). 코드 기본 믹스로 진행한다.");
            return DefaultSnapshot;
        }

        public float VolumeFor(AudioChannel channel) => Snapshot().VolumeFor(channel);
        public float DuckedVolumeFor(AudioChannel channel) => Snapshot().DuckedVolumeFor(channel);
    }

    /// <summary>믹스 값의 사본. 오디오 코드가 에셋 유무와 무관하게 같은 API를 쓴다.</summary>
    public readonly struct AudioMixSnapshot
    {
        public readonly float MasterVolume;
        public readonly float MachineVolume;
        public readonly float EventVolume;
        public readonly float PassengerVolume;
        public readonly float WarningVolume;

        public readonly float MasterDuck;
        public readonly float MachineDuck;
        public readonly float EventDuck;
        public readonly float PassengerDuck;
        public readonly float WarningDuck;

        private readonly float _humVolumeStable;
        private readonly float _humVolumeWarning;
        private readonly float _humVolumeCritical;
        private readonly float _humVolumeCollapse;

        private readonly float _humPitchStable;
        private readonly float _humPitchWarning;
        private readonly float _humPitchCritical;
        private readonly float _humPitchCollapse;

        public AudioMixSnapshot(
            float masterVolume, float machineVolume, float eventVolume,
            float passengerVolume, float warningVolume,
            float masterDuck, float machineDuck, float eventDuck,
            float passengerDuck, float warningDuck,
            float humVolumeStable, float humVolumeWarning, float humVolumeCritical, float humVolumeCollapse,
            float humPitchStable, float humPitchWarning, float humPitchCritical, float humPitchCollapse)
        {
            MasterVolume = masterVolume;
            MachineVolume = machineVolume;
            EventVolume = eventVolume;
            PassengerVolume = passengerVolume;
            WarningVolume = warningVolume;

            MasterDuck = masterDuck;
            MachineDuck = machineDuck;
            EventDuck = eventDuck;
            PassengerDuck = passengerDuck;
            WarningDuck = warningDuck;

            _humVolumeStable = humVolumeStable;
            _humVolumeWarning = humVolumeWarning;
            _humVolumeCritical = humVolumeCritical;
            _humVolumeCollapse = humVolumeCollapse;

            _humPitchStable = humPitchStable;
            _humPitchWarning = humPitchWarning;
            _humPitchCritical = humPitchCritical;
            _humPitchCollapse = humPitchCollapse;
        }

        public float VolumeFor(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Machine:   return MachineVolume;
                case AudioChannel.Event:     return EventVolume;
                case AudioChannel.Passenger: return PassengerVolume;
                case AudioChannel.Warning:   return WarningVolume;
                default:                     return MasterVolume;
            }
        }

        public float DuckScaleFor(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Machine:   return MachineDuck;
                case AudioChannel.Event:     return EventDuck;
                case AudioChannel.Passenger: return PassengerDuck;
                case AudioChannel.Warning:   return WarningDuck;
                default:                     return MasterDuck;
            }
        }

        /// <summary>과수확 정적 구간에서 그 채널이 내야 하는 볼륨.</summary>
        public float DuckedVolumeFor(AudioChannel channel) => VolumeFor(channel) * DuckScaleFor(channel);

        /// <summary>
        /// 정적 구간으로 들어가는 중간 볼륨. <paramref name="t"/>가 0이면 평상시, 1이면 완전 감쇠다.
        /// 연출 코드가 직접 Lerp를 짜면 채널마다 다른 곡선이 생기므로 여기 한 곳에 둔다.
        /// </summary>
        public float VolumeDuring(AudioChannel channel, float duckAmount)
        {
            float t = duckAmount < 0f ? 0f : (duckAmount > 1f ? 1f : duckAmount);
            float full = VolumeFor(channel);
            return full + (full * DuckScaleFor(channel) - full) * t;
        }

        public float HumVolumeScaleFor(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Strain:  return _humVolumeWarning;
                case RiskLevel.Critical: return _humVolumeCritical;
                case RiskLevel.Collapse: return _humVolumeCollapse;
                default:                 return _humVolumeStable;
            }
        }

        public float HumPitchScaleFor(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Strain:  return _humPitchWarning;
                case RiskLevel.Critical: return _humPitchCritical;
                case RiskLevel.Collapse: return _humPitchCollapse;
                default:                 return _humPitchStable;
            }
        }

        /// <summary>
        /// 위험 프로파일의 험 값에 믹스 배율과 기계 채널 볼륨을 얹은 최종 볼륨.
        /// 위험 단계의 "형태"는 <paramref name="profile"/>이, "크기"는 이 믹스가 정한다.
        /// </summary>
        public float HumVolumeFor(RiskLevel level, in RiskProfile profile)
        {
            return profile.HumVolume * HumVolumeScaleFor(level) * MachineVolume * MasterVolume;
        }

        public float HumPitchFor(RiskLevel level, in RiskProfile profile)
        {
            return profile.HumPitch * HumPitchScaleFor(level);
        }
    }
}

// 씬 배선 필요:
//   1. 험을 내는 오디오 소스(`Scripts/Risk/RiskStateView.cs` 계열)가 `AudioMixProfile` 참조를
//      받아 `HumVolumeFor(level, profile)` 를 쓰도록 바꿔야 채널 분리가 실제로 작동한다.
//   2. 과수확 접근·정적 연출(`Scripts/View/OverharvestUnlockEffect.cs`)이 `VolumeDuring(...)`
//      으로 감쇠해야 PRD §7.3의 「주변 기계음 감소 → 정적」이 데이터로 조정 가능해진다.
//   3. AudioMixer 에셋은 아직 없다. 이 프로파일은 믹서를 대체하지 않고, 믹서가 생기면
//      노출 파라미터의 목표값을 여기서 읽는다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/AudioMixProfile.asset
