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
        [SerializeField] private float _anteRatio = FloorSession.DefaultAnteRatio;
        [SerializeField] private float _anteEscalation = FloorSession.DefaultAnteEscalation;

        public RunSession Session { get; private set; }

        /// <summary>현재 런의 시드. 디버그 패널이 표시·재현에 쓴다.</summary>
        public int Seed => _seed;

        public RunMode Mode => _mode;

        /// <summary>런이 새로 만들어질 때마다 발생. 뷰가 캐시를 버릴 지점이다.</summary>
        public event System.Action<RunSession> RunStarted;

        private void Awake()
        {
            ResetRun();
        }

        public void ResetRun()
        {
            IFloorPlanSource floors = _mode == RunMode.HeroSlice
                ? (IFloorPlanSource)new HeroSliceFloorSource()
                : new TenFloorSource();

            Session = new RunSession(_seed, _startingWeight, _startingMoney,
                _anteRatio, _anteEscalation, floors);

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

        public bool SelectContract(int choiceIndex) => Session != null && Session.SelectContract(choiceIndex);
        public bool PushYourLuck() => Session != null && Session.PushYourLuck();
        public SpinResolution Spin() => Session == null ? default(SpinResolution) : Session.Spin();
        public FloorResult Bank() => Session == null ? null : Session.Bank();
        public FloorResult ForceResolve() => Session == null ? null : Session.ForceResolve();
    }
}
