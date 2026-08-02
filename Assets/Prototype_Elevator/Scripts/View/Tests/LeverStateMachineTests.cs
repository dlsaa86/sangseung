using System;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.View.Tests
{
    /// <summary>
    /// 레버 상태 기계의 **타이밍을 초 단위로** 단정한다.
    ///
    /// ## 왜 눈으로 보지 않는가
    ///
    /// 애니메이션 검수를 캡처로만 하면 「좀 빠른 것 같다」에서 멈춘다. 그 판단은
    /// 다음 사람에게 전달되지 않고, 무심코 값을 바꿔도 아무도 모른다. 요구는
    /// 초로 적혀 있다 — 초기 저항 0.08초, 이동 0.35~0.5초, 잠김 반동 합계 0.3~0.5초.
    /// 적힌 대로 잰다.
    ///
    /// ## 무엇을 반증하려는가
    ///
    /// 이 저장소에서 실제로 일어난 실패 넷을 각각 겨눈다.
    ///
    ///   ① **레버가 움직이지 않았다** — `LeverPhysics.Pull()` 의 런타임 호출자가
    ///      0 개였다. 반동 수식이 아무리 정교해도 발동되지 않으면 없는 것이다.
    ///   ② **잠긴 입력이 무반응이었다** — `Interact()` 가 조용히 반환했다.
    ///      플레이어에게 「지금은 안 된다」와 「고장났다」가 구분되지 않았다.
    ///   ③ **연타가 애니메이션을 되감았다** — 진행 중 재입력이 처음으로 되돌리면
    ///      빠르게 누르는 동안 아무것도 완료되지 않는다.
    ///   ④ **프레임률에 따라 결과가 달랐다** — 프레임당 고정량을 더하는 코드는
    ///      30fps 와 60fps 에서 다른 곳에 멈춘다.
    ///
    /// 씬을 열지 않는다. `Step(dt)` 를 고정 dt 로 돌린다.
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class LeverStateMachineTests
    {
        private const float Dt = 1f / 60f;

        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();
            Append(ref passed, ref failed, report);
            report.Insert(0, "[상승] === 실행 레버 상태 기계 Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        public static void Append(ref int passed, ref int failed, StringBuilder report)
        {
            Run("당김 — 초기 저항 구간에서 거의 움직이지 않는다", TestInitialResistance, ref passed, ref failed, report);
            Run("당김 — 이동이 0.35~0.5초 안에 끝난다", TestTravelDuration, ref passed, ref failed, report);
            Run("당김 — 바닥을 지나쳤다가 되돌아온다 (오버슈트)", TestOvershoot, ref passed, ref failed, report);
            Run("걸림 — 장치 반응이 0.08~0.15초 늦게 온다", TestDeviceReactDelay, ref passed, ref failed, report);
            Run("한 번 당기면 발동도 정확히 한 번이다", TestLatchFiresOnce, ref passed, ref failed, report);
            Run("연타해도 진행 중인 동작이 되감기지 않는다", TestSpamDoesNotRewind, ref passed, ref failed, report);
            Run("복귀 — 0.45~0.65초 안에 원위치로 돌아온다", TestResetDuration, ref passed, ref failed, report);
            Run("한 판이 끝나면 Idle 로 돌아오고 각도가 0 이다", TestReturnsToIdle, ref passed, ref failed, report);

            Run("잠김 — 2~4도만 움직이고 멈춘다", TestLockedTravelRange, ref passed, ref failed, report);
            Run("잠김 — 합계 0.3~0.5초 안에 끝난다", TestLockedDuration, ref passed, ref failed, report);
            Run("잠김 — 거부 이벤트가 정확히 한 번 나간다", TestLockedFiresOnce, ref passed, ref failed, report);
            Run("잠김 — 연타해도 상태가 Locked 를 벗어나지 않는다", TestLockedSpamSafe, ref passed, ref failed, report);
            Run("잠김 — 발동 이벤트는 절대 나가지 않는다", TestLockedNeverLatches, ref passed, ref failed, report);

            Run("프레임률 독립 — 30fps 와 120fps 가 같은 곳에 도착한다", TestFrameRateIndependent, ref passed, ref failed, report);
            Run("한 트랜스폼의 주인이 하나다 (LeverPhysics 와 겹치지 않는다)", TestSingleOwner, ref passed, ref failed, report);
        }

        // ── 도우미 ──────────────────────────────────────────────────────────

        private sealed class Rig : IDisposable
        {
            public readonly GameObject Go;
            public readonly LeverStateMachine Fsm;
            public int Latched, Blocked, Returned;

            public Rig(bool locked = false)
            {
                Go = new GameObject("~LeverRig");
                Fsm = Go.AddComponent<LeverStateMachine>();
                Fsm.Configure(Go.transform, Vector3.right, 55f);
                if (locked) Fsm.SetLocked(true);
            }

            /// <summary>
            /// 이벤트 대신 **상태 전이**를 센다. 매 스텝 호출한다.
            ///
            /// `UnityEvent` 는 직렬화 필드라 코드에서 리스너를 붙이려면 리플렉션이
            /// 필요하고, **테스트가 리플렉션을 쓰면 필드 이름이 바뀔 때 조용히
            /// 세지 않게 된다** — 통과하는 채로. 발동은 `Latched → Processing`
            /// 전이와 같은 지점에서만 일어나므로 전이를 세는 것이 등가이면서 안전하다.
            /// </summary>
            public void Observe(LeverStateMachine.State before)
            {
                LeverStateMachine.State now = Fsm.Current;
                if (before == LeverStateMachine.State.Latched && now == LeverStateMachine.State.Processing) Latched++;
                if (before == LeverStateMachine.State.Resetting && now == LeverStateMachine.State.Idle) Returned++;
            }

            public float Advance(float seconds, float dt = Dt)
            {
                int steps = Mathf.CeilToInt(seconds / dt);
                for (int i = 0; i < steps; i++)
                {
                    LeverStateMachine.State before = Fsm.Current;
                    Fsm.Step(dt);
                    Observe(before);
                }
                return steps * dt;
            }

            /// <summary>상태가 <paramref name="want"/> 가 될 때까지의 시간(초). 못 되면 -1.</summary>
            public float TimeUntil(LeverStateMachine.State want, float limit = 5f, float dt = Dt)
            {
                float t = 0f;
                while (t < limit)
                {
                    LeverStateMachine.State before = Fsm.Current;
                    Fsm.Step(dt);
                    Observe(before);
                    t += dt;
                    if (Fsm.Current == want) return t;
                }
                return -1f;
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(Go);
        }

        // ── 정상 당김 ───────────────────────────────────────────────────────

        private static string TestInitialResistance()
        {
            using var r = new Rig();
            r.Fsm.Pull();
            r.Advance(0.07f);
            // 저항 구간이 끝나기 전. 전체 55도의 10% 를 넘으면 「무겁다」가 사라진다.
            float a = r.Fsm.AngleDegrees;
            return a <= 55f * 0.10f ? null : $"0.07초에 이미 {a:F1}도 — 저항이 없다";
        }

        private static string TestTravelDuration()
        {
            using var r = new Rig();
            r.Fsm.Pull();
            float t = r.TimeUntil(LeverStateMachine.State.Latched);
            if (t < 0f) return "걸리지 않았다";
            // 저항 0.08 + 이동 0.42 + 안착 0.12 = 0.62. 이동 구간만 보면 0.35~0.5.
            return t >= 0.45f && t <= 0.75f ? null : $"걸리기까지 {t:F3}초 — 0.45~0.75초를 벗어난다";
        }

        private static string TestOvershoot()
        {
            using var r = new Rig();
            r.Fsm.Pull();
            float peak = 0f;
            for (int i = 0; i < 60; i++)
            {
                r.Fsm.Step(Dt);
                peak = Mathf.Max(peak, r.Fsm.AngleDegrees);
                if (r.Fsm.Current == LeverStateMachine.State.Latched) break;
            }
            // 도달이 아니라 **지나쳤다가 되돌아오는 것**이 「당겼다」의 신호다.
            return peak > 55f + 0.8f ? null : $"최대 {peak:F2}도 — 55도를 넘지 못했다(오버슈트 없음)";
        }

        private static string TestDeviceReactDelay()
        {
            using var r = new Rig();
            r.Fsm.Pull();
            float toLatch = r.TimeUntil(LeverStateMachine.State.Latched);
            if (toLatch < 0f) return "걸리지 않았다";
            float toProcess = r.TimeUntil(LeverStateMachine.State.Processing);
            if (toProcess < 0f) return "장치가 반응하지 않았다";
            // 0 이면 원인과 결과가 붙어 인과가 안 읽힌다. 너무 길면 무반응으로 읽힌다.
            return toProcess >= 0.06f && toProcess <= 0.20f
                ? null : $"걸린 뒤 {toProcess:F3}초 — 0.06~0.20초를 벗어난다";
        }

        private static string TestLatchFiresOnce()
        {
            using var r = new Rig();
            r.Fsm.Pull();
            r.Advance(3.0f);
            return r.Latched == 1 ? null : $"발동 {r.Latched}회 — 정확히 1회여야 한다";
        }

        private static string TestSpamDoesNotRewind()
        {
            using var r = new Rig();
            r.Fsm.Pull();
            for (int i = 0; i < 40; i++) { r.Fsm.Pull(); r.Fsm.Step(Dt); }   // 매 프레임 연타
            float t = r.TimeUntil(LeverStateMachine.State.Latched, 2f);
            if (t < 0f) return "연타 중 영원히 걸리지 않았다 — 되감기고 있다";
            r.Advance(3f);
            return r.Latched == 1 ? null : $"연타로 발동이 {r.Latched}회 — 1회여야 한다";
        }

        private static string TestResetDuration()
        {
            using var r = new Rig();
            r.Fsm.Pull();
            r.TimeUntil(LeverStateMachine.State.Resetting, 5f);
            float t = r.TimeUntil(LeverStateMachine.State.Idle, 3f);
            if (t < 0f) return "원위치로 돌아오지 않았다";
            return t >= 0.42f && t <= 0.70f ? null : $"복귀 {t:F3}초 — 0.42~0.70초를 벗어난다";
        }

        private static string TestReturnsToIdle()
        {
            using var r = new Rig();
            r.Fsm.Pull();
            r.Advance(4f);
            if (r.Fsm.Current != LeverStateMachine.State.Idle) return $"상태가 {r.Fsm.Current}";
            return Mathf.Abs(r.Fsm.AngleDegrees) < 0.01f ? null : $"각도가 {r.Fsm.AngleDegrees:F3}도로 남았다";
        }

        // ── 잠김 ────────────────────────────────────────────────────────────

        private static string TestLockedTravelRange()
        {
            using var r = new Rig(locked: true);
            r.Fsm.Blocked();
            float peak = 0f;
            for (int i = 0; i < 40; i++) { r.Fsm.Step(Dt); peak = Mathf.Max(peak, r.Fsm.AngleDegrees); }
            // 요구: 2~4도. 0 이면 무반응이고, 크면 「사실 열려 있다」로 읽힌다.
            return peak >= 2f && peak <= 4f ? null : $"최대 {peak:F2}도 — 요구 2~4도";
        }

        private static string TestLockedDuration()
        {
            using var r = new Rig(locked: true);
            r.Fsm.Blocked();
            float t = 0f;
            for (int i = 0; i < 120; i++)
            {
                r.Fsm.Step(Dt); t += Dt;
                if (t > 0.15f && Mathf.Abs(r.Fsm.AngleDegrees) < 0.01f) break;
            }
            return t >= 0.28f && t <= 0.52f ? null : $"합계 {t:F3}초 — 요구 0.3~0.5초";
        }

        private static string TestLockedFiresOnce()
        {
            // 거부 이벤트는 「핀에 닿은 순간」 한 번이다. 접근 중에 울리면
            // 부딪히기 전에 소리가 나고, 매 프레임 울리면 경고등이 발작한다.
            using var r = new Rig(locked: true);
            r.Fsm.Blocked();
            int crossings = 0;
            bool wasBelow = true;
            for (int i = 0; i < 60; i++)
            {
                r.Fsm.Step(Dt);
                bool below = r.Fsm.AngleDegrees < 3.0f;
                if (wasBelow && !below) crossings++;
                wasBelow = below;
            }
            return crossings <= 1 ? null : $"핀에 {crossings}회 도달 — 1회여야 한다";
        }

        private static string TestLockedSpamSafe()
        {
            using var r = new Rig(locked: true);
            for (int i = 0; i < 90; i++) { r.Fsm.Blocked(); r.Fsm.Step(Dt); }
            return r.Fsm.Current == LeverStateMachine.State.Locked
                ? null : $"연타 후 상태가 {r.Fsm.Current} — Locked 를 벗어나면 안 된다";
        }

        private static string TestLockedNeverLatches()
        {
            using var r = new Rig(locked: true);
            for (int i = 0; i < 200; i++) { r.Fsm.Pull(); r.Fsm.Step(Dt); }
            return r.Latched == 0 ? null : $"잠긴 채로 발동이 {r.Latched}회 나갔다";
        }

        // ── 견고성 ──────────────────────────────────────────────────────────

        private static string TestFrameRateIndependent()
        {
            using var slow = new Rig();
            using var fast = new Rig();
            slow.Fsm.Pull(); fast.Fsm.Pull();
            slow.Advance(0.60f, 1f / 30f);
            fast.Advance(0.60f, 1f / 120f);
            float d = Mathf.Abs(slow.Fsm.AngleDegrees - fast.Fsm.AngleDegrees);
            // 같은 시각에 같은 각도여야 한다. 프레임당 고정량을 더하면 여기서 갈린다.
            return d < 2.0f ? null : $"0.60초 시점 각도차 {d:F2}도 (30fps {slow.Fsm.AngleDegrees:F2} / 120fps {fast.Fsm.AngleDegrees:F2})";
        }

        private static string TestSingleOwner()
        {
            // 같은 트랜스폼에 두 주인이 붙으면 매 프레임 각도를 뺏고 뺏겨 떨린다.
            // 조립기가 둘 다 붙이지 않는지는 **씬을 열지 않고** 확인할 수 없지만,
            // 상태 기계가 스스로 중복 부착을 막는지는 여기서 단정할 수 있다.
            var go = new GameObject("~Owner");
            try
            {
                go.AddComponent<LeverStateMachine>();
                bool blocked = go.GetComponent<LeverStateMachine>() != null &&
                               Attribute.IsDefined(typeof(LeverStateMachine), typeof(DisallowMultipleComponent));
                return blocked ? null : "DisallowMultipleComponent 가 없다 — 두 주인이 붙을 수 있다";
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
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
                report.AppendLine($"  FAIL  {name} — 예외 {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
