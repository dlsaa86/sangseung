#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// **빌드 안에서** 프레임타임과 GC 할당을 잰다.
    ///
    /// ## 왜 에디터 측정으로는 안 되는가
    ///
    /// `UP-TECH-04`(1080p 90 FPS)와 `UP-TECH-05`(매 프레임 0 B)를 에디터에서 재면
    /// 둘 다 판정할 수 없다. 실측으로 확인된 것 둘:
    ///
    /// - **프레임타임이 120 Hz 상한에 붙는다.** 네 조건 전부 중앙 8.33 ms 였다.
    ///   그건 비용이 아니라 상한이고, 90 FPS 목표에 여유가 있는지 없는지를 못 말한다.
    /// - **GC 에 에디터·프로파일러 자신의 할당이 섞인다.** 0 B 프레임이 0/180 이었는데,
    ///   그것이 게임 코드 탓인지 에디터 탓인지 그 측정으로는 갈리지 않는다.
    ///
    /// 빌드에는 둘 다 없다. 그래서 **같은 질문을 빌드에 다시 묻는다.**
    ///
    /// ## 왜 커맨드라인으로만 켜는가
    ///
    /// 이 프로브가 평소 실행에 붙으면 그 자체가 비용이고, 측정하려던 것을 오염시킨다.
    /// `-ascend-perf` 가 있을 때만 깨어난다. 없으면 이 클래스는 아무것도 하지 않는다.
    /// 그리고 `DEVELOPMENT_BUILD` 밖에서는 **컴파일조차 되지 않는다** — 배포 빌드에
    /// 측정 코드가 실려 나가는 사고를 문법 수준에서 막는다.
    /// </summary>
    public static class PlayerPerfProbe
    {
        private const string Arg = "-ascend-perf";
        // 워밍업 720 프레임(6초 @120Hz). 처음엔 120 이었다.
        //
        // **한 번 잘못 읽었다.** 소거 구간(본 측정 뒤)의 기준선이 0 B 로 나오길래
        // 「1,638 B 는 정상 상태가 아니라 덜 끝난 워밍업」이라고 단정하고 워밍업을 늘렸다.
        // **틀렸다** — 720 으로 늘려도 본 측정은 여전히 1,638 B 다. 그때 소거 기준선이
        // 0 이었던 것은 워밍업 때문이 아니라 **그 순간 게임 상태가 달랐기 때문**이다
        // (결과판이 돌지 않는 국면).
        //
        // 늘린 것 자체는 남긴다 — 워밍업은 길수록 안전하고, 720 에서도 결론이 같다는 것이
        // 오히려 「이 값은 워밍업 길이에 의존하지 않는다」는 증거가 된다.
        private const int WarmupFrames = 720;
        private const int MeasureFrames = 600;  // 60 Hz 기준 10초

        /// <summary>이만큼 연속으로 0 B 면 「안정화됐다」로 본다.</summary>
        private const int SettleRunFrames = 60;

        /// <summary>안정화를 기다리는 상한. 넘으면 「안정화되지 않는다」를 결과로 적는다.</summary>
        private const int SettleTimeoutFrames = 3600;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            bool armed = false;
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (string.Equals(a, Arg, System.StringComparison.OrdinalIgnoreCase)) { armed = true; break; }
            if (!armed) return;

            var go = new GameObject("[PlayerPerfProbe]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                // **상한을 푼다.** 목표가 90 FPS 인데 VSync 가 60 이나 120 으로 묶어 두면
                // 잰 값이 능력이 아니라 상한이 된다 — 에디터 측정이 정확히 그래서 못 썼다.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);

                for (int i = 0; i < WarmupFrames; i++) yield return null;

                // **프레임 수로 「워밍업 끝」을 정하면 실행마다 다른 답이 나온다.**
                // 같은 빌드를 두 번 돌렸더니 하나는 1,638 B, 하나는 0 B 였다 —
                // 차이는 코드가 아니라 그 순간 결과판이 도는 국면이었는가였다.
                // 720 프레임이 끝난 시점이 어느 국면인지는 아무도 보장하지 않는다.
                //
                // 그래서 **관측으로 정한다** — 연속 60 프레임이 0 B 이면 안정화된 것으로 본다.
                // 안정화가 관측되면 그 뒤를 재고, 끝내 안 되면 **그 사실 자체를 적는다.**
                // 「안정화되지 않는다」도 판정이다. 못 재서 침묵하는 것보다 낫다.
                bool settled = false;
                int settleWaited = 0;
                {
                    var srec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                    int run0 = 0;
                    for (settleWaited = 0; settleWaited < SettleTimeoutFrames; settleWaited++)
                    {
                        yield return null;
                        long v = srec.Valid ? srec.LastValue : -1;
                        run0 = v == 0 ? run0 + 1 : 0;
                        if (run0 >= SettleRunFrames) { settled = true; break; }
                    }
                    srec.Dispose();
                }

                // **버퍼를 recorder 보다 먼저 잡는다.** 뒤에 잡으면 그 할당이 첫 표본에 들어가
                // 하네스가 자기 비용을 게임 비용으로 보고한다(같은 실수를 에디터 프로브에서 고쳤다).
                var ms = new float[MeasureFrames];
                var gc = new long[MeasureFrames];
                var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

                for (int i = 0; i < MeasureFrames; i++)
                {
                    yield return null;
                    ms[i] = Time.unscaledDeltaTime * 1000f;
                    gc[i] = recorder.Valid ? recorder.LastValue : -1;
                }
                recorder.Dispose();

                Write(ms, gc, settled, settleWaited);
                yield return Ablate();
                Application.Quit(0);
            }

            /// <summary>
            /// **컴포넌트를 하나씩 끄고 GC 차이를 잰다.** 1,638 B/프레임의 출처를 찾는 유일한
            /// 신뢰 가능한 방법이다.
            ///
            /// 정적으로 찾으려 했으나 Update 를 가진 파일이 48개고, 이 세션에서 추측으로
            /// 네 번 틀렸다. 코드를 읽어 「여기일 것 같다」로 고르는 대신 **끄고 재서** 고른다.
            ///
            /// 각 구간은 짧다(90 프레임). 절대값이 아니라 **켰을 때와의 차이**만 보므로
            /// 구간이 짧아도 된다 — 같은 프레임 조건에서 한 컴포넌트만 달라진다.
            /// </summary>
            private IEnumerator Ablate()
            {
                const int Seg = 90;
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("=== 컴포넌트 소거 측정 — 1,638 B/프레임 출처 추적 ===");
                sb.AppendLine($"각 구간 {Seg} 프레임 · 값은 그 구간 GC 중앙값(B/프레임)");
                sb.AppendLine("차이 = (전부 켬) − (그것만 끔). 양수면 그 컴포넌트가 그만큼 쓴다.");
                sb.AppendLine();

                // 씬의 모든 게임 MonoBehaviour 를 모은다. 목록을 손으로 적으면 적히지 않은
                // 것이 **구조적으로 상쇄**되어 무엇을 고쳐도 수치가 안 움직인다 —
                // 이 저장소가 이미 그 맹점을 한 번 겪었다(HeroSlicePerfProbe 의 손 열거).
                var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                var targets = new System.Collections.Generic.List<MonoBehaviour>();
                foreach (MonoBehaviour mb in all)
                {
                    if (mb == null || !mb.enabled) continue;
                    if (mb is Runner) continue;                       // 자기 자신
                    System.Type t = mb.GetType();
                    if (t.Namespace == null || !t.Namespace.StartsWith("Ascend")) continue;
                    targets.Add(mb);
                }

                // **먼저 `SpinBoardView` 안을 갈라 잰다.** 컴포넌트 단위 소거는 이미
                // 그것을 지목했고, 안에서 어느 부분인지가 남은 질문이다.
                long segAll = 0, segNoHi = 0, segNoUpdate = 0;
                yield return Measure(Seg, v => segAll = v);
                Ascend.Prototype.View.SpinBoardView.DiagnosticSkip = 1;
                yield return Measure(Seg, v => segNoHi = v);
                Ascend.Prototype.View.SpinBoardView.DiagnosticSkip = 2;
                yield return Measure(Seg, v => segNoUpdate = v);
                Ascend.Prototype.View.SpinBoardView.DiagnosticSkip = 0;
                sb.AppendLine("[SpinBoardView 내부 분해]");
                sb.AppendLine($"  정상                     {segAll} B/프레임");
                sb.AppendLine($"  ApplyHighlights 만 끔    {segNoHi} B/프레임  (차이 {segAll - segNoHi})");
                sb.AppendLine($"  Update 전체 끔           {segNoUpdate} B/프레임  (차이 {segAll - segNoUpdate})");
                sb.AppendLine();

                long baseAlloc = 0;
                yield return Measure(Seg, v => baseAlloc = v);
                sb.AppendLine($"[전부 켬] {baseAlloc} B/프레임 · 대상 컴포넌트 {targets.Count}개");
                sb.AppendLine();

                var rows = new System.Collections.Generic.List<string>();
                foreach (MonoBehaviour mb in targets)
                {
                    if (mb == null) continue;
                    mb.enabled = false;
                    long off = 0;
                    yield return Measure(Seg, v => off = v);
                    mb.enabled = true;
                    long delta = baseAlloc - off;
                    if (delta != 0)
                        rows.Add($"  {delta,7} B  {mb.GetType().Name} ({mb.gameObject.name})");
                }
                rows.Sort((a, b) => b.CompareTo(a));   // 문자열 정렬이지만 폭 고정이라 수 순서와 같다
                if (rows.Count == 0) sb.AppendLine("  차이를 낸 컴포넌트가 없다 — 할당이 Update 밖(엔진·렌더)이다");
                else foreach (string r in rows) sb.AppendLine(r);

                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "player_perf.txt");
                System.IO.File.AppendAllText(path, sb.ToString());
                Debug.Log("[상승] 소거 측정 기록 완료");
            }

            /// <summary>구간 하나의 GC 중앙값. 버퍼를 recorder 앞에서 잡는 규칙은 여기도 같다.</summary>
            private static IEnumerator Measure(int frames, System.Action<long> done)
            {
                var buf = new long[frames];
                var rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    buf[i] = rec.Valid ? rec.LastValue : 0;
                }
                rec.Dispose();
                System.Array.Sort(buf);
                done(buf[frames / 2]);
            }

            private static void Write(float[] ms, long[] gc, bool settled, int settleWaited)
            {
                var sorted = (float[])ms.Clone();
                System.Array.Sort(sorted);
                float median = sorted[sorted.Length / 2];
                float p95 = sorted[Mathf.Min(sorted.Length - 1, Mathf.RoundToInt(sorted.Length * 0.95f))];
                float worst = sorted[sorted.Length - 1];

                long sum = 0, gcWorst = 0; int valid = 0, zero = 0;
                for (int i = 0; i < gc.Length; i++)
                {
                    if (gc[i] < 0) continue;
                    valid++; sum += gc[i];
                    if (gc[i] == 0) zero++;
                    if (gc[i] > gcWorst) gcWorst = gc[i];
                }
                long gcMedian = -1;
                if (valid > 0)
                {
                    var v = new long[valid]; int k = 0;
                    for (int i = 0; i < gc.Length; i++) if (gc[i] >= 0) v[k++] = gc[i];
                    System.Array.Sort(v);
                    gcMedian = v[valid / 2];
                }

                // 하드 플로어 60 FPS 는 **최악 프레임**으로 본다. 평균이 넘어도 한 프레임이
                // 16.7 ms 를 넘으면 그 순간 끊긴 것이고, 요구는 그 끊김을 금지한다.
                int over16 = 0, over11 = 0;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] > 16.67f) over16++;
                    if (ms[i] > 11.11f) over11++;
                }

                var sb = new StringBuilder();
                sb.AppendLine("=== 빌드 성능 측정 (PlayerPerfProbe) ===");
                sb.AppendLine($"해상도 {Screen.width}×{Screen.height} · vSync {QualitySettings.vSyncCount} · " +
                              $"targetFrameRate {Application.targetFrameRate}");
                sb.AppendLine($"워밍업 {WarmupFrames} 프레임 버림 · 측정 {MeasureFrames} 프레임");
                sb.AppendLine(settled
                    ? $"**안정화 관측됨** — 워밍업 뒤 {settleWaited} 프레임 만에 연속 {SettleRunFrames} 프레임 0 B 도달. " +
                      "이 뒤를 잰다."
                    : $"**안정화되지 않았다** — {SettleTimeoutFrames} 프레임을 기다려도 연속 {SettleRunFrames} 프레임 " +
                      "0 B 가 한 번도 안 나왔다. 아래 수치는 안정화 이전 상태를 잰 것이다.");
                sb.AppendLine();
                sb.AppendLine("[UP-TECH-04] 1080p 목표 90 FPS / 하드 플로어 60 FPS");
                sb.AppendLine($"  프레임타임 중앙 {median:F2} ms ({1000f / Mathf.Max(0.01f, median):F0} FPS) / " +
                              $"95% {p95:F2} ms / 최악 {worst:F2} ms");
                sb.AppendLine($"  90 FPS(11.11 ms) 초과 프레임 {over11}/{ms.Length} · " +
                              $"**60 FPS(16.67 ms) 초과 프레임 {over16}/{ms.Length}**");
                sb.AppendLine(over16 == 0
                    ? "  → 하드 플로어 60 FPS: **충족** (16.67 ms 를 넘은 프레임이 없다)"
                    : $"  → 하드 플로어 60 FPS: **미충족** ({over16} 프레임이 16.67 ms 를 넘었다)");
                sb.AppendLine(over11 == 0
                    ? "  → 목표 90 FPS: **충족**"
                    : $"  → 목표 90 FPS: 미충족 ({over11} 프레임이 11.11 ms 를 넘었다)");
                sb.AppendLine();
                sb.AppendLine("[UP-TECH-05] 워밍업 후 매 프레임 0 B GC Alloc");
                if (valid > 0)
                {
                    sb.AppendLine($"  GC Alloc 중앙 {gcMedian} B/프레임 / 평균 {(double)sum / valid:F0} B / " +
                                  $"최악 {gcWorst} B / 유효 표본 {valid}/{gc.Length}");
                    sb.AppendLine($"  **0 B 프레임 {zero}/{valid}** ({100.0 * zero / valid:F1}%)");
                    sb.AppendLine(zero == valid
                        ? "  → **충족** (측정 구간의 모든 프레임이 0 B)"
                        : $"  → **미충족** ({valid - zero} 프레임이 0 B 가 아니다). " +
                          "빌드이므로 에디터·프로파일러 할당은 섞이지 않는다 — 이 수는 게임 코드다.");

                    // **0 이 아닌 프레임이 언제 나왔는지 적는다.**
                    // 「이벤트성 스파이크」와 「상시 누수」는 고칠 곳이 완전히 다른데
                    // 개수만으로는 갈리지 않는다. 흩어져 있으면 상시, 뭉쳐 있으면 이벤트다.
                    if (zero < valid)
                    {
                        var idx = new StringBuilder();
                        int shown = 0, prev = -99; int runs = 0;
                        for (int i = 0; i < gc.Length; i++)
                        {
                            if (gc[i] <= 0) continue;
                            if (i != prev + 1) runs++;          // 연속 구간의 시작
                            prev = i;
                            if (shown < 25) { idx.Append(i).Append('(').Append(gc[i]).Append(") "); shown++; }
                        }
                        sb.AppendLine($"  0 이 아닌 프레임 위치(앞 {shown}개): {idx}");
                        sb.AppendLine($"  **연속 구간 {runs}개** — 구간 수가 프레임 수보다 훨씬 적으면 " +
                                      "이벤트성(한 사건이 여러 프레임에 걸침)이고, 비슷하면 흩어진 상시 할당이다.");
                    }
                }
                else sb.AppendLine("  ProfilerRecorder 무효 — 측정 불가");

                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "player_perf.txt");
                System.IO.File.WriteAllText(path, sb.ToString());
                Debug.Log("[상승] 빌드 성능 측정 기록 → " + path);
            }
        }
    }
}
#endif
