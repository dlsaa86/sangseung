// HHRunState.cs — 런에 얹히는 것들: 승객·화물·내기.
using System;
using System.Collections.Generic;

namespace HeavensHunger
{
    /// <summary>명부에 오른 이름 하나.</summary>
    public sealed class AboardPassenger
    {
        public PassengerDef Def;
        public bool Fused;             // 융합 완료(지정 가족 부품을 건넸다)
        public int BoardedAtFloor;
        public int W { get { return Def.W; } }
    }

    /// <summary>인터폰 거래로 실은 화물.</summary>
    public sealed class CargoItem
    {
        public int W;
        public string Name;
        public int PickAt;             // 이 층에 닿으면 내린다
        public bool DropNext;          // 다음 정차에 알아서 내린다
        public int Pay;
    }

    /// <summary>종의 내기 — 이번 레버에 줄이 Need 개 이상 서면 두 배.</summary>
    public sealed class BetSlip { public int N; public int Need; }

    /// <summary>이번 정차에 문 앞에 선 사람 / 인터폰에 걸린 거래.</summary>
    public sealed class StopOffers
    {
        public PassengerDef Passenger;
        public DealDef Deal;
        public bool DealTaken;
        public bool PassengerAnswered;
    }
}
