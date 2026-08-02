using UnityEngine;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>런 구성. `CURRENT_PHASE.md`가 활성인 동안 기본값은 HeroSlice다.</summary>
    public enum RunMode
    {
        /// <summary>1층짜리 Hero Slice — 계약 2종·저항체 2종·과수확까지 한 층에서.</summary>
        HeroSlice = 0,

        /// <summary>노션 99의 10층 커리큘럼. Phase 2 이후.</summary>
        TenFloor = 1,
    }

    /// <summary>Thin Unity adapter; all run decisions live in RunSession.</summary>
    public sealed class RunSessionBehaviour : MonoBehaviour
    {
        [SerializeField] private RunMode _mode = RunMode.HeroSlice;
        [SerializeField] private int _seed = 1337;
        [SerializeField] private float _startingWeight;
        [SerializeField] private float _startingMoney;
        [Header("과수확 (UP-POWER-07)")]
        [Tooltip("PRD §7.4의 9개 수치. 비어 있으면 코드 기본값으로 진행하고 그 사실이 OverharvestSource 에 남는다.")]
        [SerializeField] private Data.Profiles.OverharvestProfile _overharvestProfile;

        [Tooltip("프로파일이 없을 때만 쓰는 폴백. 프로파일이 물려 있으면 이 둘은 무시된다.")]
        [SerializeField] private float _anteRatio = FloorSession.DefaultAnteRatio;
        [SerializeField] private float _anteEscalation = FloorSession.DefaultAnteEscalation;

        [Header("과적 (UP-TECH-09 ⑤)")]
        [Tooltip("허용 중량·무게당 요구 전력·과적 배수. 비어 있으면 코드 프리셋으로 진행하고 " +
                 "그 사실이 WeightSource 에 남는다. 밸런스는 어느 쪽이든 같다. " +
                 "PrototypeConfig 의 allowedWeight 를 여기 옮기지 말 것 — 그쪽은 단위가 다른 레거시 경로의 값이다.")]
        [SerializeField] private Data.Profiles.WeightProfile _weightProfile;

        [Header("스핀 밸런스 (UP-TECH-09 ①③)")]
        [Tooltip("심볼 가중치 3종·패턴 배수 3종·정화 최소 개수·연쇄 증분·잔류 2종. " +
                 "비어 있으면 코드 프리셋으로 진행하고 그 사실이 SpinBalanceSource 에 남는다. " +
                 "연쇄 하드 캡(20)은 여기 없다 — 그건 다이얼이 아니라 PRD §6 이 못박은 명세다.")]
        [SerializeField] private Data.Profiles.SpinBalanceProfile _spinBalanceProfile;

        [Header("층별 곡선 (UP-TECH-09 ④)")]
        [Tooltip("10층의 요구 전력·스핀 수·저항 배율. 비어 있으면 코드 곡선을 그대로 쓴다. " +
                 "덮어쓰기지 대체가 아니라서, 채우지 않은 칸은 그 층만 프리셋으로 남는다.")]
        [SerializeField] private Data.Profiles.FloorCurriculumProfile _floorCurriculum;

        public RunSession Session { get; private set; }

        /// <summary>
        /// 과수확 수치가 **어디서 왔는가.** 하네스는 「배선됐는가」가 아니라 「읽혔는가」를 묻는다 —
        /// 인스펙터에 물려 있는 것과 코드가 그 값을 쓰는 것은 다르고, 그 구분을 흐린 것이
        /// 프로파일 7종이 여러 세션 동안 「없다」로 잘못 적혀 있던 이유다.
        /// 폴백이면 「코드 기본값」이 찍히므로 검사가 조용히 통과하지 못한다.
        /// </summary>
        public string OverharvestSource { get; private set; } = "미초기화";

        /// <summary>
        /// 과적 수치가 **어디서 왔는가** (`UP-TECH-09` ⑤). 폴백이면 「코드 프리셋」이 찍힌다.
        ///
        /// 값 비교로는 이 구분이 불가능하다 — `WeightProfile` 의 기본값이 코드 프리셋과
        /// 같은 수이기 때문이다(같지 않으면 에셋을 만드는 순간 밸런스가 조용히 바뀐다).
        /// 그래서 「어느 쪽을 읽었는가」를 따로 남긴다.
        /// </summary>
        public string WeightSource { get; private set; } = "미초기화";

        /// <summary>스핀 밸런스 수치가 **어디서 왔는가** (`UP-TECH-09` ①③).</summary>
        public string SpinBalanceSource { get; private set; } = "미초기화";

        /// <summary>층별 곡선이 **어디서 왔는가** (`UP-TECH-09` ④).</summary>
        public string FloorCurriculumSource { get; private set; } = "미초기화";

        /// <summary>현재 런의 시드. 디버그 패널이 표시·재현에 쓴다.</summary>
        public int Seed => _seed;

        public RunMode Mode => _mode;

        /// <summary>런이 새로 만들어질 때마다 발생. 뷰가 캐시를 버릴 지점이다.</summary>
        public event System.Action<RunSession> RunStarted;

        private void Awake()
        {
            ResetRun();
        }

        /// <summary>
        /// 모드를 바꿔 런을 다시 시작한다. 하네스(캡처·성능·PlayMode 검증)가 씬의
        /// 직렬화 값을 고치지 않고 10층 런을 돌리기 위한 유일한 경로다.
        /// </summary>
        public void ResetRun(RunMode mode, int seed)
        {
            _mode = mode;
            _seed = seed;
            ResetRun();
        }

        public void ResetRun()
        {
            // 곡선 덮어쓰기는 10층 경로에만 뜻이 있다. Hero Slice 는 층이 하나뿐이고
            // 그 층의 요구 전력 460 은 헤드리스 400시드 측정으로 정한 값이라
            // 층 배열로 덮어쓸 대상이 아니다.
            Data.Profiles.FloorCurriculumSnapshot curriculum =
                Data.Profiles.FloorCurriculumProfile.SnapshotOrDefault(
                    _floorCurriculum, nameof(RunSessionBehaviour));
            FloorCurriculumSource = curriculum.SourceName;

            IFloorPlanSource floors = _mode == RunMode.HeroSlice
                ? (IFloorPlanSource)new HeroSliceFloorSource()
                : new TenFloorSource(curriculum);

            // 에셋이 있으면 9개 값 전부가 여기서 온다. 없으면 인스펙터의 판돈 두 값 +
            // 코드 기본값 일곱으로 진행하되, 어느 쪽인지를 기록에 남긴다.
            Data.Profiles.OverharvestSnapshot overharvest;
            if (_overharvestProfile != null)
            {
                overharvest = Data.Profiles.OverharvestProfile.SnapshotOrDefault(
                    _overharvestProfile, nameof(RunSessionBehaviour));
                OverharvestSource = _overharvestProfile.name;
            }
            else
            {
                overharvest = new Data.Profiles.OverharvestSnapshot(
                    Mathf.Max(0f, _anteRatio), Mathf.Max(0f, _anteEscalation),
                    Data.Profiles.OverharvestProfile.DefaultUnlockThreshold,
                    Data.Profiles.OverharvestProfile.DefaultApproachMachineDuckScale,
                    Data.Profiles.OverharvestProfile.DefaultMinSilenceSeconds,
                    Data.Profiles.OverharvestProfile.DefaultMaxSilenceSeconds,
                    Data.Profiles.OverharvestProfile.DefaultPassengerGazeDelaySeconds,
                    Data.Profiles.OverharvestProfile.DefaultResumeFadeSeconds,
                    Data.Profiles.OverharvestProfile.DefaultMaxExtraSpins);
                OverharvestSource = "코드 기본값 + 인스펙터 판돈";
            }

            // 에셋이 없는 것이 지금은 정상이다 — 배선은 씬 소유자의 일이고, 폴백해도
            // 밸런스가 같다. 경고를 띄우지 않는 대신 출처를 남겨 검사가 구분할 수 있게 한다.
            Data.Profiles.WeightSnapshot weight = Data.Profiles.WeightProfile.SnapshotOrDefault(
                _weightProfile, nameof(RunSessionBehaviour));
            WeightSource = weight.SourceName;

            Data.Profiles.SpinBalanceSnapshot balance = Data.Profiles.SpinBalanceProfile.SnapshotOrDefault(
                _spinBalanceProfile, nameof(RunSessionBehaviour));
            SpinBalanceSource = balance.SourceName;

            Session = new RunSession(_seed, _startingWeight, _startingMoney,
                overharvest, weight, balance, floors);

            // 캡 도달은 조용히 넘어가면 안 된다(MASTER_PRD §6). 엔진은 순수 C#이라
            // 로그 채널을 모르므로 Unity 어댑터인 여기서 붙인다.
            Session.Engine.CascadeCapReached += resolution =>
                Debug.LogWarning($"[상승] 캐스케이드 하드 캡 도달 — 보호 종료\n{resolution.DescribeCascade()}");

            RunStarted?.Invoke(Session);
        }

        /// <summary>시드를 바꿔 런을 다시 시작한다. 디버그 패널의 재현 경로.</summary>
        public void ResetRun(int seed)
        {
            _seed = seed;
            ResetRun();
        }

        public bool TakeBuildOffer(int index) => Session != null && Session.TakeBuildOffer(index);
        public bool FinishBoarding() => Session != null && Session.FinishBoarding();
        public bool SelectContract(int choiceIndex) => Session != null && Session.SelectContract(choiceIndex);
        public bool PushYourLuck() => Session != null && Session.PushYourLuck();
        public SpinResolution Spin() => Session == null ? default(SpinResolution) : Session.Spin();
        public FloorResult Bank() => Session == null ? null : Session.Bank();
        public FloorResult ForceResolve() => Session == null ? null : Session.ForceResolve();
    }
}
