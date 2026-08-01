using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 공포 연출의 강도를 플레이어가 되돌릴 수 있게 만드는 스위치들. `UP-RISK-08`.
    ///
    /// `MASTER_PRD.md` §12가 플레이테스트 지표로 "멀미, 섬광, 사이렌 피로도"를 재라고
    /// 요구한다. 재기만 하고 끌 수 없으면 그 지표는 보고서 한 줄로 끝난다.
    /// `PENDING_DECISIONS.md` PD-07도 카메라 셰이크를 **0까지 낮출 수 있어야 한다**는
    /// 제약을 이 항목에 걸어 뒀다.
    ///
    /// 헬퍼를 두는 이유: 연출 코드가 <c>if (profile.AllowSiren)</c> 를 각자 쓰기 시작하면
    /// 새 연출이 생길 때마다 그 if 를 빠뜨린 곳이 하나씩 늘어난다. 값을 읽는 곳이 아니라
    /// **값을 적용하는 함수**를 공유해야 빠뜨릴 자리가 없어진다.
    /// </summary>
    [CreateAssetMenu(fileName = "AccessibilityProfile",
                     menuName = "Ascend/Profiles/Accessibility", order = 105)]
    public sealed class AccessibilityProfile : ScriptableObject
    {
        [Header("흔들림")]
        [Tooltip("카메라 셰이크 배율. 0이면 카메라가 전혀 흔들리지 않는다 — 환경 오브젝트 흔들림은 그대로 남으므로 위험 단계는 여전히 읽힌다(PRD §8.3).")]
        [Range(0f, 1f)] [SerializeField] private float _cameraShakeScale = DefaultCameraShakeScale;

        [Tooltip("천장등·케이블 같은 환경 오브젝트 흔들림 배율. 셰이크를 껐을 때 위험을 대신 전달하는 채널이라 기본값을 낮추지 않는다.")]
        [Range(0f, 1f)] [SerializeField] private float _worldSwayScale = DefaultWorldSwayScale;

        [Header("섬광")]
        [Tooltip("깜빡임을 허용하는가. 끄면 조명은 밝기·색으로만 위험을 표현한다.")]
        [SerializeField] private bool _allowFlicker = true;

        [Tooltip("허용하는 최대 깜빡임 빈도(Hz). 광과민성 발작 기준선이 초당 3회다 — 그 위로 기본값을 올리지 않는다.")]
        [Range(0f, 12f)] [SerializeField] private float _maxFlickerHz = DefaultMaxFlickerHz;

        [Tooltip("깜빡임 깊이 배율. 0이면 밝기가 흔들리지 않는다.")]
        [Range(0f, 1f)] [SerializeField] private float _flickerDepthScale = DefaultFlickerDepthScale;

        [Header("소리")]
        [Tooltip("사이렌·경보음을 허용하는가. 끄면 경고는 시각과 험 피치로만 전달된다.")]
        [SerializeField] private bool _allowSiren = true;

        [Tooltip("저주파 배율. 낮은 험은 스피커·헤드폰에 따라 두통을 만든다.")]
        [Range(0f, 1f)] [SerializeField] private float _lowFrequencyScale = DefaultLowFrequencyScale;

        [Header("표시")]
        [Tooltip("비언어 음성·기계음에 자막을 붙이는가. 소리로만 전달되는 정보가 남지 않게 한다(PRD §9 마지막 문장).")]
        [SerializeField] private bool _showSubtitles = true;

        public const float DefaultCameraShakeScale = 1f;
        public const float DefaultWorldSwayScale = 1f;
        public const float DefaultMaxFlickerHz = 3f;
        public const float DefaultFlickerDepthScale = 1f;
        public const float DefaultLowFrequencyScale = 1f;

        public float CameraShakeScale => _cameraShakeScale;
        public float WorldSwayScale => _worldSwayScale;
        public bool AllowFlicker => _allowFlicker;
        public float MaxFlickerHz => _maxFlickerHz;
        public float FlickerDepthScale => _flickerDepthScale;
        public bool AllowSiren => _allowSiren;
        public float LowFrequencyScale => _lowFrequencyScale;
        public bool ShowSubtitles => _showSubtitles;

        /// <summary>기본은 "전부 켬"이다. 접근성 옵션은 기본값을 깎는 것이 아니라 선택지를 주는 것이다.</summary>
        public void Reset()
        {
            _cameraShakeScale = DefaultCameraShakeScale;
            _worldSwayScale = DefaultWorldSwayScale;
            _allowFlicker = true;
            _maxFlickerHz = DefaultMaxFlickerHz;
            _flickerDepthScale = DefaultFlickerDepthScale;
            _allowSiren = true;
            _lowFrequencyScale = DefaultLowFrequencyScale;
            _showSubtitles = true;
        }

        public AccessibilitySnapshot Snapshot()
        {
            return new AccessibilitySnapshot(_cameraShakeScale, _worldSwayScale, _allowFlicker,
                _maxFlickerHz, _flickerDepthScale, _allowSiren, _lowFrequencyScale, _showSubtitles);
        }

        public static AccessibilitySnapshot DefaultSnapshot
        {
            get
            {
                return new AccessibilitySnapshot(DefaultCameraShakeScale, DefaultWorldSwayScale, true,
                    DefaultMaxFlickerHz, DefaultFlickerDepthScale, true, DefaultLowFrequencyScale, true);
            }
        }

        /// <summary>
        /// 프로파일이 없을 때. 여기서는 **경고를 남기지 않는다** — 접근성 에셋이 없는 것은
        /// 아직 옵션 메뉴가 없다는 뜻이지 고장이 아니고, 매 프레임 경고가 쌓이면 진짜
        /// 오류가 콘솔에서 묻힌다.
        /// </summary>
        public static AccessibilitySnapshot SnapshotOrDefault(AccessibilityProfile profile)
        {
            return profile != null ? profile.Snapshot() : DefaultSnapshot;
        }

        public float ScaleShake(float amplitude) => Snapshot().ScaleShake(amplitude);
        public bool AllowFlickerAt(float rateHz) => Snapshot().AllowFlickerAt(rateHz);
        public float ClampFlickerRate(float rateHz) => Snapshot().ClampFlickerRate(rateHz);
    }

    /// <summary>접근성 설정의 값 사본. 연출 코드는 이 구조체의 적용 함수만 부른다.</summary>
    public readonly struct AccessibilitySnapshot
    {
        public readonly float CameraShakeScale;
        public readonly float WorldSwayScale;
        public readonly bool AllowFlicker;
        public readonly float MaxFlickerHz;
        public readonly float FlickerDepthScale;
        public readonly bool AllowSiren;
        public readonly float LowFrequencyScale;
        public readonly bool ShowSubtitles;

        public AccessibilitySnapshot(float cameraShakeScale, float worldSwayScale, bool allowFlicker,
            float maxFlickerHz, float flickerDepthScale, bool allowSiren, float lowFrequencyScale,
            bool showSubtitles)
        {
            CameraShakeScale = cameraShakeScale;
            WorldSwayScale = worldSwayScale;
            AllowFlicker = allowFlicker;
            MaxFlickerHz = maxFlickerHz;
            FlickerDepthScale = flickerDepthScale;
            AllowSiren = allowSiren;
            LowFrequencyScale = lowFrequencyScale;
            ShowSubtitles = showSubtitles;
        }

        /// <summary>카메라 흔들림 폭에 배율을 적용한다. 배율 0이면 정확히 0을 돌려준다.</summary>
        public float ScaleShake(float amplitude) => amplitude * Clamp01(CameraShakeScale);

        /// <summary>환경 오브젝트 흔들림. 카메라와 다른 축이라 별도 배율을 쓴다.</summary>
        public float ScaleSway(float amplitude) => amplitude * Clamp01(WorldSwayScale);

        /// <summary>그 빈도로 깜빡여도 되는가. false면 조명은 고정 밝기로 간다.</summary>
        public bool AllowFlickerAt(float rateHz)
        {
            if (!AllowFlicker) return false;
            if (rateHz <= 0f) return false;
            return rateHz <= MaxFlickerHz;
        }

        /// <summary>
        /// 실제로 적용할 깜빡임 빈도. 금지면 0, 너무 빠르면 상한으로 내린다.
        /// 상한을 넘겼을 때 깜빡임을 통째로 끄지 않고 늦추는 이유: 위험 단계 사이의
        /// 차이가 깜빡임에도 걸려 있어서, 꺼 버리면 Critical과 Collapse가 같아 보인다.
        /// </summary>
        public float ClampFlickerRate(float rateHz)
        {
            if (!AllowFlicker || rateHz <= 0f) return 0f;
            return rateHz > MaxFlickerHz ? MaxFlickerHz : rateHz;
        }

        /// <summary>깜빡임 깊이. 빈도를 늦춰도 깊이가 그대로면 여전히 번쩍인다.</summary>
        public float ScaleFlickerDepth(float depth)
        {
            if (!AllowFlicker) return 0f;
            return depth * Clamp01(FlickerDepthScale);
        }

        /// <summary>사이렌 볼륨. 금지면 0.</summary>
        public float SirenVolume(float volume) => AllowSiren ? volume : 0f;

        /// <summary>저주파 성분(험 등)의 볼륨.</summary>
        public float ScaleLowFrequency(float volume) => volume * Clamp01(LowFrequencyScale);

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}

// 씬 배선 필요:
//   1. 옵션 메뉴가 아직 없다. 값 변경 경로는 현재 인스펙터뿐이다 — `UP-RISK-08` 의
//      「옵션 메뉴」는 UI 소유자가 붙여야 한다.
//   2. `Scripts/Risk/RiskStateView.cs` 가 `RiskProfile.CameraShake`/`FlickerRate`/`FlickerDepth`/
//      `HumVolume` 을 쓰는 자리에서 각각 `ScaleShake`/`ClampFlickerRate`/`ScaleFlickerDepth`/
//      `ScaleLowFrequency` 를 통과시켜야 이 프로파일이 실제로 연출을 바꾼다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/AccessibilityProfile.asset
