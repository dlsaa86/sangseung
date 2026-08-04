using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ascend.Prototype.Build.Tests;
using Ascend.Prototype.Risk.Tests;
using Ascend.Prototype.Run.Tests;
using Ascend.Prototype.Spin.Tests;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 테스트 진입점 하나. 러너가 흩어져 있으면 "어느 걸 돌려야 그린인가"가 매번 달라진다.
    ///
    /// 왜 Unity Test Runner(NUnit)가 아닌가: 이 프로젝트에는 asmdef이 없어 모든 코드가
    /// `Assembly-CSharp`에 있다. Unity 규칙상 asmdef 어셈블리는 predefined `Assembly-CSharp`를
    /// **참조할 수 없으므로**, 테스트 어셈블리를 추가해도 게임 코드를 볼 수 없다.
    /// asmdef 도입은 별도 결정 없이 하지 않는다(`TECH_SPEC.md` §3).
    /// → `DECISION_LOG.md` D-20260730-06.
    /// </summary>
    public static class AscendTestMenu
    {
        /// <summary>
        /// 이 목록이 곧 "EditMode 그린"의 정의다. 스위트를 새로 만들고 여기에 넣지 않으면
        /// 깨져도 아무도 모른다 — `PrototypeSelfTest`가 같은 이유로 뒤늦게 네 스위트를
        /// 편입한 전례가 있다(그 파일의 주석).
        /// </summary>
        private static (int passed, int failed, string report)[] AllSuites() => new[]
        {
            SpinEngineTests.RunAll(),
            SpinRuleSetTests.RunAll(),
            RunTests.RunAll(),
            RiskEvaluatorTests.RunAll(),
            BuildTests.RunAll(),
            Telemetry.Tests.TelemetryTests.RunAll(),
            Data.Profiles.Tests.ProfileTests.RunAll(),
            Npc.Tests.PassengerReactionTests.RunAll(),
            Audio.Tests.AudioTests.RunAll(),
            Perf.Tests.PerfTests.RunAll(),
            Diagnostics.Tests.WiringDiagnosticsTests.RunAll(),
            UI.Tests.RunSummaryBuilderTests.RunAll(),
            Effects.Tests.PresentationBindingTests.RunAll(),
            View.Tests.OverharvestStageTests.RunAll(),
            Player.Tests.HoldInputTests.RunAll(),
            View.Tests.LeverStateMachineTests.RunAll(),
            // 레버 → 9개 챔버 동력 전달. 첫 구현이 화면에서 **아무것도 움직이지
            // 않았는데** 임시 검사가 공허하게 통과했다(움직인 적 없음끼리 순서 비교).
            // 이 스위트는 순서를 재기 **전에** 각 단계가 실제로 움직였는지 단정한다.
            View.Tests.CustomsLockViewTests.RunAll(),
            View.Tests.InstrumentPanelLineTests.RunAll(),
            // 표현 계층 물리(관성 반응자·고정 스텝 적분). 등록을 빼먹으면
            // **합계가 그대로라서 「테스트가 없다」와 「테스트가 통과했다」가 구분되지 않는다** —
            // 실제로 이번에 그 상태로 한 번 돌았다(350 → 350). 이 저장소는
            // `WiringDiagnosticsTests`·`RunSummaryBuilderTests` 에서 같은 일을 이미 겪었고,
            // 그때의 결론이 「등록되지 않은 테스트는 통과가 아니라 미검증이다」였다.
            Physics.Tests.PresentationPhysicsTests.RunAll(),
            // 절차적 메시(모따기·그레이팅·파이프·프롭). 등록 누락이 같은 배치에서
            // **두 번** 났다 — 레인은 자기 소유 경로 밖인 이 파일을 고칠 수 없고,
            // 통합자가 넣지 않으면 합계가 안 움직인다. 그 침묵이 곧 미검증이다.
            Art.Tests.ProcMeshTests.RunAll(),
            // 원형 현창 3×3. 레퍼런스가 요구한 「세로 릴이 아닌 계기판」이 구조로
            // 지켜지는지를 기하에서 직접 잰다 — 가로 리브가 더 두껍고, 세로 리브가
            // 끊겨 있고, 아홉 개 구멍이 실제로 뚫려 있다는 것. 산문 주장이 아니다.
            Art.Tests.PortholeMeshTests.RunAll(),
            // 사용자 명세(2026-08-02 「산업용 화물 엘리베이터 내부」)의 치수 불변식.
            // 상수는 틀려도 컴파일되므로 여기서만 잡힌다. 특히 「중앙 이동 공간
            // 2.2 × 2.8」은 어느 상수에도 없는 **네 값의 결과**라 하나만 바꿔도
            // 조용히 깨지고, 깨지면 플레이어가 낀다.
            Art.Tests.ReferenceRoomSpecTests.RunAll(),
            // 남은 스핀 운행 효율 정산 (`T-05` 2026-08-02). 「과수확을 고르면
            // 정산이 사라진다」가 두 선택을 겨루게 만드는 축이라, 그 단정이
            // 없으면 규칙이 조용히 무력해져도 아무도 모른다.
            Data.Profiles.Tests.SettlementTests.RunAll(),
        };

        [MenuItem("Ascend/Run All EditMode Tests %#t")]
        public static void RunAll()
        {
            var suites = AllSuites();
            int passed = 0, failed = 0;
            var sb = new System.Text.StringBuilder(4096);
            foreach (var s in suites)
            {
                passed += s.passed;
                failed += s.failed;
                sb.Append(s.report).Append("\n\n");
            }
            sb.Append($"합계: {passed} PASS / {failed} FAIL");
            string report = sb.ToString();

            // **산출물을 손으로 유지하지 않는다.** 이 파일의 "합계:" 줄은 완료 판정의
            // 근거다 — 예전에는 `tools/verify-topdown.ps1` 이 읽었고(2026-08-03 삭제),
            // 지금은 사람과 감사자가 읽는다. 읽는 주체가 바뀌어도 규칙은 같다.
            // 한때 이 파일을 아무도 쓰지 않아 스위트가 늘어도 옛 숫자가 그대로 남았다.
            // 판정 근거가 되는 파일은 판정 대상을 실제로 돌린 쪽이 써야 한다.
            WriteArtifact("editmode_tests.txt", report);

            if (failed > 0) Debug.LogError($"[상승]\n{report}");
            else Debug.Log($"[상승]\n{report}");
        }

        /// <summary>
        /// `Logs/` 아래에 산출물을 쓴다. 실패해도 테스트 결과를 삼키지 않는다 —
        /// 쓰기 실패는 경고이지 테스트 실패가 아니다.
        /// </summary>
        private static void WriteArtifact(string fileName, string body)
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, fileName), body + "\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[상승] {fileName} 을 쓰지 못했다: {e.GetType().Name}: {e.Message}");
            }
        }

        [MenuItem("Ascend/Run PlayMode Hero Slice Check")]
        public static void RunPlayModeCheck()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            // 직전 결과를 지운다. 남아 있으면 이번에 안 돌았는데 돈 것처럼 읽힌다.
            string path = Path.Combine(Directory.GetCurrentDirectory(), HeroSliceAutoPilot.ReportPath);
            if (File.Exists(path)) File.Delete(path);

            HeroSliceAutoPilot.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] PlayMode 검증 시작. 끝나면 자동 종료하고 {HeroSliceAutoPilot.ReportPath} 에 남긴다.");
        }

        /// <summary>
        /// 10층 런을 상호작용만으로 끝까지 몬다. `P2-Gate B`의 증거를 만드는 진입점이다.
        /// Hero Slice 검증과 별개인 이유는 `TenFloorAutoPilot` 주석에 있다.
        /// </summary>
        [MenuItem("Ascend/Run PlayMode TenFloor Check")]
        public static void RunTenFloorCheck()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string path = Path.Combine(Directory.GetCurrentDirectory(), TenFloorAutoPilot.ReportPath);
            if (File.Exists(path)) File.Delete(path);

            TenFloorAutoPilot.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 10층 PlayMode 검증 시작 → {TenFloorAutoPilot.ReportPath}");
        }

        /// <summary>
        /// PRD §17.6 의 증거 **영상** 두 편을 실제 런에서 찍는다.
        /// 정지 캡처로 대신할 수 없다 — 「연쇄」와 「Critical → 과수확 → 결과」는
        /// 시간축의 사건이고 한 장으로는 순서를 보일 수 없다.
        /// </summary>
        [MenuItem("Ascend/Record Evidence Clips")]
        public static void RunEvidenceClips()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string path = Path.Combine(Directory.GetCurrentDirectory(), EvidenceClipRecorder.ReportPath);
            if (File.Exists(path)) File.Delete(path);

            EvidenceClipRecorder.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 증거 영상 녹화 시작 → {EvidenceClipRecorder.ReportPath}");
        }

        [MenuItem("Ascend/Capture Hero Slice Set")]
        public static void RunCaptureSet()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string manifest = Path.Combine(Directory.GetCurrentDirectory(), HeroSliceCaptureRig.ManifestPath);
            if (File.Exists(manifest)) File.Delete(manifest);

            HeroSliceCaptureRig.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 고정 캡처 시작 → {HeroSliceCaptureRig.OutputDirectory}");
        }

        /// <summary>`AUTONOMOUS_PROTOTYPE_GOAL.md` §12의 필수 캡처 세트를 만든다.</summary>
        [MenuItem("Ascend/Capture Ten Floor Set")]
        public static void RunTenFloorCaptureSet()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            // 화면 경로로 찍는 3장(`17`·`19`·`20`)은 게임 뷰 크기로 나온다.
            // 고정하지 않으면 나머지 18장(RenderTexture, 1920×1080)과 해상도가 갈리고,
            // 실제로 816×714 로 나와 그 세 장만 판독성 평가에서 불리하게 채점됐다.
            // **실패를 조용히 넘기지 않는다** — 못 맞추면 캡처를 시작하지 않는다.
            // 틀린 해상도로 찍힌 증거는 없느니만 못하다.
            if (!Ascend.CaptureHarness.EditorTools.GameViewResolution.TrySetFixed(
                    Ascend.CaptureHarness.EditorTools.GameViewResolution.SpecWidth,
                    Ascend.CaptureHarness.EditorTools.GameViewResolution.SpecHeight))
            {
                Debug.LogError("[상승] 게임 뷰를 1920×1080 으로 고정하지 못했다 — " +
                               Ascend.CaptureHarness.EditorTools.GameViewResolution.LastError +
                               ". 화면 캡처 3장이 다른 해상도로 나오므로 캡처를 중단한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string manifest = Path.Combine(Directory.GetCurrentDirectory(), TenFloorCaptureRig.ManifestPath);
            if (File.Exists(manifest)) File.Delete(manifest);

            TenFloorCaptureRig.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 10층 고정 캡처 시작 → {TenFloorCaptureRig.OutputDirectory}");
        }

        /// <summary>
        /// 포스트를 끈 **진단** 세트. `Captures/TenFloor_NoPost/` 로 나간다.
        ///
        /// 왜 세트가 둘인가: 포스트 체인의 디더링(±1 LSB)과 필름 그레인이
        /// `GRAPHICS_TARGET` 의 축 둘을 서로 반대 방향으로 오염시킨다 —
        /// **G-4** 는 인접 화소 차 ≤ 1 인 평탄 구간을 세는데 ±1 LSB 만으로 부서지고,
        /// **G-1** 은 국소 분산이라 텍스처가 없어도 노이즈만으로 올라간다(거짓 그린).
        /// 그렇다고 그레인을 끄면 **G-6 이 Film Grain 활성을 요구한다.**
        /// 축이 서로를 부정하므로 재는 세트를 나눈다 — G-1·G-4 는 이 세트에서,
        /// G-2·G-3·G-6 은 포스트를 켠 `Captures/TenFloor/` 에서 잰다.
        ///
        /// **같은 리그·같은 시드·같은 시점이다.** 리그를 복제하지 않는 이유가 그것이다.
        /// </summary>
        [MenuItem("Ascend/Capture Ten Floor Set (No Post — diagnostic)")]
        public static void RunTenFloorCaptureSetNoPost()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            if (!Ascend.CaptureHarness.EditorTools.GameViewResolution.TrySetFixed(
                    Ascend.CaptureHarness.EditorTools.GameViewResolution.SpecWidth,
                    Ascend.CaptureHarness.EditorTools.GameViewResolution.SpecHeight))
            {
                Debug.LogError("[상승] 게임 뷰를 1920×1080 으로 고정하지 못했다 — " +
                               Ascend.CaptureHarness.EditorTools.GameViewResolution.LastError);
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            TenFloorCaptureRig.ArmNoPost("Captures/TenFloor_NoPost");
            string manifest = Path.Combine(Directory.GetCurrentDirectory(), TenFloorCaptureRig.ManifestPath);
            if (File.Exists(manifest)) File.Delete(manifest);

            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 진단(포스트 OFF) 캡처 시작 → {TenFloorCaptureRig.OutputDirectory}");
        }

        /// <summary>
        /// `P2-Gate G`의 "최대 적재와 Critical 상태 측정". Hero Slice 측정과 별개인 이유는
        /// <see cref="LoadedCriticalPerfProbe"/> 주석에 있다 — 그쪽은 무적재·Stable 만 잰다.
        /// </summary>
        [MenuItem("Ascend/Measure Loaded + Critical Performance")]
        public static void RunLoadedCriticalProbe()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string report = Path.Combine(Directory.GetCurrentDirectory(), LoadedCriticalPerfProbe.ReportPath);
            if (File.Exists(report)) File.Delete(report);

            LoadedCriticalPerfProbe.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 최대적재+Critical 측정 시작 → {LoadedCriticalPerfProbe.ReportPath}");
        }

        [MenuItem("Ascend/Measure Hero Slice Performance")]
        public static void RunPerfProbe()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] 이미 Play 모드다. 먼저 종료한다.");
                return;
            }

            const string scenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string report = Path.Combine(Directory.GetCurrentDirectory(), HeroSlicePerfProbe.ReportPath);
            if (File.Exists(report)) File.Delete(report);

            HeroSlicePerfProbe.Arm();
            EditorApplication.EnterPlaymode();
            Debug.Log($"[상승] 성능 측정 시작 → {HeroSlicePerfProbe.ReportPath}");
        }
    }
}
