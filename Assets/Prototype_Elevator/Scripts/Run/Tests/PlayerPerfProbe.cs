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
        private const int WarmupFrames = 120;   // 셰이더·풀 워밍업. 이 구간은 버린다
        private const int MeasureFrames = 600;  // 60 Hz 기준 10초

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

                Write(ms, gc);
                Application.Quit(0);
            }

            private static void Write(float[] ms, long[] gc)
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
