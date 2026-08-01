using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;
using TMPro;
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

            CollectFonts();

            // ── 1. 판정 자체의 비용 (연출·렌더 없이) ──
            MeasureResolverCost();

            // ── 2. 유휴 프레임 ──
            run.ResetRun(1337);
            yield return null;
            // Play 진입 직후는 셰이더 컴파일·에셋 임포트로 가장 시끄럽다. 충분히 지나보낸다.
            for (int i = 0; i < 240; i++) yield return null;   // 워밍업
            yield return MeasureFrames("유휴 (계약 선택 대기)", 180);

            // ── 2b. GC Alloc 범인 찾기 ──
            //
            // 숫자만 적고 "할당이 많다"로 끝내면 다음 세션이 처음부터 다시 조사한다.
            // 화면 UI를 끄고 같은 조건을 다시 재서 기여분을 분리한다.
            var hud = FindAnyObjectByType<UI.GameHudView>();
            if (hud != null)
            {
                hud.enabled = false;
                for (int i = 0; i < 30; i++) yield return null;
                yield return MeasureFrames("유휴 — 화면 UI 끔", 180);
            }

            // ── 2c. 남은 바닥이 게임 코드인가, 에디터·URP인가 ──
            //
            // HUD를 꺼도 프레임당 할당이 일정하게 남는다면, 그건 상태에 따라 변하는
            // 게임 코드가 아니라는 뜻이다. 게임 쪽 MonoBehaviour를 전부 끄고 같은 조건을
            // 재서 확인한다. 여기서도 같은 값이 나오면 남은 바닥은 우리 코드가 아니다.
            var suspects = new MonoBehaviour[]
            {
                FindAnyObjectByType<Risk.RiskStateView>(),
                FindAnyObjectByType<InstrumentPanelView>(),
                FindAnyObjectByType<PurifyMarkerView>(),
                FindAnyObjectByType<SpinBoardView>(),
                // 승객·부품 배치 뷰. 이 목록에 없으면 "게임 코드 기여분 = 전부켬 −
                // 전부끔"이 이 컴포넌트를 **구조적으로 상쇄한다** — 무엇을 고쳐도
                // 수치가 움직이지 않는다. 독립 감사가 그 맹점을 지적했다.
                FindAnyObjectByType<Ascend.Prototype.Build.BuildFigureView>(),
                FindAnyObjectByType<CrosshairInteractor>(),
                bridge,
                presenter,
            };

            var wasEnabled = new bool[suspects.Length];
            for (int i = 0; i < suspects.Length; i++)
            {
                if (suspects[i] == null) continue;
                wasEnabled[i] = suspects[i].enabled;
                suspects[i].enabled = false;
            }
            for (int i = 0; i < 60; i++) yield return null;
            yield return MeasureFrames("유휴 — 게임 뷰 컴포넌트까지 전부 끔", 180);

            for (int i = 0; i < suspects.Length; i++)
                if (suspects[i] != null) suspects[i].enabled = wasEnabled[i];
            if (hud != null) hud.enabled = true;
            for (int i = 0; i < 60; i++) yield return null;

            // ── 3. 스핀 + 캐스케이드 재생 중 ──
            int guard = 0;
            while (bridge.PreviewIndex != 2 && guard++ < 8) { panel.Interact(gameObject); yield return null; }
            lever.Interact(gameObject);
            yield return null;

            yield return MeasureDuringPlay(run, bridge, presenter, lever, overharvest);

            // ── 3c. 재생 구간 이분 탐색 ──
            //
            // 스파이크는 오직 "연출 재생 중"에만 나온다(정착 상태 300프레임에는 0개).
            // 어느 컴포넌트가 만드는지 하나씩 꺼가며 같은 재생을 반복해 좁힌다.
            _report.AppendLine("[재생 구간 이분 탐색] 시드 12 고정 — 세 번 모두 같은 연쇄를 재생한다");
            yield return BisectPresentation(run, bridge, lever, "① 전부 켬", true, true);
            yield return BisectPresentation(run, bridge, lever, "② HUD 끔", false, true);
            yield return BisectPresentation(run, bridge, lever, "③ HUD + 표식 끔", false, false);

            // **순서를 뒤집어 한 번 더 잰다.** 앞선 감사가 정확히 이 지점을 찔렀다 —
            // ①②③을 항상 이 순서로만 재면 "③에서 스파이크가 크다"와 "세션 후반에
            // 스파이크가 크다"를 구분할 수 없다. 컴포넌트를 끈 것이 원인인지 시간이
            // 흐른 것이 원인인지는 순서를 바꿔 봐야만 갈린다.
            //
            // 스파이크가 라벨을 따라가면 컴포넌트 탓이고, 위치를 따라가면 측정 순서 탓이다.
            _report.AppendLine("  — 순서 반전 대조 (같은 구성, 실행 순서만 ③②①) —");
            yield return BisectPresentation(run, bridge, lever, "③′ HUD + 표식 끔", false, false);
            yield return BisectPresentation(run, bridge, lever, "②′ HUD 끔", false, true);
            yield return BisectPresentation(run, bridge, lever, "①′ 전부 켬", true, true);
            _report.AppendLine();

            // ── 3b. 스핀 이후 상태에서 HUD 기여 분리 ──
            //
            // "유휴(계약 선택)"에서는 결과판 미러가 그려지지 않는다(스핀 기록이 없으니까).
            // 스핀이 쌓인 뒤에는 HUD가 매 OnGUI마다 9칸 GUI.Box + 라벨을 더 그린다.
            // 그 차이를 같은 게임 상태에서 재야 정직하다.
            yield return MeasureHudContribution();

            // ── 4. 대조군: 같은 길이 동안 아무것도 안 하고 재기 ──
            //
            // 3번에서 나온 스파이크는 스핀·연쇄와 상관이 없었다(스핀 입력 1/37, 글리프 성장 0/37).
            // 그렇다면 게임 코드가 아닐 수 있다. 게임 컴포넌트와 HUD를 전부 끄고 같은 시간을
            // 재서 같은 크기의 스파이크가 또 나오는지 본다. 나오면 원인은 우리 코드 밖이다.
            yield return MeasureIdleSpikes(60f);

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
            // 버퍼를 recorder **앞에서** 잡는다. 뒤에서 잡으면 이 배열들의 할당이
            // 첫 프레임의 GC Alloc 으로 잡혀 「측정 도구가 자기를 잰 값」이 최악값이 된다.
            var samples = new float[frames];
            var alloc = new long[frames];
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

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

            _report.AppendLine($"[{label}] {frames}프레임");
            _report.AppendLine($"  프레임타임 중앙 {median:F2} ms ({1000f / Mathf.Max(0.01f, median):F0} FPS) / " +
                               $"95% {p95:F2} ms / 최악 {worst:F2} ms");
            AppendAllocStats(alloc, alloc.Length);
            _report.AppendLine();
        }

        /// <summary>
        /// 스파이크 한 프레임의 정황. 느린 프레임이 **왜** 느렸는지는 프레임타임만으로는
        /// 절대 알 수 없다. 그래서 같은 프레임의 GC 수집 횟수, TMP 동적 아틀라스 성장,
        /// 연출 단계를 함께 찍어두고 나중에 대조한다.
        /// </summary>
        private struct FrameSample
        {
            public int Index;
            public float Milliseconds;
            public long GcAlloc;
            public int Gen0, Gen1, Gen2;      // 누적 수집 횟수
            public int TmpGlyphs;             // 폰트에 올라온 글리프 수(동적 아틀라스)
            public int TmpAtlasTextures;      // 아틀라스 텍스처 장수
            public int PresenterDepth;
            public bool SpinTriggered;
        }

        private TMP_FontAsset[] _fonts;

        /// <summary>씬에서 실제로 쓰이는 폰트를 모은다. 폴백까지 포함해야 성장이 안 새어 나간다.</summary>
        private void CollectFonts()
        {
            var found = new List<TMP_FontAsset>();
            foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.font == null || found.Contains(text.font)) continue;
                found.Add(text.font);
                if (text.font.fallbackFontAssetTable == null) continue;
                foreach (TMP_FontAsset fallback in text.font.fallbackFontAssetTable)
                    if (fallback != null && !found.Contains(fallback)) found.Add(fallback);
            }
            _fonts = found.ToArray();

            _report.AppendLine("[폰트 상태] 동적 아틀라스는 새 글자가 처음 나올 때 래스터라이즈된다");
            foreach (TMP_FontAsset font in _fonts)
                _report.AppendLine($"  {font.name}: 모드 {font.atlasPopulationMode} / " +
                                   $"글리프 {font.characterTable.Count} / 아틀라스 {font.atlasTextures.Length}장 " +
                                   $"({font.atlasWidth}×{font.atlasHeight})");
            _report.AppendLine();
        }

        private int TotalGlyphs()
        {
            int total = 0;
            if (_fonts != null)
                foreach (TMP_FontAsset font in _fonts)
                    if (font != null) total += font.characterTable.Count;
            return total;
        }

        private int TotalAtlasTextures()
        {
            int total = 0;
            if (_fonts != null)
                foreach (TMP_FontAsset font in _fonts)
                    if (font != null) total += font.atlasTextures.Length;
            return total;
        }

        /// <summary>느린 프레임 상위 N개를 정황과 함께 뱉는다.</summary>
        private void ReportSpikes(List<FrameSample> samples, float medianMs)
        {
            if (samples.Count < 2) return;

            var sorted = new List<FrameSample>(samples);
            sorted.Sort((a, b) => b.Milliseconds.CompareTo(a.Milliseconds));

            _report.AppendLine("[스파이크 상위 8프레임] — 같은 프레임의 정황");
            _report.AppendLine("  #프레임      ms   GC할당(B)  GC수집(g0/g1/g2)  글리프Δ  아틀라스Δ  연쇄  스핀입력");

            int shown = Mathf.Min(8, sorted.Count);
            for (int i = 0; i < shown; i++)
            {
                FrameSample s = sorted[i];
                FrameSample previous = FindPrevious(samples, s.Index);
                _report.AppendLine(
                    $"  {s.Index,7}  {s.Milliseconds,7:F1}  {s.GcAlloc,10}  " +
                    $"{s.Gen0 - previous.Gen0}/{s.Gen1 - previous.Gen1}/{s.Gen2 - previous.Gen2,-12}  " +
                    $"{s.TmpGlyphs - previous.TmpGlyphs,6}  {s.TmpAtlasTextures - previous.TmpAtlasTextures,8}  " +
                    $"{s.PresenterDepth,4}  {(s.SpinTriggered ? "예" : "-"),6}");
            }

            // 상관 요약 — 눈으로 세지 않아도 되게.
            int spikeCount = 0, withGc = 0, withGlyphGrowth = 0, withSpin = 0;
            float threshold = Mathf.Max(medianMs * 8f, 5f);
            foreach (FrameSample s in samples)
            {
                if (s.Milliseconds < threshold) continue;
                FrameSample previous = FindPrevious(samples, s.Index);
                spikeCount++;
                if (s.Gen0 > previous.Gen0 || s.Gen1 > previous.Gen1 || s.Gen2 > previous.Gen2) withGc++;
                if (s.TmpGlyphs > previous.TmpGlyphs) withGlyphGrowth++;
                if (s.SpinTriggered) withSpin++;
            }

            _report.AppendLine();
            _report.AppendLine($"  기준 {threshold:F1} ms 초과 프레임 {spikeCount}개 중");
            _report.AppendLine($"    GC 수집이 함께 일어난 프레임   {withGc}개");
            _report.AppendLine($"    TMP 글리프가 늘어난 프레임     {withGlyphGrowth}개");
            _report.AppendLine($"    스핀 입력이 있던 프레임        {withSpin}개");
            _report.AppendLine();
        }

        private static FrameSample FindPrevious(List<FrameSample> samples, int index)
        {
            for (int i = 0; i < samples.Count; i++)
                if (samples[i].Index == index)
                    return i > 0 ? samples[i - 1] : samples[i];
            return samples[0];
        }

        private IEnumerator MeasureDuringPlay(RunSessionBehaviour run, RouletteInteractionBridge bridge,
                                              SpinPresenter presenter, InteractableLever lever,
                                              InteractableOverharvestLever overharvest)
        {
            // **recorder 보다 먼저 잡는다.** 뒤에서 잡으면 4096×48 B = 196,608 B 가
            // 측정 창 안에 들어간다 — 이전 보고서의 「스핀·캐스케이드 재생 중 최대
            // 205,437 B」가 정확히 이것이었다(계산값과 24 B 차이 = List 헤더 + 배열 헤더).
            var frames = new List<FrameSample>(4096);
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            int maxDepth = 0;
            int spins = 0;
            int index = 0;

            // 벽시계 60초를 재면 대부분이 '아무 일도 없는 대기'라 통계가 희석된다.
            // 스핀이 끝나면 즉시 멈춘다.
            float deadline = Time.realtimeSinceStartup + 60f;
            float wallStart = Time.realtimeSinceStartup;
            float accumulated = 0f;
            while (Time.realtimeSinceStartup < deadline)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (!bridge.IsLocked && floor.Phase == FloorPhase.Decision &&
                    (floor.SpinsRemaining <= 0 || !floor.CanBank)) break;

                bool spunThisFrame = false;
                if (!bridge.IsLocked)
                {
                    if (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0)
                    { lever.Interact(gameObject); spins++; spunThisFrame = true; }
                    else if (floor.Phase == FloorPhase.Decision)
                    {
                        if (floor.SpinsRemaining <= 0 || !floor.CanBank) break;
                        if (overharvest != null && overharvest.CanInteract)
                        { overharvest.Interact(gameObject); spins++; spunThisFrame = true; }
                    }
                }

                yield return null;

                frames.Add(new FrameSample
                {
                    Index = index++,
                    Milliseconds = Time.unscaledDeltaTime * 1000f,
                    GcAlloc = recorder.Valid ? recorder.LastValue : -1,
                    Gen0 = System.GC.CollectionCount(0),
                    Gen1 = System.GC.CollectionCount(1),
                    Gen2 = System.GC.CollectionCount(2),
                    TmpGlyphs = TotalGlyphs(),
                    TmpAtlasTextures = TotalAtlasTextures(),
                    PresenterDepth = presenter != null ? presenter.CurrentDepth : 0,
                    SpinTriggered = spunThisFrame,
                });
                accumulated += Time.unscaledDeltaTime;
                if (presenter != null && presenter.CurrentDepth > maxDepth) maxDepth = presenter.CurrentDepth;
            }
            recorder.Dispose();

            // 벽시계와 누적 프레임타임이 크게 어긋나면 에디터가 프레임을 스로틀했다는 뜻이다.
            // 그 상태의 프레임타임 통계는 빌드 성능을 대표하지 않는다.
            float wall = Time.realtimeSinceStartup - wallStart;
            _report.AppendLine($"[측정 신뢰도] 벽시계 {wall:F1}s vs 누적 프레임타임 {accumulated:F1}s " +
                               $"(비 {wall / Mathf.Max(0.01f, accumulated):F1}×)");
            if (wall > accumulated * 1.5f)
                _report.AppendLine("  → 어긋남. 에디터가 프레임을 스로틀했다(포커스 상실 등). " +
                                   "이 구간의 프레임타임 수치는 빌드 성능의 근거로 쓸 수 없다.");
            _report.AppendLine();

            if (frames.Count == 0) { _report.AppendLine("[스핀·캐스케이드 중] 표본 없음"); yield break; }

            var times = new float[frames.Count];
            var allocs = new long[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                times[i] = frames[i].Milliseconds;
                allocs[i] = frames[i].GcAlloc;
            }
            System.Array.Sort(times);
            float median = times[times.Length / 2];
            float p95 = times[Mathf.Min(times.Length - 1, Mathf.RoundToInt(times.Length * 0.95f))];
            float worst = times[times.Length - 1];

            _report.AppendLine($"[스핀·캐스케이드 재생 중] {frames.Count}프레임 / 스핀 {spins}회 / 최대 연쇄 {maxDepth}단계");
            _report.AppendLine($"  프레임타임 중앙 {median:F2} ms ({1000f / Mathf.Max(0.01f, median):F0} FPS) / " +
                               $"95% {p95:F2} ms / 최악 {worst:F2} ms");
            AppendAllocStats(allocs, allocs.Length);
            _report.AppendLine($"  스파이크 판정: 최악 프레임이 중앙의 {worst / Mathf.Max(0.01f, median):F1}배");
            _report.AppendLine();

            ReportSpikes(frames, median);

            _report.AppendLine("[측정 후 폰트 상태]");
            foreach (TMP_FontAsset font in _fonts)
                _report.AppendLine($"  {font.name}: 글리프 {font.characterTable.Count} / " +
                                   $"아틀라스 {font.atlasTextures.Length}장");
            _report.AppendLine();
        }

        /// <summary>
        /// **중앙값과 0B 프레임 비율을 함께 낸다.** 평균만 보면 판단을 그르친다 —
        /// 에디터는 셰이더 컴파일·에셋 임포트로 수십 MB짜리 프레임을 이따금 섞고,
        /// 180프레임 표본에서 22MB 한 방이면 평균이 124KB 올라간다.
        /// "게임 루프가 매 프레임 할당하는가"는 중앙값과 0B 프레임 비율이 답한다.
        /// </summary>
        private void AppendAllocStats(long[] alloc, int count)
        {
            var valid = new System.Collections.Generic.List<long>(count);
            for (int i = 0; i < count; i++)
                if (alloc[i] >= 0) valid.Add(alloc[i]);

            if (valid.Count == 0)
            {
                _report.AppendLine("  GC Alloc 측정 불가 (ProfilerRecorder 무효)");
                return;
            }

            valid.Sort();
            long median = valid[valid.Count / 2];
            long p95 = valid[Mathf.Min(valid.Count - 1, Mathf.RoundToInt(valid.Count * 0.95f))];
            long max = valid[valid.Count - 1];

            long sum = 0;
            int zeroFrames = 0;
            foreach (long a in valid)
            {
                sum += a;
                if (a == 0) zeroFrames++;
            }

            _report.AppendLine($"  GC Alloc 중앙 {median} B / 95% {p95} B / 최대 {max} B / " +
                               $"평균 {sum / (float)valid.Count:F0} B");
            _report.AppendLine($"  0 B 프레임 {zeroFrames}/{valid.Count} " +
                               $"({zeroFrames * 100f / valid.Count:F0}%)");
        }

        /// <summary>
        /// 같은 재생을 조건만 바꿔 반복하고 스파이크 수·최대 할당을 비교한다.
        /// 시드를 고정하므로 세 번 모두 **같은 연쇄**가 재생된다 — 비교가 성립하는 근거다.
        /// </summary>
        private IEnumerator BisectPresentation(RunSessionBehaviour run, RouletteInteractionBridge bridge,
                                               InteractableLever lever, string label,
                                               bool hudOn, bool markersOn)
        {
            var hud = FindAnyObjectByType<UI.GameHudView>();
            var markers = FindAnyObjectByType<PurifyMarkerView>();
            if (hud != null) hud.enabled = hudOn;
            if (markers != null) markers.enabled = markersOn;

            run.ResetRun(12);   // 연쇄가 깊게 나오는 시드
            yield return null;
            yield return null;
            for (int i = 0; i < 30; i++) yield return null;

            // recorder 앞에서 잡는다 — 아래 두 리스트는 합쳐 약 49 KB 이고,
            // 뒤에서 잡으면 그 49 KB 가 arm 의 첫 프레임 할당으로 기록된다.
            var times = new List<float>(4096);
            var allocs = new List<long>(4096);
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            // 수거가 몇 번 일어났는지 함께 남긴다. 스파이크가 수거와 같이 움직이면
            // 그건 "그 컴포넌트를 그리는 비용"이 아니라 "그때 힙이 찼다"는 뜻이다.
            int gc0 = System.GC.CollectionCount(0);
            int gc1 = System.GC.CollectionCount(1);

            float deadline = Time.realtimeSinceStartup + 45f;
            int spins = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (!bridge.IsLocked)
                {
                    if (floor.Phase == FloorPhase.ContractSelection) run.Session.SelectContract(2);
                    else if (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0)
                    { lever.Interact(gameObject); spins++; }
                    else break;
                }
                yield return null;
                times.Add(Time.unscaledDeltaTime * 1000f);
                allocs.Add(recorder.Valid ? recorder.LastValue : -1);
            }
            recorder.Dispose();

            if (times.Count == 0) yield break;

            var sortedTimes = times.ToArray();
            System.Array.Sort(sortedTimes);
            float median = sortedTimes[sortedTimes.Length / 2];
            float worst = sortedTimes[sortedTimes.Length - 1];

            long maxAlloc = 0, sumAlloc = 0;
            int spikes = 0;
            float threshold = Mathf.Max(median * 8f, 5f);
            foreach (long a in allocs) { if (a > maxAlloc) maxAlloc = a; if (a > 0) sumAlloc += a; }
            foreach (float t in times) if (t > threshold) spikes++;

            _report.AppendLine($"  {label,-18} 프레임 {times.Count,5} / 스핀 {spins} / " +
                               $"중앙 {median:F2} ms / 최악 {worst:F1} ms / " +
                               $"스파이크 {spikes,3}개 / 최대할당 {maxAlloc / 1024f / 1024f:F1} MB / " +
                               $"총할당 {sumAlloc / 1024f / 1024f:F0} MB / " +
                               $"수거 g0 {System.GC.CollectionCount(0) - gc0} g1 {System.GC.CollectionCount(1) - gc1}");

            if (hud != null) hud.enabled = true;
            if (markers != null) markers.enabled = true;
        }

        /// <summary>
        /// 스핀 기록이 쌓인 **같은 게임 상태**에서 HUD만 껐다 켜며 잰다.
        /// 앞의 유휴 측정은 기록이 없어 결과판 미러가 안 그려지던 상태라 비교 대상이 아니었다.
        /// </summary>
        private IEnumerator MeasureHudContribution()
        {
            var hud = FindAnyObjectByType<UI.GameHudView>();
            if (hud == null) yield break;

            hud.enabled = true;
            for (int i = 0; i < 60; i++) yield return null;
            yield return MeasureFrames("스핀 이후 — 화면 UI 켬", 300);

            hud.enabled = false;
            for (int i = 0; i < 60; i++) yield return null;
            yield return MeasureFrames("스핀 이후 — 화면 UI 끔", 300);

            hud.enabled = true;
            for (int i = 0; i < 30; i++) yield return null;
        }

        /// <summary>
        /// 게임 코드를 전부 끄고 같은 시간을 재는 대조군. 여기서도 같은 스파이크가 나오면
        /// 원인은 게임 루프가 아니라 에디터·URP 쪽이다.
        /// </summary>
        private IEnumerator MeasureIdleSpikes(float seconds)
        {
            // **전수 비활성이다 — 손으로 열거하지 않는다.**
            //
            // 이전 판본은 `Disable<T>()` 를 12번 부르는 열거였고, 그래서 `AudioDirector`·
            // `PaperTapePrinterView`·`FloorIndicatorView`·`PassengerReactionView`·
            // `TubeController`×3·`OverharvestApproachBridge`·`RiskEventBridge`·
            // `OverharvestUnlockEffect`·`InteractableOverharvestLever`·
            // `TelemetryRecorderBehaviour`·`RenderBudgetProbe`·`MemoryTrendProbe`·
            // `RunSessionBehaviour` 가 「게임 코드 전부 끔」 구간에서 계속 돌았다.
            // 즉 8,805 B/프레임 바닥 **안에 게임 코드가 섞여 있었고**, 그 위에서 잰
            // 1,638 B 차이가 「게임 코드 전체 비용」으로 읽혔다.
            //
            // 열거는 새 컴포넌트를 계속 놓친다 — `BuildFigureView` 누락 때와 같은 맹점이다.
            // 자기 자신만 빼고 전부 끈다. 카메라·라이트는 `MonoBehaviour` 가 아니므로
            // 렌더 파이프라인은 그대로 돌고, 그것이 재려는 바닥이다.
            var suspects = new List<MonoBehaviour>();
            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null || !mb.enabled) continue;
                if (ReferenceEquals(mb, this)) continue;   // 이 코루틴을 돌리는 주체다
                mb.enabled = false;
                suspects.Add(mb);
            }
            _report.AppendLine($"  [대조군] 비활성화한 MonoBehaviour {suspects.Count}개 " +
                               $"(이전 판본은 손 열거 12개였다)");

            for (int i = 0; i < 120; i++) yield return null;

            // recorder 앞에서 잡는다. 대조군은 60초를 도는 동안 리스트가 4096→8192 로
            // 성장하는데, 그 성장(8192×48 = 393,216 B)이 「대조군 최대 402,053 B」로
            // 기록돼 있었다 — 게임 코드를 전부 껐다는 구간의 최악값이 측정 도구였다.
            var frames = new List<FrameSample>(16384);
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            int index = 0;
            float deadline = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                frames.Add(new FrameSample
                {
                    Index = index++,
                    Milliseconds = Time.unscaledDeltaTime * 1000f,
                    GcAlloc = recorder.Valid ? recorder.LastValue : -1,
                    Gen0 = System.GC.CollectionCount(0),
                    Gen1 = System.GC.CollectionCount(1),
                    Gen2 = System.GC.CollectionCount(2),
                    TmpGlyphs = TotalGlyphs(),
                    TmpAtlasTextures = TotalAtlasTextures(),
                    PresenterDepth = 0,
                    SpinTriggered = false,
                });
            }
            recorder.Dispose();

            foreach (MonoBehaviour behaviour in suspects)
                if (behaviour != null) behaviour.enabled = true;

            if (frames.Count == 0) yield break;

            var times = new float[frames.Count];
            var allocs = new long[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                times[i] = frames[i].Milliseconds;
                allocs[i] = frames[i].GcAlloc;
            }
            System.Array.Sort(times);
            float median = times[times.Length / 2];
            float p95 = times[Mathf.Min(times.Length - 1, Mathf.RoundToInt(times.Length * 0.95f))];
            float worst = times[times.Length - 1];

            _report.AppendLine($"[대조군 — 게임 코드 전부 끄고 {seconds:F0}초] {frames.Count}프레임");
            _report.AppendLine($"  프레임타임 중앙 {median:F2} ms / 95% {p95:F2} ms / 최악 {worst:F2} ms");
            AppendAllocStats(allocs, allocs.Length);
            ReportSpikes(frames, median);
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
