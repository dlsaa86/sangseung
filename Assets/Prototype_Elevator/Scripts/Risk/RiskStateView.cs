using UnityEngine;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Risk
{
    /// <summary>
    /// 위험 단계를 엘리베이터 공간에 표현한다.
    ///
    /// `MASTER_PRD.md` §9: "각 상태는 조명, 사운드, 기계 진동, 파티클, 계기판, 승객 행동에서
    /// **동기화**되어야 한다. 특정 감각 채널 하나에만 의존하지 않는다."
    /// 그래서 여기서 네 채널을 한 번에 민다 — 조명, 경고등, 물리 진동, 소리.
    /// 무음 영상에서도, 화면을 안 보고 소리만 들어도 단계가 구분되어야 하기 때문이다.
    ///
    /// 판정에는 전혀 관여하지 않는다. 이 컴포넌트를 꺼도 게임과 테스트는 그대로 돈다.
    /// </summary>
    public sealed class RiskStateView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;

        [Header("강도 프리셋 — 승인 대기 항목이라 하나로 잠그지 않는다")]
        [SerializeField] private RiskIntensity _intensity = RiskIntensity.Standard;

        [Header("조명")]
        [SerializeField] private Light _cabinLight;
        [SerializeField] private Renderer _lampRenderer;
        [SerializeField, Min(0f)] private float _baseLightIntensity = 1.6f;

        [Header("경고등")]
        [SerializeField] private Renderer _warningLight;

        [Header("진동")]
        [Tooltip("물리적으로 흔들릴 물체(천장등). 화면이 아니라 물체가 흔들려야 공간이 흔들린 것으로 읽힌다.")]
        [SerializeField] private Transform _swayTarget;
        [Tooltip("카메라. VISUAL_SPEC §8 때문에 아주 작게만 흔든다.")]
        [SerializeField] private Transform _cameraTarget;

        [Header("소리")]
        [SerializeField] private AudioSource _hum;

        [Header("전이")]
        [Tooltip("단계가 바뀔 때 프로파일이 섞이는 속도. 즉시 바뀌면 계단처럼 보인다.")]
        [SerializeField, Min(0.1f)] private float _blendSpeed = 2.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly RiskEvaluator _evaluator = new RiskEvaluator();
        private RiskProfile[] _profiles;
        private RiskProfile _blended;
        private MaterialPropertyBlock _block;
        private Vector3 _swayHome;
        private Vector3 _cameraHome;
        private float _phase;

        /// <summary>현재 위험 단계. HUD·계기판·검증 하네스가 읽는다.</summary>
        public RiskLevel Level => _evaluator.Current;

        /// <summary>현재 위험 점수. 디버그 표시용.</summary>
        public float Score => _evaluator.CurrentScore;

        /// <summary>왜 이 단계인지 한 줄.</summary>
        public string Reason { get; private set; } = "위험 요인 없음";

        /// <summary>런타임에 강도 프리셋을 바꾼다. 승인 비교용 캡처를 같은 조건에서 뽑기 위한 통로다.</summary>
        public void SetIntensity(RiskIntensity intensity)
        {
            _intensity = intensity;
            _profiles = RiskProfile.Preset(_intensity);
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _profiles = RiskProfile.Preset(_intensity);
            _blended = _profiles[0];
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_swayTarget != null) _swayHome = _swayTarget.localPosition;
            if (_cameraTarget != null) _cameraHome = _cameraTarget.localPosition;
            if (_hum != null && _hum.clip == null) _hum.clip = BuildHumClip();
            if (_hum != null) { _hum.loop = true; _hum.volume = 0f; _hum.Play(); }
            if (_run != null) _run.RunStarted += _ => _evaluator.Reset();
        }

        private void LateUpdate()
        {
            RiskInputs inputs = ReadInputs();
            RiskLevel level = _evaluator.Evaluate(in inputs);
            Reason = _evaluator.Explain(in inputs);

            RiskProfile target = _profiles[Mathf.Clamp((int)level, 0, _profiles.Length - 1)];
            _blended = Blend(_blended, target, Time.deltaTime * _blendSpeed);
            _phase += Time.deltaTime;

            ApplyLighting();
            ApplyWarningLight();
            ApplySway();
            ApplyAudio();
        }

        private RiskInputs ReadInputs()
        {
            FloorSession f = _run != null && _run.Session != null ? _run.Session.Current : null;
            if (f == null)
            {
                // 층이 없으면 런이 끝난 것이다. 실패로 끝났으면 그 상태를 유지한다.
                bool runFailed = _run != null && _run.Session != null && _run.Session.IsFailed;
                return new RiskInputs(0, 0, 0, false, 1, 1f, runFailed);
            }

            ResidualState residual = f.Residual;
            float ratio = f.RequiredPower > 0f ? f.Power / f.RequiredPower : 1f;
            bool floorFailed = f.Result != null && !f.Result.Succeeded;

            return new RiskInputs(
                residual.AbsorberCount, residual.ProliferatorCount,
                f.ExtraSpinsTaken, f.IsOverloaded, f.SpinsRemaining, ratio, floorFailed);
        }

        private void ApplyLighting()
        {
            float flicker = 1f;
            if (_blended.FlickerRate > 0f && _blended.FlickerDepth > 0f)
            {
                // 규칙적인 사인이 아니라 두 주파수를 섞는다. 하나만 쓰면 기계적으로 깜빡여
                // "고장난 형광등"이 아니라 "애니메이션"으로 보인다.
                float wave = Mathf.Sin(_phase * _blended.FlickerRate * Mathf.PI * 2f) * 0.6f
                           + Mathf.Sin(_phase * _blended.FlickerRate * 3.7f) * 0.4f;
                flicker = 1f - _blended.FlickerDepth * Mathf.InverseLerp(-1f, 1f, wave);
            }

            if (_cabinLight != null)
            {
                _cabinLight.intensity = _baseLightIntensity * _blended.LightIntensity * flicker;
                _cabinLight.color = _blended.LightColor;
            }

            if (_lampRenderer != null)
            {
                _lampRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, _blended.LightColor);
                _block.SetColor(EmissionColorId, _blended.LightColor * (2.4f * _blended.LightIntensity * flicker));
                _lampRenderer.SetPropertyBlock(_block);
            }
        }

        private void ApplyWarningLight()
        {
            if (_warningLight == null) return;

            float pulse = _blended.WarningPulseRate > 0f
                ? Mathf.Abs(Mathf.Sin(_phase * _blended.WarningPulseRate * Mathf.PI))
                : 1f;
            float emission = _blended.WarningEmission * pulse;

            _warningLight.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Color.Lerp(new Color(0.16f, 0.16f, 0.18f), _blended.WarningColor,
                                                    Mathf.Clamp01(_blended.WarningEmission)));
            _block.SetColor(EmissionColorId, _blended.WarningColor * emission);
            _warningLight.SetPropertyBlock(_block);
        }

        private void ApplySway()
        {
            if (_swayTarget != null)
            {
                float sway = Mathf.Sin(_phase * _blended.SwayRate * Mathf.PI * 2f) * _blended.SwayAmplitude;
                float bob = Mathf.Sin(_phase * _blended.SwayRate * 1.61f) * _blended.SwayAmplitude * 0.5f;
                _swayTarget.localPosition = _swayHome + new Vector3(sway, bob, sway * 0.6f);
            }

            if (_cameraTarget != null)
            {
                // 무작위 노이즈가 아니라 결정된 파형이다. 무작위 전체 화면 흔들림은
                // VISUAL_SPEC §6이 금지한다 — 멀미만 나고 정보는 주지 않는다.
                float shake = _blended.CameraShake;
                _cameraTarget.localPosition = _cameraHome + new Vector3(
                    Mathf.Sin(_phase * 13.1f) * shake,
                    Mathf.Sin(_phase * 17.7f) * shake,
                    0f);
            }
        }

        private void ApplyAudio()
        {
            if (_hum == null) return;
            _hum.volume = _blended.HumVolume;
            _hum.pitch = Mathf.Max(0.05f, _blended.HumPitch);
        }

        private static RiskProfile Blend(RiskProfile from, RiskProfile to, float t)
        {
            t = Mathf.Clamp01(t);
            return new RiskProfile
            {
                LightIntensity   = Mathf.Lerp(from.LightIntensity, to.LightIntensity, t),
                LightColor       = Color.Lerp(from.LightColor, to.LightColor, t),
                FlickerRate      = Mathf.Lerp(from.FlickerRate, to.FlickerRate, t),
                FlickerDepth     = Mathf.Lerp(from.FlickerDepth, to.FlickerDepth, t),
                WarningColor     = Color.Lerp(from.WarningColor, to.WarningColor, t),
                WarningPulseRate = Mathf.Lerp(from.WarningPulseRate, to.WarningPulseRate, t),
                WarningEmission  = Mathf.Lerp(from.WarningEmission, to.WarningEmission, t),
                SwayAmplitude    = Mathf.Lerp(from.SwayAmplitude, to.SwayAmplitude, t),
                SwayRate         = Mathf.Lerp(from.SwayRate, to.SwayRate, t),
                CameraShake      = Mathf.Lerp(from.CameraShake, to.CameraShake, t),
                HumVolume        = Mathf.Lerp(from.HumVolume, to.HumVolume, t),
                HumPitch         = Mathf.Lerp(from.HumPitch, to.HumPitch, t),
            };
        }

        /// <summary>
        /// 기계 험을 절차적으로 만든다. 외부 오디오 에셋을 쓰지 않는 이유는 라이선스가
        /// 불명확한 파일을 추가하지 않는다는 규칙(`CLAUDE.md`) 때문이다.
        /// 최종 사운드가 아니다 — 단계 구분이 귀로 되는지만 확인하는 플레이스홀더다.
        /// </summary>
        private static AudioClip BuildHumClip()
        {
            const int sampleRate = 44100;
            const float seconds = 2f;
            int count = (int)(sampleRate * seconds);
            var samples = new float[count];

            // 50Hz 기본파 + 배음 + 약한 노이즈. 낡은 산업용 기계의 저역 험.
            var random = new System.Random(20260730);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float value = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.55f
                            + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.22f
                            + Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.10f
                            + (float)(random.NextDouble() - 0.5) * 0.06f;
                samples[i] = value * 0.5f;
            }

            // 루프 이음매를 지운다. 안 하면 2초마다 딱딱 소리가 난다.
            int fade = sampleRate / 20;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                samples[i] *= k;
                samples[count - 1 - i] *= k;
            }

            var clip = AudioClip.Create("MachineHum", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
