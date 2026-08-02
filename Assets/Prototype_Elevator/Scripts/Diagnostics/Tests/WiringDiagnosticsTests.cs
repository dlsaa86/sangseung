using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Player;

namespace Ascend.Prototype.Diagnostics.Tests
{
    /// <summary>
    /// 필수 참조 검사기(`UP-TECH-03`)의 헤드리스 검증.
    ///
    /// **왜 이 스위트가 필요한가:** 「결함 0건」은 검사기가 건강할 때도, 검사기가
    /// 아무것도 안 볼 때도 똑같이 나온다. 항상 참인 검사는 통과가 아니라 미검증이다.
    /// 그래서 여기서는 **일부러 빈 대상을 만들어 잡히는지**(음성 대조)와
    /// **채워진 대상은 잡지 않는지**(거짓 양성 방지)를 짝으로 본다.
    ///
    /// 씬을 열지 않는다. `SceneWiringValidator.CollectDefects` 가 `object` 를 받으므로
    /// 평범한 C# 클래스로 검사기의 판정 규칙 전부를 재현할 수 있다.
    /// 실제 씬에서의 대조는 `TenFloorAutoPilot.CheckRequiredReferenceGuard` 가 본다 —
    /// 둘 다 필요하다. 규칙이 맞는 것과 씬에 적용되는 것은 다른 이야기다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class WiringDiagnosticsTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("빈 필수 참조를 전부 잡는다 (음성 대조)", TestEmptyFieldsAreCaught, ref passed, ref failed, report);
            Run("채워진 필수 참조는 잡지 않는다", TestFilledFieldsAreNotCaught, ref passed, ref failed, report);
            Run("표시하지 않은 필드는 훑지 않는다", TestUnmarkedFieldIsIgnored, ref passed, ref failed, report);
            Run("빈 문자열·길이 0 배열도 비어 있는 것으로 본다", TestEmptyStringAndArray, ref passed, ref failed, report);
            Run("부모 클래스의 필수 필드도 잡는다", TestInheritedFieldIsCaught, ref passed, ref failed, report);
            Run("파괴된 Unity 객체를 살아 있는 것으로 보지 않는다", TestDestroyedUnityObjectIsEmpty, ref passed, ref failed, report);
            Run("보고가 경로·타입·필드·원인을 함께 낸다", TestDescribeCarriesPathAndCause, ref passed, ref failed, report);
            Run("원인 문구가 없어도 경로와 필드는 남는다", TestDescribeWithoutConsequence, ref passed, ref failed, report);
            Run("null 인스턴스를 넘겨도 터지지 않는다", TestNullInstanceIsSafe, ref passed, ref failed, report);
            Run("씬 로드 직후 자동 실행 진입점이 존재한다", TestAutoEntryPointExists, ref passed, ref failed, report);
            Run("플레이어 3종에 필수 표시가 남아 있다", TestPlayerComponentsStayMarked, ref passed, ref failed, report);
            Run("에디터에서 디버그 도구가 허용된다", TestDebugToolsAllowedInEditor, ref passed, ref failed, report);
            Run("릴리스 가드가 Start 와 Update 둘 다에 걸려 있다", TestDebugPanelGuardsBothEntryPoints, ref passed, ref failed, report);
            Run("테이프가 기록의 값을 그대로 쓴다 (재계산하지 않는다)", TestPrinterUsesRecordValues, ref passed, ref failed, report);
            Run("그릴 대상이 없으면 줄이 쌓여도 그려지지 않는다", TestPrinterCounterIsNotProofOfDrawing, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Diagnostics (필수 참조 배선) Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        private static void Run(string name, Func<string> test,
                                ref int passed, ref int failed, StringBuilder report)
        {
            try
            {
                string failure = test();
                if (string.IsNullOrEmpty(failure)) { passed++; report.AppendLine($"  PASS  {name}"); }
                else { failed++; report.AppendLine($"  FAIL  {name} — {failure}"); }
            }
            catch (Exception exception)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외: {exception.Message}");
            }
        }

        // ── 검사 대상 대역 ──────────────────────────────────────────────────────

        /// <summary>상속 경로를 만들기 위한 부모. 검사기가 base 체인을 훑는지 본다.</summary>
        private class FakeBase
        {
            [RequiredReference("부모가 들고 있는 필수 참조")]
            public object InheritedRef;
        }

        /// <summary>
        /// `MonoBehaviour` 를 **일부러 상속하지 않는다.** 상속하면 GameObject 가 필요해지고,
        /// 그러면 이 스위트가 씬을 더럽히거나 아예 못 돌게 된다.
        /// </summary>
        private sealed class FakeTarget : FakeBase
        {
            [RequiredReference("문이 열리지 않는다")]
            public object Door;

            [RequiredReference("표시가 비면 아무것도 읽을 수 없다")]
            public string Label;

            [RequiredReference("표식 배열이 비면 정화가 보이지 않는다")]
            public int[] Markers;

            /// <summary>표시하지 않은 필드. 비어 있어도 보고되면 안 된다.</summary>
            public object NotMarked;
        }

        /// <summary>원인 문구 없이 표시만 한 경우.</summary>
        private sealed class FakeBareTarget
        {
            [RequiredReference]
            public object Bare;
        }

        /// <summary>
        /// 파괴 가능한 Unity 객체 대역. `ScriptableObject` 를 쓰는 이유는
        /// **씬을 건드리지 않기 때문**이다 — `GameObject` 를 만들면 이 스위트가
        /// 열려 있는 씬을 더럽히고, 그러면 씬 오너의 작업과 충돌한다.
        /// </summary>
        private sealed class FakeAsset : ScriptableObject
        {
        }

        /// <summary>파괴된 Unity 객체를 담아 두기 위한 대역.</summary>
        private sealed class FakeUnityHolder
        {
            [RequiredReference("파괴된 참조는 없는 것과 같다")]
            public FakeAsset Asset;
        }

        private static FakeTarget FilledTarget()
        {
            return new FakeTarget
            {
                InheritedRef = new object(),
                Door = new object(),
                Label = "문",
                Markers = new[] { 1 },
                NotMarked = null,
            };
        }

        private static bool HasDefect(List<WiringDefect> defects, string fieldName)
        {
            for (int i = 0; i < defects.Count; i++)
                if (defects[i].FieldName == fieldName) return true;
            return false;
        }

        // ── 판정 규칙 ───────────────────────────────────────────────────────────

        private static string TestEmptyFieldsAreCaught()
        {
            var defects = new List<WiringDefect>();
            int checkedFields = SceneWiringValidator.CollectDefects(new FakeTarget(), "씬/가짜", defects);

            if (checkedFields != 4) return $"검사한 필드 {checkedFields}, 기대 4 (표시된 것만 세야 한다)";
            if (defects.Count != 4) return $"결함 {defects.Count}, 기대 4 — {Join(defects)}";

            foreach (string field in new[] { "Door", "Label", "Markers", "InheritedRef" })
                if (!HasDefect(defects, field)) return $"{field} 를 놓쳤다 — {Join(defects)}";

            return null;
        }

        /// <summary>
        /// 거짓 양성 방지. 이 단정이 없으면 「전부 결함이라고 말하는 검사기」도
        /// 위의 음성 대조를 통과한다.
        /// </summary>
        private static string TestFilledFieldsAreNotCaught()
        {
            var defects = new List<WiringDefect>();
            int checkedFields = SceneWiringValidator.CollectDefects(FilledTarget(), "씬/가짜", defects);

            if (checkedFields != 4) return $"검사한 필드 {checkedFields}, 기대 4";
            if (defects.Count != 0) return $"채워진 대상에서 결함 {defects.Count}건 — {Join(defects)}";
            return null;
        }

        private static string TestUnmarkedFieldIsIgnored()
        {
            FakeTarget target = FilledTarget();
            target.NotMarked = null;

            var defects = new List<WiringDefect>();
            SceneWiringValidator.CollectDefects(target, "씬/가짜", defects);

            if (HasDefect(defects, "NotMarked"))
                return "표시하지 않은 필드를 보고했다 — 보고가 소음이 되면 진짜 실패가 묻힌다";
            return null;
        }

        private static string TestEmptyStringAndArray()
        {
            FakeTarget target = FilledTarget();
            target.Label = string.Empty;
            target.Markers = new int[0];

            var defects = new List<WiringDefect>();
            SceneWiringValidator.CollectDefects(target, "씬/가짜", defects);

            if (!HasDefect(defects, "Label"))
                return "빈 문자열을 통과시켰다 — 연결됐지만 아무 값도 없는 것은 null 과 결과가 같다";
            if (!HasDefect(defects, "Markers"))
                return "길이 0 배열을 통과시켰다";
            if (defects.Count != 2) return $"결함 {defects.Count}, 기대 2 — {Join(defects)}";
            return null;
        }

        private static string TestInheritedFieldIsCaught()
        {
            FakeTarget target = FilledTarget();
            target.InheritedRef = null;

            var defects = new List<WiringDefect>();
            SceneWiringValidator.CollectDefects(target, "씬/가짜", defects);

            if (defects.Count != 1 || !HasDefect(defects, "InheritedRef"))
                return $"부모 필드를 놓쳤다 — {Join(defects)}";
            return null;
        }

        /// <summary>
        /// `object` 로 박싱된 Unity 객체에 `== null` 을 쓰면 오버로드가 아니라 참조 비교가
        /// 걸려 **파괴된 객체가 살아 있는 것처럼 보인다.** 검사기가 그 함정을 피했는지 본다.
        /// 씬을 건드리지 않도록 `ScriptableObject` 로 재현한다.
        /// </summary>
        private static string TestDestroyedUnityObjectIsEmpty()
        {
            var holder = new FakeUnityHolder { Asset = ScriptableObject.CreateInstance<FakeAsset>() };

            var alive = new List<WiringDefect>();
            SceneWiringValidator.CollectDefects(holder, "씬/가짜", alive);
            if (alive.Count != 0) return $"살아 있는 에셋을 결함으로 봤다 — {Join(alive)}";

            UnityEngine.Object.DestroyImmediate(holder.Asset);

            var destroyed = new List<WiringDefect>();
            SceneWiringValidator.CollectDefects(holder, "씬/가짜", destroyed);
            if (destroyed.Count != 1 || !HasDefect(destroyed, "Asset"))
                return $"파괴된 참조를 살아 있는 것으로 봤다 — {Join(destroyed)}";
            return null;
        }

        // ── 보고 형식 ───────────────────────────────────────────────────────────

        private static string TestDescribeCarriesPathAndCause()
        {
            var defects = new List<WiringDefect>();
            SceneWiringValidator.CollectDefects(new FakeTarget(), "엘리베이터/조작반/문패널", defects);

            string described = null;
            for (int i = 0; i < defects.Count; i++)
                if (defects[i].FieldName == "Door") described = defects[i].Describe();

            if (described == null) return "Door 결함이 없다";
            if (!described.Contains("엘리베이터/조작반/문패널"))
                return $"경로가 빠졌다 — 「{described}」. 「참조가 없다」만으로는 고칠 수 없다";
            if (!described.Contains("FakeTarget")) return $"컴포넌트 이름이 빠졌다 — 「{described}」";
            if (!described.Contains("Door")) return $"필드 이름이 빠졌다 — 「{described}」";
            if (!described.Contains("문이 열리지 않는다")) return $"원인이 빠졌다 — 「{described}」";
            return null;
        }

        private static string TestDescribeWithoutConsequence()
        {
            var defects = new List<WiringDefect>();
            SceneWiringValidator.CollectDefects(new FakeBareTarget(), "씬/무명", defects);

            if (defects.Count != 1) return $"결함 {defects.Count}, 기대 1";
            if (defects[0].Consequence.Length != 0)
                return $"원인이 없는데 「{defects[0].Consequence}」 가 들어갔다";

            string described = defects[0].Describe();
            if (!described.Contains("씬/무명") || !described.Contains("Bare"))
                return $"경로나 필드가 빠졌다 — 「{described}」";
            if (described.Contains("—"))
                return $"원인이 없는데 구분선이 붙었다 — 「{described}」";
            return null;
        }

        private static string TestNullInstanceIsSafe()
        {
            var defects = new List<WiringDefect>();
            if (SceneWiringValidator.CollectDefects(null, "씬/없음", defects) != 0)
                return "null 인스턴스에서 필드를 셌다";
            if (defects.Count != 0) return "null 인스턴스가 결함을 남겼다";
            if (SceneWiringValidator.CollectDefects(new FakeTarget(), "씬/가짜", null) != 0)
                return "담을 곳이 null 인데 0 을 돌려주지 않았다";
            return null;
        }

        // ── 요구의 성격: 자동으로, 런타임에 ────────────────────────────────────

        /// <summary>
        /// 이 항목의 이전 판본이 실패한 지점이 정확히 여기다 — `#if UNITY_EDITOR` 안의
        /// `MenuItem` 은 「개발 빌드에서 즉시」를 만족하지 않는다. 진입점이 사라지면
        /// 나머지 단정이 전부 통과해도 요구는 미충족이므로 따로 본다.
        /// </summary>
        private static string TestAutoEntryPointExists()
        {
            MethodInfo[] methods = typeof(SceneWiringValidator).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            for (int i = 0; i < methods.Length; i++)
            {
                var attribute = (RuntimeInitializeOnLoadMethodAttribute)Attribute.GetCustomAttribute(
                    methods[i], typeof(RuntimeInitializeOnLoadMethodAttribute));
                if (attribute == null) continue;

                if (attribute.loadType != RuntimeInitializeLoadType.AfterSceneLoad)
                    return $"{methods[i].Name} 의 시점이 {attribute.loadType} 다 — " +
                           "씬 오브젝트가 만들어지기 전에 훑으면 아무것도 못 본다";
                if (methods[i].GetParameters().Length != 0)
                    return $"{methods[i].Name} 가 인자를 받는다 — Unity 가 부르지 못한다";
                return null;
            }

            return "RuntimeInitializeOnLoadMethod 진입점이 없다 — " +
                   "메뉴로만 도는 검사기는 개발 빌드에서 아무것도 하지 않는다 (PRD §13.5)";
        }

        /// <summary>
        /// `UP-TEST-11` 웨이브 1이 `PlayerSetupValidator` 를 지울 때 검사가 손실되지 않도록
        /// 세워 둔 말뚝이다. 그 파일이 보던 셋에 표시가 남아 있는지만 본다 —
        /// 어느 필드인지는 이 단정이 정하지 않는다(대체 경로가 있는 필드는 표시하지 않는 것이 규칙이다).
        /// </summary>
        private static string TestPlayerComponentsStayMarked()
        {
            Type[] watched =
            {
                typeof(FirstPersonController),
                typeof(CrosshairInteractor),
                typeof(CrosshairView),
            };

            for (int i = 0; i < watched.Length; i++)
            {
                int count = SceneWiringValidator.RequiredFieldCountOf(watched[i]);
                if (count == 0)
                    return $"{watched[i].Name} 에 [RequiredReference] 가 하나도 없다 — " +
                           "이 컴포넌트는 검사기에게 보이지 않는다";
            }

            return null;
        }

        private static string Join(List<WiringDefect> defects)
        {
            return DescribeDefectsCore(defects);
        }

        // ── 디버그 패널의 릴리스 가드 (`UP-TEST-06` · N08 §17) ────────────────
        //
        // 요구는 「**개발 빌드에서만** 기본 활성화」다. 한때 이 파일에는
        // `UNITY_EDITOR`·`DEVELOPMENT_BUILD`·`Debug.isDebugBuild` 가 하나도 없어서
        // 릴리스에서 F1 이 시드 재시작(R)·시드 입력(T)·스핀 로그(L)까지 열었다.
        // 지금은 `DebugToolsAllowed` 가 있고 두 진입점이 그것을 본다.
        //
        // **릴리스 동작 자체는 에디터에서 재현할 수 없다** — `Debug.isDebugBuild` 는
        // 에디터에서 항상 참이다. 그래서 검사할 수 있는 것은 두 가지뿐이다:
        // ① 에디터에서 허용이 참이라 하네스가 돈다 ② 가드가 두 진입점에 실제로 걸려 있다.
        // ②를 원본 텍스트로 보는 이유는 그것이 **제거되면 조용히 통과할 수 있는**
        // 유일한 결함이기 때문이다 — 지우면 에디터 동작은 하나도 안 바뀐다.

        private static string TestDebugToolsAllowedInEditor()
        {
            if (!Ascend.Prototype.UI.DebugPanelView.DebugToolsAllowed)
                return "에디터에서 디버그 도구가 막혔다 — HeroSliceAutoPilot 등 하네스가 이 타입에 의존한다";
            return null;
        }

        private static string TestDebugPanelGuardsBothEntryPoints()
        {
            const string relative = "Assets/Prototype_Elevator/Scripts/UI/DebugPanelView.cs";
            var root = System.IO.Directory.GetParent(Application.dataPath);
            string path = root == null ? relative
                : System.IO.Path.Combine(root.FullName,
                    relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(path)) return $"{relative} 가 없다";

            string src = System.IO.File.ReadAllText(path);
            if (!src.Contains("Debug.isDebugBuild"))
                return "DebugToolsAllowed 가 Debug.isDebugBuild 에서 오지 않는다 — 상수로 굳으면 릴리스에서 열린다";

            string start = BodyOf(src, "private void Start()");
            if (start == null) return "Start() 를 못 찾았다";
            if (!start.Contains("DebugToolsAllowed"))
                return "Start() 가 가드를 보지 않는다 — 인스펙터에서 켜 둔 채 빌드하면 릴리스에서 처음부터 떠 있다";

            string update = BodyOf(src, "private void Update()");
            if (update == null) return "Update() 를 못 찾았다";
            if (!update.Contains("DebugToolsAllowed"))
                return "Update() 가 가드를 보지 않는다 — 릴리스에서 F1·R·T·L 이 살아 있다";
            return null;
        }

        // ── 종이 테이프가 기록을 쓰는가 (`UP-REC-03`) ─────────────────────────
        //
        // 요구는 「인게임 출력과 디버그가 **같은 데이터**를 쓴다」다. 위험은 두 표시가
        // 각자 값을 **다시 계산**하는 것이고, 그러면 화면과 콘솔이 조용히 갈린다.
        //
        // 저장소에 이 요구를 검사하는 단정이 **0건**이었다. `UP-REC-04` 가 증거로 삼은
        // 「인쇄된 줄 2」는 `_printed` 카운터인데 그 증가는 `Redraw()` **앞**에서 일어나므로
        // 그릴 대상이 없어도 오른다 — 렌더링 요구를 카운터로 잰 것이다.
        //
        // `FeedRecord` 가 `private` 이라 반사로 부른다. **이 경우엔 정당하다** — 공개 API 로
        // 부르려면 씬에 기록기를 배선하고 `Update` 를 돌려야 하는데, 그 배선이 바로 지금
        // 비어 있는 것이라(`docs/runtime/DEAD_SCENE_WIRING.md`) 검사가 성립하지 않는다.
        // 테스트를 위해 공개 API 를 늘리는 것보다 반사가 낫다고 판단했다.

        private static string TestPrinterUsesRecordValues()
        {
            Ascend.Prototype.Run.FloorRecord record = FirstRecord();
            if (record == null) return "런에서 층 기록을 하나도 못 만들었다 — 선행 조건 실패";

            var go = new GameObject("PrinterProbe");
            try
            {
                var printer = go.AddComponent<Ascend.Prototype.View.PaperTapePrinterView>();
                MethodInfo feed = typeof(Ascend.Prototype.View.PaperTapePrinterView)
                    .GetMethod("FeedRecord", BindingFlags.Instance | BindingFlags.NonPublic);
                if (feed == null) return "FeedRecord 를 못 찾았다 — 이름이 바뀌었나";
                feed.Invoke(printer, new object[] { record });

                string tape = PendingText(printer);
                if (tape == null) return "_pending 을 못 읽었다";
                if (tape.Length == 0) return "기록을 넣었는데 줄이 하나도 안 쌓였다";

                // **기록의 바로 그 값**이 나와야 한다. 재계산하면 어긋난다.
                string floor = record.Floor.ToString("D2");
                if (!tape.Contains(floor + "층")) return $"층 번호 {floor} 가 테이프에 없다: {tape}";

                string required = record.RequiredPower.ToString("F0");
                string final = record.FinalPower.ToString("F0");
                if (!tape.Contains(required)) return $"요구 전력 {required} 이 없다 — 재계산했나: {tape}";
                if (!tape.Contains(final)) return $"최종 전력 {final} 이 없다: {tape}";

                string weight = record.CarriedWeight.ToString("F0");
                string capacity = record.WeightCapacity.ToString("F0");
                if (!tape.Contains(weight + "/" + capacity))
                    return $"무게 {weight}/{capacity} 가 없다 — 기록이 아니라 다른 출처를 봤나: {tape}";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// **카운터는 그렸다는 증거가 아니다.** `_printed` 는 `Redraw()` 앞에서 오르므로
        /// 그릴 대상(`_tapeText`)이 없어도 증가한다 — 씬이 지금 정확히 그 상태다.
        /// 이 검사는 그 사실을 고정한다: 다음 사람이 카운터를 다시 증거로 삼지 않도록.
        /// </summary>
        private static string TestPrinterCounterIsNotProofOfDrawing()
        {
            Ascend.Prototype.Run.FloorRecord record = FirstRecord();
            if (record == null) return "런에서 층 기록을 하나도 못 만들었다 — 선행 조건 실패";

            var go = new GameObject("PrinterProbe2");
            try
            {
                var printer = go.AddComponent<Ascend.Prototype.View.PaperTapePrinterView>();
                MethodInfo feed = typeof(Ascend.Prototype.View.PaperTapePrinterView)
                    .GetMethod("FeedRecord", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo redraw = typeof(Ascend.Prototype.View.PaperTapePrinterView)
                    .GetMethod("Redraw", BindingFlags.Instance | BindingFlags.NonPublic);
                if (feed == null || redraw == null) return "FeedRecord/Redraw 를 못 찾았다";

                feed.Invoke(printer, new object[] { record });
                if (printer.PendingLines == 0) return "줄이 안 쌓였다";

                // `_tapeText` 가 없는 채로 그려도 **터지지 않고 조용히 넘어간다.**
                // 그게 씬의 현재 상태이고, 그래서 카운터만으로는 아무것도 증명되지 않는다.
                redraw.Invoke(printer, null);
                if (printer.PrintedLines.Count != 0)
                    return "그릴 대상이 없는데 인쇄된 줄이 생겼다 — 검사가 상황을 재현하지 못했다";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>10층 런을 돌려 첫 층 기록을 만든다. `RunSummaryBuilderTests` 와 같은 방식이다.</summary>
        private static Ascend.Prototype.Run.FloorRecord FirstRecord()
        {
            var run = new Ascend.Prototype.Run.RunSession(1337, 0f, 0f);
            for (int guard = 0; guard < 40; guard++)
            {
                Ascend.Prototype.Run.FloorSession floor = run.Current;
                if (floor == null) return null;
                if (floor.Phase == Ascend.Prototype.Run.FloorPhase.Boarding) run.FinishBoarding();
                else if (floor.Phase == Ascend.Prototype.Run.FloorPhase.ContractSelection) run.SelectContract(0);
                else if (floor.Phase == Ascend.Prototype.Run.FloorPhase.Spinning && floor.SpinsRemaining > 0) run.Spin();
                else break;
            }

            Ascend.Prototype.Run.FloorSession current = run.Current;
            if (current == null) return null;
            Ascend.Prototype.Run.FloorResult result =
                current.CanBank ? run.Bank() : current.SpinsRemaining == 0 ? run.ForceResolve() : null;
            if (result == null) return null;

            return Ascend.Prototype.Run.FloorRecord.Capture(
                run.Seed, current, result,
                result.Succeeded ? Ascend.Prototype.Risk.RiskLevel.Strain : Ascend.Prototype.Risk.RiskLevel.Collapse,
                result.Succeeded ? "잔류 저항" : "층 실패", run.LastJettison);
        }

        /// <summary>큐에 쌓인 줄을 하나로 잇는다. `_pending` 은 private 이라 반사로 읽는다.</summary>
        private static string PendingText(Ascend.Prototype.View.PaperTapePrinterView printer)
        {
            FieldInfo f = typeof(Ascend.Prototype.View.PaperTapePrinterView)
                .GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return null;
            var queue = f.GetValue(printer) as System.Collections.Generic.Queue<string>;
            if (queue == null) return null;
            return string.Join("\n", queue.ToArray());
        }

        /// <summary>중괄호 균형으로 메서드 본문을 잘라낸다. 못 찾으면 null.</summary>
        private static string BodyOf(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;
            int open = source.IndexOf('{', at);
            if (open < 0) return null;

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open, i - open + 1);
                }
            }
            return null;
        }

        private static string DescribeDefectsCore(IReadOnlyList<WiringDefect> defects)
        {
            if (defects == null || defects.Count == 0) return "(결함 없음)";
            var sb = new StringBuilder();
            for (int i = 0; i < defects.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(defects[i].Describe());
            }
            return sb.ToString();
        }
    }
}
