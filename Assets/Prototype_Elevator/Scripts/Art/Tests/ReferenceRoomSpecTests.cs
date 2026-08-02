using System;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.Art.Tests
{
    /// <summary>
    /// <see cref="ReferenceRoomSpec"/> 가 사용자 명세를 위반하지 않는지 검사한다.
    ///
    /// ## 왜 「상수 파일」에 테스트를 붙이는가
    ///
    /// 상수는 컴파일되므로 틀려도 아무도 못 잡는다. 그리고 이 저장소가 그레이박스
    /// 단계에서 반복해서 당한 실패가 정확히 그 형태다 — 수치가 조용히 어긋나고,
    /// 조립기를 돌려 캡처를 뽑은 다음에야 「뭔가 이상하다」로 나타나며, 그때는 어느
    /// 숫자가 원인인지 역추적할 수 없다(`GRAPHICS_TARGET.md` §5.5 「귀속 실패」).
    ///
    /// 여기서 재는 것은 **파생값이 명세의 제약을 만족하는가**다. 예컨대 「중앙 이동
    /// 공간 2.2 × 2.8」은 어느 상수에도 적혀 있지 않다 — 벽 위치·가위문 돌출·선반
    /// 깊이·장치 돌출 **넷의 결과**다. 넷 중 하나만 손대도 깨질 수 있고, 깨지면
    /// 플레이어가 낀다.
    ///
    /// ## 이 스위트가 잡는 회귀의 구체적 예
    ///
    /// | 누군가 이렇게 바꾸면 | 어느 검사가 잡나 |
    /// |---|---|
    /// | 선반을 깊게 (0.58 → 0.7) | 돌출 한계 · 중앙 공간 X |
    /// | 창 간격을 가로만 넓힘 | 간격 동일 · 프레임 폭 (§13 슬롯머신 회귀) |
    /// | 장치를 키움 (2.1 → 2.4) | 벽 점유율 · 레버 컬럼이 우벽 초과 |
    /// | 카메라 FOV 를 60(수직)으로 되돌림 | 수평 FOV 범위 |
    /// | 천장을 다시 5.5m 로 | 실내 높이 |
    /// </summary>
    public static class ReferenceRoomSpecTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("§1 실내 치수가 명세와 같다 (4.0 × 4.6 × 2.9)", TestInteriorDimensions, ref passed, ref failed, report);
            Run("§1 중앙 이동 공간이 2.2 × 2.8 이상이다", TestClearSpace, ref passed, ref failed, report);
            Run("§1 최소 통행 폭 0.9m 를 만족한다", TestWalkway, ref passed, ref failed, report);
            Run("§2 수평 시야각이 72~78도다", TestHorizontalFov, ref passed, ref failed, report);
            Run("§2 기준 카메라가 방 안에 있고 후면 벽까지 4.0m 다", TestCameraPlacement, ref passed, ref failed, report);
            Run("§4 장치가 벽 폭 45~52% · 벽 높이 60~68% 안에 있다", TestMachineCoverage, ref passed, ref failed, report);
            Run("§4 3×3 격자가 장치 프레임 안에 들어간다", TestWindowGridFits, ref passed, ref failed, report);
            Run("§4 관찰창 프레임 폭이 80~100mm 이고 가로·세로가 같다", TestWindowGaps, ref passed, ref failed, report);
            Run("§4·§13 창 간격이 등방이라 열로 뭉치지 않는다", TestNoReelBanding, ref passed, ref failed, report);
            Run("§5·§6 레버와 전력 표시기가 우벽 안에 들어간다", TestRightStackFits, ref passed, ref failed, report);
            Run("§5 레버 회전축이 손이 닿는 높이다", TestLeverPivotReachable, ref passed, ref failed, report);
            Run("§7 선반 돌출이 0.6m 이하다", TestShelfProtrusion, ref passed, ref failed, report);
            Run("§14 관찰창 실루엣이 12~16각형이다", TestRetroSilhouette, ref passed, ref failed, report);
            Run("§14 텍셀 밀도가 128~256px/m 범위다", TestTexelDensity, ref passed, ref failed, report);
            Run("모듈 5층 구조의 깊이가 실제로 겹쳐 쌓인다", TestModuleLayerDepth, ref passed, ref failed, report);
            Run("레버 기구 치수가 한 손 조작 범위다", TestLeverMechanism, ref passed, ref failed, report);
            Run("Violations() 가 빈 배열이다 (자기 보고와 검사가 일치한다)", TestNoSelfReportedViolations, ref passed, ref failed, report);

            return (passed, failed, report.ToString());
        }

        // ── 검사 ────────────────────────────────────────────────────────────

        private static void TestInteriorDimensions()
        {
            Approx(ReferenceRoomSpec.InteriorWidth, 4.0f, "실내 폭");
            Approx(ReferenceRoomSpec.InteriorDepth, 4.6f, "실내 깊이");
            Approx(ReferenceRoomSpec.InteriorHeight, 2.9f, "실내 높이");
            Approx(ReferenceRoomSpec.EyeHeight, 1.62f, "눈높이");

            // 벽 안쪽면이 치수와 정합적인가. 상수를 손으로 적으면 여기서 갈린다.
            Approx(ReferenceRoomSpec.WallRightX - ReferenceRoomSpec.WallLeftX,
                   ReferenceRoomSpec.InteriorWidth, "좌우 벽 간격");
            Approx(ReferenceRoomSpec.WallRearZ - ReferenceRoomSpec.WallFrontZ,
                   ReferenceRoomSpec.InteriorDepth, "앞뒤 벽 간격");
        }

        private static void TestClearSpace()
        {
            // 이 값은 어느 상수에도 없다 — 넷의 결과다.
            AtLeast(ReferenceRoomSpec.ClearSpanX, ReferenceRoomSpec.RequiredClearX, "중앙 이동 공간 X");
            AtLeast(ReferenceRoomSpec.ClearSpanZ, ReferenceRoomSpec.RequiredClearZ, "중앙 이동 공간 Z");
        }

        private static void TestWalkway()
        {
            // 가위문 앞과 선반 앞 사이, 그리고 장치 앞과 앞벽 사이.
            AtLeast(ReferenceRoomSpec.ClearSpanX, ReferenceRoomSpec.MinWalkway, "좌우 통행 폭");
            AtLeast(ReferenceRoomSpec.ClearSpanZ, ReferenceRoomSpec.MinWalkway, "앞뒤 통행 폭");

            // 선반 앞을 지나가는 통로. 선반 안쪽면에서 가위문 돌출까지.
            float pastShelf = ReferenceRoomSpec.ShelfInnerX - (ReferenceRoomSpec.WallLeftX + ReferenceRoomSpec.GateProtrusion);
            AtLeast(pastShelf, ReferenceRoomSpec.PlayerWidth + 0.3f, "선반 앞 통행 여유");
        }

        private static void TestHorizontalFov()
        {
            Between(ReferenceRoomSpec.HorizontalFovDegrees, 72f, 78f, "수평 시야각");

            // 왕복이 성립하는가. Unity 에 넣는 것은 수직값이므로 이 변환이 틀리면
            // 「명세대로 적었는데 화면은 어안」이 된다 — 직전 씬이 정확히 그 상태였다.
            float v = ReferenceRoomSpec.VerticalFov;
            float backToH = 2f * Mathf.Rad2Deg * Mathf.Atan(
                Mathf.Tan(v * 0.5f * Mathf.Deg2Rad) * ReferenceRoomSpec.ReferenceAspect);
            Approx(backToH, ReferenceRoomSpec.HorizontalFovDegrees, "수직→수평 역변환", 0.01f);

            // 직전 씬의 값(수직 60)이 명세를 벗어난다는 것을 명시적으로 고정한다.
            float legacyH = 2f * Mathf.Rad2Deg * Mathf.Atan(
                Mathf.Tan(60f * 0.5f * Mathf.Deg2Rad) * ReferenceRoomSpec.ReferenceAspect);
            if (legacyH <= 78f)
                throw new Exception($"직전 수직 FOV 60 의 수평 환산이 {legacyH:F1}° 인데 명세 상한 78° 이하다 " +
                                    "— 이 테스트의 전제가 틀렸다");
        }

        private static void TestCameraPlacement()
        {
            Approx(ReferenceRoomSpec.WallRearZ - ReferenceRoomSpec.CameraZ,
                   ReferenceRoomSpec.CameraToRearWall, "카메라~후면 벽 거리");
            AtLeast(ReferenceRoomSpec.CameraZ - ReferenceRoomSpec.WallFrontZ, 0.3f, "카메라~앞벽 여유");

            // 눈높이에서 천장까지 여유. 직전 씬은 눈높이가 두 번 더해져 천장 위로 갔다.
            AtLeast(ReferenceRoomSpec.InteriorHeight - ReferenceRoomSpec.EyeHeight, 0.9f, "머리 위 여유");
        }

        private static void TestMachineCoverage()
        {
            // 영웅 오브젝트 명세(2026-08-02) — 「후면 벽 폭의 45~52%, 벽 높이의 60~68%」.
            // 직전 값 2.1 × 2.2 는 폭 52.5% · **높이 75.9%** 로 높이가 크게 초과였고,
            // 그래서 장치가 「벽에 걸린 기계」가 아니라 「벽 그 자체」로 읽혔다.
            Between(ReferenceRoomSpec.MachineWallCoverage, 0.45f, 0.52f, "장치의 벽 폭 점유율");
            Between(ReferenceRoomSpec.MachineHeight / ReferenceRoomSpec.InteriorHeight,
                    0.60f, 0.68f, "장치의 벽 높이 점유율");
            AtLeast(ReferenceRoomSpec.MachineCeilingGap, 0.30f, "장치 상단 여백");
            Approx(ReferenceRoomSpec.MachineBottomGap, 0.2f, "장치 하단 간격");

            // 1인칭 기본 시점에서 9개 창이 한눈에 들어와야 한다 —
            // 「고개를 크게 움직이지 않고」. 격자 세로 폭이 수직 화각 안이어야 한다.
            float gridSpan = 2f * ReferenceRoomSpec.WindowPitch + ReferenceRoomSpec.WindowRingDiameter;
            float subtend = 2f * Mathf.Rad2Deg * Mathf.Atan(gridSpan * 0.5f / ReferenceRoomSpec.CameraToRearWall);
            AtMost(subtend, ReferenceRoomSpec.VerticalFov * 0.75f, "격자가 차지하는 세로 화각");
        }

        private static void TestWindowGridFits()
        {
            float r = ReferenceRoomSpec.WindowRingDiameter * 0.5f;
            Vector2 topLeft = ReferenceRoomSpec.WindowCenter(0, 0);
            Vector2 bottomRight = ReferenceRoomSpec.WindowCenter(2, 2);

            AtLeast(topLeft.x - r, ReferenceRoomSpec.MachineLeftX + ReferenceRoomSpec.MachineFrameBand, "격자 좌측");
            AtMost(bottomRight.x + r, ReferenceRoomSpec.MachineRightX - ReferenceRoomSpec.MachineFrameBand, "격자 우측");
            AtMost(topLeft.y + r, ReferenceRoomSpec.MachineTopY - ReferenceRoomSpec.MachineFrameBand, "격자 상단");

            float servicePanelTop = ReferenceRoomSpec.ServicePanelCenterY + ReferenceRoomSpec.ServicePanelHeight * 0.5f;
            AtLeast(bottomRight.y - r, servicePanelTop, "격자 하단이 정비 패널 위");

            // 행 0 이 위여야 한다. 뒤집히면 `SpinBoard.Index` 규약과 어긋나 결과가
            // 위아래로 뒤집혀 표시되고, 그건 판정이 아니라 표시만 틀려서 안 잡힌다.
            if (ReferenceRoomSpec.WindowCenter(1, 0).y <= ReferenceRoomSpec.WindowCenter(1, 2).y)
                throw new Exception("행 0 이 행 2 보다 아래다 — 결과판이 상하 반전된다");
            if (ReferenceRoomSpec.WindowCenter(0, 1).x >= ReferenceRoomSpec.WindowCenter(2, 1).x)
                throw new Exception("열 0 이 열 2 보다 오른쪽이다 — 결과판이 좌우 반전된다");
        }

        private static void TestWindowGaps()
        {
            Between(ReferenceRoomSpec.WindowGapX, 0.08f, 0.10f, "가로 프레임 폭");
            Between(ReferenceRoomSpec.WindowGapY, 0.08f, 0.10f, "세로 프레임 폭");
            Between(ReferenceRoomSpec.WindowGlassDiameter, 0.24f, 0.32f, "내부 유리 지름");

            // 링 지름은 명세의 460mm 가 아니라 **435mm** 다. 저장소의 베젤 판독 상한
            // (개구부 지름의 18%)이 명세의 65mm 링을 허용하지 않기 때문이고, 그 상한은
            // 16라운드 「베젤이 심볼을 덮는다」 판정에서 나온 것이라 명세보다 우선한다.
            // **명세를 어긴 사실 자체를 여기에 고정한다** — 조용히 어기면 다음 사람이
            // 「명세대로 460 이겠지」로 읽고 다시 늘린다.
            // 링 지름은 유리 + 링 두께 ×2 에서 유도된다. 영웅 오브젝트 명세로
            // 장치가 작아지면서 함께 줄었다 — 격자가 장치 프레임 안에 들어가야 한다.
            Approx(ReferenceRoomSpec.WindowRingDiameter,
                   ReferenceRoomSpec.WindowGlassDiameter + ReferenceRoomSpec.WindowRingBand * 2f,
                   "외부 링 지름이 유리+두께에서 유도된다");

            // 베젤이 판독 상한 안에 있는가. 여기가 이 절의 존재 이유다.
            float bezelRatio = ReferenceRoomSpec.WindowRingBand / ReferenceRoomSpec.WindowGlassDiameter;
            AtMost(bezelRatio, PortholeMesh.MaxBezelToOpeningDiameter, "베젤 폭 ÷ 개구부 지름");

            // 유리가 링 안에 들어가는가. 링 두께를 양쪽에서 뺀 값보다 커지면 유리가 링을 뚫는다.
            float innerBore = ReferenceRoomSpec.WindowRingDiameter - ReferenceRoomSpec.WindowRingBand * 2f;
            AtMost(ReferenceRoomSpec.WindowGlassDiameter, innerBore, "유리가 링 안쪽 지름 이하");
        }

        private static void TestNoReelBanding()
        {
            // 명세 §4 「간격은 동일」 + §13 「슬롯머신처럼 보이는 장식 금지」.
            Approx(ReferenceRoomSpec.WindowPitchX, ReferenceRoomSpec.WindowPitchY, "가로·세로 간격 동일");

            // 등방이면 가로 이웃 거리와 세로 이웃 거리의 비가 1 이다. 이 비가 1.5 를
            // 넘으면 사람 눈에 열(또는 행)로 뭉친다 — 이 저장소의 `G-SLOT` 축이
            // 실제로 「장축/단축 비 1.51~2.67」에서 띠를 관측했다.
            float ratio = Mathf.Max(ReferenceRoomSpec.WindowPitchX, ReferenceRoomSpec.WindowPitchY)
                        / Mathf.Max(0.0001f, Mathf.Min(ReferenceRoomSpec.WindowPitchX, ReferenceRoomSpec.WindowPitchY));
            AtMost(ratio, 1.2f, "간격 이방비");
        }

        private static void TestRightStackFits()
        {
            float leverRight = ReferenceRoomSpec.LeverColumnCenterX + ReferenceRoomSpec.LeverColumnWidth * 0.5f;
            AtMost(leverRight, ReferenceRoomSpec.WallRightX, "레버 컬럼 우측 끝");

            float meterLeft = ReferenceRoomSpec.PowerMeterCenterX - ReferenceRoomSpec.PowerMeterWidth * 0.5f;
            float meterRight = ReferenceRoomSpec.PowerMeterCenterX + ReferenceRoomSpec.PowerMeterWidth * 0.5f;
            AtMost(meterRight, ReferenceRoomSpec.WallRightX, "전력 표시기 우측 끝");
            AtLeast(meterLeft, leverRight - 0.001f, "전력 표시기가 레버와 겹치지 않는다");

            // 장치와 레버 사이 간격이 명세값 그대로인가.
            Approx(ReferenceRoomSpec.LeverColumnCenterX - ReferenceRoomSpec.LeverColumnWidth * 0.5f
                   - ReferenceRoomSpec.MachineRightX,
                   ReferenceRoomSpec.LeverGapFromMachine, "장치~레버 간격");
        }

        private static void TestLeverPivotReachable()
        {
            // 회전축이 컬럼 안에 있어야 한다. 밖이면 손잡이가 허공에서 돈다.
            AtLeast(ReferenceRoomSpec.LeverPivotY, ReferenceRoomSpec.LeverColumnBottomY, "회전축이 컬럼 하단 위");
            AtMost(ReferenceRoomSpec.LeverPivotY, ReferenceRoomSpec.LeverColumnTopY, "회전축이 컬럼 상단 아래");

            // 눈높이 1.62 에서 손이 닿는 범위. 어깨~허리 사이여야 한다.
            Between(ReferenceRoomSpec.LeverPivotY, 0.9f, 1.5f, "회전축 높이");

            // 그립이 한 손에 잡히는가. 명세 §5 「한 손으로 잡을 수 있는 크기」.
            AtMost(ReferenceRoomSpec.LeverGripDiameter, 0.06f, "그립 지름");
            AtMost(ReferenceRoomSpec.LeverGripLength, ReferenceRoomSpec.LeverHandleLength, "그립이 손잡이보다 짧다");
            Between(ReferenceRoomSpec.LeverSwingDegrees, 45f, 65f, "가동 범위");
        }

        private static void TestShelfProtrusion()
        {
            AtMost(ReferenceRoomSpec.ShelfProtrusion, ReferenceRoomSpec.ShelfMaxProtrusion, "선반 돌출");
            AtLeast(ReferenceRoomSpec.ShelfLegCount, 4, "수직 지지대 수");

            // 선반이 후면 장치나 앞벽을 침범하지 않는가.
            float shelfFront = ReferenceRoomSpec.ShelfCenterZ - ReferenceRoomSpec.ShelfLength * 0.5f;
            float shelfBack = ReferenceRoomSpec.ShelfCenterZ + ReferenceRoomSpec.ShelfLength * 0.5f;
            AtLeast(shelfFront, ReferenceRoomSpec.WallFrontZ, "선반 앞끝이 앞벽 안");
            AtMost(shelfBack, ReferenceRoomSpec.WallRearZ, "선반 뒤끝이 후면 벽 안");

            // 상판이 하단 선반보다 위에 있고, 그 사이에 물건이 들어갈 높이가 있는가.
            AtLeast(ReferenceRoomSpec.ShelfTopHeight - ReferenceRoomSpec.ShelfLowerHeight, 0.4f, "선반 단 사이 높이");
        }

        private static void TestRetroSilhouette()
        {
            Between(ReferenceRoomSpec.WindowSilhouetteSides, 12, 16, "관찰창 분할 수");

            // 4의 배수가 아니면 `PortholeMesh.Clamped()` 가 조용히 깎는다 —
            // 그러면 상수와 실제 형상이 달라지고, 그 차이는 캡처에서만 드러난다.
            if (ReferenceRoomSpec.WindowSilhouetteSides % 4 != 0)
                throw new Exception($"관찰창 분할 {ReferenceRoomSpec.WindowSilhouetteSides} 가 4의 배수가 아니다 " +
                                    "— PortholeMesh 가 클램프해서 상수와 형상이 어긋난다");

            // 명세 §14 「레버 손잡이도 지나치게 매끄러운 원통으로 만들지 않음」.
            AtMost(ReferenceRoomSpec.LeverGripSides, 10, "그립 분할 수");
            AtMost(ReferenceRoomSpec.WarningLampSides, 16, "경고등 분할 수");
        }

        private static void TestTexelDensity()
        {
            Between(ReferenceRoomSpec.SurfaceTexelsPerMeter, 128, 256, "벽·바닥 텍셀 밀도");
            AtLeast(ReferenceRoomSpec.HeroTexelsPerMeter, ReferenceRoomSpec.SurfaceTexelsPerMeter,
                    "주요 오브젝트 텍셀 밀도가 표면보다 높다");

            // 「미터당 텍셀」과 「미터당 반복」의 변환이 왕복하는가.
            // 이 저장소는 타일링 단위 혼동으로 세 번 실패했다 — 그래서 산술을 고정한다.
            Approx(ReferenceRoomSpec.SurfaceUvPerMeter * ReferenceRoomSpec.SurfaceTextureSize,
                   ReferenceRoomSpec.SurfaceTexelsPerMeter, "표면 반복→텍셀 왕복");
            Approx(ReferenceRoomSpec.HeroUvPerMeter * ReferenceRoomSpec.SurfaceTextureSize,
                   ReferenceRoomSpec.HeroTexelsPerMeter, "주요 반복→텍셀 왕복");

            // 반복 수가 1 을 크게 넘으면 한 면 안에서 패턴이 여러 번 보여 이음매가 드러난다.
            AtMost(ReferenceRoomSpec.SurfaceUvPerMeter, 1.5f, "표면 미터당 반복 수");
        }

        /// <summary>
        /// 「측면에서 5개 층이 구분된다」를 **깊이 축에서** 검사한다.
        /// 층이 이름만 있고 같은 z 에 겹쳐 있으면 화면에서는 한 장이다.
        /// </summary>
        private static void TestModuleLayerDepth()
        {
            Between(ReferenceRoomSpec.WindowProtrusion, 0.14f, 0.18f, "모듈 돌출 깊이");
            Between(ReferenceRoomSpec.WindowGlassInset, 0.06f, 0.10f, "유리가 들어간 깊이");

            // ① 장착판 → ③ 칼라 앞면까지가 돌출 전체다. 그 사이에 ② 브래킷이 산다.
            AtLeast(ReferenceRoomSpec.WindowProtrusion,
                    ReferenceRoomSpec.WindowBackPlateThickness + ReferenceRoomSpec.WindowBracketThickness + 0.05f,
                    "장착판+브래킷이 돌출 안에 들어간다");

            // ④ 유리는 칼라 앞면보다 뒤에 있어야 「들어간 창」이다.
            AtMost(ReferenceRoomSpec.WindowGlassInset, ReferenceRoomSpec.WindowProtrusion,
                   "유리가 칼라 앞면보다 뒤에 있다");

            // ⑤ 영혼은 유리에서 떨어져 있어야 패럴랙스가 생긴다.
            //    0 이면 유리에 붙은 스티커로 보인다 — 그것이 직전 판본의 최우선 결함이었다.
            AtLeast(ReferenceRoomSpec.SoulStandoff, 0.03f, "영혼이 유리에서 떨어진 거리");
            AtMost(ReferenceRoomSpec.SoulStandoff, ReferenceRoomSpec.WindowChamberDepth,
                   "영혼이 챔버 안에 있다");

            AtLeast(ReferenceRoomSpec.WindowBracketCount, 3, "지지 브래킷 수");
            Between(ReferenceRoomSpec.WindowBoltCount, 6, 8, "체결 볼트 수");
        }

        /// <summary>
        /// 레버가 **한 손 조작 크기**이고 기구가 성립하는가.
        /// 영웅 오브젝트 명세의 권장 기준을 그대로 옮긴다.
        /// </summary>
        private static void TestLeverMechanism()
        {
            Between(ReferenceRoomSpec.LeverPivotY, 1.15f, 1.30f, "회전축 높이");
            Between(ReferenceRoomSpec.LeverHandleLength, 0.32f, 0.42f, "레버 암 길이");
            Between(ReferenceRoomSpec.LeverGripLength, 0.14f, 0.20f, "그립 길이");
            Between(ReferenceRoomSpec.LeverGripDiameter, 0.035f, 0.045f, "그립 지름");
            Between(ReferenceRoomSpec.LeverSwingDegrees, 45f, 60f, "전체 이동 각도");
            Between(ReferenceRoomSpec.LeverLockedTravelDegrees, 2f, 4f, "잠김 상태 허용 각도");

            // 잠금핀이 실제로 경로를 막으려면 허용 각도가 전체 가동각보다 훨씬 작아야 한다.
            if (ReferenceRoomSpec.LeverLockedTravelDegrees > ReferenceRoomSpec.LeverSwingDegrees * 0.2f)
                throw new Exception("잠김 허용 각도가 너무 커서 「막혔다」로 안 읽힌다");

            // 그립이 암 안에 들어간다.
            AtMost(ReferenceRoomSpec.LeverGripLength, ReferenceRoomSpec.LeverHandleLength * 0.6f,
                   "그립이 암의 60% 이하");

            // 손잡이 중심 높이 = 회전축 + 암이 그리는 호의 중간. 요구 1.25~1.40m.
            float handleCenterY = ReferenceRoomSpec.LeverPivotY
                + ReferenceRoomSpec.LeverHandleLength * 0.5f
                  * Mathf.Sin(ReferenceRoomSpec.LeverSwingDegrees * 0.5f * Mathf.Deg2Rad);
            Between(handleCenterY, 1.25f, 1.40f, "손잡이 중심 높이");
        }

        private static void TestNoSelfReportedViolations()
        {
            string[] v = ReferenceRoomSpec.Violations();
            if (v.Length > 0)
                throw new Exception("명세 위반 " + v.Length + "건 — " + string.Join(" / ", v));
        }

        // ── 단정 도구 ───────────────────────────────────────────────────────

        private static void Approx(float actual, float expected, string what, float tolerance = 0.001f)
        {
            if (Mathf.Abs(actual - expected) > tolerance)
                throw new Exception($"{what}: {actual:F4} ≠ {expected:F4} (허용 {tolerance})");
        }

        private static void AtLeast(float actual, float minimum, string what)
        {
            if (actual < minimum - 0.0005f)
                throw new Exception($"{what}: {actual:F4} < 하한 {minimum:F4}");
        }

        private static void AtMost(float actual, float maximum, string what)
        {
            if (actual > maximum + 0.0005f)
                throw new Exception($"{what}: {actual:F4} > 상한 {maximum:F4}");
        }

        private static void Between(float actual, float lo, float hi, string what)
        {
            if (actual < lo - 0.0005f || actual > hi + 0.0005f)
                throw new Exception($"{what}: {actual:F4} 가 [{lo:F4}, {hi:F4}] 밖이다");
        }

        private static void Run(string name, Action test, ref int passed, ref int failed, StringBuilder report)
        {
            try
            {
                test();
                passed++;
                report.AppendLine($"  PASS  {name}");
            }
            catch (Exception e)
            {
                failed++;
                report.AppendLine($"  FAIL  {name}\n        {e.Message}");
            }
        }
    }
}
