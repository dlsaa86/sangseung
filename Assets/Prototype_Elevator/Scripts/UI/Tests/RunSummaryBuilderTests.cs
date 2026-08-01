using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.UI.Tests
{
    /// <summary>
    /// 런 요약 9종(`MASTER_PRD.md` §10.2 / `UP-REC-02`)이 **실제 런에서** 전부 채워지는지 본다.
    ///
    /// `ProfileTests` 가 이미 「서식이 9줄을 낸다」를 지키고 있다. 여기서 묻는 것은 다른 것이다 —
    /// **값을 채우는 쪽이 9칸을 다 채우는가.** 백로그 `UP-REC-02` 의 남은 문제가 정확히
    /// 「항목 대조표가 없다. 9종이 전부 나오는지 검증되지 않았다」였고, 그것은 서식이 아니라
    /// 산출기의 문제다. 그래서 이 스위트는 가짜 값을 넣지 않고 `RunSession` 을 끝까지 몰아
    /// `FloorRecord` 를 실제로 쌓은 뒤 요약을 짓는다.
    ///
    /// **파일 위치에 대해**: 이 테스트는 `Scripts/Run` 을 검사하는데 `Scripts/UI/Tests` 에 있다.
    /// Pass 1 병렬 작업의 소유 경계 때문이다 — 이 작업자는 `Scripts/UI/` 와
    /// `Scripts/Run/RunSummaryBuilder.cs` 만 쓸 수 있었다. 경계가 풀리면
    /// `Scripts/Run/Tests/` 로 옮기는 것이 맞다.
    ///
    /// NUnit 을 쓰지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class RunSummaryBuilderTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Case("끝난 런에서 9줄이 전부 채워진다", TestNineFieldsFilled, ref passed, ref failed, report);
            Case("빈 줄도 「기록 없음」도 남지 않는다", TestNoPlaceholderInRealRun, ref passed, ref failed, report);
            Case("최고 캐스케이드가 기록의 최댓값과 같다", TestPeakCascadeMatchesRecords, ref passed, ref failed, report);
            Case("최고 층이 기록의 최댓값 아래로 내려가지 않는다", TestHighestFloorNotUnderRecords, ref passed, ref failed, report);
            Case("과수확한 런은 비율과 마지막 선택을 남긴다", TestOverharvestReported, ref passed, ref failed, report);
            Case("과수확 없는 런은 0% 와 「과수확 없음」이다", TestNoOverharvestReported, ref passed, ref failed, report);
            Case("실패 원인이 한국어로 나온다", TestFailureLocalized, ref passed, ref failed, report);
            Case("완주한 런의 종료 원인은 완주다", TestCompleteRunEndCause, ref passed, ref failed, report);
            Case("런 시드가 그대로 실린다", TestSeedCarried, ref passed, ref failed, report);
            Case("기록이 하나도 없어도 9줄이다", TestNoRecordsStillNineLines, ref passed, ref failed, report);
            Case("런이 null 이어도 9줄이다", TestNullRunStillNineLines, ref passed, ref failed, report);
            Case("문장을 코드가 아니라 에셋이 정한다", TestTemplateIsActuallyRead, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Run Summary Builder Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        /// <summary>이 클래스에 `Run` 이라는 이름을 쓰지 않는다 — `Ascend.Prototype.Run` 과 겹친다.</summary>
        private static void Case(string name, Func<string> test,
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
                report.AppendLine($"  FAIL  {name} — 예외: {exception.Message}");
            }
        }

        // ── 런 구동 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 10층 런을 상호작용만으로 끝까지 몰고, 층이 확정될 때마다 기록을 뜬다.
        ///
        /// `AccidentRecorder` 를 쓰지 않는 이유: 그쪽은 MonoBehaviour 라 씬이 필요하다.
        /// 대신 그것이 부르는 것과 **같은 함수**(`FloorRecord.Capture`)를 같은 시점에 부른다 —
        /// 다른 경로로 만든 기록으로 검사하면 화면이 보는 것을 검사한 것이 아니다.
        /// </summary>
        private static List<FloorRecord> Play(int seed, bool overharvest, out RunSession run)
        {
            run = new RunSession(seed, 0f, 0f,
                FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation,
                new TenFloorSource());

            var records = new List<FloorRecord>();
            int guard = 0;

            while (run.Current != null && guard++ < 60)
            {
                FloorSession floor = run.Current;

                if (floor.Phase == FloorPhase.Boarding)
                {
                    if (floor.BuildOffers.Count > 0) run.TakeBuildOffer(0);
                    if (!run.FinishBoarding()) break;
                }

                if (floor.Phase == FloorPhase.ContractSelection)
                {
                    int count = floor.Plan.ContractChoices != null ? floor.Plan.ContractChoices.Length : 0;
                    // 1번이 있으면 1번을 고른다 — 0번은 커리큘럼상 대개 「계약 없음」이라
                    // 그것만 고르면 「핵심 계약」이 영원히 비어 요약을 검사할 수 없다.
                    if (!run.SelectContract(count > 1 ? 1 : 0)) break;
                }

                // 요구 전력에 닿거나 스핀이 떨어질 때까지 당긴다. `Spin()` 이 둘 중 하나가
                // 성립하는 순간 스스로 Decision 으로 넘어가므로 조건은 이 둘로 충분하다.
                int spins = 0;
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && spins++ < 40)
                    run.Spin();

                // 과수확은 한 층에 한 번만. 판돈을 걸고 한 번 더 당긴다.
                if (overharvest && floor.CanTakeExtraSpin && run.PushYourLuck())
                {
                    // 판돈을 내도 요구 전력을 지키면 Phase 는 Spinning 인 채 CanBank 가 참이다.
                    // 그 상태에서 스핀을 걸지 않으면 층이 Decision 으로 못 넘어가 확정이 막힌다.
                    while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && spins++ < 40)
                        run.Spin();
                }

                FloorResult result = floor.CanBank ? run.Bank()
                                   : floor.SpinsRemaining == 0 ? run.ForceResolve() : null;
                if (result == null) break;

                // 위험도는 씬(`RiskStateView`)이 소유하므로 헤드리스에서는 결과에서 유도한다.
                // `AccidentRecorder` 도 실패한 층에 대해 같은 규칙을 쓴다.
                RiskLevel risk = result.Succeeded ? RiskLevel.Strain : RiskLevel.Collapse;
                string reason = result.Succeeded ? "잔류 저항" : "층 실패";

                records.Add(FloorRecord.Capture(run.Seed, floor, result, risk, reason, run.LastJettison));
            }

            return records;
        }

        /// <summary>조건에 맞는 첫 런을 찾는다. 못 찾으면 null 을 돌려 테스트가 그 사실을 보고한다.</summary>
        private static bool FindRun(bool overharvest, Func<RunSession, List<FloorRecord>, bool> accept,
                                    out RunSession run, out List<FloorRecord> records)
        {
            for (int seed = 1; seed <= 400; seed++)
            {
                List<FloorRecord> made = Play(seed, overharvest, out RunSession session);
                if (!accept(session, made)) continue;
                run = session;
                records = made;
                return true;
            }
            run = null;
            records = null;
            return false;
        }

        private static bool Ended(RunSession run, List<FloorRecord> records)
            => (run.IsComplete || run.IsFailed) && records.Count > 0;

        // ── 9종 대조 ──────────────────────────────────────────────────────────

        private static string TestNineFieldsFilled()
        {
            if (!FindRun(true, Ended, out RunSession run, out List<FloorRecord> records))
                return "400시드 안에 끝난 런이 없다";

            string[] lines = RunSummaryBuilder.ComposeLines(null, run, records);
            if (lines.Length != RunSummaryTemplate.RequiredFieldCount)
                return $"{lines.Length} 줄, 기대 {RunSummaryTemplate.RequiredFieldCount} 줄";

            for (int i = 0; i < lines.Length; i++)
                if (string.IsNullOrEmpty(lines[i])) return $"{i} 번째 줄이 비었다";

            RunSummaryData data = RunSummaryBuilder.Build(run, records);
            if (string.IsNullOrEmpty(data.KeyContract)) return "핵심 계약이 비었다";
            if (string.IsNullOrEmpty(data.KeyLoadout)) return "핵심 승객·부품이 비었다";
            if (string.IsNullOrEmpty(data.EndCause)) return "종료 원인이 비었다";
            if (string.IsNullOrEmpty(data.LastOverharvestChoice)) return "마지막 선택이 비었다";
            if (string.IsNullOrEmpty(data.LostCargo)) return "잃은 것이 비었다";
            if (data.HighestFloor <= 0) return $"최고 층이 {data.HighestFloor}";
            return null;
        }

        private static string TestNoPlaceholderInRealRun()
        {
            if (!FindRun(true, Ended, out RunSession run, out List<FloorRecord> records))
                return "400시드 안에 끝난 런이 없다";

            string[] lines = RunSummaryBuilder.ComposeLines(null, run, records);
            for (int i = 0; i < lines.Length; i++)
            {
                // 「기록 없음」은 값이 비었을 때의 자리표시자다. 실제로 굴린 런에서 이것이
                // 보이면 그 항목을 채우는 코드가 없다는 뜻이고, 화면만 봐서는 알 수 없다.
                if (lines[i].IndexOf(RunSummaryTemplate.DefaultMissingText, StringComparison.Ordinal) >= 0)
                    return $"{i} 번째 줄이 자리표시자다: {lines[i]}";
            }
            return null;
        }

        private static string TestPeakCascadeMatchesRecords()
        {
            if (!FindRun(false, Ended, out RunSession run, out List<FloorRecord> records))
                return "400시드 안에 끝난 런이 없다";

            int expected = 0;
            foreach (FloorRecord record in records)
            {
                var spins = record.Spins;
                if (spins == null) continue;
                for (int i = 0; i < spins.Count; i++)
                {
                    int depth = spins[i].Resolution.ChainDepth;
                    if (depth > expected) expected = depth;
                }
            }

            RunSummaryData data = RunSummaryBuilder.Build(run, records);
            if (data.PeakCascade != expected)
                return $"최고 캐스케이드 {data.PeakCascade}, 기록에서 센 값 {expected}";
            if (expected <= 0) return "기록에 캐스케이드가 한 단계도 없다 — 표본이 요약을 검증하지 못한다";
            return null;
        }

        private static string TestHighestFloorNotUnderRecords()
        {
            if (!FindRun(false, Ended, out RunSession run, out List<FloorRecord> records))
                return "400시드 안에 끝난 런이 없다";

            int played = 0;
            foreach (FloorRecord record in records)
                if (record.Floor > played) played = record.Floor;

            RunSummaryData data = RunSummaryBuilder.Build(run, records);
            // 1층에서 실패하면 `HighestFloorReached` 는 0 이다. 그것만 쓰면 실제로 플레이한
            // 층이 요약에서 사라진다 — 그래서 기록의 층 번호가 하한이다.
            if (data.HighestFloor < played)
                return $"최고 층 {data.HighestFloor} 가 실제로 친 {played}층보다 낮다";
            return null;
        }

        private static string TestOverharvestReported()
        {
            bool Accept(RunSession session, List<FloorRecord> made)
            {
                if (!Ended(session, made)) return false;
                foreach (FloorRecord record in made)
                    if (record.ExtraSpinsTaken > 0) return true;
                return false;
            }

            if (!FindRun(true, Accept, out RunSession run, out List<FloorRecord> records))
                return "400시드 안에 과수확이 일어난 런이 없다";

            RunSummaryData data = RunSummaryBuilder.Build(run, records);
            if (data.PeakOverharvestRatio <= 0f)
                return $"과수확이 있었는데 비율이 {data.PeakOverharvestRatio}";
            if (data.LastOverharvestChoice == RunSummaryBuilder.NoOverharvestText)
                return "과수확이 있었는데 「과수확 없음」이 나왔다";
            if (data.LastOverharvestChoice.IndexOf("과수확", StringComparison.Ordinal) < 0)
                return $"마지막 선택이 과수확을 말하지 않는다: {data.LastOverharvestChoice}";
            return null;
        }

        private static string TestNoOverharvestReported()
        {
            bool Accept(RunSession session, List<FloorRecord> made)
            {
                if (!Ended(session, made)) return false;
                foreach (FloorRecord record in made)
                    if (record.ExtraSpinsTaken > 0) return false;
                return true;
            }

            if (!FindRun(false, Accept, out RunSession run, out List<FloorRecord> records))
                return "400시드 안에 과수확 없는 런이 없다";

            RunSummaryData data = RunSummaryBuilder.Build(run, records);
            if (data.PeakOverharvestRatio != 0f)
                return $"과수확이 없었는데 비율이 {data.PeakOverharvestRatio}";
            if (data.LastOverharvestChoice != RunSummaryBuilder.NoOverharvestText)
                return $"마지막 선택이 「과수확 없음」이 아니다: {data.LastOverharvestChoice}";
            return null;
        }

        private static string TestFailureLocalized()
        {
            bool Accept(RunSession session, List<FloorRecord> made)
                => session.IsFailed && made.Count > 0;

            if (!FindRun(false, Accept, out RunSession run, out List<FloorRecord> records))
                return "400시드 안에 실패한 런이 없다";

            RunSummaryData data = RunSummaryBuilder.Build(run, records);
            // `AscendResult` 는 실패 사유를 영어로 하드코딩한다. 나머지 화면이 전부
            // 한국어인데 하필 가장 설명이 필요한 줄만 영어면 설명이 거기서 끊긴다.
            if (data.EndCause.IndexOf("Crash", StringComparison.Ordinal) >= 0)
                return $"종료 원인이 영어 그대로다: {data.EndCause}";
            if (data.EndCause.IndexOf("Jettison", StringComparison.Ordinal) >= 0)
                return $"종료 원인이 영어 그대로다: {data.EndCause}";
            if (data.EndCause == RunSummaryBuilder.InProgressText)
                return "실패한 런인데 「진행 중」이다";
            return null;
        }

        private static string TestCompleteRunEndCause()
        {
            bool Accept(RunSession session, List<FloorRecord> made)
                => session.IsComplete && !session.IsFailed && made.Count > 0;

            if (!FindRun(false, Accept, out RunSession run, out List<FloorRecord> records))
                return "400시드 안에 완주한 런이 없다";

            RunSummaryData data = RunSummaryBuilder.Build(run, records);
            if (data.EndCause != RunSummaryBuilder.CompleteText)
                return $"완주한 런의 종료 원인이 「{data.EndCause}」";
            return null;
        }

        private static string TestSeedCarried()
        {
            List<FloorRecord> records = Play(4242, true, out RunSession run);
            RunSummaryData data = RunSummaryBuilder.Build(run, records);
            if (data.RunSeed != 4242) return $"런 시드 {data.RunSeed}, 기대 4242";

            // 요약 하나로 같은 런을 다시 돌릴 수 있어야 한다(PRD §10).
            string composed = RunSummaryBuilder.Compose(null, run, records);
            if (composed.IndexOf("4242", StringComparison.Ordinal) < 0)
                return "요약 본문에 시드가 없다";
            if (composed.Split('\n').Length != RunSummaryTemplate.RequiredFieldCount)
                return $"요약이 {composed.Split('\n').Length} 줄이다";
            return null;
        }

        private static string TestNoRecordsStillNineLines()
        {
            var run = new RunSession(7, 0f, 0f,
                FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation,
                new TenFloorSource());

            string[] lines = RunSummaryBuilder.ComposeLines(null, run, null);
            if (lines.Length != RunSummaryTemplate.RequiredFieldCount)
                return $"{lines.Length} 줄이다";
            for (int i = 0; i < lines.Length; i++)
                if (string.IsNullOrEmpty(lines[i])) return $"{i} 번째 줄이 비었다";

            RunSummaryData data = RunSummaryBuilder.Build(run, null);
            if (data.EndCause != RunSummaryBuilder.InProgressText)
                return $"시작하자마자 종료 원인이 「{data.EndCause}」";
            // 진행 중인 층도 도달한 층이다.
            if (data.HighestFloor < 1) return $"최고 층이 {data.HighestFloor}";
            return null;
        }

        private static string TestNullRunStillNineLines()
        {
            string[] lines = RunSummaryBuilder.ComposeLines(null, null, null);
            if (lines.Length != RunSummaryTemplate.RequiredFieldCount)
                return $"{lines.Length} 줄이다";
            for (int i = 0; i < lines.Length; i++)
                if (string.IsNullOrEmpty(lines[i])) return $"{i} 번째 줄이 비었다";
            return null;
        }

        private static string TestTemplateIsActuallyRead()
        {
            var template = ScriptableObject.CreateInstance<RunSummaryTemplate>();
            try
            {
                template.Reset();
                List<FloorRecord> records = Play(1337, true, out RunSession run);

                RunSummarySnapshot snapshot = template.Snapshot();
                string[] lines = RunSummaryBuilder.ComposeLines(template, run, records);

                // 서식 에셋의 라벨이 실제 출력에 나타나야 「문장을 데이터로 분리했다」가 성립한다.
                for (int i = 0; i < lines.Length; i++)
                {
                    string label = snapshot.LabelFor((RunSummaryField)i);
                    if (string.IsNullOrEmpty(label)) continue;
                    if (lines[i].IndexOf(label, StringComparison.Ordinal) != 0)
                        return $"{i} 번째 줄이 에셋 라벨 「{label}」로 시작하지 않는다: {lines[i]}";
                }
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }
    }
}

// 러너 편입 필요: 이 스위트는 아직 어디에도 등록되지 않았다 — 등록하지 않으면 돌지 않는다.
//   1. `Assets/Editor/AscendTestMenu.cs` 의 `AllSuites()` 에
//        UI.Tests.RunSummaryBuilderTests.RunAll(),
//   2. `Assets/Editor/PrototypeSelfTest.cs` 의 `RunAllToString()` 에
//        FoldInSuite("런 요약 9종", Ascend.Prototype.UI.Tests.RunSummaryBuilderTests.RunAll());
//   두 파일 모두 이 작업자의 소유 경로 밖이라 여기서 고치지 않았다.
