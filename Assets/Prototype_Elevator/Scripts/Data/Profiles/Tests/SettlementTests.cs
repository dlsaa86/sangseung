using System;
using System.Text;
using Ascend.Prototype.Data.Profiles;
using UnityEngine;

namespace Ascend.Prototype.Data.Profiles.Tests
{
    /// <summary>
    /// 남은 스핀 **운행 효율 정산** 규칙 (`T-05` 2026-08-02 결정).
    ///
    /// ## 무엇을 반증하려는가
    ///
    /// 이 규칙의 목적은 **「지금 멈춘다」를 실제 선택으로 만드는 것**이다.
    /// 직전까지 남은 스핀의 가치가 0 이라 과수확이 유일하게 합리적인 선택이었다.
    ///
    /// 그래서 검사해야 하는 것은 「정산이 계산되는가」가 아니라 **두 선택이
    /// 실제로 겨루는가**다. 구체적으로 —
    ///
    /// | 검사 | 깨지면 무엇이 무너지나 |
    /// |---|---|
    /// | 과수확 후 정산 0 | 두 선택이 겨루지 않는다 — 과수확이 공짜가 된다 |
    /// | 층당 상한 | 정산만으로 한 층을 더 오른다 (5% × 10 = 50%) |
    /// | 스핀 0 회에서 정산 0 | 다 쓰고도 보상을 받는다 |
    /// | 요구 전력 0 방어 | 0 나눗셈·무한 정산 |
    /// | 상한 도달 스핀 수 | UI 가 「더 남겨도 소용없다」를 못 알린다 |
    ///
    /// 순수 계산이라 씬 없이 전부 검사된다.
    /// </summary>
    public static class SettlementTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();

            Run("T-05 기본값이 규격과 같다 (5% · 20%)", TestDefaults, ref passed, ref failed, report);
            Run("남은 스핀에 비례해 정산이 는다", TestProportional, ref passed, ref failed, report);
            Run("층당 상한 20% 를 넘지 않는다", TestFloorCap, ref passed, ref failed, report);
            Run("스핀 0 회면 정산이 0 이다", TestZeroSpins, ref passed, ref failed, report);
            Run("요구 전력이 0 이면 정산이 0 이다 (0 나눗셈 방어)", TestZeroRequired, ref passed, ref failed, report);
            Run("상한 도달 스핀 수를 정확히 센다", TestSpinsToCap, ref passed, ref failed, report);
            Run("상한 판정이 경계에서 옳다", TestCapDetection, ref passed, ref failed, report);
            Run("돈 환산이 비율을 따른다", TestMoneyConversion, ref passed, ref failed, report);
            Run("음수 입력이 정산을 만들지 않는다", TestNegativeInputs, ref passed, ref failed, report);
            Run("에셋 없이도 규칙이 사라지지 않는다 (코드 프리셋)", TestFallback, ref passed, ref failed, report);

            return (passed, failed, report.ToString());
        }

        private static RemainingSpinSettlementSnapshot Default =>
            RemainingSpinSettlementProfile.DefaultSnapshot;

        private static void TestDefaults()
        {
            // `T-05` — 「남은 스핀 1회당 요구 전력의 5%」·「층당 상한 20%」.
            // 숫자를 여기 고정한다. 누가 바꾸면 규격과 어긋난 사실이 여기서 드러난다.
            Approx(Default.PerSpinRatio, 0.05f, "회당 비율");
            Approx(Default.FloorCapRatio, 0.20f, "층당 상한 비율");
        }

        private static void TestProportional()
        {
            const float required = 400f;
            float one = Default.SettlementPower(1, required);
            float two = Default.SettlementPower(2, required);
            float three = Default.SettlementPower(3, required);

            Approx(one, 20f, "1회 정산");     // 400 × 0.05
            Approx(two, 40f, "2회 정산");
            Approx(three, 60f, "3회 정산");
            // 상한(80)에 닿기 전까지는 선형이어야 한다 — 그래야 「한 번 더 남기면
            // 얼마」가 플레이어에게 계산 가능하다.
            Approx(two - one, one, "증분이 일정하다");
        }

        private static void TestFloorCap()
        {
            const float required = 400f;
            float cap = required * 0.20f;   // 80

            // 4회면 정확히 상한, 5회 이상은 더 오르지 않는다.
            Approx(Default.SettlementPower(4, required), cap, "4회 = 상한");
            Approx(Default.SettlementPower(5, required), cap, "5회도 상한");
            Approx(Default.SettlementPower(99, required), cap, "99회도 상한");

            // **이 검사가 이 파일의 핵심이다.** 상한이 없으면 5% × 10회 = 50% 라
            // 정산만으로 한 층을 더 오르는 양이 되고, 그러면 「안 돌린 스핀」이
            // 「돌린 스핀」보다 나아진다.
            if (Default.SettlementPower(10, required) >= required * 0.5f)
                throw new Exception("10회 정산이 요구 전력의 50% 에 닿는다 — 상한이 작동하지 않는다");
        }

        private static void TestZeroSpins()
        {
            Approx(Default.SettlementPower(0, 400f), 0f, "0회 정산");
            Approx(Default.SettlementMoney(0, 400f), 0f, "0회 환산");
        }

        private static void TestZeroRequired()
        {
            // 요구 전력이 0 인 층은 없어야 하지만, 있어도 정산이 폭주하면 안 된다.
            Approx(Default.SettlementPower(5, 0f), 0f, "요구 0");
            Approx(Default.SettlementPower(5, -100f), 0f, "요구 음수");
        }

        private static void TestSpinsToCap()
        {
            // 0.20 / 0.05 = 4
            if (Default.SpinsToCap != 4)
                throw new Exception($"상한 도달 스핀 수 {Default.SpinsToCap} ≠ 4");

            // 비율이 0 이면 영원히 상한에 안 닿는다 — 0 나눗셈이 나면 안 된다.
            var zero = new RemainingSpinSettlementSnapshot(0f, 0.2f, 1f, "테스트");
            if (zero.SpinsToCap != int.MaxValue)
                throw new Exception("회당 비율 0 에서 상한 도달 수가 무한이 아니다");
        }

        private static void TestCapDetection()
        {
            const float required = 400f;
            if (Default.IsCapped(3, required)) throw new Exception("3회에서 상한으로 판정됐다");
            if (Default.IsCapped(4, required)) throw new Exception("정확히 상한인 4회를 초과로 판정했다");
            if (!Default.IsCapped(5, required)) throw new Exception("5회를 상한 초과로 판정하지 못했다");
            if (Default.IsCapped(0, required)) throw new Exception("0회가 상한으로 판정됐다");
        }

        private static void TestMoneyConversion()
        {
            var half = new RemainingSpinSettlementSnapshot(0.05f, 0.20f, 0.5f, "테스트");
            Approx(half.SettlementMoney(2, 400f), 20f, "환산 0.5배");   // 40 전력 × 0.5

            var doubled = new RemainingSpinSettlementSnapshot(0.05f, 0.20f, 2f, "테스트");
            Approx(doubled.SettlementMoney(2, 400f), 80f, "환산 2배");
        }

        private static void TestNegativeInputs()
        {
            // 생성자가 음수를 막는다 — 막지 않으면 음수 정산이 돈을 깎는다.
            var s = new RemainingSpinSettlementSnapshot(-1f, -1f, -1f, "테스트");
            if (s.PerSpinRatio < 0f || s.FloorCapRatio < 0f || s.MoneyPerPower < 0f)
                throw new Exception("음수 비율이 그대로 들어갔다");
            Approx(s.SettlementPower(5, 400f), 0f, "음수 비율의 정산");
            Approx(Default.SettlementPower(-3, 400f), 0f, "음수 스핀 수");
        }

        private static void TestFallback()
        {
            // 에셋을 배선하지 않아도 규칙이 살아 있어야 한다. 이 저장소는
            // 프로파일 8종이 여덟 세션 동안 죽어 있는 것을 못 본 적이 있다.
            RemainingSpinSettlementSnapshot s =
                RemainingSpinSettlementProfile.SnapshotOrDefault(null, "SettlementTests");
            Approx(s.PerSpinRatio, 0.05f, "폴백 회당 비율");
            if (string.IsNullOrEmpty(s.Source))
                throw new Exception("폴백이 출처를 남기지 않았다 — 「배선했다」와 구분할 수 없다");
        }

        // ── 도구 ────────────────────────────────────────────────────────────

        private static void Approx(float actual, float expected, string what, float tol = 0.0001f)
        {
            if (Mathf.Abs(actual - expected) > tol)
                throw new Exception($"{what}: {actual:F4} ≠ {expected:F4}");
        }

        private static void Run(string name, Action test, ref int passed, ref int failed, StringBuilder report)
        {
            try { test(); passed++; report.AppendLine($"  PASS  {name}"); }
            catch (Exception e) { failed++; report.AppendLine($"  FAIL  {name}\n        {e.Message}"); }
        }
    }
}
