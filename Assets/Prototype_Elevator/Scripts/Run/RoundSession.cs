using System;

namespace Ascend.Prototype.Run
{
    /// <summary>라운드가 어떻게 끝났는가.</summary>
    public enum RoundOutcome
    {
        InProgress,
        /// <summary>목표 층에 도달했다.</summary>
        Survived,
        /// <summary>스핀을 다 쓰고도 목표에 못 닿았다 — 추락.</summary>
        Crashed,
    }

    /// <summary>
    /// 한 라운드. **코어 루프의 새 척추다** (2026-08-09 사용자 결정).
    ///
    /// ```
    /// 룰렛을 돌린다 → 전력을 얻는다
    ///   → 정해진 스핀 안에 목표 층에 도달하면 산다
    ///   → 못 하면 추락
    ///   → 그 사이에는 전력이 있는 한 자유롭게 오르내린다 (층당 전력 소모)
    ///   → 도달 시점의 남은 스핀만큼 돈이 나온다
    /// ```
    ///
    /// ## 직전 모델과 무엇이 다른가
    ///
    /// 직전에는 「층마다 요구 전력을 채우면 그 층을 통과」였다 — 층이 **진행 단위**였다.
    /// 지금은 층이 **자유 변수**이고 진행 단위는 라운드다. 요구 전력은 층 통과 조건이
    /// 아니라 **이동 비용**이 됐다.
    ///
    /// ## 스핀이 떨어져도 라운드가 바로 끝나지 않는다
    ///
    /// 마지막 스핀을 돌린 뒤에도 **이동은 남아 있다.** 그러지 않으면 「마지막 스핀 전에
    /// 미리 올라가 둬야 한다」는 규칙이 암묵적으로 생기고, 그건 아무 데도 안 적혀 있다.
    /// 전력을 5번 모아서 한 번에 올라가는 것이 가장 자연스러운 플레이인데 그 길이 막힌다.
    /// 그래서 스핀 소진은 <see cref="Resolve"/> 를 **열어 줄 뿐** 끝내지 않는다.
    ///
    /// ## 순수 계산이다
    ///
    /// 씬도 유니티도 모른다. 스핀이 전력을 얼마나 주는지도 모른다 — 밖에서 넣어 준다
    /// (<see cref="Spin"/>). 그래서 이 클래스만 따로 검사할 수 있다.
    /// </summary>
    public sealed class RoundSession
    {
        private readonly RoundGoal _goal;
        private readonly ElevatorTravel _travel;
        private readonly int _startFloor;

        public RoundSession(RoundGoal goal, ElevatorTravel travel, int startFloor, float carriedPower = 0f)
        {
            _goal = goal;
            _travel = travel;
            _startFloor = startFloor;
            CurrentFloor = startFloor;
            Power = Math.Max(0f, carriedPower);
            Outcome = RoundOutcome.InProgress;

            // 시작하자마자 목표 위에 있을 수 있다 — 이월 전력으로 지난 라운드에 이미
            // 올라와 버린 경우다. 그때 라운드를 굴리게 두면 스핀을 낭비하게 된다.
            if (_goal.IsReached(CurrentFloor)) Finish(RoundOutcome.Survived);
        }

        public RoundGoal Goal => _goal;
        public ElevatorTravel Travel => _travel;
        public int StartFloor => _startFloor;

        public int CurrentFloor { get; private set; }
        public float Power { get; private set; }
        public int SpinsUsed { get; private set; }
        public RoundOutcome Outcome { get; private set; }

        /// <summary>도달해서 받은 돈. 미달이거나 진행 중이면 0.</summary>
        public float MoneyEarned { get; private set; }

        public int SpinsRemaining => Math.Max(0, _goal.Spins - SpinsUsed);
        public bool IsOver => Outcome != RoundOutcome.InProgress;

        /// <summary>목표까지 더 필요한 전력. **상승 버튼 위에 뜨는 값이다.**</summary>
        public float PowerToGoal => _travel.PowerToReach(CurrentFloor, _goal.TargetFloor);

        /// <summary>지금 전력으로 움직일 수 있는 최대 층수. 버튼이 이걸로 자기를 막는다.</summary>
        public int MaxFloorsNow => _travel.MaxFloorsFor(Power);

        /// <summary>지금 당장 목표까지 올라갈 수 있는가. 상승 버튼의 활성 조건이다.</summary>
        public bool CanReachGoalNow => !IsOver && Power + 0.0001f >= PowerToGoal;

        /// <summary>
        /// 스핀 한 번. 밖에서 계산한 전력을 받아 넣는다.
        /// **전력에 상한이 없다** — 퍼센트가 아니라 통화에 가까운 수치다.
        /// </summary>
        public bool Spin(float powerGained)
        {
            if (IsOver || SpinsRemaining <= 0) return false;
            SpinsUsed++;
            Power += Math.Max(0f, powerGained);
            return true;
        }

        /// <summary>
        /// 층을 옮긴다. 음수면 내려간다. 도달하면 그 자리에서 라운드가 끝난다.
        /// </summary>
        public TravelResult Move(int floorDelta)
        {
            if (IsOver)
                return new TravelResult(CurrentFloor, CurrentFloor, 0f, Power, "라운드가 이미 끝났다");

            TravelResult r = _travel.Move(CurrentFloor, floorDelta, Power);
            if (!r.Accepted) return r;

            CurrentFloor = r.ToFloor;
            Power = r.PowerRemaining;

            // 도달은 **이동 직후에** 판정한다. 스핀 소진까지 미루면 남은 스핀이 0 이 되어
            // 돈이 사라진다 — 일찍 도달할 이유가 통째로 없어진다.
            if (_goal.IsReached(CurrentFloor)) Finish(RoundOutcome.Survived);
            return r;
        }

        /// <summary>
        /// 라운드를 끝낸다. **스핀이 남아 있으면 거절한다** — 아직 할 수 있는 일이 있다.
        /// 스핀을 다 쓰고도 목표에 못 닿았을 때 추락을 확정하는 유일한 경로다.
        /// </summary>
        public bool Resolve()
        {
            if (IsOver) return false;
            if (SpinsRemaining > 0) return false;
            Finish(_goal.IsReached(CurrentFloor) ? RoundOutcome.Survived : RoundOutcome.Crashed);
            return true;
        }

        private void Finish(RoundOutcome outcome)
        {
            Outcome = outcome;
            MoneyEarned = outcome == RoundOutcome.Survived
                ? _goal.MoneyFor(CurrentFloor, SpinsRemaining)
                : 0f;
        }
    }
}
