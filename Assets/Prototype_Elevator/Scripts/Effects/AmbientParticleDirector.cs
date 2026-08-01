using System.Collections.Generic;
using UnityEngine;
using Ascend.Prototype.Events;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Effects
{
    /// <summary>
    /// PRD §12.5 의 파티클 다섯 갈래를 만들고 위험 단계에 묶는다 —
    /// 먼지 · 녹가루 · 스파크 · 정화 파편 · 캐스케이드 유입.
    ///
    /// **왜 이것이 필요한가**: 독립 시각 평가가 「`06`↔`10`↔`16` 의 조명·먼지·기울기·잔해가
    /// 완전히 동일하고 달라지는 것은 램프 색·틴트·문자열뿐」이라고 지적했다.
    /// 임계점 돌파가 사건이 아니라 **문자열 치환**으로만 일어나고 있었다.
    /// 공간이 상태를 말하려면 공기 중에 무언가가 있어야 한다.
    ///
    /// **왜 프리팹이 아니라 코드인가**: 파티클 5종을 프리팹·머티리얼 에셋으로 만들면
    /// 씬과 `.mat` 을 동시에 건드려야 하고, 그건 이 저장소에서 가장 조용히 깨지는 조합이다
    /// (`CLAUDE.md` 소유권 규칙). 코드로 만들면 배선이 한 곳이고 값이 전부 여기 보인다.
    ///
    /// **예산을 먼저 정한다**: PRD §12.5 가 「단계별 최대 동시 파티클 수와 오버드로우 예산」을
    /// 요구한다. <see cref="BudgetFor"/> 가 그 상한이고 <see cref="PeakConcurrent"/> 가
    /// 실측이다. 상한 없이 파티클을 켜면 저사양에서 제일 먼저 무너지는 것이 이쪽이다.
    ///
    /// **상한은 이제 데이터에서 온다** (`UP-TECH-09` ⑩). 값이 이 파일의 switch 문에 박혀
    /// 있었고, 같은 숫자를 담은 <see cref="Data.Profiles.PresentationProfile"/> 이 만들어졌는데도
    /// 읽는 쪽이 없어 **에셋이 장식이었다.** 지금은 <see cref="PresentationSource"/> 가
    /// 「코드 프리셋」이면 배선이 끊긴 것이고, 그 사실이 밖에서 보인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientParticleDirector : MonoBehaviour
    {
        /// <summary>에셋이 없을 때 쓰는 출처 이름. 「Profile」로 끝나지 않아야 단정이 폴백을 통과시키지 않는다.</summary>
        public const string CodePresetSource = "코드 프리셋";

        /// <summary>
        /// 코드 프리셋 사본. **매번 새로 만들지 않는다** — <see cref="Data.Profiles.PresentationProfile.DefaultSnapshot"/>
        /// 은 호출마다 배열을 새로 할당하는데 <see cref="MaxParticlesFor"/> 는 하네스가 반복해서 부른다.
        /// </summary>
        private static readonly Data.Profiles.PresentationSnapshot CodePreset =
            Data.Profiles.PresentationProfile.DefaultSnapshot;

        /// <summary>
        /// 단계별 동시 파티클 상한의 **코드 프리셋** (PRD §12.5). 합산이 아니라 시스템 하나당이다.
        ///
        /// 값이 여기 박혀 있었다. 이제 원본은 <see cref="Data.Profiles.PresentationProfile"/> 이고
        /// 이 정적 메서드는 **에셋이 없을 때의 폴백**이다 — 씬에 인스턴스가 없어도 답을 낼 수
        /// 있어야 하는 곳(하네스의 사전 예산 계산 등)이 있어서 남긴다. 숫자를 여기에 다시
        /// 적지 않는다: 프로파일의 기본값을 그대로 읽으므로 한쪽만 고치는 일이 생기지 않는다.
        ///
        /// **씬에 인스턴스가 있으면 이것을 묻지 마라** — 에셋이 꽂혀 있어도 이 메서드는
        /// 코드 프리셋을 돌려준다. 실제로 적용 중인 값은 <see cref="BudgetFor"/> 다.
        /// </summary>
        public static int MaxParticlesFor(RiskLevel level)
        {
            return CodePreset.MaxParticlesFor(level);
        }

        [Tooltip("비어 있으면 FindAnyObjectByType 으로 찾는다.")]
        [SerializeField] private RiskStateView _risk;
        [SerializeField] private RunSessionBehaviour _run;

        [Tooltip("파티클 밀도(PRD §14.1 ⑩)의 출처. 비우면 코드 프리셋으로 진행하고 경고를 남긴다.")]
        [SerializeField] private Data.Profiles.PresentationProfile _presentation;

        [Tooltip("파티클이 떠 있을 공간의 반지름. 엘리베이터 칸 크기에 맞춘다.")]
        [SerializeField, Min(0.5f)] private float _volumeRadius = 1.35f;

        [SerializeField, Min(0.5f)] private float _volumeHeight = 2.4f;

        private ParticleSystem _dust;
        private ParticleSystem _rust;
        private ParticleSystem _spark;
        private ParticleSystem _purify;
        private ParticleSystem _cascade;
        private readonly List<ParticleSystem> _all = new List<ParticleSystem>(5);

        private GameEventBus _bus;
        private RiskLevel _level = RiskLevel.Stable;
        private Material _shared;

        // 값은 한 번만 읽어 struct 로 들고 있는다. `LateUpdate` 가 매 프레임 도는 경로라
        // 여기서 ScriptableObject 를 다시 묻으면 그 자체가 프레임당 비용이 된다.
        private Data.Profiles.PresentationSnapshot _values = CodePreset;
        private string _valueSource = CodePresetSource;

        /// <summary>런 전체에서 관측된 최대 동시 파티클 수. 예산 판정에 쓴다.</summary>
        public int PeakConcurrent { get; private set; }

        /// <summary>지금 살아 있는 파티클 수 합계.</summary>
        public int LiveParticles
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _all.Count; i++)
                {
                    if (_all[i] != null) n += _all[i].particleCount;
                }
                return n;
            }
        }

        /// <summary>다섯 갈래가 전부 만들어졌는가. 하네스가 「존재」를 묻는다.</summary>
        public int SystemCount => _all.Count;

        /// <summary>지금 적용 중인 단계 상한. 데이터가 실제로 읽혔는지 확인용.</summary>
        public int CurrentBudget => _values.MaxParticlesFor(_level);

        /// <summary>
        /// 지금 이 인스턴스가 적용 중인 단계 상한. 정적 <see cref="MaxParticlesFor"/> 와 **다를 수 있다** —
        /// 다를 수 있다는 것이 곧 「에셋 값이 실제로 화면까지 흐른다」는 뜻이다.
        /// </summary>
        public int BudgetFor(RiskLevel level) => _values.MaxParticlesFor(level);

        /// <summary>
        /// 지금 쓰고 있는 파티클 밀도의 출처. 검증 하네스가 「에셋이 실제로 읽혔는가」를 묻는다.
        ///
        /// 값 대조로는 못 잡는다 — 에셋 기본값과 코드 프리셋이 같아서, 배선을 떼어내도
        /// 숫자는 그대로다. 출처가 유일한 반증 수단이다(`RiskStateView.AccessibilitySource`
        /// 가 같은 이유로 따로 있다).
        /// </summary>
        public string PresentationSource => _valueSource;

        /// <summary>
        /// 연출 프로파일을 런타임에 갈아 끼운다. 프리셋 비교 캡처를 같은 조건에서 뽑기 위한
        /// 통로이자, 씬을 열지 않고 배선을 검증하는 진입점이다
        /// (<see cref="Risk.RiskStateView.SetIntensity"/> 와 같은 역할).
        /// </summary>
        public void SetPresentationProfile(Data.Profiles.PresentationProfile profile)
        {
            _presentation = profile;
            ResolvePresentation();
            ApplyLevel();
        }

        /// <summary>
        /// 값의 출처를 정한다. 에셋 → 코드 프리셋 순.
        ///
        /// 이 한 줄이 없어서 `PresentationProfile.cs` 가 만들어지고도 소비처가 0곳이었다
        /// (`UP-TECH-09` ⑩). 값을 바꿔도 화면에서 아무 일이 일어나지 않으면
        /// PRD §14.2 「교체 가능한 프리셋」은 성립하지 않는다.
        /// </summary>
        private void ResolvePresentation()
        {
            _values = Data.Profiles.PresentationProfile.SnapshotOrDefault(
                _presentation, nameof(AmbientParticleDirector));
            _valueSource = _presentation != null ? _presentation.name : CodePresetSource;
        }

        private void Awake()
        {
            ResolvePresentation();

            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();

            // 셋 다 같은 머티리얼을 쓴다 — 드로우콜과 오버드로우를 아끼는 쪽이
            // `UP-TECH-07` 예산과 맞물린다. 색은 파티클 색으로 구분한다.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _shared = new Material(shader) { name = "AscendParticleShared" };
            }

            // **먼지 크기·밀도를 올렸다.** 독립 평가가 「3~5px 사각형 2~4개뿐이라
            // 먼지가 아니라 **죽은 픽셀로 읽힌다**」고 지적했다 — 배경이 어두워져
            // 이득을 볼 수 있었는데 크기·밀도가 그 이득을 못 받았다.
            // 0.020 → 0.045, 배출률 하한도 6 → 14 로 올린다(아래 ApplyLevel).
            // `UP-VIS-10`(안개·먼지가 결과판을 가리지 않는다)이 상한이라 알파는 그대로 둔다 —
            // 개수와 크기로 존재감을 만들고 불투명도로 만들지 않는다.
            _dust    = Build("Dust",    new Color(0.72f, 0.68f, 0.58f, 0.16f), 0.045f, 0.9f,  6.0f, false);
            _rust    = Build("Rust",    new Color(0.55f, 0.28f, 0.13f, 0.40f), 0.030f, 1.6f,  2.6f, true);
            _spark   = Build("Spark",   new Color(1.00f, 0.72f, 0.32f, 0.85f), 0.012f, 2.4f,  0.9f, true);
            _purify  = Build("Purify",  new Color(0.72f, 0.92f, 1.00f, 0.75f), 0.022f, 1.1f,  1.4f, true);
            _cascade = Build("Cascade", new Color(0.85f, 0.78f, 0.45f, 0.70f), 0.018f, 1.8f,  1.1f, true);
        }

        private void OnEnable()
        {
            if (_run != null) _run.RunStarted += OnRunStarted;
            Subscribe(_run != null && _run.Session != null ? _run.Session.Events : null);
        }

        private void OnDisable()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Subscribe(null);
        }

        private void OnRunStarted(RunSession session)
        {
            Subscribe(session != null ? session.Events : null);
        }

        private void Subscribe(GameEventBus bus)
        {
            if (_bus == bus) return;
            if (_bus != null) _bus.Published -= OnEvent;
            _bus = bus;
            if (_bus != null) _bus.Published += OnEvent;
        }

        private void OnEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                // 정화 파편 — 세 정화 종류 전부에 공기 중 반응을 준다.
                case GameEventKind.PurifyScattered:
                case GameEventKind.PurifyLine:
                case GameEventKind.PurifyCluster:
                    Burst(_purify, 10);
                    break;
                // 캐스케이드 유입 — 연쇄가 이어질 때마다 한 번.
                case GameEventKind.CascadeStep:
                    Burst(_cascade, 8);
                    break;
                // 사고 순간의 스파크. 단계 연출과 별개로 사건에 반응한다.
                case GameEventKind.ResidualDamage:
                case GameEventKind.CollapseBegan:
                    Burst(_spark, 16);
                    break;
            }
        }

        private void LateUpdate()
        {
            RiskLevel level = _risk != null ? _risk.Level : RiskLevel.Stable;
            if (level != _level)
            {
                _level = level;
                ApplyLevel();
            }

            int live = LiveParticles;
            if (live > PeakConcurrent) PeakConcurrent = live;
        }

        /// <summary>
        /// 단계가 바뀔 때만 배출률을 갈아 끼운다. 매 프레임 쓰면 `ParticleSystem.emission` 이
        /// 구조체 왕복을 만들어 프레임당 할당이 생긴다 — `UP-TECH-05` 가 0 B 를 요구한다.
        /// </summary>
        private void ApplyLevel()
        {
            int budget = _values.MaxParticlesFor(_level);
            float t = _level == RiskLevel.Stable ? 0f
                    : _level == RiskLevel.Strain ? 0.34f
                    : _level == RiskLevel.Critical ? 0.7f : 1f;

            SetEmission(_dust,  Mathf.Lerp(14f, 34f, t), budget);
            SetEmission(_rust,  Mathf.Lerp(0f, 20f, t), budget);
            // 스파크는 지속 배출이 아니라 사건에만 — 상시로 켜면 「고장난 기계」가 아니라
            // 「용접 중」으로 보인다. 배출률 0 이되 버스트는 살아 있다.
            SetEmission(_spark, 0f, budget);
            SetEmission(_purify, 0f, budget);
            SetEmission(_cascade, 0f, budget);
        }

        private static void SetEmission(ParticleSystem system, float rate, int budget)
        {
            if (system == null) return;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = budget;
        }

        private static void Burst(ParticleSystem system, int count)
        {
            if (system == null) return;
            system.Emit(count);
        }

        private ParticleSystem Build(string label, Color color, float size,
                                     float speed, float lifetime, bool worldSpace)
        {
            var host = new GameObject("Particles_" + label);
            host.transform.SetParent(transform, false);
            var system = host.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.startColor = color;
            main.startSize = size;
            main.startSpeed = speed;
            main.startLifetime = lifetime;
            main.maxParticles = _values.MaxParticlesFor(RiskLevel.Stable);
            main.simulationSpace = worldSpace
                ? ParticleSystemSimulationSpace.World
                : ParticleSystemSimulationSpace.Local;
            main.gravityModifier = worldSpace ? 0.12f : 0.005f;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_volumeRadius * 2f, _volumeHeight, _volumeRadius * 2f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                if (_shared != null) renderer.sharedMaterial = _shared;
                // 결과판을 가리면 안 된다 — `UP-VIS-10` 이 명시적으로 금지한다.
                // 그래서 그림자도 안 받고 안 만든다. 오버드로우도 이쪽이 싸다.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            system.Play();
            _all.Add(system);
            return system;
        }

        private void OnDestroy()
        {
            if (_shared != null) Destroy(_shared);
        }
    }
}
