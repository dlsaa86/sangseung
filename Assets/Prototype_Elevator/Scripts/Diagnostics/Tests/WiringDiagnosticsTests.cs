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
