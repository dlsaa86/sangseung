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
        //  §4 후면 3×3 영혼 통관 장치
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>장치 전체 폭(m). 명세 §4.</summary>
        public const float MachineWidth = 2.1f;

        /// <summary>장치 전체 높이(m). 명세 §4.</summary>
        public const float MachineHeight = 2.2f;

        /// <summary>장치가 벽에서 실내로 돌출되는 깊이(m). 명세 §4.</summary>
        public const float MachineDepth = 0.22f;

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

        /// <summary>프레임 외곽 폭(m). 명세 §4 「약 100mm」.</summary>
        public const float MachineFrameBand = 0.10f;

        public static float MachineBottomY => MachineBottomGap;
        public static float MachineTopY => MachineBottomGap + MachineHeight;      // 2.40
        public static float MachineCeilingGap => InteriorHeight - MachineTopY;    // 0.50 (위 주석)
        public static float MachineLeftX => MachineCenterX - MachineWidth * 0.5f;   // −1.40
        public static float MachineRightX => MachineCenterX + MachineWidth * 0.5f;  // +0.70

        /// <summary>후면 벽 폭 대비 장치 폭 비율. 명세 §4 「약 52~55%」.</summary>
        public static float MachineWallCoverage => MachineWidth / InteriorWidth;   // 0.525

        // ── 원형 관찰창 모듈 ──────────────────────────────────────────────────

        /// <summary>내부 유리 지름(m). 명세 §4 「0.30~0.32m」 — 상한을 쓴다.</summary>
        public const float WindowGlassDiameter = 0.32f;

        /// <summary>모듈이 장치 전면에서 더 돌출되는 깊이(m). 명세 §4 「약 0.10m」.</summary>
        public const float WindowProtrusion = 0.10f;

        /// <summary>
        /// 금속 링 두께(m). 명세 §4 는 「약 65mm」.
        ///
        /// 🔴 **여기서 명세가 저장소의 기존 판독 규칙과 충돌한다.**
        /// <c>PortholeMesh.MaxBezelToOpeningDiameter = 0.18</c> 은 베젤 폭이 개구부
        /// **지름의 18%** 를 넘지 못하게 막는다. 그 규칙은 취향이 아니라
        /// **16라운드 연속 「베젤이 심볼을 덮어 결과판이 안 읽힌다」** 판정에서 나왔고,
        /// `PortholeMesh.Clamped()` 가 인자를 강제로 그 안으로 민다.
        ///
        /// 개구부 0.32 에서 상한은 0.0576m 다. 명세의 65mm 를 쓰면 클램프가 조용히
        /// 깎아 **상수와 실제 형상이 달라진다** — 이 저장소가 반복해서 당한
        /// 「기록과 실물이 다르다」의 정확한 형태다. 그래서 상수 쪽을 상한에 맞춘다.
        ///
        /// 결과: 링 두께 65 → **57.6mm**, 외부 링 지름 460 → **435.2mm** (−5.4%).
        /// `ASSUMPTION_LOG` 에 기록한다. 명세를 어긴 것이 맞고, 어긴 이유가 명세 §2
        /// 「시각적 우선순위 1 = 붉게 빛나는 3×3 통관 장치」를 지키기 위해서다.
        /// </summary>
        public const float WindowRingBand = 0.0576f;

        /// <summary>외부 금속 링 지름(m). 유리 + 링 두께 ×2 에서 **유도한다.</summary>
        public const float WindowRingDiameter = WindowGlassDiameter + WindowRingBand * 2f;   // 0.4352

        /// <summary>링 둘레 고정 볼트 수. 명세 §4 「8개」.</summary>
        public const int WindowBoltCount = 8;

        /// <summary>
        /// 관찰창 중심 간격(m) — 가로·세로 **동일**.
        ///
        /// 🔴 **명세 §4 는 창 간격에 대해 세 가지를 말하고 그중 하나가 나머지 둘과
        /// 모순한다.** 셋을 그대로 옮기면 이렇다.
        ///
        ///   (a) 「가로 중심 간격 약 0.61m · 세로 중심 간격 약 0.52m」
        ///   (b) 「각 관찰창 사이에는 폭 80~100mm 의 철제 프레임이 보인다」
        ///   (c) 「모든 원형 모듈의 크기와 **간격은 동일**해야 한다」
        ///
        /// 링 지름이 0.46 이므로 (a) 가 만드는 실제 프레임 폭은 가로 **150mm** ·
        /// 세로 **60mm** 다. (b) 의 80~100mm 를 **양쪽 다** 벗어나고, 가로 ≠ 세로라
        /// (c) 도 위반한다. (b)(c) 는 서로 정합적이고 (a) 만 어긋난다.
        ///
        /// 그리고 (a) 를 채택하면 **열 간격이 행 간격의 2.5배**가 되어 아홉 창이
        /// 「세로로 늘어선 세 덩어리」로 뭉친다. 그것이 정확히 이 저장소가
        /// `G-SLOT` 축으로 측정해 온 **「슬롯머신 릴 띠」**이고, 같은 명세 §13 이
        /// 「카지노나 슬롯머신처럼 보이는 장식」을 금지한다. 즉 (a) 는 §13 과도 충돌한다.
        ///
        /// **(b)(c)(§13) 셋을 만족시키는 유일한 값을 쓴다** — 링 0.46 + 프레임 0.09
        /// = 간격 **0.55, 가로·세로 동일**. 프레임 폭 90mm 는 (b) 범위의 중앙이다.
        /// `ASSUMPTION_LOG` 에 기록한다.
        /// </summary>
        /// 값은 링 지름(0.4352) + 프레임 90mm 에서 유도했다.
        public const float WindowPitch = 0.525f;

        public const float WindowPitchX = WindowPitch;
        public const float WindowPitchY = WindowPitch;

        /// <summary>
        /// 관찰창 실루엣 분할 수. 명세 §14 「완전한 원보다 12~16각형」.
        ///
        /// **4의 배수여야 한다** — <c>PortholeMesh.Clamped()</c> 가 그렇게 강제한다.
        /// 셀 사각형의 네 모서리가 표본 각도에 걸려야 이웃 셀의 공유 모서리가 같은
        /// 점 집합이 되고, 그래야 껍질이 닫힌다. 그래서 12~16 중 쓸 수 있는 값은
        /// 12 와 16 뿐이고, 더 각진 12 를 고른다(명세 §14 「매끄러운 곡면을 피함」).
        /// </summary>
        public const int WindowSilhouetteSides = 12;

        /// <summary>
        /// 3×3 격자의 세로 중심.
        ///
        /// **정비 패널 위에서 쌓아 올린다.** 위(장치 상단)에서 내려오게 잡으면 아래쪽
        /// 링이 정비 패널을 침범하는지가 뺄셈 네 번 뒤에야 드러난다. 아래에서 쌓으면
        /// 침범이 원리적으로 불가능하고, 남는 여백이 상단으로 간다 — 그 여백은
        /// 프레임이라 비어 보이지 않는다.
        ///
        /// 실측: 0.20(하단 간격) + 0.10(프레임) + 0.42(정비 패널) + 0.78(격자 반높이)
        ///     = **1.50**. 격자 상단 링 가장자리 2.28 ≤ 장치 상단 프레임 안쪽 2.30.
        /// </summary>
        public static float WindowGridCenterY
            => MachineBottomY + MachineFrameBand + ServicePanelHeight
             + WindowPitchY + WindowRingDiameter * 0.5f;

        /// <summary>칸 (열, 행) 의 중심. 열 0 = 왼쪽, 행 0 = **위**.</summary>
        public static Vector2 WindowCenter(int column, int row)
            => new Vector2(
                MachineCenterX + (column - 1) * WindowPitchX,
                WindowGridCenterY - (row - 1) * WindowPitchY);

        /// <summary>인접 관찰창 사이에 남는 프레임 폭(m). 명세 §4 「80~100mm」.</summary>
        public static float WindowGapX => WindowPitchX - WindowRingDiameter;   // 0.15
        public static float WindowGapY => WindowPitchY - WindowRingDiameter;   // 0.06

        // ── 하단 정비 패널 ────────────────────────────────────────────────────

        /// <summary>정비 패널 높이(m). 명세 §4 「약 0.42m」.</summary>
        public const float ServicePanelHeight = 0.42f;

        /// <summary>정비 패널 수. 명세 §4 「가로로 나뉜 3개」 — 위쪽 세로 열과 정렬.</summary>
        public const int ServicePanelCount = 3;

        /// <summary>정비 패널 중심 Y. 장치 하단 프레임 바로 위.</summary>
        public static float ServicePanelCenterY => MachineBottomY + MachineFrameBand + ServicePanelHeight * 0.5f;

        // ══════════════════════════════════════════════════════════════════════
        //  §5 실행 레버
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>레버 컬럼 폭(m). 명세 §5.</summary>
        public const float LeverColumnWidth = 0.42f;

        /// <summary>레버 컬럼 높이(m). 명세 §5.</summary>
        public const float LeverColumnHeight = 1.55f;

        /// <summary>레버 컬럼 벽 돌출 깊이(m). 명세 §5.</summary>
        public const float LeverColumnDepth = 0.16f;

        /// <summary>통관 장치와 레버 컬럼 사이 간격(m). 명세 §5.</summary>
        public const float LeverGapFromMachine = 0.12f;

        /// <summary>레버 회전축 높이(m). 명세 §5 「바닥에서 약 1.25m」.</summary>
        public const float LeverPivotY = 1.25f;

        /// <summary>레버 손잡이 전체 길이(m). 명세 §5.</summary>
        public const float LeverHandleLength = 0.42f;

        /// <summary>붉은 원통형 그립 길이(m). 명세 §5.</summary>
        public const float LeverGripLength = 0.28f;

        /// <summary>그립 지름(m). 명세 §5 「약 45mm」.</summary>
        public const float LeverGripDiameter = 0.045f;

        /// <summary>레버 가동 범위(도). 명세 §5 「약 55도」.</summary>
        public const float LeverSwingDegrees = 55f;

        /// <summary>그립 실루엣 분할 수. 명세 §14 「지나치게 매끄러운 원통으로 만들지 않음」.</summary>
        public const int LeverGripSides = 8;

        public static float LeverColumnCenterX
            => MachineRightX + LeverGapFromMachine + LeverColumnWidth * 0.5f;      // +1.03

        /// <summary>컬럼 하단 Y. 회전축이 컬럼 안에서 위쪽 절반에 오게 놓는다.</summary>
        public const float LeverColumnBottomY = 0.45f;
        public static float LeverColumnCenterY => LeverColumnBottomY + LeverColumnHeight * 0.5f;   // 1.225
        public static float LeverColumnTopY => LeverColumnBottomY + LeverColumnHeight;             // 2.00

        // ── 경고등 · 표지판 ───────────────────────────────────────────────────

        /// <summary>붉은 원형 경고등 지름(m). 명세 §5 「약 0.22m」.</summary>
        public const float WarningLampDiameter = 0.22f;

        /// <summary>경고등 중심 높이(m). 레버 컬럼 위.</summary>
        public const float WarningLampCenterY = 2.42f;

        /// <summary>경고등 실루엣 분할 수. 명세 §14.</summary>
        public const int WarningLampSides = 12;

        /// <summary>「OVERHARVEST / CRITICAL SYSTEM」 표지판 크기(m).</summary>
        public const float LeverSignWidth = 0.38f;
        public const float LeverSignHeight = 0.16f;
        public const float LeverSignCenterY = 2.12f;

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

            float coverage = MachineWallCoverage;
            if (coverage < 0.52f || coverage > 0.55f)
                list.Add($"§4 장치가 후면 벽의 {coverage * 100f:F1}% — 요구 52~55%");

            if (MachineRightX + LeverGapFromMachine + LeverColumnWidth > WallRightX + 0.001f)
                list.Add("§5 레버 컬럼이 우벽을 넘는다");
            if (PowerMeterCenterX + PowerMeterWidth * 0.5f > WallRightX + 0.001f)
                list.Add("§6 전력 표시기가 우벽을 넘는다");
            if (MachineTopY > InteriorHeight - 0.30f)
                list.Add($"§4 장치 상단 여백 {MachineCeilingGap:F2}m — 0.30m 미만");

            if (WindowGapX < 0.08f || WindowGapX > 0.10f)
                list.Add($"§4 관찰창 가로 프레임 폭 {WindowGapX * 1000f:F0}mm — 요구 80~100mm");
            if (WindowGapY < 0.08f || WindowGapY > 0.10f)
                list.Add($"§4 관찰창 세로 프레임 폭 {WindowGapY * 1000f:F0}mm — 요구 80~100mm");

            // §4 「모든 원형 모듈의 크기와 간격은 동일해야 한다」.
            // 이 단정이 있어야 누가 「가로만 넓히자」로 되돌릴 때 걸린다 —
            // 그 되돌림이 곧 §13 이 금지한 슬롯머신 릴 띠를 다시 만든다.
            if (!Mathf.Approximately(WindowPitchX, WindowPitchY))
                list.Add($"§4 관찰창 간격이 가로 {WindowPitchX:F3} · 세로 {WindowPitchY:F3} 로 다르다 " +
                         "— 열 방향으로 뭉쳐 §13 이 금지한 슬롯머신 릴로 읽힌다");

            // 3×3 격자가 장치 프레임 안에 들어가는가.
            Vector2 topLeft = WindowCenter(0, 0);
            Vector2 bottomRight = WindowCenter(2, 2);
            float r = WindowRingDiameter * 0.5f;
            if (topLeft.x - r < MachineLeftX + MachineFrameBand - 0.001f)
                list.Add("§4 관찰창 격자가 장치 좌측 프레임을 침범한다");
            if (bottomRight.x + r > MachineRightX - MachineFrameBand + 0.001f)
                list.Add("§4 관찰창 격자가 장치 우측 프레임을 침범한다");
            if (topLeft.y + r > MachineTopY - MachineFrameBand + 0.001f)
                list.Add("§4 관찰창 격자가 장치 상단 프레임을 침범한다");
            if (bottomRight.y - r < ServicePanelCenterY + ServicePanelHeight * 0.5f - 0.001f)
                list.Add("§4 관찰창 격자가 정비 패널과 겹친다");

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
