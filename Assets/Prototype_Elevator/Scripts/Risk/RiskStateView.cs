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

        [Tooltip("단계별 연출 값. 비면 코드 프리셋으로 폴백한다 — 소리 없이 죽지 않는다.")]
        [SerializeField] private Data.Profiles.DangerFeedbackProfile _feedbackProfile;

        [Tooltip("셰이크·섬광 접근성 배율. 비면 기본값(감쇠 없음).")]
        [SerializeField] private Data.Profiles.AccessibilityProfile _accessibility;

        [Header("조명")]
        [SerializeField] private Light _cabinLight;

        [Header("방 전체 색조 (위험 → 환경)")]
        [Tooltip("끄면 위험 단계가 캐빈 등 하나로만 전달된다 — 그 경우 벽·천장 색차가 약 2% 다.")]
        [SerializeField] private bool _driveAmbient = true;

        [Tooltip("원래 앰비언트와 단계 색을 섞는 비율. 1이면 통째로 갈아 끼워 밝기가 튄다.")]
        [SerializeField, Range(0f, 1f)] private float _ambientBlend = 0.55f;

        [Tooltip("앰비언트를 색상축뿐 아니라 명도축으로도 내린다. 끄면 단계가 색상에만 실린다 — " +
                 "7차 독립 판정이 그 상태를 「밝기 차가 없어 같은 밴드」로 지적했다.")]
        [SerializeField] private bool _driveAmbientValue = true;

        [Tooltip("LightIntensity 가 0 일 때의 앰비언트 명도 / Stable 명도 비율. " +
                 "낮출수록 단계 사이 밝기 간격이 벌어지고 Collapse 가 어두워진다. " +
                 "무엇을 넣든 인접 간격 0.08 은 코드가 강제한다.")]
        [SerializeField, Range(0.05f, 0.95f)] private float _ambientValueFloorRatio = 0.20f;

        private Color _originalAmbient;
        private bool _ambientCaptured;

        // 사다리는 프로파일(LightIntensity)과 씬 앰비언트에서 유도한다. 새 데이터가 아니다.
        private readonly float[] _ambientLadder = new float[RiskAmbientLadder.LevelCount];
        private readonly RiskProfile[] _ladderProfiles = new RiskProfile[RiskAmbientLadder.LevelCount];
        private bool _ladderBuilt;
        private float _ambientValue = -1f;
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

        [Tooltip("험의 크기를 정하는 쪽. 비면 Awake 에서 씬에서 찾는다. " +
                 "없으면 AudioMixProfile 의 단계별 험 배율과 과수확 정적이 험에 닿지 않고, " +
                 "험은 DangerFeedbackProfile 값 그대로 난다(HumSource 가 어느 쪽인지 알려 준다).")]
        [SerializeField] private Audio.AudioDirector _audio;

        [Header("전이")]
        [Tooltip("단계가 바뀔 때 프로파일이 섞이는 속도. 즉시 바뀌면 계단처럼 보인다.")]
        [SerializeField, Min(0.1f)] private float _blendSpeed = 2.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly RiskEvaluator _evaluator = new RiskEvaluator();
        private Data.Profiles.DangerFeedbackSnapshot _levels;
        private Data.Profiles.AccessibilitySnapshot _accessibilitySnapshot;
        private string _profileSource = "(미초기화)";
        private string _accessibilitySource = "(미초기화)";
        private RiskProfile _blended;
        private MaterialPropertyBlock _block;
        private Vector3 _swayHome;
        private Vector3 _cameraHome;
        private float _phase;
        private int _reasonKey = int.MinValue;
        private float _effectiveHumVolume;

        // 험 배율은 **섞어서** 따라간다. `_blended` 는 프로파일 형태를 부드럽게 잇는데
        // 그 위에 곱하는 배율만 계단으로 튀면 단계 전이가 "천천히 커지다가 툭" 이 된다.
        private float _humVolumeScale = 1f;
        private float _humPitchScale = 1f;
        private float _humSilenceGain = 1f;
        private string _humSource = "(미초기화)";

        /// <summary>
        /// 단계별 기계음 자막 문안. 미리 만들어 둔다 — 매 프레임 문자열을 짓지 않기 위해서다.
        /// 인덱스는 <see cref="RiskLevel"/> 값이다(Stable 0 ~ Collapse 3).
        /// </summary>
        private static readonly string[] HumCaptions =
        {
            "[기계 험 — 낮고 고른 소리]",
            "[기계 험 — 삐걱이며 높아진다]",
            "[기계 험 — 거칠게 떨린다]",
            "[기계 험 — 끊기며 무너진다]",
        };

        /// <summary>현재 위험 단계. HUD·계기판·검증 하네스가 읽는다.</summary>
        public RiskLevel Level => _evaluator.Current;

        /// <summary>현재 위험 점수. 디버그 표시용.</summary>
        public float Score => _evaluator.CurrentScore;

        /// <summary>왜 이 단계인지 한 줄.</summary>
        public string Reason { get; private set; } = "위험 요인 없음";

        /// <summary>
        /// 실내등 밝기에 곱해지는 외부 배수. 과수확 해제 같은 **일시적 사건**이 실내등을
        /// 끌어내릴 때 쓴다.
        ///
        /// 왜 배수를 따로 두는가: 실내등은 이 컴포넌트가 매 LateUpdate 마다 절대값으로 다시
        /// 쓴다. 다른 컴포넌트가 코루틴에서 intensity 를 곱해봐야 같은 프레임의 LateUpdate 가
        /// 그대로 덮어써서 **아무 일도 일어나지 않는다.** 실제로 처음엔 그렇게 만들어서
        /// 해제 순간의 "기계 반응"이 전혀 보이지 않았다. 소유자는 하나여야 한다.
        /// </summary>
        public float CabinLightMultiplier { get; set; } = 1f;

        /// <summary>
        /// 런타임에 강도 프리셋을 바꾼다. 승인 비교용 캡처를 같은 조건에서 뽑기 위한 통로다.
        ///
        /// **에셋이 배선돼 있으면 그쪽이 이긴다.** 에셋은 자기 `SourcePreset` 을 갖고 있으므로
        /// 여기서 강도를 바꿔도 에셋 값은 그대로다 — 그래야 「데이터가 실제로 화면을 바꾼다」가
        /// 성립한다. 에셋을 무시하고 코드 프리셋을 다시 읽으면 배선이 장식이 된다.
        /// </summary>
        public void SetIntensity(RiskIntensity intensity)
        {
            _intensity = intensity;
            RebuildProfiles();
        }

        /// <summary>
        /// 단계별 연출 값의 출처를 정한다. 에셋 → 코드 프리셋 순.
        ///
        /// 이 한 줄이 없어서 `DangerFeedbackProfile.asset` 이 만들어지고도 아무 데도 흐르지
        /// 않았다(`DEAD_IMPLEMENTATION_AUDIT.md` §1). 값을 바꿔도 화면에서 아무 일이
        /// 일어나지 않으면 PRD §14.2 「교체 가능한 프리셋」은 성립하지 않는다.
        /// </summary>
        private void RebuildProfiles()
        {
            if (_feedbackProfile != null)
            {
                _levels = _feedbackProfile.Snapshot();
                _profileSource = _feedbackProfile.name;
            }
            else
            {
                _levels = Data.Profiles.DangerFeedbackProfile.SnapshotOf(_intensity);
                _profileSource = "코드 프리셋 " + _intensity;
            }
            _accessibilitySnapshot =
                Data.Profiles.AccessibilityProfile.SnapshotOrDefault(_accessibility);
            _accessibilitySource = _accessibility != null
                ? _accessibility.name
                : "코드 기본값";

            BuildAmbientLadder();
        }

        /// <summary>
        /// 단계별 앰비언트 **명도**를 유도한다. 값을 새로 만들지 않는다 —
        /// <see cref="RiskProfile.LightIntensity"/>(프로파일 데이터)와 씬의 원래 앰비언트에서
        /// 계산한다. 그래서 `DangerFeedbackProfile` 을 갈아 끼우면 사다리도 같이 바뀐다.
        ///
        /// 왜 여기서 하는가: 사다리의 천장이 「지금의 Stable 앰비언트 명도」라서
        /// 프로파일이 바뀌면(강도 프리셋 교체) 다시 계산되어야 한다.
        /// </summary>
        private void BuildAmbientLadder()
        {
            // 아직 원래 값을 붙잡기 전이라면 지금의 렌더 설정이 곧 원래 값이다.
            Color baseAmbient = _ambientCaptured ? _originalAmbient : RenderSettings.ambientLight;

            for (int i = 0; i < _ladderProfiles.Length; i++)
                _ladderProfiles[i] = _levels.For((RiskLevel)i);

            float ceiling = RiskAmbientLadder.CeilingFor(baseAmbient, _ladderProfiles[0], _ambientBlend);
            if (!RiskAmbientLadder.BandIsSufficient(ceiling))
            {
                // 조용히 실패하지 않는다. 이 경우 최소 간격 보장이 하한에 걸려 깨진다.
                Debug.LogWarning($"[상승] 앰비언트 명도 천장 {ceiling:F3} 이 낮아 " +
                                 $"단계 간격 {RiskAmbientLadder.MinValueStep:F2} × 3 을 담지 못한다. " +
                                 "씬 앰비언트나 Stable LightColor 가 너무 어둡다.");
            }

            RiskAmbientLadder.Build(_ladderProfiles, ceiling, _ambientValueFloorRatio, _ambientLadder);
            _ladderBuilt = true;
            if (_ambientValue < 0f) _ambientValue = _ambientLadder[0];
        }

        /// <summary>지금 쓰고 있는 연출 값의 출처. 검증 하네스가 「에셋이 실제로 읽혔는가」를 묻는다.</summary>
        public string ProfileSource => _profileSource;

        /// <summary>
        /// 접근성 값의 출처. **`ProfileSource` 와 따로 있어야 한다.**
        ///
        /// 독립 감사가 짚은 것: `AccessibilityProfile.asset` 의 8개 값이 코드 기본값
        /// (`DefaultSnapshot`)과 **전부 동일**하고, `SnapshotOrDefault` 는 null 에서 경고도
        /// 남기지 않으며, `ProfileSource` 는 `_feedbackProfile` 만 반영한다. 그래서 씬에서
        /// `_accessibility` 를 떼어내도 **화면·로그·테스트가 전부 그대로였다** —
        /// 「주입했다」를 반증할 수단이 없었다. 다른 프로파일 셋에는 출처 단정을 붙여 놓고
        /// 이 하나에만 없었던 것이라 일관성 문제이기도 하다.
        /// </summary>
        public string AccessibilitySource => _accessibilitySource;

        /// <summary>
        /// 실제로 스피커에 나간 험 볼륨. `_blended.HumVolume` 이 아니라 **접근성 배율을
        /// 통과한 뒤의 값**이다.
        ///
        /// 왜 필요한가: 접근성 값이 실제로 흐르는지 묻는 유일한 반증 수단이다.
        /// 확인법 — 프로파일의 `_lowFrequencyScale` 을 0으로 내리면 이 값이 0이 된다.
        /// 그대로 1이면 배선이 끊긴 것이고, 그때는 「주입했다」가 거짓이다.
        /// </summary>
        public float EffectiveHumVolume => _effectiveHumVolume;

        /// <summary>
        /// 험의 크기가 어디서 왔는가. `ProfileSource`·`AccessibilitySource` 와 같은 이유로
        /// 따로 있다 — `AudioDirector` 를 떼어내도 험은 계속 나므로, 출처를 노출하지 않으면
        /// 「믹스 배율이 실제로 곱해졌다」를 반증할 수단이 없다.
        /// </summary>
        public string HumSource => _humSource;

        /// <summary>
        /// 지금 험에 곱해지고 있는 단계별 볼륨 배율(`AudioMixProfile._humVolumeScale`).
        ///
        /// 이 값이 없던 동안 그 8필드는 <c>AudioDirector.HumVolume</c> 까지 계산되고
        /// **거기서 멈춰 있었다** — 험의 소유자인 이 컴포넌트가 `_blended.HumVolume` 을
        /// 절대값으로 덮어썼기 때문이다(그 파일의 배선 메모 4번이 적어 둔 그대로).
        /// </summary>
        public float HumVolumeScale => _humVolumeScale;

        /// <summary>같은 이유로 노출하는 단계별 피치 배율.</summary>
        public float HumPitchScale => _humPitchScale;

        /// <summary>
        /// 험에 적용 중인 과수확 정적 게인(0~1). 1이면 정적이 아니거나 배선이 없다.
        ///
        /// 그전까지 험은 정적 구간에 **혼자 살아남았고**, `AudioDirector` 는 그걸
        /// `AudioListener` 전체를 줄여 우회했다. 그 우회는 플레이어 발소리까지 지우므로
        /// §7.3 의 정적이 「방이 조용해졌다」가 아니라 「음소거됐다」로 읽힌다.
        /// </summary>
        public float HumSilenceGain => _humSilenceGain;

        /// <summary>
        /// 지금 나고 있는 기계음의 자막 한 줄. `ShowSubtitles` 가 꺼져 있으면 빈 문자열이다.
        ///
        /// **아직 그리는 쪽이 없다.** 자막 표시는 UI 소유자의 파일(`Scripts/UI`, `Scripts/View`)에
        /// 속하고 이 작업의 수정 범위 밖이라, 여기서는 「무엇을 자막으로 낼지」와
        /// 「낼지 말지」까지만 확정한다. 값 자체는 살아 있다 — `_showSubtitles` 를 끄면
        /// 이 프로퍼티가 빈 문자열로 바뀐다.
        ///
        /// `Reason` 에 섞지 않은 이유: `Reason` 은 `RiskEventBridge` 와 `AccidentRecorder` 를
        /// 통해 사건 로그로 나간다. 접근성 옵션이 **텔레메트리 내용을 바꾸면** 옵션에 따라
        /// 다른 기록이 남고, 그건 접근성이 아니라 데이터 손상이다.
        /// </summary>
        public string AudioCaption
        {
            get
            {
                if (_hum == null) return string.Empty;
                int index = (int)_evaluator.Current;
                if (index < 0 || index >= HumCaptions.Length) index = 0;
                return _accessibilitySnapshot.Caption(HumCaptions[index]);
            }
        }

        /// <summary>단계별 연출 값. 에셋이 배선됐으면 에셋 것이다.</summary>
        public RiskProfile ProfileFor(RiskLevel level) => _levels.For(level);

        /// <summary>
        /// 지금 앰비언트에 실린 **명도(V)**. 깜빡임을 곱하기 전 값이다.
        ///
        /// 왜 노출하는가: `ProfileSource`·`EffectiveHumVolume` 과 같은 이유다 — 명도축이
        /// 실제로 움직였는지 반증할 수단이 없으면 「고쳤다」가 검증되지 않는다.
        /// 캡처 하네스와 감사자가 이 값을 단계별로 읽어 사다리를 그대로 확인할 수 있다.
        /// </summary>
        public float AmbientValue => _ambientValue;

        /// <summary>단계별 목표 앰비언트 명도. 사다리 자체를 밖에서 읽는다.</summary>
        public float AmbientValueFor(RiskLevel level)
        {
            if (!_ladderBuilt) return -1f;
            int index = (int)level;
            if (index < 0) index = 0;
            if (index >= _ambientLadder.Length) index = _ambientLadder.Length - 1;
            return _ambientLadder[index];
        }

        /// <summary>
        /// 인접 단계 명도차의 최솟값. **이것이 회색조 구분 가능성의 지표다** —
        /// 「기준점(Stable)으로부터의 색거리」가 아니다. 그 지표를 믿었다가 인접 쌍을
        /// 하나도 못 갈랐던 이력이 백로그 §5.1 에 있다.
        /// </summary>
        public float MinAmbientValueStep =>
            _ladderBuilt ? RiskAmbientLadder.MinAdjacentStep(_ambientLadder) : -1f;

        /// <summary>
        /// **앰비언트를 반드시 되돌린다.** `RenderSettings.ambientLight` 는 씬이 아니라
        /// 렌더 설정 전역이라, 복원하지 않으면 플레이 모드를 나간 뒤에도 에디터에 남고
        /// 다음 캡처가 오염된 조명으로 찍힌다. 이 저장소는 「직전 상태를 조용히 물려받는」
        /// 오염을 이미 한 번 겪었다 — 캡처 눈높이가 2.60m 로 남아 있던 건이 그것이다.
        /// </summary>
        private void OnDisable()
        {
            RestoreAmbient();
        }

        private void OnDestroy()
        {
            RestoreAmbient();
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            RebuildProfiles();
            _blended = _levels.For(RiskLevel.Stable);
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();

            // 오디오는 **없어도 된다.** 없으면 험이 프로파일 값 그대로 나고 믹스 배율과
            // 과수확 정적만 험에 닿지 않는다 — 그 사실은 `HumSource` 로 드러난다.
            // 씬에서 찾는 이유는 `PassengerReactionView` 와 같다: 배선 하나를 빠뜨렸다고
            // 청각 채널이 통째로 죽는 것보다 자동으로 붙는 편이 낫다.
            if (_audio == null) _audio = FindAnyObjectByType<Audio.AudioDirector>();
            _humSource = _audio != null
                ? "AudioDirector (AudioMixProfile 배율 × 정적 게인)"
                : "DangerFeedbackProfile 값 그대로";

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

            // Explain 은 리스트와 string.Join 을 쓴다. 매 프레임 부르면 같은 문장을
            // 60번 새로 만든다. 위험 요인 구성이 바뀔 때만 짓는다.
            int reasonKey = inputs.AbsorberResidual
                          | (inputs.ProliferatorResidual << 5)
                          | (inputs.ExtraSpinsTaken << 10)
                          | ((inputs.Overloaded ? 1 : 0) << 15)
                          | ((inputs.SpinsRemaining == 0 && inputs.PowerRatio < 1f ? 1 : 0) << 16);
            if (reasonKey != _reasonKey)
            {
                _reasonKey = reasonKey;
                Reason = _evaluator.Explain(in inputs);
            }

            RiskProfile target = _levels.For(level);
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
            // 접근성이 섬광을 끄거나 주파수를 낮춘다. **광과민성 발작은 되돌릴 수 없는
            // 피해**라 이 분기는 연출보다 앞선다 (`UP-RISK-08`, PRD §8.5).
            float rate = _accessibilitySnapshot.ClampFlickerRate(_blended.FlickerRate);
            float depth = _accessibilitySnapshot.AllowFlickerAt(_blended.FlickerRate)
                ? _blended.FlickerDepth * _accessibilitySnapshot.FlickerDepthScale
                : 0f;

            float flicker = 1f;
            if (rate > 0f && depth > 0f)
            {
                // 규칙적인 사인이 아니라 두 주파수를 섞는다. 하나만 쓰면 기계적으로 깜빡여
                // "고장난 형광등"이 아니라 "애니메이션"으로 보인다.
                float wave = Mathf.Sin(_phase * rate * Mathf.PI * 2f) * 0.6f
                           + Mathf.Sin(_phase * rate * 3.7f) * 0.4f;
                flicker = 1f - depth * Mathf.InverseLerp(-1f, 1f, wave);
            }

            if (_cabinLight != null)
            {
                _cabinLight.intensity = _baseLightIntensity * _blended.LightIntensity * flicker
                                        * Mathf.Clamp01(CabinLightMultiplier);
                _cabinLight.color = _blended.LightColor;
            }

            ApplyAmbient(flicker);

            if (_lampRenderer != null)
            {
                _lampRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, _blended.LightColor);
                _block.SetColor(EmissionColorId, _blended.LightColor * (2.4f * _blended.LightIntensity * flicker));
                _lampRenderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// 위험 단계를 **방 전체**에 전달한다.
        ///
        /// 왜 필요한가: 지금까지 위험이 환경에 닿는 경로는 `_cabinLight.color` **한 줄**뿐이었고,
        /// 등 하나로는 벽·천장·바닥이 거의 안 움직인다. 되돌린 뒤 실측 —
        /// `06` Stable 벽 (96.5, 98.5, 99.4) vs `16` Collapse (96.5, 93.7, 95.7),
        /// 차이가 255 중 5 미만(**약 2%**)이다. 독립 평가가 세 라운드 연속
        /// 「Critical 이 Stable 과 같은 방」이라고 지적한 것의 구조적 원인이 이것이다.
        ///
        /// `RenderSettings.ambientLight` 는 URP Lit 이 그대로 읽는 **전역** 값이라
        /// 머티리얼을 하나도 안 건드리고 방 전체를 물들인다. 셰이더 교체가
        /// 순손실로 반려된 뒤 남은 가장 싼 채널이다.
        ///
        /// **원래 값을 반드시 복원한다** — 이건 씬이 아니라 렌더 설정 전역이라,
        /// 복원하지 않으면 플레이 모드를 나간 뒤에도 에디터에 남는다.
        ///
        /// **2026-08-02 — 두 번째 축을 붙였다.** 위 앰비언트는 7차에 채택됐지만 같은 판정이
        /// 이렇게 덧붙였다: 「가장 약한 인접 쌍은 Strain↔Critical 이다. 둘 다 따뜻한 갈색
        /// 방이고 **밝기 차가 없다.**」 재계산으로 확인된다 — 색상 전용 경로의 V 사다리는
        /// 0.6455 / 0.6450 / 0.6340 / 0.5900 이고 Strain↔Critical 간격이 **0.011** 이다.
        /// <see cref="RiskAmbientLadder"/> 가 `LightIntensity` 를 명도축에 실어 그 간격을
        /// 하한 0.08 이상으로 강제한다. 두 축이 같은 방향으로 움직여야 경계가 선다.
        /// </summary>
        private void ApplyAmbient(float flicker)
        {
            if (!_driveAmbient) return;
            if (!_ambientCaptured)
            {
                _originalAmbient = RenderSettings.ambientLight;
                _ambientCaptured = true;
                BuildAmbientLadder();   // 천장이 원래 앰비언트에 걸려 있다. 붙잡은 뒤 다시 세운다.
            }

            // 단계 색을 원래 앰비언트에 섞는다. 통째로 갈아 끼우면 밝기가 튀어
            // 「조명이 바뀌었다」가 아니라 「화면이 깜빡였다」로 읽힌다.
            Color tinted = Color.Lerp(_originalAmbient, _blended.LightColor, _ambientBlend);

            // **색상축만으로는 인접 단계가 갈리지 않는다.** 여기까지의 `tinted` 는 명도가
            // 사실상 정지해 있다 — 씬 앰비언트 (0.26, 0.27, 0.31) · 표준 프리셋으로 계산하면
            // 0.6455 / 0.6450 / 0.6340 / 0.5900 이고 Stable↔Strain 간격이 0.0005 다.
            // `RiskAmbientLadder` 가 `LightIntensity`(이미 단조 하강하는 데이터)를 명도축에
            // 실어 두 축이 같은 방향으로 움직이게 한다.
            if (_driveAmbientValue && _ladderBuilt)
            {
                int index = (int)_evaluator.Current;
                if (index < 0) index = 0;
                if (index >= _ambientLadder.Length) index = _ambientLadder.Length - 1;

                // `_blended` 와 같은 속도로 따라간다. 명도만 계단으로 튀면 전이가
                // 「천천히 물들다가 툭」 이 된다 — `ApplyAudio` 의 배율과 같은 이유다.
                _ambientValue = Mathf.Lerp(_ambientValue, _ambientLadder[index],
                                           Mathf.Clamp01(Time.deltaTime * _blendSpeed));
                tinted = RiskAmbientLadder.WithValue(tinted, _ambientValue);
            }

            RenderSettings.ambientLight = tinted * Mathf.Lerp(1f, flicker, 0.5f);
        }

        private void RestoreAmbient()
        {
            if (!_ambientCaptured) return;
            RenderSettings.ambientLight = _originalAmbient;
            _ambientCaptured = false;
        }

        private void ApplyWarningLight()
        {
            if (_warningLight == null) return;

            float pulse = _blended.WarningPulseRate > 0f
                ? Mathf.Abs(Mathf.Sin(_phase * _blended.WarningPulseRate * Mathf.PI))
                : 1f;

            // 사이렌을 껐으면 경고등을 키운다. 접근성 옵션이 청각 채널을 지웠는데 시각
            // 채널이 그대로면 「경고가 통째로 약해진다」가 되고, 그건 PRD §9 의
            // 「특정 감각 채널 하나에만 의존하지 않는다」를 반대로 어기는 것이다.
            float emission = _accessibilitySnapshot.CompensateWarningEmission(_blended.WarningEmission * pulse);

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
                // 물체 흔들림과 카메라 흔들림을 **따로** 감쇠한다. 카메라만 끄고 싶은 사람이
                // 대부분이고, 물체까지 멈추면 위험 단계가 화면에서 사라진다 — 접근성 옵션이
                // 정보를 지우면 안 된다 (`UP-RISK-08`).
                float amplitude = _blended.SwayAmplitude * _accessibilitySnapshot.WorldSwayScale;
                float sway = Mathf.Sin(_phase * _blended.SwayRate * Mathf.PI * 2f) * amplitude;
                float bob = Mathf.Sin(_phase * _blended.SwayRate * 1.61f) * amplitude * 0.5f;
                _swayTarget.localPosition = _swayHome + new Vector3(sway, bob, sway * 0.6f);
            }

            if (_cameraTarget != null)
            {
                // 무작위 노이즈가 아니라 결정된 파형이다. 무작위 전체 화면 흔들림은
                // VISUAL_SPEC §6이 금지한다 — 멀미만 나고 정보는 주지 않는다.
                float shake = _accessibilitySnapshot.ScaleShake(_blended.CameraShake);
                _cameraTarget.localPosition = _cameraHome + new Vector3(
                    Mathf.Sin(_phase * 13.1f) * shake,
                    Mathf.Sin(_phase * 17.7f) * shake,
                    0f);
            }
        }

        /// <summary>
        /// 험을 실제로 민다. **세 출처가 여기서 곱해진다.**
        ///
        ///   형태 — `DangerFeedbackProfile` (단계별 <c>HumVolume</c>/<c>HumPitch</c>, `_blended`)
        ///   크기 — `AudioMixProfile` 의 단계별 험 배율 8필드 (`AudioDirector` 가 소유)
        ///   순간 — 과수확 정적 게인 (`AudioDirector.SilenceGain`, §7.3)
        ///
        /// **그전까지는 첫째만 있었다.** 이 메서드가 `_hum.volume` 을 절대값으로 덮어썼기
        /// 때문에 나머지 둘은 `AudioDirector` 안에서 계산까지 되고 아무 소리에도 닿지
        /// 않았다(그 파일의 배선 메모 4번이 「남은 한 줄은 RiskStateView 쪽이다」라고
        /// 적어 둔 것이 이 자리다). 정적 구간에는 험만 살아남아 있었고, 그 컴포넌트는
        /// 그것을 `AudioListener` 전체를 줄여 우회하고 있었다 — 플레이어 발소리까지
        /// 지우는 우회라 정적이 연출이 아니라 음소거로 읽힌다.
        ///
        /// 배율을 **섞어서** 따라가는 이유: `_blended` 는 단계 사이를 부드럽게 잇는데
        /// 그 위에 곱하는 값만 계단이면 전이가 "커지다가 툭" 이 된다.
        /// </summary>
        private void ApplyAudio()
        {
            if (_hum == null) return;

            RiskLevel level = _evaluator.Current;

            // 배율의 소유자는 `AudioDirector` 다. 여기서 `AudioMixProfile` 을 직접 읽지
            // 않는 이유는 값의 출처가 두 벌이 되기 때문이다 — 한쪽만 에셋을 꽂으면
            // 험과 사건음이 서로 다른 믹스로 울리고, 그 상태는 귀로만 알 수 있다.
            float targetVolumeScale = _audio != null ? _audio.HumVolumeScaleFor(level) : 1f;
            float targetPitchScale = _audio != null ? _audio.HumPitchScaleFor(level) : 1f;

            float t = Mathf.Clamp01(Time.deltaTime * _blendSpeed);
            _humVolumeScale = Mathf.Lerp(_humVolumeScale, targetVolumeScale, t);
            _humPitchScale = Mathf.Lerp(_humPitchScale, targetPitchScale, t);

            // 정적은 섞지 않는다. `SilenceWindow` 가 이미 0.12초에 걸쳐 내려가는 곡선이고
            // 여기서 한 번 더 뭉개면 §7.3 이 못박은 0.3~0.7초 눈금이 흐려진다.
            _humSilenceGain = _audio != null ? _audio.SilenceGain : 1f;

            // 이 험은 50/100/150Hz 로 구운 **저주파** 지속음이다(BuildHumClip 참조).
            // `LowFrequencyScale` 이 말하는 「낮은 험」이 정확히 이것이라, 저주파 배율의
            // 소비처는 다른 어디도 아니고 여기다.
            _hum.volume = _accessibilitySnapshot.ScaleLowFrequency(
                _blended.HumVolume * _humVolumeScale * _humSilenceGain);

            // 사이렌을 끄면 남는 청각 채널은 피치뿐이다 — 프로파일이 그렇게 약속해 놓고
            // 지키는 코드가 없었다. 편차만 벌리므로 사이렌이 켜져 있으면 기존과 동일하다.
            _hum.pitch = Mathf.Max(0.05f, _accessibilitySnapshot.CompensateHumPitch(
                _blended.HumPitch * _humPitchScale));

            _effectiveHumVolume = _hum.volume;
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
