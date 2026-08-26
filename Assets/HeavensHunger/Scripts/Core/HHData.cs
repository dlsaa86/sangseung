// HHData.cs — 해븐즈 헝거 코어 데이터 정본
// 원본: 상승 sangseung_proto.html · coreB(RULESET 3.0, 2026-08-24 라이브)
//       balance/01_symbols.csv · 02_stages.csv · 04_config.csv · 10_progression.csv · 11_items.csv
// 이식 원칙: 숫자는 하나도 바꾸지 않는다. 3D 표현이 어려운 것만 UI로 옮긴다.
using System;
using System.Collections.Generic;

namespace HeavensHunger
{
    public enum SymKind
    {
        // 문양(살) — 터진다
        TOOTH, BONE, EAR, TONGUE, HEART, BRAIN, LUNG,
        // 장치(설비) — 뱃지로 붙어 증폭한다
        CAP, TRANS, AMP, CORE, FURN, LID, GRIND
    }

    public enum SymFamily { Flesh, Device }

    public sealed class SymDef
    {
        public SymKind Kind;
        public string Name;      // 한국어 이름
        public string Glyph;     // 이모지 (UI 폴백)
        public SymFamily Family;
        public int Val;          // 기본 W (장치는 0)
        public int Price;        // 상점 가격(닢)
        public string Rarity;    // C / U / R
        public int[] W;          // 상점 등장 가중 [초반, 중반, 후반]
        public string Desc;
    }

    /// <summary>coreB SYMB 테이블 원문 이식.</summary>
    public static class HHSymbols
    {
        public static readonly SymDef[] All = new SymDef[]
        {
            new SymDef{ Kind=SymKind.TOOTH,  Name="어금니", Glyph="🦷", Family=SymFamily.Flesh, Val=1,  Price=2,  Rarity="C", W=new[]{30,16,8}, Desc="말초. 가장 흔하고 가장 싸다" },
            new SymDef{ Kind=SymKind.BONE,   Name="뼈",     Glyph="🦴", Family=SymFamily.Flesh, Val=2,  Price=3,  Rarity="C", W=new[]{20,14,8}, Desc="말초. 어금니보다 굵다" },
            new SymDef{ Kind=SymKind.EAR,    Name="귀",     Glyph="👂", Family=SymFamily.Flesh, Val=3,  Price=3,  Rarity="C", W=new[]{10,12,8}, Desc="말초. 소리를 듣던 것" },
            new SymDef{ Kind=SymKind.TONGUE, Name="혀",     Glyph="👅", Family=SymFamily.Flesh, Val=4,  Price=4,  Rarity="U", W=new[]{6,10,8},  Desc="말초 중 가장 값진 것" },
            new SymDef{ Kind=SymKind.HEART,  Name="심장",   Glyph="🫀", Family=SymFamily.Flesh, Val=6,  Price=6,  Rarity="U", W=new[]{3,8,10},  Desc="장기. 크게 터진다" },
            new SymDef{ Kind=SymKind.BRAIN,  Name="뇌",     Glyph="🧠", Family=SymFamily.Flesh, Val=9,  Price=8,  Rarity="R", W=new[]{1,4,8},   Desc="장기. 더 크게 터진다" },
            new SymDef{ Kind=SymKind.LUNG,   Name="폐",     Glyph="🫁", Family=SymFamily.Flesh, Val=13, Price=10, Rarity="R", W=new[]{0,2,6},   Desc="장기. 가장 크게 터진다" },

            new SymDef{ Kind=SymKind.CAP,   Name="축전지",   Glyph="🧲", Family=SymFamily.Device, Val=0, Price=4,  Rarity="C", W=new[]{8,8,8}, Desc="발동 시 이번 레버 배율 +0.5× (레버당 1회)" },
            new SymDef{ Kind=SymKind.TRANS, Name="변압기",   Glyph="⚡", Family=SymFamily.Device, Val=0, Price=6,  Rarity="U", W=new[]{4,6,8}, Desc="발동한 줄을 ×1.3 (합성 ×1.6→×2.2 · 줄마다 적용)" },
            new SymDef{ Kind=SymKind.AMP,   Name="안테나",   Glyph="📡", Family=SymFamily.Device, Val=0, Price=5,  Rarity="U", W=new[]{3,5,7}, Desc="발동 시 +0.2×/레벨 · 발동 레벨합 2+면 +0.5×/레벨" },
            new SymDef{ Kind=SymKind.CORE,  Name="융합 코어", Glyph="💠", Family=SymFamily.Device, Val=0, Price=12, Rarity="R", W=new[]{1,2,5}, Desc="폭발 합 12W+ 이면 총 전력 ×2 (합성 +0.5)" },
            new SymDef{ Kind=SymKind.FURN,  Name="소각로",   Glyph="♨️", Family=SymFamily.Device, Val=0, Price=7,  Rarity="U", W=new[]{2,5,7}, Desc="발동 시 눈을 태워 +2.5W/개 (합성 1→2→4개) — 시선이 연료다" },
            new SymDef{ Kind=SymKind.LID,   Name="눈꺼풀",   Glyph="🫦", Family=SymFamily.Device, Val=0, Price=6,  Rarity="U", W=new[]{2,4,6}, Desc="발동 시 눈을 레벨 수만큼 감긴다 (순수 제거)" },
            new SymDef{ Kind=SymKind.GRIND, Name="분쇄기",   Glyph="⚙️", Family=SymFamily.Device, Val=0, Price=8,  Rarity="R", W=new[]{1,3,5}, Desc="발동 시 풀의 최저 문양을 먹고 영구 +2W (문양 5개 미만이면 쉰다)" },
        };

        static Dictionary<SymKind, SymDef> _map;
        public static SymDef Get(SymKind k)
        {
            if (_map == null)
            {
                _map = new Dictionary<SymKind, SymDef>();
                foreach (var s in All) _map[s.Kind] = s;
            }
            return _map[k];
        }
        public static bool IsFlesh(SymKind k) { return Get(k).Family == SymFamily.Flesh; }
    }

    public enum ItemTier { Common, Unique, Active }
    public enum ActiveKind { None, Swap, Surge, Lever }

    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        public string Glyph;
        public ItemTier Tier;
        public int Cost;
        public bool IsRate;           // 문양 가중 아이템 (공급 고갈 대상)
        public SymKind? RateTarget;
        public float M1Add;           // 레버 배율 합연산
        public int LuckAdd;           // 상시 운
        public int FreeLever;         // 정차마다 첫 레버 되감기
        public ActiveKind Act;
        public int Charge;            // 액티브 기본 충전
        public string Fx;
    }

    /// <summary>coreB V2ITEMS 원문 이식 (RoR식 3계층).</summary>
    public static class HHItems
    {
        public static readonly ItemDef[] All = new ItemDef[]
        {
            new ItemDef{ Id="iT", Name="어금니 주머니", Glyph="🦷", Tier=ItemTier.Common, Cost=2, IsRate=true, RateTarget=SymKind.TOOTH,  Fx="어금니 등장 가중 ×1.4 (곱연산 스택)" },
            new ItemDef{ Id="iB", Name="뼈 표본함",     Glyph="🦴", Tier=ItemTier.Common, Cost=2, IsRate=true, RateTarget=SymKind.BONE,   Fx="뼈 등장 가중 ×1.4 (곱연산 스택)" },
            new ItemDef{ Id="iE", Name="귀 컬렉션",     Glyph="👂", Tier=ItemTier.Common, Cost=2, IsRate=true, RateTarget=SymKind.EAR,    Fx="귀 등장 가중 ×1.4 (곱연산 스택)" },
            new ItemDef{ Id="iG", Name="혀 절임",       Glyph="👅", Tier=ItemTier.Common, Cost=3, IsRate=true, RateTarget=SymKind.TONGUE, Fx="혀 등장 가중 ×1.4 (곱연산 스택)" },
            new ItemDef{ Id="iH", Name="심장 방부액",   Glyph="🫀", Tier=ItemTier.Common, Cost=3, IsRate=true, RateTarget=SymKind.HEART,  Fx="심장 등장 가중 ×1.4 (곱연산 스택)" },
            new ItemDef{ Id="iN", Name="뇌수 표본",     Glyph="🧠", Tier=ItemTier.Common, Cost=4, IsRate=true, RateTarget=SymKind.BRAIN,  Fx="뇌 등장 가중 ×1.4 (곱연산 스택)" },
            new ItemDef{ Id="iL", Name="폐포 슬라이드", Glyph="🫁", Tier=ItemTier.Common, Cost=4, IsRate=true, RateTarget=SymKind.LUNG,   Fx="폐 등장 가중 ×1.4 (곱연산 스택)" },
            new ItemDef{ Id="iC", Name="구리 코일",     Glyph="🧵", Tier=ItemTier.Common, Cost=3, M1Add=0.1f, Fx="레버 배율 +0.1× (합연산 스택)" },

            new ItemDef{ Id="uP", Name="족제비 앞발",   Glyph="🐾", Tier=ItemTier.Unique, Cost=5, LuckAdd=2,  Fx="상시 운 +2 — 하나뿐이다" },
            new ItemDef{ Id="uA", Name="증폭 결정",     Glyph="💎", Tier=ItemTier.Unique, Cost=6, M1Add=0.4f, Fx="레버 배율 +0.4× — 하나뿐이다" },
            new ItemDef{ Id="uH", Name="기관사의 습관", Glyph="🎩", Tier=ItemTier.Unique, Cost=5, FreeLever=1,Fx="정차마다 첫 레버를 +1 되감는다 (탱크 5 상한) — 하나뿐이다" },

            new ItemDef{ Id="aS", Name="확률 반전기",   Glyph="🔀", Tier=ItemTier.Active, Cost=4, Act=ActiveKind.Swap,  Charge=2, Fx="발동: 3레버 동안 최고↔최저 등장 문양의 지분을 맞바꾼다 · 충전 2 (도착마다 +1)" },
            new ItemDef{ Id="aV", Name="과부하 손잡이", Glyph="🎚", Tier=ItemTier.Active, Cost=3, Act=ActiveKind.Surge, Charge=2, Fx="발동: 다음 레버 운 +4 · 충전 2 (도착마다 +1)" },
            new ItemDef{ Id="aL", Name="예비 레버 태엽",Glyph="🕹", Tier=ItemTier.Active, Cost=4, Act=ActiveKind.Lever, Charge=2, Fx="발동: 레버 +1 충전 (탱크 5 미만일 때만) · 충전 2 (도착마다 +1)" },
        };

        public static ItemDef Get(string id)
        {
            foreach (var i in All) if (i.Id == id) return i;
            return null;
        }
    }

    /// <summary>coreB DIAL — 튜닝 다이얼. 원본 DIAL_TUNED + coreB 오버라이드 값 그대로.</summary>
    public static class HHDial
    {
        public const int   BoardCols   = 5;
        public const int   BoardRows   = 3;
        public const int   Cells       = BoardCols * BoardRows;   // 15

        // 레버 = 충전식 탱크. 상한 5, 재충전은 아이템만 (동전 구매 폐지)
        public const int   LeverTank   = 5;

        // 목표 전력 (정차 1~10). 재보정 2026-08-25 — 7종 시작 풀 + 레버 탱크 5 기준
        public static readonly int[] StageTarget = { 12, 18, 26, 55, 100, 120, 150, 160, 175, 200 };
        public const float LateBeta     = 0.52f;   // 11정차+ : T = 200 × (층/186)^0.52
        public const float LateAnchor   = 186f;
        public const int   FinalFloor   = 7734;

        // 점프(층 상승)
        public const int   JumpCapBase  = 5;
        public const float JumpCapRel   = 0.15f;   // 상한 = 5 + floor(현재층 × 0.15)
        public const int   WrapMax      = 4;       // 겹침 상한

        // 눈(眼)
        public const int   EyeMaxN      = 8;
        public const int   EyeFromFloor = 3;
        /// <summary>설계자 지시(2026-08-25): 눈배수 빌드는 나중에. 부품의 eyeMult 기여를 여기서 게이트한다.</summary>
        public const bool  EyeMultFromMods = false;
        public const float EyeBase      = 1.0f;    // 눈 배수 기본 (부품 없으면 ×1 = 순수 손해)
        public const float GrindGateAt  = 1.5f;    // 눈 배수 1.5+ 빌드는 줄이 눈을 갈지 않는다
        public const float SnowBase     = 0.08f;   // 강설 0.08 + 0.02×(정차-4)
        public const float SnowStep     = 0.02f;
        public const float SnowMax      = 0.6f;

        // 뱃지
        public const float DeviceBadgeRate = 0.35f; // 장치 뱃지 부착률
        public const float BellBadgeRate   = 0.08f; // 벨 뱃지 부착률

        // 종
        public const int   GaugeNeed      = 3;
        public const int   GaugeNeedMax   = 7;
        public const int   GaugeTierEvery = 2;

        // 운
        public const int   PityFrom  = 2;
        public const int   PityStep  = 1;
        public const int   IlsLuck   = 4;   // 초반 유도 착지
        public const int   IlsStops  = 1;
        public static readonly int[] IlsPlan = { 3, 5 };
        public const float MissFrac  = 0.10f;
        public const float LuckSeatP = 0.20f;  // 운 1점 = 20% 확률로 1회 앉힘

        // 판정
        public const float MultiLineStep = 0.2f;  // 동시 N줄 = ×(1+0.2(N-1))
        public const float CoreThreshold = 12f;   // 융합 코어 임계 폭발합
        public const float FurnaceW      = 2.5f;  // 소각 눈 1개당 W
        public const float KillReqPct    = 0.08f;

        // 경제
        public const int   RemoveCost = 3;
        public const int   RerollCost = 1;
        /// <summary>남긴 레버 누진 환산 — 1·2·3·5·8 (그 이상은 8+4×초과).</summary>
        public static int SpareCoins(int rem)
        {
            int[] t = { 0, 1, 2, 3, 5, 8 };
            if (rem <= 5) return t[Math.Max(0, Math.Min(5, rem))];
            return 8 + 4 * (rem - 5);
        }

        /// <summary>정차 k(0-base)의 목표 전력. 10 이상은 층수 기반 절차 생성.</summary>
        public static int TargetOfStop(int k, int floorNow)
        {
            if (k < StageTarget.Length) return StageTarget[k];
            return (int)Math.Round(StageTarget[StageTarget.Length - 1] *
                       Math.Pow(Math.Max(1, floorNow) / LateAnchor, LateBeta));
        }
    }
}
