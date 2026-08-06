using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// `docs/runtime/FUN_CRITERIA.md` 의 지표를 시드 다수로 실측한다.
    ///
    /// ## `CurriculumCoverageProbe` 와 무엇이 다른가
    ///
    /// 기존 프로브는 **스핀을 전부 소진**한 뒤 확정한다 — 즉 「요구 전력을 채웠는데 스핀이
    /// 남았다」는 국면을 만들지 않는다. 그런데 그 국면이 이 게임의 대표 장면이고
    /// (`MASTER_PRD` §2.3 감정 곡선의 정점), Notion §7.3 이 유일하게 **숫자로** 목표를
    /// 준 지점이다 — 「안전 확정의 기대값은 추가 스핀 기대값의 30~40%」.
    ///
    /// 그래서 이 스윕은 정책 셋을 나란히 돌린다.
    ///   · 안전   — 확정 가능해지면 즉시 확정 (정산 최대)
    ///   · 탐욕   — 가능한 한 계속 추가 스핀 (정산 포기)
    ///   · 기대값 — 남은 스핀의 기대 순전력과 정산 가치를 비교해 고른다
    ///
    /// 셋의 성적이 갈리지 않으면 선택이 존재하지 않는 것이고, 그건 밸런스 문제가 아니라
    /// **설계 결함**이다. 그 판정을 사람 없이 내리기 위해 이 파일이 있다.
    ///
    /// ## 기대 순전력은 지어내지 않는다
    ///
    /// 기대값 정책이 쓰는 「스핀 하나의 기대 순전력」은 상수가 아니라 **보정 패스로 실측**한다.
    /// 층·계약마다 다르고, 잔류 저항 때문에 스핀 순서에도 의존하기 때문이다.
    /// 2026-08-03 에 「레퍼런스에서 재지 않고 목표를 지어낸」 실패를 겪었다 — 같은 실수를
    /// 정책 안에서 반복하지 않는다.
    /// </summary>
    public static class BalanceSweep
    {
        public const string ReportPath = "docs/runtime/BALANCE_REPORT.md";

        // ── 표본 수는 상수가 아니라 **호출자가 정한다** (2026-08-05) ──────────────
        //
        // 에디터 메뉴로 돌 때는 아래 기본값 그대로다. 값을 열어 둔 이유는 하나뿐이다 —
        // 이 파일이 `UnityEditor` 를 어트리뷰트로만 쓰고 판정 계층이 순수 C# 이라
        // **에디터 밖에서 같은 코드가 그대로 돈다.** 실측: 시드 300 · 정책 3종 전체
        // 스윕이 헤드리스에서 **1.0초**다(에디터 왕복은 도메인 리로드만으로 수십 초).
        // 그 차이가 만드는 것은 속도가 아니라 **표본 수**다. 300 시드에서 층별 클리어율의
        // 95% 신뢰구간은 ±3%p 대인데, 이 게임이 판정하려는 대역폭(초반 97~100%,
        // 후반 82~92%)이 그보다 좁은 구간을 포함한다 — 즉 300 은 대역 판정에 필요한
        // 분해능이 없다. 30000 시드면 ±0.4%p 로 떨어진다.
        //
        // ⚠ 값을 바꾼 채로 두지 않는다. 보고서 머리글에 시드 범위가 찍히므로 서로 다른
        // 표본 수의 보고서를 비교할 때는 그 줄을 먼저 본다.
        public const int DefaultSeedCount = 300;
        public const int DefaultFirstSeed = 20000;
        public const int DefaultCalibrationSeeds = 120;

        /// <summary>정책당 표본 시드 수. 에디터 기본값은 <see cref="DefaultSeedCount"/>.</summary>
        public static int SeedCount = DefaultSeedCount;

        /// <summary>첫 시드. 표본을 늘릴 때 앞 구간이 유지되도록 시작점은 고정한다.</summary>
        public static int FirstSeed = DefaultFirstSeed;

        /// <summary>보정 패스(평균 순전력 실측)의 표본 수.</summary>
        public static int CalibrationSeeds = DefaultCalibrationSeeds;

        /// <summary>에디터 기본값으로 되돌린다. 표본을 키운 러너가 끝나고 부른다.</summary>
        public static void ResetSampling()
        {
            SeedCount = DefaultSeedCount;
            FirstSeed = DefaultFirstSeed;
            CalibrationSeeds = DefaultCalibrationSeeds;
            BoardBuilds = true;
        }

        // ── 🔴 적재를 실제로 한다 (2026-08-07) ──────────────────────────────────
        //
        // 직전까지 이 스윕은 세 루프 전부에서 `FinishBoarding()` 만 불렀다. 그것은
        // 「문을 닫고 넘어간다」이므로 **런 내내 아무것도 실리지 않았다.** 즉
        // `FUN_CRITERIA` 의 모든 대역 — 클리어율·방문률·연쇄 비율·과수확 선택률 —
        // 이 **빌드가 하나도 없는 게임**에 대한 판정이었다. 노션 `GAMEPLAY CORE`
        // §3.2「좋은 선택이 결과를 바꾸는 체감」과 §6「빌드 시스템」전체가
        // 이 게이트에서 **한 번도 측정되지 않았다.**
        //
        // 드러난 경위: 카탈로그에 품목을 하나 더한 뒤 `sweep 20000` 이 **비트 단위로
        // 동일**했다. 15종이 14종일 때와 같은 값을 낼 방법은 하나뿐이다.
        //
        // 검사 쪽도 이미 같은 것을 가리키고 있었다 — 2026-08-06 자체 검증 실패 7건 중
        // 하나가 「적재 무게가 다음 층으로 이어진다 — **런 내내 아무것도 실리지 않음**」.
        //
        // 고르는 규칙은 새로 쓰지 않는다. <see cref="Build.BuildLoadPolicy"/> 가 이미
        // 정본이고 「하네스와 헤드리스 테스트가 **같은 것을** 쓴다」고 스스로 적어
        // 뒀는데 측정 경로의 호출자가 **0곳**이었다. 이 저장소가 세 번째로 만난 같은
        // 모양이다 (`MultiplePatterns` 를 켜는 품목 0개 · `OverharvestSnapshot` 호출자 0곳).
        //
        // `false` 로 두면 옛 판정과 비트 단위로 같다 — 되돌리기가 값 하나다.
        public static bool BoardBuilds = true;

        /// <summary>
        /// 적재 단계를 <see cref="Build.BuildLoadPolicy"/> 로 통과한다.
        /// 정책이 −1 을 주면(자리가 없거나 후보가 없으면) 그만 집고 문을 닫는다.
        ///
        /// 상한 8 은 무한 루프 방지용이다. 슬롯은 승객 4 + 부품 3 이므로 정상 경로는
        /// 7 을 넘지 않는다 — 넘으면 `TakeBuildOffer` 가 자리를 안 줄이고 있다는 뜻이고,
        /// 그건 조용히 도는 것보다 멈추는 편이 낫다.
        /// </summary>
        private static bool RunBoarding(RunSession run, FloorSession f)
        {
            if (f.Phase != FloorPhase.Boarding) return true;

            if (BoardBuilds)
            {
                int guard = 0;
                while (guard++ < 8)
                {
                    int pick = Build.BuildLoadPolicy.PickIndex(f.BuildOffers, f.Loadout);
                    if (pick < 0) break;
                    if (!run.TakeBuildOffer(pick)) break;
                }
            }

            return run.FinishBoarding();
        }

        private const int GuardLimit = 400;

        public enum Policy
        {
            /// <summary>확정 가능해지는 즉시 확정한다. 남은 스핀은 전부 정산된다.</summary>
            Safe,
            /// <summary>추가 스핀이 허용되는 한 계속 당긴다. 정산 권리를 즉시 버린다.</summary>
            Greedy,
            /// <summary>보정 패스로 잰 기대 순전력과 정산 가치를 비교해 고른다.</summary>
            ExpectedValue,
        }

        // ── 후보 곡선 주입 ─────────────────────────────────────────────────────
        //
        // 코드 프리셋을 고치고 다시 컴파일하는 왕복은 라운드마다 1~2분이다.
        // `TenFloorSource` 는 이미 `FloorCurriculumSnapshot` 을 받으므로, 그 경로로
        // 후보 곡선을 **재컴파일 없이** 넣을 수 있다. 채택된 값만 코드 프리셋으로 옮긴다.
        //
        // 주의: 이건 측정 도구의 임시 상태다. 게임은 이 값을 보지 않는다.

        private static float[] _requiredOverride;
        private static RemainingSpinSettlementSnapshot? _settlementOverride;

        private static Data.Profiles.FloorCurriculumSnapshot CurriculumSnapshot()
            => _requiredOverride == null
                ? Data.Profiles.FloorCurriculumProfile.DefaultSnapshot
                : new Data.Profiles.FloorCurriculumSnapshot(_requiredOverride, null, null, "스윕 후보");

        /// <summary>
        /// 층별 목표 소요 스핀. **이 곡선이 난이도 설계의 전부다.**
        ///
        /// 노션 03 「첫 10층 학습 구간」과 「난이도 확장 순서 ② 제한 스핀 압박」에서 나온다.
        ///   · 1~3층 Teach — 2.0~2.4. 여유가 있어야 배운다.
        ///   · 4~7층 Test  — 2.8~3.3. 단 5층은 **휴식**이라 낮춘다
        ///                   (「요구 전력을 올리지 않는 것이 휴식이다」).
        ///   · 8~10층 Twist — 3.6~4.0. 상한 5 에 가까워지되 닿지는 않는다 —
        ///                   닿으면 남는 스핀이 없어 과수확 선택 자체가 사라진다.
        ///
        /// 9층이 10층보다 낮은 것은 의도다. 9층의 핵심 질문이 「이미 올라갈 수 있는데
        /// 한 번 더 돌릴 것인가」라서, 남는 스핀이 **더 많아야** 그 질문이 성립한다.
        /// </summary>
        /// 값의 상한은 **방문률**이 정한다. 첫 시도(2.0~4.0)는 세 구간을 전부
        /// 대역에 넣었지만 완주율 8.7%·10층 방문률 17% 였다 — 층마다는 적당히
        /// 어려운데 열 번 곱해지니 커리큘럼의 뒷부분이 플레이되지 않는다.
        /// `FUN_CRITERIA` §1 의 정정 항목에 경위가 있다.
        ///
        /// 두 번째 시도(1.9~3.4)도 9·10층 방문률이 39% · 26% 였다. 원인은 **분산**이다 —
        /// 상한이 5 스핀인데 평균 3.2 를 목표로 하면 꼬리가 5 를 넘는 런이 30% 나온다.
        /// 상한을 늘리면 분산이 줄지만 `MASTER_PRD` §4.1 이 「한 층 최대 5회 스핀」을
        /// 못박았다. 그래서 목표를 낮추는 쪽으로 간다 — 압박은 스핀 **사용률**이 아니라
        /// 「확정할 수 있는데 스핀이 남았다」는 국면의 빈도로 나온다(§3 지표).
        private static readonly float[] TargetSpins =
            { 0f, 1.7f, 1.8f, 1.9f, 1.3f, 1.9f, 1.9f, 1.8f, 1.75f, 1.75f, 2.2f };
        //                              ↑ 4층만 낮다 — 이유는 아래.
        //
        // ## 4층이 예외인 이유 — 교습 층은 **약한 쪽**에 맞춰야 한다
        //
        // 이 배열의 목표 스핀은 **계약을 거는 플레이어** 기준으로 잰 산출에 곱해진다.
        // 그런데 4층은 계약이 **처음 등장하는 층**이고, 저항이 1종뿐이라 흡수체 계약이
        // 판 전체에 얹힌다 — 실측 산출이 485.5 로 10층을 뺀 전 층 최고다.
        //
        // 1.9 를 곱하면 요구 전력이 920 이 되고, 그 값은 계약을 건 플레이어에게는
        // 92.7% 지만 **계약을 안 건 플레이어에게는 53.2%** 다. 계약이 무엇인지
        // 아직 모르는 플레이어가 만나는 첫 계약 층에서 절반이 벽에 막힌다.
        // 고정 시드 8개 중 6개가 4층에서 죽는 것으로 드러났다.
        //
        // 그래서 4층만 약한 쪽(무계약)에 맞춘다. 계약을 걸면 아주 쉬워지는데,
        // **그게 이 층이 가르치려는 것**이다 — 「위험을 더 불러들이면 보상이 온다」.

        /// <summary>
        /// 측정 → 곡선 도출 → 재측정을 반복해 권장 요구 전력을 낸다.
        ///
        /// 한 번으로 끝나지 않는 이유: 요구 전력을 바꾸면 층이 끝나는 스핀 수가 바뀌고,
        /// 그러면 잔류 저항이 쌓이는 정도가 달라져 **평균 순전력 자체가 움직인다.**
        /// 고정점에 닿을 때까지 몇 번 돈다.
        /// </summary>
        [MenuItem("Ascend/Solve Balance Curve")]
        public static void Solve()
        {
            var log = new StringBuilder();
            log.AppendLine("# 요구 전력 곡선 해찾기");
            log.AppendLine();
            log.AppendLine("목표 소요 스핀: " + string.Join(" / ",
                System.Array.ConvertAll(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                    f => $"{f}층 {TargetSpins[f]:F1}")));
            log.AppendLine();

            float[] required = null;
            for (int iteration = 1; iteration <= 6; iteration++)
            {
                _requiredOverride = required;
                float[] meanNet = Calibrate();

                var next = new float[FloorCount];
                for (int f = 1; f <= FloorCount; f++)
                    next[f - 1] = meanNet[f] > 0.01f
                        ? Mathf.Round(TargetSpins[f] * meanNet[f] / 5f) * 5f
                        : (required != null ? required[f - 1] : 350f);

                float drift = 0f;
                if (required != null)
                    for (int i = 0; i < FloorCount; i++)
                        drift = Mathf.Max(drift, Mathf.Abs(next[i] - required[i]) / Mathf.Max(1f, required[i]));

                log.AppendLine($"## 반복 {iteration}" + (required == null ? " (초기)" : $" — 최대 변동 {drift * 100f:F1}%"));
                log.AppendLine();
                log.AppendLine("| 층 | 평균 순전력 | 목표 스핀 | 권장 요구 전력 |");
                log.AppendLine("|---|---|---|---|");
                for (int f = 1; f <= FloorCount; f++)
                    log.AppendLine($"| {f} | {meanNet[f]:F1} | {TargetSpins[f]:F1} | **{next[f - 1]:F0}** |");
                log.AppendLine();

                required = next;
                if (drift > 0f && drift < 0.02f)
                {
                    log.AppendLine("고정점 도달 (변동 2% 미만). 반복을 멈춘다.");
                    break;
                }
            }

            // ── 정산율 후보 훑기 ──────────────────────────────────────────────
            //
            // 곡선이 정해진 **뒤**에 한다. 정산 가치의 분모(추가 스핀 기대값)가
            // 요구 전력에 달려 있어서, 곡선이 흔들리는 동안 잰 비율은 의미가 없다.
            _requiredOverride = required;
            float[] meanNetFinal = Calibrate();

            log.AppendLine("---");
            log.AppendLine();
            log.AppendLine("## 정산율 후보 — Notion §7.3 「추가 스핀 기대값의 30~40%」");
            log.AppendLine();
            log.AppendLine("| 회당 비율 | 층당 상한 | 정산 EV 비 | 선택 발생률 | 과수확 선택률 |");
            log.AppendLine("|---|---|---|---|---|");

            float bestRatio = 0.05f;
            double bestScore = double.MaxValue;
            foreach (float perSpin in new[] { 0.05f, 0.07f, 0.09f, 0.11f, 0.13f, 0.15f, 0.18f })
            {
                float cap = perSpin * 4f;
                _settlementOverride = new RemainingSpinSettlementSnapshot(perSpin, cap, 1f, "후보");
                SweepStats safe = Sweep(Policy.Safe, meanNetFinal);
                SweepStats ev = Sweep(Policy.ExpectedValue, meanNetFinal);
                (double offer, double ratio, double take) = OverchargeMetrics(safe, ev);

                // **과수확 선택률만 사실상 판정에 쓴다.**
                //
                // 정산 EV 비(Notion §7.3 「추가 스핀 기대값의 30~40%」)는 「추가 스핀
                // 기대값」이 **한 번**인지 **남은 전부**인지 문서가 말하지 않는다.
                // 두 해석이 3~4배 차이 나므로 그 값 하나로 고를 수 없다.
                //
                // 선택률은 해석이 필요 없다 — 25~75% 를 벗어나면 그건 선택이 아니라
                // 정답이거나 함정이고, 그 문장이 `T-05` 가 존재하는 이유 전체다.
                //
                // **2026-08-04: 비율 항을 점수에서 완전히 뺐다.** 무게 0.2 로 낮추는 것으로는
                // 부족했다. 두 지표는 독립이 아니라 **같은 양의 두 표현**이기 때문이다 —
                // `선택률 50% ⟺ ratio = 1/r̄`. 즉 옛 항은 선택률 항과 **반대 방향으로**
                // 당기고 있었고, 판정하지 않는 값을 최적화 목표에 남겨 두면 후보 선택이
                // 조용히 왜곡된다. `ratio` 는 로그에만 남긴다.
                double score = Math.Abs(take - 50.0) / 25.0 * 3.0
                             + Math.Abs(offer - 55.0) / 15.0 * 0.5;
                if (score < bestScore) { bestScore = score; bestRatio = perSpin; }

                log.AppendLine($"| {perSpin:F2} | {cap:F2} | {ratio:F3} | {offer:F1}% | {take:F1}% |");
            }
            _settlementOverride = null;
            log.AppendLine();
            log.AppendLine($"**최적 후보: 회당 {bestRatio:F2} · 상한 {bestRatio * 4f:F2}**");
            log.AppendLine();

            log.AppendLine("---");
            log.AppendLine();
            log.AppendLine("## 이 곡선 + 최적 정산율로 측정한 전체 지표");
            log.AppendLine();
            _settlementOverride = new RemainingSpinSettlementSnapshot(bestRatio, bestRatio * 4f, 1f, "해찾기 최적");
            log.Append(Measure());
            _requiredOverride = null;
            _settlementOverride = null;

            string full = Path.Combine(Directory.GetCurrentDirectory(), SolveReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, log.ToString());
            Debug.Log("[상승] 곡선 해찾기 완료 → " + SolveReportPath
                      + "\n권장 요구 전력: " + string.Join(", ",
                          System.Array.ConvertAll(required, v => v.ToString("F0"))));
        }

        public const string SolveReportPath = "docs/runtime/BALANCE_SOLVE.md";
        private const int FloorCount = 10;

        [MenuItem("Ascend/Run Balance Sweep")]
        public static void Run()
        {
            string report = Measure();
            string full = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, report);
            Debug.Log("[상승] 밸런스 스윕 완료 → " + ReportPath + "\n" + Summary(report));
        }

        private static string Summary(string report)
        {
            // 콘솔에는 요약만. 전문은 파일에 있다.
            var lines = report.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length && i < 60; i++) sb.AppendLine(lines[i]);
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────── 측정 본체

        public static string Measure()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 밸런스 스윕 실측");
            sb.AppendLine();
            sb.AppendLine($"> 시드 {FirstSeed}~{FirstSeed + SeedCount - 1} ({SeedCount}개) · 정책 3종");
            sb.AppendLine("> 판정 기준: `docs/runtime/FUN_CRITERIA.md`");
            sb.AppendLine("> **이 파일은 자동 생성된다.** 손으로 고치지 않는다 — 다음 스윕이 덮어쓴다.");
            sb.AppendLine();

            float[] meanNet = Calibrate();
            sb.AppendLine("## 0. 보정 — 층별 스핀 하나의 평균 순전력");
            sb.AppendLine();
            sb.AppendLine("기대값 정책이 쓰는 값이다. 상수가 아니라 실측이다.");
            sb.AppendLine();
            sb.AppendLine("| 층 | 요구 전력 | 평균 순전력/스핀 | 요구 대비 | 이론 소요 스핀 |");
            sb.AppendLine("|---|---|---|---|---|");
            for (int f = 1; f <= 10; f++)
            {
                FloorPlan p = PrototypeCurriculum.For(f);
                float ratio = p.RequiredPower > 0f ? meanNet[f] / p.RequiredPower : 0f;
                float need = meanNet[f] > 0.01f ? p.RequiredPower / meanNet[f] : 99f;
                string flag = need > p.Spins ? "  ⚠ 스핀 부족" : (need < 1.5f ? "  ⚠ 너무 쉽다" : "");
                sb.AppendLine($"| {f} | {p.RequiredPower:F0} | {meanNet[f]:F1} | {ratio * 100f:F1}% | {need:F2} / {p.Spins}{flag} |");
            }
            sb.AppendLine();

            var results = new Dictionary<Policy, SweepStats>();
            foreach (Policy policy in new[] { Policy.Safe, Policy.Greedy, Policy.ExpectedValue })
                results[policy] = Sweep(policy, meanNet);

            WriteDifficultyCurve(sb, results);
            WriteSpinPressure(sb, results);
            WriteOverchargeChoice(sb, results);
            WriteCascade(sb, results);
            WriteBands(sb, results);
            WriteContractDominance(sb, meanNet);
            WriteVerdict(sb, results);

            return sb.ToString();
        }

        /// <summary>
        /// 층마다 「스핀 하나가 평균 몇 전력을 주는가」를 잰다.
        /// 전 스핀을 소진하는 정책으로 돌려야 잔류 저항이 쌓인 후반 스핀까지 표본에 들어온다.
        /// </summary>
        private static float[] Calibrate()
        {
            var sum = new double[11];
            var count = new int[11];

            for (int i = 0; i < CalibrationSeeds; i++)
            {
                var run = NewRun(FirstSeed + 900000 + i);
                int guard = 0;
                while (!run.IsComplete && !run.IsFailed && guard++ < GuardLimit)
                {
                    FloorSession f = run.Current;
                    if (f == null) break;
                    int floor = Mathf.Clamp(f.Plan.Floor, 1, 10);

                    if (f.Phase == FloorPhase.Boarding && !RunBoarding(run, f)) break;
                    if (f.Phase == FloorPhase.ContractSelection && !SelectEngagedContract(run, f)) break;

                    while (f.SpinsRemaining > 0)
                    {
                        if (f.Phase == FloorPhase.Decision)
                        {
                            // 보정에서는 무조건 더 돌린다 — 후반 스핀의 표본이 목적이다.
                            if (!run.PushYourLuck()) break;
                        }
                        if (f.Phase != FloorPhase.Spinning) break;
                        SpinResolution r = run.Spin();
                        sum[floor] += r.NetPower;
                        count[floor]++;
                    }

                    if (!Settle(run, f)) break;
                }
            }

            var mean = new float[11];
            for (int f = 1; f <= 10; f++)
                mean[f] = count[f] > 0 ? (float)(sum[f] / count[f]) : 0f;
            return mean;
        }

        // ────────────────────────────────────────────────────────────── 스윕

        private sealed class SweepStats
        {
            public readonly int[] Visits = new int[11];
            public readonly int[] Cleared = new int[11];
            public readonly int[] SpinsUsed = new int[11];
            public readonly int[] SpinCap = new int[11];

            /// <summary>확정 가능해진 시점에 스핀이 남아 있던 층 수 = 선택이 **발생**한 횟수.</summary>
            public readonly int[] ChoiceOffered = new int[11];
            /// <summary>그중 실제로 과수확을 택한 횟수.</summary>
            public readonly int[] ChoiceTookExtra = new int[11];

            public int Runs, Completed, Failed;
            public double TotalMoney;

            public int Spins, SpinsWithPurify, SpinsChain3Plus, SpinsChain5Plus, CascadeCapHits;
            public int MaxChain;

            public readonly Dictionary<PowerBand, int> Bands = new Dictionary<PowerBand, int>();

            /// <summary>정산 가치 ÷ 남은 스핀의 기대 순이득. Notion §7.3 목표 0.30~0.40.</summary>
            public double SettlementRatioSum;
            public int SettlementRatioCount;
        }

        private static SweepStats Sweep(Policy policy, float[] meanNet)
        {
            var s = new SweepStats();

            for (int i = 0; i < SeedCount; i++)
            {
                s.Runs++;
                var run = NewRun(FirstSeed + i);
                int guard = 0;
                int lastFloor = -1;

                while (!run.IsComplete && !run.IsFailed && guard++ < GuardLimit)
                {
                    FloorSession f = run.Current;
                    if (f == null) break;
                    int floor = Mathf.Clamp(f.Plan.Floor, 1, 10);
                    if (floor != lastFloor) { s.Visits[floor]++; lastFloor = floor; }

                    if (f.Phase == FloorPhase.Boarding && !RunBoarding(run, f)) break;
                    if (f.Phase == FloorPhase.ContractSelection && !SelectEngagedContract(run, f)) break;

                    bool choiceCounted = false;
                    bool tookExtra = false;

                    while (true)
                    {
                        // 돌 수 있으면 돈다.
                        while (f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0)
                        {
                            SpinResolution r = run.Spin();
                            RecordSpin(s, in r);
                        }

                        if (f.Phase != FloorPhase.Decision) break;

                        // 선택이 존재하는가 — 확정 가능한데 스핀이 남았다.
                        bool choiceExists = f.CanBank && f.CanTakeExtraSpin;
                        if (choiceExists && !choiceCounted)
                        {
                            s.ChoiceOffered[floor]++;
                            choiceCounted = true;
                            RecordSettlementRatio(s, f, meanNet[floor]);
                        }

                        if (!choiceExists) break;
                        if (!WantsExtraSpin(policy, f, meanNet[floor])) break;
                        if (!run.PushYourLuck()) break;
                        tookExtra = true;
                    }

                    if (tookExtra && choiceCounted) s.ChoiceTookExtra[floor]++;

                    s.SpinsUsed[floor] += f.SpinsUsed;
                    s.SpinCap[floor] += f.Plan.Spins;

                    PowerBand band = PowerThresholds.Default.BandFor(f.Power, f.RequiredPower);
                    s.Bands.TryGetValue(band, out int bn);
                    s.Bands[band] = bn + 1;
                    if (band.Ascends()) s.Cleared[floor]++;

                    if (!Settle(run, f)) break;
                }

                if (run.IsComplete && !run.IsFailed) s.Completed++;
                if (run.IsFailed) s.Failed++;
                s.TotalMoney += run.Money;
            }

            return s;
        }

        private static void RecordSpin(SweepStats s, in SpinResolution r)
        {
            s.Spins++;
            int chain = r.ChainDepth;
            if (chain > s.MaxChain) s.MaxChain = chain;
            if (chain >= 3) s.SpinsChain3Plus++;
            if (chain >= 5) s.SpinsChain5Plus++;
            if (r.CascadeCapReached) s.CascadeCapHits++;
            if (r.PurifyPower > 0.01f) s.SpinsWithPurify++;
        }

        /// <summary>
        /// 정산 가치와 「남은 스핀을 다 쓸 때의 기대 순이득」을 견준다.
        /// 분모에서 앤티를 빼는 것이 요점이다 — 추가 스핀은 공짜가 아니다.
        /// </summary>
        private static void RecordSettlementRatio(SweepStats s, FloorSession f, float meanNet)
        {
            int remaining = f.SpinsRemaining;
            if (remaining <= 0 || meanNet <= 0.01f) return;

            float settlement = f.PendingSettlementMoney;
            // 앤티는 당길 때마다 오른다. 근사로 첫 앤티 × 남은 횟수를 쓴다 —
            // 실제는 이보다 비싸므로 이 비율은 **낙관적 하한**이다. 그 방향을 명시해 둔다.
            float anteApprox = f.PendingAnte * remaining;
            float extraSpinEv = meanNet * remaining - anteApprox;
            if (extraSpinEv <= 0.01f) return;

            s.SettlementRatioSum += settlement / extraSpinEv;
            s.SettlementRatioCount++;
        }

        private static bool WantsExtraSpin(Policy policy, FloorSession f, float meanNet)
        {
            switch (policy)
            {
                case Policy.Safe:
                    return false;
                case Policy.Greedy:
                    return true;
                default:
                    // 한 번 더 당기는 것의 순이득 = 기대 순전력 − 앤티.
                    // 포기하는 것 = 지금 확정하면 받을 정산.
                    // 정산은 이번 한 번만 포기하는 것이 아니라 **전부** 포기하는 것이므로
                    // 첫 결정에서 정산 전액과 견준다.
                    float gain = meanNet - f.PendingAnte;
                    float lose = f.PendingSettlementMoney;
                    return gain > lose;
            }
        }

        /// <summary>
        /// **계약을 실제로 거는 플레이어**를 기준으로 잰다. 0번은 대개 「계약 없음」이라
        /// 그것만 고르면 설계된 게임이 아니라 그 게임을 안 하는 경우를 재게 된다.
        ///
        /// 기존 프로브(`CurriculumCoverageProbe`)와 이 스윕의 첫 판본이 둘 다 0번을
        /// 고정으로 골랐고, 그래서 층별 난이도가 **가장 소극적인 플레이 기준**으로
        /// 측정되고 있었다. §6 이 그 편차를 재 준다 — 7층에서 계약 2번은 97.9%,
        /// 계약 없음은 86.7% 였다. 11%p 는 대역 하나만큼의 차이다.
        ///
        /// 1번을 고르는 이유: 층이 **이번에 가르치려는** 계약이 거기 있다.
        /// 4층 흡수체·6층 증식체·7~9층 흡수체·10층 증식체(10층은 「계약 없음」이 없다).
        /// </summary>
        private static bool SelectEngagedContract(RunSession run, FloorSession f)
        {
            int count = f.Plan.ContractChoices?.Length ?? 0;
            if (count > 1 && run.SelectContract(1)) return true;
            return run.SelectContract(0);
        }

        private static bool Settle(RunSession run, FloorSession f)
        {
            if (f.CanBank) return run.Bank() != null;
            if (f.SpinsRemaining == 0) return run.ForceResolve() != null;
            return false;
        }

        private static RunSession NewRun(int seed)
        {
            // 앤티 두 값만 코드 기본값으로 두고 나머지 일곱은 프로파일 기본값 —
            // `RunSession` 의 5인자 경로가 하던 것과 같다. 그 경로를 못 쓰는 이유는
            // 정산 스냅샷을 넣을 자리가 없기 때문이다.
            var overharvest = new OverharvestSnapshot(
                FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation,
                OverharvestProfile.DefaultUnlockThreshold,
                OverharvestProfile.DefaultApproachMachineDuckScale,
                OverharvestProfile.DefaultMinSilenceSeconds,
                OverharvestProfile.DefaultMaxSilenceSeconds,
                OverharvestProfile.DefaultPassengerGazeDelaySeconds,
                OverharvestProfile.DefaultResumeFadeSeconds,
                OverharvestProfile.DefaultMaxExtraSpins);

            return new RunSession(seed, 0f, 0f, overharvest,
                WeightProfile.DefaultSnapshot, SpinBalanceProfile.DefaultSnapshot,
                _settlementOverride ?? RemainingSpinSettlementProfile.DefaultSnapshot,
                new TenFloorSource(CurriculumSnapshot()));
        }

        // ────────────────────────────────────────────────────────────── 보고

        private static readonly (string Name, int Lo, int Hi, double ClearLo, double ClearHi)[] Segments =
        {
            // 대역은 「모든 층 방문률 ≥ 50%」에서 역산한 값이다 (`FUN_CRITERIA` §1 정정).
            ("초반 1–3", 1, 3, 97.0, 100.0),
            ("중반 4–7", 4, 7, 90.0, 96.0),
            ("후반 8–10", 8, 10, 82.0, 92.0),
        };

        private static void WriteDifficultyCurve(StringBuilder sb, Dictionary<Policy, SweepStats> r)
        {
            sb.AppendLine("## 1. 난이도 곡선 — 구간별 클리어율");
            sb.AppendLine();
            sb.AppendLine("클리어율 = 그 층을 방문한 런 중 상승에 성공한 비율. 방문하지 않은 층은 분모에서 빠진다.");
            sb.AppendLine();
            sb.AppendLine("| 구간 | 목표 | 안전 | 탐욕 | 기대값 | 판정(기대값 기준) |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var seg in Segments)
            {
                double ev = SegmentClear(r[Policy.ExpectedValue], seg.Lo, seg.Hi);
                string verdict = ev >= seg.ClearLo && ev <= seg.ClearHi ? "✅ 대역 안"
                    : (ev > seg.ClearHi ? "❌ 너무 쉽다" : "❌ 너무 어렵다");
                sb.AppendLine($"| {seg.Name} | {seg.ClearLo:F0}–{seg.ClearHi:F0}% | " +
                              $"{SegmentClear(r[Policy.Safe], seg.Lo, seg.Hi):F1}% | " +
                              $"{SegmentClear(r[Policy.Greedy], seg.Lo, seg.Hi):F1}% | " +
                              $"**{ev:F1}%** | {verdict} |");
            }
            sb.AppendLine();
            sb.AppendLine("### 층별 상세 (기대값 정책)");
            sb.AppendLine();
            sb.AppendLine("| 층 | 방문률 | 클리어율 | 가르치는 것 |");
            sb.AppendLine("|---|---|---|---|");
            SweepStats s = r[Policy.ExpectedValue];
            for (int f = 1; f <= 10; f++)
            {
                double visit = 100.0 * s.Visits[f] / s.Runs;
                double clear = s.Visits[f] > 0 ? 100.0 * s.Cleared[f] / s.Visits[f] : 0.0;
                string mark = visit < 50.0 ? " ⚠ 절반도 못 본다" : "";
                sb.AppendLine($"| {f} | {visit:F1}%{mark} | {clear:F1}% | {PrototypeCurriculum.For(f).TeachesRule} |");
            }
            sb.AppendLine();
            sb.AppendLine($"완주율 — 안전 {100.0 * r[Policy.Safe].Completed / r[Policy.Safe].Runs:F1}% · " +
                          $"탐욕 {100.0 * r[Policy.Greedy].Completed / r[Policy.Greedy].Runs:F1}% · " +
                          $"기대값 {100.0 * s.Completed / s.Runs:F1}%");
            sb.AppendLine();

            // **1차 기준.** 설계됐지만 플레이되지 않는 층이 있으면 나머지 지표는 의미가 없다.
            int starved = 0; double worst = 100.0; int worstFloor = 0;
            for (int f = 1; f <= 10; f++)
            {
                double visit = 100.0 * s.Visits[f] / s.Runs;
                if (visit < 50.0) starved++;
                if (visit < worst) { worst = visit; worstFloor = f; }
            }
            sb.AppendLine(starved == 0
                ? $"**커버리지 ✅ — 모든 층 방문률 ≥ 50%** (최저 {worstFloor}층 {worst:F1}%)"
                : $"**커버리지 ❌ — {starved}개 층이 방문률 50% 미만** (최저 {worstFloor}층 {worst:F1}%). " +
                  "설계됐지만 플레이되지 않는 층이다. 다른 지표보다 먼저 고친다.");
            sb.AppendLine();
        }

        private static double SegmentClear(SweepStats s, int lo, int hi)
        {
            int v = 0, c = 0;
            for (int f = lo; f <= hi; f++) { v += s.Visits[f]; c += s.Cleared[f]; }
            return v > 0 ? 100.0 * c / v : 0.0;
        }

        private static void WriteSpinPressure(StringBuilder sb, Dictionary<Policy, SweepStats> r)
        {
            sb.AppendLine("## 2. 스핀 압박 — **참고 지표** (판정에 쓰지 않는다)");
            sb.AppendLine();
            sb.AppendLine("**안전 정책**으로 잰다. 탐욕은 정의상 스핀을 다 쓰므로 층의 난이도를 재지 못한다.");
            sb.AppendLine();
            sb.AppendLine("> **2026-08-04 강등.** 이 절의 목표 대역(30–55 / 55–75 / 75–95%)은 내가 정한 것이고,");
            sb.AppendLine("> 상한이 5 스핀인 이상 **커버리지 요구와 동시에 만족될 수 없다.** 후반 사용률 80%");
            sb.AppendLine("> 는 평균 4 스핀을 뜻하고, 그러면 꼬리가 5 를 넘는 런이 많아져 뒤 층을 아무도 못 본다.");
            sb.AppendLine("> 게다가 요구 전력이 `목표 스핀 × 실측 산출` 로 **역산**되므로 사용률은 독립 다이얼이");
            sb.AppendLine("> 아니라 **결과값**이다. 결과값에 목표를 걸면 같은 것을 두 번 조이게 된다.");
            sb.AppendLine(">");
            sb.AppendLine("> 압박이 실제로 있는지는 §3 「선택이 발생하는 층 비율」이 대신 답한다 —");
            sb.AppendLine("> 그건 PRD §17.3 에 근거가 있는 지표다. **미룬 요구**: 스핀 상한을 층마다");
            sb.AppendLine("> 다르게 두는 안은 사용자 결정 사항이다. ⚠ **정정(2026-08-04): 「PRD 개정 필요」는");
            sb.AppendLine("> 상한을 6 이상으로 *올리는* 안에만 해당한다.** PRD §4.1 은 「한 층 **최대** 5회」이고");
            sb.AppendLine("> `FloorPlan.Spins` 는 이미 층별 필드다 — 후반 층을 4 로 *낮추는* 것은 개정 없이 된다.");
            sb.AppendLine();
            sb.AppendLine("| 구간 | 참고 대역 | 실측 |");
            sb.AppendLine("|---|---|---|");
            var targets = new[] { (30.0, 55.0), (55.0, 75.0), (75.0, 95.0) };
            SweepStats s = r[Policy.Safe];
            for (int i = 0; i < Segments.Length; i++)
            {
                var seg = Segments[i];
                int used = 0, cap = 0;
                for (int f = seg.Lo; f <= seg.Hi; f++) { used += s.SpinsUsed[f]; cap += s.SpinCap[f]; }
                double pct = cap > 0 ? 100.0 * used / cap : 0.0;
                sb.AppendLine($"| {seg.Name} | {targets[i].Item1:F0}–{targets[i].Item2:F0}% | {pct:F1}% |");
            }
            sb.AppendLine();
            sb.AppendLine("| 층 | 평균 사용 스핀 / 상한 | 판정 |");
            sb.AppendLine("|---|---|---|");
            for (int f = 1; f <= 10; f++)
            {
                if (s.Visits[f] == 0) { sb.AppendLine($"| {f} | (방문 없음) | ⚠ |"); continue; }
                double avg = (double)s.SpinsUsed[f] / s.Visits[f];
                int cap = PrototypeCurriculum.For(f).Spins;
                string v = avg <= 1.5 ? "❌ 층이 없는 것과 같다" : (avg >= cap - 0.05 ? "❌ 선택 여지 없음" : "✅");
                sb.AppendLine($"| {f} | {avg:F2} / {cap} | {v} |");
            }
            sb.AppendLine();
        }

        private static void WriteOverchargeChoice(StringBuilder sb, Dictionary<Policy, SweepStats> r)
        {
            sb.AppendLine("## 3. 과수확이 실제 선택인가 — **1순위 지표**");
            sb.AppendLine();

            SweepStats ev = r[Policy.ExpectedValue];
            SweepStats safe = r[Policy.Safe];
            (double offerRate, double ratio, double takeRate) = OverchargeMetrics(safe, ev);

            sb.AppendLine("| 지표 | 목표 | 실측 | 판정 |");
            sb.AppendLine("|---|---|---|---|");
            // 상한 85% 는 2026-08-04 에 70% 에서 넓혔다. PRD 는 **하한만** 말한다
            // (§17.3 「10분 내 과수확 고민 1회 이상」). 70% 라는 상한은 「선택이 흔해지면
            // 무뎌진다」는 내 추측이었고 근거가 없다. 실제로 흔해서 무뎌지는지는
            // 사람이 플레이해야 알 수 있으므로, 시뮬이 판정할 수 있는 범위로 되돌린다.
            sb.AppendLine($"| 선택이 **발생**하는 층 비율 | 40–85% | {offerRate:F1}% | " +
                          $"{(offerRate >= 40 && offerRate <= 85 ? "✅" : "❌")} |");
            sb.AppendLine($"| 안전 확정 EV ÷ 추가 스핀 EV | (해석 미확정 — 판정 안 함) | **{ratio:F3}** | " +
                          "— |");
            sb.AppendLine($"| 기대값 정책의 과수확 선택률 | 25–75% | {takeRate:F1}% | " +
                          $"{(takeRate >= 25 && takeRate <= 75 ? "✅" : "❌")} |");
            sb.AppendLine();
            sb.AppendLine("> **EV 비율은 판정하지 않는다 (2026-08-04).** 옛 목표 `0.30–0.40` 을 폐기했다.");
            sb.AppendLine("> 이 값과 바로 아래 과수확 선택률은 **같은 양의 두 표현**이라 함께 만족될 수 없다.");
            sb.AppendLine(">");
            sb.AppendLine("> 기대값 정책의 판단 규칙은 `meanNet − ante > settlement`(1회분 이득 vs 정산 전액)이고");
            sb.AppendLine("> 측정 규칙은 `settlement / ((meanNet − ante) × remaining)` 이다. 둘을 합치면");
            sb.AppendLine("> **선택률 50% ⟺ ratio = 1 / r̄** (r̄ = 결정 시점 평균 잔여 스핀). 즉 한쪽을 정하면");
            sb.AppendLine("> 다른 쪽이 따라 나온다. `0.30–0.40` 은 「남은 스핀 전부를 쓰는 쪽이 정산보다");
            sb.AppendLine("> 2.5~3.3배 유리하다」는 뜻이고, 그 상태에서 합리적 플레이어는 **항상 당긴다** —");
            sb.AppendLine("> **정의상 지배 전략을 요구하는 대역**이다. 선택률 25–75% 를 ratio 로 환산하면");
            sb.AppendLine("> 약 `0.45–1.05` 이고 옛 목표와 **서로소**다.");
            sb.AppendLine(">");
            sb.AppendLine("> 애초의 출처가 이미 경고하고 있었다 — `GAMEPLAY_SPEC.md` §4.5:");
            sb.AppendLine("> 「**추가 스핀 기대값**」이 1회분인지 남은 전부인지 Notion 이 말하지 않아");
            sb.AppendLine("> 두 해석이 3~4배 차이 난다. 해석 확정은 사용자 결정 항목이다(`PENDING_DECISIONS`).");
            sb.AppendLine(">");
            sb.AppendLine("> 참고로 남긴 값의 편향: 분모가 **앤티를 뺀** 기대 순이득인데 앤티는 당길수록");
            sb.AppendLine("> ×1.35 로 오르고(첫 앤티로 근사), `Power` 기준이라 스핀마다 커지며, 잉여 전력이");
            sb.AppendLine("> 전부 돈이 되지도 않는다. 편향 방향이 전부 같다 — **표시값은 하한이다.**");
            sb.AppendLine();
            sb.AppendLine("### 층별 선택 발생률 (기대값 정책)");
            sb.AppendLine();
            sb.AppendLine("| 층 | 선택 발생 | 과수확 선택 |");
            sb.AppendLine("|---|---|---|");
            for (int f = 1; f <= 10; f++)
            {
                if (ev.Visits[f] == 0) continue;
                double o = 100.0 * ev.ChoiceOffered[f] / ev.Visits[f];
                double t = ev.ChoiceOffered[f] > 0 ? 100.0 * ev.ChoiceTookExtra[f] / ev.ChoiceOffered[f] : 0.0;
                string push = PrototypeCurriculum.For(f).EmphasizePushYourLuck ? "  ← 푸시유어럭 층" : "";
                sb.AppendLine($"| {f} | {o:F1}% | {t:F1}%{push} |");
            }
            sb.AppendLine();
        }

        private static void WriteCascade(StringBuilder sb, Dictionary<Policy, SweepStats> r)
        {
            sb.AppendLine("## 4. 연쇄 — 도파민 간격");
            sb.AppendLine();
            sb.AppendLine("| 지표 | 목표 | 안전 | 기대값 | 판정(기대값) |");
            sb.AppendLine("|---|---|---|---|---|");

            SweepStats s = r[Policy.ExpectedValue], sf = r[Policy.Safe];
            double p3 = Pct(s.SpinsChain3Plus, s.Spins), p3s = Pct(sf.SpinsChain3Plus, sf.Spins);
            double pp = Pct(s.SpinsWithPurify, s.Spins), pps = Pct(sf.SpinsWithPurify, sf.Spins);
            double cap = Pct(s.CascadeCapHits, s.Spins);

            sb.AppendLine($"| 3연쇄 이상 스핀 비율 | 5–15% | {p3s:F1}% | **{p3:F1}%** | " +
                          $"{(p3 >= 5 && p3 <= 15 ? "✅" : "❌")} |");
            sb.AppendLine($"| 정화 발생 스핀 비율 | 35–65% | {pps:F1}% | **{pp:F1}%** | " +
                          $"{(pp >= 35 && pp <= 65 ? "✅" : "❌")} |");
            sb.AppendLine($"| 연쇄 캡(20) 도달률 | < 1% | — | {cap:F2}% | {(cap < 1.0 ? "✅" : "❌")} |");
            sb.AppendLine($"| 5연쇄 이상 스핀 비율 | (참고) | — | {Pct(s.SpinsChain5Plus, s.Spins):F2}% | |");
            sb.AppendLine($"| 관측 최대 연쇄 | (참고) | {sf.MaxChain} | {s.MaxChain} | |");
            sb.AppendLine();
        }

        private static double Pct(int a, int b) => b > 0 ? 100.0 * a / b : 0.0;

        /// <summary>
        /// 과수확 3지표. 정산 EV 비는 **안전 정책**에서 잰다 — 그 정책만이 정산 권리를
        /// 끝까지 들고 있어서, 「포기하지 않았다면 얼마였는가」를 관측할 수 있다.
        /// 탐욕·기대값 정책은 첫 선택에서 권리를 버리므로 그 뒤의 표본이 0 이 된다.
        /// </summary>
        private static (double offerRate, double settlementRatio, double takeRate)
            OverchargeMetrics(SweepStats safe, SweepStats ev)
        {
            int offered = 0, visits = 0, took = 0;
            for (int f = 1; f <= 10; f++)
            {
                offered += ev.ChoiceOffered[f];
                visits += ev.Visits[f];
                took += ev.ChoiceTookExtra[f];
            }
            return (visits > 0 ? 100.0 * offered / visits : 0.0,
                    safe.SettlementRatioCount > 0 ? safe.SettlementRatioSum / safe.SettlementRatioCount : 0.0,
                    offered > 0 ? 100.0 * took / offered : 0.0);
        }

        private static void WriteBands(StringBuilder sb, Dictionary<Policy, SweepStats> r)
        {
            sb.AppendLine("## 5. 부분 실패 대역이 살아 있는가");
            sb.AppendLine();
            sb.AppendLine("| 대역 | 안전 | 탐욕 | 기대값 | 살아 있는가 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (PowerBand band in Enum.GetValues(typeof(PowerBand)))
            {
                int a = Count(r[Policy.Safe], band);
                int b = Count(r[Policy.Greedy], band);
                int c = Count(r[Policy.ExpectedValue], band);
                string alive = (a + b + c) > 0 ? "✅" : "❌ 한 번도 안 나온다";
                sb.AppendLine($"| {band.DisplayName()} | {a} | {b} | {c} | {alive} |");
            }
            sb.AppendLine();
        }

        private static int Count(SweepStats s, PowerBand band)
            => s.Bands.TryGetValue(band, out int n) ? n : 0;

        /// <summary>
        /// 계약이 선택인가. 층마다 선택지를 하나로 고정해 돌리고 클리어율을 견준다.
        /// 한 계약이 전 층에서 최선이면 「선택」이 아니라 「정답」이다.
        /// </summary>
        private static void WriteContractDominance(StringBuilder sb, float[] meanNet)
        {
            sb.AppendLine("## 6. 지배 전략 — 계약이 선택인가");
            sb.AppendLine();
            sb.AppendLine("계약 선택지를 인덱스로 고정해 각각 돌린다. 층마다 선택지 수가 달라");
            sb.AppendLine("존재하지 않는 인덱스는 마지막 선택지로 접힌다 (`SelectContract` 가 거부하므로 0번으로 폴백).");
            sb.AppendLine();
            sb.AppendLine("| 층 | 선택지 | 0번 클리어율 | 1번 | 2번 | 최대 격차 |");
            sb.AppendLine("|---|---|---|---|---|---|");

            var byIndex = new Dictionary<int, SweepStats>();
            for (int idx = 0; idx < 3; idx++) byIndex[idx] = SweepFixedContract(idx, meanNet);

            double worstGap = 999;
            for (int f = 1; f <= 10; f++)
            {
                FloorPlan p = PrototypeCurriculum.For(f);
                int choices = p.ContractChoices?.Length ?? 0;
                if (choices <= 1) { sb.AppendLine($"| {f} | {choices} | — | — | — | (선택 없음) |"); continue; }

                var vals = new List<double>();
                var cells = new string[3];
                for (int idx = 0; idx < 3; idx++)
                {
                    if (idx >= choices) { cells[idx] = "—"; continue; }
                    SweepStats s = byIndex[idx];
                    double c = s.Visits[f] > 0 ? 100.0 * s.Cleared[f] / s.Visits[f] : 0.0;
                    vals.Add(c);
                    cells[idx] = $"{c:F1}%";
                }
                double gap = vals.Count > 1 ? Max(vals) - Min(vals) : 0.0;
                if (choices > 1 && gap < worstGap) worstGap = gap;
                sb.AppendLine($"| {f} | {choices} | {cells[0]} | {cells[1]} | {cells[2]} | {gap:F1}%p |");
            }
            sb.AppendLine();
            sb.AppendLine("**격차가 0에 가까우면 계약이 장식이다.** 반대로 한 인덱스가 전 층에서");
            sb.AppendLine("일방적으로 높으면 그것이 정답이고 나머지는 함정이다 — 둘 다 실패다.");
            sb.AppendLine();
        }

        private static double Max(List<double> v) { double m = double.MinValue; foreach (var x in v) if (x > m) m = x; return m; }
        private static double Min(List<double> v) { double m = double.MaxValue; foreach (var x in v) if (x < m) m = x; return m; }

        private static SweepStats SweepFixedContract(int contractIndex, float[] meanNet)
        {
            var s = new SweepStats();
            int seeds = SeedCount / 2;   // 계약 비교는 표본을 반으로 — 3배 돌기 때문이다
            for (int i = 0; i < seeds; i++)
            {
                s.Runs++;
                var run = NewRun(FirstSeed + 500000 + i);
                int guard = 0, lastFloor = -1;
                while (!run.IsComplete && !run.IsFailed && guard++ < GuardLimit)
                {
                    FloorSession f = run.Current;
                    if (f == null) break;
                    int floor = Mathf.Clamp(f.Plan.Floor, 1, 10);
                    if (floor != lastFloor) { s.Visits[floor]++; lastFloor = floor; }

                    if (f.Phase == FloorPhase.Boarding && !RunBoarding(run, f)) break;
                    if (f.Phase == FloorPhase.ContractSelection)
                    {
                        // 없는 인덱스는 거부되므로 0번으로 접는다.
                        if (!run.SelectContract(contractIndex) && !run.SelectContract(0)) break;
                    }

                    while (true)
                    {
                        while (f.Phase == FloorPhase.Spinning && f.SpinsRemaining > 0) run.Spin();
                        if (f.Phase != FloorPhase.Decision) break;
                        if (!(f.CanBank && f.CanTakeExtraSpin)) break;
                        if (!WantsExtraSpin(Policy.ExpectedValue, f, meanNet[floor])) break;
                        if (!run.PushYourLuck()) break;
                    }

                    PowerBand band = PowerThresholds.Default.BandFor(f.Power, f.RequiredPower);
                    if (band.Ascends()) s.Cleared[floor]++;
                    if (!Settle(run, f)) break;
                }
                if (run.IsComplete && !run.IsFailed) s.Completed++;
            }
            return s;
        }

        private static void WriteVerdict(StringBuilder sb, Dictionary<Policy, SweepStats> r)
        {
            sb.AppendLine("## 7. 이 스윕이 판정하지 못하는 것");
            sb.AppendLine();
            sb.AppendLine("- 스핀을 기다리는 **체감 긴장**");
            sb.AppendLine("- 결과 공개 순서의 **판독 쾌감**");
            sb.AppendLine("- 공간 이동과 상호작용의 **손맛**");
            sb.AppendLine("- 공포 강도와 승객 반응의 정서적 효과");
            sb.AppendLine();
            sb.AppendLine("지표가 전부 대역에 들어와도 위 넷은 미검증이다.");
            sb.AppendLine("**「재밌다」가 아니라 「지표가 대역에 들어왔다」로만 보고한다.**");
        }
    }
}
