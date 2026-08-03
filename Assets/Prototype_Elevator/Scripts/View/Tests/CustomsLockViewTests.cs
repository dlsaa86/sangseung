using System;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.View.Tests
{
    /// <summary>
    /// <see cref="CustomsLockView"/> — 레버에서 9개 챔버까지의 **동력 전달**을 검사한다.
    ///
    /// ## 왜 이 스위트가 생겼는가
    ///
    /// 2026-08-03 지시가 실패 조건을 한 문장으로 못박았다 —
    /// 「레버를 당겼는데 **외부 연결부는 가만히 있고** 영혼만 갑자기 멈추면 실패다.」
    ///
    /// 그리고 첫 구현이 정확히 그 상태였다. 실측에서 로드·축·탭·클램프가 전부
    /// **0.0mm / 0.0°** 였다 — `Engage()` 직후 레버가 아직 Idle 이라 그 프레임에
    /// 곧바로 되감겼기 때문이다.
    ///
    /// ## 🔴 그때 내 검사가 **공허하게 통과했다**
    ///
    /// 「최초 움직임 시각」을 −1(=움직인 적 없음)로 초기화하고
    /// <c>rod ≤ shaft ≤ tab ≤ clamp</c> 를 확인했다. 아무것도 안 움직이면
    /// −1 ≤ −1 ≤ −1 ≤ −1 이라 **참이다.** 화면에서는 죽어 있는데 검사는 초록이었다.
    ///
    /// 이 저장소가 반복해서 당한 그 실패다. 그래서 여기서는 순서를 재기 **전에**
    /// 「각 단계가 실제로 움직였는가」를 먼저 단정한다 — 순서 검사는 그 다음이다.
    /// </summary>
    public static class CustomsLockViewTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();

            Run("체결하면 네 단계가 **전부** 실제로 움직인다", TestAllStagesMove, ref passed, ref failed, report);
            Run("단계가 지연 순서대로 시작한다 (로드→축→탭→클램프)", TestStageOrder, ref passed, ref failed, report);
            Run("아홉 클램프가 같은 각도로 함께 물린다", TestAllClampsTogether, ref passed, ref failed, report);
            Run("상태 탭 셋이 동시에 같은 거리를 간다", TestTabsMoveTogether, ref passed, ref failed, report);
            Run("풀면 원위치로 정확히 돌아온다", TestReleaseReturnsHome, ref passed, ref failed, report);
            Run("전력이 없으면 잠금핀이 슬롯을 막는다", TestPinBlocksWhenLocked, ref passed, ref failed, report);
            Run("유휴 프레임에 트랜스폼을 쓰지 않는다", TestIdleIsQuiet, ref passed, ref failed, report);
            Run("프레임률이 달라도 최종 자세가 같다", TestFrameRateIndependent, ref passed, ref failed, report);
            Run("레버가 배선돼도 체결이 다음 스텝에 풀리지 않는다", TestEngageSurvivesWiredLever, ref passed, ref failed, report);
            Run("레버가 원위치로 **돌아오면** 그때 풀린다", TestLeverReturnReleases, ref passed, ref failed, report);
            Run("풀면 아홉 클램프와 세 탭이 **전부** 원위치다", TestReleaseReturnsEveryPart, ref passed, ref failed, report);

            return (passed, failed, report.ToString());
        }

        // ── 리그 ────────────────────────────────────────────────────────────

        private sealed class Rig
        {
            public GameObject Root;
            public CustomsLockView View;
            public Transform Rod, Shaft, Pin;
            public Transform[] Tabs = new Transform[CustomsLockView.Banks];
            public Transform[] Clamps = new Transform[CustomsLockView.Chambers];

            public void Destroy() { if (Root != null) UnityEngine.Object.DestroyImmediate(Root); }
        }

        /// <summary>
        /// 씬 없이 부품만 세운다. **레버를 붙이지 않는다** — 레버 없이도 동작해야
        /// 한다는 것이 이 설계의 요점이고, 첫 판본은 그러지 못해 죽어 있었다.
        /// </summary>
        private static Rig Build()
        {
            var rig = new Rig();
            rig.Root = new GameObject("CustomsLockRig");
            rig.View = rig.Root.AddComponent<CustomsLockView>();

            rig.Rod = Child(rig.Root.transform, "Rod", new Vector3(0f, 1.5f, 0f));
            rig.Shaft = Child(rig.Root.transform, "Shaft", new Vector3(0f, 1.9f, 0f));
            rig.Pin = Child(rig.Root.transform, "Pin", new Vector3(0.2f, 1.2f, 0f));
            for (int i = 0; i < CustomsLockView.Banks; i++)
                rig.Tabs[i] = Child(rig.Root.transform, "Tab" + i, new Vector3(i * 0.5f, 1.95f, 0f));
            for (int i = 0; i < CustomsLockView.Chambers; i++)
                rig.Clamps[i] = Child(rig.Root.transform, "Clamp" + i, new Vector3(i * 0.1f, 1f, 0f));

            rig.View.Configure(null, rig.Rod, rig.Shaft, rig.Pin, rig.Tabs, rig.Clamps);
            return rig;
        }

        /// <summary>
        /// 🔴 **레버를 실제로 붙인 리그.**
        ///
        /// 왜 따로 필요한가: 위 `Build()` 는 레버를 `null` 로 넘기고, 자동 해제
        /// 경로가 통째로 `_lever != null` 뒤에 있다. 그래서 그 경로의 결함은
        /// **원리적으로** 위 리그로 잡히지 않는다 — 실제로 「지금 Idle 이면 푼다」는
        /// 수위 판정이 남아 있었고 여덟 개 검사가 전부 통과했다.
        ///
        /// 조건부 코드는 그 조건을 켠 리그가 없으면 시험되지 않은 코드다.
        /// </summary>
        private static Rig BuildWithLever(out LeverStateMachine lever)
        {
            var rig = Build();
            var leverGo = new GameObject("Lever");
            leverGo.transform.SetParent(rig.Root.transform, false);
            lever = leverGo.AddComponent<LeverStateMachine>();
            rig.View.Configure(lever, rig.Rod, rig.Shaft, rig.Pin, rig.Tabs, rig.Clamps);
            return rig;
        }

        private static Transform Child(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        /// <summary>고정 dt 로 굴린다. 프레임률 독립을 재려면 dt 를 바꿔 부른다.</summary>
        private static void Run(CustomsLockView v, float seconds, float dt)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps; i++) v.Step(dt);
        }

        // ── 검사 ────────────────────────────────────────────────────────────

        private static void TestAllStagesMove()
        {
            Rig r = Build();
            try
            {
                Vector3 rod0 = r.Rod.localPosition, tab0 = r.Tabs[0].localPosition;
                Quaternion sh0 = r.Shaft.localRotation, cl0 = r.Clamps[0].localRotation;

                r.View.Engage();
                Run(r.View, r.View.TotalDuration + 0.1f, 1f / 60f);

                // 🔴 **이 네 줄이 이 스위트의 존재 이유다.** 순서보다 먼저 온다 —
                // 아무것도 안 움직이면 순서는 언제나 참이기 때문이다.
                AtLeast((r.Rod.localPosition - rod0).magnitude * 1000f, 20f, "구동 로드 이동(mm)");
                AtLeast(Quaternion.Angle(sh0, r.Shaft.localRotation), 30f, "공통축 회전(도)");
                AtLeast((r.Tabs[0].localPosition - tab0).magnitude * 1000f, 20f, "상태 탭 이동(mm)");
                AtLeast(Quaternion.Angle(cl0, r.Clamps[0].localRotation), 20f, "클램프 회전(도)");

                if (!r.View.IsEngaged) throw new Exception("체결이 유지되지 않는다");
                AtLeast(r.View.Engagement, 0.99f, "체결 진행도");
            }
            finally { r.Destroy(); }
        }

        private static void TestStageOrder()
        {
            Rig r = Build();
            try
            {
                r.View.Engage();
                float[] first = { -1f, -1f, -1f, -1f };
                const float dt = 1f / 120f;
                int steps = Mathf.CeilToInt((r.View.TotalDuration + 0.1f) / dt);
                for (int i = 1; i <= steps; i++)
                {
                    r.View.Step(dt);
                    for (int s = 0; s < 4; s++)
                        if (first[s] < 0f && r.View.StageProgress(s) > 0.02f) first[s] = i * dt;
                }

                // 「움직인 적 없음」을 순서 통과로 세지 않는다.
                for (int s = 0; s < 4; s++)
                    if (first[s] < 0f) throw new Exception($"단계 {s} 가 끝까지 움직이지 않았다");

                if (!(first[0] <= first[1] && first[1] <= first[2] && first[2] <= first[3]))
                    throw new Exception($"순서가 깨졌다 — 로드 {first[0]:F3} · 축 {first[1]:F3} · " +
                                        $"탭 {first[2]:F3} · 클램프 {first[3]:F3}");

                // 단계 사이가 **분간될 만큼** 떨어져 있는가. 전부 같은 프레임에
                // 시작하면 「전달」이 아니라 한 덩어리 애니메이션이다.
                AtLeast(first[3] - first[0], 0.08f, "첫 단계와 마지막 단계의 시작 간격(초)");
            }
            finally { r.Destroy(); }
        }

        private static void TestAllClampsTogether()
        {
            Rig r = Build();
            try
            {
                var home = new Quaternion[CustomsLockView.Chambers];
                for (int i = 0; i < home.Length; i++) home[i] = r.Clamps[i].localRotation;

                r.View.Engage();
                Run(r.View, r.View.TotalDuration + 0.1f, 1f / 60f);

                float a0 = Quaternion.Angle(home[0], r.Clamps[0].localRotation);
                AtLeast(a0, 20f, "클램프 0 회전");
                for (int i = 1; i < CustomsLockView.Chambers; i++)
                {
                    float a = Quaternion.Angle(home[i], r.Clamps[i].localRotation);
                    if (Mathf.Abs(a - a0) > 0.5f)
                        throw new Exception($"클램프 {i} 가 {a:F2}° 로 클램프 0 의 {a0:F2}° 와 다르다 " +
                                            "— 하나의 공통축이 아홉을 동시에 물린다는 전제가 깨진다");
                }
            }
            finally { r.Destroy(); }
        }

        private static void TestTabsMoveTogether()
        {
            Rig r = Build();
            try
            {
                var home = new Vector3[CustomsLockView.Banks];
                for (int i = 0; i < home.Length; i++) home[i] = r.Tabs[i].localPosition;

                r.View.Engage();
                Run(r.View, r.View.TotalDuration + 0.1f, 1f / 60f);

                float d0 = (r.Tabs[0].localPosition - home[0]).magnitude;
                AtLeast(d0 * 1000f, 20f, "탭 0 이동(mm)");
                for (int i = 1; i < CustomsLockView.Banks; i++)
                {
                    float d = (r.Tabs[i].localPosition - home[i]).magnitude;
                    if (Mathf.Abs(d - d0) > 0.0005f)
                        throw new Exception($"탭 {i} 가 {d * 1000f:F2}mm 로 탭 0 의 {d0 * 1000f:F2}mm 와 다르다");
                    // 아래로 가야 한다. 위로 가면 「풀렸다」로 읽힌다.
                    if (r.Tabs[i].localPosition.y >= home[i].y)
                        throw new Exception($"탭 {i} 가 아래로 내려가지 않았다");
                }
            }
            finally { r.Destroy(); }
        }

        private static void TestReleaseReturnsHome()
        {
            Rig r = Build();
            try
            {
                Vector3 rod0 = r.Rod.localPosition, tab0 = r.Tabs[0].localPosition;
                Quaternion sh0 = r.Shaft.localRotation, cl0 = r.Clamps[0].localRotation;

                r.View.Engage();
                Run(r.View, r.View.TotalDuration + 0.1f, 1f / 60f);
                r.View.Release();
                Run(r.View, r.View.TotalDuration + 0.5f, 1f / 60f);

                AtMost((r.Rod.localPosition - rod0).magnitude * 1000f, 0.5f, "복귀 후 로드 잔여(mm)");
                AtMost(Quaternion.Angle(sh0, r.Shaft.localRotation), 0.5f, "복귀 후 축 잔여(도)");
                AtMost((r.Tabs[0].localPosition - tab0).magnitude * 1000f, 0.5f, "복귀 후 탭 잔여(mm)");
                AtMost(Quaternion.Angle(cl0, r.Clamps[0].localRotation), 0.5f, "복귀 후 클램프 잔여(도)");
                if (r.View.IsEngaged) throw new Exception("풀었는데 체결 상태가 남아 있다");
            }
            finally { r.Destroy(); }
        }

        private static void TestPinBlocksWhenLocked()
        {
            Rig r = Build();
            try
            {
                // 기본은 해제 — 핀이 물러나 있다.
                Run(r.View, 0.6f, 1f / 60f);
                float open = r.Pin.localPosition.x;

                r.View.SetUnlocked(false);
                Run(r.View, 0.6f, 1f / 60f);
                float blocked = r.Pin.localPosition.x;

                // 잠기면 핀이 슬롯 쪽으로 **되돌아온다.**
                if (Mathf.Abs(open - blocked) < 0.02f)
                    throw new Exception($"잠금 전후로 핀이 {Mathf.Abs(open - blocked) * 1000f:F1}mm 밖에 " +
                                        "움직이지 않는다 — 「왜 레버가 안 내려가는가」가 화면에 없다");
                AtMost(r.View.PinRetraction, 0.01f, "잠긴 상태의 핀 후퇴량");

                r.View.SetUnlocked(true);
                Run(r.View, 0.6f, 1f / 60f);
                AtLeast(r.View.PinRetraction, 0.99f, "해제 후 핀 후퇴량");
            }
            finally { r.Destroy(); }
        }

        private static void TestIdleIsQuiet()
        {
            Rig r = Build();
            try
            {
                Run(r.View, 1.0f, 1f / 60f);          // 정착시킨다
                Vector3 rod = r.Rod.localPosition;
                Quaternion sh = r.Shaft.localRotation;

                for (int i = 0; i < 120; i++) r.View.Step(1f / 60f);

                if ((r.Rod.localPosition - rod).sqrMagnitude > 1e-12f)
                    throw new Exception("유휴 상태에서 로드가 계속 움직인다");
                if (Quaternion.Angle(sh, r.Shaft.localRotation) > 0.001f)
                    throw new Exception("유휴 상태에서 축이 계속 돈다");
            }
            finally { r.Destroy(); }
        }

        private static void TestFrameRateIndependent()
        {
            Rig a = Build(), b = Build();
            try
            {
                a.View.Engage(); Run(a.View, a.View.TotalDuration + 0.2f, 1f / 144f);
                b.View.Engage(); Run(b.View, b.View.TotalDuration + 0.2f, 1f / 30f);

                float da = Mathf.Abs(a.Rod.localPosition.y), db = Mathf.Abs(b.Rod.localPosition.y);
                if (Mathf.Abs(da - db) > 0.0005f)
                    throw new Exception($"144fps 와 30fps 의 최종 로드 위치가 다르다 ({da:F5} vs {db:F5})");
                if (Mathf.Abs(a.View.Engagement - b.View.Engagement) > 0.001f)
                    throw new Exception("프레임률에 따라 최종 체결 진행도가 다르다");
            }
            finally { a.Destroy(); b.Destroy(); }
        }

        /// <summary>
        /// 🔴 이 검사가 없어서 소유권 결함이 살아남았다.
        ///
        /// `LeverStateMachine` 의 **초기 상태가 `Idle`** 이고, 직전 판본의 자동 해제는
        /// 「지금 Idle 이면 푼다」는 수위 판정이었다. 그래서 레버가 배선된 채로
        /// 체결하면 **바로 다음 스텝에 스스로 풀렸다.** 씬에서는 `Engage()` 가
        /// `onLatched`(= 레버가 `Latched`)로만 불려 우연히 드러나지 않았다.
        /// </summary>
        private static void TestEngageSurvivesWiredLever()
        {
            Rig rig = BuildWithLever(out LeverStateMachine lever);
            try
            {
                if (lever.Current != LeverStateMachine.State.Idle)
                    throw new Exception($"전제가 깨졌다 — 레버 초기 상태가 Idle 이 아니라 {lever.Current} 다");

                rig.View.Engage();
                rig.View.Step(1f / 60f);
                if (!rig.View.IsEngaged)
                    throw new Exception("체결 직후 한 스텝 만에 스스로 풀렸다 — 자동 해제가 수위로 판정하고 있다");

                Run(rig.View, rig.View.TotalDuration + 0.2f, 1f / 60f);
                if (!rig.View.IsEngaged)
                    throw new Exception("체결이 유지되지 않는다");
                AtLeast(rig.View.Engagement, 0.99f, "체결 진행도");
            }
            finally { rig.Destroy(); }
        }

        /// <summary>
        /// 그리고 **자동 해제가 죽어 있지도 않아야 한다.** 위 검사만 있으면
        /// 「폴링을 통째로 지운다」가 통과해 버리고, 그러면 레버가 올라와도
        /// 아홉 챔버가 영영 물린 채로 남는다.
        /// </summary>
        private static void TestLeverReturnReleases()
        {
            Rig rig = BuildWithLever(out LeverStateMachine lever);
            try
            {
                lever.ForceState(LeverStateMachine.State.Latched);
                rig.View.Engage();
                Run(rig.View, rig.View.TotalDuration + 0.1f, 1f / 60f);
                if (!rig.View.IsEngaged) throw new Exception("걸린 상태에서 유지되지 않았다");

                lever.ForceState(LeverStateMachine.State.Resetting);
                rig.View.Step(1f / 60f);
                if (!rig.View.IsEngaged) throw new Exception("복귀 중인데 벌써 풀렸다");

                lever.ForceState(LeverStateMachine.State.Idle);
                rig.View.Step(1f / 60f);
                if (rig.View.IsEngaged)
                    throw new Exception("레버가 원위치로 돌아왔는데 풀리지 않았다 — 자동 해제가 죽었다");
            }
            finally { rig.Destroy(); }
        }

        /// <summary>
        /// 해제 복귀를 **전부** 검사한다. 직전 판본은 클램프 0번과 탭 0번만 봤고,
        /// 독립 감사가 「9개와 3개를 개별로 보는 불변식이 비어 있다」고 지적했다.
        /// 대표 하나만 보는 검사는 나머지 여덟이 어긋나도 통과한다.
        /// </summary>
        private static void TestReleaseReturnsEveryPart()
        {
            Rig rig = Build();
            try
            {
                var tabHome = new Vector3[CustomsLockView.Banks];
                var clampHome = new Quaternion[CustomsLockView.Chambers];
                for (int i = 0; i < CustomsLockView.Banks; i++) tabHome[i] = rig.Tabs[i].localPosition;
                for (int i = 0; i < CustomsLockView.Chambers; i++) clampHome[i] = rig.Clamps[i].localRotation;

                rig.View.Engage();
                Run(rig.View, rig.View.TotalDuration + 0.2f, 1f / 60f);
                rig.View.Release();
                Run(rig.View, rig.View.TotalDuration + 0.6f, 1f / 60f);

                for (int i = 0; i < CustomsLockView.Banks; i++)
                    if (Vector3.Distance(rig.Tabs[i].localPosition, tabHome[i]) > 0.0005f)
                        throw new Exception($"상태 탭 {i} 가 원위치로 안 돌아왔다 " +
                                            $"({Vector3.Distance(rig.Tabs[i].localPosition, tabHome[i]) * 1000f:F1} mm)");
                for (int i = 0; i < CustomsLockView.Chambers; i++)
                    if (Quaternion.Angle(rig.Clamps[i].localRotation, clampHome[i]) > 0.05f)
                        throw new Exception($"클램프 {i} 가 원위치로 안 돌아왔다 " +
                                            $"({Quaternion.Angle(rig.Clamps[i].localRotation, clampHome[i]):F2}°)");
            }
            finally { rig.Destroy(); }
        }

        // ── 단정 도구 ───────────────────────────────────────────────────────

        private static void AtLeast(float actual, float minimum, string what)
        {
            if (actual < minimum) throw new Exception($"{what}: {actual:F4} < 하한 {minimum:F4}");
        }

        private static void AtMost(float actual, float maximum, string what)
        {
            if (actual > maximum) throw new Exception($"{what}: {actual:F4} > 상한 {maximum:F4}");
        }

        private static void Run(string name, Action test, ref int passed, ref int failed, StringBuilder report)
        {
            try { test(); passed++; report.AppendLine($"  PASS  {name}"); }
            catch (Exception e) { failed++; report.AppendLine($"  FAIL  {name}\n        {e.Message}"); }
        }
    }
}
