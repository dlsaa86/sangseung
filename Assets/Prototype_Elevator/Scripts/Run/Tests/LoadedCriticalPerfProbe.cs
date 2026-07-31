using System.Collections;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Ascend.Prototype.Build;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// `P2-Gate G`가 요구하는 **"최대 적재와 Critical 상태 측정"**.
    ///
    /// 왜 별도 하네스인가: `HeroSlicePerfProbe`는 1층 Hero Slice를 **무적재·Stable**로만 잰다.
    /// 독립 성능 감사가 이것을 blocker 로 지목했다 — Gate 가 이름으로 지정한 두 조건이
    /// 측정 대상에 아예 없었다. 그리고 `BuildFigureView`는 적재가 비면 early-return 하므로,
    /// 무적재 측정은 **그 컴포넌트의 비용에 구조적으로 눈이 멀어 있다.**
    ///
    /// 왜 인터리브하는가: 이 프로젝트는 이미 한 번 순서 교락에 속았다. ①②③을 항상 같은
    /// 순서로 재고 "③이 무겁다"고 결론냈는데, 순서를 뒤집으니 재현되지 않았고 원인은
    /// 첫 측정 구간의 워밍업이었다. 그래서 A→B→B→A 로 재고 **같은 조건의 두 값을 함께**
    /// 적는다. 두 값이 벌어지면 그 자체가 "이 측정은 순서에 오염됐다"는 신호다.
    ///
    /// 에디터 Play 모드 측정이다. 빌드 성능이 아니며, 그 구분은 보고서 머리말에 적는다.
    /// </summary>
    public sealed class LoadedCriticalPerfProbe : MonoBehaviour
    {
        public const string ReportPath = "Logs/loaded_critical_perf.txt";
        private const string PrefKey = "Ascend.LoadedCriticalPerfProbe.Armed";
        private const int Frames = 180;
        private const int WarmupFrames = 60;
        /// <summary>위험 상태 블렌딩(2.2/초) 수렴 대기. 캡처 리그와 같은 값을 쓴다.</summary>
        private const float SettleSeconds = 2.5f;

        private readonly StringBuilder _report = new StringBuilder();

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);
            new GameObject("LoadedCriticalPerfProbe").AddComponent<LoadedCriticalPerfProbe>();
        }

        public static void Arm() => UnityEditor.EditorPrefs.SetBool(PrefKey, true);
#endif

        private IEnumerator Start()
        {
            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            var risk = FindAnyObjectByType<RiskStateView>();
            var figures = FindAnyObjectByType<BuildFigureView>();

            Header(run, risk, figures);

            if (run == null)
            {
                _report.AppendLine("실패: RunSessionBehaviour 가 씬에 없다. 측정하지 않았다.");
                Finish();
                yield break;
            }

            // 워밍업. 첫 측정 구간이 항상 무거운 것을 조건 차이로 오독하지 않기 위해서다.
            for (int i = 0; i < WarmupFrames; i++) yield return null;

            // A→B→B→A. 같은 라벨의 두 값이 벌어지면 순서 오염이다.
            yield return Sample(run, bridge, risk, figures, loaded: false, critical: false, pass: 1);
            yield return Sample(run, bridge, risk, figures, loaded: true, critical: true, pass: 1);
            yield return Sample(run, bridge, risk, figures, loaded: true, critical: true, pass: 2);
            yield return Sample(run, bridge, risk, figures, loaded: false, critical: false, pass: 2);

            Finish();
        }

        private void Header(RunSessionBehaviour run, RiskStateView risk, BuildFigureView figures)
        {
            _report.AppendLine("=== 최대 적재 + Critical 성능 측정 ===");
            _report.AppendLine($"기기 {SystemInfo.processorType} / {SystemInfo.graphicsDeviceName} / " +
                               $"{SystemInfo.graphicsDeviceType} / {SystemInfo.operatingSystem}");
            _report.AppendLine($"해상도 {Screen.width}×{Screen.height} / vSync {QualitySettings.vSyncCount} / " +
                               $"targetFrameRate {Application.targetFrameRate}");
            _report.AppendLine($"씬 배선 — run={(run != null)} risk={(risk != null)} figures={(figures != null)}");
            _report.AppendLine();
            _report.AppendLine("이것은 **에디터 Play 모드** 측정이다. 빌드 성능이 아니다.");
            _report.AppendLine("기준 하드웨어가 확정되지 않았으므로(`ASSUMPTION_LOG` A-20260730-01)");
            _report.AppendLine("이 수치로 성능 완료를 선언하지 않는다. 조건 간 **차이**를 읽는 것이 목적이다.");
            _report.AppendLine($"순서 교락을 배제하려고 A→B→B→A 로 잰다. 프레임 {Frames} / 워밍업 {WarmupFrames}.");
            _report.AppendLine();
        }

        private IEnumerator Sample(RunSessionBehaviour run, RouletteInteractionBridge bridge,
            RiskStateView risk, BuildFigureView figures, bool loaded, bool critical, int pass)
        {
            run.ResetRun(RunMode.TenFloor, 4242);
            yield return null;

            if (loaded)
            {
                // 카탈로그 순서대로 슬롯이 찰 때까지 싣는다. 캡처 리그의 07번과 같은 방식이다.
                foreach (BuildItem item in BuildCatalog.All)
                {
                    if (run.Session.Loadout.IsFull) break;
                    run.Session.Loadout.Add(item);
                }
                // 과적까지 밀어 넣는다 — 최대 적재의 실제 비용은 과적 상태에서 나온다.
                run.Session.AddWeight(140f);
            }

            if (critical) yield return ForceCritical(run, bridge, risk);

            // 위험 상태는 프레임 단위로 블렌딩한다. 정착 전에 재면 두 조건이 사실상
            // 같은 화면이 되어 차이가 0으로 나온다 — 캡처 세트가 정확히 그렇게 실패했다.
            yield return WaitSeconds(SettleSeconds);

            int count = run.Session.Loadout != null ? run.Session.Loadout.Count : 0;
            RiskLevel level = risk != null ? risk.Level : RiskLevel.Stable;
            int figureCount = figures != null ? CountFigures(figures) : -1;
            string label = $"{(loaded ? "최대적재" : "무적재")}·{level} (pass {pass})";

            yield return MeasureFrames(label,
                $"적재 {count}개 / {run.Session.CarriedWeight:F0}kg / 과적 {run.Session.IsOverloaded} / " +
                $"위험 {level} 점수 {(risk != null ? risk.Score : 0f):F1} / 배치된 오브젝트 {figureCount}개");

            // 조건이 실제로 성립했는지 함께 적는다. 성립하지 않은 측정을 성립한 것처럼
            // 적으면 이 하네스 자체가 거짓 증거가 된다.
            if (loaded && count == 0)
                _report.AppendLine("  ⚠ 적재가 0개다 — '최대 적재' 조건이 성립하지 않았다.");
            if (critical && level < RiskLevel.Critical)
                _report.AppendLine($"  ⚠ 위험이 {level} 에 그쳤다 — 'Critical' 조건이 성립하지 않았다.");
            _report.AppendLine();
        }

        /// <summary>
        /// 실제로 서 있는 오브젝트 수. `TenFloorAutoPilot`이 쓰는 것과 같은 규약
        /// (`Load_` 접두사)을 따른다 — 세는 방법이 둘이면 두 보고서가 다른 수를 적는다.
        /// </summary>
        private static int CountFigures(BuildFigureView figures)
        {
            int n = 0;
            foreach (Transform child in figures.transform)
                if (child.gameObject.activeSelf && child.name.StartsWith("Load_")) n++;
            return n;
        }

        private IEnumerator MeasureFrames(string label, string condition)
        {
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var samples = new float[Frames];
            var alloc = new long[Frames];

            for (int i = 0; i < Frames; i++)
            {
                yield return null;
                samples[i] = Time.unscaledDeltaTime * 1000f;
                alloc[i] = recorder.Valid ? recorder.LastValue : -1;
            }
            recorder.Dispose();

            System.Array.Sort(samples);
            float median = samples[Frames / 2];
            float p95 = samples[Mathf.Min(Frames - 1, Mathf.RoundToInt(Frames * 0.95f))];
            float worst = samples[Frames - 1];

            long allocSum = 0; long allocWorst = 0; int allocValid = 0;
            for (int i = 0; i < Frames; i++)
            {
                if (alloc[i] < 0) continue;
                allocValid++;
                allocSum += alloc[i];
                if (alloc[i] > allocWorst) allocWorst = alloc[i];
            }

            _report.AppendLine($"[{label}]");
            _report.AppendLine($"  조건 — {condition}");
            _report.AppendLine($"  프레임타임 중앙 {median:F2} ms ({1000f / Mathf.Max(0.01f, median):F0} FPS) / " +
                               $"95% {p95:F2} ms / 최악 {worst:F2} ms");

            // 중앙값이 흔한 상한(60/120/144 Hz)에 붙어 있으면 그것은 **비용이 아니라 상한**이다.
            // 상한에 걸린 중앙값으로 두 조건을 비교하면 "차이가 없다"가 항상 나온다 —
            // 실제로는 둘 다 상한 아래라는 뜻일 뿐이다. 그 경우 읽을 수 있는 것은 꼬리뿐이다.
            foreach (float hz in new[] { 60f, 120f, 144f, 240f })
            {
                float capMs = 1000f / hz;
                if (Mathf.Abs(median - capMs) < 0.05f)
                {
                    _report.AppendLine($"  ⚠ 중앙값이 {hz:F0} Hz 상한({capMs:F2} ms)에 붙어 있다 — " +
                                       "중앙값은 비용이 아니라 상한이다. 조건 비교는 95%·최악으로만 한다.");
                    break;
                }
            }
            _report.AppendLine(allocValid > 0
                ? $"  GC Alloc 평균 {(double)allocSum / allocValid:F0} B/프레임 / 최악 {allocWorst} B / " +
                  $"유효 표본 {allocValid}/{Frames}"
                : "  GC Alloc 측정 불가 (ProfilerRecorder 무효)");
        }

        private IEnumerator ForceCritical(RunSessionBehaviour run,
            RouletteInteractionBridge bridge, RiskStateView risk)
        {
            int guard = 0;
            while (guard++ < 8)
            {
                Ascend.Prototype.Run.FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (risk != null && risk.Level >= RiskLevel.Critical) break;

                if (floor.Phase == Ascend.Prototype.Run.FloorPhase.Boarding) run.FinishBoarding();
                if (floor.Phase == Ascend.Prototype.Run.FloorPhase.ContractSelection) run.SelectContract(0);

                while (floor.Phase == Ascend.Prototype.Run.FloorPhase.Spinning && floor.SpinsRemaining > 0)
                {
                    run.Spin();
                    yield return WaitWhileLocked(bridge);
                }
                if (floor.Phase == Ascend.Prototype.Run.FloorPhase.Decision &&
                    floor.CanBank && floor.SpinsRemaining > 0)
                {
                    run.PushYourLuck();
                    run.Spin();
                    yield return WaitWhileLocked(bridge);
                }
                else break;
                yield return WaitSeconds(0.4f);
            }
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static IEnumerator WaitWhileLocked(RouletteInteractionBridge bridge)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (bridge != null && bridge.IsLocked && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private void Finish()
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, _report.ToString());
                Debug.Log($"[상승] 최대적재+Critical 측정 완료 → {ReportPath}\n{_report}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[상승] 측정 보고서 쓰기 실패: {e.Message}");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
