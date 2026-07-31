using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Build;
using Ascend.Prototype.Player;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Spin;
using Ascend.Prototype.View;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// `AUTONOMOUS_PROTOTYPE_GOAL.md` §12의 필수 캡처 세트를 만든다.
    ///
    /// `HeroSliceCaptureRig`와 따로 두는 이유는 두 가지다. 첫째, 그 리그의 네 시점 좌표는
    /// 구 치수(내부 폭 3.20)에 맞춰 하드코딩돼 있어 지금은 벽 안쪽을 찍는다. 둘째,
    /// 요구 세트가 커졌다 — 위험 4단계, 적재 유무, 과수확 3단계가 새로 들어왔다.
    ///
    /// **위험 상태는 연출된 것이 아니라 실제 게임 상태다.** `RiskStateView`에는 단계를
    /// 강제하는 진입점이 없고 전부 `RiskEvaluator`가 게임 상태에서 계산한다. 그래서 이
    /// 리그는 무게를 싣고 과수확을 당겨 **점수를 실제로 올린다.** 무엇을 해서 그 단계에
    /// 도달했는지는 매니페스트에 남긴다 — 캡처가 실제 플레이에서 볼 수 없는 상태를
    /// 보여주면 그건 증거가 아니라 광고다.
    ///
    /// 비교 쌍은 같은 좌표로 찍는다: 위험 4단계, 화물칸 빈/최대.
    /// </summary>
    public sealed class TenFloorCaptureRig : MonoBehaviour
    {
        public const string OutputDirectory = "Captures/TenFloor";
        public const string ManifestPath = "Captures/TenFloor/manifest.txt";
        private const string PrefKey = "Ascend.TenFloorCaptureRig.Armed";

        private const int Width = 1920;
        private const int Height = 1080;
        private const float Fov = 60f;

        private readonly StringBuilder _manifest = new StringBuilder();
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _readback;
        private int _shots;

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

        // 좌표는 2026-07-31 비례 재조정 기준이다(내부 x[-1.20..1.20] · z[-1.50..1.50] · 높이 3.20).
        // 눈높이 1.62를 유지한다 — 캡처가 플레이어가 실제로 보는 높이여야 판정이 성립한다.
        private static readonly Pose Entry       = new Pose("Entry",       new Vector3( 0.65f, 1.62f,  1.35f), new Vector3(-0.70f, 1.35f, -0.90f));
        private static readonly Pose DeviceFront = new Pose("DeviceFront", new Vector3( 0.35f, 1.62f,  0.00f), new Vector3(-0.85f, 1.60f,  0.00f));
        private static readonly Pose DeviceSide  = new Pose("DeviceSide",  new Vector3(-0.20f, 1.62f, -0.80f), new Vector3(-0.90f, 1.50f,  0.15f));
        private static readonly Pose SymbolClose = new Pose("SymbolClose", new Vector3(-0.30f, 1.62f,  0.00f), new Vector3(-0.84f, 1.60f,  0.00f));
        // 화물칸 시점은 **문지방 위**에서 내려다본다. 처음에는 (0.60, 1.62, 1.25)에 뒀는데
        // 최대 적재 상태에서 오른쪽 열 승객(x=0.85, z=0.35)이 카메라 코앞에 서서 화면의
        // 대부분을 검게 가렸다 — "동선이 살아 있는가"를 판정할 수 없는 그림이 나왔다.
        // 문 개구부 중심(x=0.65) 위 2.35m에서 안쪽을 내려다보면 여섯 자리가 모두 들어온다.
        private static readonly Pose CargoBay    = new Pose("CargoBay",    new Vector3( 0.65f, 2.35f,  1.42f), new Vector3(-0.20f, 0.35f, -0.80f));
        private static readonly Pose Risk        = new Pose("Risk",        new Vector3( 0.60f, 1.62f, -0.70f), new Vector3(-0.55f, 1.55f,  0.55f));
        // 과수확 레버는 x[0.25..0.85] · y[0.90..1.90] · z[0.91..1.47]을 차지한다.
        // 처음엔 0.9m 앞에 세웠더니 하우징이 화면을 통째로 덮어 "잠겼는가 열렸는가"를
        // 판정할 수 없었다. 1.5m 물러나 레버와 주변 맥락이 함께 들어오게 한다.
        private static readonly Pose Overharvest = new Pose("Overharvest", new Vector3( 0.30f, 1.62f, -0.35f), new Vector3( 0.55f, 1.40f,  1.20f));
        private static readonly Pose Contract    = new Pose("Contract",    new Vector3( 0.10f, 1.62f,  0.30f), new Vector3( 1.12f, 1.50f,  0.30f));

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);
            var go = new GameObject("TenFloorCaptureRig");
            go.AddComponent<TenFloorCaptureRig>();
        }

        public static void Arm() => UnityEditor.EditorPrefs.SetBool(PrefKey, true);
#endif

        private IEnumerator Start()
        {
            yield return null;

            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            var risk = FindAnyObjectByType<RiskStateView>();
            var recorder = FindAnyObjectByType<AccidentRecorder>();
            if (run == null || bridge == null)
            {
                Debug.LogError("[상승] 캡처 리그: 씬 배선 없음");
                Finish();
                yield break;
            }

            SetupCamera();
            Header(run);

            // ── 1) 공간과 장치 (Stable, 적재 없음) ──
            run.ResetRun(RunMode.TenFloor, 1337);
            yield return WaitFrames(4);

            yield return Shot("01_entry", Entry, risk, "입구에서 본 전체 내부 — 요구 캡처 §12");
            yield return Shot("02_device_front", DeviceFront, risk, "수확 장치 정면 — 세 통관과 3×3 대응");
            yield return Shot("03_device_side", DeviceSide, risk, "1인칭 조작 거리에서 본 장치");
            yield return Shot("04_symbols", SymbolClose, risk, "세 심볼 비교 — 실루엣만으로 구분되는가");
            yield return Shot("05_cargo_empty", CargoBay, risk, "빈 화물 공간 — 07 과 같은 좌표");
            yield return Shot("06_risk_stable", Risk, risk, "Stable — 09·10·11 과 같은 좌표");

            // ── 2) 적재 ──
            // 2층까지 몰고 가서 실을 수 있는 만큼 싣는다. 최대 적재 캡처는 "동선이
            // 살아 있는가"를 판정하는 자료라 실제로 꽉 찬 상태여야 한다.
            yield return DriveToFloor(run, bridge, 2);
            FloorSession floor = run.Session.Current;
            if (floor != null && floor.Phase == FloorPhase.Boarding)
            {
                while (floor.BuildOffers.Count > 0 && run.TakeBuildOffer(0)) yield return null;
                run.FinishBoarding();
            }
            // 슬롯을 마저 채운다 — 한 층의 후보만으로는 6칸이 안 찬다.
            foreach (BuildItem item in BuildCatalog.All)
            {
                if (run.Session.Loadout.IsFull) break;
                run.Session.Loadout.Add(item);
            }
            yield return WaitFrames(4);

            yield return Shot("07_cargo_full", CargoBay, risk,
                $"최대 적재 {run.Session.Loadout.Count}개 / {run.Session.CarriedWeight:F0}kg — 05 와 같은 좌표");
            yield return Shot("08_passenger_and_device", DeviceSide, risk,
                "승객과 장치가 한 화면에 — 적재가 장치 접근을 막지 않는가");

            // ── 3) 위험 4단계 (같은 좌표) ──
            // Stable 은 06 에서 이미 찍었다. 여기서는 실제로 점수를 올린다.
            //   Warning  ← 과적 (OverloadScore 3.0 ≥ WarningEnter 3.0)
            //   Critical ← 과적 + 과수확 (3.0 + 3.2 + 잔류 ≥ CriticalEnter 7.0)
            //   Collapse ← 층 실패 (점수와 무관하게 Collapse)
            run.Session.AddWeight(140f);   // 허용 중량을 확실히 넘긴다
            yield return WaitFrames(30);   // 조명·험 블렌딩이 끝날 시간
            yield return Shot("09_risk_warning", Risk, risk,
                $"Warning — 과적 {run.Session.CarriedWeight:F0}/{run.Session.WeightCapacity:F0} / 실제 단계 {LevelName(risk)}");

            yield return ForceCritical(run, bridge, risk);
            yield return Shot("10_risk_critical", Risk, risk,
                $"Critical — 실제 단계 {LevelName(risk)} / 점수 {(risk != null ? risk.Score : 0f):F1}");

            // ── 4) 과수확 3단계 ──
            //
            // 해제 조건은 `Decision && CanBank && SpinsRemaining > 0`이다. 시드 하나로는
            // 요구 전력을 마지막 스핀에서야 넘길 수 있고, 그러면 남은 스핀이 0이라
            // 영영 해제되지 않는다. 실제로 첫 촬영이 `unlocked=False`로 나왔다.
            // 조건을 만족하는 시드를 찾을 때까지 돌린다.
            var overharvest = FindAnyObjectByType<InteractableOverharvestLever>();
            int chosenSeed = 12;
            foreach (int seed in new[] { 12, 1337, 7, 4242, 90210, 1, 31415, 271828 })
            {
                run.ResetRun(RunMode.TenFloor, seed);
                yield return WaitFrames(3);
                chosenSeed = seed;
                yield return SpinUntilBankable(run, bridge);
                yield return WaitFrames(2);
                FloorSession probe = run.Session.Current;
                if (probe != null && probe.Phase == FloorPhase.Decision &&
                    probe.CanBank && probe.SpinsRemaining > 0) break;
            }

            // 잠금 상태는 조건을 만족하기 **전** 상태여야 하므로 새 런에서 찍는다.
            run.ResetRun(RunMode.TenFloor, chosenSeed);
            yield return WaitFrames(4);
            yield return Shot("11_overharvest_locked", Overharvest, risk,
                $"잠금 상태 — 시드 {chosenSeed} / unlocked={(overharvest != null && overharvest.IsUnlocked)}");

            yield return SpinUntilBankable(run, bridge);
            // 브리지가 해제를 반영하고 보호 덮개가 다 열릴 때까지 기다린다. 해제와
            // 조작 가능은 같은 순간이 아니다 — 첫 촬영에서 `unlocked=False`가 찍힌 이유다.
            yield return WaitFrames(3);
            if (overharvest != null)
            {
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!overharvest.IsCoverOpen && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return WaitFrames(2);
            }
            yield return Shot("12_overharvest_unlocked", Overharvest, risk,
                $"해제 순간 — unlocked={(overharvest != null && overharvest.IsUnlocked)} " +
                $"덮개열림={(overharvest != null && overharvest.IsCoverOpen)} " +
                $"조작={(overharvest != null && overharvest.CanInteract)}");

            if (overharvest != null && overharvest.CanInteract)
            {
                overharvest.Interact(gameObject);
                yield return WaitFrames(2);
                yield return WaitWhileLocked(bridge);
                yield return WaitFrames(2);
            }
            yield return Shot("13_overharvest_pulled", Overharvest, risk, "당긴 직후");

            // ── 5) 계약 선택 ──
            run.ResetRun(RunMode.TenFloor, 1337);
            yield return WaitFrames(2);
            yield return DriveToFloor(run, bridge, 6);   // 계약이 처음 나오는 층
            yield return WaitFrames(3);
            FloorSession contractFloor = run.Session.Current;
            yield return Shot("14_contract_select", Contract, risk,
                contractFloor != null
                    ? $"{contractFloor.Plan.Floor}층 계약 선택 — 선택지 {contractFloor.Plan.ContractChoices.Length}종"
                    : "계약 층 도달 실패");

            // ── 6) 깊은 연쇄 ──
            yield return CaptureDeepCascade(run, bridge, risk);

            // ── 7) 사고와 결과 ──
            yield return CaptureCollapse(run, bridge, risk, recorder);

            Finish();
        }

        // ── 상태 만들기 ─────────────────────────────────────────────────────

        private static string LevelName(RiskStateView risk)
            => risk != null ? risk.Level.ToString() : "—";

        /// <summary>과적 위에 과수확을 얹어 Critical 문턱을 넘긴다.</summary>
        private IEnumerator ForceCritical(RunSessionBehaviour run,
            RouletteInteractionBridge bridge, RiskStateView risk)
        {
            int guard = 0;
            while (guard++ < 8)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (risk != null && risk.Level >= RiskLevel.Critical) break;

                if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
                if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);

                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0)
                {
                    run.Spin();
                    yield return WaitWhileLocked(bridge);
                }
                if (floor.Phase == FloorPhase.Decision && floor.CanBank && floor.SpinsRemaining > 0)
                {
                    run.PushYourLuck();
                    run.Spin();
                    yield return WaitWhileLocked(bridge);
                }
                else break;
                yield return WaitFrames(20);
            }
            yield return WaitFrames(30);
        }

        private IEnumerator DriveToFloor(RunSessionBehaviour run,
            RouletteInteractionBridge bridge, int target)
        {
            int guard = 0;
            while (run.Session.CurrentFloor < target && !run.Session.IsComplete &&
                   !run.Session.IsFailed && guard++ < 60)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (floor.Plan.Floor >= target) break;

                if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
                if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0) run.Spin();
                if (floor.CanBank) run.Bank();
                else if (floor.SpinsRemaining == 0) run.ForceResolve();
                else break;
                yield return null;
            }
            yield return WaitWhileLocked(bridge);
        }

        private IEnumerator SpinUntilBankable(RunSessionBehaviour run, RouletteInteractionBridge bridge)
        {
            FloorSession floor = run.Session.Current;
            if (floor == null) yield break;
            if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
            if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);

            int guard = 0;
            while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && guard++ < 12)
            {
                run.Spin();
                yield return WaitWhileLocked(bridge);
                if (floor.CanBank) break;
            }
        }

        /// <summary>연쇄가 깊게 터진 스핀을 찾아 그 재생 중에 찍는다.</summary>
        private IEnumerator CaptureDeepCascade(RunSessionBehaviour run,
            RouletteInteractionBridge bridge, RiskStateView risk)
        {
            var presenter = FindAnyObjectByType<SpinPresenter>();
            // 깊은 연쇄는 자연 발생이 드물다. 성능 측정이 1000스핀 평균 연쇄 **1.74**를
            // 보고했고, 1층·4층에서 시드 10개를 훑어 최대 4단계에 그쳤다.
            //
            // 그래서 **실제 빌드로 확률을 올린다.** 연출된 상황이 아니라 플레이어가 만들 수
            // 있는 판이어야 캡처가 증거가 된다:
            //   사선 결속기 — 대각 연결을 열어 4칸 덩어리가 훨씬 자주 성립한다
            //   연쇄 조속기 — 연쇄 배수 증분을 올린다
            //   증식체 계약 — 대상 저항의 출현률을 1.5배로
            // 셋 다 카탈로그와 커리큘럼에 실재하는 것이고, 8층은 FullPool + 계약 3종이다.
            int[] seeds = { 12, 7, 1, 99, 2024, 31415, 271828, 8675309, 42, 1234567, 20260731, 555 };
            int bestDepth = 0;

            foreach (int seed in seeds)
            {
                run.ResetRun(RunMode.TenFloor, seed);
                yield return WaitFrames(2);
                run.Session.Loadout.Add(BuildCatalog.ById("PRT_DIAGONAL_BINDER"));
                run.Session.Loadout.Add(BuildCatalog.ById("PRT_CASCADE_GOVERNOR"));
                yield return DriveToFloor(run, bridge, 8);
                FloorSession floor = run.Session.Current;
                if (floor == null) continue;
                if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
                if (floor.Phase == FloorPhase.ContractSelection)
                    run.SelectContract(floor.Plan.ContractChoices.Length - 1);   // 증식체 계약

                int guard = 0;
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && guard++ < 8)
                {
                    SpinResolution resolution = run.Spin();
                    int depth = resolution.Steps != null ? resolution.Steps.Length : 0;
                    if (depth > bestDepth) bestDepth = depth;
                    if (depth >= 5)
                    {
                        // 연출 중간에 찍는다. 다 끝난 뒤에는 판이 비어 있어 연쇄를 볼 수 없다.
                        float deadline = Time.realtimeSinceStartup + 6f;
                        while (presenter != null && presenter.IsPresenting &&
                               presenter.CurrentDepth < 3 && Time.realtimeSinceStartup < deadline)
                            yield return null;
                        yield return Shot("15_cascade_deep", DeviceFront, risk,
                            $"시드 {seed} / 8층 / 사선 결속기+연쇄 조속기+증식체 계약 / " +
                            $"연쇄 {depth}단계 / 재생 중 깊이 {(presenter != null ? presenter.CurrentDepth : -1)}");
                        yield break;
                    }
                    yield return WaitWhileLocked(bridge);
                }
            }

            yield return Shot("15_cascade_deep", DeviceFront, risk,
                $"5연쇄 이상을 찾지 못했다 — 시도한 시드 중 최대 {bestDepth}단계");
        }

        private IEnumerator CaptureCollapse(RunSessionBehaviour run, RouletteInteractionBridge bridge,
            RiskStateView risk, AccidentRecorder recorder)
        {
            // 사고를 만든다: 무겁게 실어 요구 전력을 끌어올린 뒤 스핀을 소진시킨다.
            run.ResetRun(RunMode.TenFloor, 555555);
            yield return WaitFrames(2);
            run.Session.AddWeight(220f);

            int guard = 0;
            while (!run.Session.IsFailed && !run.Session.IsComplete && guard++ < 40)
            {
                FloorSession floor = run.Session.Current;
                if (floor == null) break;
                if (floor.Phase == FloorPhase.Boarding) run.FinishBoarding();
                if (floor.Phase == FloorPhase.ContractSelection) run.SelectContract(0);
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0) run.Spin();
                if (floor.SpinsRemaining == 0 && !floor.CanBank) { run.ForceResolve(); break; }
                if (floor.CanBank) run.Bank();
                else break;
                yield return null;
            }

            yield return WaitFrames(40);
            yield return Shot("16_risk_collapse", Risk, risk,
                $"Collapse — 실제 단계 {LevelName(risk)} / 실패 {run.Session.IsFailed} " +
                $"사유 {run.Session.FailureReason ?? "—"} / 06·09·10 과 같은 좌표");

            string record = recorder != null && recorder.Latest != null
                ? "사고 기록 있음" : "사고 기록 없음";
            yield return Shot("17_accident_recorder", Risk, risk,
                $"{record} / 기록 {(recorder != null ? recorder.Records.Count : 0)}건 / " +
                $"시드 {run.Session.Seed} / 도달 {run.Session.HighestFloorReached}층");

            // 완주 직전 — 10층에 **실제로 서 있는** 런을 찾는다. 시드 하나로 몰다가
            // 중간에 사고가 나면 "도달 8층"이 찍히고, 그건 §12가 요구한 그림이 아니다.
            FloorSession last = null;
            int finalSeed = 0;
            foreach (int seed in new[] { 1337, 4242, 90210, 7, 31415, 271828, 8675309, 20260731 })
            {
                run.ResetRun(RunMode.TenFloor, seed);
                yield return WaitFrames(2);
                yield return DriveToFloor(run, bridge, 10);
                yield return WaitFrames(3);
                FloorSession candidate = run.Session.Current;
                if (candidate != null && candidate.Plan.Floor == 10)
                {
                    last = candidate;
                    finalSeed = seed;
                    break;
                }
            }
            yield return Shot("18_final_floor", Risk, risk,
                last != null
                    ? $"시드 {finalSeed} / 10층 도달 — 요구 {last.RequiredPower:F0} / 완주 직전"
                    : $"10층에 선 런을 찾지 못했다 — 마지막 도달 {run.Session.HighestFloorReached}층");
        }

        // ── 촬영 ────────────────────────────────────────────────────────────

        private void SetupCamera()
        {
            Camera source = Camera.main;
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
            _camera.enabled = false;

            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                name = "TenFloorCapture",
            };
            _readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            _camera.targetTexture = _target;
        }

        private IEnumerator Shot(string name, Pose pose, RiskStateView risk, string note)
        {
            _camera.transform.position = pose.Position;
            _camera.transform.LookAt(pose.LookAt);
            _camera.enabled = true;

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
            File.WriteAllBytes(Path.Combine(directory, $"{name}.png"), _readback.EncodeToPNG());
            _shots++;

            _manifest.AppendLine($"{name,-26} 시점 {pose.Name,-12} pos {pose.Position:F2} look {pose.LookAt:F2}  " +
                                 $"위험 {(risk != null ? risk.Level.DisplayName() : "—")}");
            _manifest.AppendLine($"{"",-26} {note}");
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        private static IEnumerator WaitWhileLocked(RouletteInteractionBridge bridge)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (bridge != null && bridge.IsLocked && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private void Header(RunSessionBehaviour run)
        {
            _manifest.AppendLine("=== 10층 고정 캡처 세트 ===");
            _manifest.AppendLine($"해상도 {Width}×{Height} / FOV {Fov} / 모드 {run.Mode}");
            // 베이스라인은 기기에 종속된다(`CLAUDE.md`, `TECH_SPEC.md` §14).
            // 이 줄이 달라지면 이전 캡처와 비교하지 않는다.
            _manifest.AppendLine("machineFingerprint: " +
                $"{SystemInfo.operatingSystemFamily}|{SystemInfo.graphicsDeviceType}|" +
                $"{SystemInfo.graphicsDeviceName}|{Application.unityVersion}");
            _manifest.AppendLine($"OS {SystemInfo.operatingSystem}");
            _manifest.AppendLine("주의: 전용 카메라의 RenderTexture 렌더다. 화면 UGUI HUD는 포함되지 않는다.");
            _manifest.AppendLine("위험 단계는 연출이 아니라 실제 게임 상태다 — 무엇을 해서 도달했는지 각 줄에 적혀 있다.");
            _manifest.AppendLine();
        }

        private void Finish()
        {
            _manifest.AppendLine();
            _manifest.AppendLine($"촬영 {_shots}장");
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), ManifestPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, _manifest.ToString(), new UTF8Encoding(true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[상승] 매니페스트 저장 실패: {exception.Message}");
            }
            Debug.Log($"[상승] 캡처 완료\n{_manifest}");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnDestroy()
        {
            if (_target != null) { _target.Release(); Destroy(_target); }
            if (_readback != null) Destroy(_readback);
        }
    }
}
