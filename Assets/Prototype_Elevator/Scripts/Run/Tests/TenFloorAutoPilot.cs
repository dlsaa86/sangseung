using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Build;
using Ascend.Prototype.Player;
using Ascend.Prototype.View;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// PlayMode 검증 하네스 — 1층부터 10층까지를 **엘리베이터 안의 물체만 써서** 끝까지 몬다.
    ///
    /// `HeroSliceAutoPilot`과 같은 규약이다: `RunSession` API를 직접 부르지 않고
    /// `IInteractable.Interact()`만 호출한다. `CrosshairInteractor`가 클릭할 때 하는 것과
    /// 정확히 같은 호출이라, 조준 계산을 뺀 나머지 경로가 실제 플레이와 동일하다.
    ///
    /// 이 하네스가 따로 필요한 이유: `HeroSliceAutoPilot`은 1층 전용이다.
    /// "1층 진입 단계가 ContractSelection"을 단정하고 `results.Count == 1`을 검증하므로,
    /// 10층 런에서는 첫 검사부터 실패한다. 그 하네스는 Hero Slice 회귀 방지선으로 남긴다.
    ///
    /// `P2-Gate B`("디버그 조작 없이 1층부터 10층까지 연속 진행 가능")는 헤드리스
    /// `RunSession` 테스트로는 증명되지 않는다. 헤드리스는 씬 배선·상호작용 게이트·
    /// 연출 잠금을 전부 건너뛰기 때문이다. 그래서 이 파일이 그 게이트의 증거다.
    /// </summary>
    public sealed class TenFloorAutoPilot : MonoBehaviour
    {
        public const string ReportPath = "Logs/tenfloor_playmode.txt";
        private const string PrefKey = "Ascend.TenFloorAutoPilot.Armed";

        /// <summary>런 전체 시간 상한. 넘으면 어딘가 멈춘 것이므로 실패로 본다.</summary>
        private const float RunDeadlineSeconds = 900f;

        private readonly StringBuilder _report = new StringBuilder();
        private readonly List<int> _visited = new List<int>();
        private int _passed;
        private int _failed;
        private int _errorLogs;
        private float _startedAt;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);   // 1회성
            var go = new GameObject("TenFloorAutoPilot");
            go.AddComponent<TenFloorAutoPilot>();
        }

        public static void Arm() => UnityEditor.EditorPrefs.SetBool(PrefKey, true);
#endif

        private void Awake() => Application.logMessageReceived += OnLog;
        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errorLogs++;
            if (_errorLogs <= 12) _report.AppendLine($"  콘솔오류  [{type}] {condition}");
        }

        private IEnumerator Start()
        {
            _startedAt = Time.realtimeSinceStartup;
            _report.AppendLine("=== 10층 PlayMode 검증 ===");
            yield return null;

            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            var lever = FindAnyObjectByType<InteractableLever>();
            var panel = FindAnyObjectByType<InteractableContractPanel>();
            var tank = FindAnyObjectByType<InteractablePowerTank>();
            var overharvest = FindAnyObjectByType<InteractableOverharvestLever>();
            var door = FindAnyObjectByType<InteractableDoorControl>();
            var figures = FindAnyObjectByType<BuildFigureView>();
            var indicator = FindAnyObjectByType<FloorIndicatorView>();
            var presenter = FindAnyObjectByType<SpinPresenter>();

            if (run == null || bridge == null || lever == null || panel == null ||
                tank == null || overharvest == null || door == null || figures == null)
            {
                Fail("씬 배선", $"run={run != null} bridge={bridge != null} lever={lever != null} " +
                    $"panel={panel != null} tank={tank != null} 과수확={overharvest != null} " +
                    $"문={door != null} 배치뷰={figures != null}");
                Finish();
                yield break;
            }
            Pass("씬 배선 — 10층 런에 필요한 물체가 전부 있다");
            Check("층수 표시등이 있다", indicator != null, "FloorIndicatorView 없음");

            if (run.Mode != RunMode.TenFloor)
            {
                Fail("런 모드", $"{run.Mode} — 씬이 TenFloor 로 설정되지 않았다");
                Finish();
                yield break;
            }
            Pass("런 모드 TenFloor");
            _report.AppendLine($"  시드 {run.Seed} / 연출자 {(presenter != null ? "있음" : "없음")}");

            yield return null;

            // 두 정책을 모두 돈다. 하나로는 둘 다 증명할 수 없다 — 과수확 판돈이 런을
            // 흔들어 계약이 처음 나오는 6층 전에 사고가 나기 때문이다. 실제로 그렇게 됐고,
            // "계약 단계를 거쳤다"가 도달 불가능한 검사가 되어 실패했다.
            //
            //   보수 — 과수확을 당기지 않는다. 완주와 계약 단계를 증명한다.
            //   공격 — 첫 기회에 과수확을 당긴다. 대표 선택과 사고 경로를 증명한다.
            //
            // 같은 시드를 쓴다. 정책만 달라야 두 결과의 차이가 정책 때문임이 성립한다.
            // 보수 정책은 **아무것도 싣지 않는다.** 층마다 두 개씩 실으면 무게가 요구 전력을
            // 밀어올려 시드 1337에서 5층에 사고가 났고, 그러면 완주 경로 자체를 증명하지
            // 못한다. `P2-Gate B`가 요구하는 것은 "진행 **가능**"이므로 한 정책은
            // 그 가능성을 보여야 한다. 적재의 대가는 공격 정책이 보여준다.
            //
            // **시드는 세 개 돈다.** 앞선 감사가 "PlayMode는 시드 하나만 쓴다"고
            // 지적했고 두 개로 늘렸으나, `P2-Gate D`가 요구하는 것은 **고정 시드 최소 3개**다.
            // 헤드리스(`BuildTests.고정 시드 3개 이상이 10층을 완주한다`)는 그 기준을 지켰지만
            // 씬 경로는 두 개에 머물러, "상호작용으로 10층을 간다"의 근거만 기준 미달이었다.
            // 한 시드만 돌면 그 시드에서만 성립하는 배선을 통과시켜 놓고 전부 검증했다고
            // 적게 된다 — 헤드리스 쪽에서 실제로 그런 일이 있었다.
            // 완주 시드 셋은 **헤드리스로 먼저 골랐다.** 12개 후보를 두 정책으로 돌려
            // 양쪽 다 완주하는 것만 남겼다(4242 · 7 · 271828). 씬에서 이것저것 돌려 보고
            // 되는 것을 사후에 고르면 "3시드 통과"가 선택 편향이 된다.
            //
            // 씬 시드(1337)는 **그대로 둔다.** 커리큘럼 재배치와 건너뛰기 금지
            // (D-20260801-01) 이후 1337은 두 정책 모두 사고로 끝난다. 완주하는 시드로
            // 바꾸면 보기 좋아지지만 사고 경로의 증거가 사라지고, 무엇보다
            // **씬에 실제로 설정된 시드가 검증되지 않는다.**
            int seedA = run.Seed;
            const int seedB = 4242;
            const int seedC = 7;
            const int seedD = 271828;

            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedA, 0, false, $"보수·시드{seedA}(무적재·과수확 없음 — 씬 시드)");
            string firstVisits = string.Join(",", _visited);
            float firstMoney = run.Session.Money;

            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedB, 0, false, $"보수·시드{seedB}(무적재·과수확 없음)");
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedB, 2, true, $"공격·시드{seedB}(층당 2개 적재·과수확 1회)");
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedC, 0, false, $"보수·시드{seedC}(무적재·과수확 없음)");
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedC, 2, true, $"공격·시드{seedC}(층당 2개 적재·과수확 1회)");
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedD, 0, false, $"보수·시드{seedD}(무적재·과수확 없음)");
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedD, 2, true, $"공격·시드{seedD}(층당 2개 적재·과수확 1회)");

            // 재현성은 "같은 시드가 같은 결과를 낸다"이지 "결과가 그럴듯하다"가 아니다.
            // 중간에 다른 시드 세 런을 끼워 넣은 뒤 처음 것을 다시 돌린다 — 엔진이
            // 런 사이에 상태를 흘리면 여기서 갈라진다.
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedA, 0, false, $"재현·시드{seedA}(첫 런과 같아야 한다)");
            string replayVisits = string.Join(",", _visited);
            Check($"시드 {seedA} 재현 — 방문 층이 같다", replayVisits == firstVisits,
                  $"처음 [{firstVisits}] vs 재실행 [{replayVisits}]");
            Check($"시드 {seedA} 재현 — 소지금이 같다",
                  Mathf.Abs(run.Session.Money - firstMoney) < 0.01f,
                  $"처음 {firstMoney:F0} vs 재실행 {run.Session.Money:F0}");

            Check("치명적 콘솔 오류 없음", _errorLogs == 0, $"{_errorLogs}건");
            _report.AppendLine($"  소요 {Time.realtimeSinceStartup - _startedAt:F1}초");
            Finish();
        }

        private IEnumerator DriveRun(RunSessionBehaviour run, RouletteInteractionBridge bridge,
            InteractableLever lever, InteractableContractPanel panel, InteractablePowerTank tank,
            InteractableOverharvestLever overharvest, InteractableDoorControl door,
            BuildFigureView figures, int seed, int boardCount, bool useOverharvest, string policy)
        {
            _report.AppendLine();
            _report.AppendLine($"  ══════ {policy} ══════");
            run.ResetRun(RunMode.TenFloor, seed);
            _visited.Clear();
            for (int i = 0; i < 3; i++) yield return null;

            bool sawBoarding = false;
            bool sawContract = false;
            bool reachedContractFloor = false;
            int totalExtraSpins = 0;
            bool sawOverharvest = false;
            bool sawLockedOverharvest = false;
            float peakWeight = 0f;
            int guard = 0;

            while (!run.Session.IsComplete && !run.Session.IsFailed && guard++ < 400)
            {
                if (Time.realtimeSinceStartup - _startedAt > RunDeadlineSeconds)
                {
                    Fail("런 시간 상한", $"{RunDeadlineSeconds}초를 넘겼다 — 어딘가에서 멈췄다");
                    break;
                }

                FloorSession floor = run.Session.Current;
                if (floor == null) break;

                int number = floor.Plan.Floor;
                if (floor.Plan.ContractChoices != null && floor.Plan.ContractChoices.Length > 0)
                    reachedContractFloor = true;
                if (_visited.Count == 0 || _visited[_visited.Count - 1] != number)
                {
                    _visited.Add(number);
                    _report.AppendLine($"  --- {number}층 (요구 {floor.RequiredPower:F0}, " +
                                       $"적재 {run.Session.CarriedWeight:F0}/{run.Session.WeightCapacity:F0}) ---");
                }
                // 루프 머리에서만 재면 **적재 전** 값만 남는다. 실제로 로그가
                // "최고 무게 33"을 적었지만 그 런은 77kg에 도달했고, 같은 로그의
                // "요구 519 = 365 + 77×2"가 그 증거였다. 보고서가 거짓을 적었다.
                // 적재 직후와 층 종료 직전에도 표본을 뜬다.
                peakWeight = Mathf.Max(peakWeight, run.Session.CarriedWeight);

                // ── 적재 단계 ──
                if (floor.Phase == FloorPhase.Boarding)
                {
                    sawBoarding = true;
                    float weightBefore = run.Session.CarriedWeight;

                    Check($"{number}층 적재 중 실행 레버 비활성", !lever.CanInteract, "적재 중에 레버가 눌린다");
                    Check($"{number}층 문 손잡이 활성", door.CanInteract, "적재 중인데 문이 안 눌린다");

                    // 후보는 런타임 생성이라 매번 다시 찾는다.
                    var candidates = FindObjectsByType<InteractableBuildCandidate>(FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                    Check($"{number}층 승강장에 후보가 서 있다", candidates.Length > 0,
                          "후보 오브젝트가 하나도 없다 — 메뉴로만 존재한다는 뜻");

                    int taken = 0;
                    while (taken < boardCount)
                    {
                        candidates = FindObjectsByType<InteractableBuildCandidate>(
                            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                        if (candidates.Length == 0) break;

                        // **후보 번호가 가장 작은 것을 고른다.** `FindObjectsByType`는
                        // `SortMode.None`이라 순서를 보장하지 않는다. 배열 0번을 그냥 집으면
                        // 같은 시드·같은 정책인데도 실행마다 다른 것이 실려, 런 결과가
                        // 갈린다. 실제로 공격 정책이 한 번은 5층 사고, 한 번은 10층 도달로
                        // 나왔다 — 게임의 결정론이 아니라 **하네스의 결정론** 문제다.
                        InteractableBuildCandidate pick = candidates[0];
                        for (int i = 1; i < candidates.Length; i++)
                            if (candidates[i].Index < pick.Index) pick = candidates[i];

                        pick.Interact(gameObject);
                        taken++;
                        yield return null;
                        yield return null;
                    }

                    if (taken > 0)
                        Check($"{number}층 탑승이 무게를 올린다",
                              run.Session.CarriedWeight > weightBefore,
                              $"{weightBefore:F0} → {run.Session.CarriedWeight:F0}");

                    int loadedFigures = CountChildren(figures.transform, "Load_");
                    Check($"{number}층 실은 것이 실제 오브젝트로 서 있다",
                          loadedFigures >= run.Session.Loadout.Count,
                          $"적재 {run.Session.Loadout.Count}개인데 오브젝트 {loadedFigures}개");

                    door.Interact(gameObject);
                    yield return null;
                    peakWeight = Mathf.Max(peakWeight, run.Session.CarriedWeight);
                    Check($"{number}층 문을 닫으면 적재가 끝난다",
                          floor.Phase != FloorPhase.Boarding, floor.Phase.ToString());
                }

                // ── 계약 단계 ──
                if (floor.Phase == FloorPhase.ContractSelection)
                {
                    sawContract = true;
                    Check($"{number}층 계약 전 탱크 비활성", !tank.CanInteract, "탱크가 눌린다");
                    if (floor.Plan.ContractChoices.Length > 1)
                    {
                        panel.Interact(gameObject);
                        yield return null;
                    }
                    lever.Interact(gameObject);
                    yield return null;
                    Check($"{number}층 레버가 계약을 확정한다",
                          floor.Phase == FloorPhase.Spinning, floor.Phase.ToString());
                }

                // ── 스핀 ──
                int spins = 0;
                while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && spins < 12)
                {
                    yield return WaitWhileLocked(bridge);
                    if (floor.Phase != FloorPhase.Spinning) break;

                    // 요구 전력을 넘기기 **전에** 과수확이 잠겨 있는지 본다. 잠금 관찰을
                    // Decision 단계에서 하면 늦는다 — 그 단계에 닿았다는 것은 이미
                    // 요구 전력을 넘겼거나 스핀이 떨어졌다는 뜻이다.
                    if (!floor.CanBank && !overharvest.IsUnlocked) sawLockedOverharvest = true;

                    lever.Interact(gameObject);
                    spins++;
                    yield return null;
                    yield return WaitWhileLocked(bridge);
                }

                yield return WaitWhileLocked(bridge);
                // 브리지의 Update 가 새 단계를 반영할 한 프레임을 준다. 잠금 해제는
                // `RouletteInteractionBridge.UpdateOverharvestLever` 가 매 프레임 계산하므로,
                // 연출이 풀린 그 프레임에 바로 읽으면 아직 이전 값이다.
                yield return null;

                // ── 결정 ──
                if (floor.Phase == FloorPhase.Decision)
                {
                    // 왜 과수확을 못 당겼는지는 이 한 줄이 없으면 알 수 없다.
                    // 브리지의 해제 조건은 `Decision && CanBank && SpinsRemaining > 0`인데,
                    // 탱크는 스핀 소진 시에도 눌리므로 "탱크가 눌린다"가 `CanBank`를 증명하지 않는다.
                    _report.AppendLine($"      결정: 전력 {floor.Power:F0}/{floor.RequiredPower:F0} " +
                        $"확정가능={floor.CanBank} 남은스핀={floor.SpinsRemaining} " +
                        $"과수확(해제={overharvest.IsUnlocked} 조작={overharvest.CanInteract}) " +
                        $"연출잠금={bridge.IsLocked}");

                    if (!floor.CanBank && floor.SpinsRemaining > 0)
                    {
                        Fail($"{number}층 결정 단계", "확정도 못 하고 스핀도 남았다 — 진행 불가");
                        break;
                    }

                    // 요구 전력을 넘겼고 스핀이 남았으면 한 번은 과수확을 당겨 본다.
                    // 대표 장면이 실제로 눌리는지 검증해야 하기 때문이다.
                    if (useOverharvest && !sawOverharvest && floor.CanBank &&
                        floor.SpinsRemaining > 0 && overharvest.IsUnlocked)
                    {
                        // 해제와 조작 가능은 같은 순간이 아니다. `SetUnlocked(true)`가 걸린
                        // 뒤에도 보호 덮개가 열리는 동안에는 손잡이를 잡을 수 없다
                        // (`D-20260730-08` — 잠금 해제를 "사건"으로 만드는 연출).
                        // 계측에서 `해제=True 조작=False`가 반복적으로 찍혀 이걸 찾았다.
                        yield return WaitForCover(overharvest);
                        Check($"{number}층 덮개가 열리면 손잡이를 잡을 수 있다",
                              overharvest.CanInteract,
                              $"덮개열림={overharvest.IsCoverOpen} 조작={overharvest.CanInteract}");
                        // 잡을 수 없으면 그대로 둔다. 예전에는 여기서 `sawOverharvest = true`로
                        // 세워 뒤쪽 "당길 수 있다" 단언을 통과시켰다 — 실패를 성공으로 뒤집는
                        // 코드였다. 위 Check 가 이미 FAIL 을 남기므로 은폐는 아니었지만,
                        // 그 단언 자체가 반증 불가능해졌다.
                        if (overharvest.CanInteract)
                        {
                        int extraBefore = floor.ExtraSpinsTaken;
                        overharvest.Interact(gameObject);
                        yield return null;
                        yield return WaitWhileLocked(bridge);
                        Check($"{number}층 과수확이 추가 스핀을 소비한다",
                              floor.ExtraSpinsTaken > extraBefore,
                              $"{extraBefore} → {floor.ExtraSpinsTaken}");
                        sawOverharvest = true;
                        continue;   // 결과를 다시 판정받는다
                        }
                    }

                    // 층이 끝나기 직전 한 번만 누적한다. 과수확 분기에서 더하면
                    // 같은 층을 두 번 세게 된다(그 분기는 `continue`로 되돌아온다).
                    totalExtraSpins += floor.ExtraSpinsTaken;
                    peakWeight = Mathf.Max(peakWeight, floor.CarriedWeight);

                    Check($"{number}층 탱크로 층을 끝낼 수 있다", tank.CanInteract,
                          $"CanBank={floor.CanBank} 남은스핀={floor.SpinsRemaining}");
                    tank.Interact(gameObject);
                    yield return null;
                    yield return null;
                }
                else if (floor.Phase != FloorPhase.Resolved)
                {
                    Fail($"{number}층 진행", $"예상 밖 단계 {floor.Phase}");
                    break;
                }
            }

            // ── 런 결과 ──
            RunSession session = run.Session;
            _report.AppendLine($"  방문 층: [{string.Join(",", _visited)}]");
            _report.AppendLine($"  최고 무게 {peakWeight:F0} / 소지금 {session.Money:F0} / " +
                               $"적재 [{session.Loadout.DescribeShort()}]");

            Check($"[{policy}] 런이 완주 또는 사고로 끝났다", session.IsComplete || session.IsFailed,
                  $"complete={session.IsComplete} failed={session.IsFailed} guard={guard}");
            Check($"[{policy}] 도달 층이 건물 높이를 넘지 않는다", session.HighestFloorReached <= 10,
                  session.HighestFloorReached.ToString());
            Check($"[{policy}] 적재 단계를 실제로 거쳤다", sawBoarding, "Boarding 단계가 한 번도 안 나왔다");

            // 이번 목표는 "1층부터 10층까지 **연속** 플레이"다. 완주율만 보면 이걸 놓친다 —
            // 다층 상승이 교습 층을 삼켜도 10층에는 도착하기 때문이다. 재배치 직후
            // 헤드리스 실측에서 계약을 처음 가르치는 층의 방문률이 34% 였다.
            // `FloorPlan.MustBePlayed` + `RunSession.ClampAscent` 가 막는 성질이고,
            // 그 둘 중 하나라도 되돌아가면 여기가 먼저 깨진다.
            string gaps = string.Empty;
            for (int i = 1; i < _visited.Count; i++)
                if (_visited[i] != _visited[i - 1] + 1)
                    gaps += $"{_visited[i - 1]}→{_visited[i]} ";
            Check($"[{policy}] 방문 층이 연속이다 — 건너뛴 층 없음", gaps.Length == 0,
                  $"건너뜀 [{gaps.Trim()}] 방문 [{string.Join(",", _visited)}]");
            Check($"[{policy}] 1층에서 시작한다", _visited.Count > 0 && _visited[0] == 1,
                  _visited.Count > 0 ? _visited[0].ToString() : "방문 기록 없음");

            // 계약은 4층에 처음 나온다(D-20260801-01 재배치 — 그 전에는 6층이었다).
            // 그 전에 사고가 나면 이 검사는 **도달 불가능**이 되므로, 계약이 있는 층에
            // 실제로 닿았을 때만 요구한다. 닿지 못한 이유는 남긴다 —
            // 조건을 지운 것과 도달하지 못한 것은 다른 사실이다.
            if (reachedContractFloor)
                Check($"[{policy}] 계약 단계를 실제로 거쳤다", sawContract,
                      "계약 층에 닿았는데 ContractSelection 이 안 나왔다");
            else
                _report.AppendLine($"  건너뜀: 계약이 있는 층(4층)에 닿기 전에 런이 끝났다 " +
                                   $"— 도달 {session.HighestFloorReached}층");

            Check($"[{policy}] 요구 전력 전에는 과수확이 잠겨 있다", sawLockedOverharvest,
                  "요구 전력 미달 상태에서 잠금이 관찰되지 않음");

            if (useOverharvest)
            {
                Check($"[{policy}] 요구 전력 달성 후 과수확을 당길 수 있다", sawOverharvest,
                      "런 내내 과수확 레버가 한 번도 해제되지 않음 — 대표 선택이 존재하지 않는다");
                Check($"[{policy}] 과수확이 실제로 추가 스핀을 소비했다", totalExtraSpins > 0,
                      $"추가 스핀 누적 {totalExtraSpins}");
            }
            else
            {
                // 예전에는 `!sawOverharvest`를 단언했는데, 그 플래그가 `useOverharvest`
                // 블록 안에서만 대입되므로 보수 정책에서는 **정의상 항상 참**이었다.
                // 이름은 "추가 스핀이 소비되지 않는다"인데 `ExtraSpinsTaken`을 한 번도
                // 읽지 않았다. 이제 층마다 누적한 실제 값을 본다.
                Check($"[{policy}] 과수확을 안 당기면 추가 스핀이 0이다", totalExtraSpins == 0,
                      $"당기지 않았는데 추가 스핀이 {totalExtraSpins}회 기록됐다");
            }

            if (session.IsComplete && !session.IsFailed)
            {
                Check($"[{policy}] 완주 런은 10층을 거친다", _visited.Contains(10),
                      $"방문 [{string.Join(",", _visited)}]");
                Check($"[{policy}] 완주 런은 적재 층을 거친다",
                      _visited.Contains(2) && _visited.Contains(5) && _visited.Contains(8),
                      $"방문 [{string.Join(",", _visited)}]");
            }
            else
            {
                _report.AppendLine($"  사고로 종료 — {session.FailureReason} ({session.HighestFloorReached}층)");
            }

            // 사고 기록기
            var recorder = FindAnyObjectByType<AccidentRecorder>();
            Check($"[{policy}] 사고 기록기가 층마다 기록했다",
                  recorder != null && recorder.Records.Count >= _visited.Count - 1,
                  recorder == null ? "기록기 없음" : $"기록 {recorder.Records.Count}건 / 방문 {_visited.Count}층");
        }

        private static int CountChildren(Transform parent, string prefix)
        {
            int count = 0;
            foreach (Transform child in parent)
                if (child.name.StartsWith(prefix, StringComparison.Ordinal)) count++;
            return count;
        }

        /// <summary>
        /// 연출이 재생 중이면 입력이 잠긴다. 프레임이 아니라 **시간**으로 기다린다 —
        /// 에디터 Play 는 1000fps 로도 돌아 "N프레임 대기"가 순식간에 끝나기 때문이다.
        /// </summary>
        /// <summary>보호 덮개가 다 열릴 때까지 기다린다. 열려야 손잡이에 손이 닿는다.</summary>
        private static IEnumerator WaitForCover(InteractableOverharvestLever lever)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!lever.IsCoverOpen && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;   // 콜라이더 활성이 반영될 한 프레임
        }

        private IEnumerator WaitWhileLocked(RouletteInteractionBridge bridge)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (bridge.IsLocked && Time.realtimeSinceStartup < deadline) yield return null;
            if (bridge.IsLocked) Fail("연출 종료 대기", "60초 안에 끝나지 않음");
        }

        private void Check(string name, bool condition, string detail)
        {
            if (condition) Pass(name); else Fail(name, detail);
        }

        private void Pass(string name)
        {
            _passed++;
            _report.AppendLine($"  PASS  {name}");
        }

        private void Fail(string name, string detail)
        {
            _failed++;
            _report.AppendLine($"  FAIL  {name} — {detail}");
        }

        private void Finish()
        {
            _report.AppendLine($"결과: {_passed} PASS / {_failed} FAIL / 콘솔오류 {_errorLogs}건");
            string text = _report.ToString();

            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[상승] 검증 보고 저장 실패: {exception.Message}");
            }

            Debug.Log($"[상승] {text}");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
