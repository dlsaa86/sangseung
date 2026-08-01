using System;
using System.Text;

namespace Ascend.Prototype.Perf
{
    /// <summary>추세 판정. 값이 아니라 **읽는 법**을 고정한다.</summary>
    public enum MemoryTrendVerdict
    {
        /// <summary>표본이 2개 미만. 판정하지 않았다 — 안정이 아니다.</summary>
        Insufficient = 0,
        Stable = 1,
        Growing = 2,
        Shrinking = 3,
    }

    /// <summary>한 번의 추세 분석 결과. 판정과 그 근거를 함께 들고 다닌다.</summary>
    public struct MemoryTrendResult
    {
        public MemoryTrendVerdict Verdict;
        public int SampleCount;

        public long First;
        public long Last;
        public long Min;
        public long Max;

        /// <summary>첫 표본 대비 마지막 표본. UP-TECH-08 이 이름으로 요구하는 값이다.</summary>
        public long FirstToLastBytes;

        /// <summary>앞 1/4 구간 평균. 표본 하나의 스파이크에 판정이 끌려가지 않게 한다.</summary>
        public double HeadAverage;

        /// <summary>뒤 1/4 구간 평균.</summary>
        public double TailAverage;

        /// <summary>TailAverage − HeadAverage.</summary>
        public double DeltaBytes;

        /// <summary>DeltaBytes / HeadAverage. HeadAverage 가 0 이하면 0.</summary>
        public double RelativeDelta;

        /// <summary>이웃한 표본 사이에서 값이 오른 횟수.</summary>
        public int RisingSteps;

        /// <summary>이웃한 표본 사이에서 값이 내린 횟수.</summary>
        public int FallingSteps;

        /// <summary>표본 하나당 평균 증가분. 층 경계마다 재면 "층당 누적"이 된다.</summary>
        public double PerSampleBytes;

        /// <summary>판정이 단조 증가·감소 규칙으로 결정됐는가. 근거를 숨기지 않기 위한 것이다.</summary>
        public bool DecidedByMonotonicity;

        public string Describe()
        {
            if (Verdict == MemoryTrendVerdict.Insufficient)
                return "표본 " + SampleCount + "개 — 판정하지 않았다(2개 이상 필요). 안정이라는 뜻이 아니다.";

            var sb = new StringBuilder(256);
            sb.Append(VerdictText(Verdict));
            sb.Append(" · 표본 ").Append(SampleCount);
            sb.Append(" · 첫 ").Append(MemoryTrend.FormatBytes(First));
            sb.Append(" → 마지막 ").Append(MemoryTrend.FormatBytes(Last));
            sb.Append(" (").Append(FirstToLastBytes >= 0 ? "+" : "−");
            sb.Append(MemoryTrend.FormatBytes(Math.Abs(FirstToLastBytes))).Append(')');
            sb.Append(" · 구간평균 차 ").Append(DeltaBytes >= 0 ? "+" : "−");
            sb.Append(MemoryTrend.FormatBytes((long)Math.Abs(DeltaBytes)));
            sb.Append(" (").Append((RelativeDelta * 100.0).ToString("0.0")).Append("%)");
            sb.Append(" · 상승 ").Append(RisingSteps).Append('/').Append(Math.Max(0, SampleCount - 1));
            sb.Append(" · 표본당 ").Append(PerSampleBytes >= 0 ? "+" : "−");
            sb.Append(MemoryTrend.FormatBytes((long)Math.Abs(PerSampleBytes)));
            if (DecidedByMonotonicity) sb.Append(" · 단조 변화로 판정");
            return sb.ToString();
        }

        private static string VerdictText(MemoryTrendVerdict v)
        {
            switch (v)
            {
                case MemoryTrendVerdict.Growing: return "증가";
                case MemoryTrendVerdict.Shrinking: return "감소";
                case MemoryTrendVerdict.Stable: return "안정";
                default: return "판정 불가";
            }
        }
    }

    /// <summary>
    /// 메모리 표본에서 추세를 읽는 순수 함수. `MASTER_PRD.md` §13.2 의
    /// "10층 연속 플레이 중 진행 불가 상태 없음"과 짝을 이루는 기술 조건
    /// (UP-TECH-08 "10층 연속 플레이에서 메모리 누적 없음")의 판정부다.
    ///
    /// 왜 MonoBehaviour 에서 떼어냈나: 판정 규칙이 측정기 안에 있으면 씬을 띄우지
    /// 않고는 그 규칙을 검증할 수 없다. 그러면 "누적 없음" 이라는 문장이 무엇을
    /// 뜻하는지 아무도 테스트하지 못한 채로 남는다(`TECH_SPEC.md` §2).
    ///
    /// 판정 규칙은 둘이다.
    ///  ① **단조 변화** — 표본이 한 번도 내려가지 않고 계속 올랐다면, 증가량이
    ///     아무리 작아도 증가로 본다. 층마다 조금씩 새는 누수가 정확히 이 모습이고,
    ///     10층 런에서 작아 보이는 기울기가 100층에서는 작지 않다.
    ///  ② **구간 평균 차** — 앞 1/4 평균과 뒤 1/4 평균을 비교한다. 절대량과 상대량을
    ///     둘 다 넘어야 증가로 본다. GC 타이밍 때문에 표본 하나가 튀는 일은 흔하고,
    ///     그 한 번을 누수로 부르면 이 측정은 곧 무시된다.
    /// </summary>
    public static class MemoryTrend
    {
        /// <summary>이만큼 늘지 않았으면 노이즈로 본다. GC 한 번의 흔들림 폭을 기준으로 잡았다.</summary>
        public const long DefaultAbsoluteToleranceBytes = 4L * 1024L * 1024L;

        /// <summary>시작 값 대비 이 비율 미만이면 노이즈로 본다.</summary>
        public const double DefaultRelativeTolerance = 0.05;

        /// <summary>
        /// 단조 규칙을 적용할 최소 표본 수. 표본이 2~3개면 "한 번도 되돌아가지 않았다"가
        /// 사실상 우연이다 — 층 두 개만 재고 1 B 올랐다고 누수라 부르면 이 측정은
        /// 첫날부터 거짓 경보만 낸다. 층 경계 네 번은 봐야 흐름이라고 부를 수 있다.
        /// </summary>
        public const int MinimumSamplesForMonotonicRule = 4;

        public static MemoryTrendResult Analyze(long[] samples)
        {
            return Analyze(samples, samples != null ? samples.Length : 0,
                           DefaultAbsoluteToleranceBytes, DefaultRelativeTolerance);
        }

        public static MemoryTrendResult Analyze(long[] samples, int count,
                                                long absoluteToleranceBytes, double relativeTolerance)
        {
            var result = new MemoryTrendResult();
            result.Verdict = MemoryTrendVerdict.Insufficient;

            if (samples == null) { result.SampleCount = 0; return result; }
            if (count > samples.Length) count = samples.Length;
            if (count < 0) count = 0;

            result.SampleCount = count;
            if (count < 2) return result;

            result.First = samples[0];
            result.Last = samples[count - 1];
            result.FirstToLastBytes = result.Last - result.First;

            long min = samples[0];
            long max = samples[0];
            int rising = 0;
            int falling = 0;
            for (int i = 1; i < count; i++)
            {
                long value = samples[i];
                if (value < min) min = value;
                if (value > max) max = value;
                if (value > samples[i - 1]) rising++;
                else if (value < samples[i - 1]) falling++;
            }
            result.Min = min;
            result.Max = max;
            result.RisingSteps = rising;
            result.FallingSteps = falling;

            int window = count / 4;
            if (window < 1) window = 1;

            double head = 0.0;
            for (int i = 0; i < window; i++) head += samples[i];
            head /= window;

            double tail = 0.0;
            for (int i = count - window; i < count; i++) tail += samples[i];
            tail /= window;

            result.HeadAverage = head;
            result.TailAverage = tail;
            result.DeltaBytes = tail - head;
            result.RelativeDelta = head > 0.0 ? result.DeltaBytes / head : 0.0;
            result.PerSampleBytes = (double)result.FirstToLastBytes / (count - 1);

            // ① 단조 변화. 한 번도 되돌아가지 않은 흐름은 노이즈가 아니다.
            //    단, 표본이 너무 적으면 우연과 구분되지 않으므로 적용하지 않는다.
            int steps = count - 1;
            if (count >= MinimumSamplesForMonotonicRule)
            {
                if (rising == steps) { result.Verdict = MemoryTrendVerdict.Growing; result.DecidedByMonotonicity = true; return result; }
                if (falling == steps) { result.Verdict = MemoryTrendVerdict.Shrinking; result.DecidedByMonotonicity = true; return result; }
            }

            // ② 구간 평균 차. 절대량과 상대량을 둘 다 넘어야 한다.
            double magnitude = Math.Abs(result.DeltaBytes);
            bool passesAbsolute = magnitude >= absoluteToleranceBytes;
            bool passesRelative = head > 0.0
                ? Math.Abs(result.RelativeDelta) >= relativeTolerance
                : true;   // 시작이 0이면 상대 비교가 의미를 잃는다. 절대량만으로 본다.

            if (passesAbsolute && passesRelative)
                result.Verdict = result.DeltaBytes > 0.0 ? MemoryTrendVerdict.Growing : MemoryTrendVerdict.Shrinking;
            else
                result.Verdict = MemoryTrendVerdict.Stable;

            return result;
        }

        /// <summary>바이트를 사람이 읽는 단위로. 보고서와 테스트 실패 메시지가 함께 쓴다.</summary>
        public static string FormatBytes(long bytes)
        {
            double v = bytes;
            if (Math.Abs(v) < 1024.0) return bytes + " B";
            v /= 1024.0;
            if (Math.Abs(v) < 1024.0) return v.ToString("0.0") + " KB";
            v /= 1024.0;
            if (Math.Abs(v) < 1024.0) return v.ToString("0.00") + " MB";
            v /= 1024.0;
            return v.ToString("0.000") + " GB";
        }
    }
}
