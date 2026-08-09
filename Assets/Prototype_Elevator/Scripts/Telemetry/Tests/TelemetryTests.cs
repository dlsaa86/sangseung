using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ascend.Prototype.Events;
using Ascend.Prototype.Risk;
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
    /// 여기서 지키는 것은 넷이다.
    ///   1) **Notion §16.2 11항목 대조** — 완료 판정의 기준이다(`D-20260801-06`).
    ///      개수를 기준으로 삼으면 "20개를 채웠으니 완료"가 성립해 실제로 빠진 다섯이
    ///      통과해 버린다. 첫 구현이 정확히 그렇게 통과했다.
    ///   2) 필드 **순서** — CSV 헤더·CSV 행·JSON 키가 어긋나면 과거 로그의 열이 밀려
    ///      뜻이 바뀐다. 사람이 그 어긋남을 눈으로 잡아낼 수는 없다.
    ///   3) 결정론 — 같은 시드가 같은 기록을 내지 않으면 로그로 재현할 수 없고,
    ///      그러면 사고 기록기가 존재할 이유가 없다(`TECH_SPEC.md` §7).
    ///   4) 숫자 정합 — 스핀 순 전력의 누적이 층 전력과 어긋나면 기록이 게임을
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

            Run("§16.2 11항목이 빠짐없이 필드로 덮인다", TestNotion162Coverage, ref passed, ref failed, report);
            Run("헤더 모양이 성하다 (중복·빈 이름·상수 불일치)", TestHeaderShape, ref passed, ref failed, report);
            Run("CSV 열 순서가 헤더와 1:1 대응", TestCsvColumnOrder, ref passed, ref failed, report);
            Run("JSON 키 순서가 CSV 헤더와 같다", TestJsonKeyOrder, ref passed, ref failed, report);
            Run("목록형은 CSV 한 칸·JSONL 배열로 갈라진다", TestListFieldsSplitRepresentation, ref passed, ref failed, report);
            Run("문자열 필드가 이스케이프된다", TestEscaping, ref passed, ref failed, report);
            Run("NaN·무한대가 JSON 을 깨뜨리지 않는다", TestNonFiniteNumbers, ref passed, ref failed, report);
            Run("런 하나의 스핀 수만큼 레코드가 쌓인다", TestRecordsPerSpin, ref passed, ref failed, report);
            Run("캐스케이드 단계마다 보드가 하나씩 남는다", TestCascadeBoardsPerStep, ref passed, ref failed, report);
            Run("발동 순서가 개수·최고 패턴과 어긋나지 않는다", TestActivationOrder, ref passed, ref failed, report);
            Run("잔류 개수가 손실과 같은 이야기를 한다", TestResidualCounts, ref passed, ref failed, report);
            Run("출처가 없으면 위험 단계를 (unknown) 으로 적는다", TestRiskUnknownWithoutSource, ref passed, ref failed, report);
            Run("위험 단계 사건과 출처가 기록에 흘러든다", TestRiskFlowsIn, ref passed, ref failed, report);
            Run("표본기가 없으면 프레임·GC 를 모른다고 적는다", TestPerfUnknownWithoutSampler, ref passed, ref failed, report);
            Run("표본기를 붙이면 스핀마다 한 번씩 잰다", TestPerfSamplerIsPulledPerSpin, ref passed, ref failed, report);
            Run("적재가 없으면 (none), 문맥이 없으면 (unknown)", TestLoadoutMarkers, ref passed, ref failed, report);
            Run("실은 승객·부품과 그 발동이 기록된다", TestLoadoutIsRecorded, ref passed, ref failed, report);
            Run("같은 시드 두 번 — 레코드 열이 완전히 같다", TestDeterminism, ref passed, ref failed, report);
            Run("다른 시드는 다른 기록을 낸다", TestSeedActuallyUsed, ref passed, ref failed, report);
            Run("netPower 누적이 층 전력과 맞는다", TestPowerLedger, ref passed, ref failed, report);
            Run("과수확으로 산 스핀이 추가 스핀으로 표시된다", Shelved(TestExtraSpinIsMarked), ref passed, ref failed, report);
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

        /// <summary>보류된 과수확(<see cref="PrototypeFeatures.Overharvest"/>) 검사를 범위 안에서만 켠다.</summary>
        private static Func<string> Shelved(Func<string> test)
        {
            return () =>
            {
                using (PrototypeFeatures.EnableOverharvest()) return test();
            };
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
        /// 기대 헤더. 이름까지 여기에 다시 적는 이유: 구현이 헤더를 고치면 이 목록과
        /// 어긋나야 한다. 구현에서 이름을 가져와 비교하면 아무것도 검사하지 않는 단언이 된다.
        ///
        /// **이 배열의 길이가 완료 기준은 아니다.** 기준은 아래 <see cref="Notion162Items"/> 다.
        /// </summary>
        private static readonly string[] ExpectedHeader =
        {
            "runSeed", "spinSeed", "floor", "spinIndex", "isExtraSpin",
            "contract", "initialBoard", "finalBoard", "cascadeDepth", "cascadeCapped",
            "normalSouls", "purifyCount", "bestPattern", "grossPower", "residualLoss",
            "netPower", "powerAfter", "requiredPower", "carriedWeight", "overloaded",
            "cascadeBoards", "activationOrder", "residualAbsorbers", "residualProliferators",
            "riskLevel", "loadout", "frameTimeMs", "gcAllocBytes",
        };

        /// <summary>Notion §16.2 「플레이 로그」 한 항목과 그것을 덮는 필드들.</summary>
        private struct LogItem
        {
            /// <summary>§16.2 원문의 항목 이름.</summary>
            public string Name;

            /// <summary>이 항목을 덮는 스핀 레코드 필드. 런 단위 항목만 비어 있을 수 있다.</summary>
            public string[] Fields;

            /// <summary>스핀의 속성이 아니라 런의 속성인가.</summary>
            public bool RunScoped;
        }

        /// <summary>
        /// **완료 기준.** `D-20260801-06`이 「필드 수를 목표로 삼지 않고 §16.2의 11항목을
        /// 빠짐없이 덮는다」고 결정하면서 「`TelemetryTests`가 11항목 대조표를 코드로
        /// 검사한다」를 회귀 방지로 약속했다. 이 배열이 그 약속이다.
        ///
        /// 이 표가 잡는 것은 셋이다.
        ///   ① 어느 항목이 필드를 하나도 갖지 못한 상태 — 곧 미구현이다.
        ///   ② §16.2의 어느 항목도 설명하지 못하는 필드가 늘어난 상태 — 개수 채우기다.
        ///   ③ 필드 이름이 바뀌었는데 이 표가 옛 이름을 가리키는 상태 — 대조표의 부패다.
        ///
        /// 11번은 **일부러 비어 있다.** 런 종료 원인은 스핀의 속성이 아니라 런의 속성이고,
        /// 스핀 레코드에 넣으면 스핀마다 "아직 안 끝났다"를 반복하게 된다. 같은 결정이
        /// "런 단위 레코드가 따로 생긴다"고 적었고 그것은 아직 없다 —
        /// **이 표는 그 사실을 감추지 않고 자리로 남겨 둔다.**
        /// </summary>
        private static readonly LogItem[] Notion162Items =
        {
            new LogItem { Name = "층·스핀·시드",
                Fields = new[] { "runSeed", "spinSeed", "floor", "spinIndex" } },
            new LogItem { Name = "초기 보드와 캐스케이드별 보드",
                Fields = new[] { "initialBoard", "finalBoard", "cascadeDepth", "cascadeCapped", "cascadeBoards" } },
            new LogItem { Name = "정화·패턴·발동 순서",
                Fields = new[] { "normalSouls", "purifyCount", "bestPattern", "activationOrder" } },
            new LogItem { Name = "획득/요구 전력",
                Fields = new[] { "grossPower", "netPower", "powerAfter", "requiredPower" } },
            new LogItem { Name = "선택 계약",
                Fields = new[] { "contract" } },
            new LogItem { Name = "잔류 저항",
                Fields = new[] { "residualLoss", "residualAbsorbers", "residualProliferators" } },
            new LogItem { Name = "현재 위험 단계",
                Fields = new[] { "riskLevel" } },
            new LogItem { Name = "과수확 선택과 결과",
                Fields = new[] { "isExtraSpin" } },
            new LogItem { Name = "승객·부품 발동",
                Fields = new[] { "loadout", "carriedWeight", "overloaded" } },
            new LogItem { Name = "프레임 타임과 GC Alloc",
                Fields = new[] { "frameTimeMs", "gcAllocBytes" } },
            new LogItem { Name = "런 종료 원인", Fields = new string[0], RunScoped = true },
        };

        private static string TestNotion162Coverage()
        {
            if (Notion162Items.Length != 11)
                return $"대조표가 {Notion162Items.Length}항목이다 — §16.2 는 11항목이다";

            var claimedBy = new Dictionary<string, string>();
            foreach (LogItem item in Notion162Items)
            {
                int count = item.Fields != null ? item.Fields.Length : 0;

                if (item.RunScoped)
                {
                    if (count != 0)
                        return $"「{item.Name}」은 런 단위인데 스핀 레코드가 {count}개 필드를 주장한다 — " +
                               "스핀마다 반복되는 런 속성은 기록이 아니라 잡음이다";
                    continue;
                }

                if (count == 0)
                    return $"「{item.Name}」을 덮는 필드가 하나도 없다 — §16.2 항목이 미구현이다";

                foreach (string field in item.Fields)
                {
                    if (Array.IndexOf(SpinTelemetryRecord.CsvHeader, field) < 0)
                        return $"「{item.Name}」이 존재하지 않는 필드 '{field}' 를 가리킨다 — " +
                               "필드 이름이 바뀌었는데 대조표가 따라가지 않았다";
                    if (claimedBy.ContainsKey(field))
                        return $"필드 '{field}' 를 「{claimedBy[field]}」와 「{item.Name}」이 겹쳐 주장한다";
                    claimedBy[field] = item.Name;
                }
            }

            foreach (string field in SpinTelemetryRecord.CsvHeader)
                if (!claimedBy.ContainsKey(field))
                    return $"필드 '{field}' 가 §16.2 의 어느 항목도 설명하지 못한다 — " +
                           "필드 수를 늘리는 것은 목표가 아니다(D-20260801-06). " +
                           "덮는 항목을 대조표에 적거나 필드를 지운다";

            return null;
        }

        private static string TestHeaderShape()
        {
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
            if (sample.ToCsvValues().Length != ExpectedHeader.Length)
                return $"ToCsvValues {sample.ToCsvValues().Length}개, 기대 {ExpectedHeader.Length}";
            List<string> cells = SplitCsvLine(sample.ToCsvRow());
            if (cells.Count != ExpectedHeader.Length)
                return $"ToCsvRow 열 {cells.Count}개, 기대 {ExpectedHeader.Length}";
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
                CascadeBoards = new[] { "B21a", "B21b" },
                ActivationOrder = new[] { "1:Soul*2", "2:Absorber/Line*3" },
                ResidualAbsorbers = 231,
                ResidualProliferators = 242,
                RiskLevel = "Critical",
                Loadout = "PRT_X[CascadeStep=0.25]",
                FrameTimeMs = 27.5f,
                GcAllocBytes = 288L,
            };
        }

        private static readonly string[] SentinelCells =
        {
            "11", "22", "33", "44", "true",
            "C6", "B7", "B8", "99", "true",
            "111", "122", "Line", "14.5", "15.5",
            "-16.5", "17.5", "18.5", "19.5", "false",
            // 목록형 둘은 CSV 에서 ListSeparator 로 접힌 한 칸이다.
            "B21a;B21b", "1:Soul*2;2:Absorber/Line*3", "231", "242",
            "Critical", "PRT_X[CascadeStep=0.25]", "27.5", "288",
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
            if (keys.Count != ExpectedHeader.Length)
                return $"JSON 키 {keys.Count}개, 기대 {ExpectedHeader.Length}";
            for (int i = 0; i < keys.Count; i++)
                if (keys[i] != SpinTelemetryRecord.CsvHeader[i])
                    return $"JSON {i}번 키가 '{keys[i]}', CSV 헤더는 '{SpinTelemetryRecord.CsvHeader[i]}'";

            // 값도 눈으로 확인 가능한 형태여야 한다.
            if (!line.Contains("\"floor\":33")) return $"floor 값이 보이지 않는다: {line}";
            if (!line.Contains("\"netPower\":-16.5")) return $"netPower 값이 보이지 않는다: {line}";
            if (!line.Contains("\"cascadeCapped\":true")) return $"bool 이 JSON 리터럴이 아니다: {line}";
            if (!line.Contains("\"gcAllocBytes\":288")) return $"gcAllocBytes 가 정수 리터럴이 아니다: {line}";
            return null;
        }

        /// <summary>
        /// 목록형 두 항목은 CSV와 JSONL의 표현이 **일부러 다르다.** CSV 한 행은 한 스핀이라
        /// 열을 늘릴 수 없고, JSONL은 스크립트가 읽으므로 접을 이유가 없다.
        /// 이 차이가 실수가 아니라 설계라는 것을 코드로 고정한다 —
        /// 나중에 "CSV도 배열로 내자"거나 "JSON도 접자"는 변경이 조용히 들어오면 여기서 걸린다.
        /// </summary>
        private static string TestListFieldsSplitRepresentation()
        {
            SpinTelemetryRecord record = Sentinel();
            string[] values = record.ToCsvValues();

            int boardsColumn = Array.IndexOf(SpinTelemetryRecord.CsvHeader, "cascadeBoards");
            int orderColumn = Array.IndexOf(SpinTelemetryRecord.CsvHeader, "activationOrder");
            if (boardsColumn < 0 || orderColumn < 0) return "목록형 열을 헤더에서 찾지 못했다";

            if (values[boardsColumn] != string.Join(SpinTelemetryRecord.ListSeparator, record.CascadeBoards))
                return $"CSV cascadeBoards 가 '{values[boardsColumn]}' — 구분자로 이은 한 칸이 아니다";
            if (values[orderColumn] != string.Join(SpinTelemetryRecord.ListSeparator, record.ActivationOrder))
                return $"CSV activationOrder 가 '{values[orderColumn]}'";

            string json = record.ToJsonLine();
            if (!json.Contains("\"cascadeBoards\":[\"B21a\",\"B21b\"]"))
                return $"JSONL 이 cascadeBoards 를 배열로 내지 않았다: {json}";
            if (!json.Contains("\"activationOrder\":[\"1:Soul*2\",\"2:Absorber/Line*3\"]"))
                return $"JSONL 이 activationOrder 를 배열로 내지 않았다: {json}";

            // 빈 목록은 CSV 빈 칸 · JSON 빈 배열이다. null 로 새어 나가면 소비자가 갈라진다.
            SpinTelemetryRecord empty = Sentinel();
            empty.CascadeBoards = null;
            empty.ActivationOrder = new string[0];
            string[] emptyValues = empty.ToCsvValues();
            if (emptyValues[boardsColumn] != string.Empty)
                return $"빈 목록의 CSV 칸이 '{emptyValues[boardsColumn]}'";
            string emptyJson = empty.ToJsonLine();
            if (!emptyJson.Contains("\"cascadeBoards\":[]") || !emptyJson.Contains("\"activationOrder\":[]"))
                return $"빈 목록이 JSON 빈 배열이 아니다: {emptyJson}";
            if (SplitCsvLine(empty.ToCsvRow()).Count != ExpectedHeader.Length)
                return "빈 목록이 CSV 열을 줄였다";
            return null;
        }

        private static string TestEscaping()
        {
            SpinTelemetryRecord record = Sentinel();
            // 계약 라벨은 사람이 쓰는 문자열이라 쉼표·따옴표가 언제든 들어올 수 있다.
            record.Contract = "위험 \"계약\", 흡수체\\증식체";
            record.InitialBoard = "줄\n바꿈";
            // 목록형 원소에도 같은 일이 일어날 수 있다. 접힌 한 칸이 열을 밀면
            // 그 뒤의 모든 열이 뜻을 잃는다 — 목록을 넣으면서 새로 생긴 위험이다.
            record.CascadeBoards = new[] { "쉼표,포함", "따옴표\"포함" };

            List<string> cells = SplitCsvLine(record.ToCsvRow());
            if (cells.Count != ExpectedHeader.Length)
                return $"이스케이프 후 열이 {cells.Count}개로 밀렸다";
            if (cells[5] != record.Contract) return $"CSV 계약 열이 '{cells[5]}'";
            if (!record.ToCsvRow().Contains("\"\"계약\"\""))
                return "CSV 가 따옴표를 두 번 적지 않았다";

            int boardsColumn = Array.IndexOf(SpinTelemetryRecord.CsvHeader, "cascadeBoards");
            if (cells[boardsColumn] != "쉼표,포함;따옴표\"포함")
                return $"목록형 칸이 '{cells[boardsColumn]}' — 쉼표·따옴표가 살아 돌아오지 않았다";

            string json = record.ToJsonLine();
            List<string> keys = ParseTopLevelKeys(json, out string error);
            if (error != null) return $"이스케이프된 JSON 이 깨졌다: {error} — {json}";
            if (keys.Count != ExpectedHeader.Length)
                return $"이스케이프 후 JSON 키 {keys.Count}개";
            if (!json.Contains("\\\"계약\\\"")) return $"JSON 이 따옴표를 이스케이프하지 않았다: {json}";
            if (!json.Contains("흡수체\\\\증식체")) return $"JSON 이 역슬래시를 이스케이프하지 않았다: {json}";
            if (!json.Contains("줄\\n바꿈")) return $"JSON 이 줄바꿈을 이스케이프하지 않았다: {json}";
            if (!json.Contains("[\"쉼표,포함\",\"따옴표\\\"포함\"]"))
                return $"JSON 배열 원소가 제 모습을 잃었다: {json}";
            if (json.IndexOf('\n') >= 0) return "JSONL 줄이 두 줄로 쪼개졌다";
            return null;
        }

        private static string TestNonFiniteNumbers()
        {
            SpinTelemetryRecord record = Sentinel();
            record.GrossPower = float.NaN;
            record.NetPower = float.PositiveInfinity;
            // 재지 못한 프레임 타임의 기본값이 NaN 이다. 그 값이 JSONL 을 깨뜨리면
            // 표본기가 없는 모든 런의 로그가 통째로 파싱 불가가 된다.
            record.FrameTimeMs = float.NaN;

            string json = record.ToJsonLine();
            ParseTopLevelKeys(json, out string error);
            if (error != null) return $"NaN 이 JSON 을 깨뜨렸다: {error} — {json}";
            if (!json.Contains("\"grossPower\":null")) return $"NaN 이 null 로 낮춰지지 않았다: {json}";
            if (!json.Contains("\"frameTimeMs\":null")) return $"frameTimeMs NaN 이 null 이 아니다: {json}";

            // CSV 쪽에는 사실이 남아야 한다 — 0으로 적으면 로그가 거짓말을 한다.
            string[] values = record.ToCsvValues();
            if (values[13] != "NaN") return $"CSV grossPower 가 '{values[13]}'";
            if (values[15] != "Infinity") return $"CSV netPower 가 '{values[15]}'";
            int frameColumn = Array.IndexOf(SpinTelemetryRecord.CsvHeader, "frameTimeMs");
            if (values[frameColumn] != "NaN") return $"CSV frameTimeMs 가 '{values[frameColumn]}'";
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

        // ── §16.2 에서 빠져 있던 다섯 항목 ───────────────────────────────────

        /// <summary>
        /// 처음과 끝만 남기면 "3단계에서 무엇이 터져 4단계가 열렸는가"를 되짚을 수 없다.
        /// 단계 수와 보드 수가 어긋나면 사슬 어딘가가 통째로 사라진 것이다.
        /// </summary>
        private static string TestCascadeBoardsPerStep()
        {
            int deepest = 0;
            int seen = 0;

            // 시드 하나에 걸지 않는다. 특정 시드의 런이 전부 1단계로 끝나면 검사가
            // 통과하는 것이 아니라 **아무것도 보지 못한 채** 통과한다.
            for (int seed = 1337; seed < 1347; seed++)
            {
                foreach (SpinTelemetryRecord r in RecordRun(seed))
                {
                    seen++;
                    if (r.CascadeBoards == null)
                        return $"시드 {seed} {r.Floor}층 스핀 {r.SpinIndex}: " +
                               "캐스케이드 보드가 null — 빈 배열이어야 한다";
                    if (r.CascadeBoards.Length != r.CascadeDepth)
                        return $"시드 {seed} {r.Floor}층 스핀 {r.SpinIndex}: " +
                               $"단계별 보드 {r.CascadeBoards.Length}개, 연쇄 깊이 {r.CascadeDepth}";
                    if (r.CascadeDepth > deepest) deepest = r.CascadeDepth;

                    foreach (string board in r.CascadeBoards)
                    {
                        if (string.IsNullOrEmpty(board))
                            return $"시드 {seed} {r.Floor}층 스핀 {r.SpinIndex}: 빈 보드 문자열이 섞였다";
                        if (board.IndexOf(SpinTelemetryRecord.ListSeparator, StringComparison.Ordinal) >= 0)
                            return $"보드 표기가 구분자 '{SpinTelemetryRecord.ListSeparator}' 를 담고 있다 — " +
                                   $"CSV 한 칸이 무너진다: {board}";
                    }

                    string last = r.CascadeBoards[r.CascadeBoards.Length - 1];
                    if (last != r.FinalBoard)
                        return $"시드 {seed} {r.Floor}층 스핀 {r.SpinIndex}: 마지막 단계 보드 '{last}' 가 " +
                               $"최종 보드 '{r.FinalBoard}' 와 다르다 — 사슬이 끊겼다";
                }
            }

            if (seen == 0) return "열 개 시드에서 기록이 하나도 나오지 않았다";
            if (deepest < 2)
                return $"스핀 {seen}회가 전부 1단계로 끝났다 — 단계별 보드를 실제로 본 적이 없다";
            return null;
        }

        /// <summary>
        /// 패턴 이름의 서열. **구현의 열거형 값을 빌리지 않는다 — 빌리면 검사가 사라진다.**
        ///
        /// 2026-08-09: `Duo`(쌍, 2.5×)와 `Cross`(십자, 5.0×)를 넣었다.
        /// 자리는 배수 순서를 따른다 — Line 2.0 &lt; **Duo 2.5** &lt; Cluster 3.0 &lt;
        /// **Cross 5.0** &lt; FullBoard 10.0.
        ///
        /// ⚠ 이 배열을 손으로 유지하는 것이 이 검사의 존재 이유다. 열거형을
        /// `Enum.GetNames` 로 가져오면 구현이 서열을 바꿔도 검사가 따라 바뀌어
        /// **아무것도 못 잡는다.** 그러니 새 패턴이 생길 때마다 여기도 손으로 고친다 —
        /// 그 수고가 비용이 아니라 이 검사의 값이다.
        /// </summary>
        private static readonly string[] PatternRank =
            { "None", "Scattered", "Line", "Duo", "Cluster", "Cross", "FullBoard" };

        /// <summary>
        /// `purifyCount`(개수)와 `bestPattern`(최고 하나)만으로는 "무엇이 먼저 터졌는가"가
        /// 남지 않는다. 순서 목록이 그 둘과 다른 말을 하면 셋 중 하나는 거짓이다.
        /// </summary>
        private static string TestActivationOrder()
        {
            int totalPurifies = 0;
            int seen = 0;

            for (int seed = 1337; seed < 1347; seed++)
            foreach (SpinTelemetryRecord r in RecordRun(seed))
            {
                seen++;
                if (r.ActivationOrder == null)
                    return $"{r.Floor}층 스핀 {r.SpinIndex}: 발동 순서가 null — 빈 배열이어야 한다";

                int purifies = 0, souls = 0, bestRank = 0, lastDepth = 0;
                foreach (string entry in r.ActivationOrder)
                {
                    int colon = entry.IndexOf(':');
                    if (colon <= 0) return $"발동 항목에 깊이가 없다: '{entry}'";
                    if (!int.TryParse(entry.Substring(0, colon), NumberStyles.Integer,
                                      CultureInfo.InvariantCulture, out int depth))
                        return $"깊이를 읽을 수 없다: '{entry}'";
                    if (depth < 1 || depth > r.CascadeDepth)
                        return $"깊이 {depth} 가 1..{r.CascadeDepth} 밖이다: '{entry}'";
                    if (depth < lastDepth)
                        return $"발동 순서가 뒤로 갔다 ({lastDepth} → {depth}): '{entry}' — 순서가 보존되지 않았다";
                    lastDepth = depth;

                    string body = entry.Substring(colon + 1);
                    int star = body.LastIndexOf('*');
                    if (star <= 0) return $"개수 표기가 없다: '{entry}'";
                    if (!int.TryParse(body.Substring(star + 1), NumberStyles.Integer,
                                      CultureInfo.InvariantCulture, out int count))
                        return $"개수를 읽을 수 없다: '{entry}'";
                    if (count <= 0) return $"개수가 {count} 인 발동이 기록됐다: '{entry}'";

                    string head = body.Substring(0, star);
                    if (head == "Soul") { souls += count; continue; }

                    int slash = head.IndexOf('/');
                    if (slash <= 0) return $"정화 항목이 종류/패턴 형태가 아니다: '{entry}'";
                    string pattern = head.Substring(slash + 1);
                    int rank = Array.IndexOf(PatternRank, pattern);
                    if (rank < 0) return $"모르는 패턴 이름: '{pattern}' ({entry})";
                    if (rank > bestRank) bestRank = rank;
                    purifies++;
                }

                if (purifies != r.PurifyCount)
                    return $"{r.Floor}층 스핀 {r.SpinIndex}: 순서 목록의 정화 {purifies}건, purifyCount {r.PurifyCount}";
                if (souls != r.NormalSouls)
                    return $"{r.Floor}층 스핀 {r.SpinIndex}: 순서 목록의 영혼 {souls}개, normalSouls {r.NormalSouls}";
                if (PatternRank[bestRank] != r.BestPattern)
                    return $"{r.Floor}층 스핀 {r.SpinIndex}: 순서 목록의 최고 패턴 {PatternRank[bestRank]}, " +
                           $"bestPattern {r.BestPattern}";
                totalPurifies += purifies;
            }

            if (seen == 0) return "열 개 시드에서 기록이 하나도 나오지 않았다";
            if (totalPurifies == 0)
                return $"스핀 {seen}회 동안 정화가 한 번도 일어나지 않았다 — 검사가 아무것도 보지 못했다";
            return null;
        }

        /// <summary>
        /// 잔류 손실만 있으면 "왜 그만큼 깎였는가"를 설명하지 못한다. 개수와 손실이 서로
        /// 모순되는 조합(손실은 있는데 흡수체가 0)은 둘 중 하나가 틀렸다는 뜻이다.
        /// </summary>
        private static string TestResidualCounts()
        {
            bool sawResidual = false;
            int seen = 0;

            for (int seed = 1337; seed < 1347; seed++)
            foreach (SpinTelemetryRecord r in RecordRun(seed))
            {
                seen++;
                if (r.ResidualAbsorbers < 0 || r.ResidualProliferators < 0)
                    return $"시드 {seed} {r.Floor}층 스핀 {r.SpinIndex}: 잔류 개수가 음수 " +
                           $"({r.ResidualAbsorbers}, {r.ResidualProliferators})";
                if (r.ResidualAbsorbers + r.ResidualProliferators > 9)
                    return $"시드 {seed} {r.Floor}층 스핀 {r.SpinIndex}: 잔류가 아홉 칸을 넘는다 " +
                           $"({r.ResidualAbsorbers} + {r.ResidualProliferators})";
                if (r.ResidualLoss > 0f && r.ResidualAbsorbers == 0)
                    return $"시드 {seed} {r.Floor}층 스핀 {r.SpinIndex}: 저장 전력이 {r.ResidualLoss} 깎였는데 " +
                           "남은 흡수체가 0이다 — 손실과 개수가 다른 이야기를 한다";
                if (r.ResidualAbsorbers > 0 || r.ResidualProliferators > 0) sawResidual = true;
            }

            if (seen == 0) return "열 개 시드에서 기록이 하나도 나오지 않았다";
            if (!sawResidual)
                return $"스핀 {seen}회 동안 잔류가 한 번도 없었다 — 개수가 늘 0이라 검사가 무의미하다";
            return null;
        }

        private sealed class FakeRiskSource : ITelemetryRiskSource
        {
            public bool Available = true;
            public RiskLevel Level = RiskLevel.Critical;
            public int Calls;

            public bool TryGetRiskLevel(out RiskLevel level)
            {
                Calls++;
                level = Level;
                return Available;
            }
        }

        /// <summary>
        /// 아무도 알려 주지 않았을 때 Stable로 채우면 "안정이었다"는 거짓 기록이 남는다.
        /// 헤드리스에는 `RiskEventBridge`(MonoBehaviour)가 없으므로 이것이 기본 상황이다.
        /// </summary>
        private static string TestRiskUnknownWithoutSource()
        {
            var run = new RunSession(1337);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run));
            Drive(run);
            recorder.Detach();

            if (sink.Records.Count == 0) return "기록이 비어 있다";
            foreach (SpinTelemetryRecord r in sink.Records)
                if (r.RiskLevel != SpinTelemetryRecord.Unknown)
                    return $"{r.Floor}층 스핀 {r.SpinIndex}: 아무도 알려 주지 않았는데 " +
                           $"위험 단계가 '{r.RiskLevel}' 로 적혔다";
            return null;
        }

        /// <summary>사건 폴백과 출처 우선, 둘 다 실제로 기록에 도달하는지 본다.</summary>
        private static string TestRiskFlowsIn()
        {
            // ① 사건만 있는 경우 — 마지막으로 알려진 단계가 남아야 한다.
            var eventRun = new RunSession(1337);
            var eventSink = new InMemoryTelemetrySink();
            var eventRecorder = new TelemetryRecorder(eventRun.Events, eventSink,
                new RunSessionTelemetryContext(eventRun));
            eventRun.Events.Publish(GameEventKind.RiskLevelChanged, eventRun.CurrentFloor, -1,
                (int)RiskLevel.Strain, 0f, null, RiskLevel.Stable);
            Drive(eventRun);
            eventRecorder.Detach();

            if (eventSink.Records.Count == 0) return "사건 경로: 기록이 비어 있다";
            foreach (SpinTelemetryRecord r in eventSink.Records)
                if (r.RiskLevel != "Strain")
                    return $"사건 경로: 위험 단계가 '{r.RiskLevel}', 기대 'Strain' — " +
                           "RiskLevelChanged 를 보고도 기록이 따라가지 않았다";

            // ② 출처가 있으면 사건보다 우선한다 — 사건은 전이만 알리므로 늘 뒤처진다.
            var sourceRun = new RunSession(1337);
            var sourceSink = new InMemoryTelemetrySink();
            var source = new FakeRiskSource { Level = RiskLevel.Collapse };
            var sourceRecorder = new TelemetryRecorder(sourceRun.Events, sourceSink,
                new RunSessionTelemetryContext(sourceRun))
            {
                RiskSource = source,
            };
            sourceRun.Events.Publish(GameEventKind.RiskLevelChanged, sourceRun.CurrentFloor, -1,
                (int)RiskLevel.Strain, 0f, null, RiskLevel.Stable);
            Drive(sourceRun);
            sourceRecorder.Detach();

            if (sourceSink.Records.Count == 0) return "출처 경로: 기록이 비어 있다";
            if (source.Calls == 0) return "출처를 붙였는데 한 번도 묻지 않았다";
            foreach (SpinTelemetryRecord r in sourceSink.Records)
                if (r.RiskLevel != "Collapse")
                    return $"출처 경로: 위험 단계가 '{r.RiskLevel}', 기대 'Collapse'";

            // ③ 출처가 "모른다"고 답하면 사건 폴백으로 내려앉는다.
            var fallbackRun = new RunSession(1337);
            var fallbackSink = new InMemoryTelemetrySink();
            var fallbackRecorder = new TelemetryRecorder(fallbackRun.Events, fallbackSink,
                new RunSessionTelemetryContext(fallbackRun))
            {
                RiskSource = new FakeRiskSource { Available = false },
            };
            fallbackRun.Events.Publish(GameEventKind.RiskLevelChanged, fallbackRun.CurrentFloor, -1,
                (int)RiskLevel.Critical, 0f, null, RiskLevel.Stable);
            Drive(fallbackRun);
            fallbackRecorder.Detach();

            if (fallbackSink.Records.Count == 0) return "폴백 경로: 기록이 비어 있다";
            foreach (SpinTelemetryRecord r in fallbackSink.Records)
                if (r.RiskLevel != "Critical")
                    return $"폴백 경로: 위험 단계가 '{r.RiskLevel}', 기대 'Critical'";
            return null;
        }

        private sealed class FakePerformanceSampler : ITelemetryPerformanceSampler
        {
            public bool Available = true;
            public float FrameTimeMs = 12.5f;
            public long GcBytes = 4096L;
            public int Begins;
            public int Ends;

            public void BeginSpin() { Begins++; }

            public bool TryEndSpin(out float frameTimeMs, out long gcAllocBytes)
            {
                Ends++;
                frameTimeMs = FrameTimeMs;
                gcAllocBytes = GcBytes;
                return Available;
            }
        }

        private static string TestPerfUnknownWithoutSampler()
        {
            var run = new RunSession(1337);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run));
            Drive(run);
            recorder.Detach();

            if (sink.Records.Count == 0) return "기록이 비어 있다";
            foreach (SpinTelemetryRecord r in sink.Records)
            {
                // 0 ms·0 B 는 존재할 법한 값이라 "빨랐다"로 읽힌다. 그 오독은 되돌릴 수 없다.
                if (!float.IsNaN(r.FrameTimeMs))
                    return $"{r.Floor}층 스핀 {r.SpinIndex}: 재지 않았는데 프레임 타임이 {r.FrameTimeMs}";
                if (r.GcAllocBytes != SpinTelemetryRecord.UnknownBytes)
                    return $"{r.Floor}층 스핀 {r.SpinIndex}: 재지 않았는데 GC 가 {r.GcAllocBytes}";
            }
            return null;
        }

        private static string TestPerfSamplerIsPulledPerSpin()
        {
            var run = new RunSession(1337);
            var sink = new InMemoryTelemetrySink();
            var sampler = new FakePerformanceSampler { FrameTimeMs = 16.7f, GcBytes = 2048L };
            var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run))
            {
                PerformanceSampler = sampler,
            };
            Drive(run);
            recorder.Detach();

            int records = sink.Records.Count;
            if (records == 0) return "기록이 비어 있다";
            if (sampler.Ends != records)
                return $"구간을 {sampler.Ends}번 닫았는데 레코드는 {records}개다";
            if (sampler.Begins != records)
                return $"구간을 {sampler.Begins}번 열고 {records}번 닫았다 — " +
                       "SpinStarted 를 놓쳤거나 두 번 셌다";

            foreach (SpinTelemetryRecord r in sink.Records)
            {
                if (Math.Abs(r.FrameTimeMs - 16.7f) > 0.001f)
                    return $"프레임 타임이 {r.FrameTimeMs}, 기대 16.7";
                if (r.GcAllocBytes != 2048L) return $"GC 가 {r.GcAllocBytes}, 기대 2048";
            }

            // 표본기가 "못 쟀다"고 답하면 그 사실이 그대로 남아야 한다.
            var blindRun = new RunSession(1337);
            var blindSink = new InMemoryTelemetrySink();
            var blindRecorder = new TelemetryRecorder(blindRun.Events, blindSink,
                new RunSessionTelemetryContext(blindRun))
            {
                PerformanceSampler = new FakePerformanceSampler { Available = false },
            };
            Drive(blindRun);
            blindRecorder.Detach();

            foreach (SpinTelemetryRecord r in blindSink.Records)
            {
                if (!float.IsNaN(r.FrameTimeMs))
                    return $"표본기가 실패를 알렸는데 프레임 타임이 {r.FrameTimeMs} 로 남았다";
                if (r.GcAllocBytes != SpinTelemetryRecord.UnknownBytes)
                    return $"표본기가 실패를 알렸는데 GC 가 {r.GcAllocBytes} 로 남았다";
            }
            return null;
        }

        /// <summary>
        /// "아무것도 안 실었다"와 "적재를 읽지 못했다"는 다른 사실이다. 같은 값으로 적으면
        /// 빈 적재로 돈 런과 문맥이 끊긴 런을 나중에 구분할 수 없다.
        /// </summary>
        private static string TestLoadoutMarkers()
        {
            // 문맥 없음 → 모른다.
            var blindRun = new RunSession(1337);
            var blindSink = new InMemoryTelemetrySink();
            var blindRecorder = new TelemetryRecorder(blindRun.Events, blindSink);
            Drive(blindRun);
            blindRecorder.Detach();

            if (blindSink.Records.Count == 0) return "문맥 없는 런의 기록이 비어 있다";
            foreach (SpinTelemetryRecord r in blindSink.Records)
                if (r.Loadout != SpinTelemetryRecord.Unknown)
                    return $"문맥이 없는데 적재가 '{r.Loadout}' 로 적혔다";

            // 문맥 있음 + 아무것도 싣지 않음 → 비었다.
            var emptyRun = new RunSession(1337);
            var emptySink = new InMemoryTelemetrySink();
            var emptyRecorder = new TelemetryRecorder(emptyRun.Events, emptySink,
                new RunSessionTelemetryContext(emptyRun));
            Drive(emptyRun);   // Drive 는 후보를 하나도 싣지 않고 문을 닫는다
            emptyRecorder.Detach();

            if (emptySink.Records.Count == 0) return "빈 적재 런의 기록이 비어 있다";
            if (emptySink.Records[0].Loadout != SpinTelemetryRecord.NoneMarker)
                return $"아무것도 안 실었는데 적재가 '{emptySink.Records[0].Loadout}'";
            return null;
        }

        /// <summary>
        /// §16.2가 요구하는 것은 "승객·부품 **발동**"이지 탑승자 명단이 아니다.
        /// 아이디만 있고 효과가 없으면 "이 스핀의 규칙이 왜 달랐는가"를 설명하지 못한다.
        /// </summary>
        private static string TestLoadoutIsRecorded()
        {
            // 적재 층은 2층·5층이다(`FloorPlan.OffersBuildReward`). 시드에 따라 거기까지
            // 못 가는 런이 있으므로 몇 개를 훑는다 — 여기서 재는 것은 밸런스가 아니다.
            for (int seed = 1337; seed < 1367; seed++)
            {
                var run = new RunSession(seed);
                var sink = new InMemoryTelemetrySink();
                var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run));
                bool boarded = DriveTakingOffers(run);
                recorder.Detach();
                if (!boarded) continue;

                bool sawLoaded = false;
                foreach (SpinTelemetryRecord r in sink.Records)
                {
                    if (r.Loadout == SpinTelemetryRecord.Unknown)
                        return $"시드 {seed}: 문맥을 붙였는데 적재를 모른다고 적었다";
                    if (r.Loadout == SpinTelemetryRecord.NoneMarker) continue;

                    sawLoaded = true;
                    if (r.Loadout.IndexOf('[') < 0 && r.Loadout.IndexOf(']') < 0)
                    {
                        // 효과가 하나도 없는 승객(짐꾼 계열)만 실린 경우는 정상이다.
                        // 대괄호가 전혀 없어도 아이디는 남아 있어야 한다.
                        if (r.Loadout.Trim().Length == 0)
                            return $"시드 {seed}: 실었는데 적재 요약이 공백이다";
                    }
                    if (r.Loadout.IndexOf(',') >= 0)
                        return $"시드 {seed}: 적재 요약에 쉼표가 들어 있다 — " +
                               $"CSV 가 따옴표로 감싸야 하는 값이다: {r.Loadout}";
                }

                if (!sawLoaded) continue;
                return null;
            }
            return "30개 시드 안에 적재 층까지 오른 런이 없다 — 자동 진행이나 밸런스가 깨졌다";
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
                    if (keys.Count != ExpectedHeader.Length)
                        return $"파일의 JSON 키 {keys.Count}개, 기대 {ExpectedHeader.Length}";
                }

                string[] csvLines = ReadNonEmptyLines(sink.CsvPath);
                if (csvLines.Length != 3) return $"csv 줄 수 {csvLines.Length}, 기대 3(헤더+2)";
                if (csvLines[0] != SpinTelemetryRecord.CsvHeaderLine)
                    return $"csv 첫 줄이 헤더가 아니다: {csvLines[0]}";
                if (SplitCsvLine(csvLines[1]).Count != ExpectedHeader.Length)
                    return $"csv 데이터 줄의 열 수가 {ExpectedHeader.Length} 가 아니다";

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

        /// <summary>
        /// 한 런을 끝까지 돌려 레코드를 **구조체 그대로** 돌려준다.
        /// <see cref="RecordRows"/>가 CSV 문자열을 내는 것과 달리 여기는 필드를 직접 보게 한다 —
        /// 문자열로 접으면 목록형 필드의 원소를 검사할 수 없다.
        /// </summary>
        private static IReadOnlyList<SpinTelemetryRecord> RecordRun(int seed)
        {
            var run = new RunSession(seed);
            var sink = new InMemoryTelemetrySink();
            var recorder = new TelemetryRecorder(run.Events, sink, new RunSessionTelemetryContext(run));
            Drive(run);
            recorder.Detach();
            return sink.Records;
        }

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

        /// <summary>
        /// <see cref="Drive"/>와 같지만 적재 단계에서 첫 후보를 하나 싣는다.
        /// 하나만 싣는 이유: 여럿을 실으면 과적이 되어 층이 늘 실패하고, 그러면 정작
        /// 보고 싶은 "승객이 실린 상태의 스핀 기록"이 남지 않는다.
        /// </summary>
        /// <returns>한 번이라도 실었는가. 못 실었다면 그 시드는 검사 대상이 아니다.</returns>
        private static bool DriveTakingOffers(RunSession run)
        {
            bool boarded = false;
            int guard = 0;
            while (!run.IsComplete && !run.IsFailed && guard++ < 400)
            {
                FloorSession floor = run.Current;
                if (floor == null) break;

                if (floor.Phase == FloorPhase.Boarding)
                {
                    if (floor.BuildOffers.Count > 0 && run.TakeBuildOffer(0)) boarded = true;
                    if (!run.FinishBoarding()) break;
                }
                if (floor.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) break;

                int inner = 0;
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && inner++ < 40)
                {
                    int before = floor.SpinsUsed;
                    run.Spin();
                    if (floor.SpinsUsed == before) break;
                }

                if (floor.CanBank) { if (run.Bank() == null) break; }
                else if (floor.SpinsRemaining == 0) { if (run.ForceResolve() == null) break; }
                else break;
            }
            return boarded;
        }

        /// <summary><see cref="Drive"/>와 같지만 확정 전에 한 번은 과수확 레버를 당긴다.</summary>
        private static int DrivePushingLuck(RunSession run)
        {
            // 과수확 열쇠를 먼저 싣는다 (2026-08-09).
            //
            // 과수확이 「요구 전력 100% 를 넘기면 그냥 열린다」에서
            // 「전력 **그리고** 열쇠」로 바뀌었다(`FloorSession.IsOverharvestUnlocked`).
            // 이 헬퍼는 빈 적재로 시작하는 `RunSession` 을 받아 추가 스핀이 일어나기를
            // 기다리는데, 열쇠가 없으면 **50개 시드 전부에서 한 번도 안 일어난다.**
            //
            // 게이트를 느슨하게 하는 대신 테스트가 조건을 갖추게 한다 — 이 테스트가
            // 검증하려는 것은 「과수확으로 산 스핀이 추가 스핀으로 표시되는가」이지
            // 「과수확이 공짜인가」가 아니다. 그 질문은 그대로 유효하다.
            if (run.Loadout != null && !run.Loadout.HasEffect(Build.BuildEffectKind.OverharvestUnlock))
                run.Loadout.Add(Build.BuildCatalog.ById("PRT_OVERHARVEST_TRANSFORMER"));

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
