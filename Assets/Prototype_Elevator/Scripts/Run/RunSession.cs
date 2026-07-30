using System;
using System.Collections.Generic;
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
        private ResidualState _residual;
        private FloorSession _current;
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
            CarriedWeight = Math.Max(0f, startingWeight);
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
        public float CarriedWeight { get; private set; }
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

        /// <summary>Updates the load before the next floor is created.</summary>
        public bool AddWeight(float amount)
        {
            if (amount < 0f || IsComplete || IsFailed) return false;
            CarriedWeight += amount;
            return true;
        }

        public bool SetCarriedWeight(float weight)
        {
            if (weight < 0f || IsComplete || IsFailed) return false;
            CarriedWeight = weight;
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
            _current = new FloorSession(plan, _engine, _thresholds,
                CarriedWeight, _residual, _anteRatio, _anteEscalation);
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

            HighestFloorReached = Math.Max(HighestFloorReached,
                CurrentFloor + result.FloorsAscended);
            CurrentFloor += result.FloorsAscended;
            // The default run banks surplus between floors. A caller that wants a
            // different economy can allocate from result.Ascent before continuing.
            Money += result.ExcessPower;
            CreateCurrentFloor();
        }
    }
}
