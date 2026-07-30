using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Player;
using Ascend.Prototype.Spin;
using Ascend.Prototype.View;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// PlayMode 검증 하네스. 1층 Hero Slice를 **엘리베이터 안의 물체만 써서** 끝까지 몬다.
    ///
    /// 왜 `RunSession` API를 직접 부르지 않는가: 그러면 세션 로직만 검증되고 씬 배선·상호작용
    /// 게이트·연출 잠금이 전부 빠진다. 실제로 이번 Phase에서 깨지기 쉬운 곳은 그쪽이다.
    /// 그래서 `IInteractable.Interact()`를 부른다 — `CrosshairInteractor`가 클릭 시 하는 것과
    /// 정확히 같은 호출이다. 조준 계산만 빼고 나머지 경로는 실제 플레이와 동일하다.
    ///
    /// 결과는 파일로 남긴다. Play 모드 진입은 도메인 리로드를 동반해 에디터 쪽 변수를
    /// 전부 날리기 때문에, 결과를 메모리로 되돌려 받으려는 시도는 매번 실패한다.
    /// </summary>
    public sealed class HeroSliceAutoPilot : MonoBehaviour
    {
        public const string ReportPath = "Logs/heroslice_playmode.txt";
        private const string PrefKey = "Ascend.HeroSliceAutoPilot.Armed";

        private readonly StringBuilder _report = new StringBuilder();
        private int _passed;
        private int _failed;
        private int _errorLogs;

#if UNITY_EDITOR
        /// <summary>에디터 메뉴가 무장시켜 두면 Play 진입 직후 스스로 붙는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return;
            UnityEditor.EditorPrefs.SetBool(PrefKey, false);   // 1회성 — 다음 Play는 평범하게
            var go = new GameObject("HeroSliceAutoPilot");
            go.AddComponent<HeroSliceAutoPilot>();
        }

        public static void Arm() => UnityEditor.EditorPrefs.SetBool(PrefKey, true);
#endif

        private void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            // 치명적 콘솔 오류가 하나라도 나면 통과로 볼 수 없다(Gate D).
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errorLogs++;
                _report.AppendLine($"  콘솔오류  [{type}] {condition}");
            }
        }

        private IEnumerator Start()
        {
            _report.AppendLine("=== Hero Slice PlayMode 검증 ===");
            yield return null;   // Awake/Start 한 바퀴 돌 시간

            var run = FindAnyObjectByType<RunSessionBehaviour>();
            var bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            var lever = Find<InteractableLever>("ExecutionLever");
            var panel = FindAnyObjectByType<InteractableContractPanel>();
            var tank = FindAnyObjectByType<InteractablePowerTank>();
            var overharvest = FindAnyObjectByType<InteractableOverharvestLever>();
            var presenter = FindAnyObjectByType<SpinPresenter>();

            if (run == null || bridge == null || lever == null || panel == null ||
                tank == null || overharvest == null)
            {
                Fail("씬 배선", $"run={run != null} bridge={bridge != null} lever={lever != null} " +
                                $"panel={panel != null} tank={tank != null} 과수확={overharvest != null}");
                Finish();
                yield break;
            }
            Pass("씬 배선 — 필요한 물체가 전부 있다");

            _report.AppendLine($"  시드 {run.Seed} / 모드 {run.Mode} / 연출자 {(presenter != null ? "있음" : "없음")}");

            yield return null;   // 브리지 Update가 한 번 돌아 상호작용 상태가 정해질 시간

            // ── 1. 계약 단계 게이트 ──
            FloorSession floor = run.Session.Current;
            Check("1층 진입 단계가 ContractSelection", floor.Phase == FloorPhase.ContractSelection,
                  floor.Phase.ToString());
            Check("계약 전 실행 레버 활성", lever.CanInteract, "레버가 죽어 있다");
            Check("계약 전 전력 탱크 비활성", !tank.CanInteract, "탱크가 눌린다");
            Check("계약 전 과수확 레버 잠김", !overharvest.CanInteract && !overharvest.IsUnlocked,
                  $"unlocked={overharvest.IsUnlocked} can={overharvest.CanInteract}");

            // ── 2. 계약 패널로 순환 ──
            int before = bridge.PreviewIndex;
            panel.Interact(gameObject);
            yield return null;
            Check("계약 패널이 선택지를 넘긴다", bridge.PreviewIndex != before,
                  $"{before} → {bridge.PreviewIndex}");

            // 흡수체 계약(인덱스 1)까지 돌린다.
            int guard = 0;
            while (bridge.PreviewIndex != 1 && guard++ < 8) { panel.Interact(gameObject); yield return null; }
            ResistanceContract chosen = bridge.PreviewContract;

            // ── 3. 실행 레버로 계약 확정 ──
            lever.Interact(gameObject);
            yield return null;
            Check("실행 레버가 계약을 확정한다", floor.Phase == FloorPhase.Spinning, floor.Phase.ToString());
            Check("확정된 계약이 미리보기와 같다", floor.SelectedContract.Label == chosen.Label,
                  $"{floor.SelectedContract.Label} vs {chosen.Label}");
            Check("계약 확정이 스핀을 소비하지 않는다", floor.SpinsUsed == 0, floor.SpinsUsed.ToString());
            Check("확정 후 계약 패널 비활성", !panel.CanInteract, "계약을 다시 바꿀 수 있다");

            // ── 4. 첫 스핀 + 연출 중 입력 잠금 ──
            lever.Interact(gameObject);
            yield return null;
            Check("레버 한 번으로 스핀이 진행된다", floor.SpinsUsed == 1, floor.SpinsUsed.ToString());

            if (presenter != null)
            {
                Check("스핀 직후 연출이 재생 중", presenter.IsPresenting, "연출이 시작되지 않았다");
                Check("연출 중 입력 잠금", bridge.IsLocked && !lever.CanInteract,
                      $"locked={bridge.IsLocked} lever={lever.CanInteract}");

                // 중복 입력이 상태를 손상시키지 않는가.
                int usedBefore = floor.SpinsUsed;
                for (int i = 0; i < 5; i++) { lever.Interact(gameObject); tank.Interact(gameObject); }
                yield return null;
                Check("연출 중 중복 입력이 상태를 바꾸지 않는다", floor.SpinsUsed == usedBefore,
                      $"{usedBefore} → {floor.SpinsUsed}");

                yield return WaitForPresentation(presenter);
                Check("연출 종료 후 입력 복구", !bridge.IsLocked, "잠금이 안 풀린다");
            }

            // ── 5. 요구 전력까지 스핀 ──
            guard = 0;
            while (floor.Phase == FloorPhase.Spinning && floor.SpinsRemaining > 0 && guard++ < 10)
            {
                if (!lever.CanInteract) { yield return null; continue; }
                lever.Interact(gameObject);
                yield return null;
                yield return WaitForPresentation(presenter);
            }
            _report.AppendLine($"  스핀 {floor.SpinsUsed}회 후 전력 {floor.Power:F0} / 요구 {floor.RequiredPower:F0}");
            Check("스핀 뒤 Decision 단계로 전이", floor.Phase == FloorPhase.Decision, floor.Phase.ToString());

            // ── 6. 확정과 과수확이 동시에, 대등하게 열린다 ──
            if (floor.CanBank && floor.SpinsRemaining > 0)
            {
                Check("요구 전력 달성 시 전력 탱크 활성", tank.CanInteract, "확정할 수 없다");
                Check("요구 전력 달성 시 과수확 잠금 해제", overharvest.IsUnlocked, "잠긴 채다");
                Check("Decision 단계에서 실행 레버 비활성", !lever.CanInteract,
                      "실행 레버가 세 번째 선택지로 남아 있다");

                yield return WaitForCover(overharvest);
                Check("덮개가 열려야 과수확을 당길 수 있다", overharvest.CanInteract,
                      $"cover={overharvest.IsCoverOpen}");

                // ── 7. 과수확 — 판돈이 먼저 빠진다 ──
                float powerBefore = floor.Power;
                float ante = floor.PendingAnte;
                int spinsBefore = floor.SpinsUsed;
                overharvest.Interact(gameObject);
                yield return null;
                Check("과수확이 판돈을 먼저 차감한다",
                      Mathf.Abs((powerBefore - ante) - (floor.Power - LastSpinNet(floor))) < 0.5f,
                      $"이전 {powerBefore:F1} 판돈 {ante:F1} 이후 {floor.Power:F1}");
                Check("과수확이 스핀을 소비한다", floor.SpinsUsed == spinsBefore + 1,
                      $"{spinsBefore} → {floor.SpinsUsed}");
                Check("과수확 판돈이 기록된다", floor.TotalAnte > 0f, floor.TotalAnte.ToString("F1"));
                yield return WaitForPresentation(presenter);
            }
            else
            {
                _report.AppendLine($"  (이 시드는 요구 전력 미달로 끝났다 — 과수확 경로 미검증. " +
                                   $"전력 {floor.Power:F0}/{floor.RequiredPower:F0})");
            }

            // ── 8. 층 종료 ──
            guard = 0;
            while (run.Session.Current != null && guard++ < 20)
            {
                floor = run.Session.Current;
                if (bridge.IsLocked) { yield return null; continue; }
                if (tank.CanInteract) { tank.Interact(gameObject); yield return null; continue; }
                if (lever.CanInteract && floor.Phase == FloorPhase.Spinning)
                {
                    lever.Interact(gameObject);
                    yield return null;
                    yield return WaitForPresentation(presenter);
                    continue;
                }
                yield return null;
            }

            Check("디버그 조작 없이 1층이 종료된다",
                  run.Session.IsComplete || run.Session.IsFailed,
                  $"complete={run.Session.IsComplete} failed={run.Session.IsFailed} " +
                  $"current={(run.Session.Current != null ? run.Session.Current.Phase.ToString() : "null")}");

            var results = run.Session.Results;
            Check("층 결과가 기록된다", results.Count == 1, results.Count.ToString());
            if (results.Count == 1)
                _report.AppendLine($"  결과: {results[0]}");

            Check("치명적 콘솔 오류 없음", _errorLogs == 0, $"{_errorLogs}건");

            Finish();
        }

        private static float LastSpinNet(FloorSession floor)
        {
            var history = floor.History;
            return history != null && history.Count > 0 ? history[history.Count - 1].NetPower : 0f;
        }

        private IEnumerator WaitForPresentation(SpinPresenter presenter)
        {
            if (presenter == null) yield break;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (presenter.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            if (presenter.IsPresenting) Fail("연출 종료 대기", "30초 안에 끝나지 않음 — 연출이 멈춰 있다");
        }

        private IEnumerator WaitForCover(InteractableOverharvestLever lever)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!lever.IsCoverOpen && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static T Find<T>(string name) where T : Component
        {
            foreach (T candidate in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (candidate.name == name) return candidate;
            return FindAnyObjectByType<T>();
        }

        private void Check(string name, bool condition, string detail)
        {
            if (condition) Pass(name);
            else Fail(name, detail);
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
