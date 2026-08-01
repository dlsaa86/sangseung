using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ascend.Prototype.Telemetry
{
    /// <summary>
    /// 레코드를 `Logs/telemetry/run_&lt;시드&gt;_&lt;n&gt;.jsonl` 과 같은 이름의 `.csv` 두 벌로 쓴다.
    ///
    /// 왜 두 벌인가: JSONL은 스크립트가 읽기 좋고 CSV는 사람이 표 계산기로 연다.
    /// 하나만 남기면 반드시 다른 쪽으로 변환하는 일회용 스크립트가 생기고, 그 스크립트가
    /// 필드 순서를 자기 나름대로 해석한다 — 같은 로그의 두 번째 정의가 생긴다는 뜻이다.
    /// 둘 다 <see cref="SpinTelemetryRecord"/> 하나에서 나오면 그 일이 없다.
    ///
    /// 왜 `&lt;n&gt;`이 붙는가: 같은 시드를 두 번 돌리는 것이 정상 작업(결정론 확인)이라
    /// 이름이 겹친다. 덮어쓰면 비교할 대상이 사라지므로 비어 있는 번호를 찾아 쓴다.
    ///
    /// `UnityEditor`를 참조하지 않고 `System.IO`만 쓴다 — 빌드에도 들어가야 하고
    /// (`MASTER_PRD.md` §10은 개발 편의 기능이 아니라 결과 설명 수단으로 요구한다),
    /// 씬 없는 테스트에서도 생성할 수 있어야 한다.
    /// </summary>
    public sealed class TelemetryFileSink : ITelemetrySink
    {
        /// <summary>기본 출력 폴더(작업 디렉터리 기준). `.gitignore` 대상인 `Logs/` 아래다.</summary>
        public const string DefaultRelativeDirectory = "Logs/telemetry";

        /// <summary>
        /// 이 개수마다 자동으로 디스크에 민다. 런 종료까지 버퍼에 들고 있으면 사고로
        /// 프로세스가 죽었을 때 **사고 직전 기록이 통째로 사라진다** — 사고 기록기가
        /// 가장 필요한 순간이 바로 그 순간이다.
        /// </summary>
        public int AutoFlushEvery { get; set; } = 8;

        private readonly StringBuilder _jsonl = new StringBuilder(4096);
        private readonly StringBuilder _csv = new StringBuilder(4096);

        // BOM 없이 쓴다. JSONL 앞에 BOM이 붙으면 첫 줄만 파서가 거부하는 일이 잦고,
        // 원인이 눈에 보이지 않아 찾는 데 오래 걸린다.
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private int _pending;
        private bool _headerWritten;

        public TelemetryFileSink(int runSeed) : this(runSeed, null) { }

        /// <param name="directory">
        /// null이면 <see cref="DefaultRelativeDirectory"/>. 경로를 주입받는 이유는
        /// 테스트가 임시 폴더를 쓰고 프로젝트 트리를 더럽히지 않기 위해서다.
        /// </param>
        public TelemetryFileSink(int runSeed, string directory)
        {
            RunSeed = runSeed;
            Directory = string.IsNullOrEmpty(directory)
                ? Path.Combine(System.IO.Directory.GetCurrentDirectory(),
                    DefaultRelativeDirectory.Replace('/', Path.DirectorySeparatorChar))
                : directory;

            int index = NextFreeIndex(Directory, runSeed);
            string stem = "run_" + runSeed.ToString(CultureInfo.InvariantCulture) + "_" +
                          index.ToString(CultureInfo.InvariantCulture);
            JsonlPath = Path.Combine(Directory, stem + ".jsonl");
            CsvPath = Path.Combine(Directory, stem + ".csv");
        }

        public int RunSeed { get; }
        public string Directory { get; }
        public string JsonlPath { get; }
        public string CsvPath { get; }

        /// <summary>받은 레코드 수. 디스크에 나갔는지와는 별개다.</summary>
        public int WrittenCount { get; private set; }

        /// <summary>디스크에 실제로 나간 레코드 수.</summary>
        public int FlushedCount { get; private set; }

        /// <summary>마지막 IO 실패. 조용히 삼키지 않기 위해 남긴다(`GameEventBus.LastError`와 같은 규약).</summary>
        public Exception LastError { get; private set; }

        public int ErrorCount { get; private set; }

        /// <summary>
        /// IO가 실패했을 때 부를 곳. 이 클래스는 `Debug.LogWarning`을 직접 부르지 않는다 —
        /// 순수 C#이라야 헤드리스에서 돌기 때문이고, 로그 채널은 붙이는 쪽이 정한다
        /// (`SpinEngine.CascadeCapReached`와 같은 방식).
        /// </summary>
        public Action<string> ErrorReported { get; set; }

        public void Write(in SpinTelemetryRecord record)
        {
            _jsonl.Append(record.ToJsonLine()).Append('\n');
            _csv.Append(record.ToCsvRow()).Append('\n');
            WrittenCount++;
            _pending++;
            if (AutoFlushEvery > 0 && _pending >= AutoFlushEvery) Flush();
        }

        public void Flush()
        {
            // 레코드가 하나도 없으면 파일을 만들지 않는다. 헤더만 있는 빈 파일이 쌓이면
            // "이 런은 기록됐다"와 "이 런은 스핀이 없었다"가 구분되지 않는다.
            if (_pending == 0) return;

            try
            {
                System.IO.Directory.CreateDirectory(Directory);

                if (!_headerWritten)
                {
                    File.AppendAllText(CsvPath, SpinTelemetryRecord.CsvHeaderLine + "\n", Utf8NoBom);
                    _headerWritten = true;
                }

                if (_jsonl.Length > 0)
                {
                    File.AppendAllText(JsonlPath, _jsonl.ToString(), Utf8NoBom);
                    _jsonl.Length = 0;
                }
                if (_csv.Length > 0)
                {
                    File.AppendAllText(CsvPath, _csv.ToString(), Utf8NoBom);
                    _csv.Length = 0;
                }

                FlushedCount += _pending;
                _pending = 0;
            }
            catch (Exception e)
            {
                // 버퍼를 비우지 않는다. 다음 Flush에서 다시 시도할 수 있고, 실패한 기록을
                // 버리면 "쓴 줄 알았는데 없다"가 되어 사고 조사가 불가능해진다.
                LastError = e;
                ErrorCount++;
                ErrorReported?.Invoke($"[상승] 텔레메트리 기록 실패 ({JsonlPath}): {e.Message}");
            }
        }

        /// <summary>
        /// 같은 시드의 앞선 런을 덮어쓰지 않는 첫 번호. 폴더를 못 읽으면 0으로 시작한다 —
        /// 번호를 못 정하는 것이 기록을 포기할 이유는 되지 않는다.
        /// </summary>
        private static int NextFreeIndex(string directory, int runSeed)
        {
            try
            {
                for (int n = 0; n < 10000; n++)
                {
                    string stem = "run_" + runSeed.ToString(CultureInfo.InvariantCulture) + "_" +
                                  n.ToString(CultureInfo.InvariantCulture);
                    if (!File.Exists(Path.Combine(directory, stem + ".jsonl")) &&
                        !File.Exists(Path.Combine(directory, stem + ".csv")))
                        return n;
                }
            }
            catch (Exception)
            {
                // 경로가 없거나 권한이 없는 상태. 아래 0을 돌려주고 실제 실패는 Flush가 보고한다.
            }
            return 0;
        }
    }
}
