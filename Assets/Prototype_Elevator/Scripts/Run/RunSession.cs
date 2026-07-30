using System;
using System.Collections.Generic;
using Ascend.Prototype.Build;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>Pure-C# ten-floor run coordinator.</summary>
    public sealed class RunSession
    {
        private readonly IFloorPlanSource _floors;
        private readonly SpinEngine _engine;
        private readonly PowerThresholds _thresholds;
        private readonly List<FloorResult> _results = new List<FloorResult>();
        private readonly BuildLoadout _loadout = new BuildLoadout();
        private readonly List<BuildItem> _lastDeparted = new List<BuildItem>();
        private ResidualState _residual;
        private FloorSession _current;
        private float _baseWeight;
        private readonly float _anteRatio;
        private readonly float _anteEscalation;

        public RunSession(int seed = 1337, float startingWeight = 0f, float startingMoney = 0f)
            : this(seed, startingWeight, startingMoney,
                FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation)
        {
        }

        public RunSession(int seed, float startingWeight, float startingMoney,
            float anteRatio, float anteEscalation)
            : this(seed, startingWeight, startingMoney, anteRatio, anteEscalation, null)
        {
        }

        /// <summary>
        /// <paramref name="floors"/>가 null이면 10층 커리큘럼. Hero Slice는
        /// <see cref="HeroSliceFloorSource"/>를 넘긴다.
        /// </summary>
        public RunSession(int seed, float startingWeight, float startingMoney,
            float anteRatio, float anteEscalation, IFloorPlanSource floors)
        {
            _floors = floors ?? new TenFloorSource();
            _engine = new SpinEngine(seed);
            _thresholds = PowerThresholds.Default;
            Seed = seed;
            _baseWeight = Math.Max(0f, startingWeight);
            Money = startingMoney;
            _anteRatio = Math.Max(0f, anteRatio);
            _anteEscalation = Math.Max(0f, anteEscalation);
            CurrentFloor = _floors.FirstFloor;
            CreateCurrentFloor();
        }

        /// <summary>이 런의 층 구성. HUD가 "1층 중 1층"인지 "10층 중 3층"인지 표시할 때 쓴다.</summary>
        public IFloorPlanSource Floors => _floors;

        /// <summary>엔진이 캐스케이드 하드 캡에 걸렸을 때 알림을 받으려는 어댑터용.</summary>
        public SpinEngine Engine => _engine;

        public int Seed { get; }
        public int CurrentFloor { get; private set; }
        public int HighestFloorReached { get; private set; }

        /// <summary>지금 실려 있는 승객·부품. 층을 건너 살아남으며 무게와 규칙을 함께 바꾼다.</summary>
        public BuildLoadout Loadout => _loadout;

        /// <summary>직전 층 도착에서 내린 승객. HUD와 사고 기록기가 읽는다.</summary>
        public IReadOnlyList<BuildItem> LastDeparted => _lastDeparted;

        /// <summary>기본 무게 + 적재 무게. 요구 전력과 위험 점수가 이 값을 본다.</summary>
        public float CarriedWeight => _baseWeight + _loadout.TotalWeight;

        /// <summary>허용 중량(짐꾼 보너스 포함). 넘으면 과적이다.</summary>
        public float WeightCapacity => FloorSession.AllowedWeight + _loadout.TotalCapacityBonus;

        public bool IsOverloaded => CarriedWeight > WeightCapacity;
        public float Money { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsFailed { get; private set; }
        public string FailureReason { get; private set; }
        public FloorSession Current => _current;
        public FloorSession Floor => _current;
        public IReadOnlyList<FloorResult> Results => _results;
        public ResidualState Residual => _residual;
        public RunResult Result => new RunResult(
            IsComplete && !IsFailed, IsComplete, IsFailed, HighestFloorReached,
            Money, CarriedWeight, FailureReason, _results);

        public bool SelectContract(int choiceIndex) => _current != null && _current.SelectContract(choiceIndex);
        public bool PushYourLuck() => _current != null && _current.PushYourLuck();
        public bool ContinueSpinning() => PushYourLuck();
        public SpinResolution Spin() => _current == null ? default(SpinResolution) : _current.Spin();

        public FloorResult Bank()
        {
            if (_current == null) return null;
            FloorResult result = _current.Bank();
            if (result != null) CompleteFloor(result);
            return result;
        }

        public FloorResult ForceResolve()
        {
            if (_current == null) return null;
            FloorResult result = _current.ForceResolve();
            if (result != null) CompleteFloor(result);
            return result;
        }

        /// <summary>적재 단계에서 후보 하나를 싣는다.</summary>
        public bool TakeBuildOffer(int index) => _current != null && _current.TakeOffer(index);

        /// <summary>문을 닫고 다음 단계로 넘어간다. 아무것도 싣지 않아도 진행된다.</summary>
        public bool FinishBoarding() => _current != null && _current.FinishBoarding();

        /// <summary>Updates the load before the next floor is created.</summary>
        public bool AddWeight(float amount)
        {
            if (amount < 0f || IsComplete || IsFailed) return false;
            _baseWeight += amount;
            return true;
        }

        public bool SetCarriedWeight(float weight)
        {
            if (weight < 0f || IsComplete || IsFailed) return false;
            _baseWeight = weight;
            return true;
        }

        public void AddMoney(float amount)
        {
            if (!IsComplete && !IsFailed) Money += amount;
        }

        private void CreateCurrentFloor()
        {
            if (CurrentFloor < _floors.FirstFloor || CurrentFloor > _floors.LastFloor)
            {
                IsComplete = true;
                _current = null;
                return;
            }

            FloorPlan plan = _floors.For(CurrentFloor);
            // 기본 무게만 넘긴다. 적재 무게는 층이 `_loadout`에서 직접 읽는다 —
            // 적재 단계에서 무게가 바뀌면 요구 전력이 그 자리에서 갱신되어야 하기 때문이다.
            _current = new FloorSession(plan, _engine, _thresholds,
                _baseWeight, _residual, _anteRatio, _anteEscalation, _loadout);
        }

        /// <summary>
        /// 목적지에 닿은 승객을 내리고 요금을 받는다. 부품은 목적지가 없어 남는다.
        ///
        /// 층 도착 직후, 다음 층을 만들기 **전에** 호출한다. 순서가 뒤집히면 이미 내린
        /// 승객의 무게로 요구 전력을 계산하게 된다.
        /// </summary>
        private void DisembarkAt(int floor)
        {
            _lastDeparted.Clear();
            List<BuildItem> leaving = _loadout.TakeDeparting(floor, out float reward);
            if (leaving.Count == 0) return;

            _lastDeparted.AddRange(leaving);
            Money += reward;
        }

        private void CompleteFloor(FloorResult result)
        {
            _results.Add(result);
            _residual = _current.Residual;

            if (!result.Succeeded)
            {
                IsFailed = true;
                FailureReason = result.FailureReason;
                _current = null;
                return;
            }

            int ascended = ClampAscent(CurrentFloor, result.FloorsAscended);

            // 도달 층은 건물 높이를 넘지 않는다. 10층 건물에서 "13층 도달"은 보고서에
            // 넣을 수 없는 숫자다.
            HighestFloorReached = Math.Max(HighestFloorReached,
                Math.Min(_floors.LastFloor, CurrentFloor + ascended));
            CurrentFloor += ascended;

            // 도착한 층에서 내린다. 다음 층을 만들기 전에 해야 이미 내린 승객의 무게가
            // 요구 전력에 섞이지 않는다. 건물 밖으로 나간 런은 전원 하차로 본다.
            DisembarkAt(Math.Min(CurrentFloor, _floors.LastFloor));

            // 추가 층에 쓴 전력은 돈이 되지 않는다. 예전에는 잉여 전체를 돈으로 주면서
            // 동시에 그 잉여로 층까지 올랐다 — 같은 전력을 두 번 쓴 것이다
            // (`AscendResult.AllocateSurplus`가 원래 막으려던 지점).
            float spentOnExtraFloors = Math.Max(0, ascended - result.Ascent.BaseFloors) *
                                       result.Ascent.PowerPerExtraFloor;
            Money += Math.Max(0f, result.ExcessPower - spentOnExtraFloors);
            CreateCurrentFloor();
        }

        /// <summary>
        /// 다층 상승이 삼켜서는 안 되는 층 앞에서 멈춘다.
        ///
        /// 자동 다층 상승은 높은 임계점의 보상이지만, 그대로 두면 커리큘럼을 지운다.
        /// 실측(시드 1337·4242)에서 1→2→3→4→**8**→9로 뛰어 5·6·7층을 통째로 건너뛰었고,
        /// 5개 시드 중 2개가 최종 층인 10층을 치르지 않고 런을 끝냈다. 가르치는 층과
        /// 종합 시험을 건너뛴 완주는 "10층까지 진행했다"의 증거가 되지 못한다.
        ///
        /// 두 가지만 막는다. 그 외의 건너뛰기는 보상으로 남긴다.
        ///   1) 최종 층 — 종합 시험은 반드시 치른다.
        ///   2) 빌드 보상 층 — 승객·부품을 얻는 유일한 지점이라 건너뛰면 빌드가 성립하지 않는다.
        /// </summary>
        private int ClampAscent(int from, int floorsAscended)
        {
            if (floorsAscended <= 1) return floorsAscended;

            // 최종 층에서의 상승은 런 종료다. 여기서 자르면 층을 벗어나지 못해
            // 같은 층이 무한히 다시 생성된다.
            if (from >= _floors.LastFloor) return floorsAscended;

            int target = Math.Min(from + floorsAscended, _floors.LastFloor);
            for (int floor = from + 1; floor < target; floor++)
            {
                if (_floors.For(floor).OffersBuildReward)
                {
                    target = floor;
                    break;
                }
            }
            return Math.Max(1, target - from);
        }
    }
}
