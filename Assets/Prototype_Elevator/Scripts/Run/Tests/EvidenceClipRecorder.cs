using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Build;
using Ascend.Prototype.Player;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// PRD §17.6 이 요구하는 증거 **영상** 두 편을 실제 런에서 찍는다.
    /// `UP-TEST-08`(5연쇄 이상) · `UP-TEST-09`(Critical → 과수확 → 결과).
    ///
    /// 왜 따로 필요했나: `GifEncoder` 와 `SequenceRecorder` 는 이미 있고 왕복 검증까지
    /// 끝났는데 **부르는 코드가 자기 파일 말고 0곳**이었고 저장소의 `.gif` 는 0개였다.
    /// 만들어졌고 아무도 쓰지 않는, 이 저장소가 반복해서 당하는 그 모양이다.
    ///
    /// 정지 캡처로 대신할 수 없는 이유가 요구사항 자체에 있다 — 「연쇄」와
    /// 「Critical → 과수확 → 결과」는 **시간축의 사건**이고 한 장으로는 순서를 못 보인다.
    ///
    /// `TenFloorAutoPilot` 과 같은 규약을 쓴다: `RunSession` API 를 직접 부르지 않고
    /// `IInteractable.Interact()` 만 호출한다. 그래야 찍힌 영상이 실제 플레이 경로다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EvidenceClipRecorder : MonoBehaviour
    {
        public const string ReportPath = "Logs/evidence_clips.txt";
        public const string ClipDirectory = "Captures/evidence";
        private const string PrefKey = "Ascend.EvidenceClipRecorder.Armed";

        /// <summary>연쇄 깊이가 이 이상이면 「5연쇄 이상」으로 본다 (PRD §17.6).</summary>
        private const int DeepCascadeDepth = 5;

        private const float DeadlineSeconds = 420f;

        private readonly StringBuilder _report = new StringBuilder();
        private float _startedAt;

        private SequenceRecorder _recorder;
        private RiskStateView _risk;
        private RiskLevel _peakRisk = RiskLevel.Stable;
        private RiskLevel _peakRiskOverall = RiskLevel.Stable;

        private bool _cascadeClipWritten;
        private bool _overharvestClipWritten;
        private int _deepestCascade;

#if UNITY_EDITOR
        /// <summary>에디터 메뉴가 플레이 진입 전에 세운다.</summary>
        public static void Arm() => UnityEditor.EditorPrefs.SetBool(PrefKey, true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapIfArmed()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);
            var host = new GameObject("EvidenceClipRecorder");
            DontDestroyOnLoad(host);
            host.AddComponent<EvidenceClipRecorder>();
        }
#endif

        private IEnumerator Start()
        {
            _startedAt = Time.realtimeSinceStartup;
            _report.AppendLine("=== 증거 영상 (UP-TEST-08 · UP-TEST-09) ===");
            yield return null;

            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            var lever = FindAnyObjectByType<InteractableLever>();
            var panel = FindAnyObjectByType<InteractableContractPanel>();
            var tank = FindAnyObjectByType<InteractablePowerTank>();
            var overharvest = FindAnyObjectByType<InteractableOverharvestLever>();
            var door = FindAnyObjectByType<InteractableDoorControl>();
            var player = FindAnyObjectByType<FirstPersonController>();

            if (run == null || bridge == null || lever == null || tank == null ||
                overharvest == null || door == null)
            {
                _report.AppendLine("씬 배선 실패 — 녹화하지 않는다.");
                Finish();
                yield break;
            }

            _risk = FindAnyObjectByType<RiskStateView>();
            _recorder = gameObject.AddComponent<SequenceRecorder>();

            // **플레이어 카메라로 찍는다.** 캡처 하네스가 자기 카메라를 따로 만들어 쓰는 바람에
            // 눈높이 결함(카메라가 천장 위 3.22m)이 그림에 드러나지 않았던 전례가 있다.
            // 증거 영상은 플레이어가 실제로 본 것이어야 한다.
            Camera cam = player != null && player.ViewCamera != null
                ? player.ViewCamera
                : Camera.main;
            _report.AppendLine($"  카메라 {(cam != null ? cam.name : "없음")}" +
                               (player != null ? $" / 눈높이 {player.EyeHeightAboveRoot:F3}m" : ""));

            Directory.CreateDirectory(ClipDirectory);

            // **장치를 향해 세운다.** 앞선 영상은 화면 절반이 바닥이었고 결과판이 좌측에서
            // 잘려 3열 중 2열만 보였다. 플레이어 시점으로 찍는 것과 **볼 것을 보고 있는 것**은
            // 다르다 — 증거 영상은 후자여야 한다.
            AimAtBoard(player);

            // **두 런이 필요하다.** 한 런으로 둘 다 찍으려다 실패한 기록을 남긴다 —
            // 시드 1337 에 적재와 과수확을 함께 걸었더니 5층에서 사고로 끝나 연쇄가
            // 깊이 2 에 그쳤고(기준 5), 과수확은 1층에서 위험 `Stable` 일 때 당겨져
            // 「Critical → 과수확」이 아니었다. 파일 이름은 `Warning` 인데 그 런의 최고
            // 위험은 `Collapse` 였다 — **찍은 순간의 상태와 런 전체의 최고치는 다르다.**
            //
            //   A. 연쇄 — 완주하는 시드로 오래 돈다. 과수확도 적재도 걸지 않는다.
            //      깊은 연쇄는 층이 높아야 나오고, 사고로 끝나면 거기 못 간다.
            //   B. 과수확 — 적재로 위험을 올리되 **Critical 이 될 때까지 당기지 않는다.**
            yield return Drive(run, bridge, lever, panel, tank, overharvest, door, cam, player,
                               4242, load: false, overharvestAtOrAbove: null);
            // B 도 4242 를 쓴다. 1337 은 적재를 걸면 5층에서 사고로 끝나 — `Collapse` 에는
            // 닿지만 그 사이에 과수확이 열린 `Decision` 단계가 오지 않는다. 4242 는
            // 적재 없이도 `Critical` 에 닿으면서 10층을 완주하므로 기회가 훨씬 많고,
            // 적재를 얹으면 무게가 위험 점수를 한 번 더 민다.
            yield return Drive(run, bridge, lever, panel, tank, overharvest, door, cam, player,
                               4242, load: true, overharvestAtOrAbove: RiskLevel.Critical);

            _report.AppendLine();
            _report.AppendLine($"  최고 연쇄 깊이 {_deepestCascade} (기준 {DeepCascadeDepth})");
            _report.AppendLine($"  전 런 최고 위험 단계 {_peakRiskOverall}");
            _report.AppendLine($"  UP-TEST-08 연쇄 영상 {(_cascadeClipWritten ? "기록됨" : "**없음**")}");
            _report.AppendLine($"  UP-TEST-09 과수확 영상 {(_overharvestClipWritten ? "기록됨" : "**없음**")}");
            Finish();
        }

        private IEnumerator Drive(RunSessionBehaviour run, RouletteInteractionBridge bridge,
            InteractableLever lever, InteractableContractPanel panel, InteractablePowerTank tank,
            InteractableOverharvestLever overharvest, InteractableDoorControl door,
            Camera cam, FirstPersonController player, int seed, bool load,
            RiskLevel? overharvestAtOrAbove)
        {
            _report.AppendLine();
            _report.AppendLine($"  ── 시드 {seed} · 적재 {(load ? "함" : "안 함")} · " +
                               $"과수확 {(overharvestAtOrAbove.HasValue ? overharvestAtOrAbove.Value + " 이상에서" : "안 함")} ──");
            run.ResetRun(RunMode.TenFloor, seed);
            _peakRisk = RiskLevel.Stable;   // 런마다 다시 센다 — 앞 런의 최고치를 물려받으면 거짓이 된다
            for (int i = 0; i < 3; i++) yield return null;

            int guard = 0;
            while (!run.Session.IsComplete && !run.Session.IsFailed && guard++ < 300)
            {
                if (Time.realtimeSinceStartup - _startedAt > DeadlineSeconds)
                {
                    _report.AppendLine("  시간 상한 — 중단한다.");
                    yield break;
                }

                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                int number = floor.Plan.Floor;

                if (floor.Phase == FloorPhase.Boarding && load)
                {
                    int pick;
                    int takeGuard = 0;
                    while ((pick = BuildLoadPolicy.PickIndex(floor.BuildOffers, run.Session.Loadout)) >= 0
                           && takeGuard++ <= BuildLoadout.MaxSlots)
                    {
                        var candidates = FindObjectsByType<InteractableBuildCandidate>(
                            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                        InteractableBuildCandidate target = null;
                        for (int i = 0; i < candidates.Length; i++)
                            if (candidates[i].Index == pick) { target = candidates[i]; break; }
                        if (target == null) break;
                        target.Interact(gameObject);
                        yield return null;
                        yield return null;
                    }
                    _report.AppendLine($"  {number}층 적재 {run.Session.CarriedWeight:F0}/" +
                                       $"{run.Session.WeightCapacity:F0}kg");
                }

                if (floor.Phase == FloorPhase.Boarding)
                {
                    door.Interact(gameObject);
                    yield return null;
                }

                if (floor.Phase == FloorPhase.ContractSelection)
                {
                    if (floor.Plan.ContractChoices != null && floor.Plan.ContractChoices.Length > 1)
                    {
                        panel.Interact(gameObject);
                        yield return null;
                    }
                    lever.Interact(gameObject);
                    yield return null;
                }

                int spins = 0;
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && spins < 12)
                {
                    yield return WaitUnlocked(bridge);
                    if (floor.Phase != FloorPhase.Spinning) break;

                    // **스핀 전에 녹화를 시작한다.** 연쇄가 깊어질지는 판정 뒤에야 알지만,
                    // 그때 시작하면 연쇄의 시작을 놓친다. 그래서 매 스핀을 찍고,
                    // 깊지 않으면 버린다 — 필름이 아니라 메모리라 버리는 값이 싸다.
                    bool wantCascade = !_cascadeClipWritten;
                    // **화면 캡처로 찍는다.** 카메라 렌더 경로는 `ScreenSpaceOverlay` HUD 를
                    // 담지 못하고, 「연쇄 N단계」를 보여야 하는 영상에서 바로 그 HUD 가 빠졌다.
                    if (wantCascade) _recorder.Begin(cam, SequenceRecorder.DefaultWidth,
                                                     SequenceRecorder.DefaultFps, 10f,
                                                     screenCapture: true);

                    lever.Interact(gameObject);
                    spins++;
                    yield return null;
                    yield return WaitUnlocked(bridge);
                    for (int i = 0; i < 8; i++) yield return null;   // 결과가 화면에 남는 여운

                    TrackRisk();
                    int depth = DeepestCascadeOf(floor);
                    if (depth > _deepestCascade) _deepestCascade = depth;

                    if (wantCascade)
                    {
                        _recorder.StopRecording();
                        if (depth >= DeepCascadeDepth)
                        {
                            string path = Path.Combine(ClipDirectory,
                                $"cascade_depth{depth}_seed{run.Session.Seed}_f{number}.gif");
                            string written = _recorder.WriteTo(path);
                            _cascadeClipWritten = !string.IsNullOrEmpty(written);
                            _report.AppendLine($"  {number}층 연쇄 깊이 {depth} → {written}");
                        }
                    }
                }

                yield return WaitUnlocked(bridge);
                yield return null;

                if (floor.Phase == FloorPhase.Decision)
                {
                    TrackRisk();

                    // **위험 단계가 요구 수준에 닿았을 때만 당긴다.** `UP-TEST-09` 가 요구하는
                    // 것은 「Critical → 과수확 → 결과」라는 **순서**이고, 아무 때나 당기면
                    // 그 순서가 화면에 없다. 처음엔 열리자마자 당겨서 1층 `Stable` 상태의
                    // 영상을 찍었고, 그것은 요구사항이 묻는 그림이 아니었다.
                    bool riskReady = !overharvestAtOrAbove.HasValue ||
                                     CurrentRisk() >= overharvestAtOrAbove.Value;

                    // **덮개가 열려야 당길 수 있다.** `CanInteract == _unlocked && IsCoverOpen` 이고,
                    // 덮개는 해제 직후 애니메이션으로 열린다 — 잠금 해제와 같은 프레임에 읽으면
                    // 언제나 `False` 다. 처음엔 이걸 몰라 「과수확 조작가능 False」가 열 층 내내
                    // 찍혔고, 위험이 안 올라간 것으로 오해할 뻔했다. 위험은 `Critical` 까지 갔다.
                    if (overharvest.IsUnlocked) yield return WaitForCover(overharvest);

                    // 놓쳤을 때 **왜** 놓쳤는지 남긴다. 이것이 없으면 「영상 없음」만 남고
                    // 위험이 안 올라간 것인지 과수확이 안 열린 것인지 알 수 없다.
                    if (overharvestAtOrAbove.HasValue && !_overharvestClipWritten)
                        _report.AppendLine($"    {number}층 결정 — 위험 {CurrentRisk()} / " +
                                           $"해제 {overharvest.IsUnlocked} / 덮개 {overharvest.IsCoverOpen} / " +
                                           $"조작가능 {overharvest.CanInteract} / " +
                                           $"확정가능 {floor.CanBank} / 남은스핀 {floor.SpinsRemaining}");
                    // **당기는 것과 찍는 것을 분리한다.** 위험을 올리는 것이 바로 추가 스핀이므로,
                    // 「Critical 이 될 때까지 기다렸다가 당긴다」는 스스로를 막는 조건이었다 —
                    // 열 층 내내 `Warning` 에 머물렀다. 이제 열릴 때마다 당기고, 위험이 요구
                    // 수준에 닿은 그 당김만 녹화한다. 인과가 요구사항의 순서와 같아진다:
                    // 추가 스핀 → 위험 상승 → Critical → 과수확 → 결과.
                    if (overharvest.CanInteract && !_overharvestClipWritten &&
                        overharvestAtOrAbove.HasValue)
                    {
                        RiskLevel riskAtPull = CurrentRisk();
                        bool record = riskReady;
                        // 녹화할 당김에서만 시점을 옮긴다. 옮기지 않는 당김까지 카메라를
                        // 흔들면 런의 나머지가 다른 자리에서 진행된다.
                        if (record) AimAtOverharvest(player);
                        _report.AppendLine($"  {number}층 과수확 — 위험 {riskAtPull} / " +
                                           $"전력 {floor.Power:F0}/{floor.RequiredPower:F0} / " +
                                           $"{(record ? "녹화" : "녹화 안 함 — 위험이 아직 낮다")}");

                        if (!record)
                        {
                            overharvest.Interact(gameObject);
                            yield return null;
                            yield return WaitUnlocked(bridge);
                            TrackRisk();
                            continue;   // 결과를 다시 판정받는다
                        }
                        // **폭을 640 으로 올린다.** 위험 계기는 월드 공간 벽면 텍스트라
                        // 2.8m 거리에서 480px 폭이면 「위험도 Critical」이 십수 화소로 뭉갠다.
                        // 판정 대상이 프레임에 있어도 **읽히지 않으면 증거가 아니다.**
                        _recorder.Begin(cam, 640, SequenceRecorder.DefaultFps, 12f,
                                        screenCapture: true);

                        // **당기기 전 상태를 필름에 넣는다.** 앞서는 `Begin()` 직후 곧바로
                        // `Interact()` 를 불러 추출한 프레임이 전부 동일했다 — 요구가
                        // 「Critical **→** 과수확 **→** 결과」라는 **순서**인데 그것이 로그에만 있었다.
                        //
                        // 24 → 16 프레임으로 줄인다. 재판정이 「102 중 42프레임(41%)이 완전히
                        // 빈 판」이라고 지적했다 — 순서를 보이는 데 필요한 만큼만 남긴다.
                        for (int i = 0; i < 16; i++) yield return null;   // 약 0.8초

                        overharvest.Interact(gameObject);
                        yield return null;
                        yield return WaitUnlocked(bridge);
                        for (int i = 0; i < 16; i++) yield return null;   // 결과가 화면에 남는 여운
                        TrackRisk();

                        _recorder.StopRecording();
                        // **찍은 순간의 위험 단계를 파일 이름에 쓴다.** 앞서 `_peakRisk`(런 전체
                        // 최고치)를 썼더니 `Warning` 으로 찍힌 파일의 런이 나중에 `Collapse` 에
                        // 닿아서, 이름이 그 영상에 없는 상태를 가리켰다.
                        string path = Path.Combine(ClipDirectory,
                            $"overharvest_{riskAtPull}_seed{run.Session.Seed}_f{number}.gif");
                        string written = _recorder.WriteTo(path);
                        _overharvestClipWritten = !string.IsNullOrEmpty(written);
                        _report.AppendLine($"  → {written} (당긴 순간 {riskAtPull} · 직후 {CurrentRisk()})");
                        continue;
                    }

                    if (!tank.CanInteract)
                    {
                        _report.AppendLine($"  {number}층 진행 불가 — 중단");
                        break;
                    }
                    tank.Interact(gameObject);
                    yield return null;
                    yield return null;
                }
                else if (floor.Phase != FloorPhase.Resolved)
                {
                    _report.AppendLine($"  {number}층 예상 밖 단계 {floor.Phase} — 중단");
                    break;
                }

                TrackRisk();
            }

            _report.AppendLine($"  런 종료 — 완주={run.Session.IsComplete} 사고={run.Session.IsFailed} " +
                               $"도달 {run.Session.HighestFloorReached}층 · 이 런의 최고 위험 {_peakRisk}");
            if (_peakRisk > _peakRiskOverall) _peakRiskOverall = _peakRisk;
        }

        /// <summary>
        /// 플레이어를 결과판 정면으로 돌려세운다. 좌표는 `TenFloorCaptureRig.DeviceFront`
        /// 와 같은 근거를 쓴다 — 결과판은 월드 x[-1.10..-0.76] 을 차지하고, 판 앞면에서
        /// 1.1m 물러나면 세 열이 한 화면에 들어온다.
        ///
        /// 눈높이는 **건드리지 않는다.** 그 소유자는 계층(`Head`)이고 여기서 다시 계산하면
        /// 소유권이 둘로 갈린다 — 이번 세션에 그 실수로 시점이 천장 위로 갔다.
        /// </summary>
        private void AimAtBoard(FirstPersonController player)
        {
            if (player == null) return;

            Transform root = player.transform;
            var controller = root.GetComponent<CharacterController>();
            bool hadController = controller != null && controller.enabled;
            if (hadController) controller.enabled = false;   // 콜라이더가 텔레포트를 막는다

            var boardTarget = new Vector3(-0.90f, 1.60f, 0.00f);
            var stand = new Vector3(0.35f, root.position.y, 0.00f);
            root.position = stand;

            Vector3 flat = boardTarget - stand;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f) root.rotation = Quaternion.LookRotation(flat);

            if (player.ViewCamera != null)
            {
                Vector3 eye = player.ViewCamera.transform.position;
                player.ViewCamera.transform.rotation = Quaternion.LookRotation(boardTarget - eye);
            }

            if (hadController) controller.enabled = true;
            _report.AppendLine($"  결과판 정면에 세웠다 — {stand} → {boardTarget}");
        }

        /// <summary>
        /// 과수확 편 전용 시점. **레버와 위험 계기를 한 프레임에 넣는다.**
        ///
        /// 독립 재판정이 이 편을 반려한 사유가 정확히 그것이었다 — `Critical` 이 어느
        /// 프레임에도 없었다(당기기 전 24프레임이 R=G=B 완전 중립). 위험 문자열은
        /// `InstrumentPanelView` 의 **월드 공간 벽면 계기판**에만 있고 화면 HUD 는 위험을
        /// 출력하지 않는데, 결과판만 겨냥한 `AimAtBoard` 가 계기판을 화각 밖으로 밀어냈다.
        /// **과수확 레버도 한 프레임도 안 나와** 당기는 행위 자체가 필름에 없었다.
        ///
        /// 둘 다 앞벽 쪽이다 — 레버 (0.55, 1.19) · 계기 라벨 (-1.04, 1.38).
        /// 뒤에서 보면 두 목표의 벌어짐이 약 32° 이고, 이 클립의 수평 화각은
        /// **약 67°**(640×560 · 종횡비 1.14)라 중점을 겨누면 둘 다 든다.
        /// 고정 캡처의 91°(16:9)와 다르므로 그쪽 좌표를 그대로 쓸 수 없다.
        ///
        /// (이 주석은 폭을 480 → 640 으로 올린 커밋에서 **480 인 채로 남아 있었다.**
        /// 종횡비가 같아 화각 계산은 그대로였지만, 폭을 바꾼 바로 그 메서드의 문서가
        /// 옛 값을 가리키고 있었다 — 3차 감사가 잡았다. 이 저장소가 반복해서 당하는
        /// 「기록이 실제와 다름」의 소형판이다.)
        /// </summary>
        private void AimAtOverharvest(FirstPersonController player)
        {
            if (player == null) return;

            Transform root = player.transform;
            var controller = root.GetComponent<CharacterController>();
            bool hadController = controller != null && controller.enabled;
            if (hadController) controller.enabled = false;

            // 레버와 계기 라벨의 중점. 「대상을 겨눈다」가 아니라 **「두 대상의 중점을
            // 겨눈다」**가 이 문제의 규칙이다 — 고정 캡처 쪽에서 같은 것을 배웠다.
            var target = new Vector3(-0.25f, 1.50f, 1.28f);
            var stand = new Vector3(0.20f, root.position.y, -1.35f);
            root.position = stand;

            Vector3 flat = target - stand;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f) root.rotation = Quaternion.LookRotation(flat);

            if (player.ViewCamera != null)
            {
                Vector3 eye = player.ViewCamera.transform.position;
                player.ViewCamera.transform.rotation = Quaternion.LookRotation(target - eye);
            }

            if (hadController) controller.enabled = true;
            _report.AppendLine($"  과수확 시점으로 옮겼다 — {stand} → {target} (레버 + 위험 계기)");
        }

        /// <summary>이 층에서 지금까지 나온 가장 깊은 연쇄.</summary>
        private static int DeepestCascadeOf(FloorSession floor)
        {
            int deepest = 0;
            var history = floor.History;
            if (history == null) return 0;
            for (int i = 0; i < history.Count; i++)
                if (history[i].ChainDepth > deepest) deepest = history[i].ChainDepth;
            return deepest;
        }

        private RiskLevel CurrentRisk() => _risk != null ? _risk.Level : RiskLevel.Stable;

        private void TrackRisk()
        {
            RiskLevel now = CurrentRisk();
            if (now > _peakRisk) _peakRisk = now;
        }

        /// <summary>덮개가 다 열릴 때까지 기다린다. 열리지 않아도 진행은 막지 않는다.</summary>
        private static IEnumerator WaitForCover(InteractableOverharvestLever lever)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!lever.IsCoverOpen && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private IEnumerator WaitUnlocked(RouletteInteractionBridge bridge)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (bridge.IsLocked && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private void Finish()
        {
            _report.AppendLine($"소요 {Time.realtimeSinceStartup - _startedAt:F1}초");
            string text = _report.ToString();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, text, new UTF8Encoding(false));
            }
            catch (IOException exception)
            {
                Debug.LogError("[상승] 증거 영상 보고서를 쓰지 못했다: " + exception.Message);
            }
            Debug.Log("[상승] " + text);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
