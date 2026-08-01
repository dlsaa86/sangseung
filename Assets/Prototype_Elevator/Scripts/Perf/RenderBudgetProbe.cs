using System;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Ascend.Prototype.Perf
{
    /// <summary>
    /// 렌더링 예산 측정기. `TECH_SPEC.md` §13 이 요구하는 것 중
    /// **"최악 장면에서 무엇이 비쌌는가"** 를 숫자로 남긴다 — 드로우콜, SetPass,
    /// 삼각형, 프레임타임.
    ///
    /// 왜 프레임 디버거로 충분하지 않은가: 프레임 디버거는 사람이 그 순간 화면을
    /// 보고 있어야 한다. 최악 프레임은 캐스케이드 중이거나 Collapse 연출 중에 오는데,
    /// 그때 사람은 게임을 보고 있지 디버거를 열고 있지 않다. 그래서 계속 재고
    /// 최악을 기억하는 쪽이 필요하다.
    ///
    /// **0 을 좋은 값으로 오해하게 두지 않는다.** 프로파일러 카운터가 잡히지 않는
    /// 실행 환경(비개발 빌드 등)에서는 값이 없는 것이지 드로우콜이 0인 것이 아니다.
    /// 그런 표본은 −1 로 저장하고 보고서에 "측정 불가"라고 못박는다. 성능 보고서가
    /// 거짓 안심을 주는 것이 측정을 못 하는 것보다 나쁘다.
    ///
    /// 예산은 float 로 주입받는다. <c>VisualQualityProfile</c> 같은 에셋을 직접
    /// 참조하지 않는 이유는, 그 타입이 아직 없을 수 있고 없다고 해서 측정까지
    /// 멈춰서는 안 되기 때문이다. 예산이 없으면 대조 없이 값만 남긴다.
    /// </summary>
    public sealed class RenderBudgetProbe : MonoBehaviour
    {
        public const string ReportPath = "Logs/render_budget.txt";

        /// <summary>
        /// 중앙값 창의 크기. 링 버퍼라 이보다 오래된 표본은 잊는다 —
        /// 대신 최악값은 전 구간에 걸쳐 따로 기억한다. 측정기가 스스로
        /// 무한히 자라면 그것이 곧 측정 대상을 오염시킨다.
        /// </summary>
        private const int Capacity = 600;

        [Header("샘플링")]
        [Tooltip("매 프레임 자동으로 찍는다. 끄면 Sample() 을 직접 부른다.")]
        [SerializeField] private bool _autoSample = true;

        [Tooltip("몇 프레임마다 찍는가. 1이면 매 프레임.")]
        [SerializeField] private int _sampleEveryNFrames = 1;

        [Tooltip("처음 이만큼의 프레임은 버린다. 워밍업을 최악값으로 오해하지 않기 위해서다.")]
        [SerializeField] private int _warmupFrames = 60;

        [Header("예산 (0 이면 대조하지 않는다)")]
        [SerializeField] private float _drawCallBudget;
        [SerializeField] private float _setPassBudget;
        [SerializeField] private float _triangleBudget;
        [SerializeField] private float _frameMsBudget;

        [Header("보고")]
        [Tooltip("파괴될 때 자동으로 Logs/render_budget.txt 를 쓴다.")]
        [SerializeField] private bool _writeOnDestroy = true;

        // 고정 크기 링 버퍼. 측정기가 스스로 할당을 만들면 재는 대상이 오염된다.
        private readonly long[] _drawCalls = new long[Capacity];
        private readonly long[] _setPass = new long[Capacity];
        private readonly long[] _triangles = new long[Capacity];
        private readonly float[] _frameMs = new float[Capacity];
        private readonly long[] _sortLongs = new long[Capacity];
        private readonly float[] _sortFloats = new float[Capacity];

        private ProfilerRecorder _drawCallRecorder;
        private ProfilerRecorder _setPassRecorder;
        private ProfilerRecorder _triangleRecorder;

        private int _write;
        private int _stored;
        private int _frameCounter;

        private long _worstDrawCalls = -1;
        private long _worstSetPass = -1;
        private long _worstTriangles = -1;
        private float _worstFrameMs;

        private bool _drawCallsMeasurable;
        private bool _setPassMeasurable;
        private bool _trianglesMeasurable;
#if UNITY_EDITOR
        private bool _editorFallbackUsed;
#endif

        /// <summary>지금까지 찍은 총 표본 수. 링 버퍼 용량과 별개다.</summary>
        public int TotalSamples { get; private set; }

        /// <summary>예산이 하나라도 주입됐는가.</summary>
        public bool HasBudget
        {
            get
            {
                return _drawCallBudget > 0f || _setPassBudget > 0f ||
                       _triangleBudget > 0f || _frameMsBudget > 0f;
            }
        }

        /// <summary>
        /// 예산을 넣는다. 0 이나 음수는 "이 항목은 대조하지 않는다"는 뜻이다 —
        /// 확정되지 않은 예산을 0 으로 두고 항상 초과라고 외치는 것보다,
        /// 대조하지 않는다고 말하는 쪽이 정직하다.
        /// </summary>
        public void SetBudgets(float drawCalls, float setPassCalls, float triangles, float frameMs)
        {
            _drawCallBudget = drawCalls;
            _setPassBudget = setPassCalls;
            _triangleBudget = triangles;
            _frameMsBudget = frameMs;
        }

        private void OnEnable()
        {
            // 이름은 Unity 내장 렌더 프로파일러 카운터의 것이다. 틀리면 예외가 아니라
            // Valid == false 로 돌아온다 — 그 경우를 "측정 불가"로 기록한다.
            _drawCallRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangleRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        private void OnDisable()
        {
            _drawCallRecorder.Dispose();
            _setPassRecorder.Dispose();
            _triangleRecorder.Dispose();
        }

        private void LateUpdate()
        {
            if (!_autoSample) return;

            _frameCounter++;
            if (_frameCounter <= _warmupFrames) return;

            int stride = _sampleEveryNFrames > 0 ? _sampleEveryNFrames : 1;
            if ((_frameCounter - _warmupFrames) % stride != 0) return;

            Sample();
        }

        /// <summary>한 프레임 스냅샷. 값을 못 얻은 항목은 −1 로 남긴다.</summary>
        public void Sample()
        {
            long draw = _drawCallRecorder.Valid ? _drawCallRecorder.LastValue : -1L;
            long setPass = _setPassRecorder.Valid ? _setPassRecorder.LastValue : -1L;
            long tris = _triangleRecorder.Valid ? _triangleRecorder.LastValue : -1L;

#if UNITY_EDITOR
            // 에디터에서는 프로파일러 카운터가 비어 있어도 통계 창의 원본을 읽을 수 있다.
            // 빌드에는 이 경로가 없으므로 보고서에 "에디터 통계로 보정했다"고 적는다.
            bool fellBack = false;
            if (draw < 0) { draw = UnityEditor.UnityStats.drawCalls; fellBack = true; }
            if (setPass < 0) { setPass = UnityEditor.UnityStats.setPassCalls; fellBack = true; }
            if (tris < 0) { tris = UnityEditor.UnityStats.triangles; fellBack = true; }
            if (fellBack) _editorFallbackUsed = true;
#endif

            if (draw >= 0) _drawCallsMeasurable = true;
            if (setPass >= 0) _setPassMeasurable = true;
            if (tris >= 0) _trianglesMeasurable = true;

            float ms = Time.unscaledDeltaTime * 1000f;

            _drawCalls[_write] = draw;
            _setPass[_write] = setPass;
            _triangles[_write] = tris;
            _frameMs[_write] = ms;

            _write = (_write + 1) % Capacity;
            if (_stored < Capacity) _stored++;
            TotalSamples++;

            if (draw > _worstDrawCalls) _worstDrawCalls = draw;
            if (setPass > _worstSetPass) _worstSetPass = setPass;
            if (tris > _worstTriangles) _worstTriangles = tris;
            if (ms > _worstFrameMs) _worstFrameMs = ms;
        }

        /// <summary>측정을 처음부터 다시 시작한다. 조건을 바꿔 다시 잴 때 쓴다.</summary>
        public void ResetSamples()
        {
            _write = 0;
            _stored = 0;
            _frameCounter = 0;
            TotalSamples = 0;
            _worstDrawCalls = -1;
            _worstSetPass = -1;
            _worstTriangles = -1;
            _worstFrameMs = 0f;
        }

        /// <summary>사람이 읽는 보고서. 최악·중앙값과 예산 대조를 담는다.</summary>
        public string Report()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("=== 렌더링 예산 측정 (UP-TECH-07) ===");
            sb.AppendLine("기기 " + SystemInfo.processorType + " / " + SystemInfo.graphicsDeviceName +
                          " / " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.operatingSystem);
            sb.AppendLine("해상도 " + Screen.width + "×" + Screen.height +
                          " / vSync " + QualitySettings.vSyncCount +
                          " / targetFrameRate " + Application.targetFrameRate);
            sb.AppendLine("표본 " + TotalSamples + "개 (중앙값 창 " + _stored + "/" + Capacity +
                          ", 워밍업 " + _warmupFrames + "프레임 제외)");
#if UNITY_EDITOR
            sb.AppendLine("이것은 **에디터 Play 모드** 측정이다. 빌드 성능이 아니다.");
            if (_editorFallbackUsed)
                sb.AppendLine("일부 값은 UnityStats(에디터 전용)로 보정했다. 빌드에는 이 경로가 없다.");
#endif
            sb.AppendLine("기준 하드웨어가 확정되지 않았으므로(`ASSUMPTION_LOG` A-20260730-01)");
            sb.AppendLine("이 수치로 성능 완료를 선언하지 않는다. `TECH_SPEC.md` §13 대조는 Pass 4 다.");
            sb.AppendLine();

            if (_stored == 0)
            {
                sb.AppendLine("표본이 없다. 측정하지 않았다 — 이 보고서는 통과 근거가 될 수 없다.");
                return sb.ToString();
            }

            AppendLongLine(sb, "드로우콜", _drawCalls, _worstDrawCalls, _drawCallsMeasurable, _drawCallBudget);
            AppendLongLine(sb, "SetPass", _setPass, _worstSetPass, _setPassMeasurable, _setPassBudget);
            AppendLongLine(sb, "삼각형", _triangles, _worstTriangles, _trianglesMeasurable, _triangleBudget);
            AppendFrameLine(sb);

            sb.AppendLine();
            if (!HasBudget)
                sb.AppendLine("예산이 주입되지 않았다. 값만 기록했고 판정은 하지 않았다.");
            return sb.ToString();
        }

        /// <summary>보고서를 파일로 남긴다. 실패해도 게임을 죽이지 않되 조용히 넘기지 않는다.</summary>
        public bool WriteReport()
        {
            string body = Report();
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, body);
                Debug.Log("[상승] 렌더링 예산 측정 → " + ReportPath + "\n" + body);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[상승] 렌더링 예산 보고서 쓰기 실패: " + e.Message);
                return false;
            }
        }

        private void OnDestroy()
        {
            if (_writeOnDestroy && TotalSamples > 0) WriteReport();
        }

        private void AppendLongLine(StringBuilder sb, string label, long[] ring,
                                    long worst, bool measurable, float budget)
        {
            if (!measurable)
            {
                // 여기서 0 을 적으면 나중에 누군가 "드로우콜 0, 훌륭하다"고 읽는다.
                sb.AppendLine("[" + label + "] 측정 불가 — 프로파일러 카운터가 이 실행 환경에 없다. " +
                              "0 이 아니라 값이 없는 것이다.");
                return;
            }

            int valid = 0;
            for (int i = 0; i < _stored; i++)
            {
                if (ring[i] < 0) continue;
                _sortLongs[valid++] = ring[i];
            }
            if (valid == 0)
            {
                sb.AppendLine("[" + label + "] 유효 표본 0 — 측정 불가.");
                return;
            }

            Array.Sort(_sortLongs, 0, valid);
            long median = _sortLongs[valid / 2];
            long p95 = _sortLongs[Math.Min(valid - 1, (int)(valid * 0.95f))];

            sb.Append("[" + label + "] 중앙 " + median + " / 95% " + p95 + " / 최악 " + worst +
                      " (유효 " + valid + "/" + _stored + ")");
            if (budget > 0f)
                sb.Append(worst <= budget ? "  예산 " + budget.ToString("0") + " 이내"
                                          : "  ⚠ 예산 " + budget.ToString("0") + " 초과");
            sb.AppendLine();
        }

        private void AppendFrameLine(StringBuilder sb)
        {
            for (int i = 0; i < _stored; i++) _sortFloats[i] = _frameMs[i];
            Array.Sort(_sortFloats, 0, _stored);

            float median = _sortFloats[_stored / 2];
            float p95 = _sortFloats[Math.Min(_stored - 1, (int)(_stored * 0.95f))];

            sb.Append("[프레임타임] 중앙 " + median.ToString("0.00") + " ms (" +
                      (1000f / Mathf.Max(0.01f, median)).ToString("0") + " FPS) / 95% " +
                      p95.ToString("0.00") + " ms / 최악 " + _worstFrameMs.ToString("0.00") + " ms");
            if (_frameMsBudget > 0f)
                sb.Append(p95 <= _frameMsBudget ? "  예산 " + _frameMsBudget.ToString("0.00") + " ms 이내"
                                                : "  ⚠ 예산 " + _frameMsBudget.ToString("0.00") + " ms 초과");
            sb.AppendLine();

            // 중앙값이 흔한 표시 상한에 붙어 있으면 그것은 비용이 아니라 상한이다.
            // 이 프로젝트는 이미 한 번 그 값을 비용으로 읽고 "차이가 없다"고 결론냈다.
            float[] caps = { 60f, 120f, 144f, 240f };
            for (int i = 0; i < caps.Length; i++)
            {
                float capMs = 1000f / caps[i];
                if (Mathf.Abs(median - capMs) < 0.05f)
                {
                    sb.AppendLine("  ⚠ 중앙값이 " + caps[i].ToString("0") + " Hz 상한(" + capMs.ToString("0.00") +
                                  " ms)에 붙어 있다 — 중앙값은 비용이 아니라 상한이다. 비교는 95%·최악으로만 한다.");
                    break;
                }
            }
        }
    }
}

// 씬 배선 필요:
//   1) `Prototype_Elevator.unity` 에 빈 GameObject "RenderBudgetProbe" 를 만들고 이 컴포넌트를 붙인다.
//      붙이는 것만으로 매 프레임 샘플링하고 파괴 시 `Logs/render_budget.txt` 를 쓴다.
//   2) 예산은 코드에서 주입한다 — 품질 프로파일(`VisualQualityProfile`)이 확정되면
//      그 값을 읽어 `SetBudgets(드로우콜, SetPass, 삼각형, 프레임ms)` 를 한 번 부른다.
//      주입하지 않으면 값만 기록하고 판정하지 않는다(의도된 동작이다).
