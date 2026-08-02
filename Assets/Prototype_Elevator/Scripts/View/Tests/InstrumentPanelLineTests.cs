using System;
using System.Text;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View.Tests
{
    /// <summary>
    /// 계기판 상태 라벨이 **세 줄이 되지 않는다**를 씬 없이 단정한다 (`UP-FIX-51`).
    ///
    /// ## 왜 이 검사가 필요한가
    ///
    /// 상태 라벨 셋째 줄은 판 로컬 y **1.402…1.486** 에 앉고, 그 자리는
    /// `CascadeLabel`(**1.400…1.484**) — 계기판 여섯째 줄이다. 겹치면 둘 다 못 읽는다.
    ///
    /// **그런데 자동 가림 계측이 이것을 못 잡는다.** 매니페스트는 글자↔글자 겹침을
    /// 명시적 예외로 두기 때문에 두 라벨을 **둘 다 「온전」이라고 적는다.**
    /// 즉 캡처 검사가 초록인 채로 화면이 손상되는 종류다. 시각 판정이 사람 눈으로
    /// 잡아 `UP-FIX-51` 로 올렸다.
    ///
    /// 그래서 화면이 아니라 **문자열에서** 막는다. 줄 수는 조립 시점에 결정되고,
    /// 조립은 순수 함수 하나가 소유한다.
    ///
    /// ## 무엇을 반증하려는가
    ///
    /// 「흡수체와 증식체가 동시에 남은 층에서만」 세 줄이 됐다. 그래서 대부분의
    /// 캡처에서 보이지 않았고 프레임 `18` 하나에서만 드러났다 — **드물게 나타나는
    /// 손상은 눈으로 찾기 가장 어렵다.** 여기서는 그 조합을 직접 만들어 확인한다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class InstrumentPanelLineTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();
            Append(ref passed, ref failed, report);
            report.Insert(0, "[상승] === 계기판 줄 충돌 Tests (UP-FIX-51) ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        public static void Append(ref int passed, ref int failed, StringBuilder report)
        {
            Run("잔류 줄은 어떤 조합에서도 줄바꿈을 만들지 않는다", TestNeverWraps, ref passed, ref failed, report);
            Run("흡수체 + 증식체가 동시에 있어도 한 줄이다 (18 프레임 조합)", TestBothOnOneLine, ref passed, ref failed, report);
            Run("두 항목의 값이 모두 문자열에 남는다 (정보를 버리지 않았다)", TestKeepsBothValues, ref passed, ref failed, report);
            Run("잔류가 없으면 「잔류 없음」이다", TestCleanState, ref passed, ref failed, report);
            Run("한 항목만 있으면 그 항목만 쓴다", TestSingleItem, ref passed, ref failed, report);
            Run("null 버퍼에 터지지 않는다", TestNullBuffer, ref passed, ref failed, report);
        }

        private static string Build(int absorbers, int proliferators)
        {
            var sb = new StringBuilder();
            InstrumentPanelView.AppendResidual(new ResidualState
            {
                AbsorberCount = absorbers,
                ProliferatorCount = proliferators,
                StoredPowerLoss = 32.0f,
                NextProliferatorWeightAdd = 0.15f,
            }, sb);
            return sb.ToString();
        }

        private static string TestNeverWraps()
        {
            // 0~6 × 0~6 을 전수로 돈다. 「대부분의 조합에서 괜찮다」는 이 결함이
            // 열여덟 라운드를 살아남은 이유 그 자체다.
            for (int a = 0; a <= 6; a++)
                for (int p = 0; p <= 6; p++)
                {
                    string s = Build(a, p);
                    if (s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
                        return $"흡수체 {a} · 증식체 {p} 에서 줄바꿈이 났다 — 「{s}」";
                }
            return null;
        }

        private static string TestBothOnOneLine()
        {
            string s = Build(4, 2);
            if (s.IndexOf('\n') >= 0) return $"줄바꿈이 있다 — 「{s}」";
            return s.Contains("흡수체") && s.Contains("증식체")
                ? null : $"두 항목이 함께 있지 않다 — 「{s}」";
        }

        private static string TestKeepsBothValues()
        {
            // 겹침을 없애려고 정보를 버리면 그건 고친 것이 아니라 지운 것이다.
            string s = Build(4, 2);
            if (!s.Contains("32.0")) return $"흡수체 전력 손실 값이 사라졌다 — 「{s}」";
            if (!s.Contains("0.15")) return $"증식체 가중치 값이 사라졌다 — 「{s}」";
            if (!s.Contains("4") || !s.Contains("2")) return $"개수가 사라졌다 — 「{s}」";
            return null;
        }

        private static string TestCleanState()
        {
            string s = Build(0, 0);
            return s == "잔류 없음" ? null : $"「{s}」";
        }

        private static string TestSingleItem()
        {
            string a = Build(3, 0);
            if (a.Contains("증식체")) return $"흡수체만 있는데 증식체가 나왔다 — 「{a}」";
            string p = Build(0, 5);
            if (p.Contains("흡수체")) return $"증식체만 있는데 흡수체가 나왔다 — 「{p}」";
            // 한 항목만 있을 때 구분자가 붙어 끝나면 안 된다.
            return a.TrimEnd() == a && p.TrimEnd() == p ? null : "구분자 공백이 끝에 남았다";
        }

        private static string TestNullBuffer()
        {
            try { InstrumentPanelView.AppendResidual(ResidualState.Empty, null); return null; }
            catch (Exception e) { return $"{e.GetType().Name}"; }
        }

        private static void Run(string name, Func<string> test,
                                ref int passed, ref int failed, StringBuilder report)
        {
            try
            {
                string failure = test();
                if (string.IsNullOrEmpty(failure)) { passed++; report.AppendLine($"  PASS  {name}"); }
                else { failed++; report.AppendLine($"  FAIL  {name} — {failure}"); }
            }
            catch (Exception exception)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외 {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
