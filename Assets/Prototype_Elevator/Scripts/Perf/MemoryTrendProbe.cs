using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Ascend.Prototype.Events;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Perf
{
    /// <summary>
    /// 층 경계마다 메모리를 찍어 두는 측정기. UP-TECH-08 —
    /// "10층 연속 플레이에서 메모리 누적 없음"(`MASTER_PRD.md` §13.2)의 증거를 만든다.
    ///
    /// 왜 층 경계인가: 프레임마다 재면 GC 주기가 신호를 덮는다. 누수는 "층을 넘을
    /// 때마다 무언가가 남는다"는 형태로 나타나므로, 같은 위상(층 시작 / 층 종료)에서
    /// 재야 층 사이를 비교할 수 있다. <see cref="GameEventKind.FloorStarted"/> 와
    /// <see cref="GameEventKind.FloorResolved"/> 를 구독하는 이유가 이것이다.
    ///
    /// 왜 GC.Collect 를 부르지 않는가: 강제 수집은 그 자체로 수십 ms 스파이크이고,
    /// 그러면 이 측정기가 재려는 프레임타임을 스스로 망친다. 대신 같은 위상끼리만
    /// 비교하고, 판정은 <see cref="MemoryTrend"/> 의 노이즈 허용치에 맡긴다.
    ///
    /// 표본은 고정 크기 배열이다. 측정기가 자라면서 할당을 만들면 그 할당이 곧
    /// 측정 결과에 섞여 들어간다.
    /// </summary>
    public sealed class MemoryTrendProbe : MonoBehaviour
    {
        public const string ReportPath = "Logs/memory_trend.txt";

        /// <summary>층 경계 표본 상한. 10층 런은 경계가 20개다. 128 이면 6런 분량이다.</summary>
        private const int Capacity = 128;

        [SerializeField] private RunSessionBehaviour _run;

        [Tooltip("런이 끝날 때(RunEnded) 자동으로 보고서를 쓴다.")]
        [SerializeField] private bool _writeOnRunEnded = true;

        // 고정 크기. 인덱스는 전부 같은 표본을 가리킨다.
        private readonly long[] _gcBytes = new long[Capacity];
        private readonly long[] _unityBytes = new long[Capacity];
        private readonly int[] _floors = new int[Capacity];
        private readonly bool[] _atFloorStart = new bool[Capacity];

        /// <summary>같은 위상만 골라 담는 작업 버퍼. Report() 마다 새로 만들지 않는다.</summary>
        private readonly long[] _phaseScratch = new long[Capacity];

        private GameEventBus _bus;
        private RunSession _session;
        private int _count;
        private bool _truncated;

        /// <summary>지금까지 담은 층 경계 표본 수.</summary>
        public int SampleCount { get { return _count; } }

        /// <summary>상한을 넘겨 버린 표본이 있는가. 있으면 보고서가 그 사실을 밝힌다.</summary>
        public bool Truncated { get { return _truncated; } }

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_run == null)
            {
                // 왜 null 인지 남긴다. 이 컴포넌트만 붙이고 런이 없는 씬에서는
                // 아무 표본도 안 생기는데, 이유가 없으면 "측정했더니 0" 으로 읽힌다.
                Debug.LogWarning("[상승] MemoryTrendProbe: 씬에 RunSessionBehaviour 가 없다. " +
                                 "층 경계를 알 방법이 없어 표본을 만들지 않는다.");
                return;
            }
            _run.RunStarted += OnRunStarted;
            if (_run.Session != null) Bind(_run.Session);
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Unbind();
        }

        private void Update()
        {
            // RunStarted 를 놓친 늦은 결선. RiskEventBridge 와 같은 이유다 —
            // 런이 코드로 먼저 만들어지면 이 컴포넌트의 Awake 가 그 뒤에 돈다.
            if (_bus != null || _run == null) return;
            RunSession session = _run.Session;
            if (session != null) Bind(session);
        }

        private void OnRunStarted(RunSession session)
        {
            // 새 런은 새 추세다. 옛 표본을 이어 붙이면 재시작이 곧 "누수"로 보인다.
            // 버리기 전에 남긴다 — 재시작 때문에 직전 런의 측정이 사라지면 안 된다.
            if (_count > 0) WriteReport();
            _count = 0;
            _truncated = false;
            Bind(session);
        }

        private void Bind(RunSession session)
        {
            Unbind();
            _session = session;
            _bus = session != null ? session.Events : null;
            if (_bus != null) _bus.Published += OnEvent;
        }

        private void Unbind()
        {
            if (_bus != null) _bus.Published -= OnEvent;
            _bus = null;
            _session = null;
        }

        private void OnEvent(GameEvent e)
        {
            if (e.Kind == GameEventKind.FloorStarted) { Sample(e.Floor, true); return; }
            if (e.Kind == GameEventKind.FloorResolved) { Sample(e.Floor, false); return; }
            if (e.Kind == GameEventKind.RunEnded && _writeOnRunEnded) WriteReport();
        }

        /// <summary>
        /// 지금 이 순간을 한 표본으로 찍는다. 층 경계 밖에서 직접 부를 수도 있다.
        /// <c>GC.GetTotalMemory(false)</c> — false 는 수집을 강제하지 않는다는 뜻이다.
        /// </summary>
        public void Sample(int floor, bool atFloorStart)
        {
            if (_count >= Capacity)
            {
                if (!_truncated)
                {
                    _truncated = true;
                    Debug.LogWarning("[상승] MemoryTrendProbe: 표본 상한 " + Capacity +
                                     " 에 도달했다. 이후 층 경계는 기록하지 않는다.");
                }
                return;
            }

            _gcBytes[_count] = GC.GetTotalMemory(false);
            _unityBytes[_count] = Profiler.GetTotalAllocatedMemoryLong();
            _floors[_count] = floor;
            _atFloorStart[_count] = atFloorStart;
            _count++;
        }

        /// <summary>표본을 버리고 처음부터 다시 잰다.</summary>
        public void ResetSamples()
        {
            _count = 0;
            _truncated = false;
        }

        /// <summary>층별 표와 첫 층 대비 마지막 층 증가분.</summary>
        public string Report()
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("=== 메모리 누적 측정 (UP-TECH-08) ===");
            sb.AppendLine("기기 " + SystemInfo.processorType + " / " + SystemInfo.operatingSystem +
                          " / 시스템 메모리 " + SystemInfo.systemMemorySize + " MB");
            sb.AppendLine("시드 " + (_run != null ? _run.Seed.ToString() : "미상") +
                          " / 모드 " + (_run != null ? _run.Mode.ToString() : "미상") +
                          " / 보고 시점 층 " + (_session != null ? _session.CurrentFloor.ToString() : "미상"));
            sb.AppendLine("표본 " + _count + "개" + (_truncated ? " (상한 " + Capacity + " 도달, 이후 누락)" : ""));
            sb.AppendLine("GC.GetTotalMemory(false) — 수집을 강제하지 않은 값이다. 강제 수집은 그 자체가");
            sb.AppendLine("프레임 스파이크라 측정 대상을 망친다. 같은 위상(층 시작/층 종료)끼리만 비교한다.");
            sb.AppendLine();

            if (_count == 0)
            {
                sb.AppendLine("표본이 없다. 층 경계 사건(FloorStarted/FloorResolved)이 오지 않았다.");
                sb.AppendLine("측정하지 않은 것이지 누적이 없는 것이 아니다.");
                return sb.ToString();
            }

            sb.AppendLine("  #   층  위상      관리 힙(GC)        Unity 총 할당");
            for (int i = 0; i < _count; i++)
            {
                sb.Append("  ").Append(i.ToString("00"));
                sb.Append("  ").Append(_floors[i].ToString("00"));
                sb.Append("  ").Append(_atFloorStart[i] ? "시작 " : "종료 ");
                sb.Append("  ").Append(MemoryTrend.FormatBytes(_gcBytes[i]).PadLeft(12));
                sb.Append("   ").Append(MemoryTrend.FormatBytes(_unityBytes[i]).PadLeft(12));
                if (i > 0)
                {
                    long delta = _gcBytes[i] - _gcBytes[i - 1];
                    sb.Append("   ").Append(delta >= 0 ? "+" : "−").Append(MemoryTrend.FormatBytes(Math.Abs(delta)));
                }
                sb.AppendLine();
            }
            sb.AppendLine();

            AppendPhase(sb, "층 종료 시점 · 관리 힙", _gcBytes, false);
            AppendPhase(sb, "층 시작 시점 · 관리 힙", _gcBytes, true);
            AppendPhase(sb, "층 종료 시점 · Unity 총 할당", _unityBytes, false);

            sb.AppendLine();
            sb.AppendLine("판정 규칙: 표본 " + MemoryTrend.MinimumSamplesForMonotonicRule +
                          "개 이상에서 한 번도 내려가지 않고 오른 흐름은 크기와 무관하게 '증가'다.");
            sb.AppendLine("그 외에는 앞 1/4 평균과 뒤 1/4 평균의 차가 " +
                          MemoryTrend.FormatBytes(MemoryTrend.DefaultAbsoluteToleranceBytes) + " 와 " +
                          (MemoryTrend.DefaultRelativeTolerance * 100.0).ToString("0") + "% 를 모두 넘어야 '증가'다.");
            return sb.ToString();
        }

        /// <summary>보고서를 파일로 남긴다.</summary>
        public bool WriteReport()
        {
            string body = Report();
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, body);
                Debug.Log("[상승] 메모리 누적 측정 → " + ReportPath + "\n" + body);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[상승] 메모리 추세 보고서 쓰기 실패: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 한 위상만 골라 추세를 낸다. 층 시작과 층 종료를 섞으면 층 안에서 쓴 메모리가
        /// 층 사이 누적처럼 보인다 — 그 둘을 구분하는 것이 이 측정의 전부다.
        /// </summary>
        private void AppendPhase(StringBuilder sb, string label, long[] source, bool wantStart)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
                if (_atFloorStart[i] == wantStart) _phaseScratch[n++] = source[i];

            if (n < 2)
            {
                sb.AppendLine("[" + label + "] 표본 " + n + "개 — 판정하지 않았다.");
                return;
            }

            MemoryTrendResult result = MemoryTrend.Analyze(_phaseScratch, n,
                MemoryTrend.DefaultAbsoluteToleranceBytes, MemoryTrend.DefaultRelativeTolerance);
            sb.AppendLine("[" + label + "] " + result.Describe());
        }
    }
}

// 씬 배선 필요:
//   1) `Prototype_Elevator.unity` 에 빈 GameObject "MemoryTrendProbe" 를 만들고 이 컴포넌트를 붙인다.
//   2) `_run` 은 비워 둬도 Awake 에서 FindAnyObjectByType 으로 찾는다. 씬에 RunSessionBehaviour 가
//      둘 이상이면 명시적으로 끌어다 넣는다.
//   3) 10층 런 전후 비교가 UP-TECH-08 의 검증 방법이다 — `TenFloorAutoPilot` 이 도는 씬에
//      이 컴포넌트를 함께 올려 두면 런 종료(RunEnded)에 `Logs/memory_trend.txt` 가 남는다.
