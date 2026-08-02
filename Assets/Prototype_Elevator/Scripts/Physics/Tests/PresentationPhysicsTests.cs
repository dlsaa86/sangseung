using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.Physics.Tests
{
    /// <summary>
    /// 표현 계층 물리의 헤드리스 검증. 씬도 PlayMode 도 필요 없다.
    ///
    /// 무엇을 묻는가 — 넷이고, 넷 다 「나중에 조용히 깨지는」 종류다.
    ///
    /// 1. <b>발산하지 않는다.</b> 적분기가 에너지를 만들면 몇 분 뒤 램프가 천장을
    ///    뚫는다. 개발 중에는 절대 안 잡히고 캡처에서 처음 보인다.
    /// 2. <b>결정론.</b> 같은 입력 시퀀스가 <b>부동소수 비트 단위로</b> 같은 상태를
    ///    낸다. 이게 깨지면 `Captures/baseline.txt` 비교가 통째로 무의미해진다.
    /// 3. <b>할당 0.</b> `UP-TECH-04` 가 VERIFIED 인 「매 프레임 0 B」를 이 계층이
    ///    깨뜨리기 가장 쉽다. 물리 콜백이 없어도 박싱·클로저 하나면 끝난다.
    /// 4. <b>진폭 상한과 복귀.</b> 판독성(`VISUAL_SPEC` §8)과
    ///    `CollapseSequence` 의 「복귀 오차 0.00000」이 여기 걸린다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조
    /// (asmdef 이 없어 테스트 어셈블리가 `Assembly-CSharp` 를 볼 수 없다).
    ///
    /// <b>등록하지 않으면 통과가 아니라 미검증이다.</b> 이 스위트는
    /// `Assets/Editor/AscendTestMenu.AllSuites()` 와 `PrototypeSelfTest.RunAllToString()`
    /// 두 곳에 등록되어야 한다 — 그 두 파일은 이 작업의 소유 경로 밖이라
    /// 통합자가 넣는다. 인수인계 문서에 정확한 줄이 적혀 있다.
    /// </summary>
    public static class PresentationPhysicsTests
    {
        private const float Step = PhysicsStepper.DefaultStep;

        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            // ── 안정성 ──────────────────────────────────────────────────────────
            Run("스프링이 에너지를 잃고 정확히 0 으로 멈춘다", TestSpringSettles, ref passed, ref failed, report);
            Run("스프링이 극단 임펄스에서도 발산하지 않는다", TestSpringExtremeImpulse, ref passed, ref failed, report);
            Run("스프링이 최저 감쇠·최고 강성 조합에서도 유계다", TestSpringWorstCaseBounded, ref passed, ref failed, report);
            Run("상한이 움직이는 목표를 따라다니지 않는다", TestClampIsAbsolute, ref passed, ref failed, report);
            Run("상한 기준점을 옮기면 창도 함께 옮겨간다", TestClampCenterMoves, ref passed, ref failed, report);
            Run("진자가 에너지를 잃고 정확히 0 으로 멈춘다", TestPendulumSettles, ref passed, ref failed, report);
            Run("진자가 극단 가속(±1000)에서도 상한 안에 있다", TestPendulumExtremeDrive, ref passed, ref failed, report);
            Run("진자가 자유낙하 입력에서도 뒤집히지 않는다", TestPendulumFreeFall, ref passed, ref failed, report);
            Run("누산기가 거대한 dt 에서 스텝 수를 자른다", TestStepperClampsBurst, ref passed, ref failed, report);
            Run("누산기가 남은 시간을 이월해 슬로모션이 되지 않는다", TestStepperCarriesRemainder, ref passed, ref failed, report);

            // ── 결정론 ──────────────────────────────────────────────────────────
            Run("고정 스텝 시계가 스텝 수에 비례한 시각을 낸다", TestFixedClockTime, ref passed, ref failed, report);
            Run("같은 입력 시퀀스 → 스프링 상태 비트 동일", TestSpringDeterminism, ref passed, ref failed, report);
            Run("같은 입력 시퀀스 → 진자 상태 비트 동일", TestPendulumDeterminism, ref passed, ref failed, report);
            Run("같은 입력 시퀀스 → 램프 각도 비트 동일", TestLampDeterminism, ref passed, ref failed, report);
            Run("해시 잡음은 난수가 아니다 — 같은 키가 같은 값", TestHashDeterminism, ref passed, ref failed, report);

            // ── 할당 0 ──────────────────────────────────────────────────────────
            Run("스프링 10,000 스텝 할당 0 B", TestSpringZeroAlloc, ref passed, ref failed, report);
            Run("진자 10,000 스텝 할당 0 B", TestPendulumZeroAlloc, ref passed, ref failed, report);
            Run("반응자 10,000 스텝 할당 0 B", TestReactorZeroAlloc, ref passed, ref failed, report);
            Run("사슬 4마디 10,000 스텝 할당 0 B", TestChainZeroAlloc, ref passed, ref failed, report);

            // ── 진폭 상한 (판독성) ──────────────────────────────────────────────
            Run("램프 흔들림이 설정 상한을 넘지 않는다", TestLampAmplitudeCap, ref passed, ref failed, report);
            Run("승객이 가속 쪽으로 기울고 상한을 지킨다", TestBraceSignAndCap, ref passed, ref failed, report);
            Run("화물이 정지 마찰 아래에서는 꿈쩍도 하지 않는다", TestCargoStiction, ref passed, ref failed, report);
            Run("화물이 문턱을 넘으면 실제로 미끄러진다", TestCargoActuallySlides, ref passed, ref failed, report);
            Run("화물이 최대 표류 거리를 넘지 않는다", TestCargoDriftCap, ref passed, ref failed, report);
            Run("문 오버슛이 상한을 넘지 않는다", TestDoorOvershootCap, ref passed, ref failed, report);
            Run("레버가 오버슛한 뒤 쉬는 각도로 정확히 돌아온다", TestLeverReturns, ref passed, ref failed, report);

            // ── 사고 복귀 ───────────────────────────────────────────────────────
            Run("합성 가속도가 낙하에서 음수, 착지에서 양수", TestCollapseAccelShape, ref passed, ref failed, report);
            Run("사고 산란 뒤 복귀 오차가 정확히 0", TestCollapseRestoreExact, ref passed, ref failed, report);
            Run("반응자 충격 뒤 복귀 오차가 정확히 0", TestReactorRestoreExact, ref passed, ref failed, report);

            // ── 의존 방향 ───────────────────────────────────────────────────────
            Run("물리 타입이 게임 판정 타입을 참조하지 않는다", TestNoJudgementDependency, ref passed, ref failed, report);
            Run("물리 계층에 Rigidbody·Joint·Collider 필드가 없다", TestNoRigidbodyFields, ref passed, ref failed, report);

            report.Insert(0, "[상승] === 표현 계층 물리 Tests ===\n");
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
                report.AppendLine($"  FAIL  {name} — 예외: {e.GetType().Name}: {e.Message}");
            }
        }

        // ── 안정성 ──────────────────────────────────────────────────────────────

        private static string TestSpringSettles()
        {
            var s = new DampedSpring1D();
            s.AddImpulse(40f);
            float prevEnergy = s.Energy(0f, 12f);
            int rose = 0;

            for (int i = 0; i < 20000; i++)
            {
                s.Step(Step, 0f, 12f, 0.25f, 100f);
                float e = s.Energy(0f, 12f);
                // 반음시적 오일러는 한 스텝 안에서 미세하게 오르내릴 수 있다.
                // 문제는 **추세**이므로 연속 상승 횟수를 센다.
                if (e > prevEnergy + 1e-6f) rose++;
                prevEnergy = e;
                if (s.IsAtRest()) break;
            }

            if (!s.IsAtRest())
                return $"20,000 스텝 뒤에도 멈추지 않았다 (값 {s.Value:F6}, 속도 {s.Velocity:F6})";
            if (rose > 50)
                return $"에너지가 {rose}회 증가했다 — 적분기가 에너지를 만들고 있다";
            if (s.Value != 0f || s.Velocity != 0f)
                return $"정지가 정확히 0 이 아니다 ({s.Value:E}, {s.Velocity:E})";
            return null;
        }

        private static string TestSpringExtremeImpulse()
        {
            var s = new DampedSpring1D();
            s.AddImpulse(1e6f);
            for (int i = 0; i < 40000; i++)
            {
                s.Step(Step, 0f, 30f, 0.05f, 0.5f);
                if (float.IsNaN(s.Value) || float.IsInfinity(s.Value))
                    return $"{i} 스텝에서 NaN/Inf 가 됐다";
                if (Mathf.Abs(s.Value) > 0.5f + 1e-4f)
                    return $"{i} 스텝에서 상한 0.5 를 넘었다 ({s.Value:F6})";
            }
            if (!s.IsAtRest())
                return $"극단 임펄스 뒤 40,000 스텝에도 정지하지 않았다 (속도 {s.Velocity:E})";
            return null;
        }

        private static string TestSpringWorstCaseBounded()
        {
            // 인스펙터가 허용하는 최악 조합: 최고 각진동수 · 최저 감쇠 · 기본 서브스텝.
            // 안정 조건은 dt < 2/ω = 2/160 = 0.0125 이고 기본 스텝은 1/240 = 0.00417 이다.
            var s = new DampedSpring1D();
            s.AddImpulse(500f);
            float peak = 0f;
            for (int i = 0; i < 60000; i++)
            {
                s.Step(Step, 0f, 160f, 0.05f, 0.02f);
                peak = Mathf.Max(peak, Mathf.Abs(s.Value));
                if (float.IsNaN(s.Value)) return $"{i} 스텝에서 NaN";
            }
            if (peak > 0.02f + 1e-5f) return $"상한 0.02 를 넘었다 (최대 {peak:F6})";
            if (!s.IsAtRest()) return $"정지하지 않았다 (속도 {s.Velocity:E})";
            return null;
        }

        /// <summary>
        /// **이 버그가 실제로 있었다.** 상한을 <c>target ± maxAbs</c> 로 걸면, 목표가
        /// 움직이는 곳(사슬 추종·고리 비틀림·케이블 순응)에서 창이 목표를 따라가
        /// 실효 상한이 최대 두 배가 된다. 사슬 18° 에서 18.39°, 고리 80° 에서 80.27°
        /// 가 나왔다. 판독성 상한이 「대체로 지켜진다」면 그건 상한이 아니다.
        /// </summary>
        private static string TestClampIsAbsolute()
        {
            var s = new DampedSpring1D();
            const float cap = 0.3f;
            for (int i = 0; i < 20000; i++)
            {
                // 목표를 상한 밖으로 계속 밀어붙인다. 상한이 목표를 따라가면 여기서 새어 나간다.
                float target = ((i / 100) % 2 == 0) ? 5f : -5f;
                s.Step(Step, target, 20f, 0.15f, cap);
                if (Mathf.Abs(s.Value) > cap + 1e-5f)
                    return $"{i} 스텝에서 절대 상한 {cap} 을 넘었다 ({s.Value:F6})";
            }
            return null;
        }

        private static string TestClampCenterMoves()
        {
            // 레버처럼 쉬는 위치가 0 이 아닌 경우. 창은 center 기준이어야 한다.
            var s = new DampedSpring1D();
            s.Reset(20f);
            const float center = 20f, cap = 10f;
            for (int i = 0; i < 20000; i++)
            {
                s.AddImpulse(((i / 200) % 2 == 0) ? 60f : -60f);
                s.Step(Step, center, 18f, 0.12f, cap, center);
                if (s.Value < center - cap - 1e-4f || s.Value > center + cap + 1e-4f)
                    return $"{i} 스텝에서 [{center - cap}, {center + cap}] 밖이다 ({s.Value:F5})";
            }
            s.Step(Step, center, 18f, 0.12f, cap, center);
            for (int i = 0; i < 40000; i++) s.Step(Step, center, 18f, 0.12f, cap, center);
            if (!s.IsAtRest(center)) return $"쉬는 각도로 정확히 돌아오지 않았다 ({s.Value:E})";
            return null;
        }

        private static string TestPendulumSettles()
        {
            var p = new PendulumState();
            p.AddImpulse(6f, -4f);
            float prev = p.Energy(0.8f);
            int rose = 0;

            for (int i = 0; i < 40000; i++)
            {
                p.Step(Step, 0f, 0f, 0f, 0.8f, 0.9f, 12f * Mathf.Deg2Rad);
                float e = p.Energy(0.8f);
                if (e > prev + 1e-6f) rose++;
                prev = e;
                if (p.IsAtRest()) break;
            }

            if (!p.IsAtRest())
                return $"멈추지 않았다 (각 {p.AngleX:E}/{p.AngleZ:E}, 속도 {p.VelocityX:E}/{p.VelocityZ:E})";
            if (rose > 200)
                return $"에너지가 {rose}회 증가했다 — 상한에 튕기는 것이 아니라 발산이면 문제다";
            return null;
        }

        private static string TestPendulumExtremeDrive()
        {
            var p = new PendulumState();
            float cap = 12f * Mathf.Deg2Rad;
            for (int i = 0; i < 20000; i++)
            {
                // 부호를 흔들어 최악의 공진 입력을 만든다.
                float a = (i / 40) % 2 == 0 ? 1000f : -1000f;
                p.Step(Step, a, -a, 0f, 0.8f, 0.9f, cap);
                if (float.IsNaN(p.AngleX) || float.IsNaN(p.AngleZ))
                    return $"{i} 스텝에서 NaN";
                if (Mathf.Abs(p.AngleX) > cap + 1e-5f || Mathf.Abs(p.AngleZ) > cap + 1e-5f)
                    return $"{i} 스텝에서 상한을 넘었다 ({p.AngleX * Mathf.Rad2Deg:F3}°, {p.AngleZ * Mathf.Rad2Deg:F3}°)";
            }
            return null;
        }

        private static string TestPendulumFreeFall()
        {
            // 자유낙하: 유효 중력이 0 이 되어 복원력이 사라진다. 바닥(0.5)을 깔아
            // 두지 않으면 부호가 뒤집혀 추가 위로 서 버린다.
            var p = new PendulumState();
            p.AddImpulse(2f, 0f);
            float cap = 20f * Mathf.Deg2Rad;
            for (int i = 0; i < 5000; i++)
            {
                p.Step(Step, 0f, 0f, -PendulumState.Gravity * 1.5f, 0.8f, 0.9f, cap);
                if (float.IsNaN(p.AngleX)) return $"{i} 스텝에서 NaN";
                if (Mathf.Abs(p.AngleX) > cap + 1e-5f)
                    return $"{i} 스텝에서 상한을 넘었다 ({p.AngleX * Mathf.Rad2Deg:F3}°)";
            }
            return null;
        }

        private static string TestStepperClampsBurst()
        {
            var st = new PhysicsStepper();
            st.Configure(Step);
            int steps = st.Begin(5f);   // 5초짜리 히칭
            if (steps != PhysicsStepper.MaxStepsPerFrame)
                return $"스텝 수가 {steps} 다 — {PhysicsStepper.MaxStepsPerFrame} 로 잘려야 한다";

            // 다음 프레임이 정상 dt 면 정상 스텝 수여야 한다(이월분을 버렸으므로).
            int next = st.Begin(1f / 60f);
            if (next > PhysicsStepper.MaxStepsPerFrame)
                return $"다음 프레임도 {next} 스텝이다 — 이월분을 안 버려 죽음의 나선이 된다";
            return null;
        }

        private static string TestStepperCarriesRemainder()
        {
            var st = new PhysicsStepper();
            st.Configure(Step);
            int total = 0;
            // 60fps 를 1초. 이월이 없으면 240 스텝에 크게 못 미친다.
            for (int i = 0; i < 60; i++) total += st.Begin(1f / 60f);
            if (total < 236 || total > 240)
                return $"1초에 {total} 스텝이다 — 240 근처여야 이월이 동작하는 것이다";
            return null;
        }

        // ── 결정론 ──────────────────────────────────────────────────────────────

        private static string TestFixedClockTime()
        {
            var c = new FixedStepPhysicsClock(1f / 60f);
            for (int i = 0; i < 600; i++) c.Tick();
            if (c.StepIndex != 600) return $"스텝 인덱스가 {c.StepIndex}";
            if (c.DeltaTime != 1f / 60f) return "DeltaTime 이 고정 스텝과 다르다";

            // 누산이 아니라 곱이므로, 같은 스텝 수는 항상 같은 비트다.
            var d = new FixedStepPhysicsClock(1f / 60f);
            for (int i = 0; i < 600; i++) d.Tick();
            if (Bits(c.ElapsedTime) != Bits(d.ElapsedTime))
                return $"같은 스텝 수인데 시각이 다르다 ({c.ElapsedTime:E} vs {d.ElapsedTime:E})";
            return null;
        }

        private static string TestSpringDeterminism()
        {
            var a = new DampedSpring1D();
            var b = new DampedSpring1D();
            for (int i = 0; i < 5000; i++)
            {
                float target = Mathf.Sin(i * 0.013f) * 0.4f;
                if (i % 500 == 0) { a.AddImpulse(7.5f); b.AddImpulse(7.5f); }
                a.Step(Step, target, 14f, 0.2f, 1f);
                b.Step(Step, target, 14f, 0.2f, 1f);
                if (Bits(a.Value) != Bits(b.Value) || Bits(a.Velocity) != Bits(b.Velocity))
                    return $"{i} 스텝에서 갈라졌다 ({a.Value:E} vs {b.Value:E})";
            }
            return null;
        }

        private static string TestPendulumDeterminism()
        {
            var a = new PendulumState();
            var b = new PendulumState();
            for (int i = 0; i < 5000; i++)
            {
                float ax = Mathf.Sin(i * 0.007f) * 9f;
                float az = Mathf.Cos(i * 0.011f) * 5f;
                a.Step(Step, ax, az, 0f, 0.7f, 0.8f, 0.4f);
                b.Step(Step, ax, az, 0f, 0.7f, 0.8f, 0.4f);
                if (Bits(a.AngleX) != Bits(b.AngleX) || Bits(a.AngleZ) != Bits(b.AngleZ))
                    return $"{i} 스텝에서 갈라졌다";
            }
            return null;
        }

        private static string TestLampDeterminism()
        {
            GameObject ga = null, gb = null;
            try
            {
                SwingingLamp a = New<SwingingLamp>(out ga);
                SwingingLamp b = New<SwingingLamp>(out gb);
                for (int i = 0; i < 3000; i++)
                {
                    Vector3 accel = new Vector3(Mathf.Sin(i * 0.01f) * 6f, Mathf.Cos(i * 0.017f) * 3f, 4f);
                    a.StepForTest(Step, accel, 1);
                    b.StepForTest(Step, accel, 1);
                }
                Vector2 x = a.AngleDegrees, y = b.AngleDegrees;
                if (Bits(x.x) != Bits(y.x) || Bits(x.y) != Bits(y.y))
                    return $"각도가 비트 단위로 다르다 ({x} vs {y})";
                return null;
            }
            finally { Kill(ga); Kill(gb); }
        }

        private static string TestHashDeterminism()
        {
            // 해시가 난수라면 두 번째 호출이 다른 값을 준다. 그러면 캡처가 재현되지 않는다.
            for (int i = 0; i < 64; i++)
            {
                float p = ProbeHash(20260802, i);
                float q = ProbeHash(20260802, i);
                if (Bits(p) != Bits(q)) return $"index {i} 에서 두 호출이 다르다";
                if (p < 0f || p > 1f) return $"index {i} 값이 [0,1] 밖이다 ({p})";
            }
            // 서로 다른 인덱스가 전부 같은 값이면 변주가 없는 것이다.
            float first = ProbeHash(20260802, 0);
            bool anyDifferent = false;
            for (int i = 1; i < 64; i++)
                if (Bits(ProbeHash(20260802, i)) != Bits(first)) { anyDifferent = true; break; }
            if (!anyDifferent) return "모든 인덱스가 같은 값이다 — 변주가 없다";
            return null;
        }

        // ── 할당 0 ──────────────────────────────────────────────────────────────

        private static string TestSpringZeroAlloc()
        {
            var s = new DampedSpring1D();
            s.AddImpulse(3f);
            // 예열: JIT 과 첫 접근의 할당을 측정 밖으로 뺀다.
            for (int i = 0; i < 256; i++) s.Step(Step, 0f, 12f, 0.3f, 1f);

            long before = AllocatedBytes();
            for (int i = 0; i < 10000; i++)
            {
                if (i % 1000 == 0) s.AddImpulse(3f);
                s.Step(Step, 0f, 12f, 0.3f, 1f);
            }
            long delta = AllocatedBytes() - before;
            return delta == 0 ? null : $"10,000 스텝에 {delta} B 할당됐다";
        }

        private static string TestPendulumZeroAlloc()
        {
            var p = new PendulumState();
            for (int i = 0; i < 256; i++) p.Step(Step, 1f, 1f, 0f, 0.8f, 0.9f, 0.3f);

            long before = AllocatedBytes();
            for (int i = 0; i < 10000; i++) p.Step(Step, 1f, -1f, 0.5f, 0.8f, 0.9f, 0.3f);
            long delta = AllocatedBytes() - before;
            return delta == 0 ? null : $"10,000 스텝에 {delta} B 할당됐다";
        }

        private static string TestReactorZeroAlloc()
        {
            GameObject host = null;
            try
            {
                SwingingLamp lamp = New<SwingingLamp>(out host);
                Vector3 accel = new Vector3(2f, 1f, 3f);
                lamp.StepForTest(Step, accel, 256);   // 예열

                long before = AllocatedBytes();
                lamp.StepForTest(Step, accel, 10000);
                long delta = AllocatedBytes() - before;
                return delta == 0 ? null : $"10,000 스텝에 {delta} B 할당됐다";
            }
            finally { Kill(host); }
        }

        private static string TestChainZeroAlloc()
        {
            GameObject host = null;
            try
            {
                HangingChain chain = New<HangingChain>(out host);
                var links = new Transform[4];
                for (int i = 0; i < links.Length; i++)
                {
                    var go = new GameObject($"link{i}");
                    go.transform.SetParent(host.transform, false);
                    links[i] = go.transform;
                }
                chain.ConfigureLinks(links);

                Vector3 accel = new Vector3(2f, 0.5f, 3f);
                chain.StepForTest(Step, accel, 256);

                long before = AllocatedBytes();
                chain.StepForTest(Step, accel, 10000);
                long delta = AllocatedBytes() - before;
                return delta == 0 ? null : $"4마디 10,000 스텝에 {delta} B 할당됐다";
            }
            finally { Kill(host); }
        }

        // ── 진폭 상한 ───────────────────────────────────────────────────────────

        private static string TestLampAmplitudeCap()
        {
            GameObject host = null;
            try
            {
                SwingingLamp lamp = New<SwingingLamp>(out host);
                // 기본 상한은 12도. 인스펙터 기본값을 그대로 검사하는 것이 핵심이다 —
                // 배선하는 사람이 아무것도 안 건드려도 판독성이 지켜져야 한다.
                for (int i = 0; i < 8000; i++)
                {
                    float sign = (i / 60) % 2 == 0 ? 1f : -1f;
                    lamp.StepForTest(Step, new Vector3(400f * sign, 200f * sign, 400f * sign), 1);
                    Vector2 a = lamp.AngleDegrees;
                    if (Mathf.Abs(a.x) > 12f + 1e-3f || Mathf.Abs(a.y) > 12f + 1e-3f)
                        return $"{i} 스텝에서 12° 상한을 넘었다 ({a.x:F3}, {a.y:F3})";
                }
                return null;
            }
            finally { Kill(host); }
        }

        private static string TestBraceSignAndCap()
        {
            GameObject host = null;
            try
            {
                PassengerBrace brace = New<PassengerBrace>(out host);
                // +X 방향 가속 → 사람은 +X 쪽으로 몸을 넣는다.
                // 매달린 것과 **반대 부호**여야 한다.
                brace.StepForTest(Step, new Vector3(6f, 0f, 0f), 2000);
                Vector2 tilt = brace.TiltDegrees;
                if (tilt.y <= 0f)
                    return $"+X 가속에 Z 기울기가 {tilt.y:F3} — 부호가 반대다(가속 쪽으로 버텨야 한다)";

                for (int i = 0; i < 6000; i++)
                {
                    float sign = (i / 50) % 2 == 0 ? 1f : -1f;
                    brace.StepForTest(Step, new Vector3(500f * sign, 300f * sign, 500f * sign), 1);
                    Vector2 t = brace.TiltDegrees;
                    if (Mathf.Abs(t.x) > 7f + 1e-3f || Mathf.Abs(t.y) > 7f + 1e-3f)
                        return $"{i} 스텝에서 7° 상한을 넘었다 ({t.x:F3}, {t.y:F3})";
                }
                return null;
            }
            finally { Kill(host); }
        }

        private static string TestCargoStiction()
        {
            GameObject host = null;
            try
            {
                LooseCargo cargo = New<LooseCargo>(out host);
                // 기본 정지 마찰 문턱은 3.2 m/s². 그 아래 가속은 상자를 못 움직인다.
                cargo.StepForTest(Step, new Vector3(1.5f, 0f, 1.5f), 5000);
                if (cargo.DriftDistance > 1e-6f)
                    return $"문턱 아래 가속에 {cargo.DriftDistance:E} m 움직였다 — 상자가 영원히 떤다";
                return null;
            }
            finally { Kill(host); }
        }

        /// <summary>
        /// **이 단정이 없어서 죽은 컴포넌트를 놓칠 뻔했다.** 첫 판본은 최대 구동력이
        /// 운동 마찰보다 작아 화물이 어떤 입력에도 움직이지 않았고, 「상한을 넘지
        /// 않는다」류 단정만 있으면 그 상태가 그대로 초록으로 통과한다.
        /// 상한 검사에는 반드시 「그래도 움직인다」가 짝으로 붙어야 한다.
        /// </summary>
        private static string TestCargoActuallySlides()
        {
            GameObject host = null;
            try
            {
                LooseCargo cargo = New<LooseCargo>(out host);
                // μs = 0.33 → 문턱 약 3.24 m/s². 6 m/s² 는 확실히 넘는다.
                cargo.StepForTest(Step, new Vector3(6f, 0f, 0f), 240);   // 1초
                if (cargo.DriftDistance < 0.005f)
                    return $"문턱을 넘는 가속에도 {cargo.DriftDistance:E} m 밖에 안 움직였다 " +
                           $"— 운동 마찰이 구동력보다 커서 화물이 얼어 있는 것이다";
                if (!cargo.IsSliding)
                    return "미끄러지는 중인데 IsSliding 이 false 다";
                return null;
            }
            finally { Kill(host); }
        }

        private static string TestCargoDriftCap()
        {
            GameObject host = null;
            try
            {
                LooseCargo cargo = New<LooseCargo>(out host);
                for (int i = 0; i < 20000; i++)
                {
                    // 한 방향으로 계속 밀어 최대치를 노린다.
                    cargo.StepForTest(Step, new Vector3(18f, 0f, 18f), 1);
                    if (cargo.DriftDistance > 0.22f + 1e-4f)
                        return $"{i} 스텝에서 최대 표류 0.22m 를 넘었다 ({cargo.DriftDistance:F5})";
                }
                return null;
            }
            finally { Kill(host); }
        }

        private static string TestDoorOvershootCap()
        {
            GameObject host = null;
            try
            {
                DoorImpact door = New<DoorImpact>(out host);
                for (int i = 0; i < 40; i++)
                {
                    door.Latch(20f);              // 말도 안 되는 속도로 계속 때린다
                    door.StepForTest(Step, 40);
                    if (Mathf.Abs(door.OvershootMeters) > 0.018f + 1e-5f)
                        return $"문짝 오버슛 상한 0.018m 를 넘었다 ({door.OvershootMeters:F6})";
                    if (Mathf.Abs(door.FrameShudderMeters) > 0.004f + 1e-5f)
                        return $"문틀 떨림 상한 0.004m 를 넘었다 ({door.FrameShudderMeters:F6})";
                }
                door.StepForTest(Step, 40000);
                if (door.IsRinging)
                    return "충격이 멈추지 않았다 — 문이 영원히 운다";
                return null;
            }
            finally { Kill(host); }
        }

        private static string TestLeverReturns()
        {
            GameObject host = null;
            try
            {
                LeverPhysics lever = New<LeverPhysics>(out host);
                lever.Pull();

                float extreme = 0f;
                for (int i = 0; i < 4000; i++)
                {
                    lever.StepForTest(Step, 1);
                    extreme = Mathf.Min(extreme, lever.AngleDegrees);
                }
                if (extreme > -5f)
                    return $"레버가 당겨지지 않았다 (최저 {extreme:F3}°)";
                if (extreme < -62f - 1e-3f)
                    return $"레버가 최대 각변위 62° 를 넘었다 ({extreme:F3}°)";

                lever.StepForTest(Step, 40000);
                if (!lever.IsAtRest)
                    return $"레버가 쉬는 각도로 정확히 돌아오지 않았다 ({lever.AngleDegrees:E})";
                return null;
            }
            finally { Kill(host); }
        }

        // ── 사고 복귀 ───────────────────────────────────────────────────────────

        private static string TestCollapseAccelShape()
        {
            GameObject host = null;
            try
            {
                CollapsePhysics cp = New<CollapsePhysics>(out host);

                // 기본 박자: 암전 0.55 · 낙하 0.9 · 자유낙하 비율 0.55 → 충격은 1.045 초.
                if (cp.SyntheticVerticalAcceleration(0.2f) != 0f)
                    return "암전 구간에서 가속이 0 이 아니다 — 아직 떨어지지 않았다";

                float falling = cp.SyntheticVerticalAcceleration(0.8f);
                if (falling >= 0f)
                    return $"자유낙하 구간 가속이 {falling:F3} 이다 — 음수(가벼워짐)여야 한다";

                float landing = cp.SyntheticVerticalAcceleration(1.05f);
                if (landing <= 0f)
                    return $"착지 구간 가속이 {landing:F3} 이다 — 큰 양수(때림)여야 한다";
                if (landing < Mathf.Abs(falling))
                    return "착지 충격이 자유낙하보다 약하다 — 채찍질이 안 보인다";

                if (cp.SyntheticVerticalAcceleration(5f) != 0f)
                    return "연출이 끝난 뒤에도 가속을 밀고 있다";
                return null;
            }
            finally { Kill(host); }
        }

        private static string TestCollapseRestoreExact()
        {
            GameObject host = null;
            try
            {
                CollapsePhysics cp = New<CollapsePhysics>(out host);

                var targets = new Transform[3];
                var homes = new Vector3[3];
                for (int i = 0; i < targets.Length; i++)
                {
                    var go = new GameObject($"debris{i}");
                    go.transform.SetParent(host.transform, false);
                    go.transform.localPosition = new Vector3(0.13f * i, 1.7f - 0.31f * i, -0.42f + 0.09f * i);
                    targets[i] = go.transform;
                    homes[i] = go.transform.localPosition;
                }
                cp.Configure(null, null, null, targets);

                cp.FireLandingForTest();
                cp.StepScatterForTest(Step, 200);

                if (cp.PeakDisplacement <= 0f)
                    return "산란이 아예 일어나지 않았다 — 사고가 관측되지 않는다";

                bool moved = false;
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i].localPosition != homes[i]) { moved = true; break; }
                if (!moved) return "대상이 하나도 움직이지 않았다";

                cp.RestoreAll();

                for (int i = 0; i < targets.Length; i++)
                {
                    Vector3 p = targets[i].localPosition;
                    if (Bits(p.x) != Bits(homes[i].x) ||
                        Bits(p.y) != Bits(homes[i].y) ||
                        Bits(p.z) != Bits(homes[i].z))
                        return $"debris{i} 복귀 오차가 0 이 아니다 " +
                               $"({(p - homes[i]).x:E}, {(p - homes[i]).y:E}, {(p - homes[i]).z:E})";
                }
                return null;
            }
            finally { Kill(host); }
        }

        private static string TestReactorRestoreExact()
        {
            GameObject host = null;
            try
            {
                SwingingLamp lamp = New<SwingingLamp>(out host);
                host.transform.localPosition = new Vector3(0.4f, 2.9f, -1.13f);
                host.transform.localRotation = Quaternion.Euler(3f, 41f, -7f);
                Vector3 homePos = host.transform.localPosition;
                Quaternion homeRot = host.transform.localRotation;
                // 홈을 지금 자세로 다시 잡는다(테스트가 Awake 를 안 돌렸으므로).
                lamp.ResetToHome();
                lamp.StepForTest(Step, Vector3.zero, 1);
                homePos = host.transform.localPosition;
                homeRot = host.transform.localRotation;

                lamp.AddShock(new Vector3(3f, -2f, 4f));
                lamp.StepForTest(Step, new Vector3(9f, 4f, -6f), 400);

                lamp.ResetToHome();

                Vector3 p = host.transform.localPosition;
                Quaternion r = host.transform.localRotation;
                if (Bits(p.x) != Bits(homePos.x) || Bits(p.y) != Bits(homePos.y) || Bits(p.z) != Bits(homePos.z))
                    return $"위치 복귀 오차가 0 이 아니다 ({(p - homePos)})";
                if (Bits(r.x) != Bits(homeRot.x) || Bits(r.y) != Bits(homeRot.y) ||
                    Bits(r.z) != Bits(homeRot.z) || Bits(r.w) != Bits(homeRot.w))
                    return "회전 복귀 오차가 0 이 아니다";
                if (!lamp.IsAtRest) return "상태가 정지로 돌아가지 않았다";
                return null;
            }
            finally { Kill(host); }
        }

        // ── 의존 방향 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 물리가 판정을 읽지 않는지 **코드로** 검사한다.
        ///
        /// 한계를 정직하게 적는다: 필드·프로퍼티·메서드 시그니처만 본다. IL 을 훑지
        /// 않으므로 메서드 본문 안에서 <c>FindAnyObjectByType&lt;RunSessionBehaviour&gt;</c>
        /// 를 부르는 것까지는 못 잡는다. 그럼에도 값이 있는 이유는 **판정 상태를
        /// 쓰려면 결국 그 타입이 시그니처에 나타나야** 하기 때문이다 — 상태를 바꾸는
        /// 코드는 참조를 보관하거나 인자로 받는다.
        /// </summary>
        private static string TestNoJudgementDependency()
        {
            string[] forbidden =
            {
                "RunSession", "RunSessionBehaviour", "FloorSession", "SpinEngine",
                "SpinBoard", "SpinResolution", "GameEventBus", "RiskEvaluator",
                "RunController", "ElevatorState", "PassengerManager", "SpinRuleSet",
            };

            var offenders = new StringBuilder();
            foreach (Type t in PhysicsTypes())
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Static |
                                                    BindingFlags.Public | BindingFlags.NonPublic))
                    Check(f.FieldType, $"{t.Name}.{f.Name}", forbidden, offenders);

                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Instance | BindingFlags.Static |
                                                           BindingFlags.Public | BindingFlags.NonPublic))
                    Check(p.PropertyType, $"{t.Name}.{p.Name}", forbidden, offenders);

                foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                                      BindingFlags.Public | BindingFlags.NonPublic |
                                                      BindingFlags.DeclaredOnly))
                {
                    Check(m.ReturnType, $"{t.Name}.{m.Name}()", forbidden, offenders);
                    foreach (ParameterInfo pi in m.GetParameters())
                        Check(pi.ParameterType, $"{t.Name}.{m.Name}({pi.Name})", forbidden, offenders);
                }
            }

            return offenders.Length == 0 ? null
                : $"판정 타입 참조: {offenders}";
        }

        private static void Check(Type type, string where, string[] forbidden, StringBuilder into)
        {
            if (type == null) return;
            Type t = type.IsArray ? type.GetElementType() : type;
            if (t == null) return;
            for (int i = 0; i < forbidden.Length; i++)
                if (t.Name == forbidden[i]) into.Append(where).Append(" → ").Append(t.Name).Append("; ");
        }

        /// <summary>
        /// 「Rigidbody 를 쓰지 않았다」를 주장이 아니라 검사로 만든다. 나중에 누가
        /// 편의로 하나 붙이면 그 순간 결정론·할당 0 근거가 사라지므로, 붙이려면
        /// 이 테스트를 **의도적으로** 고쳐야 한다.
        /// </summary>
        private static string TestNoRigidbodyFields()
        {
            string[] forbidden = { "Rigidbody", "Rigidbody2D", "HingeJoint", "ConfigurableJoint",
                                   "FixedJoint", "SpringJoint", "CharacterJoint", "Collider",
                                   "BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider",
                                   "PhysicsMaterial" };
            var offenders = new StringBuilder();
            foreach (Type t in PhysicsTypes())
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Static |
                                                    BindingFlags.Public | BindingFlags.NonPublic))
                    Check(f.FieldType, $"{t.Name}.{f.Name}", forbidden, offenders);

            return offenders.Length == 0 ? null
                : $"강체·조인트·콜라이더 필드가 있다: {offenders}";
        }

        private static System.Collections.Generic.List<Type> PhysicsTypes()
        {
            var list = new System.Collections.Generic.List<Type>();
            Type anchor = typeof(CabinInertiaReactor);
            foreach (Type t in anchor.Assembly.GetTypes())
                if (t.Namespace == "Ascend.Prototype.Physics") list.Add(t);
            return list;
        }

        // ── 도구 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 비활성 오브젝트에 붙인다. 그래야 <c>Awake</c> 가 돌지 않아
        /// <c>FindAnyObjectByType</c> 이 씬을 훑지 않는다 — `PresentationBindingTests`
        /// 가 같은 이유로 같은 방식을 쓴다.
        /// </summary>
        private static T New<T>(out GameObject host) where T : Component
        {
            host = new GameObject($"__PhysicsProbe_{typeof(T).Name}__");
            host.SetActive(false);
            return host.AddComponent<T>();
        }

        private static void Kill(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>
        /// 부동소수를 **비트로** 비교한다. <c>==</c> 는 -0.0 과 0.0 을 같다고 하고
        /// NaN 을 전부 다르다고 한다. 「비트 동일」을 묻는 자리에서는 틀린 도구다.
        /// </summary>
        private static int Bits(float v) => BitConverter.ToInt32(BitConverter.GetBytes(v), 0);

        /// <summary>
        /// 이 스레드가 지금까지 할당한 누적 바이트. <c>GC.GetTotalMemory</c> 와 달리
        /// 수집에 흔들리지 않아 「이 구간에서 0 B」를 직접 물을 수 있다.
        /// </summary>
        private static long AllocatedBytes() => GC.GetAllocatedBytesForCurrentThread();

        /// <summary>
        /// <see cref="CabinInertiaReactor.Hash01"/> 과 같은 식. protected 라
        /// 테스트에서 직접 못 부르므로 같은 식을 복제한다 — 복제가 아니라
        /// **독립 재구현**이어서, 원본이 바뀌면 이 테스트가 그것을 잡는다.
        /// </summary>
        private static float ProbeHash(int seed, int index)
        {
            uint h = (uint)(seed * 374761393 + index * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) * (1f / 0xFFFFFF);
        }
    }
}
