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
            Run("§4 프레임 3단 위계 — 외곽 > 뱅크 리브 > 격벽", TestFrameHierarchy, ref passed, ref failed, report);
            Run("§4 압력창이 도어 안에 들어가고 베젤이 판독 상한 안이다", TestWindowGaps, ref passed, ref failed, report);
            Run("§4·§13 창 간격 이방비가 1.2 이하라 열로 뭉치지 않는다", TestNoReelBanding, ref passed, ref failed, report);
            Run("§4b 공통 잠금 기구가 성립한다 (축·캠 로드·상태 탭)", TestCommonLockMechanism, ref passed, ref failed, report);
            Run("§5·§6 레버와 전력 표시기가 우벽 안에 들어간다", TestRightStackFits, ref passed, ref failed, report);
            Run("§5 레버 회전축이 손이 닿는 높이다", TestLeverPivotReachable, ref passed, ref failed, report);
            Run("§7 선반 돌출이 0.6m 이하다", TestShelfProtrusion, ref passed, ref failed, report);
            Run("§14 관찰창 실루엣이 12~16각형이다", TestRetroSilhouette, ref passed, ref failed, report);
            Run("§14 텍셀 밀도가 128~256px/m 범위다", TestTexelDensity, ref passed, ref failed, report);
            Run("창 4층 구조의 깊이가 실제로 갈라져 쌓인다", TestModuleLayerDepth, ref passed, ref failed, report);
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

            // 「플레이어 기본 시점에서 9개 창과 레버가 함께 보임」 — 지시의 합격 기준.
            // 세로는 챔버 적층 전체, 가로는 캐비닛 좌측 끝부터 **레버 컬럼 우측 끝**까지다.
            // 직전 판본은 세로만 쟀고, 그래서 레버가 화각 밖으로 나가도 통과했다.
            float vSpan = ReferenceRoomSpec.ChamberStackHeight;
            float vSubtend = 2f * Mathf.Rad2Deg * Mathf.Atan(vSpan * 0.5f / ReferenceRoomSpec.CameraToRearWall);
            AtMost(vSubtend, ReferenceRoomSpec.VerticalFov * 0.75f, "챔버 적층이 차지하는 세로 화각");

            // 카메라는 x=0 에 선다. 장치 좌단(−1.274)과 레버 우단(+0.914) 중
            // 먼 쪽이 수평 반화각 안에 들어와야 둘이 한 화면에 있다.
            float leverRight = ReferenceRoomSpec.LeverColumnCenterX + ReferenceRoomSpec.LeverColumnWidth * 0.5f;
            float halfSpan = Mathf.Max(Mathf.Abs(ReferenceRoomSpec.MachineLeftX), Mathf.Abs(leverRight));
            float hSubtend = 2f * Mathf.Rad2Deg * Mathf.Atan(halfSpan / ReferenceRoomSpec.CameraToRearWall);
            AtMost(hSubtend, ReferenceRoomSpec.HorizontalFovDegrees * 0.92f,
                   "장치+레버가 차지하는 가로 화각");
        }

        /// <summary>
        /// 지시 「모든 프레임의 굵기가 같아 보이지 않게 한다」 — 3단 위계.
        ///
        /// 굵기가 같으면 격자 무늬가 되고, 격자 무늬는 하중 경로를 말하지 않는다.
        /// 이 검사가 있어야 누가 「정렬이 예쁘게」 한 값을 맞출 때 걸린다.
        /// </summary>
        private static void TestFrameHierarchy()
        {
            Between(ReferenceRoomSpec.OuterFrameBand, 0.070f, 0.090f, "외곽 하중 프레임 폭");

            if (!(ReferenceRoomSpec.OuterFrameBand > ReferenceRoomSpec.BankRibWidth))
                throw new Exception($"외곽 {ReferenceRoomSpec.OuterFrameBand * 1000f:F0}mm 가 " +
                                    $"뱅크 리브 {ReferenceRoomSpec.BankRibWidth * 1000f:F0}mm 보다 두껍지 않다");
            if (!(ReferenceRoomSpec.BankRibWidth > ReferenceRoomSpec.BulkheadHeight))
                throw new Exception($"뱅크 리브 {ReferenceRoomSpec.BankRibWidth * 1000f:F0}mm 가 " +
                                    $"격벽 {ReferenceRoomSpec.BulkheadHeight * 1000f:F0}mm 보다 두껍지 않다");

            // 눈에 보이는 차이여야 한다. 5mm 차이는 4m 거리에서 위계가 아니다.
            AtLeast(ReferenceRoomSpec.OuterFrameBand - ReferenceRoomSpec.BankRibWidth, 0.020f, "외곽−뱅크 차이");
            AtLeast(ReferenceRoomSpec.BankRibWidth - ReferenceRoomSpec.BulkheadHeight, 0.020f, "뱅크−격벽 차이");

            // 캐비닛 폭·높이가 **유도값**인가. 상수로 되돌리면 여기서 갈린다.
            Approx(ReferenceRoomSpec.MachineWidth,
                   ReferenceRoomSpec.OuterFrameBand * 2f + ReferenceRoomSpec.ChamberDoorWidth * 3f
                   + ReferenceRoomSpec.BankRibWidth * 2f, "캐비닛 폭이 도어 3장에서 유도된다");
            Approx(ReferenceRoomSpec.MachineHeight,
                   ReferenceRoomSpec.OuterFrameBand * 2f + ReferenceRoomSpec.ChamberDoorHeight * 3f
                   + ReferenceRoomSpec.BulkheadHeight * 2f + ReferenceRoomSpec.ShaftHousingHeight,
                   "캐비닛 높이가 도어 3장 + 샤프트 하우징에서 유도된다");

            // 지시의 권장 비율.
            Between(ReferenceRoomSpec.MachineWidth, 1.80f, 2.00f, "캐비닛 전체 폭");
            Between(ReferenceRoomSpec.MachineHeight, 1.75f, 1.95f, "캐비닛 전체 높이");
            Between(ReferenceRoomSpec.MachineDepth, 0.20f, 0.28f, "캐비닛 돌출 깊이");
        }

        /// <summary>
        /// 「레버에서 챔버까지 동력 전달 경로를 추적할 수 있음」이 **치수 수준에서**
        /// 성립하는가. 형상이 맞아도 캠 로드가 리브보다 굵으면 밖으로 튀어나온다.
        /// </summary>
        private static void TestCommonLockMechanism()
        {
            // 캠 로드는 뱅크 리브 **안**을 지난다 — 그래야 노출되지 않는다.
            if (ReferenceRoomSpec.CamRodWidth >= ReferenceRoomSpec.BankRibWidth)
                throw new Exception($"캠 로드 {ReferenceRoomSpec.CamRodWidth * 1000f:F0}mm 가 " +
                                    $"뱅크 리브 {ReferenceRoomSpec.BankRibWidth * 1000f:F0}mm 이상 — 밖으로 노출된다");

            // 공통축이 상단 하우징 안에 있는가.
            AtMost(ReferenceRoomSpec.CommonShaftRadius * 2f, ReferenceRoomSpec.ShaftHousingHeight, "공통축 지름");
            Between(ReferenceRoomSpec.CommonShaftY,
                    ReferenceRoomSpec.ChamberStackTopY, ReferenceRoomSpec.MachineTopY, "공통축 높이");

            // 레버 컬럼이 캐비닛에 **결합**돼 있는가. 지시가 「고립된 레버」를
            // 제거 대상으로 지목했고, 결합의 증거가 이 둘이다.
            Approx(ReferenceRoomSpec.LeverGapFromMachine, 0f, "장치~레버 간격이 0 (직결)");
            AtLeast(ReferenceRoomSpec.LeverPlateOverlap, 0.005f, "베이스 플레이트가 캐비닛을 무는 양");

            // 컬럼 상단이 공통축과 같은 높이여야 구동 로드가 갈 곳이 있다.
            Approx(ReferenceRoomSpec.LeverColumnTopY, ReferenceRoomSpec.CommonShaftY, "컬럼 상단 = 공통축 높이");
            AtMost(ReferenceRoomSpec.LeverColumnTopY, ReferenceRoomSpec.MachineTopY, "컬럼이 캐비닛보다 낮다");

            // 상태 탭 셋이 뱅크 위에 정렬되는가.
            for (int b = 0; b < ReferenceRoomSpec.BankCount; b++)
                Approx(ReferenceRoomSpec.BankCenterX(b), ReferenceRoomSpec.WindowCenter(b, 1).x,
                       $"뱅크 {b} 중심이 창 열과 정렬");
            AtMost(ReferenceRoomSpec.StatusTabWidth, ReferenceRoomSpec.ChamberDoorWidth * 0.4f, "상태 탭 폭");
            AtLeast(ReferenceRoomSpec.StatusTabTravel, 0.02f, "상태 탭 이동 거리 (보여야 한다)");

            // 🔴 **공통축이 모서리 기어박스까지 실제로 닿는가.**
            //
            // 첫 판본은 축 길이를 `캐비닛 폭 − 외곽×2` 로 잡아 오른쪽 끝이 x=0.478
            // 에서 끝났고 기어박스는 x=0.684 에서 시작했다 — **206mm 벌어져 있었다.**
            // 독립 평가가 「거기서 사슬이 끊긴다」로 잡았고 좌표가 그것을 확인했다.
            // 원인은 두 함수가 각자 계산한 것이고, 그래서 양 끝을 유도값으로 묶었다.
            Approx(ReferenceRoomSpec.CommonShaftRightX, ReferenceRoomSpec.LeverColumnCenterX,
                   "공통축 오른쪽 끝 = 기어박스 중심");
            Approx(ReferenceRoomSpec.CommonShaftLeftX,
                   ReferenceRoomSpec.MachineLeftX + ReferenceRoomSpec.OuterFrameBand, "공통축 왼쪽 끝");
            AtLeast(ReferenceRoomSpec.CommonShaftLength, ReferenceRoomSpec.MachineWidth * 0.8f, "공통축 길이");

            // 레버 컬럼이 캐비닛과 **같은 깊이**여야 두 몸통이 하나로 읽힌다.
            Approx(ReferenceRoomSpec.LeverColumnDepth, ReferenceRoomSpec.MachineDepth,
                   "레버 컬럼 깊이 = 캐비닛 깊이 (앞면 일치)");
        }

        private static void TestWindowGridFits()
        {
            float r = ReferenceRoomSpec.WindowRingDiameter * 0.5f;
            Vector2 topLeft = ReferenceRoomSpec.WindowCenter(0, 0);
            Vector2 bottomRight = ReferenceRoomSpec.WindowCenter(2, 2);

            AtLeast(topLeft.x - r, ReferenceRoomSpec.MachineLeftX + ReferenceRoomSpec.OuterFrameBand, "격자 좌측");
            AtMost(bottomRight.x + r, ReferenceRoomSpec.MachineRightX - ReferenceRoomSpec.OuterFrameBand, "격자 우측");

            // 🔴 링이 아니라 **도어**로 잰다. 도어가 캐비닛을 타일링하므로 도어가
            // 들어가면 링은 자동으로 들어간다 — 그리고 도어가 안 들어가면
            // 캐비닛 면에 구멍이 생겨 **벽이 보인다.** 그쪽이 더 나쁜 실패다.
            float dx = ReferenceRoomSpec.ChamberDoorWidth * 0.5f;
            float dy = ReferenceRoomSpec.ChamberDoorHeight * 0.5f;
            AtLeast(topLeft.x - dx, ReferenceRoomSpec.MachineLeftX + ReferenceRoomSpec.OuterFrameBand, "도어 좌측");
            AtMost(bottomRight.x + dx, ReferenceRoomSpec.MachineRightX - ReferenceRoomSpec.OuterFrameBand, "도어 우측");
            AtMost(topLeft.y + dy, ReferenceRoomSpec.ChamberStackTopY, "챔버 적층 상단");
            AtLeast(bottomRight.y - dy, ReferenceRoomSpec.ChamberStackBottomY, "챔버 적층 하단");

            // 적층 위에 샤프트 하우징이 실제로 들어갈 자리가 남는가.
            AtLeast(ReferenceRoomSpec.MachineTopY - ReferenceRoomSpec.ChamberStackTopY,
                    ReferenceRoomSpec.ShaftHousingHeight, "적층 위 하우징 자리");

            // 행 0 이 위여야 한다. 뒤집히면 `SpinBoard.Index` 규약과 어긋나 결과가
            // 위아래로 뒤집혀 표시되고, 그건 판정이 아니라 표시만 틀려서 안 잡힌다.
            if (ReferenceRoomSpec.WindowCenter(1, 0).y <= ReferenceRoomSpec.WindowCenter(1, 2).y)
                throw new Exception("행 0 이 행 2 보다 아래다 — 결과판이 상하 반전된다");
            if (ReferenceRoomSpec.WindowCenter(0, 1).x >= ReferenceRoomSpec.WindowCenter(2, 1).x)
                throw new Exception("열 0 이 열 2 보다 오른쪽이다 — 결과판이 좌우 반전된다");
        }

        private static void TestWindowGaps()
        {
            // 지시의 권장 크기.
            Between(ReferenceRoomSpec.ChamberDoorWidth, 0.48f, 0.56f, "챔버 도어 폭");
            Between(ReferenceRoomSpec.ChamberDoorHeight, 0.44f, 0.52f, "챔버 도어 높이");
            Between(ReferenceRoomSpec.WindowRingDiameter, 0.36f, 0.40f, "외부 클램프 링 지름");
            Between(ReferenceRoomSpec.WindowGlassDiameter, 0.25f, 0.29f, "실제 유리 지름");
            Between(ReferenceRoomSpec.WindowProtrusion, 0.050f, 0.070f, "링 돌출 깊이");
            Between(ReferenceRoomSpec.WindowGlassInset, 0.035f, 0.055f, "유리가 링 앞면에서 들어간 깊이");
            Between(ReferenceRoomSpec.WindowBoltCount, 6, 8, "창당 고정 볼트 수");

            // 링 두께는 지름 둘에서 **유도된다.**
            Approx(ReferenceRoomSpec.WindowRingBand,
                   (ReferenceRoomSpec.WindowRingDiameter - ReferenceRoomSpec.WindowGlassDiameter) * 0.5f,
                   "링 두께가 지름 둘에서 유도된다");

            // 🔴 **베젤 판독 상한은 살아 있다.** 개구부 지름의 18% — 16라운드 연속
            // 「베젤이 심볼을 덮어 결과판이 안 읽힌다」 판정에서 나온 값이다.
            // 형태 아키텍처가 바뀌었다고 이 방어선을 함께 버리면, 다음 사람이
            // 링을 두껍게 만들 때 아무도 막지 않는다.
            float bezelRatio = ReferenceRoomSpec.WindowRingBand / ReferenceRoomSpec.WindowGlassDiameter;
            AtMost(bezelRatio, PortholeMesh.MaxBezelToOpeningDiameter, "베젤 폭 ÷ 개구부 지름");

            // 원형 창이 사각 도어 안에 들어가고, 힌지·클램프가 살 여백이 남는가.
            AtMost(ReferenceRoomSpec.WindowRingDiameter, ReferenceRoomSpec.ChamberDoorHeight, "링이 도어 높이 안");
            AtLeast(ReferenceRoomSpec.DoorEdgeMarginX, ReferenceRoomSpec.DoorClampWidth * 0.9f,
                    "도어 좌우 여백이 클램프를 담는다");
            AtLeast(ReferenceRoomSpec.DoorEdgeMarginY, 0.030f, "도어 상하 여백");

            // 볼트가 유리와 링 사이 **금속 위**에 박히는가. 유리 위에 박히면 창이 깨진다.
            AtLeast(ReferenceRoomSpec.WindowBoltRadius, ReferenceRoomSpec.WindowGlassDiameter * 0.5f,
                    "볼트가 유리 바깥");
            AtMost(ReferenceRoomSpec.WindowBoltRadius, ReferenceRoomSpec.WindowRingDiameter * 0.5f,
                    "볼트가 링 안쪽");
        }

        private static void TestNoReelBanding()
        {
            // 🔴 **이 검사가 「같아야 한다」에서 「비가 1.2 이하」로 바뀌었다.**
            //
            // 직전 판본은 가로·세로 간격이 정확히 같기를 요구했다. 2026-08-03 지시가
            // 프레임 3단 위계(뱅크 리브 58mm ≠ 격벽 30mm)를 요구하므로 간격이
            // 필연적으로 달라진다. 방어선을 **없앤 것이 아니라** 원래 근거로 되돌렸다 —
            // `G-SLOT` 축이 띠를 실제로 관측한 구간은 **비 1.51~2.67** 이었고,
            // 「같아야 한다」는 그보다 훨씬 보수적인 대리 조건이었을 뿐이다.
            //
            // 지금 비는 0.578 / 0.510 = **1.133**. 관측 하한에 한참 못 미친다.
            AtMost(ReferenceRoomSpec.WindowPitchAnisotropy, 1.2f, "간격 이방비");

            // 🔴 **그리고 이방비만으로는 부족하다는 것이 실측으로 드러났다.**
            //
            // 이방비 1.133 으로 이 검사를 통과한 판본이 화면에서는 세로 띠 셋으로
            // 읽혔다. 세로 뱅크 리브를 캐비닛 전 높이로 한 토막으로 이어 놓아
            // **분리재의 연속성이 세로에 몰렸기** 때문이다. 간격은 「창이 어디
            // 있나」를 재고, 연속성은 「눈이 무엇을 따라가나」를 잰다 — 릴 띠를
            // 만드는 것은 후자다. 지표를 하나 더 둔다.
            AtMost(ReferenceRoomSpec.LongestVerticalRunRatio, ReferenceRoomSpec.MaxVerticalRunRatio,
                   "끊기지 않는 세로 부재 ÷ 캐비닛 높이");

            // 🔴 **가로 연속성을 「돌출」이 아니라 「음각」으로 얻는다.**
            //
            // 직전 판본은 격벽을 +14mm 앞으로 내밀어 가로가 이기게 했다. 릴 띠는
            // 실제로 막혔지만 그 대가가 컸다 — 격벽이 화면에서 **가장 밝은 선형
            // 요소**가 되어 판이 「가로 선반 3단」으로 읽혔고, `VISUAL_SPEC` §3 의
            // 「각 통관 열 = 결과판 한 열」이 형태로 부정됐다. 독립 평가가
            // 399 px/m 실측으로 잡았다 — 폭은 리브 26px > 격벽 13px 인데
            // 시각 무게는 격벽 > 외곽 ≥ 리브였다.
            //
            // 음각 채널은 **연속된 그림자선**을 남기므로 가로 연속성은 그대로
            // 유지하면서 하이라이트만 포기한다. 그래서 이 검사는 이제
            // 「격벽이 앞이다」가 아니라 「격벽이 음각이다」를 요구한다.
            if (ReferenceRoomSpec.BulkheadProud >= 0f)
                throw new Exception($"격벽이 음각이 아니다 ({ReferenceRoomSpec.BulkheadProud * 1000f:F0}mm) " +
                                    "— 앞으로 나온 가로 부재는 폭이 얼마든 화면에서 가장 강한 분할이 된다");

            // 그리고 세로가 가로를 **시각적으로** 이겨야 한다. 리브 캡이 그 수단이다.
            if (ReferenceRoomSpec.RibCapProud <= ReferenceRoomSpec.BulkheadProud + 0.012f)
                throw new Exception($"세로 리브가 가로 격벽을 못 이긴다 — " +
                                    $"리브 캡 {ReferenceRoomSpec.RibCapProud * 1000f:F0}mm vs " +
                                    $"격벽 {ReferenceRoomSpec.BulkheadProud * 1000f:F0}mm");

            // 그런데 캡이 이어지면 릴 띠가 돌아온다. **끊김**을 함께 요구한다 —
            // 이 두 단정은 반드시 같이 있어야 한다. 하나만 있으면
            // 「리브를 전 높이로 앞에 내민다」가 통과해 버린다.
            if (ReferenceRoomSpec.RibCapSegmentHeight >= ReferenceRoomSpec.ChamberDoorHeight)
                throw new Exception($"리브 캡 토막 {ReferenceRoomSpec.RibCapSegmentHeight:F3} 이 " +
                                    $"도어 높이 {ReferenceRoomSpec.ChamberDoorHeight:F3} 이상이다 — 캡이 이어진다");
            AtMost(ReferenceRoomSpec.RibCapWidth, ReferenceRoomSpec.BankRibWidth * 0.6f, "리브 캡 폭");

            // 간격이 유도값인가. 상수로 되돌리면 프레임 위계와 갈라진다.
            Approx(ReferenceRoomSpec.WindowPitchX,
                   ReferenceRoomSpec.ChamberDoorWidth + ReferenceRoomSpec.BankRibWidth, "가로 간격 유도");
            Approx(ReferenceRoomSpec.WindowPitchY,
                   ReferenceRoomSpec.ChamberDoorHeight + ReferenceRoomSpec.BulkheadHeight, "세로 간격 유도");
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
            // 지시 「측면에서 최소 네 단계의 깊이가 구분됨」.
            // 네 z 값이 **서로 다른 순서로** 늘어서야 한다. 이름만 넷이고 같은
            // 평면에 겹쳐 있으면 화면에서는 한 장이다 — 그것이 직전 판본의 결함이었다.
            float zRingFront = -ReferenceRoomSpec.WindowProtrusion;                 // −0.060
            float zGlass = -ReferenceRoomSpec.WindowGlassFrontOffset;               // −0.015
            const float zDoor = 0f;                                                 //  0.000
            float zSoul = ReferenceRoomSpec.SoulDepthFromDoorFace;                  // +0.100
            float zBack = ReferenceRoomSpec.ChamberBackFromDoorFace;                // +0.198

            if (!(zRingFront < zGlass && zGlass < zDoor && zDoor < zSoul && zSoul < zBack))
                throw new Exception($"깊이 순서가 깨졌다 — 링앞 {zRingFront:F3} / 유리 {zGlass:F3} / " +
                                    $"도어 {zDoor:F3} / 영혼 {zSoul:F3} / 챔버후면 {zBack:F3}");

            // 단계마다 **눈에 보이는** 간격이 있어야 한다. 5mm 차이는 층이 아니다.
            AtLeast(zGlass - zRingFront, 0.030f, "링 앞면 → 유리");
            AtLeast(zDoor - zGlass, 0.010f, "유리 → 도어 면");
            AtLeast(zSoul - zDoor, 0.060f, "도어 면 → 영혼");
            AtLeast(zBack - zSoul, 0.030f, "영혼 → 챔버 후면");

            // 유리가 도어 면보다 앞이어야 링 보어 안에 앉는다.
            AtLeast(ReferenceRoomSpec.WindowGlassFrontOffset, 0.005f, "유리가 도어 면보다 앞");

            // 영혼이 유리에서 8~15cm 뒤 — 지시가 직접 준 범위다.
            // 0 이면 유리에 붙은 스티커로 보인다.
            Between(ReferenceRoomSpec.SoulStandoff, 0.08f, 0.15f, "영혼이 유리에서 떨어진 거리");
            AtMost(ReferenceRoomSpec.SoulStandoff, ReferenceRoomSpec.WindowChamberDepth, "영혼이 챔버 안");

            // 영혼이 유리도 챔버 후면도 뚫지 않는가.
            //
            // ⚠ **인스턴스 크기 흔들림(최대 1.22)을 곱해야 한다.** 조립기가 칸마다
            // 크기를 다르게 주므로, 기준 크기만 검사하면 아홉 중 **가장 큰 하나만**
            // 뚫고 나머지 여덟은 멀쩡하다 — 캡처 한 장으로는 못 잡는 종류의 결함이다.
            float soulHalfZ = ReferenceRoomSpec.SoulMaxHalfDepth;
            AtMost(soulHalfZ, ReferenceRoomSpec.SoulStandoff, "영혼 앞면이 유리 뒤에 머문다");
            AtMost(soulHalfZ, ReferenceRoomSpec.SoulToChamberBack, "영혼 뒷면이 챔버 후면 앞에 머문다");

            // 영혼이 창을 꽉 채우지 않는가. 채우면 둘레의 어둠이 사라져
            // 「챔버 안의 물질」이 아니라 「창 모양 아이콘」이 된다.
            AtMost(ReferenceRoomSpec.SoulRadius * 2f * ReferenceRoomSpec.SoulMaxInstanceScale,
                   ReferenceRoomSpec.WindowGlassDiameter * 0.62f, "가장 큰 영혼의 지름");

            // 챔버가 캐비닛 깊이에서 유도되는가. 상수로 되돌리면 벽을 뚫는다.
            Approx(ReferenceRoomSpec.WindowChamberDepth,
                   ReferenceRoomSpec.MachineDepth - ReferenceRoomSpec.MountFrameThickness
                   - ReferenceRoomSpec.CabinetBackThickness - ReferenceRoomSpec.CabinetFaceThickness,
                   "챔버 깊이가 캐비닛 판재에서 유도된다");
            AtLeast(ReferenceRoomSpec.WindowChamberDepth, 0.12f, "챔버 깊이");
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
