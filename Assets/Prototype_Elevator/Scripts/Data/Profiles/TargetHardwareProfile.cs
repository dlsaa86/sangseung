using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 성능 판정의 기준이 되는 하드웨어와 목표치. `MASTER_PRD.md` §13.2가 "지정 하드웨어
    /// 기준 성능 조건 충족"을 완료 조건으로 걸고 있는데, **그 하드웨어가 어디에도 적혀
    /// 있지 않았다** — `UP-PLAT-04`. 기준이 없으면 "90 FPS 나왔다"는 문장이 무엇에 대한
    /// 주장인지 알 수 없고, 기기를 옮기면 과거 수치와 비교조차 되지 않는다.
    ///
    /// 값은 <see cref="Ratified"/>가 켜지기 전까지 **가정**이다(`PD-10`). 성능 리포트가
    /// <see cref="TargetHardwareSnapshot.Describe"/>를 한 줄 인용하도록 만든 이유가 이것이다 —
    /// 미승인 상태가 리포트 본문에 같이 찍혀야 나중에 그 수치를 근거로 완료를 선언하지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "TargetHardwareProfile",
                     menuName = "Ascend/Profiles/Target Hardware", order = 100)]
    public sealed class TargetHardwareProfile : ScriptableObject
    {
        [Header("기기")]
        [Tooltip("CPU 모델명. 측정 리포트에 그대로 인용된다.")]
        [SerializeField] private string _cpu = DefaultCpu;

        [Tooltip("GPU 모델명.")]
        [SerializeField] private string _gpu = DefaultGpu;

        [Tooltip("설치 RAM. 단위를 포함한 문자열로 둔다 — 듀얼채널 여부처럼 숫자로 안 담기는 것이 있다.")]
        [SerializeField] private string _ram = DefaultRam;

        [Tooltip("OS 이름과 빌드. 같은 GPU라도 드라이버·OS 빌드가 다르면 렌더 결과가 달라진다.")]
        [SerializeField] private string _operatingSystem = DefaultOperatingSystem;

        [Tooltip("그래픽 API. D3D11/D3D12/Vulkan은 같은 씬에서도 프레임 비용이 다르다.")]
        [SerializeField] private string _graphicsApi = DefaultGraphicsApi;

        [Header("측정 조건")]
        [Tooltip("기준 해상도 가로. `TECH_SPEC.md` §13이 1920×1080으로 고정한다.")]
        [SerializeField] private int _referenceWidth = DefaultReferenceWidth;

        [Tooltip("기준 해상도 세로.")]
        [SerializeField] private int _referenceHeight = DefaultReferenceHeight;

        [Tooltip("모니터 주사율. 프레임 시간이 이 상한에 붙어 있으면 그것은 비용이 아니라 vSync다.")]
        [SerializeField] private float _refreshRateHz = DefaultRefreshRateHz;

        [Tooltip("vSync를 끄고 측정하는가. 켠 채로 잰 중앙값은 상한이지 비용이 아니다.")]
        [SerializeField] private bool _measureWithVSyncOff = true;

        [Header("목표")]
        [Tooltip("일반 플레이 목표 FPS. `TECH_SPEC.md` §13.")]
        [SerializeField] private float _targetFps = DefaultTargetFps;

        [Tooltip("최악 장면에서도 지켜야 하는 하한 FPS. 이 아래로 내려가면 장면을 고친다.")]
        [SerializeField] private float _hardFloorFps = DefaultHardFloorFps;

        [Header("승인")]
        [Tooltip("사용자가 이 기기를 PRD §13의 기준 하드웨어로 확정했는가. 꺼져 있으면 성능 완료를 선언할 수 없다(PD-10).")]
        [SerializeField] private bool _ratified;

        // ── 코드 기본값 ────────────────────────────────────────────────────────
        // `ASSUMPTION_LOG.md` A-20260731-01 / `PENDING_DECISIONS.md` PD-10의 개발 기기다.
        // 여기 상수로 둔 이유: 에셋이 아직 없어도 성능 프로브가 인용할 기준이 있어야 한다.
        public const string DefaultCpu = "AMD Ryzen 5 5600X";
        public const string DefaultGpu = "NVIDIA GeForce RTX 3070";
        public const string DefaultRam = "32 GB";
        public const string DefaultOperatingSystem = "Windows 11 Pro (10.0.26200)";
        public const string DefaultGraphicsApi = "Direct3D12";
        public const int DefaultReferenceWidth = 1920;
        public const int DefaultReferenceHeight = 1080;
        public const float DefaultRefreshRateHz = 120f;
        public const float DefaultTargetFps = 90f;
        public const float DefaultHardFloorFps = 60f;

        public string Cpu => _cpu;
        public string Gpu => _gpu;
        public string Ram => _ram;
        public string OperatingSystem => _operatingSystem;
        public string GraphicsApi => _graphicsApi;
        public int ReferenceWidth => _referenceWidth;
        public int ReferenceHeight => _referenceHeight;
        public float RefreshRateHz => _refreshRateHz;
        public bool MeasureWithVSyncOff => _measureWithVSyncOff;
        public float TargetFps => _targetFps;
        public float HardFloorFps => _hardFloorFps;
        public bool Ratified => _ratified;

        /// <summary>인스펙터에서 갓 만든 에셋이 곧바로 쓸 만하도록 채운다. Unity가 호출한다.</summary>
        public void Reset()
        {
            _cpu = DefaultCpu;
            _gpu = DefaultGpu;
            _ram = DefaultRam;
            _operatingSystem = DefaultOperatingSystem;
            _graphicsApi = DefaultGraphicsApi;
            _referenceWidth = DefaultReferenceWidth;
            _referenceHeight = DefaultReferenceHeight;
            _refreshRateHz = DefaultRefreshRateHz;
            _measureWithVSyncOff = true;
            _targetFps = DefaultTargetFps;
            _hardFloorFps = DefaultHardFloorFps;
            _ratified = false;
        }

        public TargetHardwareSnapshot Snapshot()
        {
            return new TargetHardwareSnapshot(_cpu, _gpu, _ram, _operatingSystem, _graphicsApi,
                _referenceWidth, _referenceHeight, _refreshRateHz, _measureWithVSyncOff,
                _targetFps, _hardFloorFps, _ratified);
        }

        /// <summary>
        /// 에셋이 없을 때 쓰는 값. ScriptableObject를 정적으로 들고 있으면 도메인 리로드에서
        /// 파괴된 객체를 참조하게 되므로, 정적으로 두는 것은 값 스냅샷 쪽이다.
        /// </summary>
        public static TargetHardwareSnapshot DefaultSnapshot
        {
            get
            {
                return new TargetHardwareSnapshot(DefaultCpu, DefaultGpu, DefaultRam,
                    DefaultOperatingSystem, DefaultGraphicsApi,
                    DefaultReferenceWidth, DefaultReferenceHeight, DefaultRefreshRateHz,
                    true, DefaultTargetFps, DefaultHardFloorFps, false);
            }
        }

        /// <summary>프로파일이 null이어도 성능 코드가 계속 돌게 한다. null이면 왜 null인지 남긴다.</summary>
        public static TargetHardwareSnapshot SnapshotOrDefault(TargetHardwareProfile profile, string caller)
        {
            if (profile != null) return profile.Snapshot();
            Debug.LogWarning($"[상승] TargetHardwareProfile 이 배선되지 않았다 ({caller}). " +
                             "코드 기본값(PD-10 개발 기기)으로 진행한다 — 이 수치로 성능 완료를 선언하지 않는다.");
            return DefaultSnapshot;
        }
    }

    /// <summary>
    /// <see cref="TargetHardwareProfile"/>의 값 사본. 에셋 없이도 돌아야 하는 코드가 쓴다.
    /// </summary>
    public readonly struct TargetHardwareSnapshot
    {
        public readonly string Cpu;
        public readonly string Gpu;
        public readonly string Ram;
        public readonly string OperatingSystem;
        public readonly string GraphicsApi;
        public readonly int ReferenceWidth;
        public readonly int ReferenceHeight;
        public readonly float RefreshRateHz;
        public readonly bool MeasureWithVSyncOff;
        public readonly float TargetFps;
        public readonly float HardFloorFps;

        /// <summary>사용자가 기준 하드웨어로 확정했는가. false면 성능 완료 선언이 금지된다(PD-10).</summary>
        public readonly bool Ratified;

        public TargetHardwareSnapshot(string cpu, string gpu, string ram, string operatingSystem,
            string graphicsApi, int referenceWidth, int referenceHeight, float refreshRateHz,
            bool measureWithVSyncOff, float targetFps, float hardFloorFps, bool ratified)
        {
            Cpu = cpu;
            Gpu = gpu;
            Ram = ram;
            OperatingSystem = operatingSystem;
            GraphicsApi = graphicsApi;
            ReferenceWidth = referenceWidth;
            ReferenceHeight = referenceHeight;
            RefreshRateHz = refreshRateHz;
            MeasureWithVSyncOff = measureWithVSyncOff;
            TargetFps = targetFps;
            HardFloorFps = hardFloorFps;
            Ratified = ratified;
        }

        /// <summary>목표 FPS에 해당하는 프레임 예산(ms). 두 곳에 적으면 반드시 어긋나므로 계산한다.</summary>
        public float TargetFrameTimeMs => MillisecondsFor(TargetFps);

        /// <summary>하한 FPS에 해당하는 프레임 시간(ms). 최악 장면이 이 값을 넘으면 실패다.</summary>
        public float HardFloorFrameTimeMs => MillisecondsFor(HardFloorFps);

        /// <summary>vSync 상한에 해당하는 프레임 시간(ms).</summary>
        public float RefreshCeilingMs => MillisecondsFor(RefreshRateHz);

        private static float MillisecondsFor(float fps) => fps > 0.0001f ? 1000f / fps : 0f;

        public bool MeetsTarget(float medianFrameMs) => medianFrameMs <= TargetFrameTimeMs;
        public bool MeetsFloor(float worstFrameMs) => worstFrameMs <= HardFloorFrameTimeMs;

        /// <summary>
        /// 잰 값이 비용이 아니라 vSync 상한일 가능성을 판정한다.
        ///
        /// 실제로 겪은 실패다 — `Logs/heroslice_perf.txt`의 중앙값 8.33ms를 "여유 있다"로
        /// 읽었지만 그것은 120Hz vSync 상한이었다(백로그 `UP-TEST` 항목의 「남은 문제」).
        /// 상한에 붙은 값으로는 90 FPS 목표를 판정할 수 없다.
        /// </summary>
        public bool LooksLikeVSyncCeiling(float medianFrameMs)
        {
            if (MeasureWithVSyncOff) return false;
            float ceiling = RefreshCeilingMs;
            if (ceiling <= 0.0001f) return false;
            return medianFrameMs >= ceiling - 0.25f && medianFrameMs <= ceiling + 0.25f;
        }

        /// <summary>성능 리포트가 한 줄로 인용하는 형태. 미승인이면 그 사실이 함께 찍힌다.</summary>
        public string Describe()
        {
            string vsync = MeasureWithVSyncOff ? "vSync 끔" : $"vSync 켬({RefreshRateHz:0}Hz)";
            string approval = Ratified ? "기준 확정" : "미승인 — PD-10";
            return $"기준 하드웨어: {Cpu} / {Gpu} / {Ram} / {OperatingSystem} / {GraphicsApi} · " +
                   $"{ReferenceWidth}×{ReferenceHeight} · 목표 {TargetFps:0} FPS ({TargetFrameTimeMs:0.00} ms) · " +
                   $"하한 {HardFloorFps:0} FPS ({HardFloorFrameTimeMs:0.00} ms) · {vsync} · [{approval}]";
        }
    }
}

// 씬 배선 필요: 없음. 개발 전용 데이터다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/TargetHardwareProfile.asset
//   (Create ▸ Ascend ▸ Profiles ▸ Target Hardware). 에셋이 없어도 DefaultSnapshot 으로 돈다.
// 소비 지점 연결 필요: 성능 프로브(`Logs/heroslice_perf.txt`, `loaded_critical_perf.txt`)가
//   리포트 첫 줄에 Describe() 를 찍어야 UP-PLAT-04 의 「성능 산출물이 이 프로파일을 인용」이 성립한다.
