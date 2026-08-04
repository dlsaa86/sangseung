using System;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.Player.Tests
{
    /// <summary>
    /// 유지 입력 (`MASTER_PRD.md` §7 「0.7~1.0초 유지 입력」).
    ///
    /// ## 무엇을 반증하려는가
    ///
    /// §7 의 요점은 오입력 방지가 아니라 **몸짓**이다 —
    /// 「한 번 당기는 것으로는 실행되지 않는다. 그 유지 시간이 자발적 선택의 몸짓이다.」
    /// 그러므로 검사해야 하는 것은 「유지 시간이 있는가」가 아니라 —
    ///
    /// | 검사 | 깨지면 무엇이 무너지나 |
    /// |---|---|
    /// | 기본값이 §7 대역 안 | 0.2초면 클릭과 구별되지 않고 2초면 조작이 굼떠진다 |
    /// | 진행도가 손잡이 각도가 된다 | 「얼마나 더 눌러야 하는가」를 화면에서 못 읽는다 |
    /// | 취소가 각도를 되돌린다 | 반쯤 내려간 채 얼어붙어 「이미 걸었다」로 읽힌다 |
    /// | 잠기면 진행도가 버려진다 | 잠긴 레버가 진행도를 들고 있다가 다음 해제에 이어진다 |
    ///
    /// ## 왜 `Update` 를 부르지 않는가
    ///
    /// `Update` 는 `Time.deltaTime` 과 마우스 입력에 달려 있어 EditMode 에서 의미가 없다.
    /// 대신 <see cref="IHoldInteractable"/> **계약의 양쪽 끝**을 직접 부른다 —
    /// 조작자가 부를 메서드를 부르고, 상호작용물이 노출하는 상태를 읽는다.
    /// 그 사이의 시간 누적은 조작자의 산수이지 이 물체의 규칙이 아니다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class HoldInputTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();

            Run("과수확 레버가 유지 입력 계약을 구현한다", TestImplementsContract, ref passed, ref failed, report);
            Run("기본 유지 시간이 PRD §7 대역(0.7~1.0) 안이다", TestDefaultInRange, ref passed, ref failed, report);
            Run("진행도가 그대로 손잡이 각도가 된다", TestProgressDrivesHandle, ref passed, ref failed, report);
            Run("취소가 진행도를 버린다", TestCancelDropsProgress, ref passed, ref failed, report);
            Run("진행도가 0~1 밖으로 나가지 않는다", TestProgressClamped, ref passed, ref failed, report);
            Run("유지가 걸린 레버는 프롬프트가 다르다", TestPromptSaysHold, ref passed, ref failed, report);
            Run("유지 시간을 0 으로 두면 즉시 실행 경로다", TestZeroHoldIsInstant, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Hold Input Tests ===\n");
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
            catch (Exception e)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외: {e.Message}");
            }
        }

        /// <summary>
        /// 검사용 레버 하나. 손잡이 피벗을 붙여야 각도를 관측할 수 있다 —
        /// 붙이지 않으면 `ApplyHandle` 이 조용히 반환해 **모든 각도 검사가 통과한다.**
        /// `D-20260803-08` 이 정확히 그 부류(조건부 코드를 조건 없이 시험)를 다뤘다.
        /// </summary>
        private static InteractableOverharvestLever NewLever(out GameObject root, out Transform handle)
        {
            root = new GameObject("HoldTestLever");
            var lever = root.AddComponent<InteractableOverharvestLever>();
            handle = new GameObject("HandlePivot").transform;
            handle.SetParent(root.transform, false);
            SetPrivate(lever, "_handlePivot", handle);
            return lever;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f == null) throw new Exception($"필드 {field} 가 없다 — 이름이 바뀌었으면 검사도 함께 고쳐야 한다");
            f.SetValue(target, value);
        }

        private static float HandleAngle(Transform handle)
        {
            float x = handle.localRotation.eulerAngles.x;
            return x > 180f ? x - 360f : x;
        }

        private static string TestImplementsContract()
        {
            var lever = NewLever(out GameObject root, out _);
            try
            {
                if (!(lever is IHoldInteractable))
                    return "과수확 레버가 IHoldInteractable 을 구현하지 않는다 — §7 의 몸짓이 없다";
                if (!(lever is IInteractable))
                    return "IInteractable 계약이 깨졌다 — 조작자가 조준하지 못한다";
                return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string TestDefaultInRange()
        {
            var lever = NewLever(out GameObject root, out _);
            try
            {
                float s = lever.HoldSeconds;
                // §7 — 「약 0.7~1.0초 유지 입력」. 이 대역을 코드에 고정한다.
                // 누가 0.2 로 내리면 클릭과 구별되지 않고, 2.0 으로 올리면 조작이 굼떠진다.
                if (s < 0.7f || s > 1.0f)
                    return $"기본 유지 시간 {s:F2}초 — PRD §7 은 0.7~1.0 을 요구한다";
                return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string TestProgressDrivesHandle()
        {
            var lever = NewLever(out GameObject root, out Transform handle);
            try
            {
                lever.OnHoldProgress(0f);
                float atZero = HandleAngle(handle);
                lever.OnHoldProgress(1f);
                float atOne = HandleAngle(handle);

                if (Mathf.Abs(atOne - atZero) < 1f)
                    return $"진행도 0 과 1 의 손잡이 각도가 같다 ({atZero:F1} vs {atOne:F1}) — " +
                           "진행도가 화면에 나타나지 않으면 「얼마나 더」를 읽을 수 없다";

                lever.OnHoldProgress(0.5f);
                float mid = HandleAngle(handle);
                bool between = (mid - atZero) * (atOne - mid) > 0f;
                if (!between)
                    return $"중간 진행도의 각도 {mid:F1} 이 양끝({atZero:F1}, {atOne:F1}) 사이가 아니다";
                return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string TestCancelDropsProgress()
        {
            var lever = NewLever(out GameObject root, out _);
            try
            {
                lever.OnHoldProgress(0.8f);
                if (lever.HoldProgress < 0.79f) return "진행도가 반영되지 않았다";
                lever.OnHoldCancelled();
                if (lever.HoldProgress != 0f)
                    return $"취소 후 진행도가 {lever.HoldProgress:F2} — 0 이어야 한다";
                return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string TestProgressClamped()
        {
            var lever = NewLever(out GameObject root, out _);
            try
            {
                lever.OnHoldProgress(-3f);
                if (lever.HoldProgress != 0f) return $"음수 진행도가 {lever.HoldProgress} 로 남았다";
                lever.OnHoldProgress(7f);
                if (lever.HoldProgress != 1f) return $"1 초과 진행도가 {lever.HoldProgress} 로 남았다";
                return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string TestPromptSaysHold()
        {
            var lever = NewLever(out GameObject root, out _);
            try
            {
                SetPrivate(lever, "_unlocked", true);
                string prompt = lever.Prompt;
                if (string.IsNullOrEmpty(prompt)) return "프롬프트가 비었다";
                // 「길게」가 없으면 플레이어는 클릭으로 알아듣고 한 번 누르고 만다.
                if (!prompt.Contains("길게"))
                    return $"유지가 걸렸는데 프롬프트가 「{prompt}」다 — 유지해야 한다는 것을 말하지 않는다";
                return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string TestZeroHoldIsInstant()
        {
            var lever = NewLever(out GameObject root, out _);
            try
            {
                SetPrivate(lever, "_holdSeconds", 0f);
                SetPrivate(lever, "_unlocked", true);
                if (lever.HoldSeconds > 0f) return "0 으로 설정했는데 HoldSeconds 가 양수다";
                // 0 이면 조작자가 유지 분기를 타지 않는다(즉시 실행). 프롬프트도 되돌아가야
                // 「길게 누르라」는 거짓 안내가 남지 않는다.
                if (lever.Prompt.Contains("길게"))
                    return "유지 시간이 0 인데 프롬프트가 여전히 「길게」라고 말한다";
                return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
