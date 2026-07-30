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

            bool sawBoarding = false;
            bool sawContract = false;
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
                if (_visited.Count == 0 || _visited[_visited.Count - 1] != number)
                {
                    _visited.Add(number);
                    _report.AppendLine($"  --- {number}층 (요구 {floor.RequiredPower:F0}, " +
                                       $"적재 {run.Session.CarriedWeight:F0}/{run.Session.WeightCapacity:F0}) ---");
                }
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

                    // 두 개만 태운다. 전부 태우면 과적으로 죽는 시드가 생겨 10층 진행 자체를
                    // 검증하지 못하고, 하나도 안 태우면 적재 경로가 검증되지 않는다.
                    int taken = 0;
                    while (taken < 2)
                    {
                        candidates = FindObjectsByType<InteractableBuildCandidate>(
                            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                        if (candidates.Length == 0) break;
                        candidates[0].Interact(gameObject);
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
                    if (!floor.CanBank && floor.SpinsRemaining > 0)
                    {
                        Fail($"{number}층 결정 단계", "확정도 못 하고 스핀도 남았다 — 진행 불가");
                        break;
                    }

                    // 요구 전력을 넘겼고 스핀이 남았으면 한 번은 과수확을 당겨 본다.
                    // 대표 장면이 실제로 눌리는지 검증해야 하기 때문이다.
                    if (!sawOverharvest && floor.CanBank && floor.SpinsRemaining > 0 &&
                        overharvest.IsUnlocked && overharvest.CanInteract)
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

            Check("런이 완주 또는 사고로 끝났다", session.IsComplete || session.IsFailed,
                  $"complete={session.IsComplete} failed={session.IsFailed} guard={guard}");
            Check("도달 층이 건물 높이를 넘지 않는다", session.HighestFloorReached <= 10,
                  session.HighestFloorReached.ToString());
            Check("적재 단계를 실제로 거쳤다", sawBoarding, "Boarding 단계가 한 번도 안 나왔다");
            Check("계약 단계를 실제로 거쳤다", sawContract, "ContractSelection 이 한 번도 안 나왔다");
            Check("요구 전력 전에는 과수확이 잠겨 있다", sawLockedOverharvest,
                  "요구 전력 미달 상태에서 잠금이 관찰되지 않음");
            Check("요구 전력 달성 후 과수확을 당길 수 있다", sawOverharvest,
                  "런 내내 과수확 레버가 한 번도 해제되지 않음 — 대표 선택이 존재하지 않는다");

            if (session.IsComplete && !session.IsFailed)
            {
                Check("완주 런은 10층을 거친다", _visited.Contains(10),
                      $"방문 [{string.Join(",", _visited)}]");
                Check("완주 런은 적재 층을 거친다",
                      _visited.Contains(2) && _visited.Contains(5) && _visited.Contains(8),
                      $"방문 [{string.Join(",", _visited)}]");
            }
            else
            {
                _report.AppendLine($"  사고로 종료 — {session.FailureReason} ({session.HighestFloorReached}층)");
            }

            // 사고 기록기
            var recorder = FindAnyObjectByType<AccidentRecorder>();
            Check("사고 기록기가 층마다 기록했다",
                  recorder != null && recorder.Records.Count >= _visited.Count - 1,
                  recorder == null ? "기록기 없음" : $"기록 {recorder.Records.Count}건 / 방문 {_visited.Count}층");

            Check("치명적 콘솔 오류 없음", _errorLogs == 0, $"{_errorLogs}건");
            _report.AppendLine($"  소요 {Time.realtimeSinceStartup - _startedAt:F1}초");
            Finish();
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
