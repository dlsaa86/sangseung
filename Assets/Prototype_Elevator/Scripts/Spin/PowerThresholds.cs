using System;

namespace Ascend.Prototype.Spin
{
    /// <summary>요구 전력 대비 달성 구간. 노션 01 "전력 임계점" + 03 "부분 실패"를 하나로 합친 것이다.</summary>
    public enum PowerBand
    {
        /// <summary>70% 미만 — 추락·런 종료 위험.</summary>
        Crash = 0,
        /// <summary>70~89% — 승객·화물 포기 또는 큰 대가.</summary>
        Jettison = 1,
        /// <summary>90~99% — 비상 상승과 장치 손상.</summary>
        Damaged = 2,
        /// <summary>100~129% — 정상 상승.</summary>
        Normal = 3,
        /// <summary>130~169% — 추가 보상.</summary>
        Rewarded = 4,
        /// <summary>170~219% — 추가 층 상승.</summary>
        MultiFloor = 5,
        /// <summary>220~299% — 과수확 보너스.</summary>
        Overharvest = 6,
        /// <summary>300% 이상 — 폭주 상승.</summary>
        Runaway = 7,
    }

    /// <summary>
    /// 임계점 표. 수치는 노션 기준 임시값이며 플레이테스트로 조정한다 — 그래서 상수가 아니라
    /// 인스턴스 필드다. 밸런스 반복이 코드 수정 없이 데이터에서 끝나야 한다.
    /// </summary>
    [Serializable]
    public struct PowerThresholds
    {
        public float CrashCeiling;      // 0.70
        public float JettisonCeiling;   // 0.90
        public float DamagedCeiling;    // 1.00
        public float RewardedFloor;     // 1.30
        public float MultiFloorFloor;   // 1.70
        public float OverharvestFloor;  // 2.20
        public float RunawayFloor;      // 3.00

        public static PowerThresholds Default => new PowerThresholds
        {
            CrashCeiling     = 0.70f,
            JettisonCeiling  = 0.90f,
            DamagedCeiling   = 1.00f,
            RewardedFloor    = 1.30f,
            MultiFloorFloor  = 1.70f,
            OverharvestFloor = 2.20f,
            RunawayFloor     = 3.00f,
        };

        public PowerBand BandFor(float power, float required)
        {
            if (required <= 0f) return PowerBand.Normal;
            float ratio = power / required;
            if (ratio < CrashCeiling)     return PowerBand.Crash;
            if (ratio < JettisonCeiling)  return PowerBand.Jettison;
            if (ratio < DamagedCeiling)   return PowerBand.Damaged;
            if (ratio < RewardedFloor)    return PowerBand.Normal;
            if (ratio < MultiFloorFloor)  return PowerBand.Rewarded;
            if (ratio < OverharvestFloor) return PowerBand.MultiFloor;
            if (ratio < RunawayFloor)     return PowerBand.Overharvest;
            return PowerBand.Runaway;
        }

        /// <summary>다음 임계점까지 남은 전력. 계기판이 "조금만 더" 긴장을 만들 수 있게 한다.</summary>
        public float PowerToNextBand(float power, float required)
        {
            if (required <= 0f) return 0f;
            float ratio = power / required;
            float[] gates = { CrashCeiling, JettisonCeiling, DamagedCeiling, RewardedFloor,
                              MultiFloorFloor, OverharvestFloor, RunawayFloor };
            foreach (float gate in gates)
                if (ratio < gate) return (gate - ratio) * required;
            return 0f;
        }
    }

    public static class PowerBands
    {
        public static string DisplayName(this PowerBand band)
        {
            switch (band)
            {
                case PowerBand.Crash:       return "추락 위험";
                case PowerBand.Jettison:    return "화물 포기";
                case PowerBand.Damaged:     return "비상 상승";
                case PowerBand.Normal:      return "정상 상승";
                case PowerBand.Rewarded:    return "추가 보상";
                case PowerBand.MultiFloor:  return "다층 상승";
                case PowerBand.Overharvest: return "과수확";
                default:                    return "폭주 상승";
            }
        }

        /// <summary>이 구간에서 상승이 성립하는가. Damaged 이하는 대가를 치르고 오르거나 못 오른다.</summary>
        public static bool Ascends(this PowerBand band) => band >= PowerBand.Damaged;
    }
}
