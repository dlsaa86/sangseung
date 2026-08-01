using System;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.Build.Tests
{
    /// <summary>
    /// 월드 라벨 배치 검사 (`UP-FIX-12` · `UP-FIX-14` · `UP-FIX-21`).
    ///
    /// **Unity 오브젝트를 하나도 만들지 않는다** — 전부 `BuildLabelGeometry` 의 순수
    /// 함수라 헤드리스 러너에서 돈다. 카메라 좌표는 `TenFloorCaptureRig` 의 실제 시점을
    /// 옮겨 적은 것이고, 씬 좌표는 `Prototype_Elevator.unity` 를 파싱해 얻은 값이다.
    /// 값이 바뀌면 이 검사가 먼저 깨지는 것이 맞다.
    ///
    /// `BuildTests.RunAll` 이 접어 넣으므로 러너 등록(`Assets/Editor/*`)을 고치지 않는다.
    /// </summary>
    public static class BuildLabelPlacementTests
    {
        // ── 캡처 시점 (TenFloorCaptureRig.cs 의 Pose 목록 그대로) ──
        private static readonly (string name, Vector3 pos)[] CapturePoses =
        {
            ("Entry",         new Vector3( 0.12f, 1.62f,  1.30f)),
            ("EntryHeight",   new Vector3( 0.00f, 0.95f, -1.40f)),
            ("DeviceFront",   new Vector3( 0.35f, 1.62f,  0.00f)),
            ("DeviceSide",    new Vector3(-0.20f, 1.62f, -0.80f)),
            ("SymbolClose",   new Vector3(-0.05f, 1.60f,  0.00f)),
            ("CargoBay",      new Vector3( 0.65f, 2.35f,  1.42f)),
            ("Risk",          new Vector3( 0.60f, 1.62f, -0.70f)),
            ("Overharvest",   new Vector3( 0.10f, 1.62f, -0.95f)),
            ("BoardAndGauge", new Vector3( 0.95f, 1.62f,  0.69f)),
            ("Contract",      new Vector3(-0.60f, 1.58f, -0.15f)),
        };

        /// <summary>
        /// 씬에서 가장 깊이 묻힌 라벨. 6번째 적재 자리(`CarSlots[5]`)의 이름표다.
        /// 자리 자체가 `TubeFrame`(x -1.10..-0.80) 과 `ConsoleSlab`(x -1.19..-0.85) 안에 있다.
        /// </summary>
        private static readonly Vector3 BuriedLabelAnchor = new Vector3(-0.88f, 1.80f, -0.45f);

        /// <summary>`Tube_0` 의 프레임 상자 (씬 실측: pos (-0.95,1.60,-0.50) scale (0.30,1.30,0.34)).</summary>
        private static readonly Bounds TubeFrame0 =
            new Bounds(new Vector3(-0.95f, 1.60f, -0.50f), new Vector3(0.30f, 1.30f, 0.34f));

        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();

            Run("당김이 화면 좌표를 바꾸지 않는다", TestPushKeepsScreenPosition, ref passed, ref failed, report);
            Run("당김이 화면 크기 상한을 올리지 않는다", TestPushDoesNotRaiseSizeCap, ref passed, ref failed, report);
            Run("§5.1 이 못박은 크기 상수를 유지한다", TestSizeAxisUntouched, ref passed, ref failed, report);
            Run("당겨도 카메라 근접 한계를 지킨다", TestPushRespectsMinViewerDistance, ref passed, ref failed, report);
            Run("묻힌 이름표가 캡처 시점마다 통관 상자를 빠져나온다", TestBuriedLabelEscapesTube, ref passed, ref failed, report);
            Run("계약 시점만 예외이고 그 이유가 거리다", TestContractPoseIsTheKnownException, ref passed, ref failed, report);
            Run("화면 밖으로 삐져나간 사각형을 걸러낸다", TestEdgeRuleRejectsClippedRects, ref passed, ref failed, report);
            Run("겹침 비율이 내 면적 기준이다", TestOverlapFractionIsRelativeToMine, ref passed, ref failed, report);
            Run("위계 양보 문턱이 동급 양보보다 낮다", TestYieldThresholdsOrdered, ref passed, ref failed, report);
            Run("배킹판이 글자 뒤에 놓인다", TestPlateSitsBehindGlyphs, ref passed, ref failed, report);

            return (passed, failed, report.ToString());
        }

        // ── 검사 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 당김은 **시선 위**를 움직이므로 라벨의 화면 좌표가 변하지 않는다.
        /// 이것이 「이름표가 승객 머리 위에 붙어 있다」를 유지하면서 가림만 없애는 근거다.
        /// </summary>
        private static string TestPushKeepsScreenPosition()
        {
            foreach (var pose in CapturePoses)
            {
                Vector3 look = new Vector3(-0.20f, 1.40f, 0.00f);
                if (!Basis(pose.pos, look, out Vector3 r, out Vector3 u, out Vector3 f)) continue;

                Vector3 pushed = BuildLabelGeometry.PushTowardViewer(BuriedLabelAnchor, pose.pos);
                bool a = BuildLabelGeometry.Project(BuriedLabelAnchor, pose.pos, r, u, f,
                                                    0.5774f, 1.7778f, out Vector2 na, out _);
                bool b = BuildLabelGeometry.Project(pushed, pose.pos, r, u, f,
                                                    0.5774f, 1.7778f, out Vector2 nb, out _);
                if (!a || !b) continue;   // 화면 뒤 — 이 검사의 대상이 아니다
                if (Vector2.Distance(na, nb) > 0.0005f)
                    return $"{pose.name}: 당김 전 {na} → 후 {nb} (차이 {Vector2.Distance(na, nb):F5})";
            }
            return null;
        }

        /// <summary>
        /// 화면 크기 = 스케일 ÷ 거리. 당김은 거리를 줄이지만 축소도 함께 줄어들어
        /// **상한 1/1.8 을 넘지 않는다.** §5.1 의 「축소 자체는 유지한다」가 이 형태다.
        ///
        /// 당김이 없는 구간(카메라 코앞)은 예전 값과 **한 비트도 다르지 않아야** 한다 —
        /// 거기가 하한 0.35 가 걸리는 구간이고, 독립 평가 둘이 손대지 말라고 한 자리다.
        /// </summary>
        private static string TestPushDoesNotRaiseSizeCap()
        {
            float cap = 1f / BuildLabelGeometry.ReferenceDistance;
            for (float d = 0.05f; d <= 8f; d += 0.05f)
            {
                float size = BuildLabelGeometry.ApparentSize(d);
                float unpushed = BuildLabelGeometry.Shrink(d) / d;

                if (d < BuildLabelGeometry.MinViewerDistance)
                {
                    // 당기지 않는 구간 — 예전 규칙 그대로여야 한다.
                    if (Mathf.Abs(size - unpushed) > 1e-5f)
                        return $"거리 {d:F2}m 는 당기지 않는 구간인데 크기가 {unpushed:F4} → {size:F4} 로 변했다";
                    continue;
                }

                if (size > cap + 1e-4f)
                    return $"거리 {d:F2}m 에서 화면 크기 계수 {size:F4} > 상한 {cap:F4}";
            }
            return null;
        }

        private static string TestSizeAxisUntouched()
        {
            if (!Mathf.Approximately(BuildLabelGeometry.MinScale, 0.35f))
                return $"하한이 {BuildLabelGeometry.MinScale} — §5.1 은 0.35 를 더 낮추지 말라고 적었다";
            if (!Mathf.Approximately(BuildLabelGeometry.ReferenceDistance, 1.8f))
                return $"기준 거리가 {BuildLabelGeometry.ReferenceDistance} — 1.8 이어야 한다";
            if (!Mathf.Approximately(BuildLabelGeometry.Shrink(0.1f), 0.35f))
                return "코앞에서 하한이 걸리지 않는다";
            if (!Mathf.Approximately(BuildLabelGeometry.Shrink(3f), 1f))
                return "기준 거리 밖에서 크기를 손대고 있다";
            return null;
        }

        private static string TestPushRespectsMinViewerDistance()
        {
            for (float d = 0.05f; d <= 4f; d += 0.05f)
            {
                float push = BuildLabelGeometry.PushDistance(d);
                if (push < 0f) return $"거리 {d:F2}m 에서 음수 당김 {push}";
                if (push > BuildLabelGeometry.DepthPush + 1e-5f)
                    return $"거리 {d:F2}m 에서 상한 초과 당김 {push}";
                if (d - push < BuildLabelGeometry.MinViewerDistance - 1e-5f && d > BuildLabelGeometry.MinViewerDistance)
                    return $"거리 {d:F2}m 를 당겨 {d - push:F3}m — 근접 한계 {BuildLabelGeometry.MinViewerDistance} 침범";
            }
            return null;
        }

        /// <summary>
        /// **`UP-FIX-12` 의 핵심 주장을 숫자로 못박는다.**
        /// 6번 자리 이름표는 `Tube_0` 의 프레임 상자 **안**에 있다. 당김 뒤에는
        /// 어느 캡처 시점에서도 그 상자 밖이어야 한다 — 안에 있으면 상자의 앞면이
        /// 글자를 가로로 자른다(4차 판정 「글자 상·하반부가 분리」).
        /// </summary>
        private static string TestBuriedLabelEscapesTube()
        {
            if (!TubeFrame0.Contains(BuriedLabelAnchor))
                return "전제가 깨졌다 — 6번 자리 이름표가 더는 통관 상자 안에 있지 않다. 이 검사를 다시 쓸 것";

            foreach (var pose in CapturePoses)
            {
                float distance = Vector3.Distance(BuriedLabelAnchor, pose.pos);
                // 카메라가 라벨보다 가까이 있으면 당길 여유가 없다 — 아래 검사가 따로 다룬다.
                if (distance < BuildLabelGeometry.MinViewerDistance + 0.10f) continue;

                Vector3 placed = BuildLabelGeometry.PushTowardViewer(BuriedLabelAnchor, pose.pos);
                if (TubeFrame0.Contains(placed))
                    return $"{pose.name}: 당긴 뒤에도 통관 상자 안 {placed} (거리 {distance:F2}m)";
            }
            return null;
        }

        /// <summary>
        /// `Contract` 시점만 빠져나오지 못한다. 원인은 배치 규칙이 아니라 **그 시점이
        /// 승객에게서 0.47m 밖에 안 떨어져 있는 것**이다(`UP-FIX-09` 재설계 대상).
        /// 이 검사는 그 예외가 조용히 늘어나지 않도록 잠근다.
        /// </summary>
        private static string TestContractPoseIsTheKnownException()
        {
            int stuck = 0;
            string names = string.Empty;
            foreach (var pose in CapturePoses)
            {
                Vector3 placed = BuildLabelGeometry.PushTowardViewer(BuriedLabelAnchor, pose.pos);
                if (!TubeFrame0.Contains(placed)) continue;
                stuck++;
                names += (names.Length > 0 ? ", " : string.Empty) + pose.name;
            }
            if (stuck != 1 || names != "Contract")
                return $"통관 상자에 남는 시점이 {stuck}개({names}) — 기대는 Contract 하나";
            return null;
        }

        private static string TestEdgeRuleRejectsClippedRects()
        {
            Rect inside = Rect.MinMaxRect(-0.6f, -0.2f, -0.3f, 0.1f);
            if (!BuildLabelGeometry.InsideFrame(inside)) return "화면 안 사각형을 걸러냈다";

            // `07_cargo_full` 에서 4·5번 자리 이름표가 실제로 이 모양이다(윗변이 1.0 을 넘는다).
            Rect overTop = Rect.MinMaxRect(-0.2f, 0.88f, 0.1f, 1.04f);
            if (BuildLabelGeometry.InsideFrame(overTop)) return "윗변이 프레임을 넘는데 통과시켰다";

            Rect overLeft = Rect.MinMaxRect(-1.02f, -0.1f, -0.7f, 0.1f);
            if (BuildLabelGeometry.InsideFrame(overLeft)) return "좌변이 프레임을 넘는데 통과시켰다";
            return null;
        }

        private static string TestOverlapFractionIsRelativeToMine()
        {
            Rect mine = Rect.MinMaxRect(0f, 0f, 1f, 1f);
            Rect quarter = Rect.MinMaxRect(0.5f, 0.5f, 1.5f, 1.5f);
            float f = BuildLabelGeometry.OverlapFractionOf(mine, quarter);
            if (Mathf.Abs(f - 0.25f) > 1e-4f) return $"1/4 겹침을 {f:F3} 로 쟀다";

            Rect tiny = Rect.MinMaxRect(0.9f, 0.9f, 1.4f, 1.4f);
            float g = BuildLabelGeometry.OverlapFractionOf(mine, tiny);
            if (Mathf.Abs(g - 0.01f) > 1e-4f) return $"1% 겹침을 {g:F4} 로 쟀다";
            // 스치는 정도로 라벨이 사라지면 안 된다.
            if (g > BuildLabelGeometry.YieldToHigherRank)
                return "1% 스침에도 라벨이 물러선다 — 문턱이 너무 낮다";
            // 반대편 기준은 상대 면적으로 잰다 — 얇은 벽 기록문 한 줄을 지키는 축이다.
            float back = BuildLabelGeometry.OverlapFractionOf(tiny, mine);
            if (Mathf.Abs(back - 0.04f) > 1e-4f) return $"상대 기준 겹침을 {back:F4} 로 쟀다";

            Rect none = Rect.MinMaxRect(2f, 2f, 3f, 3f);
            if (BuildLabelGeometry.OverlapFractionOf(mine, none) != 0f) return "안 겹치는데 겹쳤다고 쟀다";
            return null;
        }

        private static string TestYieldThresholdsOrdered()
        {
            if (!(BuildLabelGeometry.YieldToHigherRank < BuildLabelGeometry.YieldToNearerPeer))
                return "위계 양보가 동급 양보보다 관대하다 — 5순위가 2순위를 덮는다";
            if (BuildLabelGeometry.YieldToHigherRank <= 0f)
                return "위계 양보 문턱이 0 이하 — 스치기만 해도 사라진다";
            if (BuildLabelGeometry.YieldToNearerPeer >= 1f)
                return "동급 양보 문턱이 1 이상 — 완전히 덮여도 남는다";
            if (BuildLabelGeometry.CoverHigherRank <= 0f || BuildLabelGeometry.CoverHigherRank >= 1f)
                return $"상대 면적 기준이 {BuildLabelGeometry.CoverHigherRank} — 0..1 밖이다";
            if (BuildLabelGeometry.MinObstacleArea <= 0f || BuildLabelGeometry.MinObstacleArea > 0.01f)
                return $"측정 하한 면적이 {BuildLabelGeometry.MinObstacleArea} — 화면의 0.25% 를 넘으면 큰 것도 무시한다";
            return null;
        }

        private static string TestPlateSitsBehindGlyphs()
        {
            // TMP 는 로컬 -Z 쪽에서 읽힌다(회전이 +Z 를 카메라 반대로 돌린다).
            // 판이 +Z 로 물러나 있어야 글자를 덮지 않는다.
            if (BuildLabelPlate.BackOffset <= 0f)
                return $"판 오프셋 {BuildLabelPlate.BackOffset} — 글자 앞에 선다";
            if (BuildLabelPlate.PadX <= 0f || BuildLabelPlate.PadY <= 0f)
                return "판 여백이 0 이하 — 글자 획이 판 밖으로 나간다";
            if (BuildLabelPlate.MinHalf <= 0f)
                return "판 최소 크기가 0 이하";
            return null;
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────

        private static bool Basis(Vector3 pos, Vector3 look,
                                  out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            right = Vector3.right; up = Vector3.up; forward = Vector3.forward;
            Vector3 delta = look - pos;
            if (delta.sqrMagnitude < 1e-6f) return false;
            Quaternion q = Quaternion.LookRotation(delta.normalized, Vector3.up);
            right = q * Vector3.right;
            up = q * Vector3.up;
            forward = q * Vector3.forward;
            return true;
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
    }
}
