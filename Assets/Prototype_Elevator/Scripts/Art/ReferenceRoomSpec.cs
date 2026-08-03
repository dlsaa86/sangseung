using UnityEngine;

namespace Ascend.Prototype.Art
{
    /// <summary>
    /// 사용자 제시 명세(2026-08-02 「산업용 화물 엘리베이터 내부」)의 **치수 정본**.
    ///
    /// 왜 상수 파일을 따로 두는가: 이 저장소는 치수가 여러 파일에 흩어져 서로 어긋나는
    /// 사고를 반복해서 당했다 — `Reproportion Elevator Car` 가 멱등이 아니어서 두 번
    /// 돌자 계기판이 두 번 밀렸고(`AscendGraphicsBuilder` 주석), 「미터당 반복 수」
    /// 타일링 모델은 캐빈 4배 확대가 전제를 깨뜨려 무너졌다(`GRAPHICS_TARGET` §6.3).
    ///
    /// 그래서 **조립기는 숫자를 갖지 않는다.** 전부 여기서 읽는다. 그리고 이 파일은
    /// 런타임 어셈블리에 있으므로 헤드리스 테스트가 명세 불변식을 직접 검사할 수 있다
    /// (`ReferenceRoomSpecTests`) — 에디터를 띄우지 않고 「명세를 위반했다」를 잡는다.
    ///
    /// ⚠ **이 파일이 명세와 어긋나면 이 파일이 틀린 것이다.** 조립 결과가 마음에 들지
    /// 않아 숫자를 바꾸고 싶어지면, 바꾸기 전에 명세의 어느 줄을 바꾸는 것인지 적는다.
    ///
    /// ── 좌표 규약 ────────────────────────────────────────────────────────────
    ///   +X = 오른쪽 (보관 선반 벽)
    ///   −X = 왼쪽   (가위문 벽)
    ///   +Z = 정면   (통관 장치가 붙은 후면 벽 — 기준 카메라가 바라보는 방향)
    ///   +Y = 위
    ///   원점 = 바닥면의 방 중심. 바닥 윗면이 y = 0 이다.
    ///
    /// 직전 씬은 이 규약과 달랐다 — 장치가 x ≈ −0.98 의 **자립 패널**이었고 출입구가
    /// +Z 였다. 명세 §1·§2 가 「좌 가위문 / 정면 장치 / 우 선반」을 못박으므로
    /// 축을 명세 쪽으로 맞춘다.
    /// </summary>
    public static class ReferenceRoomSpec
    {
        // ══════════════════════════════════════════════════════════════════════
        //  §1 전체 공간 구조
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>실내 유효 좌우 폭(m). 명세 §1.</summary>
        public const float InteriorWidth = 4.0f;

        /// <summary>실내 유효 앞뒤 깊이(m). 명세 §1.</summary>
        public const float InteriorDepth = 4.6f;

        /// <summary>바닥부터 천장까지 높이(m). 명세 §1.</summary>
        public const float InteriorHeight = 2.9f;

        /// <summary>벽·바닥·천장 두께(m). 명세에 없다 — 산업용 철판 셸의 기본값으로 둔다.</summary>
        public const float ShellThickness = 0.16f;

        /// <summary>플레이어 눈높이(m). 명세 §1·§2 가 같은 값을 말한다.</summary>
        public const float EyeHeight = 1.62f;

        /// <summary>플레이어 충돌체 폭(m). 명세 §1.</summary>
        public const float PlayerWidth = 0.6f;

        /// <summary>최소 통행 폭(m). 명세 §1 — 이 값 미만이면 조립이 명세 위반이다.</summary>
        public const float MinWalkway = 0.9f;

        /// <summary>중앙 방해물 없는 이동 공간 최소 폭(m). 명세 §1.</summary>
        public const float RequiredClearX = 2.2f;

        /// <summary>중앙 방해물 없는 이동 공간 최소 깊이(m). 명세 §1.</summary>
        public const float RequiredClearZ = 2.8f;

        // 벽 안쪽면의 좌표. 조립기와 테스트가 같은 값을 쓴다.
        public const float WallLeftX  = -InteriorWidth * 0.5f;   // −2.00
        public const float WallRightX =  InteriorWidth * 0.5f;   // +2.00
        public const float WallFrontZ = -InteriorDepth * 0.5f;   // −2.30  (카메라 뒤)
        public const float WallRearZ  =  InteriorDepth * 0.5f;   // +2.30  (장치 벽)

        // ══════════════════════════════════════════════════════════════════════
        //  §2 카메라와 기본 구도
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>기준 카메라에서 후면 벽까지 거리(m). 명세 §2 「약 4.0m」.</summary>
        public const float CameraToRearWall = 4.0f;

        /// <summary>기준 카메라 Z. 후면 벽에서 <see cref="CameraToRearWall"/> 만큼 뒤.</summary>
        public const float CameraZ = WallRearZ - CameraToRearWall;   // −1.70

        /// <summary>
        /// 수평 시야각(도). 명세 §2 「72~78도」의 중앙값.
        ///
        /// ⚠ Unity 의 `Camera.fieldOfView` 는 **수직**이다. 직전 씬은 60(수직)이었고
        /// 그건 16:9 에서 수평 **약 91°** — 명세 상한을 13° 넘는다. 광각 왜곡이
        /// 「과장된 어안」에 가까워지는 구간이고, 그래서 캡처마다 벽이 휘어 보였다.
        /// 변환은 <see cref="VerticalFovFor"/> 가 한다.
        /// </summary>
        public const float HorizontalFovDegrees = 75f;

        /// <summary>기준 종횡비. 캡처 세트가 1920×1080 이다.</summary>
        public const float ReferenceAspect = 16f / 9f;

        /// <summary>수평 시야각을 Unity 가 요구하는 수직 시야각으로 바꾼다.</summary>
        public static float VerticalFovFor(float horizontalFovDegrees, float aspect)
        {
            float halfH = horizontalFovDegrees * 0.5f * Mathf.Deg2Rad;
            float halfV = Mathf.Atan(Mathf.Tan(halfH) / Mathf.Max(0.0001f, aspect));
            return halfV * 2f * Mathf.Rad2Deg;
        }

        /// <summary>기준 종횡비에서의 수직 시야각(도). 약 46.8°.</summary>
        public static float VerticalFov => VerticalFovFor(HorizontalFovDegrees, ReferenceAspect);

        // ══════════════════════════════════════════════════════════════════════
        //  §3 왼쪽 가위문과 출입구
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>출입구 폭(m). 명세 §3 「2.0~2.2m」.</summary>
        public const float GateOpeningWidth = 2.1f;

        /// <summary>출입구 높이(m). 명세 §3.</summary>
        public const float GateOpeningHeight = 2.35f;

        /// <summary>가위문이 벽면에서 실내로 돌출되는 두께(m). 명세 §3.</summary>
        public const float GateProtrusion = 0.08f;

        /// <summary>평철 하나의 폭(m). 명세 §3 「약 45mm」.</summary>
        public const float GateFlatBarWidth = 0.045f;

        /// <summary>평철 두께(m). 명세 §3 「약 8mm」.</summary>
        public const float GateFlatBarThickness = 0.008f;

        /// <summary>마름모 가로 간격(m). 명세 §3 「약 180mm」.</summary>
        public const float GateLatticePitchZ = 0.18f;

        /// <summary>마름모 세로 간격(m). 명세 §3 「약 380mm」.</summary>
        public const float GateLatticePitchY = 0.38f;

        /// <summary>가이드 레일 단면(m). 바닥·천장 각 하나.</summary>
        public const float GateRailSize = 0.05f;

        /// <summary>하단 롤러 반지름(m).</summary>
        public const float GateRollerRadius = 0.028f;

        /// <summary>층수 표시기 폭(m). 명세 §3.</summary>
        public const float FloorIndicatorWidth = 0.65f;

        /// <summary>층수 표시기 높이(m). 명세 §3.</summary>
        public const float FloorIndicatorHeight = 0.22f;

        /// <summary>층수 표시기 중심 높이(m). 출입구 위 — 문틀 상단과 천장 사이에 둔다.</summary>
        public const float FloorIndicatorCenterY = 2.60f;

        // ══════════════════════════════════════════════════════════════════════
        //  §4 후면 9실 영혼 통관 장치 — 하나의 밀폐 캐비닛
        // ══════════════════════════════════════════════════════════════════════
        //
        // 🔴 **2026-08-03 사용자 지시로 형태 아키텍처를 다시 정의했다.**
        //
        // 직전 판본은 「원형 기계 9개를 벽에 붙인 것」이었다. 링 뒤로 벽이 그대로
        // 노출됐고, 팔각 링·검은 빈 공간·세로봉·소켓·케이블이 **기능적 관계 없이**
        // 붙어 있었다. 지시의 문장 그대로 옮기면 「기계처럼 보이기 위한 장식의 집합」.
        //
        // 지금 정의는 하나다 —
        //   **9개의 영혼을 하나의 레버로 동시에 통관하는 밀폐형 산업 장치.**
        //
        // 3×3 인 이유가 기능에서 나온다: 영혼 하나 = 밀폐 챔버 하나, 총 9개가 필요하고,
        // 좁은 엘리베이터 벽에서 1인칭으로 한눈에 확인하려면 3열 × 3행으로 적층한다.
        // 9개는 **하나의 전력·흡입·잠금 시스템을 공유**하므로 서로 분리된 기계가
        // 아니라 **하나의 직사각형 캐비닛 안의 동일 규격 챔버 9개**다.
        //
        // 그래서 치수의 유도 방향이 뒤집혔다. 직전에는 「장치 폭 1.94」를 먼저 적고
        // 창을 그 안에 욱여넣었다. 지금은 **챔버 도어 한 장**이 원자 단위이고,
        // 캐비닛 폭·높이는 그것을 3×3 으로 쌓은 **결과**다. 이 방향이어야
        // 「왜 이 크기인가」에 부품 이름으로 답할 수 있다.

        // ── 챔버 도어 — 이 장치의 원자 단위 ──────────────────────────────────

        /// <summary>사각 챔버 도어 폭(m). 지시 「약 0.50 × 0.48m」.</summary>
        public const float ChamberDoorWidth = 0.52f;

        /// <summary>사각 챔버 도어 높이(m).</summary>
        public const float ChamberDoorHeight = 0.48f;

        /// <summary>도어 판 두께(m). 챔버를 밀폐하는 판이므로 얇지 않다.</summary>
        public const float CabinetFaceThickness = 0.028f;

        /// <summary>
        /// 도어 판과 그것을 둘러싼 구조 프레임 사이의 이음매 폭(m), 한쪽당.
        ///
        /// 🔴 첫 판본은 5mm 였고 독립 평가자가 **「도어가 없다 — 리브 격자만 있고
        /// 그 안은 통짜 면이다」**로 읽었다. 5mm 는 4m 거리에서 2화소이고,
        /// 2화소짜리 선은 「분리된 판」이 아니라 「긁힌 자국」이다.
        /// </summary>
        public const float DoorSeamWidth = 0.011f;

        /// <summary>
        /// 도어를 밀폐하는 개스킷 압착 프레임의 폭·돌출(m).
        ///
        /// 장식이 아니다 — 압력 챔버의 도어는 둘레를 눌러 밀폐한다.
        /// 그리고 이것이 「이 사각형은 판이 아니라 **문**이다」를 말하는 형상이다.
        /// </summary>
        public const float DoorGasketBand = 0.020f;
        public const float DoorGasketProud = 0.007f;

        /// <summary>
        /// 도어 개구부의 반변(m). **정사각 구멍이다.**
        ///
        /// 원형으로 뚫지 않는 이유: 12각 구멍을 부채꼴로 이은 판이 모든 기하 검사를
        /// 통과하고도 화면에서 챔버를 막았다(중심 덮는 삼각형 0개 · 메시 콜라이더
        /// 레이캐스트 통과 · 그런데 그 도어만 끄면 붉은 화소 0.00% → 0.58%).
        /// 원인을 형상 수준에서 특정하지 못했고, **증명할 수 없는 구조는 쓰지 않는다.**
        ///
        /// 정사각이어도 밖에서는 원으로만 보인다 — 대각 0.187 이 링 안착 단
        /// 반지름 0.204 보다 작아 네 모서리가 링에 완전히 가린다. 그 관계를
        /// <see cref="Violations"/> 가 단정한다.
        /// </summary>
        /// 값은 유리 반지름과 같다 — **도어가 링보다 좁으면 도어가 시야를 먼저 막는다.**
        public static float DoorApertureHalf => WindowGlassDiameter * 0.5f;   // 0.145

        /// <summary>도어 개구부 정사각의 대각 반지름(m). 링 안착 단이 이보다 커야 한다.</summary>
        public static float DoorApertureCornerRadius => DoorApertureHalf * Mathf.Sqrt(2f);

        /// <summary>링이 앉는 얕은 단의 반지름(m). 개구부 모서리를 가리는 부품이다.</summary>
        public static float WindowSeatRadius => WindowRingDiameter * 0.5f + 0.022f;

        /// <summary>
        /// 🔴 **클램프 링 보어가 앞으로 벌어지는 양(m).**
        ///
        /// 실제 압력창의 시트는 테이퍼져 있다 — 원통형 구멍이 아니다. 그리고 그
        /// 테이퍼가 여기서 판독성을 결정한다.
        ///
        /// **레버를 당기려고 선 자리에서 아홉 창이 전부 안 보였다** (붉은 화소 0.02%).
        /// 독립 평가 둘이 각각 최우선 결함으로 지목했고, `visual-criteria.md`
        /// `B-5 #15`「핵심 결과가 특정 위치에서만 보이는가」 금지 항목이다.
        /// 레버를 당겨 판을 해결하는 게임인데 레버 앞이 판이 안 보이는 자리였다.
        ///
        /// **유리가 아니었다.** 통제 실험이 가렸다 —
        ///   유리 제거 0.03% (무관) · 챔버 제거 0.02% (무관)
        ///   도어 제거 **0.21%** · 링 제거 **0.32%** · 둘 다 **1.04%**
        /// 즉 예각에서 보어 벽이 시선을 막는 **기하 비네팅**이다.
        /// 42° 에서 깊이 60mm 는 측면으로 54mm 를 먹는다 — 반지름 145mm 의 37% 다.
        /// </summary>
        public const float WindowBoreFlare = 0.030f;

        // ── 프레임 3단 위계 ───────────────────────────────────────────────────
        //
        // 지시 「모든 프레임의 굵기가 같아 보이지 않게 한다」 —
        //   외곽 하중 프레임 > 세로 뱅크 프레임 > 챔버 사이 가로 격벽.
        //
        // 굵기가 같으면 격자 무늬가 되고, 격자 무늬는 하중 경로를 말하지 않는다.
        // 각 굵기가 **무엇을 받는가**로 정해진다 —
        //   외곽: 캐비닛 전체 자중 + 벽 고정. 가장 두껍다.
        //   뱅크: 챔버 3개 적층 하중 + 챔버 간 격벽 + 공통 잠금 링크 보호.
        //   격벽: 챔버 하나와 하나 사이의 밀폐 칸막이. 하중을 받지 않는다.

        /// <summary>
        /// 외곽 하중 프레임 폭(m). 지시 「약 70~90mm」.
        ///
        /// ⚠ 86mm 였는데 아래 <see cref="BankRibWidth"/> 가 58 → 68 로 올라가면서
        /// **외곽−뱅크 차이가 18mm 로 줄어 3단 위계 하한(20mm)을 깼다.**
        /// 「5mm 차이는 4m 거리에서 위계가 아니다」로 세운 그 하한이다.
        ///
        /// 뱅크를 되돌리지 않는다 — 68 : 30 은 격벽 돌출 하이라이트를 이기려고
        /// 독립 평가의 REJECT 를 받고 올린 값이다. 대신 **외곽을 지시 상한까지 올려**
        /// 위계를 위쪽에서 복원한다. 90 : 68 : 30 = 3.0 : 2.3 : 1.0.
        /// 캐비닛 폭 1.868 → 1.876 (벽 46.9%) · 높이 1.792 → 1.800 (벽 62.1%) 로
        /// 둘 다 요구 범위 안이다.
        /// </summary>
        public const float OuterFrameBand = 0.090f;

        /// <summary>
        /// 세로 뱅크 프레임 폭(m). 외곽보다 얇고 격벽보다 두껍다.
        ///
        /// ⚠ 58mm 였을 때 독립 평가가 「굵기 3단이 **2단으로** 읽힌다 — 30mm 격벽이
        /// 14mm 돌출해 하이라이트를 얻으며 58mm 리브보다 굵어 보인다」로 판정했다.
        /// **깊이 위계를 뒤집은 대가**다(가로를 앞에 둬서 세로 릴 띠를 닫았다).
        ///
        /// 둘 중 하나를 포기하지 않고 **굵기 차이를 키워** 푼다 — 68 : 30 = 2.3배.
        /// 화면에서 27 : 12 px 이면 그림자 이득으로 뒤집히지 않는다.
        /// 캐비닛 폭이 1.848 → 1.868 로 20mm 늘지만 벽 점유 46.7% 로 요구 안이다.
        /// </summary>
        public const float BankRibWidth = 0.068f;

        /// <summary>챔버 사이 가로 격벽 두께(m). 셋 중 가장 얇다.</summary>
        public const float BulkheadHeight = 0.030f;

        /// <summary>상단 공통 샤프트 하우징 높이(m). 잠금축이 실제로 들어가는 자리다.</summary>
        public const float ShaftHousingHeight = 0.120f;

        /// <summary>
        /// 장치 전체 폭(m). **적지 않고 유도한다** — 도어 3장 + 뱅크 리브 2 + 외곽 2.
        ///
        /// ⚠ 직전 판본은 이 값을 상수로 적었고, 그래서 창 간격을 바꿀 때마다
        /// 「격자가 프레임을 침범하는가」를 뺄셈으로 다시 확인해야 했다.
        /// 유도하면 침범이 **원리적으로 불가능**하다.
        /// </summary>
        public static float MachineWidth
            => OuterFrameBand * 2f + ChamberDoorWidth * 3f + BankRibWidth * 2f;   // 1.848 → 46.2%

        /// <summary>장치 전체 높이(m). 도어 3장 + 격벽 2 + 외곽 2 + 샤프트 하우징.</summary>
        public static float MachineHeight
            => OuterFrameBand * 2f + ChamberDoorHeight * 3f + BulkheadHeight * 2f
             + ShaftHousingHeight;                                                 // 1.792 → 61.8%

        /// <summary>
        /// 장치가 벽에서 실내로 돌출되는 깊이(m). 지시 「약 0.20~0.28m」.
        ///
        /// 이 값이 **챔버 내부 깊이를 결정한다.** 영혼이 유리 뒤 8~15cm 에 떠 있고
        /// 그 뒤로 챔버 후면이 더 있어야 하므로, 0.20 으로는 공간이 모자란다.
        /// </summary>
        public const float MachineDepth = 0.26f;

        /// <summary>캐비닛 후면판 두께(m). 챔버의 뒷벽이다.</summary>
        public const float CabinetBackThickness = 0.024f;

        /// <summary>벽에 고정되는 후면 하중 프레임의 두께(m). 캐비닛과 벽 사이에 낀다.</summary>
        public const float MountFrameThickness = 0.038f;

        /// <summary>후면 하중 프레임의 부재 폭(m).</summary>
        public const float MountFrameBand = 0.072f;

        /// <summary>
        /// 장치 하단과 바닥 사이 간격(m). 명세 §4 「약 0.2m」.
        ///
        /// ⚠ **명세 안에 산술 불일치가 하나 있다.** §4 는 하단 간격 0.2 · 높이 2.2 ·
        /// 상단 여백 0.35 를 함께 적는데 그 합은 **2.75** 이고 실내 높이는 2.9 다.
        /// 0.15m 가 남는다. 셋 중 둘(하단 간격·높이)이 더 구체적인 수치이므로 그 둘을
        /// 그대로 지키고, 파생값인 상단 여백이 **0.50m** 가 되게 둔다.
        /// `ASSUMPTION_LOG` 에 기록한다 — 임의 조정이 아니라 명세 내부 모순의 해소다.
        /// </summary>
        public const float MachineBottomGap = 0.2f;

        /// <summary>장치 중심 X. 명세 §4 「후면 벽의 중앙에서 약간 왼쪽」.</summary>
        public const float MachineCenterX = -0.35f;

        /// <summary>
        /// 외곽 프레임 폭(m). <see cref="OuterFrameBand"/> 의 옛 이름.
        ///
        /// ⚠ 이름을 남겨 두는 것이 아니라 **한 값을 가리키게** 한다. 두 상수로 두면
        /// 반드시 갈라진다 — 이 파일의 첫 주석이 적고 있는 그 사고다.
        /// </summary>
        public static float MachineFrameBand => OuterFrameBand;

        public static float MachineBottomY => MachineBottomGap;                     // 0.200
        public static float MachineTopY => MachineBottomGap + MachineHeight;        // 1.992
        public static float MachineCeilingGap => InteriorHeight - MachineTopY;      // 0.908
        public static float MachineLeftX => MachineCenterX - MachineWidth * 0.5f;   // −1.274
        public static float MachineRightX => MachineCenterX + MachineWidth * 0.5f;  // +0.574

        /// <summary>캐비닛 전면 z. 벽 안쪽면에서 <see cref="MachineDepth"/> 만큼 실내 쪽.</summary>
        public static float MachineFrontZ => WallRearZ - MachineDepth;              // 2.04

        /// <summary>후면 벽 폭 대비 장치 폭 비율. 요구 45~52%.</summary>
        public static float MachineWallCoverage => MachineWidth / InteriorWidth;    // 0.462

        // ── 원형 압력 관찰창 — 별도의 기계가 아니라 도어에 설치된 창 ────────────
        //
        // 🔴 지시: 「각 원형 요소는 별도의 기계가 아니라, 사각형 챔버 도어에 설치된
        // **압력 관찰창**이다. 원형인 이유는 내부 압력을 균등하게 견디기 위한 것이다.」
        //
        // 그래서 층이 넷뿐이다 — ① 사각 도어 ② 낮고 두꺼운 원형 클램프 링
        // ③ 링 안쪽으로 들어간 두꺼운 유리 ④ 유리 뒤 영혼 포획 공간.
        //
        // 직전 판본의 ①후면 장착판 ②지지 브래킷 ⑤케이블 소켓은 **삭제한다.**
        // 캐비닛이 연속면이 된 지금 「모듈을 벽에 붙이는 판」은 받을 하중이 없고,
        // 하중을 안 받는 브래킷은 정의상 장식이다.

        /// <summary>내부 유리 지름(m). 지시 「약 0.25~0.29m」 — 상한.</summary>
        public const float WindowGlassDiameter = 0.29f;

        /// <summary>클램프 링이 도어 면에서 돌출되는 깊이(m). 지시 「약 50~70mm」.</summary>
        public const float WindowProtrusion = 0.060f;

        /// <summary>
        /// 외부 클램프 링 지름(m). 지시 「약 0.36~0.40m」.
        ///
        /// **이번에는 지름을 적고 두께를 유도한다** — 직전과 방향이 반대다.
        /// 지시가 링 지름과 유리 지름을 **둘 다** 범위로 주었고, 그 둘이 정해지면
        /// 두께는 남는 값이기 때문이다. 두께를 적으면 지름이 범위를 벗어날 수 있고
        /// 그러면 어느 쪽을 어긴 것인지 사후에 못 가른다.
        /// </summary>
        public const float WindowRingDiameter = 0.38f;

        /// <summary>
        /// 클램프 링의 반경 방향 두께(m). 지름 둘에서 유도한다 → 45mm.
        ///
        /// 🔴 **저장소의 판독 상한이 여기서 살아 있다.**
        /// <c>PortholeMesh.MaxBezelToOpeningDiameter = 0.18</c> — 베젤 폭이 개구부
        /// 지름의 18% 를 넘으면 「베젤이 심볼을 덮어 결과판이 안 읽힌다」는 판정이
        /// 16라운드 연속으로 나왔다. 지금 값은 45/290 = **0.155** 로 상한 안이다.
        /// 직전 판본(0.179)은 상한에 붙어 있었고, 그것이 근접 캡처에서 링이
        /// 「과도하게 두꺼운 이중 팔각 링」으로 읽힌 원인의 절반이다.
        /// </summary>
        public static float WindowRingBand => (WindowRingDiameter - WindowGlassDiameter) * 0.5f;

        /// <summary>링 둘레 고정 볼트 수. 지시 「창 하나당 6개 또는 8개」.</summary>
        public const int WindowBoltCount = 8;

        /// <summary>
        /// 볼트가 박히는 원의 반지름(m). 유리 가장자리와 링 가장자리의 중간.
        ///
        /// **창마다 같아야 한다.** 지시 「볼트는 일정한 간격으로 배치한다. 창마다
        /// 볼트 개수와 위치가 무작위로 달라지면 안 된다」 — 규격품이 아니면
        /// 「같은 시스템의 챔버 9개」가 아니라 「제각각인 기계 9개」로 읽힌다.
        /// </summary>
        public static float WindowBoltRadius => (WindowGlassDiameter + WindowRingDiameter) * 0.25f;

        /// <summary>
        /// 챔버 중심 가로 간격(m) = 도어 폭 + 뱅크 리브. **유도값이다.**
        ///
        /// 🔴 **직전 판본은 가로·세로를 강제로 같게 두었다** — 「간격이 다르면 열로
        /// 뭉쳐 슬롯머신 릴이 된다」는 `G-SLOT` 방어선 때문이다. 그 방어선의 근거는
        /// 실측이었다: 장축/단축 비 **1.51~2.67** 에서 띠가 관측됐다.
        ///
        /// 지금 구조는 프레임 3단 위계를 요구하므로 뱅크 리브(58mm)와 격벽(30mm)이
        /// 다르고, 따라서 간격도 다르다. 하지만 비는 **0.578 / 0.510 = 1.133** 으로
        /// 관측 하한 1.51 에 한참 못 미친다. 방어선은 「같아야 한다」가 아니라
        /// 「비가 1.2 를 넘지 않아야 한다」로 바꿔 유지한다 — 없앤 것이 아니다.
        /// </summary>
        public static float WindowPitchX => ChamberDoorWidth + BankRibWidth;    // 0.578

        /// <summary>챔버 중심 세로 간격(m) = 도어 높이 + 격벽. **유도값이다.**</summary>
        public static float WindowPitchY => ChamberDoorHeight + BulkheadHeight; // 0.510

        /// <summary>간격 이방비. 1.2 를 넘으면 열(또는 행)로 뭉쳐 릴 띠가 된다.</summary>
        public static float WindowPitchAnisotropy
            => Mathf.Max(WindowPitchX, WindowPitchY) / Mathf.Max(0.0001f, Mathf.Min(WindowPitchX, WindowPitchY));

        /// <summary>
        /// 🔴 **끊기지 않는 세로 부재의 최대 길이 ÷ 캐비닛 높이.**
        ///
        /// 이 지표가 왜 필요한가 — <see cref="WindowPitchAnisotropy"/> **하나로는
        /// 릴 띠 회귀를 잡지 못한다.** 실제로 못 잡았다.
        ///
        /// 간격 이방비 1.133 으로 통과한 판본이 화면에서는 세로 띠 셋으로 읽혔다.
        /// 세로 뱅크 리브를 캐비닛 전 높이로 **한 토막**으로 이어 놓았기 때문이다 —
        /// 간격은 등방인데 **분리재의 연속성**이 세로에 몰렸다. 독립 평가가
        /// 「무중단 713px 채널 · 가로 격벽은 리브마다 끊김 · 재질을 입힐수록 나빠진다」
        /// 로 잡아냈다.
        ///
        /// 간격은 「창이 어디 있나」를 재고, 이 비는 「눈이 무엇을 따라가나」를 잰다.
        /// 둘은 다른 것이고, 릴 띠를 만드는 것은 후자다.
        ///
        /// 리브를 행마다 끊으면 무중단 길이가 도어 한 장 높이로 떨어진다 →
        /// 0.48 / 1.792 = **0.268**.
        /// </summary>
        public static float LongestVerticalRunRatio => ChamberDoorHeight / MachineHeight;

        /// <summary>세로 무중단 비의 상한. 이 값을 넘으면 판이 열로 갈라져 보인다.</summary>
        public const float MaxVerticalRunRatio = 0.40f;

        /// <summary>캡처 하네스가 「한 칸만큼 옮긴다」에 쓰는 대표 간격(m).</summary>
        public static float WindowPitch => WindowPitchX;

        /// <summary>
        /// 관찰창 실루엣 분할 수. 명세 §14 「완전한 원보다 12~16각형」.
        ///
        /// **4의 배수여야 한다** — <c>PortholeMesh.Clamped()</c> 가 그렇게 강제한다.
        /// 셀 사각형의 네 모서리가 표본 각도에 걸려야 이웃 셀의 공유 모서리가 같은
        /// 점 집합이 되고, 그래야 껍질이 닫힌다. 그래서 12~16 중 쓸 수 있는 값은
        /// 12 와 16 뿐이고, 더 각진 12 를 고른다(명세 §14 「매끄러운 곡면을 피함」).
        /// </summary>
        public const int WindowSilhouetteSides = 12;

        // ── 창의 4층 구조 — 측면에서 이 넷이 구분돼야 한다 ─────────────────────
        //
        //   ① 사각 챔버 도어        z  0.000  (캐비닛 전면과 같은 평면)
        //   ② 클램프 링             z −0.060  링 앞면 (도어에서 60mm 돌출)
        //   ③ 두꺼운 유리           z −0.015  링 앞면에서 45mm 안쪽
        //   ④ 영혼 포획 공간        z +0.100  유리에서 115mm 뒤
        //      챔버 후면            z +0.208
        //
        // (−z 가 실내 쪽. 지시의 「측면에서 최소 네 단계의 깊이가 구분됨」이 이것이다.)

        /// <summary>③ 유리가 링 앞면에서 **안쪽으로** 들어간 깊이(m). 지시 「약 35~55mm」.</summary>
        public const float WindowGlassInset = 0.045f;

        /// <summary>유리 두께(m). 압력 용기의 창이므로 얇지 않다 — 가장자리가 어둡게 읽힌다.</summary>
        public const float WindowGlassThickness = 0.018f;

        /// <summary>
        /// 유리가 도어 면보다 **앞에** 있는 양(m). 링 돌출 − 유리 인셋 = 15mm.
        /// 양수여야 유리가 링 보어 안에 앉는다. 음수가 되면 유리가 도어를 뚫는다.
        /// </summary>
        public static float WindowGlassFrontOffset => WindowProtrusion - WindowGlassInset;

        /// <summary>
        /// ④ 챔버 내부 깊이(m). **유도값이다** — 캐비닛 돌출에서 판재 셋을 뺀다.
        ///
        ///   벽 ─ 후면 하중 프레임 38 ─ 캐비닛 후면판 24 ─ **챔버 170** ─ 도어 28 ─ 실내
        ///
        /// 직전 판본은 0.115 를 상수로 적었고 캐비닛 깊이와 아무 관계가 없었다.
        /// 그래서 「챔버가 캐비닛보다 깊다」가 가능했고 실제로 그랬다.
        /// 유도하면 그 사고가 원리적으로 불가능하다.
        /// </summary>
        public static float WindowChamberDepth
            => MachineDepth - MountFrameThickness - CabinetBackThickness - CabinetFaceThickness;  // 0.170

        /// <summary>도어 바깥면에서 챔버 후면까지의 거리(m).</summary>
        public static float ChamberBackFromDoorFace => CabinetFaceThickness + WindowChamberDepth; // 0.198

        /// <summary>챔버 내부 단면(m). 원형 창 너머로 보이는 어두운 공간의 크기.</summary>
        public static float ChamberInnerWidth => ChamberDoorWidth - BankRibWidth * 2f;            // 0.404
        public static float ChamberInnerHeight => ChamberDoorHeight - BulkheadHeight * 2f;        // 0.420

        /// <summary>
        /// 영혼이 **유리 뒤로** 떨어진 거리(m). 지시 「챔버 내부 약 8~15cm 뒤」.
        ///
        /// 이 거리가 패럴랙스의 전부다. 0 이면 유리에 붙은 스티커이고, 그것이
        /// 직전 판본에서 「분홍색 평면 원」으로 읽힌 이유다.
        /// </summary>
        public const float SoulStandoff = 0.105f;

        /// <summary>영혼 중심이 **도어 면 기준**으로 얼마나 뒤인가(m).</summary>
        public static float SoulDepthFromDoorFace => SoulStandoff - WindowGlassFrontOffset;  // 0.100

        /// <summary>영혼 뒤에 남는 어두운 챔버 후면까지의 거리(m). 0 이면 배경이 없다.</summary>
        public static float SoulToChamberBack => ChamberBackFromDoorFace - SoulDepthFromDoorFace; // 0.098

        /// <summary>
        /// 영혼 덩어리의 기준 반지름(m). 유리 **지름** 대비 0.20 → 0.058.
        ///
        /// ⚠ 첫 값 0.30 은 챔버 후면을 뚫었다(불변식이 즉시 잡았다: 뒷면 반경
        /// 0.1118 > 여유 0.0980). 원인은 「지름 대비」와 「반지름 대비」를 섞은 것 —
        /// 이 저장소가 타일링에서 세 번 당한 것과 **같은 종류의 단위 혼동**이다.
        /// 그래서 이 자리에 두 값을 다 적어 둔다: 반지름 0.058 = 지름 0.116,
        /// 유리 지름 0.29 의 40%. 어둠이 둘레에 남아 「챔버 안의 물질」로 읽힌다.
        /// </summary>
        public static float SoulRadius => WindowGlassDiameter * 0.20f;                       // 0.0580

        /// <summary>
        /// 영혼 인스턴스에 걸리는 **최대** 크기 흔들림. 조립기가 칸마다 다르게 준다.
        /// 침범 검사가 이 배수를 곱해야 한다 — 안 곱하면 아홉 중 가장 큰 하나만 뚫는다.
        /// </summary>
        public const float SoulMaxInstanceScale = 1.22f;

        /// <summary>가장 큰 개체의 깊이 방향 반경(m). 메시 z 신장 1.02 × 정점 흔들림 1.26.</summary>
        public static float SoulMaxHalfDepth => SoulRadius * 1.02f * 1.26f * SoulMaxInstanceScale;

        // ── 도어 금구 — 힌지 하나와 잠금 클램프 하나 ───────────────────────────
        //
        // 지시 「챔버 도어의 한쪽에는 실제로 열릴 것처럼 보이는 작은 힌지를 배치하고
        // 반대쪽에는 잠금 클램프 하나를 둔다. 단, 지나치게 많은 손잡이와 볼트를
        // 추가하지 마라.」 — 그래서 힌지 2개(위·아래 경첩), 클램프 **1개**다.

        /// <summary>도어 힌지 개수. 문 하나에 경첩 둘이면 열리는 물건으로 읽힌다.</summary>
        public const int DoorHingeCount = 2;

        public const float DoorHingeWidth = 0.046f;
        public const float DoorHingeHeight = 0.062f;

        /// <summary>잠금 클램프 — 도어당 하나. 공통 캠 로드가 이것을 돌린다.</summary>
        public const float DoorClampWidth = 0.054f;
        public const float DoorClampHeight = 0.040f;

        /// <summary>체결될 때 클램프가 도는 각도(도). 9개가 동시에 돈다.</summary>
        public const float DoorClampSwingDegrees = 34f;

        /// <summary>링 가장자리와 도어 가장자리 사이 여백(m). 힌지·클램프가 사는 자리.</summary>
        public static float DoorEdgeMarginX => (ChamberDoorWidth - WindowRingDiameter) * 0.5f;   // 0.070
        public static float DoorEdgeMarginY => (ChamberDoorHeight - WindowRingDiameter) * 0.5f;  // 0.050

        // ══════════════════════════════════════════════════════════════════════
        //  §4b 공통 잠금 기구 — 레버 하나가 9개 챔버를 동시에 잠근다
        // ══════════════════════════════════════════════════════════════════════
        //
        // 지시가 요구하는 동력 전달 사슬 —
        //   ① 레버를 당긴다
        //   ② 레버 회전축이 캐비닛 쪽으로 들어간다
        //   ③ 공통 수평축이 회전한다
        //   ④ 세 개의 세로 캠 로드가 동시에 움직인다
        //   ⑤ 캠 로드 하나가 그 뱅크의 챔버 셋을 동시에 잠근다
        //   ⑥ 9개 클램프가 한 번에 체결된다
        //
        // 🔴 **모든 링크를 밖으로 노출하지 않는다.** 지시 「기능 없는 파이프와
        // 케이블로 연결된 것처럼 꾸미지 마라」. 캠 로드는 뱅크 리브 **안**을 지나고
        // 밖에서 보이는 것은 넷뿐이다 —
        //   · 레버 허브에서 나오는 굵은 회전축
        //   · 컬럼을 타고 올라가는 구동 로드 (허브 높이 → 공통축 높이)
        //   · 캐비닛 상단의 공통 샤프트 하우징
        //   · 뱅크 셋 위에 정렬된 기계식 상태 탭 3개
        //
        // ⚠ **구동 로드는 지시의 사슬에 없는 부품이다.** 지시는 「회전 허브 → 공통
        // 수평축」을 직결로 적었는데, 그러려면 둘의 높이가 같아야 한다. 공통축은
        // 상단 하우징(y≈1.93)에 있고 허브는 손이 닿아야 하므로 1.15~1.30 이다.
        // 둘을 같은 높이로 두는 방법은 둘뿐이었다 —
        //   (a) 공통축을 허브 높이로 내린다 → 하우징이 3×3 격자 한가운데를 가로지른다.
        //       「가장 먼저 하나의 직사각형 통관 기계가 보임」이 깨진다.
        //   (b) 허브를 상단으로 올린다 → 손이 닿지 않는다(눈높이 1.62, 허브 1.93).
        // 그래서 세로 구동 로드를 **명시적인 부품으로** 넣는다. 이름이 있고,
        // 무엇을 무엇으로 옮기는지 한 문장으로 답할 수 있고, 없으면 사슬이 끊긴다.

        // ── 앞으로 나온 양 — **두 파일이 같은 숫자를 봐야 한다** ────────────────
        //
        // 이 넷은 조립기 안에 지역 상수로 있었다. 그래서 레버 컬럼을 짓는 함수가
        // 캐비닛이 얼마나 튀어나왔는지 몰랐고, 모서리 기어박스와 공통축이 **206mm
        // 어긋난 채** 만들어졌다. 독립 평가자가 「기어박스와 공통축이 만나는 것이
        // 보이지 않는다 · 거기서 사슬이 끊긴다」로 정확히 잡아냈다.
        //
        // 치수가 두 곳에 있으면 반드시 갈라진다 — 이 파일의 첫 주석이 그것이다.

        /// <summary>외곽 하중 프레임이 도어 면보다 앞으로 나온 양(m). 가장 앞.</summary>
        public const float OuterFrameProud = 0.022f;

        /// <summary>
        /// 가로 격벽이 도어 면보다 **들어간** 양(m). 음각 채널이다 — 부호가 반대다.
        ///
        /// 🔴 **돌출이 위계를 만든다. 폭이 아니다.**
        ///
        /// 직전 판본은 격벽을 +14mm 앞으로 내밀었다. 그 결과 실측이 이렇게 나왔다
        /// (399 px/m · 독립 평가 실측):
        ///
        ///   외곽 86mm(30px) 돌출 22mm — 벽과 값 차 미미
        ///   리브 68mm(26px) 돌출  7mm — **하이라이트도 그림자도 없다**
        ///   격벽 30mm(13px) 돌출 14mm — **화면에서 가장 밝은 선형 요소**
        ///
        /// 폭은 사양대로 68 : 30 = 2.3배로 들어가 있는데 화면의 시각 무게는
        /// 격벽 > 외곽 ≥ 리브 로 **완전히 뒤집혀 있었다.** 14mm 돌출한 13px 부재는
        /// 하이라이트와 그림자 **두 개**의 신호를 얻고, 7mm 돌출한 26px 부재는
        /// 아무 신호도 얻지 못하기 때문이다. 폭을 58 → 68 로 올린 조치는
        /// **원인이 아닌 축을 건드린 것**이었고 그래서 지적이 그대로 남았다.
        ///
        /// 그리고 이 실패는 취향 문제가 아니었다. 격벽이 가장 강한 분할이 되면
        /// 판이 **가로 선반 3단**으로 읽히고, `VISUAL_SPEC` §3 의
        /// 「세 개의 수직 통관이 나란히 배치되고 각 통관은 결과판의 한 열과
        /// 대응한다」가 형태로 부정된다. 릴 띠를 잡느라 **열 대응을 잃은 것**이다.
        ///
        /// 음각으로 뒤집으면 가로 연속성은 **그림자선으로 유지되고**(릴 띠 방어는
        /// 살아 있다) 하이라이트만 잃는다. 리브가 상대적으로 앞선다.
        /// </summary>
        public const float BulkheadRecess = 0.008f;

        /// <summary>
        /// 격벽의 실효 돌출(m). 음각이므로 **음수**다.
        /// 깊이 위계 불변식이 이 값을 본다.
        /// </summary>
        public static float BulkheadProud => -BulkheadRecess;

        /// <summary>세로 뱅크 프레임이 앞으로 나온 양(m). 리브 몸통.</summary>
        public const float BankRibProud = 0.007f;

        // ── 하중 경로 — **굵기가 아니라 접속부가 하중을 말한다** ──────────────
        //
        // 🔴 외곽 프레임 86mm 는 사양대로인데 독립 평가가 세 라운드 연속
        // 「앵커·플랜지 없는 균일폭 액자 몰딩」으로 읽었다. 399 px/m 실측에서
        // 네 변이 전부 폭 30px 평면 띠였고, **볼트·러그·브래킷·거싯·기초 채널이
        // 화면에 하나도 없었다.** 1.868 × 1.792 × 0.26 m 강재 캐비닛이 벽에
        // 무엇으로 붙어 있는지 형태가 답하지 못한 것이다.
        //
        // 기존 마운트 프레임에도 볼트와 거싯은 있었다 — 전부 **캐비닛 뒤에
        // 가려** 정면에서 한 개도 안 보였다. 안 보이는 접속부는 없는 것과 같다.
        // 그래서 러그를 **실루엣 밖으로** 내민다.

        /// <summary>벽 앵커 러그 한 장의 폭(m). 프레임 바깥으로 나가는 방향.</summary>
        public const float AnchorLugWidth = 0.120f;

        /// <summary>벽 앵커 러그 한 장의 높이(m).</summary>
        public const float AnchorLugHeight = 0.090f;

        /// <summary>러그가 캐비닛 실루엣 **밖으로** 나가는 양(m). 이것이 보여야 한다.</summary>
        public const float AnchorLugOverhang = 0.030f;

        /// <summary>한쪽 변의 러그 개수. 좌우 합계 6개.</summary>
        public const int AnchorLugPerSide = 3;

        /// <summary>기초 채널(sill)의 깊이(m). 캐비닛 아래를 받친다.</summary>
        public const float SillDepth = 0.140f;

        /// <summary>기초 채널의 높이(m).</summary>
        public const float SillHeight = 0.090f;

        /// <summary>기초 채널 아래 받침 다리의 개수. 바닥까지 하중을 내린다.</summary>
        public const int SillLegCount = 3;

        /// <summary>러그 i 의 높이(m). 캐비닛 높이를 4등분한 안쪽 세 점.</summary>
        public static float AnchorLugY(int i)
            => MachineBottomY + MachineHeight * (i + 1) / (AnchorLugPerSide + 1);

        /// <summary>
        /// 리브 앞면에 얹는 **캡 스트립**의 폭(m).
        ///
        /// 리브 몸통을 통째로 앞으로 내밀지 않는 이유: 그러면 리브가 26px 폭으로
        /// 전면에 나와 다시 세로 채널이 되고 `G-SLOT` 릴 띠가 돌아온다.
        /// 캡은 리브보다 **좁고**(28 vs 68mm) 무엇보다 **행마다 끊긴다** —
        /// 하이라이트는 얻고 무중단 세로 길이는 도어 높이(0.48 m)를 넘지 않는다.
        /// </summary>
        public const float RibCapWidth = 0.028f;

        /// <summary>리브 캡이 도어 면보다 앞으로 나온 양(m). 격벽보다 확실히 앞이다.</summary>
        public const float RibCapProud = 0.018f;

        /// <summary>리브 캡 한 토막의 세로 길이(m). 도어 높이보다 짧아야 끊긴다.</summary>
        public static float RibCapSegmentHeight => ChamberDoorHeight - 0.040f;

        /// <summary>샤프트 하우징이 외곽 프레임보다 더 앞으로 나온 양(m).</summary>
        public const float ShaftHousingProud = 0.010f;

        /// <summary>공통 수평 잠금축의 반지름(m). 9개 챔버의 체결력을 전부 받는다.</summary>
        public const float CommonShaftRadius = 0.028f;

        /// <summary>공통 수평축의 높이(m). 상단 샤프트 하우징의 한가운데.</summary>
        public static float CommonShaftY => MachineTopY - ShaftHousingHeight * 0.5f;   // 1.932

        /// <summary>샤프트 하우징 앞면의 z (벽 안쪽면 기준, 음수가 실내 쪽).</summary>
        public static float ShaftHousingFrontZ => -(MachineDepth + OuterFrameProud + ShaftHousingProud);

        /// <summary>
        /// 공통축 중심의 z. **모서리 기어박스가 이 값을 그대로 쓴다** —
        /// 두 부품이 만나는 지점의 좌표는 한 곳에서만 나와야 한다.
        /// </summary>
        public static float CommonShaftZ => ShaftHousingFrontZ - CommonShaftRadius * 0.35f;

        /// <summary>공통축의 왼쪽 끝 x. 캐비닛 좌측 하중 프레임 안쪽.</summary>
        public static float CommonShaftLeftX => MachineLeftX + OuterFrameBand;

        /// <summary>
        /// 공통축의 오른쪽 끝 x = **레버 컬럼 중심.** 거기서 모서리 기어박스와 만난다.
        /// 축을 캐비닛 안에서 끊으면 「레버에서 챔버까지」가 화면에서 끊긴다.
        /// </summary>
        public static float CommonShaftRightX => LeverColumnCenterX;

        public static float CommonShaftLength => CommonShaftRightX - CommonShaftLeftX;

        /// <summary>세로 캠 로드 수 = 세로 뱅크 수.</summary>
        public const int BankCount = 3;

        /// <summary>세로 캠 로드 단면(m). 뱅크 리브 안을 지나므로 리브보다 얇아야 한다.</summary>
        public const float CamRodWidth = 0.030f;

        /// <summary>기계식 상태 탭(m). 뱅크마다 하나씩 하우징 앞면에 난다.</summary>
        public const float StatusTabWidth = 0.072f;
        public const float StatusTabHeight = 0.048f;

        /// <summary>체결될 때 상태 탭이 내려가는 거리(m). 셋이 동시에 움직인다.</summary>
        public const float StatusTabTravel = 0.034f;

        /// <summary>뱅크 <paramref name="bank"/> 의 중심 X.</summary>
        public static float BankCenterX(int bank) => MachineCenterX + (bank - 1) * WindowPitchX;

        // ── 레버 기구 (영웅 오브젝트 명세 2단계) ───────────────────────────

        /// <summary>
        /// **벽과 장치 양쪽에 고정된** 베이스 플레이트(m). 지시가 요구하는 첫 부품이다.
        ///
        /// 폭 0.36 은 컬럼(0.34)보다 넓다 — 의도다. 컬럼 중심이 0.744 이므로
        /// 판의 왼쪽 끝이 0.564 가 되어 캐비닛 우측면(0.574)을 **10mm 물고 들어간다.**
        /// 그 겹침이 「레버가 캐비닛에 볼트로 물려 있다」의 형상적 근거다.
        /// 판이 컬럼과 같은 폭이면 두 물체가 그냥 나란히 서 있는 것으로 보인다.
        /// </summary>
        public const float LeverBasePlateW = 0.36f;
        public const float LeverBasePlateH = 0.52f;
        public const float LeverBasePlateT = 0.028f;

        /// <summary>베이스 플레이트가 캐비닛 우측면을 물고 들어가는 양(m). 양수여야 결합이다.</summary>
        public static float LeverPlateOverlap
            => MachineRightX - (LeverColumnCenterX - LeverBasePlateW * 0.5f);   // 0.010

        /// <summary>하중을 받는 원형 회전 허브(m).</summary>
        public const float LeverHubRadius = 0.052f;
        public const float LeverHubDepth = 0.070f;

        /// <summary>암이 지나는 가이드 슬롯 폭(m).</summary>
        public const float LeverSlotWidth = 0.042f;

        /// <summary>시작·끝 스토퍼 크기(m).</summary>
        public const float LeverStopperSize = 0.038f;

        /// <summary>잠금핀(m). **이동 경로를 물리적으로 막는 부품이다.**</summary>
        public const float LeverPinRadius = 0.019f;
        public const float LeverPinLength = 0.108f;

        /// <summary>잠긴 상태에서 핀에 부딪히기까지 허용되는 각도(도). 요구 2~4도.</summary>
        public const float LeverLockedTravelDegrees = 3.2f;

        /// <summary>
        /// 허브 높이에서 상단 공통축까지 회전을 올리는 세로 구동 로드의 단면(m).
        /// (<see cref="CommonShaftY"/> 옆 주석에 이 부품이 왜 필요한지 적혀 있다.)
        /// </summary>
        public const float LeverDriveRodWidth = 0.046f;

        /// <summary>구동 로드가 공통축을 만나는 모서리 기어박스의 한 변(m).</summary>
        public const float LeverCornerBoxSize = 0.092f;

        // ── 3×3 챔버 적층 ────────────────────────────────────────────────────

        /// <summary>챔버 적층의 최하단 Y. 하단 외곽 프레임 바로 위.</summary>
        public static float ChamberStackBottomY => MachineBottomY + OuterFrameBand;          // 0.286

        /// <summary>챔버 적층 전체 높이(m). 도어 3장 + 격벽 2.</summary>
        public static float ChamberStackHeight
            => ChamberDoorHeight * 3f + BulkheadHeight * 2f;                                 // 1.500

        /// <summary>챔버 적층의 최상단 Y. 이 위에 상단 프레임과 샤프트 하우징이 온다.</summary>
        public static float ChamberStackTopY => ChamberStackBottomY + ChamberStackHeight;    // 1.786

        /// <summary>
        /// 3×3 격자의 세로 중심. **아래에서 쌓아 올린다.**
        ///
        /// 위에서 내려오게 잡으면 아래쪽 도어가 하단 프레임을 침범하는지가 뺄셈
        /// 네 번 뒤에야 드러난다. 아래에서 쌓으면 침범이 원리적으로 불가능하다.
        /// 직전 판본은 여기에 정비 패널 높이가 끼어 있었다 — 그 패널은 이번에
        /// 삭제됐다(지시: 「각 열 아래에 매달린 기능 불명의 작은 원판」).
        /// </summary>
        public static float WindowGridCenterY => ChamberStackBottomY + ChamberStackHeight * 0.5f;  // 1.036

        /// <summary>칸 (열, 행) 의 중심. 열 0 = 왼쪽, 행 0 = **위**.</summary>
        public static Vector2 WindowCenter(int column, int row)
            => new Vector2(
                BankCenterX(column),
                WindowGridCenterY - (row - 1) * WindowPitchY);

        /// <summary>인접 관찰창 링 사이에 남는 면(m). 구조 프레임이 아니라 **도어 면**이다.</summary>
        public static float WindowGapX => WindowPitchX - WindowRingDiameter;   // 0.198
        public static float WindowGapY => WindowPitchY - WindowRingDiameter;   // 0.130

        // ══════════════════════════════════════════════════════════════════════
        //  §5 실행 레버 — 캐비닛 오른쪽 측면에 결합된 제어 컬럼
        // ══════════════════════════════════════════════════════════════════════
        //
        // 🔴 지시: 「레버는 장치에서 떨어진 별도 오브젝트가 아니다. 통관 장치 오른쪽
        // 측면에 **결합된** 좁은 제어 컬럼이다.」 그래서 간격이 0 이다 — 직전 판본의
        // 0.12m 틈이 「기계적으로 연결되지 않은 고립된 레버」의 절반이었다.
        //
        // 컬럼에 두는 것은 지시가 열거한 여덟뿐이다 —
        //   베이스 플레이트 · 회전 허브 · 슬롯 · 상단 스토퍼 · 하단 스토퍼 ·
        //   솔레노이드 잠금핀 · 붉은 그립 · 상단 경고등.
        // 「레버 옆에 의미 없는 작은 장치, 손잡이, 다이얼을 추가하지 마라」 —
        // 직전 판본의 톱니 7개·스프링 가이드·표지판이 여기서 사라진다.

        /// <summary>레버 컬럼 폭(m). 「좁은 제어 컬럼」.</summary>
        public const float LeverColumnWidth = 0.34f;

        /// <summary>
        /// 레버 컬럼 벽 돌출 깊이(m) = **캐비닛과 같다.**
        ///
        /// 🔴 첫 판본은 0.15 였다(캐비닛 0.26). 독립 평가자가 사선 캡처에서
        /// 「캐비닛의 우측면이 레버 플레이트와 도어 면 **사이에 끼어 있다**」고
        /// 지적했고, 그것이 정확했다 — 컬럼이 110mm 뒤에 물러나 있어서 두 물체
        /// 사이에 공기층이 생겼고, 그래서 「옆에 서 있는 별개 물체」로 읽혔다.
        ///
        /// 지시가 제거를 요구한 「기계적으로 연결되지 않은 고립된 레버」가 그것이다.
        /// 앞면을 맞추면 두 몸통이 하나의 실루엣이 된다.
        /// </summary>
        public static float LeverColumnDepth => MachineDepth;

        /// <summary>
        /// 통관 장치와 레버 컬럼 사이 간격(m). **0 — 직결이다.**
        /// 상수로 남겨 두는 이유는 「0 이어야 한다」를 테스트가 단정할 수 있게 하기 위해서다.
        /// </summary>
        public const float LeverGapFromMachine = 0f;

        /// <summary>레버 회전축 높이(m). 명세 §5 「바닥에서 약 1.25m」.</summary>
        public const float LeverPivotY = 1.25f;

        /// <summary>레버 손잡이 전체 길이(m). 명세 §5.</summary>
        public const float LeverHandleLength = 0.38f;  // 요구 32~42cm

        /// <summary>붉은 원통형 그립 길이(m). 명세 §5.</summary>
        public const float LeverGripLength = 0.18f;    // 요구 14~20cm — 28cm 는 두 손 크기였다

        /// <summary>그립 지름(m). 명세 §5 「약 45mm」.</summary>
        public const float LeverGripDiameter = 0.045f;

        /// <summary>레버 가동 범위(도). 명세 §5 「약 55도」.</summary>
        public const float LeverSwingDegrees = 55f;

        /// <summary>그립 실루엣 분할 수. 명세 §14 「지나치게 매끄러운 원통으로 만들지 않음」.</summary>
        public const int LeverGripSides = 8;

        public static float LeverColumnCenterX
            => MachineRightX + LeverGapFromMachine + LeverColumnWidth * 0.5f;      // +0.744

        /// <summary>컬럼 하단 Y.</summary>
        public const float LeverColumnBottomY = 0.42f;

        /// <summary>
        /// 컬럼 상단 Y = **공통축 높이**. 컬럼이 거기까지 올라가야 구동 로드가
        /// 갈 곳이 있고, 「레버가 캐비닛에 결합돼 있다」가 실루엣으로 읽힌다.
        /// </summary>
        public static float LeverColumnTopY => CommonShaftY;                                       // 1.932

        /// <summary>컬럼 높이(m). **유도값이다** — 하단과 공통축 사이.</summary>
        public static float LeverColumnHeight => LeverColumnTopY - LeverColumnBottomY;             // 1.512
        public static float LeverColumnCenterY => LeverColumnBottomY + LeverColumnHeight * 0.5f;   // 1.176

        // ── 상단 경고등 ───────────────────────────────────────────────────────
        //
        // 지시의 컬럼 부품 목록 마지막 항목이다. **표지판은 목록에 없다** —
        // 「단순 장식용 위험 표지판」이 제거 대상으로 명시됐으므로
        // `LeverSignWidth/Height/CenterY` 셋을 삭제했다. 판을 세워 두고 글자를
        // 나중에 넣겠다는 계획이 세 배치째 이어졌고, 그동안 그것은 기능을 설명할 수
        // 없는 밝은 사각형이었다.

        /// <summary>붉은 원형 경고등 지름(m). 명세 §5 「약 0.22m」.</summary>
        public const float WarningLampDiameter = 0.22f;

        /// <summary>경고등 중심 높이(m). 레버 컬럼 **바로 위**에 얹힌다.</summary>
        public static float WarningLampCenterY
            => LeverColumnTopY + WarningLampDiameter * 0.5f + 0.030f;   // 2.072

        /// <summary>경고등 실루엣 분할 수. 명세 §14.</summary>
        public const int WarningLampSides = 12;

        // ══════════════════════════════════════════════════════════════════════
        //  §6 전력 표시기
        // ══════════════════════════════════════════════════════════════════════

        public const float PowerMeterWidth = 0.72f;
        public const float PowerMeterHeight = 0.50f;
        public const float PowerMeterDepth = 0.14f;

        /// <summary>표시기 중심 높이(m). 명세 §6 「바닥에서 약 1.35m」.</summary>
        public const float PowerMeterCenterY = 1.35f;

        /// <summary>
        /// 표시기 중심 X.
        ///
        /// 명세 §6 은 「레버 오른쪽 벽에」라고만 적는다. 레버 컬럼 우측 끝(1.24)에
        /// 붙이고 폭 0.72 를 두면 오른쪽 끝이 1.96 이 되어 우벽(2.00) 안에 **0.04m**
        /// 여유로 들어간다. 그보다 오른쪽으로 밀 자리가 없다.
        /// </summary>
        public static float PowerMeterCenterX
            => LeverColumnCenterX + LeverColumnWidth * 0.5f + PowerMeterWidth * 0.5f;   // +1.60

        /// <summary>표시기를 읽을 수 있어야 하는 거리(m). 명세 §12.</summary>
        public const float PowerMeterReadDistance = 2.5f;

        // ══════════════════════════════════════════════════════════════════════
        //  §7 오른쪽 보관 선반
        // ══════════════════════════════════════════════════════════════════════

        public const float ShelfLength = 2.7f;
        public const float ShelfDepth = 0.58f;
        public const float ShelfTopHeight = 0.95f;
        public const float ShelfTopThickness = 0.05f;
        public const float ShelfLowerHeight = 0.22f;

        /// <summary>선반이 중앙 플레이 공간 쪽으로 돌출되는 한계(m). 명세 §7.</summary>
        public const float ShelfMaxProtrusion = 0.6f;

        /// <summary>수직 지지대 수. 명세 §7 「네 개 이상」.</summary>
        public const int ShelfLegCount = 4;

        /// <summary>선반 안쪽면 X. 우벽에 밀착한다.</summary>
        public static float ShelfInnerX => WallRightX - ShelfDepth;   // +1.42

        public static float ShelfCenterX => WallRightX - ShelfDepth * 0.5f;   // +1.71
        public const float ShelfCenterZ = 0f;

        /// <summary>「STORAGE」 스텐실과 안전 표지판의 중심 높이(m). 선반 위 벽면.</summary>
        public const float WallSignCenterY = 1.75f;

        // ══════════════════════════════════════════════════════════════════════
        //  §8 바닥
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>중앙 타공 철판 폭(m). 명세 §8 「약 2.7m」.</summary>
        public const float GrateWidth = 2.7f;

        /// <summary>중앙 타공 철판 깊이(m). 명세 §8 「약 3.5m」.</summary>
        public const float GrateDepth = 3.5f;

        /// <summary>철판이 주변 바닥보다 낮은 정도(m). 명세 §8 「15~20mm」.</summary>
        public const float GrateRecess = 0.018f;

        /// <summary>타공 반복 간격(m).</summary>
        public const float GratePerforationPitch = 0.11f;

        /// <summary>타공 구멍 크기(m). 발이 빠지지 않을 크기(명세 §8).</summary>
        public const float GratePerforationSize = 0.035f;

        /// <summary>화물 고정용 타이다운 링 수. 명세 §8 「4~6개」.</summary>
        public const int TieDownRingCount = 6;

        /// <summary>타이다운 링이 바닥 위로 솟는 높이(m). 접힌 상태 — 이동을 방해하지 않는다.</summary>
        public const float TieDownRingHeight = 0.012f;

        /// <summary>
        /// 중앙 철판 둘레의 테두리 폭(m).
        ///
        /// ⚠ **여기에도 명세 내부 모순이 있다.** §8 은 테두리를 「0.35~0.45m」로 적는데,
        /// 실내 폭 4.0 에서 중앙 철판 2.7 을 빼면 좌우 각 **0.65m** 다. 두 수를 동시에
        /// 만족시키는 방법이 없다. 철판 크기(2.7 × 3.5)가 화면을 지배하는 값이므로
        /// 그쪽을 지키고 테두리는 파생값으로 둔다 — X 0.65 · Z 0.55.
        /// `ASSUMPTION_LOG` 에 기록한다.
        /// </summary>
        public static float FloorBorderX => (InteriorWidth - GrateWidth) * 0.5f;   // 0.65
        public static float FloorBorderZ => (InteriorDepth - GrateDepth) * 0.5f;   // 0.55

        // ══════════════════════════════════════════════════════════════════════
        //  §9 벽과 천장 · 케이지 전구
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>케이지 전구 전체 지름(m). 명세 §9 「약 0.28m」.</summary>
        public const float CageLampDiameter = 0.28f;

        /// <summary>케이지 전구 전체 높이(m). 명세 §9 「약 0.32m」.</summary>
        public const float CageLampHeight = 0.32f;

        /// <summary>보호망 세로살 수.</summary>
        public const int CageLampRibs = 6;

        /// <summary>전구 하단 Y. 명세 §9 「짧은 금속 베이스에 직접 고정」 — 펜던트가 아니다.</summary>
        public static float CageLampBottomY => InteriorHeight - CageLampHeight;   // 2.58

        /// <summary>광원(필라멘트) 위치. 케이지 안쪽 중앙.</summary>
        public static Vector3 CageLampFilament
            => new Vector3(0f, InteriorHeight - CageLampHeight * 0.55f, 0f);

        /// <summary>천장 보강빔 수. 명세 §9 「굵은 사각 보강빔」.</summary>
        public const int CeilingBeamCount = 3;
        public const float CeilingBeamWidth = 0.16f;
        public const float CeilingBeamDrop = 0.12f;

        /// <summary>벽 수평 보강 레일 높이(m). 명세 §9 「허리 높이와 천장 가까이」.</summary>
        public const float WallRailLowY = 0.95f;
        public static float WallRailHighY => InteriorHeight - 0.35f;   // 2.55
        public const float WallRailHeight = 0.09f;
        public const float WallRailProtrusion = 0.035f;

        /// <summary>벽 세로 철판 이음매 간격(m).</summary>
        public const float WallPlateSeamPitch = 0.92f;

        // ══════════════════════════════════════════════════════════════════════
        //  §11 조명 — 색온도와 세기
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>천장 케이지 전구 색. 명세 §11 「약 2700~3000K」 · §10 「탁한 황백색」.</summary>
        public static readonly Color CageLampColor = new Color(1.000f, 0.784f, 0.541f, 1f);

        /// <summary>가위문 보조등 색. 명세 §11 「약 3000K」.</summary>
        public static readonly Color GateLampColor = new Color(1.000f, 0.820f, 0.612f, 1f);

        /// <summary>영혼 발광. 명세 §10 「검붉은색 중심에 소량의 주황색」.</summary>
        public static readonly Color SoulEmission = new Color(0.780f, 0.132f, 0.068f, 1f);

        /// <summary>경고등·레버 그립의 바랜 적색. 명세 §10.</summary>
        public static readonly Color FadedRed = new Color(0.392f, 0.098f, 0.078f, 1f);

        /// <summary>철판 기본색. 명세 §9 「매우 어두운 회갈색 또는 올리브가 섞인 차콜색」.</summary>
        public static readonly Color SteelPlate = new Color(0.126f, 0.118f, 0.101f, 1f);

        /// <summary>탁한 적갈색 녹. 명세 §10.</summary>
        public static readonly Color Rust = new Color(0.298f, 0.157f, 0.086f, 1f);

        /// <summary>낡은 크림색 표지판. 명세 §10 「더럽고 누렇게 변색된」.</summary>
        public static readonly Color SignCream = new Color(0.678f, 0.639f, 0.529f, 1f);

        // ══════════════════════════════════════════════════════════════════════
        //  §14 레트로 그래픽 — 텍셀 밀도
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>벽·바닥 텍스처 밀도(px/m). 명세 §14 「128~256px/m」.</summary>
        public const int SurfaceTexelsPerMeter = 192;

        /// <summary>주요 오브젝트(장치·레버) 텍셀 밀도(px/m). 명세 §14 「조금 더 높게」.</summary>
        public const int HeroTexelsPerMeter = 320;

        /// <summary>
        /// 생성 텍스처의 변 길이(px). `AscendSurfaceSynth` 의 주 표면들이 256 이다.
        /// </summary>
        public const int SurfaceTextureSize = 256;

        /// <summary>
        /// 미터당 텍스처 **반복 수**. 메시 생성기의 `uvPerMeter` 에 그대로 들어간다.
        ///
        /// ⚠ **이 둘을 혼동하면 밀도가 조용히 두 배가 된다.** 「미터당 텍셀 수」와
        /// 「미터당 반복 수」는 다른 값이고, 변환은 텍스처 크기로 나누는 것이다.
        /// 첫 판본이 `SurfaceTexelsPerMeter / 128f` 를 넘겨 실제 밀도가 **384 px/m** 였다 —
        /// 명세 §14 의 상한 256 을 1.5배 넘긴 값이고, 그러면 픽셀 그레인이 화면에서
        /// 뭉개져 「로우파이 손그림」이 아니라 그냥 노이즈로 보인다.
        ///
        /// 이 저장소는 타일링 모델로 **세 번** 실패했다(`GRAPHICS_TARGET` §6.3).
        /// 세 번 다 원인이 「어느 단위인가」였다. 그래서 변환을 여기 한 곳에 둔다.
        /// </summary>
        public static float SurfaceUvPerMeter => (float)SurfaceTexelsPerMeter / SurfaceTextureSize;  // 0.75
        public static float HeroUvPerMeter => (float)HeroTexelsPerMeter / SurfaceTextureSize;        // 1.25

        // ══════════════════════════════════════════════════════════════════════
        //  명세 불변식 — 테스트가 이것을 검사한다
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>중앙 이동 공간의 실측 X 폭. 가위문 돌출과 선반 안쪽면 사이.</summary>
        public static float ClearSpanX => ShelfInnerX - (WallLeftX + GateProtrusion);

        /// <summary>중앙 이동 공간의 실측 Z 깊이. 앞벽과 장치 전면 사이.</summary>
        public static float ClearSpanZ => (WallRearZ - MachineDepth) - WallFrontZ;

        /// <summary>선반이 실제로 돌출되는 양(m).</summary>
        public static float ShelfProtrusion => ShelfDepth;

        /// <summary>
        /// 명세를 위반하는 항목을 사람이 읽을 수 있는 문장으로 돌려준다.
        /// 위반이 없으면 빈 배열. 조립기와 테스트가 **같은 함수**를 부른다 —
        /// 조립기만 검사하면 조립기를 안 돌린 사람은 위반을 못 본다.
        /// </summary>
        public static string[] Violations()
        {
            var list = new System.Collections.Generic.List<string>();

            if (ClearSpanX < RequiredClearX)
                list.Add($"§1 중앙 이동 공간 X {ClearSpanX:F2}m < 요구 {RequiredClearX:F2}m");
            if (ClearSpanZ < RequiredClearZ)
                list.Add($"§1 중앙 이동 공간 Z {ClearSpanZ:F2}m < 요구 {RequiredClearZ:F2}m");
            if (ClearSpanX < MinWalkway || ClearSpanZ < MinWalkway)
                list.Add($"§1 최소 통행 폭 {MinWalkway:F2}m 미달");
            if (ShelfProtrusion > ShelfMaxProtrusion + 0.001f)
                list.Add($"§7 선반 돌출 {ShelfProtrusion:F2}m > 한계 {ShelfMaxProtrusion:F2}m");

            // 영웅 오브젝트 명세(2026-08-02) — 폭 45~52% · 높이 60~68%.
            // ⚠ 이 범위를 테스트에서만 고치고 여기를 안 고쳐서 조립기가 한 번 멈췄다.
            // **자기 보고와 검사는 같은 숫자를 봐야 한다** — 그러라고 둘을 이어 놨다.
            float coverage = MachineWallCoverage;
            if (coverage < 0.45f || coverage > 0.52f)
                list.Add($"§4 장치가 벽 폭의 {coverage * 100f:F1}% — 요구 45~52%");
            float hCoverage = MachineHeight / InteriorHeight;
            if (hCoverage < 0.60f || hCoverage > 0.68f)
                list.Add($"§4 장치가 벽 높이의 {hCoverage * 100f:F1}% — 요구 60~68%");

            if (MachineRightX + LeverGapFromMachine + LeverColumnWidth > WallRightX + 0.001f)
                list.Add("§5 레버 컬럼이 우벽을 넘는다");
            if (PowerMeterCenterX + PowerMeterWidth * 0.5f > WallRightX + 0.001f)
                list.Add("§6 전력 표시기가 우벽을 넘는다");
            if (MachineTopY > InteriorHeight - 0.30f)
                list.Add($"§4 장치 상단 여백 {MachineCeilingGap:F2}m — 0.30m 미만");

            // ── 프레임 3단 위계 ──────────────────────────────────────────────
            // 지시 「모든 프레임의 굵기가 같아 보이지 않게 한다 —
            //   외곽 하중 프레임: 가장 두꺼움 / 세로 뱅크 프레임: 중간 / 가로 격벽: 가장 얇음」
            //
            // 부등호 하나로 셋을 묶는다. 누가 한 값만 손대도 여기서 걸린다.
            if (!(OuterFrameBand > BankRibWidth && BankRibWidth > BulkheadHeight))
                list.Add($"§4 프레임 위계가 깨졌다 — 외곽 {OuterFrameBand * 1000f:F0}mm / " +
                         $"뱅크 {BankRibWidth * 1000f:F0}mm / 격벽 {BulkheadHeight * 1000f:F0}mm. " +
                         "굵기가 같으면 하중 경로가 아니라 격자 무늬로 읽힌다");
            if (OuterFrameBand < 0.070f || OuterFrameBand > 0.090f)
                list.Add($"§4 외곽 프레임 {OuterFrameBand * 1000f:F0}mm — 요구 70~90mm");

            // §13 슬롯머신 릴 회귀 방지선. **없앤 것이 아니라 형태를 바꿔 유지한다** —
            // 프레임 위계가 가로·세로 간격을 다르게 만들지만, 비가 1.2 를 넘으면
            // 아홉 창이 열(또는 행)로 뭉쳐 `G-SLOT` 이 측정해 온 그 띠가 된다.
            if (WindowPitchAnisotropy > 1.2f)
                list.Add($"§13 관찰창 간격 이방비 {WindowPitchAnisotropy:F3} > 1.2 " +
                         "— 열 방향으로 뭉쳐 슬롯머신 릴로 읽힌다");

            // 간격만으로는 못 잡는다. 연속성도 잰다 — 이방비 1.133 으로 통과한
            // 판본이 화면에서 세로 띠 셋으로 읽혔고, 원인이 연속성이었다.
            if (LongestVerticalRunRatio > MaxVerticalRunRatio)
                list.Add($"§13 끊기지 않는 세로 부재가 캐비닛 높이의 {LongestVerticalRunRatio:P0} " +
                         $"(상한 {MaxVerticalRunRatio:P0}) — 판이 세로 띠로 갈라져 슬롯머신 릴이 된다");

            // ── 깊이 위계 — **화면의 시각 무게가 폭 위계를 따라야 한다** ──────────
            //
            // 앞에서 뒤로: 외곽(22) > 리브 캡(18) > 리브 몸통(7) > 도어 면(0) > 격벽(−8).
            // 격벽만 음각이라 부호가 뒤집힌다.
            if (!(OuterFrameProud > RibCapProud && RibCapProud > BankRibProud && BankRibProud > 0f))
                list.Add($"§4 깊이 위계가 깨졌다 — 외곽 {OuterFrameProud * 1000f:F0} / " +
                         $"리브 캡 {RibCapProud * 1000f:F0} / 리브 {BankRibProud * 1000f:F0} mm");

            // 🔴 **이것이 `UP-FIX-60` 을 세 라운드 살려 둔 불변식이다.**
            //
            // 직전 판본은 폭만 검사했다(리브 68 > 격벽 30). 폭은 통과했는데
            // 화면에서는 격벽이 가장 강한 분할이었다 — 격벽이 +14mm 앞이라
            // 하이라이트와 그림자를 둘 다 얻었고 리브는 7mm 라 아무것도 못 얻었다.
            // **폭을 재는 불변식은 이 실패를 원리적으로 잡을 수 없다.**
            //
            // 그리고 이건 취향이 아니다. 격벽이 이기면 판이 가로 선반 3단이 되고
            // `VISUAL_SPEC` §3 의 「각 통관 열 = 결과판 한 열」이 형태로 부정된다.
            if (RibCapProud <= BulkheadProud + 0.012f)
                list.Add($"§4c 세로 리브가 가로 격벽을 시각적으로 못 이긴다 — " +
                         $"리브 캡 {RibCapProud * 1000f:F0}mm vs 격벽 {BulkheadProud * 1000f:F0}mm. " +
                         "열 분할이 행 분할보다 약하면 3×3 이 「가로 선반 3단」으로 읽히고 " +
                         "VISUAL_SPEC §3 의 열-결과판 대응이 깨진다");

            // 격벽은 **음각이어야 한다.** 앞으로 내밀면 위 불변식을 통과하고도
            // 다시 가장 밝은 선형 요소가 된다.
            if (BulkheadProud >= 0f)
                list.Add($"§4d 가로 격벽이 음각이 아니다({BulkheadProud * 1000f:F0}mm) — " +
                         "앞으로 나온 가로 부재는 폭이 얼마든 화면에서 가장 강한 분할이 된다");

            // 리브 캡이 행마다 끊기는가. 이으면 `G-SLOT` 릴 띠가 돌아온다.
            if (RibCapSegmentHeight >= ChamberDoorHeight)
                list.Add($"§4e 리브 캡 토막 {RibCapSegmentHeight:F3} 이 도어 높이 " +
                         $"{ChamberDoorHeight:F3} 이상이다 — 캡이 이어져 세로 채널이 된다");

            // 캡은 리브보다 좁아야 한다. 같으면 리브 전체가 앞으로 나온 것과 같다.
            if (RibCapWidth >= BankRibWidth * 0.6f)
                list.Add($"§4f 리브 캡 폭 {RibCapWidth * 1000f:F0}mm 이 리브 " +
                         $"{BankRibWidth * 1000f:F0}mm 의 60% 이상이다 — 리브가 통째로 앞에 나온 것과 같다");

            // 공통축이 모서리 기어박스까지 실제로 닿는가.
            if (CommonShaftRightX < LeverColumnCenterX - 0.001f)
                list.Add($"§4b 공통축 오른쪽 끝 {CommonShaftRightX:F3} 이 " +
                         $"레버 기어박스 {LeverColumnCenterX:F3} 에 닿지 않는다 — 동력 사슬이 끊긴다");
            if (CommonShaftLength < MachineWidth * 0.8f)
                list.Add($"§4b 공통축 길이 {CommonShaftLength:F3} 이 캐비닛 폭에 비해 짧다");

            // ── 3×3 챔버 적층이 캐비닛 안에 정확히 들어가는가 ─────────────────
            // 폭·높이가 유도값이라 원리적으로는 맞지만, 누가 유도를 상수로 되돌리면
            // 조용히 어긋난다. 그 되돌림을 여기서 잡는다.
            Vector2 topLeft = WindowCenter(0, 0);
            Vector2 bottomRight = WindowCenter(2, 2);
            float dx = ChamberDoorWidth * 0.5f;
            float dy = ChamberDoorHeight * 0.5f;
            if (topLeft.x - dx < MachineLeftX + OuterFrameBand - 0.001f)
                list.Add("§4 챔버 도어가 캐비닛 좌측 하중 프레임을 침범한다");
            if (bottomRight.x + dx > MachineRightX - OuterFrameBand + 0.001f)
                list.Add("§4 챔버 도어가 캐비닛 우측 하중 프레임을 침범한다");
            if (topLeft.y + dy > ChamberStackTopY + 0.001f)
                list.Add("§4 챔버 적층이 상단 샤프트 하우징을 침범한다");
            if (bottomRight.y - dy < ChamberStackBottomY - 0.001f)
                list.Add("§4 챔버 적층이 하단 하중 프레임을 침범한다");

            // 도어 개구부의 정사각 모서리가 링 안착 단에 가려지는가.
            // 안 가려지면 밖에서 **사각 구멍**이 보이고, 지시의 「원형 압력창」이 깨진다.
            if (DoorApertureCornerRadius > WindowSeatRadius - 0.002f)
                list.Add($"§4 도어 개구부 대각 {DoorApertureCornerRadius * 1000f:F0}mm 가 " +
                         $"링 안착 단 {WindowSeatRadius * 1000f:F0}mm 를 넘는다 — 사각 모서리가 밖에서 보인다");
            if (DoorApertureHalf * 2f < WindowGlassDiameter * 0.85f)
                list.Add($"§4 도어 개구부 {DoorApertureHalf * 2000f:F0}mm 가 유리 지름의 85% 미만 " +
                         "— 창이 좁아 챔버 안이 안 보인다");

            // 원형 창이 사각 도어 안에 들어가는가. 힌지와 클램프가 살 여백도 남아야 한다.
            if (DoorEdgeMarginX < 0.045f)
                list.Add($"§4 도어 좌우 여백 {DoorEdgeMarginX * 1000f:F0}mm — 힌지·클램프가 링에 겹친다");
            if (DoorEdgeMarginY < 0.030f)
                list.Add($"§4 도어 상하 여백 {DoorEdgeMarginY * 1000f:F0}mm — 링이 격벽을 넘는다");

            // ── 창의 4층이 깊이 축에서 실제로 갈라지는가 ─────────────────────
            if (WindowGlassFrontOffset <= 0f)
                list.Add($"§4 유리가 도어 면보다 뒤에 있다 ({WindowGlassFrontOffset * 1000f:F0}mm) " +
                         "— 링 보어 안에 앉지 않는다");
            if (SoulToChamberBack < 0.03f)
                list.Add($"§4 영혼 뒤 챔버 후면까지 {SoulToChamberBack * 1000f:F0}mm " +
                         "— 배경이 없어 영혼이 뒷벽에 붙은 것으로 보인다");
            if (SoulStandoff > WindowChamberDepth)
                list.Add("§4 영혼이 챔버 후면을 뚫고 나간다");

            // ── 공통 잠금 기구가 성립하는가 ──────────────────────────────────
            if (CamRodWidth >= BankRibWidth)
                list.Add($"§4b 캠 로드 {CamRodWidth * 1000f:F0}mm 가 뱅크 리브 {BankRibWidth * 1000f:F0}mm 이상 " +
                         "— 리브 안을 지날 수 없어 밖으로 노출된다");
            if (CommonShaftRadius * 2f > ShaftHousingHeight)
                list.Add("§4b 공통축이 샤프트 하우징보다 굵다");
            if (CommonShaftY > MachineTopY || CommonShaftY < ChamberStackTopY)
                list.Add("§4b 공통축이 상단 하우징 밖에 있다");
            if (LeverPlateOverlap <= 0f)
                list.Add($"§5 레버 베이스 플레이트가 캐비닛을 물지 않는다 ({LeverPlateOverlap * 1000f:F0}mm) " +
                         "— 「기계적으로 연결되지 않은 고립된 레버」로 읽힌다");
            if (LeverColumnTopY > MachineTopY + 0.001f)
                list.Add("§5 레버 컬럼이 캐비닛보다 높다 — 컬럼이 주인공이 된다");

            if (CameraZ < WallFrontZ + 0.001f)
                list.Add($"§2 기준 카메라 Z {CameraZ:F2} 가 앞벽 밖이다");
            if (EyeHeight > InteriorHeight - 0.5f)
                list.Add("§1 눈높이가 천장에 너무 가깝다");

            // §14 관찰창 실루엣은 12~16각형.
            if (WindowSilhouetteSides < 12 || WindowSilhouetteSides > 16)
                list.Add($"§14 관찰창 분할 {WindowSilhouetteSides} — 요구 12~16");

            return list.ToArray();
        }
    }
}
