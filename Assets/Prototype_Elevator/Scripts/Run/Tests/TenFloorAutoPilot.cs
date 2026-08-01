using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Build;
using Ascend.Prototype.Npc;
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

        /// <summary>
        /// **런 하나**의 시간 상한. 넘으면 그 런이 어딘가에서 멈춘 것이다.
        ///
        /// 전에는 하네스 **전체**에 900초를 걸었다. 그러면 상한이 "멈췄는가"가 아니라
        /// "런이 몇 개인가"를 재게 된다 — 실제로 적재 정책 두 런을 더하자 총 903초가 되어
        /// 마지막 재현 런이 6층에서 잘렸고, 그 잘린 런을 첫 런과 비교한 재현성 단정
        /// 두 개가 함께 실패했다. **게임이 아니라 예산이 낸 실패였고, 진단하기 어려운
        /// 모양이었다** — 로그에는 "방문 층이 다르다"만 남는다.
        ///
        /// 런당으로 바꾸면 상한이 런 수와 무관해지고, 덤으로 멈춤 감지가 900초에서
        /// 200초로 빨라진다. 실측은 런당 약 86초다.
        /// </summary>
        private const float PerRunDeadlineSeconds = 200f;

        /// <summary>
        /// 이 하네스가 도는 런 수. **아래 `DriveRun` 호출 수와 같아야 한다.**
        /// 총 예산을 여기서 유도하므로, 런을 추가하고 이 수를 안 고치면 총 예산이
        /// 런당 상한보다 먼저 걸려 **마지막 런(재현 검증)이 잘린다** — 이미 한 번 그렇게 됐다.
        /// </summary>
        private const int ExpectedRuns = 11;

        /// <summary>
        /// 하네스 전체의 뒷문. 런당 상한이 어떤 이유로든 안 걸릴 때만 쓴다.
        /// 상수로 두지 않고 런 수에서 유도한다 — 상수였을 때 런을 둘 더하자마자 깨졌다.
        /// </summary>
        private const float TotalDeadlineSeconds = PerRunDeadlineSeconds * ExpectedRuns + 300f;

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

        /// <summary>
        /// 1인칭 시점이 사람 키에 있는가.
        ///
        /// **이 단정이 없어서 카메라가 천장 위로 올라간 채 394건이 전부 통과했다.**
        /// `Head` 아래에 `CameraRig` 를 끼우자 `FirstPersonController.ApplyEyeHeight` 가
        /// 눈높이를 두 번 더해 시점이 3.22m 가 됐는데(칸 천장 3.2m),
        /// 어느 검사도 카메라가 어디 있는지 묻지 않았다. 캡처 하네스는 자기 카메라를
        /// 따로 만들어 쓰므로 그림도 멀쩡히 나왔고, 트랜스폼이 엉뚱한 곳에 있는 것은
        /// 콘솔 오류도 아니다.
        ///
        /// **Pass 3 의 시각 루브릭 전체가 이 시점에 의존한다.** 천장 위에서 찍은
        /// 그림으로 "공간의 높이가 읽히는가"를 채점하면 판정 자체가 무의미하다.
        /// 그래서 회귀선은 여기다.
        /// </summary>
        private void CheckEyeHeight()
        {
            var player = FindAnyObjectByType<Player.FirstPersonController>();
            if (player == null)
            {
                Check("1인칭 카메라 눈높이", false, "FirstPersonController 없음");
                return;
            }

            float actual = player.EyeHeightAboveRoot;
            float expected = player.ConfiguredEyeHeight;
            const float tolerance = 0.05f;

            Check($"1인칭 카메라가 눈높이에 있다 — 실측 {actual:F3}m (설정 {expected:F2}m)",
                  Mathf.Abs(actual - expected) <= tolerance,
                  $"루트 대비 {actual:F3}m 로 설정값 {expected:F2}m 에서 " +
                  $"{Mathf.Abs(actual - expected):F3}m 벗어났다 — 시점이 사람 키가 아니다");
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
            CheckEyeHeight();
            CheckProfileInjection();

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
                seedA, 0, false, $"보수·시드{seedA}(무적재·과수확 없음 — 씬 시드)", BoardStyle.ByIndex);
            string firstVisits = string.Join(",", _visited);
            float firstMoney = run.Session.Money;

            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedB, 0, false, $"보수·시드{seedB}(무적재·과수확 없음)", BoardStyle.ByIndex);
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedB, 2, true, $"공격·시드{seedB}(층당 2개 적재·과수확 1회)", BoardStyle.ByIndex);
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedC, 0, false, $"보수·시드{seedC}(무적재·과수확 없음)", BoardStyle.ByIndex);
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedC, 2, true, $"공격·시드{seedC}(층당 2개 적재·과수확 1회)", BoardStyle.ByIndex);
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedD, 0, false, $"보수·시드{seedD}(무적재·과수확 없음)", BoardStyle.ByIndex);
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedD, 2, true, $"공격·시드{seedD}(층당 2개 적재·과수확 1회)", BoardStyle.ByIndex);

            // ── 적재 정책 ──
            //
            // 앞의 일곱 런은 칸을 거의 비운 채로 돈다. 보수는 정의상 0kg 이고, 공격도
            // 후보 번호가 작은 것부터 두 개씩만 집어 최고 51kg(허용 100kg)이었다.
            // 그래서 「최대 적재 상태의 동선」·「과적」·「승객 반응」이 전부 **관측 불가**였다.
            // 반응 0건은 고장이 아니라 칸이 비어 있었다는 뜻이다.
            //
            // 두 시드를 쓰는 이유는 한 런으로는 셋을 동시에 못 보기 때문이다.
            // 헤드리스로 12개 시드를 정책에 태워 먼저 골랐다(같은 `BuildLoadPolicy` 를 쓴다).
            //
            //   1337  — 108kg / 허용 100kg. **과적**이 나고 5층에서 사고로 끝난다.
            //           적재의 대가를 보여주는 유일한 시드였다.
            //   12321 — 6칸·승객 4명으로 10층을 완주한다. 최대 적재로 열 층을 도는 그림은
            //           완주하는 시드에서만 나온다.
            //
            // 여기서 과수확은 쓰지 않는다. 적재의 효과와 과수확의 판돈이 겹치면
            // 사고 원인이 둘 중 무엇인지 말할 수 없다.
            const int seedLoadOver = 1337;
            const int seedLoadFull = 12321;
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedLoadOver, BuildLoadout.MaxSlots, false,
                $"적재·시드{seedLoadOver}(칸을 채운다 — 과적 예상)", BoardStyle.MaxLoad);
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedLoadFull, BuildLoadout.MaxSlots, false,
                $"적재·시드{seedLoadFull}(칸을 채운다 — 완주 예상)", BoardStyle.MaxLoad);

            // 재현성은 "같은 시드가 같은 결과를 낸다"이지 "결과가 그럴듯하다"가 아니다.
            // 중간에 다른 시드 세 런을 끼워 넣은 뒤 처음 것을 다시 돌린다 — 엔진이
            // 런 사이에 상태를 흘리면 여기서 갈라진다.
            yield return DriveRun(run, bridge, lever, panel, tank, overharvest, door, figures,
                seedA, 0, false, $"재현·시드{seedA}(첫 런과 같아야 한다)", BoardStyle.ByIndex);
            string replayVisits = string.Join(",", _visited);
            Check($"시드 {seedA} 재현 — 방문 층이 같다", replayVisits == firstVisits,
                  $"처음 [{firstVisits}] vs 재실행 [{replayVisits}]");
            Check($"시드 {seedA} 재현 — 소지금이 같다",
                  Mathf.Abs(run.Session.Money - firstMoney) < 0.01f,
                  $"처음 {firstMoney:F0} vs 재실행 {run.Session.Money:F0}");

            // **완주를 요구하는 단정.** 이것이 없던 동안 무슨 일이 가능했나:
            // 층 검사 전부가 `if (완주)` 안에 있어서, 모든 런이 1층에서 사고로 끝나도
            // 하나도 실행되지 않고 0 FAIL 이 나온다. 이 프로젝트가 헤드리스 쪽에서
            // 이미 한 번 당한 "실패 런을 continue 로 넘긴다" 패턴 그대로다.
            // Gate B 의 근거가 로그를 눈으로 읽는 것뿐이었다.
            //
            // 최소 3회인 이유: `P2-Gate D` 가 고정 시드 3개와 10층 런 3회를 요구한다.
            // 완주 시드 셋을 헤드리스로 미리 골랐으므로 정책당 하나만 성공해도 3이 된다.
            Check($"10층 완주가 최소 3회 — 실제 {_runsCompleted}/{_runsDriven}회",
                  _runsCompleted >= 3,
                  $"완주 {_runsCompleted}회 / 구동 {_runsDriven}회 — 시드 [{_completedSeeds}]");
            Check($"서로 다른 완주 시드가 최소 3개 — 실제 {_completedSeedSet.Count}개",
                  _completedSeedSet.Count >= 3,
                  $"완주 시드 [{_completedSeeds}]");

            // ── 적재가 실제로 일어났는가 ──
            //
            // 이 네 줄이 여섯 항목의 증거다. 없는 동안 그 여섯은 「구현했다」와
            // 「관측했다」를 구분할 방법이 없었다.
            _report.AppendLine();
            _report.AppendLine($"  ══════ 적재 관측 (전 런 누적) ══════");
            _report.AppendLine($"  최대 {_peakLoadSlots}/{BuildLoadout.MaxSlots}칸 · " +
                               $"승객 최대 {_peakPassengersHeld}명 · " +
                               $"최고 {_peakWeightSeen:F0}kg / 허용 {_capacityAtPeak:F0}kg" +
                               (_sawOverCapacity
                                   ? $" · 과적 최대 {_overWeight:F0}/{_overCapacityAt:F0}kg"
                                   : " · 과적 없음"));
            _report.AppendLine($"  승객반응 시작 {_reactionsStarted} · 억제 {_reactionsSuppressed} · " +
                               $"최대동시 {_peakConcurrentReactions}(상한 {_maxConcurrentSeen})");

            Check($"칸을 끝까지 채운 적이 있다 — {_peakLoadSlots}/{BuildLoadout.MaxSlots}칸",
                  _peakLoadSlots >= BuildLoadout.MaxSlots,
                  $"최대 {_peakLoadSlots}칸 — 최대 적재 상태를 한 번도 안 만들었으면 " +
                  "「최대 적재에서도 동선이 유지된다」는 검증되지 않은 것이다");

            // **상한보다 많이 태운 적이 있는가**를 묻는다. `BuildLoadPolicy.PassengerFloor` 와
            // 비교하면 정책이 지키는 값과 검사가 요구하는 값이 같은 상수가 되어, 그 상수를
            // 낮추면 둘이 함께 내려가고 아무 검사도 실패하지 않는다. 실제 불변식은
            // 「§9.4 상한을 관측하려면 승객이 상한보다 많아야 한다」이다.
            Check($"승객이 동시 반응 상한보다 많이 탄 적이 있다 — {_peakPassengersHeld}명 / 상한 {_maxConcurrentSeen}",
                  _maxConcurrentSeen > 0 && _peakPassengersHeld > _maxConcurrentSeen,
                  $"최대 {_peakPassengersHeld}명 · 상한 {_maxConcurrentSeen} — " +
                  "승객이 상한 이하면 §9.4 는 관측 불가이지 충족이 아니다");

            Check($"허용 중량을 넘긴 적이 있다 — {_overWeight:F0}/{_overCapacityAt:F0}kg",
                  _sawOverCapacity,
                  $"최고 {_peakWeightSeen:F0}kg 이 허용 {_capacityAtPeak:F0}kg 을 넘지 못했다 — " +
                  "과적 경고 경로가 도달 불가");

            Check($"승객 반응이 실제로 시작됐다 — 승객 최대 {_peakPassengersHeld}명에 {_reactionsStarted}건",
                  _reactionsStarted > 0,
                  _peakPassengersHeld > 0
                      ? $"승객이 최대 {_peakPassengersHeld}명 탔는데 반응 0건 — 배선 문제다"
                      : "승객이 한 명도 안 탔다 — 반응 0건은 배선이 아니라 관측 불가다");

            // **이것이 §9.4 의 유일한 단정이다.** 없으면 `Notify` 의 동시 수 가드가 사라져
            // 승객 전원이 한 사건에 반응해도 `_reactionsStarted > 0` 이 **더 확실히** 통과한다 —
            // 회귀가 유일한 단정을 강화하는 상태가 된다.
            //
            // 억제 수를 근거로 쓰지 않는 이유: `SuppressedCount` 는 「아무도 못 했다」에서만
            // 오르고, 그 대부분은 동시 수 상한이 아니라 **쿨다운**(4~10초)이다.
            Check($"동시 반응이 상한을 넘지 않았다 — 최대동시 {_peakConcurrentReactions} / 상한 {_maxConcurrentSeen}",
                  _sawReactionView && _maxConcurrentSeen > 0 &&
                  _peakPassengersHeld > _maxConcurrentSeen &&
                  _peakConcurrentReactions <= _maxConcurrentSeen,
                  $"뷰={_sawReactionView} 상한={_maxConcurrentSeen} 승객최대={_peakPassengersHeld} " +
                  $"최대동시={_peakConcurrentReactions} — 상한 초과이거나 관측 조건이 안 섰다");

            Check("치명적 콘솔 오류 없음", _errorLogs == 0, $"{_errorLogs}건");
            _report.AppendLine($"  소요 {Time.realtimeSinceStartup - _startedAt:F1}초");
            Finish();
        }

        private int _runsDriven;
        private int _runsCompleted;
        private string _completedSeeds = string.Empty;
        private readonly System.Collections.Generic.HashSet<int> _completedSeedSet =
            new System.Collections.Generic.HashSet<int>();

        // ── 적재 관측 (모든 런에 걸쳐 누적) ──
        // 런 하나로는 셋을 동시에 못 본다. 과적은 사고로 끝나는 시드에서 나오고,
        // 승객 4명이 열 층을 함께 가는 그림은 완주하는 시드에서만 나온다.
        private int _peakLoadSlots;
        private int _peakPassengersHeld;
        private float _peakWeightSeen;
        private float _capacityAtPeak;
        private bool _sawOverCapacity;
        private float _overWeight;
        private float _overCapacityAt;
        private int _reactionsStarted;
        private int _reactionsSuppressed;
        private int _peakConcurrentReactions;
        private int _maxConcurrentSeen;
        private bool _sawReactionView;
        private bool _budgetExhausted;

        /// <summary>
        /// 적재 방식. <c>boardCount</c> 하나로는 「몇 개」만 말할 수 있고 「무엇을」은 말할 수 없다.
        /// </summary>
        private enum BoardStyle
        {
            /// <summary>후보 번호 순. 기존 아홉 런의 동작을 그대로 보존한다.</summary>
            ByIndex = 0,

            /// <summary>칸이 찰 때까지 <see cref="BuildLoadPolicy"/>가 고른 것을 집는다.</summary>
            MaxLoad = 1,
        }

        private IEnumerator DriveRun(RunSessionBehaviour run, RouletteInteractionBridge bridge,
            InteractableLever lever, InteractableContractPanel panel, InteractablePowerTank tank,
            InteractableOverharvestLever overharvest, InteractableDoorControl door,
            BuildFigureView figures, int seed, int boardCount, bool useOverharvest, string policy,
            BoardStyle style)
        {
            // 예산이 이미 바닥났으면 남은 런은 돌지 않는다. 돌아봐야 각자 즉시 같은 검사에
            // 걸려 FAIL 만 늘리고, 그 더미 속에서 진짜 원인이 안 보인다.
            if (_budgetExhausted)
            {
                _report.AppendLine();
                _report.AppendLine($"  ══════ {policy} — 예산 초과로 건너뜀 ══════");
                yield break;
            }

            _report.AppendLine();
            _report.AppendLine($"  ══════ {policy} ══════");
            _runsDriven++;
            float runStartedAt = Time.realtimeSinceStartup;
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
                float now = Time.realtimeSinceStartup;
                FloorSession floor = run.Session.Current;

                if (now - runStartedAt > PerRunDeadlineSeconds)
                {
                    // 「어딘가에서 멈췄다」가 어디인지 로그에 남겨야 진단이 된다.
                    Fail($"[{policy}] 런 시간 상한",
                         $"이 런 하나가 {PerRunDeadlineSeconds:F0}초를 넘겼다 — " +
                         $"{(floor != null ? floor.Plan.Floor.ToString() : "?")}층 " +
                         $"단계={(floor != null ? floor.Phase.ToString() : "?")} " +
                         $"연출잠금={bridge.IsLocked} 방문=[{string.Join(",", _visited)}]");
                    break;
                }
                if (now - _startedAt > TotalDeadlineSeconds)
                {
                    // 여기서 `break` 만 하면 남은 런이 계속 돌면서 같은 검사에 즉시 걸려
                    // FAIL 이 연쇄로 쌓인다. 한 번의 예산 초과가 열 줄의 실패로 번지면
                    // 원인이 묻힌다. 그래서 하네스를 끝낸다.
                    Fail("하네스 전체 시간 상한",
                         $"{TotalDeadlineSeconds:F0}초({ExpectedRuns}런 × {PerRunDeadlineSeconds:F0}초 + 여유)를 " +
                         $"넘겼다 — {_runsDriven}번째 런에서. 런을 추가했으면 ExpectedRuns 도 고칠 것");
                    _budgetExhausted = true;
                    break;
                }

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
                SampleLoad(run.Session);

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

                    // `MaxLoad` 는 `boardCount` 를 보지 않는다. 두 축이 함께 상한을 걸면
                    // `DriveRun(..., 0, ..., BoardStyle.MaxLoad)` 가 컴파일되고 정책이 한 번도
                    // 불리지 않은 채 적재 관측 넷이 0으로 실패한다 — 실패 메시지는 정책을 가리킨다.
                    int taken = 0;
                    while (!run.Session.Loadout.IsFull &&
                           (style == BoardStyle.MaxLoad || taken < boardCount))
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

                        // 적재 정책은 **무엇을** 집을지까지 정한다. 번호 순으로 집으면
                        // 아홉 런 전부에서 칸이 거의 비었고(최고 51kg / 허용 100kg),
                        // 승객이 없으니 승객 반응은 「시작 0건」으로 찍혔다 —
                        // 고장이 아니라 관측 불가였다. 규칙은 `BuildLoadPolicy` 한 곳에
                        // 있고 헤드리스 시드 탐색이 같은 것을 쓴다.
                        if (style == BoardStyle.MaxLoad)
                        {
                            int want = BuildLoadPolicy.PickIndex(floor.BuildOffers, run.Session.Loadout);
                            if (want < 0) break;

                            InteractableBuildCandidate byPolicy = null;
                            for (int i = 0; i < candidates.Length; i++)
                                if (candidates[i].Index == want) { byPolicy = candidates[i]; break; }

                            // **번호가 아니라 물건을 확인한다.** `want` 는 줄어드는
                            // `floor.BuildOffers` 의 인덱스이고 `candidate.Index` 는 뷰가 마지막에
                            // 재빌드한 시점의 인덱스다. 오늘은 취득마다 두 프레임을 줘서 일치하지만,
                            // 그 정합성은 **프레임 관습에 걸려 있다** — 뷰가 그 프레임에 재빌드하지
                            // 못하면 번호는 맞는데 다른 물건을 집는다. 번호만 보는 검사는 그대로 통과한다.
                            string wantedId = floor.BuildOffers[want].Id;
                            bool matches = byPolicy != null &&
                                           byPolicy.name == "Candidate_" + wantedId;
                            Check($"{number}층 정책이 고른 {floor.BuildOffers[want].Label} 을 집는다",
                                  matches,
                                  $"{want}번을 원했는데 {(byPolicy == null ? "그 번호가 없다" : byPolicy.name)} · " +
                                  $"후보 {candidates.Length}개 · 제시 {floor.BuildOffers.Count}개");
                            if (!matches) break;
                            pick = byPolicy;
                        }

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
                    SampleLoad(run.Session);
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
            SampleLoad(session);
            CollectReactions();
            _report.AppendLine($"  방문 층: [{string.Join(",", _visited)}]");
            _report.AppendLine($"  최고 무게 {peakWeight:F0} / 소지금 {session.Money:F0} / " +
                               $"적재 [{session.Loadout.DescribeShort()}] / " +
                               $"소요 {Time.realtimeSinceStartup - runStartedAt:F1}초");

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

            // 무엇을 **안** 봤는지 적는다. 완주 단정 집합이 ClampAscent 가 이미 보장하는
            // 층과 같으면 그 단정은 실패할 수 없다 — 실제로 그런 상태였다.
            // 이 줄은 단정이 아니라 관측이다. 사고로 끝난 런에서는 남은 층이 당연히 미방문이다.
            var unseen = new StringBuilder();
            for (int floor = 1; floor <= 10; floor++)
                if (!_visited.Contains(floor)) unseen.Append(floor).Append(' ');
            _report.AppendLine(unseen.Length == 0
                ? "  미방문 층: 없음 — 열 층을 모두 밟았다"
                : $"  미방문 층: [{unseen.ToString().Trim()}]");

            bool completed = session.IsComplete && !session.IsFailed && _visited.Contains(10);
            if (completed)
            {
                _runsCompleted++;
                _completedSeedSet.Add(seed);
                _completedSeeds += seed + " ";
            }

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

        /// <summary>
        /// 프로파일 에셋이 **실제로 읽혔는가**를 본다. 배선됐는가가 아니다.
        ///
        /// 이 검사가 필요한 이유: `.asset` 7종이 만들어지고도 런타임 소비처가 0곳이라
        /// 아무 데도 흐르지 않았다(`DEAD_IMPLEMENTATION_AUDIT.md` §1). 인스펙터에 물려 있는
        /// 것과 코드가 그 값을 읽는 것은 다르고, **둘을 구분하지 않으면 「데이터화했다」가
        /// 참인지 알 수 없다.** 그래서 각 소비자가 자기 출처 이름을 내놓게 하고 그것을 읽는다 —
        /// 폴백이면 "코드 프리셋"·"인스펙터 슬라이더"라고 적히므로 통과하지 않는다.
        /// </summary>
        private void CheckProfileInjection()
        {
            Risk.RiskStateView risk = FindAnyObjectByType<Risk.RiskStateView>();
            Check("위험 연출이 DangerFeedbackProfile 을 읽는다",
                  risk != null && risk.ProfileSource.EndsWith("Profile"),
                  risk == null ? "RiskStateView 없음" : $"출처 「{risk.ProfileSource}」");

            Audio.AudioDirector audio = FindAnyObjectByType<Audio.AudioDirector>();
            Check("오디오가 AudioMixProfile 을 읽는다",
                  audio != null && audio.MixSource.EndsWith("Profile"),
                  audio == null ? "AudioDirector 없음" : $"출처 「{audio.MixSource}」");

            Perf.RenderBudgetProbe probe = FindAnyObjectByType<Perf.RenderBudgetProbe>();
            if (probe != null)
            {
                // **통과를 요구하지 않는다.** 지금 측정은 816×714 이고 기준은 1920×1080 이다.
                // 조건이 다르다는 사실이 로그에 남는 것이 이번 목표다 — 그것 없이는
                // 「90 FPS 목표를 만족한다」가 무엇에 대한 문장인지 알 수 없다.
                _report.AppendLine($"  렌더 예산 측정 조건이 기준과 " +
                                   $"{(probe.MeasuredAtReference ? "같다" : "다르다")} " +
                                   $"— {Screen.width}×{Screen.height} / vSync {QualitySettings.vSyncCount}");
            }
        }

        /// <summary>
        /// 적재 최고치를 거둔다. 층 머리에서만 재면 **적재 전** 값만 남는다 —
        /// 무게 쪽에서 이미 한 번 당했고(보고서가 "최고 무게 33"을 적었지만 실제로는 77kg),
        /// 칸 수·승객 수도 같은 함정을 갖는다. 승객은 목적지에서 내리므로 층이 끝난 뒤에
        /// 세면 언제나 더 적다.
        /// </summary>
        private void SampleLoad(RunSession session)
        {
            if (session == null || session.Loadout == null) return;

            if (session.Loadout.Count > _peakLoadSlots) _peakLoadSlots = session.Loadout.Count;

            int passengers = BuildLoadPolicy.CountPassengers(session.Loadout);
            if (passengers > _peakPassengersHeld) _peakPassengersHeld = passengers;

            if (session.CarriedWeight > _peakWeightSeen)
            {
                _peakWeightSeen = session.CarriedWeight;
                _capacityAtPeak = session.WeightCapacity;
            }

            // 과적이었던 **그 순간**의 쌍을 따로 남긴다. 최고 무게였던 순간과 과적이었던
            // 순간은 다를 수 있다 — 짐꾼(허용 +30)이 실린 동안 120/130(과적 아님)을 찍고
            // 하차 뒤 105/100(과적)이 되면, 한 쌍만 들고 다니는 보고서는
            // 「허용 중량을 넘긴 적이 있다 — 최고 120/130kg」이라는 **스스로를 반증하는**
            // 문장을 낸다. 이 저장소가 이미 당한 "전력 314/요구 355인데 게이지 0"과 같은 모양이다.
            if (session.CarriedWeight > session.WeightCapacity)
            {
                _sawOverCapacity = true;
                if (session.CarriedWeight - session.WeightCapacity > _overWeight - _overCapacityAt)
                {
                    _overWeight = session.CarriedWeight;
                    _overCapacityAt = session.WeightCapacity;
                }
            }
        }

        /// <summary>
        /// 승객 반응 누적치를 런이 끝나기 전에 거둔다. 다음 런의 `ResetRun` 이
        /// 뷰의 누적을 0 으로 되돌리므로 여기서 안 거두면 영영 못 본다.
        /// </summary>
        private void CollectReactions()
        {
            // `FindAnyObjectByType` 는 기본이 `FindObjectsInactive.Exclude` 다. 뷰가 비활성
            // 오브젝트로 옮겨가면 null 이 오는데, 조용히 `return` 하면 리포트에 아무 줄도 남지
            // 않고 최종 단정만 「반응 0건 — 배선 문제다」로 실패한다. 그 문구는 **다른 상황**을
            // 가리킨다. 이 저장소의 원형 실패(비활성 오브젝트 위의 죽은 파이프라인)가 정확히 이 모양이다.
            PassengerReactionView view = FindAnyObjectByType<PassengerReactionView>();
            if (view == null)
            {
                Fail("승객 반응 뷰가 씬에 있다",
                     "FindAnyObjectByType 가 null — 비활성 오브젝트 위인지 확인할 것");
                return;
            }
            _sawReactionView = true;

            _reactionsStarted += view.TotalStartedCount;
            _reactionsSuppressed += view.TotalSuppressedCount;
            if (view.PeakActiveCount > _peakConcurrentReactions)
                _peakConcurrentReactions = view.PeakActiveCount;
            if (view.MaxConcurrent > _maxConcurrentSeen) _maxConcurrentSeen = view.MaxConcurrent;

            _report.AppendLine($"  승객반응 시작 {view.TotalStartedCount} / 억제 {view.TotalSuppressedCount}" +
                               $" / 최대동시 {view.PeakActiveCount}(상한 {view.MaxConcurrent})" +
                               $" / 승객 {view.PassengerCount}명");
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
