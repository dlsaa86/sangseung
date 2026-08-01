using System.Collections;
using UnityEngine;
using Ascend.Prototype.Player;
using Ascend.Prototype.Risk;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 과수확 레버가 풀리는 **순간**을 사건으로 만든다.
    ///
    /// `VISUAL_SPEC.md` §7: "해제 순간 조명, 소리, 기계 반응이 집중된다."
    /// `MASTER_PRD.md` §7: "과수확은 단순한 점수 버튼이 아니라 조명, 음향, 진동, 승객 반응,
    /// 기계 상태를 변화시키는 **공간적 사건**이어야 한다."
    ///
    /// `InteractableOverharvestLever.Unlocked` 이벤트는 만들어져 있었지만 **구독자가 없어서**
    /// 덮개만 조용히 열리고 끝났다. 그래서 캡처에서 이 레버가 "대표 선택"으로 보이지 않았다.
    ///
    /// 해제 순간에 네 가지가 동시에 일어난다:
    ///   조명   — 전용 등이 확 켜졌다가 낮게 가라앉는다
    ///   기계   — 실내등이 순간 어두워진다(전력을 끌어다 쓴 것처럼)
    ///   소리   — 걸쇠 풀리는 둔탁한 소리 + 올라가는 톤
    ///   진동   — 레버 덩어리가 짧게 떨린다
    ///
    /// 그리고 풀린 **동안** 경고 띠가 계속 발광한다 — 잠김/해제가 정지 화면에서도 구분되도록.
    ///
    /// 강도는 전부 인스펙터 값이다. 최종 연출은 승인 대기 항목이라 코드에 잠그지 않는다.
    /// </summary>
    public sealed class OverharvestUnlockEffect : MonoBehaviour
    {
        [SerializeField] private InteractableOverharvestLever _lever;

        [Header("전용 조명 — 해제 순간 집중")]
        [SerializeField] private Light _spotLight;
        [SerializeField] private float _flashIntensity = 9f;
        [SerializeField] private float _armedIntensity = 1.6f;
        [SerializeField] private Color _flashColor = new Color(1f, 0.74f, 0.32f);

        [Header("기계 반응 — 실내등이 전력을 뺏긴다")]
        [Tooltip("실내등의 소유자. 여기에 배수를 넘긴다 — Light.intensity 를 직접 건드리면 덮어써진다.")]
        [SerializeField] private RiskStateView _riskView;
        [SerializeField, Range(0f, 1f)] private float _cabinDip = 0.45f;

        [Header("진동")]
        [SerializeField] private Transform _shakeTarget;
        [SerializeField] private float _shakeAmplitude = 0.022f;

        [Header("경고 띠 — 풀린 동안 계속 발광")]
        [SerializeField] private Renderer[] _warningStripes = new Renderer[0];
        [SerializeField] private Color _stripeArmed = new Color(1f, 0.62f, 0.16f);
        [SerializeField] private Color _stripeLocked = new Color(0.10f, 0.10f, 0.11f);
        [SerializeField, Min(0f)] private float _stripeEmission = 3.2f;

        [Header("타이밍 (초)")]
        [SerializeField, Min(0.05f)] private float _flashDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float _settleDuration = 0.9f;

        [Header("소리")]
        [SerializeField] private AudioSource _audio;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.55f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _block;
        private Vector3 _shakeHome;
        private Coroutine _playing;
        private bool _armed;

        /// <summary>이 컴포넌트가 정한 스팟 목표 밝기. 경고 배수는 여기에 **곱해진다**.</summary>
        private float _spotTarget;

        private float _warningScale = 1f;

        /// <summary>검증 하네스용 — 해제 연출이 실제로 재생됐는가.</summary>
        public bool HasPlayed { get; private set; }

        /// <summary>검증 하네스용 — 지금 무장 상태로 표시 중인가.</summary>
        public bool IsArmedVisual => _armed;

        /// <summary>해제 연출이 재생 중인가.</summary>
        public bool IsPlaying => _playing != null;

        /// <summary>경고등을 실제로 밀 대상이 하나라도 배선돼 있는가. 없으면 이 채널은 화면에 없다.</summary>
        public bool HasWarningChannel
        {
            get
            {
                if (_spotLight != null) return true;
                for (int i = 0; i < _warningStripes.Length; i++)
                    if (_warningStripes[i] != null) return true;
                return false;
            }
        }

        /// <summary>지금 걸려 있는 경고 배수. 1 이 평소다.</summary>
        public float WarningScale => _warningScale;

        /// <summary>
        /// 경고등 채널을 **빌려준다.** §7.3 5단계의 「경고등 재개」는 해금 연출과 다른 사건인데,
        /// 경고 띠와 스팟의 소유자는 여기다 — 두 컴포넌트가 같은 렌더러에
        /// <see cref="MaterialPropertyBlock"/> 을 쓰면 나중에 쓴 쪽이 이기고, 어느 쪽이
        /// 나중인지는 스크립트 실행 순서에 달린다. 그런 고장은 "어떤 날은 되고 어떤 날은
        /// 안 된다"로 나타나 원인을 찾는 데 가장 오래 걸린다.
        ///
        /// 그래서 소유권을 옮기지 않고 배수만 받는다. 1 이면 그 전과 **완전히 같은 그림**이다.
        /// </summary>
        /// <param name="scale">0 = 꺼짐, 1 = 평소, 1 초과 = 과증폭.</param>
        public void SetWarningScale(float scale)
        {
            float clamped = scale < 0f ? 0f : (scale > 4f ? 4f : scale);
            if (Mathf.Approximately(_warningScale, clamped)) return;
            _warningScale = clamped;
            ApplySpot();
            ApplyStripeVisual();
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_lever == null) _lever = FindAnyObjectByType<InteractableOverharvestLever>();
            if (_riskView == null) _riskView = FindAnyObjectByType<RiskStateView>();
            if (_shakeTarget != null) _shakeHome = _shakeTarget.localPosition;
            if (_audio != null && _audio.clip == null) _audio.clip = BuildUnlockClip();
            SetSpot(0f);

            ApplyStripes(false);
            if (_lever != null) _lever.Unlocked += OnUnlocked;
        }

        private void OnDestroy()
        {
            if (_lever != null) _lever.Unlocked -= OnUnlocked;
        }

        /// <summary>
        /// 꺼지거나 파괴될 때 실내등을 반드시 원래대로 돌린다.
        ///
        /// 이게 없으면 연출 도중에 컴포넌트가 비활성화되거나 층이 바뀌어 코루틴이 끊길 때
        /// 배수가 0.55 인 채로 굳는다 — **방이 영구히 어두워진다.** 빌린 것은 돌려준다.
        /// </summary>
        private void OnDisable()
        {
            if (_playing != null) { StopCoroutine(_playing); _playing = null; }
            DipCabin(1f);
            Shake(0f);

            // 빌려준 경고 배수도 돌려받는다. 재개 버스트 도중에 꺼지면 띠가 과증폭인 채로 굳는다.
            SetWarningScale(1f);
        }

        private void Update()
        {
            // 잠김으로 되돌아가면(층이 바뀌거나 스핀이 떨어지면) 표시도 되돌린다.
            bool unlocked = _lever != null && _lever.IsUnlocked;
            if (!unlocked && _armed)
            {
                ApplyStripes(false);
                SetSpot(0f);
            }
        }

        private void OnUnlocked()
        {
            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            HasPlayed = true;
            ApplyStripes(true);

            if (_audio != null)
            {
                _audio.volume = _volume;
                _audio.Play();
            }

            // 1) 확 켜지고 실내등이 꺼지듯 어두워진다 — 전력을 이쪽으로 뺏긴 것처럼 보인다.
            float elapsed = 0f;
            while (elapsed < _flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _flashDuration);

                if (_spotLight != null) _spotLight.color = _flashColor;
                SetSpot(Mathf.Lerp(0f, _flashIntensity, t));
                DipCabin(Mathf.Lerp(1f, 1f - _cabinDip, t));
                Shake(1f - t * 0.3f);
                yield return null;
            }

            // 2) 가라앉으면서 실내등이 돌아온다. 스팟은 낮게 남아 "지금 쓸 수 있다"를 유지한다.
            elapsed = 0f;
            while (elapsed < _settleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _settleDuration);
                float eased = t * t * (3f - 2f * t);

                SetSpot(Mathf.Lerp(_flashIntensity, _armedIntensity, eased));
                DipCabin(Mathf.Lerp(1f - _cabinDip, 1f, eased));
                Shake((1f - eased) * 0.7f);
                yield return null;
            }

            DipCabin(1f);
            Shake(0f);
            SetSpot(_armedIntensity);
            _playing = null;
        }

        /// <summary>
        /// 실내등을 잠깐 끌어내린다. `Light.intensity`를 직접 건드리지 않고 소유자
        /// (<see cref="RiskStateView"/>)의 배수를 움직인다 — 직접 쓰면 같은 프레임의
        /// LateUpdate 가 덮어써서 아무 일도 일어나지 않는다.
        /// </summary>
        private void DipCabin(float multiplier)
        {
            if (_riskView != null) _riskView.CabinLightMultiplier = Mathf.Clamp01(multiplier);
        }

        /// <summary>검증 하네스용 — 지금 실내등을 얼마나 끌어내리고 있는가.</summary>
        public float CabinDipAmount => _riskView != null ? 1f - _riskView.CabinLightMultiplier : 0f;

        private void Shake(float amount)
        {
            if (_shakeTarget == null) return;
            if (amount <= 0.001f) { _shakeTarget.localPosition = _shakeHome; return; }

            // 무작위가 아니라 결정된 파형이다. 같은 시드에서 캡처가 재현되어야 한다.
            float phase = Time.time * 46f;
            _shakeTarget.localPosition = _shakeHome + new Vector3(
                Mathf.Sin(phase) * _shakeAmplitude * amount,
                Mathf.Sin(phase * 1.7f) * _shakeAmplitude * 0.6f * amount,
                0f);
        }

        /// <summary>
        /// 경고 띠. 잠김일 때는 하우징보다 **어둡고**, 풀리면 발광한다.
        /// 색만 바꾸면 회색조에서 사라지므로 밝기 자체를 반대로 뒤집는다.
        /// </summary>
        private void ApplyStripes(bool armed)
        {
            _armed = armed;
            ApplyStripeVisual();
        }

        /// <summary>
        /// 무장 상태와 경고 배수를 합쳐 실제 색을 쓴다. 배수가 1 이면 예전과 같은 값이다.
        /// 잠김일 때는 배수를 곱하지 않는다 — 꺼진 램프가 재개 버스트에 반짝이면
        /// 「잠겨 있다」가 거짓말이 된다.
        /// </summary>
        private void ApplyStripeVisual()
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            Color baseColor = _armed ? _stripeArmed : _stripeLocked;
            Color emission = _armed ? _stripeArmed * (_stripeEmission * _warningScale) : Color.black;

            foreach (Renderer stripe in _warningStripes)
            {
                if (stripe == null) continue;
                stripe.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, baseColor);
                _block.SetColor(EmissionColorId, emission);
                stripe.SetPropertyBlock(_block);
            }
        }

        /// <summary>스팟 목표 밝기를 정하고 경고 배수를 곱해 실제로 쓴다.</summary>
        private void SetSpot(float intensity)
        {
            _spotTarget = intensity;
            ApplySpot();
        }

        private void ApplySpot()
        {
            if (_spotLight != null) _spotLight.intensity = _spotTarget * _warningScale;
        }

        /// <summary>
        /// 걸쇠가 풀리는 소리를 절차적으로 만든다. 외부 오디오 에셋을 쓰지 않는 이유는
        /// 라이선스가 불명확한 파일을 추가하지 않는다는 규칙(`CLAUDE.md`) 때문이다.
        /// 최종 사운드가 아니라 "해제가 귀로도 들리는가"만 확인하는 플레이스홀더다.
        /// </summary>
        private static AudioClip BuildUnlockClip()
        {
            const int sampleRate = 44100;
            const float seconds = 1.1f;
            int count = (int)(sampleRate * seconds);
            var samples = new float[count];
            var random = new System.Random(20260731);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;

                // 걸쇠 — 앞쪽 0.08초에 몰린 둔탁한 노이즈 버스트.
                float clunkEnvelope = Mathf.Exp(-t * 42f);
                float clunk = (float)(random.NextDouble() - 0.5) * clunkEnvelope * 0.9f
                            + Mathf.Sin(2f * Mathf.PI * 86f * t) * clunkEnvelope * 0.7f;

                // 올라가는 톤 — 무언가 준비됐다는 신호.
                float riseEnvelope = Mathf.Clamp01(t * 6f) * Mathf.Exp(-t * 2.4f);
                float rise = Mathf.Sin(2f * Mathf.PI * (150f + 220f * t) * t) * riseEnvelope * 0.35f;

                samples[i] = Mathf.Clamp(clunk + rise, -1f, 1f) * 0.8f;
            }

            var clip = AudioClip.Create("OverharvestUnlock", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
