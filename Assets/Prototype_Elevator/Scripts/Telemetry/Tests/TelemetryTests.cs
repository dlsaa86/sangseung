using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascend.Prototype.Run;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace Ascend.Prototype.Telemetry.Tests
{
    /// <summary>
    /// 텔레메트리의 헤드리스 검증. `MASTER_PRD.md` §4.1이 필수로 요구하는 기록을
    /// "파일이 생기더라"가 아니라 **내용**으로 확인한다.
    ///
    /// 여기서 지키는 것은 셋이다.
    ///   1) 20개 필드의 개수와 순서 — CSV 헤더·CSV 행·JSON 키가 어긋나면 과거 로그의
    ///      열이 밀려 뜻이 바뀐다. 사람이 그 어긋남을 눈으로 잡아낼 수는 없다.
    ///   2) 결정론 — 같은 시드가 같은 기록을 내지 않으면 로그로 재현할 수 없고,
    ///      그러면 사고 기록기가 존재할 이유가 없다(`TECH_SPEC.md` §7).
    ///   3) 숫자 정합 — 스핀 순 전력의 누적이 층 전력과 어긋나면 기록이 게임을
    ///      설명하지 못한다.
    ///
    /// NUnit에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class TelemetryTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("필드가 정확히 20개다", TestFieldCount, ref passed, ref failed, report);
            Run("CSV 열 순서가 헤더와 1:1 대응", TestCsvColumnOrder, ref passed, ref failed, report);
            Run("JSON 키 순서가 CSV 헤더와 같다", TestJsonKeyOrder, ref passed, ref failed, report);
            Run("문자열 필드가 이스케이프된다", TestEscaping, ref passed, ref failed, report);
            Run("NaN·무한대가 JSON 을 깨뜨리지 않는다", TestNonFiniteNumbers, ref passed, ref failed, report);
            Run("런 하나의 스핀 수만큼 레코드가 쌓인다", TestRecordsPerSpin, ref passed, ref failed, report);
            Run("같은 시드 두 번 — 레코드 열이 완전히 같다", TestDeterminism, ref passed, ref failed, report);
            Run("다른 시드는 다른 기록을 낸다", TestSeedActuallyUsed, ref passed, ref failed, report);
            Run("netPower 누적이 층 전력과 맞는다", TestPowerLedger, ref passed, ref failed, report);
            Run("과수확으로 산 스핀이 추가 스핀으로 표시된다", TestExtraSpinIsMarked, ref passed, ref failed, report);
            Run("과적 런이 무게와 과적을 기록한다", TestOverloadIsRecorded, ref passed, ref failed, report);
            Run("런 종료가 Flush 를 부른다", TestFlushOnRunEnd, ref passed, ref failed, report);
            Run("Detach 후에는 기록되지 않는다", TestDetach, ref passed, ref failed, report);
            Run("층 문맥을 붙이면 요구 전력이 채워진다", TestFloorContextFillsRequiredPower, ref passed, ref failed, report);
            Run("문맥 없이도 2층부터는 요구 전력을 안다", TestEventOnlyRequiredPower, ref passed, ref failed, report);
            Run("파일 sink 가 jsonl·csv 두 벌을 쓴다", TestFileSinkWritesBothFiles, ref passed, ref failed, report);
            Run("같은 시드를 다시 돌려도 앞 파일을 덮지 않는다", TestFileSinkDoesNotClobber, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Telemetry Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
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
                report.AppendLine($"  FAIL  {name} — 예외: {exception.Message}");
            }
        }

        // ── 형식 계약 ────────────────────────────────────────────────────────

        /// <summary>
        /// PRD §10의 필수 기록을 스핀 단위로 분해한 결과가 20개다. 이름까지 여기에
        /// 다시 적는 이유: 구현이 헤더를 고치면 이 목록과 어긋나야 한다. 구현에서
        /// 이름을 가져와 비교하면 아무것도 검사하지 않는 단언이 된다.
        /// </summary>
        private static readonly string[] ExpectedHeader =
        {
            "runSeed", "spinSeed", "floor", "spinIndex", "isExtraSpin",
            "contract", "initialBoard", "finalBoard", "cascadeDepth", "cascadeCapped",
            "normalSouls", "purifyCount", "bestPattern", "grossPower", "residualLoss",
            "netPower", "powerAfter", "requiredPower", "carriedWeight", "overloaded",
        };

        private static string TestFieldCount()
        {
            if (ExpectedHeader.Length != 20)
                return $"테스트가 든 기대 목록이 {ExpectedHeader.Length}개다 — 검사 자체가 틀렸다";
            if (SpinTelemetryRecord.CsvHeader.Length != ExpectedHeader.Length)
                return $"헤더 {SpinTelemetryRecord.CsvHeader.Length}개, 기대 {ExpectedHeader.Length}";
            if (SpinTelemetryRecord.CsvHeader.Length != SpinTelemetryRecord.FieldCount)
                return $"헤더 {SpinTelemetryRecord.CsvHeader.Length}개인데 FieldCount 는 " +
                       $"{SpinTelemetryRecord.FieldCount} — 상수와 실제가 어긋났다";

            var seen = new HashSet<string>();
            foreach (string name in SpinTelemetryRecord.CsvHeader)
            {
                if (string.IsNullOrEmpty(name)) return "헤더에 빈 이름이 있다";
                if (!seen.Add(name)) return $"헤더에 중복된 이름: {name}";
            }

            for (int i = 0; i < ExpectedHeader.Length; i++)
                if (SpinTelemetryRecord.CsvHeader[i] != ExpectedHeader[i])
                    return $"헤더 {i}번이 '{SpinTelemetryRecord.CsvHeader[i]}', 기대 '{ExpectedHeader[i]}'";

            SpinTelemetryRecord sample = Sentinel();
            if (sample.ToCsvValues().Length != 20)
                return $"ToCsvValues {sample.ToCsvValues().Length}개, 기대 20";
            List<string> cells = SplitCsvLine(sample.ToCsvRow());
            if (cells.Count != 20) return $"ToCsvRow 열 {cells.Count}개, 기대 20";
            return null;
        }

        /// <summary>모든 필드가 서로 다른 값을 갖는 표본. 열이 밀리면 즉시 드러난다.</summary>
        private static SpinTelemetryRecord Sentinel()
        {
            return new SpinTelemetryRecord
            {
                RunSeed = 11,
                SpinSeed = 22,
                Floor = 33,
                SpinIndex = 44,
                IsExtraSpin = true,
                Contract = "C6",
                InitialBoard = "B7",
                FinalBoard = "B8",
                CascadeDepth = 99,
                CascadeCapped = true,
                NormalSouls = 111,
                PurifyCount = 122,
                BestPattern = "Line",
                GrossPower = 14.5f,
                ResidualLoss = 15.5f,
                NetPower = -16.5f,
                PowerAfter = 17.5f,
                RequiredPower = 18.5f,
                CarriedWeight = 19.5f,
                Overloaded = false,
            };
        }

        private static readonly string[] SentinelCells =
        {
            "11", "22", "33", "44", "true",
            "C6", "B7", "B8", "99", "true",
            "111", "122", "Line", "14.5", "15.5",
            "-16.5", "17.5", "18.5", "19.5", "false",
        };

        private static string TestCsvColumnOrder()
        {
            string[] values = Sentinel().ToCsvValues();
            for (int i = 0; i < SentinelCells.Length; i++)
                if (values[i] != SentinelCells[i])
                    return $"{i}번 열({SpinTelemetryRecord.CsvHeader[i]})이 '{values[i]}', 기대 '{SentinelCells[i]}'";

            List<string> cells = SplitCsvLine(Sentinel().ToCsvRow());
            for (int i = 0; i < SentinelCells.Length; i++)
                if (cells[i] != SentinelCells[i])
                    return $"행 {i}번 열이 '{cells[i]}', 기대 '{SentinelCells[i]}'";

            if (SpinTelemetryRecord.CsvHeaderLine != string.Join(",", ExpectedHeader))
                return $"헤더 줄이 '{SpinTelemetryRecord.CsvHeaderLine}'";
            return null;
        }

        private static string TestJsonKeyOrder()
        {
            string line = Sentinel().ToJsonLine();
            if (line.IndexOf('\n') >= 0 || line.IndexOf('\r') >= 0)
                return "JSONL 한 줄에 줄바꿈이 들어 있다";

            List<string> keys = ParseTopLevelKeys(line, out string error);
            if (error != null) return $"JSON 형태가 아니다: {error} — {line}";
            if (keys.Count != 20) return $"JSON 키 {keys.Count}개, 기대 20";
            for (int i = 0; i < keys.Count; i++)
                if (keys[i] != SpinTelemetryRecord.CsvHeader[i])
                    return $"JSON {i}번 키가 '{keys[i]}', CSV 헤더는 '{SpinTelemetryRecord.CsvHeader[i]}'";

            // 값도 눈으로 확인 가능한 형태여야 한다.
            if (!line.Contains("\"floor\":33")) return $"floor 값이 보이지 않는다: {line}";
            if (!line.Contains("\"netPower\":-16.5")) return $"netPower 값이 보이지 않는다: {line}";
            if (!line.Contains("\"cascadeCapped\":true")) return $"bool 이 JSON 리터럴이 아니다: {line}";
            return null;
        }

        private static string TestEscaping()
        {
            SpinTelemetryRecord record = Sentinel();
            // 계약 라벨은 사람이 쓰는 문자열이라 쉼표·따옴표가 언제든 들어올 수 있다.
            record.Contract = "위험 \"계약\", 흡수체\\증식체";
            record.InitialBoard = "줄\n바꿈";

            List<string> cells = SplitCsvLine(record.ToCsvRow());
            if (cells.Count != 20) return $"이스케이프 후 열이 {cells.Count}개로 밀렸다";
            if (cells[5] != record.Contract) return $"CSV 계약 열이 '{cells[5]}'";
            if (!record.ToCsvRow().Contains("\"\"계약\"\""))
                return "CSV 가 따옴표를 두 번 적지 않았다";

            string json = record.ToJsonLine();
            List<string> keys = ParseTopLevelKeys(json, out string error);
            if (error != null) return $"이스케이프된 JSON 이 깨졌다: {error} — {json}";
            if (keys.Count != 20) return $"이스케이프 후 JSON 키 {keys.Count}개";
            if (!json.Contains("\\\"계약\\\"")) return $"JSON 이 따옴표를 이스케이프하지 않았다: {json}";
            if (!json.Contains("흡수체\\\\증식체")) return $"JSON 이 역슬래시를 이스케이프하지 않았다: {json}";
            if (!json.Contains("줄\\n바꿈")) return $"JSON 이 줄바꿈을 이스케이프하지 않았다: {json}";
            if (json.IndexOf('\n') >= 0) return "JSONL 줄이 두 줄로 쪼개졌다";
            return null;
        }

        private static string TestNonFiniteNumbers()
        {
            SpinTelemetryRecord record = Sentinel();
            record.GrossPower = float.NaN;
            record.NetPower = float.PositiveInfinity;

            string json = record.ToJsonLine();
            ParseTopLevelKeys(json, out string error);
            if (error != null) return $"NaN 이 JSON 을 깨뜨렸다: {error} — {json}";
            if (!json.Contains("\"grossPower\":null")) return $"NaN 이 null 로 낮춰지지 않았다: {json}";

            // CSV 쪽에는 사실이 남아야 한다 — 0으로 적으면 로그가 거짓말을 한다.
            string[] values = record.ToCsvValues();
            if (values[13] != "NaN") return $"CSV grossPower 가 '{values[13]}'";
            if (values[15] != "Infinity") return $"CSV netPower 가 '{values[15]}'";
            return null;
        }

        // ── 런 기록 ──────────────────────────────────────────────────────────

        private static string TestRecordsPerSpin()
        {
            var run = new RunSession(1337);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink);

            int spins = Drive(run);
            recorder.Detach();

            if (spins <= 0) return "런이 스핀을 하나도 돌리지 않았다 — 진행 코드가 막혔다";
            if (sink.Records.Count != spins)
                return $"레코드 {sink.Records.Count}개, 실제 스핀 {spins}회";
            if (recorder.RecordCount != spins)
                return $"RecordCount {recorder.RecordCount}, 실제 스핀 {spins}회";
            if (recorder.DroppedCount != 0)
                return $"버려진 SpinResolved {recorder.DroppedCount}건 — 사건 계약이 깨졌다";

            foreach (SpinTelemetryRecord r in sink.Records)
            {
                if (r.RunSeed != 1337) return $"runSeed 가 {r.RunSeed}";
                if (r.Floor <= 0) return $"층 번호가 {r.Floor}";
                if (r.SpinIndex < 0) return $"스핀 인덱스가 {r.SpinIndex}";
                if (string.IsNullOrEmpty(r.InitialBoard) || string.IsNullOrEmpty(r.FinalBoard))
                    return "보드 문자열이 비어 있다";
                if (string.IsNullOrEmpty(r.Contract)) return "계약 라벨이 비어 있다";
                if (string.IsNullOrEmpty(r.BestPattern)) return "최고 패턴이 비어 있다";
                if (r.CascadeDepth < 1) return $"연쇄 깊이가 {r.CascadeDepth} — 최초 판정도 한 단계다";
                if (r.ResidualLoss < 0f) return $"잔류 손실이 음수 {r.ResidualLoss}";
            }
            return null;
        }

        private static string TestDeterminism()
        {
            List<string> first = RecordRows(1337);
            List<string> second = RecordRows(1337);
            if (first.Count == 0) return "기록이 비어 있다";
            if (first.Count != second.Count)
                return $"레코드 수 {first.Count} vs {second.Count}";
            for (int i = 0; i < first.Count; i++)
                if (first[i] != second[i])
                    return $"{i}번 레코드 불일치\n  A: {first[i]}\n  B: {second[i]}";
            return null;
        }

        /// <summary>
        /// 결정론 검사만 있으면 시드를 통째로 무시하는 구현도 통과한다.
        /// `PrototypeSelfTest.Test8` 이 같은 함정에 실제로 빠졌던 적이 있다.
        /// </summary>
        private static string TestSeedActuallyUsed()
        {
            List<string> a = RecordRows(1337);
            List<string> b = RecordRows(9001);
            if (a.Count == 0 || b.Count == 0) return "기록이 비어 있다";
            if (a.Count != b.Count) return null;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return null;
            return "다른 시드가 완전히 같은 기록을 냈다 — 시드가 쓰이지 않는다";
        }

        private static string TestPowerLedger()
        {
            var run = new RunSession(1337);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink);
            Drive(run);
            recorder.Detach();

            IReadOnlyList<SpinTelemetryRecord> records = sink.Records;
            if (records.Count == 0) return "기록이 비어 있다";

            int i = 0;
            while (i < records.Count)
            {
                int floor = records[i].Floor;
                float running = 0f;   // 층 전력은 0에서 시작한다
                int j = i;
                while (j < records.Count && records[j].Floor == floor)
                {
                    SpinTelemetryRecord r = records[j];
                    if (r.SpinIndex != j - i)
                        return $"{floor}층 {j - i}번째 레코드의 spinIndex 가 {r.SpinIndex}";

                    float ceiling = running + r.NetPower;
                    if (!r.IsExtraSpin)
                    {
                        if (!Near(r.PowerAfter, ceiling))
                            return $"{floor}층 스핀 {r.SpinIndex}: powerAfter {r.PowerAfter:0.###}, " +
                                   $"기대 {ceiling:0.###} (직전 {running:0.###} + net {r.NetPower:0.###})";
                    }
                    else if (r.PowerAfter > ceiling + Tolerance(ceiling))
                    {
                        // 추가 스핀은 앤티를 먼저 물었으므로 누적이 기대치보다 **작아야** 한다.
                        return $"{floor}층 추가 스핀 {r.SpinIndex}: powerAfter {r.PowerAfter:0.###} 가 " +
                               $"앤티 없는 상한 {ceiling:0.###} 을 넘었다 — 판돈이 기록에서 사라졌다";
                    }
                    running = r.PowerAfter;
                    j++;
                }
                i = j;
            }
            return null;
        }

        /// <summary>
        /// 판돈을 치른 스핀과 기본 스핀이 구분되지 않으면 "과수확이 이득이었는가"를
        /// 로그로 물을 수 없다 — `MASTER_PRD.md` §10이 "과수확 선택 여부"를 필수로 든 이유다.
        /// 기본 진행(요구 전력을 넘으면 즉시 확정)은 추가 스핀을 한 번도 만들지 않으므로
        /// 여기서만 일부러 레버를 당긴다.
        /// </summary>
        private static string TestExtraSpinIsMarked()
        {
            for (int seed = 1337; seed < 1387; seed++)
            {
                var run = new RunSession(seed);
                var sink = new InMemoryTelemetrySink();
                var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run));
                DrivePushingLuck(run);
                recorder.Detach();

                IReadOnlyList<SpinTelemetryRecord> records = sink.Records;
                for (int i = 0; i < records.Count; i++)
                {
                    if (!records[i].IsExtraSpin) continue;
                    SpinTelemetryRecord extra = records[i];

                    if (extra.SpinIndex == 0)
                        return $"시드 {seed}: 층의 첫 스핀이 추가 스핀으로 표시됐다";
                    if (i == 0 || records[i - 1].Floor != extra.Floor)
                        return $"시드 {seed}: 추가 스핀 앞에 같은 층의 스핀이 없다";

                    // 앤티는 레버를 당기는 순간 빠진다(`FloorSession.PushYourLuck`).
                    // 따라서 누적 전력은 "앤티가 없었다면" 값보다 반드시 작아야 한다.
                    float ceiling = records[i - 1].PowerAfter + extra.NetPower;
                    if (extra.PowerAfter >= ceiling - Tolerance(ceiling))
                        return $"시드 {seed}: 추가 스핀인데 판돈이 빠진 흔적이 없다 " +
                               $"(powerAfter {extra.PowerAfter:0.###}, 앤티 없는 상한 {ceiling:0.###})";
                    return null;
                }
            }
            return "50개 시드 안에 추가 스핀이 한 번도 일어나지 않았다 — " +
                   "과수확 경로가 막혔거나 요구 전력 달성 시 스핀이 남지 않는다";
        }

        /// <summary>
        /// 무게와 과적은 요구 전력에 1.5배를 거는 값이라(`FloorSession.OverloadRequiredPowerMultiplier`)
        /// 기록에 없으면 "왜 이 층이 그렇게 어려웠는가"를 설명할 수 없다.
        /// </summary>
        private static string TestOverloadIsRecorded()
        {
            const float heavy = 200f;   // 허용 중량 100 의 두 배
            var run = new RunSession(1337, heavy, 0f);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run));
            Drive(run);
            recorder.Detach();

            if (sink.Records.Count == 0) return "기록이 비어 있다";
            foreach (SpinTelemetryRecord r in sink.Records)
            {
                if (r.CarriedWeight < heavy)
                    return $"{r.Floor}층 무게가 {r.CarriedWeight}, 기대 {heavy} 이상";
                if (!r.Overloaded)
                    return $"{r.Floor}층 무게 {r.CarriedWeight} 가 허용 " +
                           $"{FloorSession.AllowedWeight} 를 넘는데 과적으로 기록되지 않았다";
            }
            return null;
        }

        private static string TestFlushOnRunEnd()
        {
            var run = new RunSession(1337);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink);
            Drive(run);

            if (!run.IsComplete && !run.IsFailed)
                return "런이 끝나지 않았다 — 자동 진행이 중간에 막혔다";
            if (sink.FlushCount == 0)
                return "런이 끝났는데 Flush 가 불리지 않았다 — 버퍼가 디스크에 나가지 않는다";
            recorder.Detach();
            return null;
        }

        private static string TestDetach()
        {
            var run = new RunSession(1337);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink);

            // 1층 첫 스핀만 기록한 뒤 끊는다.
            FloorSession floor = run.Current;
            if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
            if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);
            run.Spin();
            int before = sink.Records.Count;
            if (before == 0) return "첫 스핀이 기록되지 않았다";

            recorder.Detach();
            Drive(run);
            if (sink.Records.Count != before)
                return $"Detach 후에도 {sink.Records.Count - before}건이 더 기록됐다";

            recorder.Detach();   // 두 번 불러도 터지지 않아야 한다
            return null;
        }

        private static string TestFloorContextFillsRequiredPower()
        {
            var run = new RunSession(1337);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run));
            Drive(run);
            recorder.Detach();

            if (sink.Records.Count == 0) return "기록이 비어 있다";
            foreach (SpinTelemetryRecord r in sink.Records)
            {
                if (r.RequiredPower <= 0f)
                    return $"{r.Floor}층 스핀 {r.SpinIndex} 의 요구 전력이 {r.RequiredPower}";
                if (r.CarriedWeight < 0f)
                    return $"{r.Floor}층 무게가 음수 {r.CarriedWeight}";
            }
            if (recorder.LastWarning != null)
                return $"문맥을 붙였는데 경고가 남았다: {recorder.LastWarning}";
            return null;
        }

        /// <summary>
        /// 문맥 없이 사건만 볼 때의 한계를 고정한다. 1층 `FloorStarted`는 `RunSession`
        /// 생성자 안에서 발행되므로 어떤 구독자도 받을 수 없다 — 그 사실을 기록이
        /// 숨기지 않고 경고로 드러내는지까지 본다.
        /// </summary>
        private static string TestEventOnlyRequiredPower()
        {
            // 2층 이상 올라간 런이 필요하다. 특정 시드가 1층에서 죽어도 검사가 무의미해지지
            // 않도록 몇 개를 훑는다 — 여기서 재는 것은 밸런스가 아니다.
            for (int seed = 1337; seed < 1367; seed++)
            {
                var run = new RunSession(seed);
                var sink = new InMemoryTelemetrySink();
                var recorder = new TelemetryRecorder(run.Events, sink);
                int firstFloor = run.Current.Plan.Floor;
                Drive(run);
                recorder.Detach();

                bool sawLaterFloor = false;
                foreach (SpinTelemetryRecord r in sink.Records)
                {
                    if (r.Floor == firstFloor) continue;
                    sawLaterFloor = true;
                    if (r.RequiredPower <= 0f)
                        return $"시드 {seed} {r.Floor}층 요구 전력이 {r.RequiredPower} — " +
                               "FloorStarted 를 받고도 비었다";
                }
                if (!sawLaterFloor) continue;

                if (recorder.LastWarning == null)
                    return "1층 요구 전력을 모르는데 경고가 없다 — 모르는 것을 아는 척했다";
                return null;
            }
            return "30개 시드 안에 2층 이상 오른 런이 없다 — 자동 진행이나 밸런스가 깨졌다";
        }

        // ── 파일 sink ────────────────────────────────────────────────────────

        private static string TestFileSinkWritesBothFiles()
        {
            string dir = NewTempDirectory();
            try
            {
                var sink = new TelemetryFileSink(4242, dir) { AutoFlushEvery = 0 };
                sink.Write(Sentinel());
                SpinTelemetryRecord second = Sentinel();
                second.SpinIndex = 45;
                sink.Write(second);

                if (File.Exists(sink.CsvPath)) return "Flush 전에 파일이 생겼다 — 버퍼가 동작하지 않는다";
                sink.Flush();

                if (sink.ErrorCount != 0) return $"IO 실패: {sink.LastError?.Message}";
                if (!File.Exists(sink.JsonlPath)) return $"jsonl 이 없다: {sink.JsonlPath}";
                if (!File.Exists(sink.CsvPath)) return $"csv 가 없다: {sink.CsvPath}";
                if (!sink.JsonlPath.EndsWith("run_4242_0.jsonl"))
                    return $"파일 이름 규약이 다르다: {sink.JsonlPath}";

                string[] jsonLines = ReadNonEmptyLines(sink.JsonlPath);
                if (jsonLines.Length != 2) return $"jsonl 줄 수 {jsonLines.Length}, 기대 2";
                foreach (string line in jsonLines)
                {
                    List<string> keys = ParseTopLevelKeys(line, out string error);
                    if (error != null) return $"파일의 JSON 줄이 깨졌다: {error}";
                    if (keys.Count != 20) return $"파일의 JSON 키 {keys.Count}개";
                }

                string[] csvLines = ReadNonEmptyLines(sink.CsvPath);
                if (csvLines.Length != 3) return $"csv 줄 수 {csvLines.Length}, 기대 3(헤더+2)";
                if (csvLines[0] != SpinTelemetryRecord.CsvHeaderLine)
                    return $"csv 첫 줄이 헤더가 아니다: {csvLines[0]}";
                if (SplitCsvLine(csvLines[1]).Count != 20)
                    return "csv 데이터 줄의 열 수가 20이 아니다";

                if (sink.FlushedCount != 2) return $"FlushedCount {sink.FlushedCount}, 기대 2";

                // 두 번째 Flush 는 아무것도 더 쓰지 않아야 한다.
                sink.Flush();
                if (ReadNonEmptyLines(sink.CsvPath).Length != 3) return "빈 Flush 가 줄을 더 썼다";
                return null;
            }
            finally { DeleteQuietly(dir); }
        }

        private static string TestFileSinkDoesNotClobber()
        {
            string dir = NewTempDirectory();
            try
            {
                var first = new TelemetryFileSink(777, dir) { AutoFlushEvery = 0 };
                first.Write(Sentinel());
                first.Flush();

                var second = new TelemetryFileSink(777, dir) { AutoFlushEvery = 0 };
                if (second.JsonlPath == first.JsonlPath)
                    return $"같은 시드의 두 번째 런이 앞 파일을 덮어쓴다: {second.JsonlPath}";
                second.Write(Sentinel());
                second.Flush();

                if (ReadNonEmptyLines(first.JsonlPath).Length != 1)
                    return "첫 런의 기록이 손상됐다";
                if (second.ErrorCount != 0) return $"IO 실패: {second.LastError?.Message}";
                return null;
            }
            finally { DeleteQuietly(dir); }
        }

        // ── 도우미 ──────────────────────────────────────────────────────────

        private static List<string> RecordRows(int seed)
        {
            var run = new RunSession(seed);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run));
            Drive(run);
            recorder.Detach();

            var rows = new List<string>(sink.Records.Count);
            foreach (SpinTelemetryRecord r in sink.Records) rows.Add(r.ToCsvRow());
            return rows;
        }

        /// <summary>
        /// 런을 끝까지 자동 진행한다. `BuildTests.Drive`와 같은 골격이지만 정책 없이
        /// 항상 0번 선택지를 고른다 — 여기서 재는 것은 밸런스가 아니라 기록이다.
        /// </summary>
        private static int Drive(RunSession run)
        {
            int spins = 0;
            int guard = 0;
            while (!run.IsComplete && !run.IsFailed && guard++ < 400)
            {
                FloorSession floor = run.Current;
                if (floor == null) break;

                if (floor.Phase == FloorPhase.Boarding && !run.FinishBoarding()) break;
                if (floor.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) break;

                int inner = 0;
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && inner++ < 40)
                {
                    int before = floor.SpinsUsed;
                    run.Spin();
                    if (floor.SpinsUsed == before) break;   // 거부됐다면 무한 루프를 만들지 않는다
                    spins++;
                }

                if (floor.CanBank) { if (run.Bank() == null) break; }
                else if (floor.SpinsRemaining == 0) { if (run.ForceResolve() == null) break; }
                else break;
            }
            return spins;
        }

        /// <summary><see cref="Drive"/>와 같지만 확정 전에 한 번은 과수확 레버를 당긴다.</summary>
        private static int DrivePushingLuck(RunSession run)
        {
            int spins = 0;
            int guard = 0;
            while (!run.IsComplete && !run.IsFailed && guard++ < 400)
            {
                FloorSession floor = run.Current;
                if (floor == null) break;

                if (floor.Phase == FloorPhase.Boarding && !run.FinishBoarding()) break;
                if (floor.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) break;

                bool pushed = false;
                int inner = 0;
                while (inner++ < 40)
                {
                    if (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0)
                    {
                        int before = floor.SpinsUsed;
                        run.Spin();
                        if (floor.SpinsUsed == before) break;
                        spins++;
                        continue;
                    }
                    // 층당 한 번만 당긴다. 계속 당기면 앤티가 불어나 층이 늘 실패하고,
                    // 그러면 다음 층의 기록을 볼 수 없다.
                    if (!pushed && floor.Phase == FloorPhase.Decision && floor.CanBank &&
                        floor.SpinsRemaining > 0 && run.PushYourLuck())
                    {
                        pushed = true;
                        continue;
                    }
                    break;
                }

                if (floor.CanBank) { if (run.Bank() == null) break; }
                else if (floor.SpinsRemaining == 0) { if (run.ForceResolve() == null) break; }
                else break;
            }
            return spins;
        }

        private static float Tolerance(float magnitude) => 0.01f + 0.0005f * Math.Abs(magnitude);

        private static bool Near(float a, float b) => Math.Abs(a - b) <= Tolerance(b);

        /// <summary>RFC 4180 한 줄 분해. 따옴표 안의 쉼표를 열 구분자로 세지 않는다.</summary>
        private static List<string> SplitCsvLine(string line)
        {
            var cells = new List<string>(20);
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else sb.Append(c);
                    continue;
                }
                if (c == '"') { quoted = true; continue; }
                if (c == ',') { cells.Add(sb.ToString()); sb.Length = 0; continue; }
                sb.Append(c);
            }
            cells.Add(sb.ToString());
            return cells;
        }

        /// <summary>
        /// JSON 객체의 최상위 키를 순서대로 뽑는다. 완전한 파서가 아니라 **형태 검사기**다 —
        /// 목적은 값을 읽는 것이 아니라 "따옴표가 새지 않았는가, 키 순서가 계약과 같은가"다.
        /// </summary>
        private static List<string> ParseTopLevelKeys(string line, out string error)
        {
            var keys = new List<string>();
            error = null;
            if (string.IsNullOrEmpty(line)) { error = "빈 줄"; return keys; }

            int depth = 0;
            bool inString = false, escaped = false;
            var sb = new StringBuilder();
            string lastString = null;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString)
                {
                    if (escaped) { escaped = false; sb.Append(c); continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c == '"') { inString = false; lastString = sb.ToString(); sb.Length = 0; continue; }
                    if (c < ' ')
                    {
                        error = $"이스케이프되지 않은 제어 문자 U+{(int)c:X4}";
                        return keys;
                    }
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': inString = true; sb.Length = 0; break;
                    case '{': case '[': depth++; break;
                    case '}': case ']': depth--; break;
                    case ':':
                        if (depth == 1)
                        {
                            if (lastString == null) { error = "키 없이 ':' 가 나왔다"; return keys; }
                            keys.Add(lastString);
                        }
                        lastString = null;
                        break;
                }
            }

            if (inString) error = "닫히지 않은 문자열";
            else if (depth != 0) error = $"괄호 균형이 맞지 않는다 (depth={depth})";
            return keys;
        }

        private static string[] ReadNonEmptyLines(string path)
        {
            string[] raw = File.ReadAllLines(path);
            var kept = new List<string>(raw.Length);
            foreach (string line in raw) if (!string.IsNullOrEmpty(line)) kept.Add(line);
            return kept.ToArray();
        }

        private static string NewTempDirectory()
        {
            // 프로젝트 트리를 더럽히지 않는다. 테스트가 남긴 파일이 캡처 하네스나
            // 커밋 게이트의 입력으로 새어 들어가면 원인 분리가 어려워진다.
            string dir = Path.Combine(Path.GetTempPath(),
                "ascend_telemetry_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteQuietly(string dir)
        {
            try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
            catch (Exception) { /* 정리 실패가 테스트 결과를 뒤집으면 안 된다 */ }
        }

#if UNITY_EDITOR
        [MenuItem("Ascend/Run Telemetry Tests")]
        public static void RunFromMenu()
        {
            var result = RunAll();
            if (result.failed > 0) Debug.LogError(result.report);
            else Debug.Log(result.report);
        }
#endif
    }
}
