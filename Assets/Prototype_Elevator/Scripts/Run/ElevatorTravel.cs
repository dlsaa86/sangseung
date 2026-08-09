using System;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// 이번 라운드의 목표 — **전력량이 아니라 층수다** (2026-08-09 사용자 결정).
    ///
    /// 직전 모델은 「층마다 요구 전력을 채우면 그 층을 통과」였다. 새 모델은
    /// 「라운드마다 목표 층이 있고, 정해진 스핀 안에 거기 도달하지 못하면 죽는다」이다.
    /// 그 사이에는 전력이 있는 한 **자유롭게 오르내린다.**
    ///
    /// 화면에 띄우는 것은 목표 층수와, 거기까지 가는 데 필요한 **총 전력**이다
    /// (<see cref="ElevatorTravel.PowerToReach"/>). 층당 요구 전력을 따로 보여 주면
    /// 플레이어가 매번 곱셈을 해야 한다.
    /// </summary>
    public readonly struct RoundGoal
    {
        /// <summary>이번 라운드 안에 도달해야 하는 층.</summary>
        public readonly int TargetFloor;

        /// <summary>이번 라운드에 주어지는 스핀 수.</summary>
        public readonly int Spins;

        /// <summary>
        /// 목표에 도달한 **그 시점의** 남은 스핀 하나당 지급되는 돈.
        ///
        /// ⚠ 라운드가 끝난 뒤가 아니라 **도달 순간**의 잔량이다. 도달하면 라운드가
        /// 즉시 끝나므로 둘은 같은 값이지만, 「끝까지 돌린 뒤 세는 것」으로 구현하면
        /// 일찍 도달할 이유가 사라진다 — 효율에 대한 보상이 이 규칙의 전부다.
        /// </summary>
        public readonly float MoneyPerUnusedSpin;

        public RoundGoal(int targetFloor, int spins, float moneyPerUnusedSpin)
        {
            TargetFloor = targetFloor;
            Spins = Math.Max(0, spins);
            MoneyPerUnusedSpin = Math.Max(0f, moneyPerUnusedSpin);
        }

        /// <summary>
        /// 도달했는가. **넘어서도 도달이다** — 목표를 지나쳐 올라간 것을 실패로 만들면
        /// 오버슈트가 죽음이 되어, 「전력이 남으면 더 올라가도 된다」와 정면으로 부딪힌다.
        /// </summary>
        public bool IsReached(int floor) => floor >= TargetFloor;

        /// <summary>도달 시 지급되는 돈. 도달하지 못했으면 0 이다.</summary>
        public float MoneyFor(int floor, int spinsRemaining)
        {
            if (!IsReached(floor)) return 0f;
            return Math.Max(0, spinsRemaining) * MoneyPerUnusedSpin;
        }

        public bool IsValid => TargetFloor > 0 && Spins > 0;
    }

    /// <summary>한 번의 이동 시도 결과. 거절된 이유가 문자열로 남는다.</summary>
    public readonly struct TravelResult
    {
        public readonly int FromFloor;
        public readonly int ToFloor;
        public readonly float PowerSpent;
        public readonly float PowerRemaining;

        /// <summary>거절 사유. 성공이면 null.</summary>
        public readonly string Rejection;

        public TravelResult(int from, int to, float spent, float remaining, string rejection)
        {
            FromFloor = from; ToFloor = to;
            PowerSpent = spent; PowerRemaining = remaining;
            Rejection = rejection;
        }

        public bool Accepted => Rejection == null;

        /// <summary>실제로 움직인 층수. 부호가 있다 — 음수면 내려간 것이다.</summary>
        public int FloorsMoved => ToFloor - FromFloor;
    }

    /// <summary>
    /// 층 이동 규칙. **순수 계산이다** — 런도 씬도 모른다.
    ///
    /// ## 오르내림은 대칭이다
    ///
    /// 3층 올라가나 3층 내려가나 같은 전력을 쓴다. 하강을 싸게 만들면 「일단 내려갔다
    /// 올라오기」가 항상 이득인 구간이 생기고, 비싸게 만들면 상점·이벤트 층에 들르는
    /// 비용이 층 높이에 따라 달라져 설계가 어려워진다.
    ///
    /// ## 하강은 아직 소비처가 없다
    ///
    /// 2026-08-09 현재 아래층에 갈 이유가 게임 안에 없다 — 목표는 늘 위에 있고
    /// 도달하면 라운드가 끝난다. **그래도 구현한다**: 특정 층에 상점·이벤트를 둘
    /// 예정이라는 사용자 결정이 있고, 그것이 붙는 순간 필요한 규칙이기 때문이다.
    /// 붙기 전까지는 「호출되지 않는 정상 코드」다 — 죽은 코드가 아니라 대기 중인 코드다.
    /// </summary>
    public readonly struct ElevatorTravel
    {
        /// <summary>층 하나를 오르내리는 데 드는 전력.</summary>
        public readonly float PowerPerFloor;

        public readonly int MinFloor;
        public readonly int MaxFloor;

        public const float DefaultPowerPerFloor = 60f;

        public ElevatorTravel(float powerPerFloor, int minFloor, int maxFloor)
        {
            PowerPerFloor = Math.Max(0.0001f, powerPerFloor);
            MinFloor = minFloor;
            MaxFloor = Math.Max(minFloor, maxFloor);
        }

        /// <summary>층수 차이의 비용. 방향과 무관하다.</summary>
        public float CostFor(int floorDelta) => Math.Abs(floorDelta) * PowerPerFloor;

        /// <summary>
        /// 목표까지 가는 데 필요한 **총 전력**. 상승 버튼 위에 띄우는 값이 이것이다.
        /// 이미 도달했거나 지나쳤으면 0 이다 — 내려올 필요는 없다.
        /// </summary>
        public float PowerToReach(int fromFloor, int targetFloor)
        {
            int up = targetFloor - fromFloor;
            return up <= 0 ? 0f : up * PowerPerFloor;
        }

        /// <summary>주어진 전력으로 움직일 수 있는 최대 층수 (방향 무관, 절대값).</summary>
        public int MaxFloorsFor(float power)
        {
            if (power <= 0f) return 0;
            return (int)Math.Floor(power / PowerPerFloor);
        }

        /// <summary>
        /// 이동을 시도한다. **전부 되거나 전혀 안 된다** — 요청한 만큼 못 가면 거절한다.
        ///
        /// 부분 이동을 허용하지 않는 이유: 「3층 올라가기」를 눌렀는데 2층만 올라가면
        /// 플레이어는 전력을 얼마나 쓴 건지 화면을 다시 읽어야 한다. 갈 수 있는 층수는
        /// <see cref="MaxFloorsFor"/> 로 미리 알 수 있으므로 버튼 쪽에서 막는 편이 낫다.
        /// </summary>
        public TravelResult Move(int fromFloor, int floorDelta, float availablePower)
        {
            if (floorDelta == 0)
                return new TravelResult(fromFloor, fromFloor, 0f, availablePower, null);

            int to = fromFloor + floorDelta;
            if (to < MinFloor)
                return new TravelResult(fromFloor, fromFloor, 0f, availablePower,
                    $"{MinFloor}층 아래로는 갈 수 없다 (요청 {to}층)");
            if (to > MaxFloor)
                return new TravelResult(fromFloor, fromFloor, 0f, availablePower,
                    $"{MaxFloor}층 위로는 갈 수 없다 (요청 {to}층)");

            float cost = CostFor(floorDelta);
            if (cost > availablePower + 0.0001f)
                return new TravelResult(fromFloor, fromFloor, 0f, availablePower,
                    $"전력 부족 — {Math.Abs(floorDelta)}층에 {cost:0.##} 필요, 보유 {availablePower:0.##}");

            return new TravelResult(fromFloor, to, cost, availablePower - cost, null);
        }
    }
}
