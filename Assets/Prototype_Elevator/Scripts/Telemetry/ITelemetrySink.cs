using System.Collections.Generic;

namespace Ascend.Prototype.Telemetry
{
    /// <summary>
    /// 기록이 흘러가는 곳. 파일이든 메모리든 기록기는 구분하지 않는다.
    ///
    /// 왜 인터페이스인가: 텔레메트리를 검증하려면 런을 실제로 돌려야 하는데, 검증이
    /// 파일을 요구하면 헤드리스 테스트가 디스크·권한·경로에 묶인다. 그러면 검증을
    /// 안 돌리게 되고 결국 "파일이 생기더라"까지만 확인하게 된다.
    /// 인메모리 구현이 있어야 **내용**을 검사할 수 있다.
    /// </summary>
    public interface ITelemetrySink
    {
        /// <summary>레코드 하나를 받는다. 실패해도 예외를 던지지 않는다 — 로그가 런을 죽이면 안 된다.</summary>
        void Write(in SpinTelemetryRecord record);

        /// <summary>버퍼를 밀어낸다. 런 종료 시 기록기가 호출한다.</summary>
        void Flush();
    }

    /// <summary>
    /// 디스크를 건드리지 않는 구현. 테스트와 디버그 패널이 쓴다.
    /// </summary>
    public sealed class InMemoryTelemetrySink : ITelemetrySink
    {
        private readonly List<SpinTelemetryRecord> _records = new List<SpinTelemetryRecord>();

        /// <summary>기록 순서 그대로. 마지막 원소가 가장 최근 스핀이다.</summary>
        public IReadOnlyList<SpinTelemetryRecord> Records => _records;

        /// <summary><see cref="Flush"/>가 몇 번 불렸는가. "런이 끝날 때 정말 내보냈는가"를 검사한다.</summary>
        public int FlushCount { get; private set; }

        public void Write(in SpinTelemetryRecord record) => _records.Add(record);

        public void Flush() => FlushCount++;

        public void Clear()
        {
            _records.Clear();
            FlushCount = 0;
        }
    }
}
