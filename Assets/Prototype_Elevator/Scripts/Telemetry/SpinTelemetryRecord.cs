using System;
using System.Globalization;
using System.Text;

namespace Ascend.Prototype.Telemetry
{
    /// <summary>
    /// 인게임 런의 스핀 하나를 남기는 기록 단위.
    ///
    /// 왜 스핀 단위인가: `MASTER_PRD.md` §10 「사고 기록기와 텔레메트리」가 요구하는
    /// 아홉 항목 — 런 시드·층, 선택 계약, 초기 보드와 캐스케이드, 발동한 정화와 패턴,
    /// 획득/요구 전력, 잔류, 과수확 여부, 무게와 과적, 종료 원인 — 은 전부 "레버를 한 번
    /// 당긴 순간"에 값이 정해진다. 층 단위로 접으면 "몇 번째 스핀에서 무너졌는가"가
    /// 사라져 실패 원인을 되짚을 수 없다. 층 요약은 `FloorRecord`가 이미 하고 있고,
    /// 여기는 그 아래 해상도를 담당한다.
    ///
    /// `Sim/SimRecords.cs`와 겹치지 않는다. 저쪽은 헤드리스 시뮬레이터의 산출물이라
    /// 사람이 실제로 친 런을 한 줄도 기록하지 않는다.
    ///
    /// **20개 필드의 개수와 순서가 계약이다.** CSV 헤더·CSV 행·JSON 키가 셋 다 같은
    /// 순서를 지켜야 하고, `TelemetryTests`가 그것을 검사한다. 중간에 항목을 끼워 넣으면
    /// 과거 로그의 열이 밀려 뜻이 바뀐다(`GameEventKind`가 값을 고정한 것과 같은 이유다).
    ///
    /// UnityEngine에 의존하지 않는다 — 씬 없이 도는 헤드리스 테스트가 이 구조체를
    /// 그대로 만들어 검증할 수 있어야 한다(`TECH_SPEC.md` §2).
    /// </summary>
    [Serializable]
    public struct SpinTelemetryRecord
    {
        /// <summary>런 시드. 이 값 하나로 런 전체를 다시 세울 수 있다.</summary>
        public int RunSeed;

        /// <summary>이 스핀의 파생 시드(<see cref="Spin.SpinSeed.Derive"/>). 단독 재현의 입력이다.</summary>
        public int SpinSeed;

        /// <summary>층 번호.</summary>
        public int Floor;

        /// <summary>그 층의 몇 번째 스핀인가(0부터). 추가 스핀도 이 번호를 이어 쓴다.</summary>
        public int SpinIndex;

        /// <summary>과수확으로 산 추가 스핀인가. 판돈을 치른 스핀과 기본 스핀을 섞으면 승률 통계가 무의미해진다.</summary>
        public bool IsExtraSpin;

        /// <summary>선택 계약 라벨. 계약이 없으면 "계약 없음".</summary>
        public string Contract;

        /// <summary>최초 3×3 보드. <see cref="Spin.SpinBoard.ToString"/> 표기("N A N / P P N / A N A").</summary>
        public string InitialBoard;

        /// <summary>모든 캐스케이드가 끝난 뒤의 보드.</summary>
        public string FinalBoard;

        /// <summary>도달한 연쇄 깊이. 1이면 최초 판정만 있었다.</summary>
        public int CascadeDepth;

        /// <summary>
        /// 하드 캡에 걸려 강제 종료됐는가. 자연 종료와 구분되지 않으면 무한 루프 방지가
        /// 동작했는지 증명할 수 없다(`SpinResolution.CascadeCapReached` 주석과 같은 이유).
        /// </summary>
        public bool CascadeCapped;

        /// <summary>수확한 정상 영혼 개수(모든 캐스케이드 단계 합).</summary>
        public int NormalSouls;

        /// <summary>정화 발동 횟수(모든 단계의 <see cref="Spin.PurifyEvent"/> 개수 합).</summary>
        public int PurifyCount;

        /// <summary>
        /// 이 스핀의 최고 패턴. <see cref="Spin.PatternKind"/>의 **열거형 이름**을 그대로 쓴다
        /// (None/Scattered/Line/Cluster/FullBoard). 표시용 한국어 이름을 쓰지 않는 이유는
        /// 로그가 UI 문구 변경에 딸려 깨지면 안 되기 때문이다.
        /// </summary>
        public string BestPattern;

        /// <summary>잔류 차감 전 전력.</summary>
        public float GrossPower;

        /// <summary>잔류가 깎은 전력. 양수로 적는다.</summary>
        public float ResidualLoss;

        /// <summary>이 스핀의 순 전력. 음수가 될 수 있다.</summary>
        public float NetPower;

        /// <summary>이 스핀 직후의 층 누적 전력.</summary>
        public float PowerAfter;

        /// <summary>이 층의 요구 전력.</summary>
        public float RequiredPower;

        /// <summary>이 스핀 시점의 적재 무게.</summary>
        public float CarriedWeight;

        /// <summary>과적 상태인가. 요구 전력에 1.5배가 걸려 있다는 뜻이다.</summary>
        public bool Overloaded;

        /// <summary>기록 항목 수. 헤더·행·JSON 키가 전부 이 값과 같아야 한다.</summary>
        public const int FieldCount = 20;

        /// <summary>
        /// 전력·무게 표기 형식. 고정 소수점을 쓰지 않는 이유는 0이 "0.0000"으로 부풀어
        /// 파일이 커지기 때문이고, 지수 표기를 피하는 이유는 CSV 소비자가 문자열로
        /// 읽어 버리는 일이 잦기 때문이다.
        /// </summary>
        private const string NumberFormat = "0.####";

        /// <summary>
        /// CSV 헤더. 이 배열의 순서가 <see cref="ToCsvValues"/>·<see cref="ToJsonLine"/>의
        /// 순서와 같아야 한다. 세 곳을 손으로 맞추는 것이 위험해 보이지만, 하나로 묶어
        /// 자동 생성하면 "셋이 어긋났다"를 검사할 수 없어진다 — 검사 대상이 곧 그 어긋남이다.
        /// </summary>
        public static readonly string[] CsvHeader =
        {
            "runSeed",      // 1
            "spinSeed",     // 2
            "floor",        // 3
            "spinIndex",    // 4
            "isExtraSpin",  // 5
            "contract",     // 6
            "initialBoard", // 7
            "finalBoard",   // 8
            "cascadeDepth", // 9
            "cascadeCapped",// 10
            "normalSouls",  // 11
            "purifyCount",  // 12
            "bestPattern",  // 13
            "grossPower",   // 14
            "residualLoss", // 15
            "netPower",     // 16
            "powerAfter",   // 17
            "requiredPower",// 18
            "carriedWeight",// 19
            "overloaded",   // 20
        };

        /// <summary>CSV 파일 첫 줄.</summary>
        public static string CsvHeaderLine => string.Join(",", CsvHeader);

        /// <summary>
        /// 이스케이프하지 않은 원본 값 20개. 열 위치를 검사하는 쪽이 원본을 봐야
        /// "따옴표 때문에 통과했다"는 착시가 생기지 않는다.
        /// </summary>
        public string[] ToCsvValues()
        {
            return new[]
            {
                RunSeed.ToString(CultureInfo.InvariantCulture),
                SpinSeed.ToString(CultureInfo.InvariantCulture),
                Floor.ToString(CultureInfo.InvariantCulture),
                SpinIndex.ToString(CultureInfo.InvariantCulture),
                Boolean(IsExtraSpin),
                Contract ?? string.Empty,
                InitialBoard ?? string.Empty,
                FinalBoard ?? string.Empty,
                CascadeDepth.ToString(CultureInfo.InvariantCulture),
                Boolean(CascadeCapped),
                NormalSouls.ToString(CultureInfo.InvariantCulture),
                PurifyCount.ToString(CultureInfo.InvariantCulture),
                BestPattern ?? string.Empty,
                Number(GrossPower),
                Number(ResidualLoss),
                Number(NetPower),
                Number(PowerAfter),
                Number(RequiredPower),
                Number(CarriedWeight),
                Boolean(Overloaded),
            };
        }

        /// <summary>CSV 한 줄. 값에 쉼표·따옴표·줄바꿈이 있으면 RFC 4180 방식으로 감싼다.</summary>
        public string ToCsvRow()
        {
            string[] values = ToCsvValues();
            var sb = new StringBuilder(160);
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsvField(values[i]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// JSONL 한 줄. `JsonUtility`를 쓰지 않는 이유는 struct 배열·nullable에서 조용히
        /// 빈 객체를 뱉는 함정이 많고, 필드 순서를 보장하지 않아 위 계약을 검사할 수
        /// 없기 때문이다. 키 순서는 <see cref="CsvHeader"/>와 같다.
        /// </summary>
        public string ToJsonLine()
        {
            var sb = new StringBuilder(320);
            sb.Append('{');
            AppendNumber(sb, "runSeed", RunSeed.ToString(CultureInfo.InvariantCulture), true);
            AppendNumber(sb, "spinSeed", SpinSeed.ToString(CultureInfo.InvariantCulture), false);
            AppendNumber(sb, "floor", Floor.ToString(CultureInfo.InvariantCulture), false);
            AppendNumber(sb, "spinIndex", SpinIndex.ToString(CultureInfo.InvariantCulture), false);
            AppendNumber(sb, "isExtraSpin", Boolean(IsExtraSpin), false);
            AppendString(sb, "contract", Contract);
            AppendString(sb, "initialBoard", InitialBoard);
            AppendString(sb, "finalBoard", FinalBoard);
            AppendNumber(sb, "cascadeDepth", CascadeDepth.ToString(CultureInfo.InvariantCulture), false);
            AppendNumber(sb, "cascadeCapped", Boolean(CascadeCapped), false);
            AppendNumber(sb, "normalSouls", NormalSouls.ToString(CultureInfo.InvariantCulture), false);
            AppendNumber(sb, "purifyCount", PurifyCount.ToString(CultureInfo.InvariantCulture), false);
            AppendString(sb, "bestPattern", BestPattern);
            AppendNumber(sb, "grossPower", JsonNumber(GrossPower), false);
            AppendNumber(sb, "residualLoss", JsonNumber(ResidualLoss), false);
            AppendNumber(sb, "netPower", JsonNumber(NetPower), false);
            AppendNumber(sb, "powerAfter", JsonNumber(PowerAfter), false);
            AppendNumber(sb, "requiredPower", JsonNumber(RequiredPower), false);
            AppendNumber(sb, "carriedWeight", JsonNumber(CarriedWeight), false);
            AppendNumber(sb, "overloaded", Boolean(Overloaded), false);
            sb.Append('}');
            return sb.ToString();
        }

        public override string ToString() => ToCsvRow();

        // ── 직렬화 도우미 ────────────────────────────────────────────────────

        private static void AppendNumber(StringBuilder sb, string key, string literal, bool first)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(literal);
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append(",\"").Append(key).Append("\":");
            if (value == null) { sb.Append("null"); return; }
            sb.Append('"').Append(EscapeJsonString(value)).Append('"');
        }

        private static string Boolean(bool value) => value ? "true" : "false";

        private static string Number(float value)
        {
            // 무한대·NaN은 값이 아니라 사고다. 문자열로 그대로 남겨 사람이 알아채게 한다 —
            // 0으로 바꿔 적으면 로그가 "정상이었다"고 거짓말을 한다.
            if (float.IsNaN(value)) return "NaN";
            if (float.IsPositiveInfinity(value)) return "Infinity";
            if (float.IsNegativeInfinity(value)) return "-Infinity";
            return value.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// JSON에는 NaN·Infinity 리터럴이 없다. 그대로 적으면 파일 전체가 파싱 불가가 되어
        /// 정상 줄까지 잃는다. 값을 <c>null</c>로 낮추고, 사실은 CSV 쪽에 남는다.
        /// </summary>
        private static string JsonNumber(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "null";
            return value.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>RFC 4180. 쉼표·따옴표·줄바꿈이 있으면 감싸고 내부 따옴표를 두 번 적는다.</summary>
        public static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            bool needsQuotes = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 ||
                               value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            if (!needsQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>JSON 문자열 본문 이스케이프. 제어 문자는 \uXXXX로 낮춘다.</summary>
        public static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
