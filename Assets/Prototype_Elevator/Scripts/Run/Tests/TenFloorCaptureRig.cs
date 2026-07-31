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
        // 입구 시점을 두 번 틀렸다.
        //
        // 처음엔 y=1.35 를 내려다봐서 화면 절반이 무특징 검은 바닥이었다. 눈높이로
        // 들었더니 이번엔 **카메라가 과수확 레버 하우징 안에 들어갔다** —
        // `Housing` 은 x[0.25..0.85] · y[0.90..1.74] · z[0.95..1.47] 를 차지하는데
        // (0.72, 1.62, 1.38) 이 그 안이다. 독립 평가자가 "흰 다각형이 장치를 관통한다"고
        // 지적한 것은 관통 기하가 아니라 그 상자의 **안쪽 면**이었다.
        //
        // 통관 볼륨과 겹치는 렌더러를 전수 조사해서야 알았다 — 외부 기하는 하나도
        // 없었고, 그래서 원인이 씬이 아니라 카메라라는 것이 드러났다.
        // 하우징 왼쪽으로 비켜서 실내를 길이 방향으로 훑는다.
        //
        // 하우징을 피한 뒤에도 구도가 틀렸다 — 앞벽이 화면 중앙을 채우고 장치가
        // 오른쪽 끝으로 밀렸다. 시선을 장치 쪽으로 더 돌려 왼쪽 벽(결과판)과
        // 바닥·천장이 함께 들어오게 한다.
        private static readonly Pose Entry       = new Pose("Entry",       new Vector3( 0.12f, 1.62f,  1.30f), new Vector3(-0.85f, 1.35f, -0.35f));
        private static readonly Pose DeviceFront = new Pose("DeviceFront", new Vector3( 0.35f, 1.62f,  0.00f), new Vector3(-0.85f, 1.60f,  0.00f));
        private static readonly Pose DeviceSide  = new Pose("DeviceSide",  new Vector3(-0.20f, 1.62f, -0.80f), new Vector3(-0.90f, 1.50f,  0.15f));
        // 결과판은 x[-1.10..-0.76] · y[0.95..2.25] · z[-0.69..0.69]를 차지한다.
        // 처음엔 x=-0.30(판에서 0.54m)에 뒀더니 **카메라가 판 안에 들어가** 조각만
        // 잡혔다. 1.35m 물러나 가운데 줄 세 칸이 나란히 들어오게 한다 —
        // 한 화면에 구·정육면체·캡슐이 같은 크기로 놓여야 "3종 비교"가 된다.
        //
        // 그런데 1.35m 는 `DeviceFront`(x=0.35)와 0.05m 차이라 **두 장이 사실상 같은
        // 그림**이 됐다. 독립 평가자가 "04는 02와 중복이다. 세 심볼 비교는 별도
        // 근접 샷이어야 한다"고 지적했다. 판 앞면이 x=-0.76 이므로 x=-0.05 면
        // 0.71m — 판 안에 들어가지 않으면서 심볼이 화면을 채운다.
        private static readonly Pose SymbolClose = new Pose("SymbolClose", new Vector3(-0.05f, 1.60f,  0.00f), new Vector3(-0.90f, 1.60f,  0.00f));
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
        // 계약 선택자는 오른쪽 벽의 명판 세 장(월드 x≈0.99, y 1.21~1.80)이다.
        // 처음에는 뒷벽 계기판을 함께 담으려 했지만 둘은 서로 90° 떨어진 벽에 있어
        // 한 프레임에 둘 다 읽히게 넣을 수 없었다. 조건 문구를 명판 옆으로 옮긴 뒤로는
        // 선택자 하나만 봐도 "무엇을·얼마에" 고르는지가 전부 들어온다.
        private static readonly Pose Contract    = new Pose("Contract",    new Vector3(-0.60f, 1.58f, -0.15f), new Vector3( 0.97f, 1.50f, -0.05f));

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

        /// <summary>고정 프레임 시간. manifest 머리말에 적어 재현 조건을 남긴다.</summary>
        public const float CaptureDeltaTime = 1f / 60f;
        private int _restoreVSync = -1;

        /// <summary>세운 시계를 되돌린다. 안 되돌리면 다음 Play 세션이 캡처 시계로 돈다.</summary>
        private void RestoreClock()
        {
            if (_restoreVSync < 0) return;
            Time.captureDeltaTime = 0f;
            QualitySettings.vSyncCount = _restoreVSync;
            _restoreVSync = -1;
        }

        private IEnumerator Start()
        {
            yield return null;

            // 프레임 시간을 못박는다. 이걸 세우지 않으면 `Time.deltaTime` 이 기계 부하를
            // 따라가고, 시간으로 재는 모든 대기가 **다른 프레임 수**로 끝난다.
            // 위험 상태 블렌딩·덮개 각도·승객 기울기가 전부 프레임 단위로 진행하므로
            // 같은 시드의 두 캡처가 바이트 단위로 달라진다. 고정 캡처 세트의 전제가
            // "같은 입력이면 같은 그림"이므로 여기서 시계를 고정한다.
            _restoreVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Time.captureDeltaTime = CaptureDeltaTime;

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
            yield return WaitSeconds(2.5f);   // 09·10·16 과 같은 정착 시간을 준다

            // **판을 채운 뒤에 찍는다.** 처음에는 이 네 장이 전부 빈 판때기였다 —
            // 엔진은 스핀이 끝나면 수확·정화된 칸을 비우고 `ResetRun`도 판을 지우므로,
            // 아무 때나 찍으면 9칸이 전부 공란이다. 독립 평가자가
            // "세 심볼 비교 샷에 비교 대상이 0개"라고 지적했다.
            //
            // 게임의 핵심이 결과판인데 그것이 안 찍히면 이 세트는 무엇을 하는 게임인지
            // 보여주지 못한다.
            var board = FindAnyObjectByType<SpinBoardView>();

            // 열마다 한 종류씩. "세 통관이 각각 한 열"이라는 대응과 "세 심볼 구분"을
            // 한 장에서 동시에 보여준다.
            ShowBoard(board, ColumnPerKind());
            yield return WaitFrames(3);
            yield return Shot("02_device_front", DeviceFront, risk,
                "수확 장치 정면 — 세 통관과 3×3 대응 / 열마다 한 종류(영혼·흡수체·증식체)");
            yield return Shot("03_device_side", DeviceSide, risk, "1인칭 조작 거리에서 본 장치");
            yield return Shot("04_symbols", SymbolClose, risk,
                "세 심볼 비교 — 왼쪽 열 정상 영혼 / 가운데 흡수체 / 오른쪽 증식체");

            // 실제 추첨 결과도 한 장. 인위적 배열만으로는 "실제로 이렇게 나온다"를 못 보인다.
            ShowBoard(board, DrawSample(1337, 8, 0));
            yield return WaitFrames(3);
            yield return Shot("01_entry", Entry, risk, "입구에서 본 전체 내부 — 요구 캡처 §12");
            yield return Shot("05_cargo_empty", CargoBay, risk, "빈 화물 공간 — 07 과 같은 좌표");

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
            //
            // **같은 좌표만으로는 대조가 안 된다.** 처음에는 Stable 을 1층·화물 없음에서
            // 찍고 Warning·Critical 을 2층·화물 있음에서 찍었다. 독립 평가자가 바로
            // 짚었다 — "좌측에 화물 상자가 있고 없고가 상태 차이보다 눈에 먼저 띈다.
            // 지금 세트로는 '위험 단계가 공간을 바꾸는가'를 판정할 수 없다."
            //
            // 대조군은 **나머지를 고정해야** 대조군이다. Stable 도 여기서 찍는다 —
            // 같은 층, 같은 적재, 같은 요구 전력. 달라지는 것은 위험 단계뿐이다.
            //   Warning  ← 과적 (OverloadScore 3.0 ≥ WarningEnter 3.0)
            //   Critical ← 과적 + 과수확 (3.0 + 3.2 + 잔류 ≥ CriticalEnter 7.0)
            //   Collapse ← 층 실패 (점수와 무관하게 Collapse) — 이것만 별도 런이다
            FloorSession riskFloor = run.Session.Current;
            yield return WaitSeconds(2.5f);   // 09·10 과 같은 정착 시간을 준다
            yield return Shot("06_risk_stable", Risk, risk,
                $"Stable — {(riskFloor != null ? riskFloor.Plan.Floor : 0)}층 / " +
                $"적재 {run.Session.Loadout.Count}개 {run.Session.CarriedWeight:F0}kg / " +
                $"요구 {(riskFloor != null ? riskFloor.RequiredPower : 0f):F0} / " +
                $"실제 단계 {LevelName(risk)} — 대조군. 09·10·16 과 **고정된 것**: 층·카메라 좌표·적재 개수. " +
                "**달라지는 것**: 무게(09 에서 +140kg)·요구 전력·소비 스핀. " +
                "과적이 요구를 끌어올리는 것이 Warning 의 정의라 그것까지 고정하면 상태 자체를 만들 수 없다. " +
                "즉 이 넷은 '같은 판을 같은 각도에서 본 것'이지 '같은 숫자'가 아니다");

            run.Session.AddWeight(140f);   // 허용 중량을 확실히 넘긴다
            yield return WaitSeconds(2.5f);   // 조명·험 블렌딩이 수렴할 시간(2.2/초)
            yield return Shot("09_risk_warning", Risk, risk,
                $"Warning — {(riskFloor != null ? riskFloor.Plan.Floor : 0)}층 / " +
                $"과적 {run.Session.CarriedWeight:F0}/{run.Session.WeightCapacity:F0} / " +
                $"실제 단계 {LevelName(risk)} — 06 에서 무게만 +140kg");

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
            //
            // `WaitFrames(4)` 였다. 에디터 Play 는 100fps 넘게 돌아 약 0.04초다.
            // `RiskStateView._blendSpeed` 는 2.2/초라 직전 런의 위험 상태에서
            // 안정으로 수렴하는 데 1.4초가 걸린다 — 그래서 이 장이 "위험도 안정" 문구와
            // **적색 램프**를 함께 찍었다. 다른 위험 샷들은 이미 `WaitSeconds(2.5f)` 를 쓴다.
            // 같은 함정을 두 번 밟았다.
            run.ResetRun(RunMode.TenFloor, chosenSeed);
            yield return WaitSeconds(2.5f);
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

            // **당긴 직후**를 찍는다. 연출이 끝나기를 기다리면 덮개가 다시 닫혀
            // 11(잠금)과 기하학적으로 같은 그림이 된다 — 첫 촬영이 그랬다.
            // 판돈이 빠지고 스핀이 도는 순간이 이 선택의 결과다.
            float anteBefore = 0f;
            int extraBefore = 0;
            if (overharvest != null && overharvest.CanInteract)
            {
                FloorSession before = run.Session.Current;
                anteBefore = before != null ? before.PendingAnte : 0f;
                extraBefore = before != null ? before.ExtraSpinsTaken : 0;
                overharvest.Interact(gameObject);
                yield return WaitFrames(2);
            }
            FloorSession afterPull = run.Session.Current;
            yield return Shot("13_overharvest_pulled", Overharvest, risk,
                afterPull != null
                    ? $"당긴 직후 — 판돈 {anteBefore:F0} 지불 / 추가 스핀 {extraBefore}→{afterPull.ExtraSpinsTaken} / " +
                      $"전력 {afterPull.Power:F0}/{afterPull.RequiredPower:F0} / 위험 {LevelName(risk)}"
                    : "당긴 직후");
            yield return WaitWhileLocked(bridge);

            // ── 5) 계약 선택 ──
            run.ResetRun(RunMode.TenFloor, 1337);
            yield return WaitFrames(2);
            yield return DriveToFloor(run, bridge, 6);   // 계약이 처음 나오는 층
            yield return WaitFrames(3);
            FloorSession contractFloor = run.Session.Current;

            // **탑승 단계에서 찍으면 계약 단계가 아니다.** 첫 시도가 그랬고, 계기판은
            // 당연히 "확정 — 변화 없음"을 띄웠다. 캡처 이름이 `contract_select`인데
            // 화면은 계약을 고르는 중이 아니었던 것이다. 문을 닫아 단계를 넘긴다.
            if (contractFloor != null && contractFloor.Phase == FloorPhase.Boarding)
            {
                run.FinishBoarding();
                yield return WaitFrames(3);
            }
            yield return Shot("14_contract_select", Contract, risk,
                contractFloor != null
                    ? $"{contractFloor.Plan.Floor}층 {contractFloor.Phase} — 선택지 " +
                      $"{contractFloor.Plan.ContractChoices.Length}종 / 미리보기 " +
                      $"{(bridge != null ? bridge.PreviewIndex + 1 : 0)}"
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

        // ── 결과판 채우기 ────────────────────────────────────────────────────

        /// <summary>
        /// 결과판에 보드를 밀어 넣고 연출자가 덮어쓰지 못하게 잠근다.
        ///
        /// `DrivenExternally`를 세우지 않으면 `SpinBoardView`가 다음 프레임에
        /// 런의 최종 보드(대개 빈 판)를 따라가 방금 넣은 것을 지운다.
        /// </summary>
        private static void ShowBoard(SpinBoardView view, SpinBoard board)
        {
            if (view == null) return;
            view.DrivenExternally = true;
            view.ShowBoard(board);
        }

        /// <summary>
        /// 한 캐스케이드 단계의 정화를 표식·강조로 세운다. 재생을 거치지 않고 판을
        /// 직접 밀어 넣었을 때 `SpinPresenter`가 하던 일을 대신한다.
        /// </summary>
        private static int ShowPurifies(SpinBoardView board, CascadeStep step)
        {
            var markers = FindAnyObjectByType<PurifyMarkerView>();
            if (step.Purifies == null) return 0;

            // 강조를 1.0 으로 줬더니 9칸 중 8칸이 계조 없는 순백으로 날아가
            // 심볼도 정화 원인도 사라졌다. 연출에서 1.0 은 사인파가 **스치는**
            // 정점이라 눈이 잔상으로 형태를 유지하지만, 정지 화면에서는 그 값이
            // 계속 걸려 있다. 움직이는 연출의 최댓값을 정지 샷에 그대로 쓸 수 없다.
            //
            // 0.42 로 낮췄더니 형태는 살아났지만 이번엔 **사건 둘이 8칸을 같은 값으로
            // 칠했다.** 어느 칸이 어느 사건에 속하는지 갈리지 않으면 그 채널은 정보를
            // 나르지 않는다. 사건마다 밝기를 달리해 칸이 원인별로 묶이게 한다.
            // 막대(표식)가 "왜"를 말하고, 밝기 층이 "어느 것끼리"를 말한다.
            var levels = new[] { 0.46f, 0.22f, 0.34f, 0.16f };

            markers?.Begin();
            int count = 0;
            foreach (PurifyEvent purify in step.Purifies)
            {
                float level = levels[count % levels.Length];
                if (board != null && purify.Cells != null)
                    foreach (int cell in purify.Cells) board.SetHighlight(cell, level);
                markers?.Add(in purify, 1f);   // 막대는 밝아야 한다 — 이게 주 신호다
                count++;
            }
            markers?.End();
            return count;
        }

        /// <summary>열마다 한 종류. 통관–열 대응과 심볼 3종 구분을 한 장에 담는다.</summary>
        private static SpinBoard ColumnPerKind()
        {
            var board = default(SpinBoard);
            for (int row = 0; row < SpinBoard.Rows; row++)
            {
                board[0, row] = SymbolKind.NormalSoul;
                board[1, row] = SymbolKind.Absorber;
                board[2, row] = SymbolKind.Proliferator;
            }
            return board;
        }

        /// <summary>실제 추첨 결과 하나. 인위적 배열만으로는 "이렇게 나온다"를 못 보인다.</summary>
        private static SpinBoard DrawSample(int runSeed, int floor, int spinIndex)
        {
            FloorPlan plan = PrototypeCurriculum.For(floor);
            SpinRuleSet rules = PrototypeCurriculum.BuildRules(in plan);
            var engine = new SpinEngine(runSeed);
            ResistanceContract none = ResistanceContract.None;
            ResidualState residual = ResidualState.Empty;
            SpinResolution resolution = engine.SpinWithSeed(
                SpinSeed.Derive(runSeed, floor, spinIndex), rules, in none, in residual, floor, spinIndex);
            return resolution.InitialBoard;
        }

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
                yield return WaitSeconds(0.4f);
            }
            yield return WaitSeconds(2.5f);
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
                        // 연출이 끝나기를 기다렸다가 찍으면 판이 비어 있다 — 엔진이 수확·정화된
                        // 칸을 전부 비우기 때문이다. 첫 촬영이 "회색 상자 하나"로 나온 이유다.
                        //
                        // 그래서 **연쇄 중간 단계의 보드를 직접 밀어 넣는다.** 깊이의 절반쯤이
                        // 가장 판이 꽉 찬 시점이고, 그것이 "왜 연쇄가 이어졌는가"를 보여준다.
                        int step = Mathf.Clamp(depth / 2, 0, resolution.Steps.Length - 1);
                        var boardView = FindAnyObjectByType<SpinBoardView>();
                        ShowBoard(boardView, resolution.Steps[step].BoardBefore);

                        // 판만 밀어 넣으면 "심볼이 놓여 있다"까지밖에 안 보인다. 독립
                        // 평가자가 정확히 그걸 지적했다 — "연쇄를 시사하는 것이 하나도 없다.
                        // 이 그림만 보면 02_device_front 와 구별되는 건 심볼 배치뿐이다."
                        //
                        // 정화 표식은 `SpinPresenter`가 재생 중에 세운다. 판을 직접
                        // 밀어 넣으면 그 경로를 건너뛰므로 표식도 안 선다. 같은 단계의
                        // 정화 사건을 표식 뷰에 직접 먹인다 — 어느 칸이 왜 터졌는지가
                        // 선과 연결로 남는다(B-2.6). 강조는 맥동의 정점(1.0)으로 고정한다.
                        int purifies = ShowPurifies(boardView, resolution.Steps[step]);

                        yield return WaitFrames(3);
                        yield return Shot("15_cascade_deep", DeviceFront, risk,
                            $"시드 {seed} / 8층 / 사선 결속기+연쇄 조속기+증식체 계약 / " +
                            $"연쇄 {depth}단계 중 {step + 1}단계 진입 시점의 판 / " +
                            $"이 단계의 정화 {purifies}건이 표식으로 서 있다");
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
            // 사고를 만든다. **몇 층은 올라간 뒤에** 무너져야 한다 —
            // 처음에는 1층에서 곧바로 220kg 을 실어 그 층에서 죽었고, 사고 기록기 캡처가
            // "기록 1건 / 도달 0층"이 됐다. 아무 일도 없었던 런의 기록은 기록기가 무엇을
            // 설명할 수 있는지 보여주지 못한다.
            //
            // 4층까지 정상 진행한 뒤 과적을 걸어 요구 전력을 감당 못 하게 만든다.
            // 실제 플레이에서 "욕심내서 싣다가 무너지는" 경로와 같은 모양이다.
            // 시드 하나에 고정하면 안 된다. 이 자리는 원래 `555555`를 4층까지 몰고
            // 220kg 을 얹어 사고를 내는 코드였는데, 커리큘럼 재배치와 건너뛰기 금지
            // (D-20260801-01·02) 이후 그 시드는 거기서 죽지 않았다. 그래서 파일 이름은
            // `16_risk_collapse` 인데 매니페스트에는 **"실제 단계 Warning / 실패 False"**
            // 가 적힌 채로 나갔고, 독립 평가자가 "네 번째 상태가 세트에 아예 없다"고 잡았다.
            //
            // 캡처가 자기 파일 이름을 못 지키면 그 세트 전체의 신뢰가 무너진다.
            // 그래서 여러 시드·여러 층·여러 중량으로 **실제로 Collapse 에 닿을 때까지** 찾고,
            // 못 찾으면 아래에서 그 사실을 매니페스트에 그대로 적는다. 이름을 지키는 것보다
            // 못 지켰다고 말하는 것이 낫다.
            bool reachedCollapse = false;
            int usedSeed = 555555, usedFloor = 4;
            float usedWeight = 220f;

            foreach (int seed in new[] { 555555, 1337, 8675309, 31415, 90210, 20260731, 4242, 7 })
            {
                foreach (int targetFloor in new[] { 4, 6, 8 })
                {
                    foreach (float extra in new[] { 220f, 320f, 460f })
                    {
                        run.ResetRun(RunMode.TenFloor, seed);
                        yield return WaitFrames(2);
                        yield return DriveToFloor(run, bridge, targetFloor);
                        yield return WaitFrames(2);
                        run.Session.AddWeight(extra);

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

                        // 위험 뷰가 붕괴로 수렴할 시간을 준 뒤에 판정한다. 블렌딩(2.2/초)이
                        // 끝나기 전에 읽으면 도달했는데 아니라고 판정한다.
                        yield return WaitSeconds(1.6f);
                        if (risk != null && risk.Level >= RiskLevel.Collapse)
                        {
                            reachedCollapse = true;
                            usedSeed = seed; usedFloor = targetFloor; usedWeight = extra;
                        }
                        if (reachedCollapse) break;
                    }
                    if (reachedCollapse) break;
                }
                if (reachedCollapse) break;
            }

            if (!reachedCollapse)
                Debug.LogWarning("[상승] 16_risk_collapse — 어떤 조합으로도 Collapse 에 닿지 못했다. " +
                                 "매니페스트에 그대로 적는다.");

            // 붕괴에서도 결과가 읽혀야 한다 — `VISUAL_SPEC` §6이 "단순한 암전이나
            // 즉사 연출로 정보를 숨기지 않는다"고 요구한다. 그런데 사고 런은 판을
            // 비운 상태로 끝나서 "무엇이 터졌는지 알 수 없다"는 지적을 받았다.
            ShowBoard(FindAnyObjectByType<SpinBoardView>(), DrawSample(usedSeed, 2, usedFloor));
            yield return WaitSeconds(2.5f);
            yield return Shot("16_risk_collapse", Risk, risk,
                (reachedCollapse
                    ? $"Collapse 도달 — 시드 {usedSeed} / {usedFloor}층 / 추가 중량 {usedWeight:F0}kg. "
                    : "**Collapse 미도달** — 시드 8개 × 층 3개 × 중량 3종을 전부 시도했으나 " +
                      "붕괴 상태를 만들지 못했다. 이 장은 파일 이름이 주장하는 상태가 아니다. ") +
                $"실제 단계 {LevelName(risk)} / 실패 {run.Session.IsFailed} " +
                $"사유 {run.Session.FailureReason ?? "—"} / 06·09·10 과 같은 좌표");

            // 사고 기록기는 `GameHudView`가 화면에 그린다. 그런데 이 리그의 다른 샷은
            // 전용 카메라의 RenderTexture 렌더라 **화면 UI가 들어가지 않는다** — 그래서
            // 첫 촬영에서 17이 16과 픽셀 단위로 같은 그림이 됐다. 독립 평가자가
            // "사고 기록기라는 물체도, 기록 1건이라는 표시도 화면에 없다"고 지적했다.
            //
            // 이 한 장만 화면 캡처로 찍는다. 해상도가 게임 뷰에 종속되므로
            // 고정 비교 세트가 아니라는 것을 매니페스트에 남긴다.
            string record = recorder != null && recorder.Latest != null
                ? "사고 기록 있음" : "사고 기록 없음";
            yield return ScreenShot("17_accident_recorder",
                $"{record} / 기록 {(recorder != null ? recorder.Records.Count : 0)}건 / " +
                $"시드 {run.Session.Seed} / 도달 {run.Session.HighestFloorReached}층 / " +
                "**화면 캡처** — 전용 카메라 렌더에는 화면 UI가 들어가지 않아 이 한 장만 방식이 다르다. " +
                "해상도가 게임 뷰에 종속되므로 고정 비교 세트가 아니다");

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
            // 직전 샷이 Collapse 라 위험 연출이 붉게 물들어 있다. 정착 시간을 안 주면
            // "위험도 안정"인데 램프가 빨간 모순된 그림이 나온다 — 지적받은 그대로다.
            ShowBoard(FindAnyObjectByType<SpinBoardView>(), DrawSample(4242, 10, 0));
            yield return WaitSeconds(2.5f);
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
            _manifest.AppendLine($"{"",-26} {GaugeFill()}");
        }

        /// <summary>
        /// 셔터가 열린 순간 게이지가 **실제로 얼마나 차 있었는지**를 적는다.
        ///
        /// 독립 평가자가 "16은 전력 88%인데 채움이 0%인 18과 구별되지 않는다"고 했다.
        /// 그런데 그것이 가시성 문제인지(막대가 작고 대비가 낮다) 상태 문제인지
        /// (셔터가 열렸을 때 판정이 이미 끝나 전력이 0이었다) 그림만으로는 못 가른다.
        /// 판이 비어 나온 것도, 위험 단계가 안 물든 것도 전부 후자였다.
        ///
        /// 그러니 캡처가 스스로 증언하게 한다. 막대의 실제 폭이 여기 남으면 다음
        /// 평가는 "안 보인다"와 "없다"를 구분할 수 있다.
        /// </summary>
        private static string GaugeFill()
        {
            var panel = FindAnyObjectByType<InstrumentPanelView>();
            Transform pivot = panel != null ? panel.BarPivot : null;
            if (panel == null || pivot == null) return "게이지 — 계기판 없음";

            float width = panel.BarWidth;
            float filled = pivot.localScale.x;
            return $"게이지 실측 — 채움 {filled:F3} m / 전체 {width:F2} m " +
                   $"({(width > 0f ? filled / width * 100f : 0f):F0}% 길이, " +
                   $"최대 {panel.MaxRatio * 100f:F0}% 기준)";
        }

        /// <summary>
        /// 게임 뷰를 그대로 찍는다. 화면 UI(사고 기록기·HUD)가 포함되는 유일한 경로다.
        /// 전용 카메라 렌더와 달리 해상도가 에디터 창에 종속되므로 비교용이 아니다.
        /// </summary>
        private IEnumerator ScreenShot(string name, string note)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, $"{name}.png"), shot.EncodeToPNG());
                _shots++;
                _manifest.AppendLine($"{name,-26} 화면 캡처 {shot.width}×{shot.height}");
                _manifest.AppendLine($"{"",-26} {note}");
            }
            finally
            {
                Destroy(shot);
            }
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>
        /// **시간**으로 기다린다. 프레임 수가 아니다.
        ///
        /// `RiskStateView._blendSpeed`는 2.2/초라 조명·험이 새 단계로 수렴하는 데 약 1.4초가
        /// 걸린다. 그런데 에디터 Play 모드는 100fps 넘게 돌기 때문에 `WaitFrames(30)`이
        /// 0.25초밖에 안 된다 — 블렌딩이 시작만 한 시점에 셔터가 열린다.
        ///
        /// 그래서 위험 4단계 캡처가 "텍스트와 경고등만 바뀌고 방 안은 그대로"로 나왔다.
        /// 독립 평가자가 "06→09→10 사이에서 방 안의 어떤 것도 변하지 않는다"고 지적했는데,
        /// 프리셋에는 밝기 1.0 → 0.80 → 0.58 → 0.34 의 차이가 실제로 들어 있다.
        /// 연출이 없었던 것이 아니라 **캡처가 기다리지 않았다.**
        ///
        /// `HeroSliceAutoPilot`이 이미 같은 함정을 주석으로 경고해 뒀다.
        /// </summary>
        /// <remarks>
        /// `Time.realtimeSinceStartup` 이 아니라 `Time.time` 을 쓴다.
        /// <see cref="Start"/>가 `Time.captureDeltaTime = 1/60` 을 세우므로 프레임당
        /// 게임 시간이 정확히 1/60초씩 흐른다 — 벽시계로 재면 기계 부하에 따라
        /// 프레임 수가 달라지고, 블렌딩이 프레임 수만큼 진행하므로 **같은 시드로 찍은
        /// 두 캡처가 픽셀 단위로 달라진다.** 게임 시간으로 재야 프레임 수가 고정된다.
        /// </remarks>
        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline) yield return null;
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
            // 재현 조건. 프레임 시간이 고정되지 않으면 같은 시드도 다른 프레임 수에서
            // 찍혀 블렌딩 진행도가 달라진다 — 두 캡처가 바이트 단위로 갈라진다.
            _manifest.AppendLine($"captureDeltaTime {CaptureDeltaTime:F5} (vSync off) — " +
                                 "대기는 벽시계가 아니라 게임 시간으로 잰다");
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
            RestoreClock();
            if (_target != null) { _target.Release(); Destroy(_target); }
            if (_readback != null) Destroy(_readback);
        }
    }
}
