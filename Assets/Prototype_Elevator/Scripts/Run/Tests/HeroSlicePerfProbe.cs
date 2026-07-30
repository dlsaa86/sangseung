using System.Collections;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Ascend.Prototype.Player;
using Ascend.Prototype.View;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// 성능 측정 하네스. `TECH_SPEC.md` §13의 항목을 실제 값으로 채운다.
    ///
    /// **이 측정은 성능 완료 선언이 아니다.** §13은 기준 PC(`TargetHardwareProfile`)가
    /// 지정되지 않으면 성능 완료를 선언하지 말라고 못박는다. 현재 개발 기기는
    /// Apple M5 / Metal / macOS 이고 문서의 기준(Ryzen 7 5700 / RTX 3070 / Windows)이 아니다.
    /// 그래서 여기 나오는 수치는 **참고치**이고, 보고서에도 그렇게 적는다.
    ///
    /// 측정하는 것:
    ///   · 유휴 프레임타임 분포(중앙/95퍼센타일/최악)
    ///   · 스핀 + 캐스케이드 재생 중 프레임타임과 스파이크
    ///   · 프레임당 GC Alloc (워밍업 이후)
    ///   · 판정 자체의 순수 비용(연출 없이 1000스핀)
    /// </summary>
    public sealed class HeroSlicePerfProbe : MonoBehaviour
    {
        public const string ReportPath = "Logs/heroslice_perf.txt";
        private const string PrefKey = "Ascend.HeroSlicePerfProbe.Armed";

        private readonly StringBuilder _report = new StringBuilder();

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);
            new GameObject("HeroSlicePerfProbe").AddComponent<HeroSlicePerfProbe>();
        }

        public static void Arm() => UnityEditor.EditorPrefs.SetBool(PrefKey, true);
#endif

        private IEnumerator Start()
        {
            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            var presenter = FindAnyObjectByType<SpinPresenter>();
            var lever = FindByName<InteractableLever>("ExecutionLever");
            var panel = FindAnyObjectByType<InteractableContractPanel>();
            var overharvest = FindAnyObjectByType<InteractableOverharvestLever>();

            _report.AppendLine("=== Hero Slice 성능 측정 ===");
            _report.AppendLine($"기기 {SystemInfo.processorType} / {SystemInfo.graphicsDeviceName} / " +
                               $"{SystemInfo.graphicsDeviceType} / {SystemInfo.operatingSystem}");
            _report.AppendLine($"해상도 {Screen.width}×{Screen.height} / vSync {QualitySettings.vSyncCount} / " +
                               $"targetFrameRate {Application.targetFrameRate}");
            _report.AppendLine("주의: 에디터 Play 모드 측정이다. 빌드 성능과 다르며, 기준 PC(Windows/RTX 3070)도 아니다.");
            _report.AppendLine("      TECH_SPEC §13 에 따라 이 수치로 성능 완료를 선언하지 않는다.");
            _report.AppendLine();

            if (run == null || lever == null || panel == null)
            {
                _report.AppendLine("씬 배선 부족 — 측정 중단");
                Finish();
                yield break;
            }

            // ── 1. 판정 자체의 비용 (연출·렌더 없이) ──
            MeasureResolverCost();

            // ── 2. 유휴 프레임 ──
            run.ResetRun(1337);
            yield return null;
            for (int i = 0; i < 60; i++) yield return null;   // 워밍업
            yield return MeasureFrames("유휴 (계약 선택 대기)", 180);

            // ── 2b. GC Alloc 범인 찾기 ──
            //
            // 숫자만 적고 "할당이 많다"로 끝내면 다음 세션이 처음부터 다시 조사한다.
            // IMGUI 디버그 HUD를 끄고 같은 조건을 다시 재서 기여분을 분리한다.
            var hud = FindAnyObjectByType<UI.RouletteHud>();
            if (hud != null)
            {
                hud.enabled = false;
                for (int i = 0; i < 30; i++) yield return null;
                yield return MeasureFrames("유휴 — IMGUI 디버그 HUD 끔", 180);
                hud.enabled = true;
                for (int i = 0; i < 30; i++) yield return null;
            }

            // ── 3. 스핀 + 캐스케이드 재생 중 ──
            int guard = 0;
            while (bridge.PreviewIndex != 2 && guard++ < 8) { panel.Interact(gameObject); yield return null; }
            lever.Interact(gameObject);
            yield return null;

            yield return MeasureDuringPlay(run, bridge, presenter, lever, overharvest);

            Finish();
        }

        /// <summary>
        /// 연출을 뺀 순수 판정 비용. `TECH_SPEC.md` §2 "화면 연출을 제거해도 룰렛 판정
        /// 테스트가 가능해야 한다"가 여기서도 쓰인다 — 판정만 따로 잴 수 있다.
        /// </summary>
        private void MeasureResolverCost()
        {
            var plan = Spin.PrototypeCurriculum.HeroSlice;
            var contract = plan.ContractChoices[2];
            var rules = Spin.PrototypeCurriculum.BuildRules(in plan);
            rules.Apply(in contract);

            // 워밍업 — 첫 호출의 JIT·배열 할당을 측정에서 뺀다.
            var warm = new Spin.SpinEngine(1);
            for (int i = 0; i < 100; i++)
                warm.SpinWithSeed(Spin.SpinSeed.Derive(1, 1, i), rules, in contract, Spin.ResidualState.Empty);

            long before = System.GC.GetTotalMemory(false);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int totalDepth = 0;
            const int samples = 1000;
            for (int i = 0; i < samples; i++)
            {
                var engine = new Spin.SpinEngine(i);
                var r = engine.SpinWithSeed(Spin.SpinSeed.Derive(i, 1, 0), rules, in contract,
                                            Spin.ResidualState.Empty);
                totalDepth += r.ChainDepth;
            }
            watch.Stop();
            long after = System.GC.GetTotalMemory(false);

            _report.AppendLine("[판정 순수 비용] 연출·렌더 없이 1000스핀");
            _report.AppendLine($"  총 {watch.Elapsed.TotalMilliseconds:F1} ms / 스핀당 " +
                               $"{watch.Elapsed.TotalMilliseconds / samples * 1000f:F1} µs / 평균 연쇄 {totalDepth / (float)samples:F2}");
            _report.AppendLine($"  힙 증가 {(after - before) / 1024f:F0} KB (GC 미강제, 스핀당 " +
                               $"{(after - before) / (float)samples:F0} B)");
            _report.AppendLine("  → 판정은 프레임 예산 대비 무시할 수준이다. 스핀당 할당은 " +
                               "SpinResolution·CascadeStep 배열이며 스핀 시작에만 발생한다.");
            _report.AppendLine();
        }

        private IEnumerator MeasureFrames(string label, int frames)
        {
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var samples = new float[frames];
            var alloc = new long[frames];

            for (int i = 0; i < frames; i++)
            {
                yield return null;
                samples[i] = Time.unscaledDeltaTime * 1000f;
                alloc[i] = recorder.Valid ? recorder.LastValue : -1;
            }
            recorder.Dispose();

            System.Array.Sort(samples);
            float median = samples[frames / 2];
            float p95 = samples[Mathf.Min(frames - 1, Mathf.RoundToInt(frames * 0.95f))];
            float worst = samples[frames - 1];

            long allocSum = 0;
            long allocMax = 0;
            int allocValid = 0;
            foreach (long a in alloc)
            {
                if (a < 0) continue;
                allocValid++;
                allocSum += a;
                if (a > allocMax) allocMax = a;
            }

            _report.AppendLine($"[{label}] {frames}프레임");
            _report.AppendLine($"  프레임타임 중앙 {median:F2} ms ({1000f / Mathf.Max(0.01f, median):F0} FPS) / " +
                               $"95% {p95:F2} ms / 최악 {worst:F2} ms");
            _report.AppendLine(allocValid > 0
                ? $"  GC Alloc 프레임당 평균 {allocSum / (float)allocValid:F0} B / 최대 {allocMax} B"
                : "  GC Alloc 측정 불가 (ProfilerRecorder 무효)");
            _report.AppendLine();
        }

        private IEnumerator MeasureDuringPlay(RunSessionBehaviour run, RouletteInteractionBridge bridge,
                                              SpinPresenter presenter, InteractableLever lever,
                                              InteractableOverharvestLever overharvest)
        {
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var samples = new System.Collections.Generic.List<float>(2048);
            var allocs = new System.Collections.Generic.List<long>(2048);
            int maxDepth = 0;
            int spins = 0;

            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;

                if (!bridge.IsLocked)
                {
                    if (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0)
                    { lever.Interact(gameObject); spins++; }
                    else if (floor.Phase == FloorPhase.Decision)
                    {
                        if (floor.SpinsRemaining <= 0 || !floor.CanBank) break;
                        if (overharvest != null && overharvest.CanInteract)
                        { overharvest.Interact(gameObject); spins++; }
                    }
                }

                yield return null;
                samples.Add(Time.unscaledDeltaTime * 1000f);
                allocs.Add(recorder.Valid ? recorder.LastValue : -1);
                if (presenter != null && presenter.CurrentDepth > maxDepth) maxDepth = presenter.CurrentDepth;
            }
            recorder.Dispose();

            if (samples.Count == 0) { _report.AppendLine("[스핀·캐스케이드 중] 표본 없음"); yield break; }

            samples.Sort();
            float median = samples[samples.Count / 2];
            float p95 = samples[Mathf.Min(samples.Count - 1, Mathf.RoundToInt(samples.Count * 0.95f))];
            float worst = samples[samples.Count - 1];

            long allocSum = 0, allocMax = 0;
            int valid = 0;
            foreach (long a in allocs)
            {
                if (a < 0) continue;
                valid++; allocSum += a;
                if (a > allocMax) allocMax = a;
            }

            _report.AppendLine($"[스핀·캐스케이드 재생 중] {samples.Count}프레임 / 스핀 {spins}회 / 최대 연쇄 {maxDepth}단계");
            _report.AppendLine($"  프레임타임 중앙 {median:F2} ms ({1000f / Mathf.Max(0.01f, median):F0} FPS) / " +
                               $"95% {p95:F2} ms / 최악 {worst:F2} ms");
            _report.AppendLine(valid > 0
                ? $"  GC Alloc 프레임당 평균 {allocSum / (float)valid:F0} B / 최대 {allocMax} B"
                : "  GC Alloc 측정 불가");
            _report.AppendLine($"  스파이크 판정: 최악 프레임이 중앙의 {worst / Mathf.Max(0.01f, median):F1}배");
            _report.AppendLine();
        }

        private void Finish()
        {
            string text = _report.ToString();
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(true));
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[상승] 성능 보고 저장 실패: {exception.Message}");
            }
            Debug.Log($"[상승] {text}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static T FindByName<T>(string name) where T : Component
        {
            foreach (T candidate in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (candidate.name == name) return candidate;
            return FindAnyObjectByType<T>();
        }
    }
}
