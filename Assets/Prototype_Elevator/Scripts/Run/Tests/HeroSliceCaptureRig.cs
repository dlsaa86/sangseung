using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Player;
using Ascend.Prototype.Risk;
using Ascend.Prototype.View;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// 고정 시점 캡처 하네스.
    ///
    /// `MASTER_PRD.md` §11과 `TECH_SPEC.md` §14가 요구하는 것은 "예쁜 스크린샷"이 아니라
    /// **비교 가능한 캡처**다. 해상도·FOV·카메라 위치·품질·시드가 고정되지 않으면 두 빌드를
    /// 나란히 놓아도 무엇이 달라졌는지 말할 수 없다.
    ///
    /// 그래서 플레이어 카메라를 쓰지 않는다. 전용 카메라를 정해진 좌표에 놓고
    /// 1920×1080 RenderTexture로 직접 렌더한다 — 에디터 창 크기와 무관해진다.
    /// Stable과 Critical은 **같은 좌표**에서 찍는다. 그래야 무음 비교가 성립한다.
    ///
    /// 화면 위 IMGUI 디버그 HUD는 이 캡처에 들어오지 않는다. 평가 대상은 공간의 판독성이고,
    /// 디버그 오버레이는 최종 UI가 아니기 때문이다.
    /// </summary>
    public sealed class HeroSliceCaptureRig : MonoBehaviour
    {
        public const string OutputDirectory = "Captures/HeroSlice";
        public const string ManifestPath = OutputDirectory + "/manifest.txt";
        private const string PrefKey = "Ascend.HeroSliceCaptureRig.Armed";

        private const int Width = 1920;
        private const int Height = 1080;
        private const float Fov = 60f;

        /// <summary>고정 시점. 좌표를 바꾸면 이전 캡처와 비교할 수 없게 된다.</summary>
        private readonly struct Pose
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 LookAt;

            public Pose(string name, Vector3 position, Vector3 lookAt)
            {
                Name = name; Position = position; LookAt = lookAt;
            }
        }

        // 방 좌표: x -1.7~1.7, z -1.7~1.7, 바닥 0, 천장 2.6. 눈높이 1.62.
        private static readonly Pose Interior =
            new Pose("interior", new Vector3(1.25f, 1.62f, -1.32f), new Vector3(-0.55f, 1.38f, 0.55f));
        // 0.93m 에서 찍었더니 3×3 이 위아래로 잘려 "3×3 으로 읽히는가"를 판정할 수 없었다.
        // 1.87m 로 물러나면 결과판 전체와 통관 경계가 한 프레임에 들어온다.
        private static readonly Pose BoardFront =
            new Pose("board_front", new Vector3(0.42f, 1.60f, 0f), new Vector3(-1.45f, 1.60f, 0f));
        private static readonly Pose ContractWall =
            new Pose("contract", new Vector3(0.45f, 1.58f, 0.30f), new Vector3(1.55f, 1.52f, 0.30f));
        // "과수확" 명판이 프레임 위로 잘려 나가 레버가 무엇인지 캡처만으로는 알 수 없었다.
        // 시선을 조금 올리고 물러나 명판·덮개·손잡이·전력 탱크가 한 프레임에 들어오게 한다.
        private static readonly Pose OverharvestApproach =
            new Pose("overharvest", new Vector3(0.55f, 1.62f, -0.15f), new Vector3(0.66f, 1.46f, 1.34f));

        private readonly StringBuilder _manifest = new StringBuilder();
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _readback;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);
            new GameObject("HeroSliceCaptureRig").AddComponent<HeroSliceCaptureRig>();
        }

        public static void Arm() => UnityEditor.EditorPrefs.SetBool(PrefKey, true);
#endif

        private IEnumerator Start()
        {
            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            var presenter = FindAnyObjectByType<SpinPresenter>();
            var risk = FindAnyObjectByType<RiskStateView>();
            var lever = FindByName<InteractableLever>("ExecutionLever");
            var panel = FindAnyObjectByType<InteractableContractPanel>();
            var overharvest = FindAnyObjectByType<InteractableOverharvestLever>();

            if (run == null || bridge == null || lever == null || panel == null || overharvest == null)
            {
                Debug.LogError("[상승] 캡처 하네스: 씬 배선이 부족하다.");
                Stop();
                yield break;
            }

            SetupCamera();
            Header(run, risk);

            // ── 패스 A: 기준 시드. 계약 선택 → Stable → 과수확 접근 ──
            yield return StartCoroutine(PassA(run, bridge, presenter, risk, lever, panel, overharvest));

            // ── 패스 B: Critical 에 닿는 시드 ──
            yield return StartCoroutine(PassB(run, bridge, presenter, risk, lever, panel, overharvest));

            // ── 패스 C: 5연쇄 이상 ──
            yield return StartCoroutine(PassC(run, bridge, presenter, lever, panel));

            // ── 패스 D: 직선 패턴 표식 ──
            yield return StartCoroutine(PassD(run, bridge, presenter, lever, panel));

            WriteManifest();
            Stop();
        }

        // ── 패스 ──

        private IEnumerator PassA(RunSessionBehaviour run, RouletteInteractionBridge bridge,
                                  SpinPresenter presenter, RiskStateView risk,
                                  InteractableLever lever, InteractableContractPanel panel,
                                  InteractableOverharvestLever overharvest)
        {
            const int seed = 1337;
            run.ResetRun(seed);
            yield return null;
            yield return null;

            yield return Shot("01_interior", Interior, seed, "계약 선택 단계 — 엘리베이터 내부 전경", risk);

            // 과수확 레버 잠김 상태. 아래 06(해제)과 **같은 좌표**라 나란히 비교할 수 있다 —
            // Gate C "과수확 레버가 일반 실행 레버와 혼동되지 않는다"의 증거다.
            yield return Shot("05b_overharvest_locked", OverharvestApproach, seed,
                              "과수확 레버 — 잠김(덮개 닫힘·띠 어두움). 06과 같은 좌표", risk);
            yield return Shot("02_board_front", BoardFront, seed, "3×3 결과판 정면", risk);
            yield return Shot("03_contract_select", ContractWall, seed,
                              "계약 선택 — 출현률·보상·대가가 함께 걸린다", risk);

            // 화면 UI는 Overlay 라 위 캡처들에 안 나온다. 따로 잡는다.
            yield return ScreenShot("09_screen_ui_hint", Interior, seed,
                                    "화면 UI — 계약 선택 안내 한 줄. 숫자는 계기판이 말한다");

            // 흡수체 계약으로 확정한 뒤 요구 전력까지 돌린다.
            yield return CycleContractTo(bridge, panel, 1);
            lever.Interact(gameObject);
            yield return null;
            yield return SpinUntilDecision(run, bridge, presenter, lever);

            // Stable/Critical 은 반드시 같은 좌표에서 찍는다 — 무음 비교의 전제다.
            yield return Shot("04_stable", Interior, seed, "Stable — 기준 상태", risk);
            yield return Shot("06_overharvest_access", OverharvestApproach, seed,
                              "과수확 레버 — 해제(덮개 열림·띠 발광·전용 등). 05b와 같은 좌표", risk);

        }

        private IEnumerator PassB(RunSessionBehaviour run, RouletteInteractionBridge bridge,
                                  SpinPresenter presenter, RiskStateView risk,
                                  InteractableLever lever, InteractableContractPanel panel,
                                  InteractableOverharvestLever overharvest)
        {
            // 시드 7 / 증식체 계약 / 반복 과수확 → 헤드리스 탐색에서 Critical(점수 8.8) 확인됨.
            const int seed = 7;
            run.ResetRun(seed);
            yield return null;
            yield return null;

            yield return CycleContractTo(bridge, panel, 2);
            lever.Interact(gameObject);
            yield return null;

            // 시간 기준으로 기다린다. 처음에는 반복 횟수로 막았는데, 과수확 덮개가 열리는
            // 0.3초(≈18프레임) 동안 대기 루프가 카운터를 다 써버려 Critical 에 닿기 전에
            // 빠져나왔다. 그래서 캡처가 Stable 상태로 찍혔다.
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (risk != null && risk.Level >= RiskLevel.Critical) break;
                if (bridge.IsLocked) { yield return null; continue; }

                if (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0)
                {
                    lever.Interact(gameObject);
                    yield return null;
                    yield return WaitForPresentation(presenter);
                    continue;
                }

                if (floor.Phase == FloorPhase.Decision)
                {
                    if (floor.SpinsRemaining <= 0 || !floor.CanBank) break;
                    if (!overharvest.CanInteract) { yield return null; continue; }   // 덮개 여는 중
                    overharvest.Interact(gameObject);
                    yield return null;
                    yield return WaitForPresentation(presenter);
                    continue;
                }
                yield return null;
            }

            if (risk != null && risk.Level < RiskLevel.Critical)
                _manifest.AppendLine($"05_critical : 경고 — Critical 에 닿지 못했다(현재 {risk.Level.DisplayName()}).");

            yield return Shot("05_critical", Interior, seed,
                              "Critical — 04_stable 과 같은 좌표·FOV·해상도", risk);
        }

        private IEnumerator PassC(RunSessionBehaviour run, RouletteInteractionBridge bridge,
                                  SpinPresenter presenter, InteractableLever lever,
                                  InteractableContractPanel panel)
        {
            // 시드 12 / 증식체 계약 / 스핀 1(0-기준) 에서 연쇄 6 — 헤드리스 탐색 결과.
            const int seed = 12;
            run.ResetRun(seed);
            yield return null;
            yield return null;

            yield return CycleContractTo(bridge, panel, 2);
            lever.Interact(gameObject);
            yield return null;

            // 요구 전력을 먼저 넘겨버리면 Decision 으로 빠져 다음 스핀이 오지 않는다.
            // 연쇄가 깊은 스핀은 그 뒤에 있으므로, Decision 에서는 과수확으로 계속 돌린다.
            var overharvest = FindAnyObjectByType<InteractableOverharvestLever>();
            float deadline = Time.realtimeSinceStartup + 90f;
            bool captured = false;
            while (Time.realtimeSinceStartup < deadline && !captured)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null || floor.SpinsRemaining <= 0) break;
                if (bridge.IsLocked) { yield return null; continue; }

                if (floor.Phase == FloorPhase.Decision)
                {
                    if (!floor.CanBank) break;
                    if (overharvest == null || !overharvest.CanInteract) { yield return null; continue; }
                    overharvest.Interact(gameObject);
                }
                else
                {
                    if (!lever.CanInteract) { yield return null; continue; }
                    lever.Interact(gameObject);
                }
                yield return null;

                // 연쇄가 4단계 이상 올라간 순간을 잡는다. 재생이 끝나면 최종 판만 남아
                // "연쇄가 있었다"는 사실이 화면에서 사라진다.
                float playDeadline = Time.realtimeSinceStartup + 30f;
                while (presenter != null && presenter.IsPresenting && Time.realtimeSinceStartup < playDeadline)
                {
                    if (!captured && presenter.CurrentDepth >= 4)
                    {
                        yield return Shot("07_cascade_deep", BoardFront, seed,
                                          $"연쇄 {presenter.CurrentDepth}단계 진행 중 — {presenter.CurrentCause}", null);
                        captured = true;
                    }
                    yield return null;
                }
            }

            if (!captured)
                _manifest.AppendLine("07_cascade_deep : 실패 — 4단계 이상 연쇄를 잡지 못했다");
        }

        /// <summary>
        /// 직선 정화의 표식을 잡는다. 연결(패스 C)과 나란히 놓고 보면 "형태가 다른가"를
        /// 판정할 수 있다 — `.claude/visual-criteria.md` B-2.6 이 요구하는 비교다.
        ///
        /// 시드 1 / 계약 없음 / 첫 스핀에서 대각선 [0,4,8] 이 성립한다(헤드리스 탐색 결과).
        /// </summary>
        private IEnumerator PassD(RunSessionBehaviour run, RouletteInteractionBridge bridge,
                                  SpinPresenter presenter, InteractableLever lever,
                                  InteractableContractPanel panel)
        {
            const int seed = 1;
            run.ResetRun(seed);
            yield return null;
            yield return null;

            yield return CycleContractTo(bridge, panel, 0);
            lever.Interact(gameObject);
            yield return null;
            lever.Interact(gameObject);
            yield return null;

            bool captured = false;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (presenter != null && presenter.IsPresenting &&
                   Time.realtimeSinceStartup < deadline && !captured)
            {
                // 표식은 정화 맥동 동안에만 서 있다. 그 창을 놓치면 빈 판만 찍힌다.
                if (!string.IsNullOrEmpty(presenter.CurrentCause) &&
                    presenter.CurrentCause.Contains("직선"))
                {
                    yield return Shot("08_pattern_line", BoardFront, seed,
                                      $"직선 정화 표식 — {presenter.CurrentCause}", null);
                    captured = true;
                }
                yield return null;
            }

            if (!captured)
                _manifest.AppendLine("08_pattern_line : 실패 — 직선 정화 순간을 잡지 못했다");

            // 연출 중 화면 UI(연쇄 배너)도 한 장.
            if (presenter != null && presenter.IsPresenting)
                yield return ScreenShot("10_screen_ui_cascade", BoardFront, seed,
                                        "화면 UI — 연출 중 연쇄 단계와 발동 원인");
        }

        // ── 조작 헬퍼 ──

        private IEnumerator CycleContractTo(RouletteInteractionBridge bridge,
                                            InteractableContractPanel panel, int index)
        {
            int guard = 0;
            while (bridge.PreviewIndex != index && guard++ < 8)
            {
                panel.Interact(gameObject);
                yield return null;
            }
        }

        private IEnumerator SpinUntilDecision(RunSessionBehaviour run, RouletteInteractionBridge bridge,
                                              SpinPresenter presenter, InteractableLever lever)
        {
            int guard = 0;
            while (guard++ < 12)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null || floor.Phase == FloorPhase.Decision) break;
                if (!lever.CanInteract) { yield return null; continue; }
                lever.Interact(gameObject);
                yield return null;
                yield return WaitForPresentation(presenter);
            }
        }

        private IEnumerator WaitForPresentation(SpinPresenter presenter)
        {
            if (presenter == null) yield break;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (presenter.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>
        /// 화면 UI가 포함된 캡처. Screen Space Overlay 캔버스는 카메라의 RenderTexture에
        /// **렌더되지 않으므로** 고정 시점 캡처(전용 카메라 → RT)에는 절대 나오지 않는다.
        /// 그래서 화면 자체를 잡는다.
        ///
        /// 해상도가 게임 뷰 크기라 **고정 비교 세트가 아니다.** UI가 실제로 뜨는지 확인하는
        /// 용도이고, 매니페스트에도 그렇게 적는다.
        /// </summary>
        private IEnumerator ScreenShot(string name, Pose pose, int seed, string note)
        {
            // 화면 캡처는 **플레이어 카메라**가 찍는다(전용 카메라가 아니다). UI만 맞고 뒤가
            // 아무 데나 보이면 평가에 못 쓰므로, 플레이어 시점을 고정 좌표로 옮긴다.
            Camera player = Camera.main;
            Vector3 savedPosition = default;
            Quaternion savedRotation = default;
            if (player != null)
            {
                savedPosition = player.transform.position;
                savedRotation = player.transform.rotation;
                player.transform.position = pose.Position;
                player.transform.rotation = Quaternion.LookRotation(pose.LookAt - pose.Position);
            }

            yield return null;
            yield return new WaitForEndOfFrame();

            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, $"{name}.png"), screen.EncodeToPNG());

                _manifest.AppendLine($"{name,-34} 시드 {seed,-6} 화면 캡처 {screen.width}×{screen.height} " +
                                     "(게임 뷰 해상도 — 고정 비교 세트 아님)");
                _manifest.AppendLine($"{"",-34} {note}");
            }
            finally
            {
                Destroy(screen);
                if (player != null)
                {
                    player.transform.position = savedPosition;
                    player.transform.rotation = savedRotation;
                }
            }
        }

        // ── 캡처 ──

        private void SetupCamera()
        {
            var source = Camera.main;
            var go = new GameObject("CaptureCamera");
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();

            if (source != null)
            {
                _camera.clearFlags = source.clearFlags;
                _camera.backgroundColor = source.backgroundColor;
                _camera.cullingMask = source.cullingMask;
                _camera.nearClipPlane = source.nearClipPlane;
                _camera.farClipPlane = source.farClipPlane;
            }
            _camera.fieldOfView = Fov;
            _camera.enabled = false;   // 매 프레임 렌더하지 않는다. 필요할 때만 켠다.

            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                name = "HeroSliceCapture",
            };
            _readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            _camera.targetTexture = _target;
        }

        private IEnumerator Shot(string name, Pose pose, int seed, string note, RiskStateView risk)
        {
            _camera.transform.position = pose.Position;
            _camera.transform.LookAt(pose.LookAt);
            _camera.enabled = true;

            // 두 프레임 돌린다 — 위험 상태 블렌딩과 연출 하이라이트가 이번 프레임 값으로
            // 반영된 뒤에 찍어야 한다.
            yield return null;
            yield return new WaitForEndOfFrame();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            _readback.Apply(false);
            RenderTexture.active = previous;
            _camera.enabled = false;

            string directory = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, $"{name}.png");
            File.WriteAllBytes(file, _readback.EncodeToPNG());

            _manifest.AppendLine(
                $"{name,-34} 시드 {seed,-6} 시점 {pose.Name,-12} " +
                $"pos {pose.Position:F2} look {pose.LookAt:F2}  " +
                $"위험 {(risk != null ? risk.Level.DisplayName() : "—")}");
            _manifest.AppendLine($"{"",-34} {note}");
        }

        private void Header(RunSessionBehaviour run, RiskStateView risk)
        {
            _manifest.AppendLine("=== Hero Slice 고정 캡처 세트 ===");
            _manifest.AppendLine($"해상도 {Width}×{Height} / FOV {Fov} / 모드 {run.Mode}");
            _manifest.AppendLine($"기기 {SystemInfo.graphicsDeviceType} · {SystemInfo.graphicsDeviceName} · " +
                                 $"{SystemInfo.operatingSystem}");
            _manifest.AppendLine("주의: 캡처는 전용 카메라의 RenderTexture 렌더다. 화면 IMGUI 디버그 HUD는 포함되지 않는다.");
            _manifest.AppendLine();
        }

        private void WriteManifest()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), ManifestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, _manifest.ToString(), new UTF8Encoding(true));
            Debug.Log($"[상승] 캡처 완료\n{_manifest}");
        }

        private void OnDestroy()
        {
            if (_target != null) { _target.Release(); Destroy(_target); }
            if (_readback != null) Destroy(_readback);
        }

        private static T FindByName<T>(string name) where T : Component
        {
            foreach (T candidate in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (candidate.name == name) return candidate;
            return FindAnyObjectByType<T>();
        }

        private static void Stop()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
