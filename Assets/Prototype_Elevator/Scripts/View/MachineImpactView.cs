using UnityEngine;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 레버를 당긴 **순간**에 방이 반응하게 만든다 — 타격감의 표현 계층.
    ///
    /// ## 무엇이 타격감을 만드는가
    ///
    /// 사람이 「닿았다」를 읽는 신호는 **도달**이 아니라 **반동과 여파**다.
    /// 그래서 이 컴포넌트는 네 채널을 서로 다른 시간 상수로 움직인다 —
    /// 한 채널만 쓰면 애니메이션으로 보이고, 전부 같은 곡선을 쓰면 한 덩어리로 보인다.
    ///
    /// | 채널 | 시간 | 왜 |
    /// |---|---|---|
    /// | 전구 서지 → 처짐 | 0.06s 상승 · 0.45s 하강 | 전력이 빨려 나가는 감각. 가장 빠르다 |
    /// | 경고등 점멸 | 0.9s 감쇠 진동 | 「장치가 반응했다」의 국소 신호 |
    /// | 캐빈 흔들림 | 0.5s 감쇠 | 공간이 받은 충격 |
    /// | 통관 점화 | 열마다 0.08s 지연 | 결과가 **순서대로** 읽히게 한다 |
    ///
    /// 마지막 줄이 게임적으로 가장 중요하다. 아홉 칸이 동시에 켜지면 무엇이 일어났는지
    /// 읽을 시간이 없다 — 사용자가 지적한 「정확히 어떤 일이 일어나는지 인지가 안 된다」가
    /// 그것이고, 열 단위 지연이 그 인지를 만든다.
    ///
    /// ## 판정을 몰지 않는다
    ///
    /// **이 컴포넌트는 게임 상태를 읽기만 하고 쓰지 않는다.** 물리가 판정을 몰면
    /// 결정론이 깨지고, 이 저장소는 같은 시드가 같은 `SpinResult` 를 내는 것을
    /// 회귀 감시선으로 삼는다(`GRAPHICS_TARGET` G-7 마지막 줄).
    /// 여기서 하는 일은 전부 **표현**이다 — 발광, 광원 세기, 트랜스폼 오프셋.
    ///
    /// ## 프레임당 0 B
    ///
    /// `UP-TECH-05`(워밍업 후 매 프레임 0 B)가 이 저장소의 회귀 감시선이다.
    /// 그래서 <see cref="MaterialPropertyBlock"/> 을 하나만 만들어 재사용하고,
    /// **<c>GetPropertyBlock</c> 을 부르지 않는다** — 그 호출이 매번 새 저장소를 만든다.
    /// `SpinBoardView` 가 정확히 그것으로 1,638 B/프레임을 흘린 적이 있다.
    /// 유휴 상태에서는 아예 아무것도 쓰지 않고 이른 반환한다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class MachineImpactView : MonoBehaviour
    {
        [Header("전구 — 당기는 순간 전력이 빨린다")]
        [Tooltip("천장 케이지 전구의 광원. 비면 이 채널은 조용히 꺼진다.")]
        [SerializeField] private Light _keyLight;

        [Tooltip("서지 배수. 1.6 이면 순간적으로 60% 밝아졌다가 처진다.")]
        [SerializeField, Range(1f, 3f)] private float _surge = 1.55f;

        [Tooltip("서지 뒤 처지는 깊이. 0.55 면 기준의 55% 까지 내려갔다 돌아온다.")]
        [SerializeField, Range(0.1f, 1f)] private float _sag = 0.55f;

        [Header("경고등 — 장치가 반응했다는 국소 신호")]
        [SerializeField] private Renderer _warningLens;
        [SerializeField] private Color _warningColor = new Color(1f, 0.22f, 0.12f);
        [SerializeField, Range(0f, 8f)] private float _warningPeak = 4.2f;

        [Header("캐빈 — 공간이 받은 충격")]
        [Tooltip("흔들릴 대상. 보통 카메라 리그가 아니라 **물체**여야 공간이 흔들린 것으로 읽힌다.")]
        [SerializeField] private Transform _shakeTarget;
        [SerializeField, Range(0f, 0.05f)] private float _shakeAmplitude = 0.012f;

        [Header("통관 점화 — 열마다 지연시켜 순서를 만든다")]
        [Tooltip("열 0·1·2 의 영혼 렌더러. 각 열 3개씩.")]
        [SerializeField] private Renderer[] _column0 = new Renderer[0];
        [SerializeField] private Renderer[] _column1 = new Renderer[0];
        [SerializeField] private Renderer[] _column2 = new Renderer[0];

        [SerializeField] private Color _soulColor = new Color(0.78f, 0.132f, 0.068f);
        [SerializeField, Range(0f, 6f)] private float _soulIdle = 1.15f;
        [SerializeField, Range(0f, 12f)] private float _soulFlash = 5.4f;

        [Tooltip("열 사이 지연(초). 0 이면 세 열이 동시에 켜져 순서가 사라진다.")]
        [SerializeField, Range(0f, 0.4f)] private float _columnDelay = 0.08f;

        [Header("시간 상수")]
        [SerializeField, Range(0.01f, 0.3f)] private float _attack = 0.06f;
        [SerializeField, Range(0.1f, 2f)] private float _lampRelease = 0.45f;
        [SerializeField, Range(0.1f, 3f)] private float _warningRelease = 0.9f;
        [SerializeField, Range(0.1f, 2f)] private float _shakeRelease = 0.5f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _block;
        private float _baseLightIntensity;
        private Vector3 _shakeHome;
        private bool _homeCaptured;

        // 각 채널의 남은 에너지(0~1). 전부 0 이면 이 컴포넌트는 아무것도 하지 않는다.
        private float _lampEnergy;
        private float _warningEnergy;
        private float _shakeEnergy;
        private float _strikeAge = -1f;      // 음수 = 진행 중인 타격 없음

        /// <summary>직전에 실제로 쓴 값. 같은 값을 다시 쓰지 않기 위한 것이다.</summary>
        private float _lastWarning = -1f;
        private readonly float[] _lastColumn = { -1f, -1f, -1f };

        /// <summary>지금 연출이 도는가. 헤드리스 단정이 읽는다.</summary>
        public bool IsActive => _strikeAge >= 0f;

        /// <summary>마지막 타격 이후 경과 시간(초). 음수면 타격이 없었다.</summary>
        public float StrikeAge => _strikeAge;

        /// <summary>
        /// 레버가 바닥에 닿은 순간 호출한다.
        /// `LeverPhysics.onBottomedOut` 또는 `InteractableLever` 의 이벤트에 인스펙터로 건다 —
        /// 코드 의존이 아니라 배선이라 소유 경로를 넘지 않는다.
        /// </summary>
        public void Strike()
        {
            EnsureHome();
            _strikeAge = 0f;
            _lampEnergy = 1f;
            _warningEnergy = 1f;
            _shakeEnergy = 1f;
        }

        /// <summary>세기를 지정해 때린다. 과수확처럼 「더 세게」가 의미를 갖는 경우에 쓴다.</summary>
        public void StrikeWithStrength(float strength01)
        {
            Strike();
            float k = Mathf.Clamp01(strength01);
            _lampEnergy = k;
            _warningEnergy = k;
            _shakeEnergy = k;
        }

        /// <summary>
        /// **거부** — 잠긴 레버를 당겼을 때. 경고등만 한 번 번쩍인다.
        ///
        /// `Strike()` 를 대신 쓰면 안 된다. 그쪽은 전구·흔들림·통관 점화까지
        /// 전부 돌려서 **아무 일도 일어나지 않은 입력이 성공처럼 보인다.**
        /// 거부는 거부로 보여야 한다 — 경고 채널 하나만 쓰고, 나머지 채널의
        /// 에너지는 0 으로 둔다.
        ///
        /// 이미 타격이 진행 중이면 무시한다. 진행 중인 성공 연출을 거부 연출이
        /// 덮으면 그 프레임의 화면은 거짓말이 된다.
        /// </summary>
        public void Deny()
        {
            if (_strikeAge >= 0f) return;
            EnsureHome();
            _strikeAge = 0f;
            _lampEnergy = 0f;
            _shakeEnergy = 0f;
            _warningEnergy = 1f;
        }

        /// <summary>헤드리스 테스트 전용. 씬 없이 감쇠와 순서를 확인한다.</summary>
        public void StepForTest(float dt, int steps)
        {
            EnsureHome();
            for (int i = 0; i < steps; i++) Integrate(dt);
        }

        /// <summary>열 <paramref name="column"/> 의 점화 세기(0~1). 순서 단정이 읽는다.</summary>
        public float ColumnIgnition(int column)
        {
            if (_strikeAge < 0f) return 0f;
            float t = _strikeAge - _columnDelay * Mathf.Clamp(column, 0, 2);
            if (t < 0f) return 0f;
            // 빠른 상승 · 느린 하강. 「점화」의 모양이다.
            if (t < _attack) return t / Mathf.Max(0.0001f, _attack);
            return Mathf.Exp(-(t - _attack) / Mathf.Max(0.0001f, _warningRelease));
        }

        private void Awake()
        {
            EnsureHome();
            // 유휴 발광을 한 번 칠한다. 이게 없으면 첫 타격 전까지 구체가 꺼져 있다.
            PaintColumns(true);
        }

        /// <summary>
        /// 기준 상태를 잡는다. **블록 생성도 여기서 한다** — `Awake` 에 두면
        /// 에디트 모드에서 돌지 않아 `StepForTest` 가 null 로 죽는다. 실제로 죽였다.
        /// 헤드리스에서 못 돌리는 연출은 회귀를 잡을 수단이 없는 연출이다.
        /// </summary>
        private void EnsureHome()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            if (_homeCaptured) return;
            if (_keyLight != null) _baseLightIntensity = _keyLight.intensity;
            if (_shakeTarget != null) _shakeHome = _shakeTarget.localPosition;
            _homeCaptured = true;
        }

        private void LateUpdate()
        {
            // **유휴에서는 아무것도 하지 않는다.** 프레임당 0 B 가 회귀 감시선이다.
            if (_strikeAge < 0f) return;
            Integrate(Time.deltaTime);
        }

        private void Integrate(float dt)
        {
            if (_strikeAge < 0f) return;
            _strikeAge += dt;

            _lampEnergy = Decay(_lampEnergy, dt, _lampRelease);
            _warningEnergy = Decay(_warningEnergy, dt, _warningRelease);
            _shakeEnergy = Decay(_shakeEnergy, dt, _shakeRelease);

            ApplyLamp();
            ApplyWarning();
            ApplyShake();
            PaintColumns(false);

            // 전부 잦아들면 원위치로 되돌리고 **완전히 멈춘다.**
            if (_lampEnergy < 0.002f && _warningEnergy < 0.002f && _shakeEnergy < 0.002f
                && ColumnIgnition(2) < 0.002f)
            {
                if (_keyLight != null) _keyLight.intensity = _baseLightIntensity;
                if (_shakeTarget != null) _shakeTarget.localPosition = _shakeHome;
                _strikeAge = -1f;
                _lampEnergy = _warningEnergy = _shakeEnergy = 0f;
                PaintColumns(true);
            }
        }

        private static float Decay(float value, float dt, float tau)
            => value * Mathf.Exp(-dt / Mathf.Max(0.0001f, tau));

        private void ApplyLamp()
        {
            if (_keyLight == null) return;
            // 서지 → 처짐 → 복귀. 상승 구간에서는 밝아지고 그 뒤 기준 아래로 처진다.
            float k;
            if (_strikeAge < _attack)
                k = Mathf.Lerp(1f, _surge, _strikeAge / Mathf.Max(0.0001f, _attack));
            else
                k = Mathf.Lerp(1f, _sag, _lampEnergy);
            _keyLight.intensity = _baseLightIntensity * k;
        }

        private void ApplyWarning()
        {
            if (_warningLens == null) return;
            // 감쇠 진동 — 단순 감쇠는 「꺼진다」로 보이고 진동이 있어야 「점멸」로 읽힌다.
            float pulse = Mathf.Abs(Mathf.Sin(_strikeAge * 18f));
            float amount = _warningEnergy * (0.45f + 0.55f * pulse);

            // 32 단계로 끊는다. 연속값이면 매 프레임 「바뀌었다」가 참이 되어
            // `SetPropertyBlock` 이 계속 돈다 — `SpinBoardView` 가 같은 함정에 빠졌다.
            float q = Mathf.Round(amount * 32f) / 32f;
            if (Mathf.Abs(q - _lastWarning) < 0.0001f) return;
            _lastWarning = q;

            _block.SetColor(EmissionColorId, _warningColor * (0.35f + _warningPeak * q));
            _warningLens.SetPropertyBlock(_block);
        }

        private void ApplyShake()
        {
            if (_shakeTarget == null || _shakeAmplitude <= 0f) return;
            // 두 주파수를 섞는다. 하나면 기계적으로 흔들려 「애니메이션」으로 보인다.
            float a = _shakeEnergy * _shakeAmplitude;
            float x = Mathf.Sin(_strikeAge * 41f) * 0.6f + Mathf.Sin(_strikeAge * 67f) * 0.4f;
            float y = Mathf.Sin(_strikeAge * 53f + 1.3f);
            _shakeTarget.localPosition = _shakeHome + new Vector3(x * a, y * a * 0.7f, 0f);
        }

        private void PaintColumns(bool idleOnly)
        {
            PaintColumn(0, _column0, idleOnly);
            PaintColumn(1, _column1, idleOnly);
            PaintColumn(2, _column2, idleOnly);
        }

        private void PaintColumn(int index, Renderer[] renderers, bool idleOnly)
        {
            if (renderers == null || renderers.Length == 0) return;

            float ignition = idleOnly ? 0f : ColumnIgnition(index);
            float q = Mathf.Round(ignition * 32f) / 32f;
            if (Mathf.Abs(q - _lastColumn[index]) < 0.0001f) return;
            _lastColumn[index] = q;

            Color c = _soulColor * (_soulIdle + _soulFlash * q);
            _block.SetColor(EmissionColorId, c);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(_block);
        }
    }
}
