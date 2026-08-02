using UnityEngine;

namespace Ascend.Prototype.Art
{
    /// <summary>
    /// 「원형 현창 3×3」 장치벽의 인자. 전부 미터·개수이고, 기본값은
    /// <see cref="PortholeMesh.DefaultSpec"/> 에 있다.
    ///
    /// 값 타입이지만 **필드가 공개돼 있다.** 인자가 20개가 넘어서 생성자로 받으면
    /// 호출부가 읽히지 않는다. 대신 모든 빌더가 첫 줄에서 <see cref="Clamped"/> 를 부르므로
    /// 잘못된 값이 그대로 형상이 되는 경로가 없다 — 그리고 <see cref="Clamped"/> 가
    /// 「가로 리브 ≥ 세로 리브」·「베젤이 개구부를 침범하지 않는다」 같은 **판독 규칙**을
    /// 코드로 강제하는 자리다.
    /// </summary>
    public struct PortholeSpec
    {
        // ── 격자 ────────────────────────────────────────────────────────────
        /// <summary>셀 피치(m). **가로·세로가 같다** — 정사각 격자다.</summary>
        public float CellPitch;
        /// <summary>개구부 반지름(m).</summary>
        public float OpeningRadius;
        /// <summary>개구부 다각형 변 수. **4의 배수**여야 셀 사각형의 네 모서리가 정점에 걸린다.</summary>
        public int OpeningSides;

        // ── 베젤 ────────────────────────────────────────────────────────────
        /// <summary>베젤 링의 폭(m). 개구부 **바깥으로** 자란다 — 안쪽을 덮지 않는다.</summary>
        public float BezelWidth;
        /// <summary>베젤이 판 앞으로 나오는 높이(m).</summary>
        public float BezelProtrusion;
        /// <summary>베젤 하나에 도는 볼트 수. 0 이면 없음.</summary>
        public int BoltsPerBezel;

        // ── 판·테두리 ───────────────────────────────────────────────────────
        public float PlateThickness;
        /// <summary>격자 바깥 테두리 폭(m).</summary>
        public float FrameMargin;
        /// <summary>테두리가 판 앞으로 나오는 두께(m).</summary>
        public float FrameDepth;
        /// <summary>테두리 모따기(m). 0 이면 상자 4개 = 48 삼각형, 0 초과면 176.</summary>
        public float FrameBevel;
        /// <summary>테두리 밴드 하나에 박히는 리벳 수.</summary>
        public int RivetsPerBand;

        // ── 리브 ────────────────────────────────────────────────────────────
        /// <summary>**세로** 리브 굵기(m). 가로는 여기에 <see cref="HorizontalRibBoost"/> 를 곱한다.</summary>
        public float RibWidth;
        /// <summary>가로 리브가 세로 리브보다 몇 배 굵은가. **1 미만은 허용되지 않는다.**</summary>
        public float HorizontalRibBoost;
        /// <summary>
        /// 가로 리브 돌출(m). 세로 리브는 이것의
        /// <see cref="PortholeMesh.VerticalRibDepthFactor"/> 만큼만 나온다.
        /// </summary>
        public float RibDepth;
        /// <summary>세로 리브 한 조각이 한 행에서 차지하는 비율(0~1 미만). **1 미만이라야 끊긴다.**</summary>
        public float VerticalRibSpan;

        // ── 하단 해치 ───────────────────────────────────────────────────────
        public int HatchCount;
        public float HatchRadius;
        /// <summary>격자 아래로 더 내려가는 해치 띠 높이(m).</summary>
        public float HatchBandHeight;

        // ── 우물 ────────────────────────────────────────────────────────────
        public float WellDepth;
        public float WellWallThickness;
        public float WellFloorThickness;

        // ── 유리 ────────────────────────────────────────────────────────────
        public float GlassThickness;
        public int GlassSides;
        /// <summary>볼록 단 수. 1 = 얕은 원뿔대(28 삼각형), 2 = 두 단(56).</summary>
        public int GlassTiers;

        // ── 파생값 (검증과 씬 배치가 이걸 읽는다) ────────────────────────────
        public float HorizontalRibWidth => RibWidth * HorizontalRibBoost;
        public float VerticalRibWidth => RibWidth;
        public float VerticalRibSegmentLength => CellPitch * VerticalRibSpan;
        public float BezelOuterRadius => OpeningRadius + BezelWidth;
        /// <summary>베젤 폭 ÷ 개구부 **지름**. 판독 상한 <see cref="PortholeMesh.MaxBezelToOpeningDiameter"/>.</summary>
        public float BezelToOpeningDiameter => BezelWidth / (OpeningRadius * 2f);

        /// <summary>
        /// 형상이 되기 전에 인자를 규칙 안으로 밀어 넣는다. 순수 함수다.
        ///
        /// 여기서 막는 것들은 전부 **화면에서만 드러나는 실패**다 —
        /// 베젤이 심볼을 덮거나, 세로 리브가 이어져 릴로 읽히거나, 리브가 개구부를 침범하거나,
        /// 우물이 판 뒤로 뚫고 나가는 것. 컴파일러도 예외도 이걸 잡지 못한다.
        /// </summary>
        public PortholeSpec Clamped()
        {
            PortholeSpec s = this;

            s.CellPitch = Mathf.Max(0.12f, s.CellPitch);
            float half = s.CellPitch * 0.5f;

            // 변 수는 4의 배수여야 한다. 셀 사각형의 **네 모서리**가 표본 각도에 걸려야
            // 이웃 셀의 공유 모서리가 같은 점 집합이 되고, 그래야 껍질이 닫힌다.
            s.OpeningSides = Mathf.Clamp(s.OpeningSides, 8, 16);
            s.OpeningSides -= s.OpeningSides % 4;

            // 개구부 + 베젤이 셀 반폭 안에 들어와야 리브가 설 자리가 남는다.
            s.OpeningRadius = Mathf.Clamp(s.OpeningRadius, 0.02f, half - 0.030f);

            float bezelCap = PortholeMesh.MaxBezelToOpeningDiameter * (s.OpeningRadius * 2f);
            float bezelRoom = half - s.OpeningRadius - 0.005f;
            s.BezelWidth = Mathf.Clamp(s.BezelWidth, 0f, Mathf.Min(bezelCap, bezelRoom));
            s.BezelProtrusion = Mathf.Clamp(s.BezelProtrusion, 0f, 0.12f);
            s.BoltsPerBezel = Mathf.Clamp(s.BoltsPerBezel, 0, 8);

            s.PlateThickness = Mathf.Clamp(s.PlateThickness, 0.02f, 0.30f);
            s.FrameMargin = Mathf.Clamp(s.FrameMargin, 0.04f, s.CellPitch);
            s.FrameDepth = Mathf.Clamp(s.FrameDepth, 0.008f, 0.20f);
            s.FrameBevel = Mathf.Clamp(s.FrameBevel, 0f, Mathf.Min(s.FrameMargin, s.FrameDepth) * 0.45f);
            s.RivetsPerBand = Mathf.Clamp(s.RivetsPerBand, 0, 16);

            // **이 두 줄이 이 파일의 목적이다.** 가로가 세로보다 얇아지는 순간
            // 3×3 은 「세로 릴 3개」로 읽힌다 — 16차 독립 평가가 11라운드 연속으로 지적한 것.
            s.HorizontalRibBoost = Mathf.Max(1f, s.HorizontalRibBoost);
            // 세로 리브는 한 행을 다 채우지 못한다. 채우면 세 조각이 이어져 기둥이 된다.
            s.VerticalRibSpan = Mathf.Clamp(s.VerticalRibSpan, 0.2f, 0.9f);

            // 가로 리브의 **반폭**이 개구부·베젤과 셀 경계 사이의 틈을 넘으면 안 된다.
            float ribRoom = half - (s.OpeningRadius + s.BezelWidth);
            float maxRib = Mathf.Max(0.006f, 2f * ribRoom / s.HorizontalRibBoost - 0.002f);
            s.RibWidth = Mathf.Clamp(s.RibWidth, 0.006f, maxRib);
            s.RibDepth = Mathf.Clamp(s.RibDepth, 0.004f, 0.10f);

            s.HatchCount = Mathf.Clamp(s.HatchCount, 0, 6);
            s.HatchBandHeight = Mathf.Clamp(s.HatchBandHeight, 0f, s.CellPitch * 2f);
            float hatchRoomY = Mathf.Max(0.01f, (s.HatchBandHeight - 0.05f) * 0.5f);
            float hatchRoomX = s.HatchCount > 0
                ? Mathf.Max(0.01f, (3f * s.CellPitch) / (2f * s.HatchCount) - 0.02f)
                : 0.01f;
            s.HatchRadius = Mathf.Clamp(s.HatchRadius, 0.01f, Mathf.Min(hatchRoomY, hatchRoomX));

            // 우물이 판 뒤로 뚫고 나가면 벽 안쪽이 비쳐 보인다.
            float wellRoom = s.PlateThickness + s.BezelProtrusion - 0.002f;
            s.WellDepth = Mathf.Clamp(s.WellDepth, 0.010f, wellRoom);
            s.WellWallThickness = Mathf.Clamp(s.WellWallThickness, 0.003f,
                                              (s.OpeningRadius - PortholeMesh.WellBoreClearance) * 0.35f);
            s.WellFloorThickness = Mathf.Clamp(s.WellFloorThickness, 0.003f, s.WellDepth * 0.5f);

            s.GlassSides = Mathf.Clamp(s.GlassSides, 6, 16);
            s.GlassTiers = Mathf.Clamp(s.GlassTiers, 1, 3);
            s.GlassThickness = Mathf.Clamp(s.GlassThickness, 0.004f,
                                           Mathf.Max(0.006f, s.BezelProtrusion + s.PlateThickness * 0.5f));
            return s;
        }
    }

    /// <summary>
    /// **원형 현창 3×3 장치벽.** `docs/VISUAL_REFERENCE.md` §1 의 뒷벽 — 리벳 캐비닛에
    /// 둥근 창 아홉 개가 박혀 있고 각 창 안에 발광체가 하나씩 앉는다.
    ///
    /// ## 왜 이 형상인가 — 「릴」을 「관」으로 바꾸는 것이 목적이다
    ///
    /// 16차 독립 시각 평가에서 「슬롯머신처럼 보인다」가 **11라운드 연속 스타일 1~2점의
    /// 유일한 원인**이었다. 직전 배치가 셀을 잇는 막대를 없애 페이라인은 사라졌지만
    /// 평가자가 남은 것을 정확히 짚었다 — 「남은 것은 막대가 아니라 **세로 발광 릴 3개**다」.
    ///
    /// 색과 재질로는 이걸 못 고친다. **형상이 세로 스트립이면 세로 스트립으로 읽힌다.**
    /// 그래서 이 클래스는 다섯 가지를 구조로 못 박는다.
    ///
    /// | # | 무엇을 했는가 | 왜 그것이 「릴」을 죽이는가 |
    /// |---|---|---|
    /// | 1 | 칸이 **원형 개구부**다 | 원에는 축이 없다. 세로로 긴 사각 칸이 없으면 「띠」가 생기지 않는다 |
    /// | 2 | **가로 리브가 세로보다 굵다** (<see cref="PortholeSpec.HorizontalRibBoost"/> ≥ 1, 기본 1.7) | 눈은 굵은 선을 먼저 따라간다. 굵은 쪽이 가로면 시선이 **행**을 훑는다 |
    /// | 3 | **가로 리브는 캐비닛 폭 전체를 끊김 없이 지나고, 세로 리브는 행마다 끊긴다** | 이어진 선만 「기둥」이 된다. 세로가 세 토막이면 열이 하나의 물체로 묶이지 않는다 |
    /// | 4 | 가로 리브가 세로보다 **더 튀어나온다** (돌출 1 : 0.62) | 그림자 줄이 가로로 떨어진다. 조명이 방향을 한 번 더 말해 준다 |
    /// | 5 | 하단에 **원형 해치 3개**가 가로로 늘어선다 | 화면 아래쪽에 또 하나의 가로 띠. 세로 3열 대신 **가로 4단**이 지배 리듬이 된다 |
    ///
    /// 이 다섯 가지는 취향이 아니라 <see cref="PortholeSpec.Clamped"/> 가 강제하는 규칙이고,
    /// `PortholeMeshTests` 가 **기하에서 직접 재서** 반증한다 (리브 굵기비, 세로 리브의 끊김,
    /// 개구부가 실제로 비어 있는가).
    ///
    /// ## 세 메시로 나뉜다 — 머티리얼이 다르기 때문이다
    ///
    /// | 메시 | 머티리얼 | 왜 따로인가 |
    /// |---|---|---|
    /// | <see cref="Panel"/> | 불투명 금속 | 캐비닛 본체 |
    /// | <see cref="WellCluster"/> | 불투명(어두운) + 발광 수용 | 우물 안쪽은 본체와 다른 명도여야 창이 「구멍」으로 읽힌다 |
    /// | <see cref="GlassCluster"/> | **투명 · 알파 0.25 · ZWrite off** | 씬 소유자가 확인한 값이다. 불투명으로 바꾸면 안쪽 심볼이 사라진다 |
    ///
    /// ## 좌표 규약
    ///
    /// 원점은 **3×3 격자의 중심**이고 벽 접촉면이 z = 0, 밖이 +Z 다. 캐비닛은 격자보다
    /// 아래로 더 내려간다(해치 띠). 격자 중심을 원점으로 잡은 이유는 씬 소유자가 배치할 때
    /// 맞춰야 하는 것이 캐비닛 중심이 아니라 **눈높이에 오는 격자**이기 때문이다.
    /// <see cref="CellCenter"/> · <see cref="CellMouth"/> · <see cref="PanelSize"/> 가
    /// 그 좌표계를 그대로 돌려준다.
    ///
    /// ⚠ <c>localScale</c> 을 쓰지 마라. UV 가 미터에서 나오므로 스케일하면 텍셀 밀도가 어긋난다.
    /// ⚠ <see cref="MeshBuildPhase.Close"/> 이후에는 <c>*Mesh</c> 판이 예외로 막힌다.
    /// </summary>
    public static class PortholeMesh
    {
        /// <summary>
        /// 베젤 폭 ÷ 개구부 지름의 **상한**. 이 값을 넘으면 링이 안쪽 심볼을 잡아먹기 시작한다.
        /// 판독성이 스타일보다 우선한다 — 두꺼운 링은 순손실이다.
        /// </summary>
        public const float MaxBezelToOpeningDiameter = 0.18f;

        /// <summary>우물 바깥면이 판의 보어에서 물러나는 간격(m). 이 틈이 입구의 그림자 선이 된다.</summary>
        public const float WellBoreClearance = 0.002f;

        /// <summary>유리가 보어에서 물러나는 간격(m). 0 이면 z-파이팅이 난다.</summary>
        public const float GlassBoreClearance = 0.006f;

        /// <summary>
        /// 세로 리브 돌출 ÷ 가로 리브 돌출. **1 미만이라야 가로가 앞선다** —
        /// 그림자 줄이 가로로 떨어지고, 조명이 「행」을 한 번 더 말해 준다.
        /// 검증도 이 상수로 세로 리브의 앞면 평면을 찾는다(테스트가 숫자를 베껴 쓰지 않는다).
        /// </summary>
        public const float VerticalRibDepthFactor = 0.55f;

        /// <summary>메시 하나의 삼각형 상한. 검증이 이 값으로 회귀를 잡는다.</summary>
        public const int PanelTriangleBudget = 1500;
        public const int WellClusterTriangleBudget = 640;
        public const int GlassClusterTriangleBudget = 340;

        /// <summary>
        /// 기본 인자. 캐비닛 1.60 × 1.94 m, 개구부 지름 0.31 m, 정사각 격자 0.44 m.
        ///
        /// 격자를 **정사각**으로 잡은 것은 `DEVICE_DESIGN_SPEC.md` §3.1 의 지적 때문이다 —
        /// 현재 씬은 열 0.50 · 행 0.40 이라 대각선이 45° 가 아니라 38.7° 로 기울고,
        /// 그것이 「같은 저항체 3개를 빠르게 셀 수 있는가」를 직접 해친다.
        /// </summary>
        public static PortholeSpec DefaultSpec => new PortholeSpec
        {
            CellPitch = 0.44f,
            OpeningRadius = 0.155f,
            OpeningSides = 8,

            BezelWidth = 0.030f,
            BezelProtrusion = 0.030f,
            BoltsPerBezel = 3,

            // 판이 두꺼운 이유는 「튼튼해 보이려고」가 아니다. **우물이 발광체를 담아야 한다.**
            // 얇게 잡으면 창이 오목해 보이기만 하고 구체를 넣을 자리가 없어서, 씬 소유자가
            // 심볼을 유리 밖으로 밀어내거나 납작하게 눌러야 한다 — 어느 쪽이든 판독이 깨진다.
            // <see cref="SymbolFitSize"/> 가 실제로 들어가는 크기를 돌려준다.
            PlateThickness = 0.135f,
            FrameMargin = 0.140f,
            FrameDepth = 0.050f,
            FrameBevel = 0.012f,
            RivetsPerBand = 5,

            RibWidth = 0.038f,
            HorizontalRibBoost = 1.70f,
            RibDepth = 0.024f,
            VerticalRibSpan = 0.55f,

            HatchCount = 3,
            HatchRadius = 0.105f,
            HatchBandHeight = 0.340f,

            WellDepth = 0.150f,
            WellWallThickness = 0.008f,
            WellFloorThickness = 0.010f,

            GlassThickness = 0.020f,
            GlassSides = 8,
            GlassTiers = 1,
        };

        // ══ 좌표 ═════════════════════════════════════════════════════════════

        /// <summary>
        /// 셀 중심(판 로컬, z = 0). <paramref name="col"/> 0→2 가 왼쪽→오른쪽,
        /// <paramref name="row"/> 0→2 가 **위→아래**다 — `SpinBoardView` 의 `Cell_0` 이 위인 규약과 같다.
        /// </summary>
        public static Vector3 CellCenter(PortholeSpec spec, int col, int row)
        {
            PortholeSpec s = spec.Clamped();
            return new Vector3((col - 1) * s.CellPitch, (1 - row) * s.CellPitch, 0f);
        }

        /// <summary>
        /// 셀의 **입 평면** 중심 — 베젤 안쪽 립이 있는 z. 우물과 유리가 여기 앉는다.
        /// </summary>
        public static Vector3 CellMouth(PortholeSpec spec, int col, int row)
        {
            PortholeSpec s = spec.Clamped();
            Vector3 c = CellCenter(s, col, row);
            c.z = s.PlateThickness + s.BezelProtrusion;
            return c;
        }

        /// <summary>
        /// 우물 안에 **실제로 들어가는** 발광체의 최대 크기 — x = 가로 지름, y = 앞뒤 깊이.
        ///
        /// 이 함수가 있는 이유: 현재 씬의 심볼 27개는 0.15~0.17 m 짜리이고, 그건 「평평한
        /// 칸 앞에 붙은 판」을 전제로 정해진 값이다. 오목한 창 안에 같은 크기를 넣으면
        /// 유리를 뚫고 나온다. 씬 소유자가 눈으로 맞추다 실패하는 대신 여기서 읽는다.
        /// </summary>
        public static Vector2 SymbolFitSize(PortholeSpec spec)
        {
            PortholeSpec s = spec.Clamped();
            float dia = 2f * (s.OpeningRadius - WellBoreClearance - s.WellWallThickness) - 0.010f;
            float glassBack = s.PlateThickness + s.BezelProtrusion - s.GlassThickness;
            float depth = glassBack - WellFloorZ(s) - 0.005f;
            return new Vector2(Mathf.Max(0f, dia), Mathf.Max(0f, depth));
        }

        /// <summary>
        /// 높이 <paramref name="height"/> 의 심볼을 우물 **바닥에 앉히는** 판 로컬 좌표.
        /// 심볼의 피벗이 중심이라는 전제다(구·큐브·캡슐 프리미티브가 그렇다).
        /// </summary>
        public static Vector3 SymbolSeat(PortholeSpec spec, int col, int row, float height)
        {
            PortholeSpec s = spec.Clamped();
            Vector3 p = CellCenter(s, col, row);
            p.z = WellFloorZ(s) + height * 0.5f;
            return p;
        }

        /// <summary>캐비닛의 경계 상자 (가로, 세로, 가장 앞 z). 씬 배치와 검증이 같이 읽는다.</summary>
        public static Vector3 PanelSize(PortholeSpec spec)
        {
            PortholeSpec s = spec.Clamped();
            float w = 3f * s.CellPitch + 2f * s.FrameMargin;
            float h = 3f * s.CellPitch + 2f * s.FrameMargin + s.HatchBandHeight;
            return new Vector3(w, h, PanelFrontZ(s));
        }

        /// <summary>격자 중심 기준 캐비닛 위·아래 끝(m). 위는 양수, 아래는 음수다.</summary>
        public static void PanelVerticalExtent(PortholeSpec spec, out float bottom, out float top)
        {
            PortholeSpec s = spec.Clamped();
            float gridHalf = 1.5f * s.CellPitch;
            top = gridHalf + s.FrameMargin;
            bottom = -(gridHalf + s.FrameMargin + s.HatchBandHeight);
        }

        private static float PanelFrontZ(in PortholeSpec s)
        {
            float z = s.PlateThickness + s.BezelProtrusion;
            z = Mathf.Max(z, s.PlateThickness + s.RibDepth);
            z = Mathf.Max(z, s.PlateThickness + s.FrameDepth + RivetHeight);
            if (s.HatchCount > 0)
                z = Mathf.Max(z, s.PlateThickness + s.FrameDepth + HatchDiscDepth + HatchBossDepth);
            return z;
        }

        private const float RivetHeight = 0.009f;
        private const float RivetRadius = 0.013f;
        private const float HatchDiscDepth = 0.028f;
        private const float HatchBossDepth = 0.022f;

        // ══ A. 캐비닛 본체 ═══════════════════════════════════════════════════

        public static Mesh PanelMesh(PortholeSpec spec, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(4096);
            Panel(b, spec, uvPerMeter);
            return b.ToMesh("PM_PortholePanel");
        }

        /// <summary>
        /// 리벳 판금 캐비닛 + 원형 개구부 3×3 + 하단 원형 해치.
        ///
        /// 개구부는 **실제 구멍**이다 — 텍스처로 그린 원이 아니라 판을 뚫고, 그 뒤로
        /// 우물이 들어간다. 비스듬히 봤을 때 「그려진 원」과 「뚫린 창」은 완전히 다르게 읽히고,
        /// 이 장치는 화면 정면이 아니라 **1인칭 시점에서 비스듬히** 보이는 시간이 훨씬 길다.
        /// </summary>
        public static void Panel(ProcMeshBuilder b, PortholeSpec spec, float uvPerMeter = 1f)
        {
            PortholeSpec s = spec.Clamped();

            float p = s.CellPitch;
            float half = p * 0.5f;
            float ro = s.OpeningRadius;
            float rb = s.BezelOuterRadius;
            float t = s.PlateThickness;
            float pr = s.BezelProtrusion;
            int n = s.OpeningSides;

            float step = 360f / n;
            float offset = 45f - Mathf.Floor(45f / step) * step;

            // 방향·사각 테두리 표본을 한 번만 만든다. 아홉 셀이 **같은 배열**을 쓰므로
            // 이웃 셀의 공유 모서리가 같은 식에서 나온다.
            var dir = new Vector2[n];
            var sq = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                dir[i] = Dir(offset + step * i);
                sq[i] = SquareEdge(dir[i], half);
            }

            // ── ① 천공 슬래브 ────────────────────────────────────────────────
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                Vector3 c = new Vector3((col - 1) * p, (1 - row) * p, 0f);
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Vector3 rad = Radial(dir[i], dir[j]);

                    Vector3 sqi0 = c + new Vector3(sq[i].x, sq[i].y, 0f);
                    Vector3 sqj0 = c + new Vector3(sq[j].x, sq[j].y, 0f);
                    Vector3 sqi1 = sqi0 + new Vector3(0f, 0f, t);
                    Vector3 sqj1 = sqj0 + new Vector3(0f, 0f, t);

                    Vector3 rbi = c + At(dir[i], rb, t);
                    Vector3 rbj = c + At(dir[j], rb, t);
                    Vector3 roi0 = c + At(dir[i], ro, 0f);
                    Vector3 roj0 = c + At(dir[j], ro, 0f);
                    Vector3 roi1 = c + At(dir[i], ro, t + pr);
                    Vector3 roj1 = c + At(dir[j], ro, t + pr);

                    // 앞면 — 셀 사각 테두리에서 베젤 바깥까지.
                    b.AddQuad(sqi1, sqj1, rbj, rbi, Vector3.forward, uvPerMeter);

                    // 베젤 챔퍼 — 바깥에서 안으로 **올라간다.** 안쪽 끝이 정확히 개구부
                    // 반지름이라 링이 창을 덮지 않는다. 이것이 판독성 상한의 형상판이다.
                    Vector3 chamfer = (rad * Mathf.Max(pr, 1e-4f)
                                       + Vector3.forward * Mathf.Max(s.BezelWidth, 1e-4f)).normalized;
                    b.AddQuad(rbi, rbj, roj1, roi1, chamfer, uvPerMeter);

                    // 보어 — 법선이 **안쪽**을 본다. 창 안을 들여다보는 면이다.
                    b.AddQuad(roi1, roj1, roj0, roi0, -rad, uvPerMeter);

                    // 뒷면 — 벽을 향한다. 보이지 않지만 껍질을 닫는다.
                    b.AddQuad(sqi0, sqj0, roj0, roi0, Vector3.back, uvPerMeter);

                    // 옆벽은 **격자 바깥 둘레에만** 세운다. 안쪽 경계는 이웃 셀의 면이
                    // 이어받으므로 여기에 벽을 세우면 삼각형만 두 배가 된다.
                    if (OnGridBoundary(sq[i], sq[j], half, col, row))
                    {
                        Vector2 mid = (sq[i] + sq[j]) * 0.5f;
                        b.AddQuad(sqi1, sqj1, sqj0, sqi0,
                                  new Vector3(mid.x, mid.y, 0f).normalized, uvPerMeter);
                    }
                }
            }

            float gridHalf = 1.5f * p;
            float w = 2f * gridHalf + 2f * s.FrameMargin;
            PanelVerticalExtent(s, out float cabBottom, out float cabTop);
            float frameZ = t + s.FrameDepth;

            // ── ② 테두리 밴드 4개 ────────────────────────────────────────────
            float sideX = gridHalf + s.FrameMargin * 0.5f;
            float sideCY = (cabTop + cabBottom) * 0.5f;
            float sideH = cabTop - cabBottom;
            b.AddBox(new Vector3(-sideX, sideCY, frameZ * 0.5f),
                     new Vector3(s.FrameMargin, sideH, frameZ), s.FrameBevel, uvPerMeter);
            b.AddBox(new Vector3(sideX, sideCY, frameZ * 0.5f),
                     new Vector3(s.FrameMargin, sideH, frameZ), s.FrameBevel, uvPerMeter);
            b.AddBox(new Vector3(0f, gridHalf + s.FrameMargin * 0.5f, frameZ * 0.5f),
                     new Vector3(2f * gridHalf, s.FrameMargin, frameZ), s.FrameBevel, uvPerMeter);
            float lowerH = s.FrameMargin + s.HatchBandHeight;
            b.AddBox(new Vector3(0f, -gridHalf - lowerH * 0.5f, frameZ * 0.5f),
                     new Vector3(2f * gridHalf, lowerH, frameZ), s.FrameBevel, uvPerMeter);

            // ── ③ 리브 — 가로는 이어지고 세로는 끊긴다 ────────────────────────
            float hRibW = s.HorizontalRibWidth;
            float hRibD = s.RibDepth;
            for (int k = 0; k < 2; k++)
            {
                float y = k == 0 ? half : -half;
                // 폭이 **캐비닛 전체**다. 테두리에 파고들어 끊기지 않는다.
                b.AddBox(new Vector3(0f, y, t + hRibD * 0.5f),
                         new Vector3(w, hRibW, hRibD), 0f, uvPerMeter);
            }

            float vRibW = s.VerticalRibWidth;
            float vRibD = hRibD * VerticalRibDepthFactor;   // 세로가 덜 튀어나온다
            float vRibLen = s.VerticalRibSegmentLength;   // 한 행보다 짧다 = 끊긴다
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? -half : half;
                for (int row = 0; row < 3; row++)
                {
                    float y = (1 - row) * p;
                    b.AddBox(new Vector3(x, y, t + vRibD * 0.5f),
                             new Vector3(vRibW, vRibLen, vRibD), 0f, uvPerMeter);
                }
            }

            // ── ④ 하단 원형 해치 ─────────────────────────────────────────────
            if (s.HatchCount > 0)
            {
                float hatchY = cabBottom + s.HatchBandHeight * 0.5f;
                float r = s.HatchRadius;
                float x0 = -gridHalf + r + 0.02f;
                float x1 = gridHalf - r - 0.02f;
                for (int i = 0; i < s.HatchCount; i++)
                {
                    float u = s.HatchCount == 1 ? 0.5f : (i + 0.5f) / s.HatchCount;
                    float x = Mathf.Lerp(x0, x1, u);
                    b.AddPrism(new Vector3(x, hatchY, frameZ + HatchDiscDepth * 0.5f),
                               r, r * 0.94f, HatchDiscDepth, 8, MeshAxis.Z, 22.5f,
                               true, true, false, uvPerMeter);
                    // 가운데 육각 보스 — 「열 수 있는 것」이라는 신호. 손잡이가 아니라 볼트다.
                    b.AddPrism(new Vector3(x, hatchY, frameZ + HatchDiscDepth + HatchBossDepth * 0.5f),
                               r * 0.30f, r * 0.24f, HatchBossDepth, 6, MeshAxis.Z, 0f,
                               true, true, false, uvPerMeter);
                }
            }

            // ── ⑤ 리벳 — 테두리를 따라 도는 가로·세로 점선 ────────────────────
            if (s.RivetsPerBand > 0)
            {
                float inset = s.FrameMargin * 0.5f;
                float rx = gridHalf + inset;
                float ry = gridHalf + inset;
                for (int i = 0; i < s.RivetsPerBand; i++)
                {
                    float u = (i + 0.5f) / s.RivetsPerBand;
                    // 아주 작은 결정론적 흔들림. 완벽한 등간격은 「쇼룸」(금지 9)으로 읽힌다.
                    float jx = ProcMeshBuilder.HashSigned(i * 197 + 5) * 0.006f;
                    float x = Mathf.Lerp(-gridHalf + 0.05f, gridHalf - 0.05f, u) + jx;
                    ProcMesh.Rivet(b, new Vector3(x, ry, frameZ), RivetRadius, RivetHeight,
                                   MeshAxis.Z, 4, uvPerMeter);
                    ProcMesh.Rivet(b, new Vector3(x, -ry, frameZ), RivetRadius, RivetHeight,
                                   MeshAxis.Z, 4, uvPerMeter);
                }
                int sideCount = Mathf.Max(1, s.RivetsPerBand - 2);
                for (int i = 0; i < sideCount; i++)
                {
                    float u = (i + 0.5f) / sideCount;
                    float jy = ProcMeshBuilder.HashSigned(i * 89 + 31) * 0.006f;
                    float y = Mathf.Lerp(-gridHalf + 0.05f, gridHalf - 0.05f, u) + jy;
                    ProcMesh.Rivet(b, new Vector3(-rx, y, frameZ), RivetRadius, RivetHeight,
                                   MeshAxis.Z, 4, uvPerMeter);
                    ProcMesh.Rivet(b, new Vector3(rx, y, frameZ), RivetRadius, RivetHeight,
                                   MeshAxis.Z, 4, uvPerMeter);
                }
            }

            // ── ⑥ 베젤 볼트 — 챔퍼 **위에** 앉는다 ────────────────────────────
            if (s.BoltsPerBezel > 0 && s.BezelWidth > 0.006f)
            {
                float boltR = Mathf.Min(0.009f, s.BezelWidth * 0.5f - 0.002f);
                float ringR = ro + s.BezelWidth * 0.5f;
                float boltZ = t + pr * 0.5f;              // 챔퍼의 정확히 중간 높이
                float boltStep = 360f / s.BoltsPerBezel;
                for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    Vector3 c = new Vector3((col - 1) * p, (1 - row) * p, 0f);
                    for (int i = 0; i < s.BoltsPerBezel; i++)
                    {
                        // 셀마다 각도를 어긋나게 둔다 — 아홉 개가 같은 각이면 격자가
                        // 「인쇄된 무늬」로 읽힌다.
                        float a = 20f + boltStep * i + (row * 3 + col) * 11f;
                        Vector2 d = Dir(a);
                        ProcMesh.Rivet(b, c + new Vector3(d.x * ringR, d.y * ringR, boltZ),
                                       boltR, 0.007f, MeshAxis.Z, 3, uvPerMeter);
                    }
                }
            }
        }

        // ══ B. 유리 ═══════════════════════════════════════════════════════════

        public static Mesh GlassClusterMesh(PortholeSpec spec, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(1024);
            GlassCluster(b, spec, uvPerMeter);
            return b.ToMesh("PM_PortholeGlass");
        }

        /// <summary>
        /// 창 유리 9장 — <see cref="Panel"/> 과 **같은 로컬 좌표계**에 놓인다.
        ///
        /// ⚠ 이 메시에는 **투명 머티리얼**을 물려야 한다 — 알파 0.25 · Transparent ·
        /// ZWrite off. 씬 소유자가 통관 유리에서 확인한 값이고, 불투명으로 바꾸면
        /// 안쪽 심볼 27개가 그대로 사라진다. 같은 함정을 여기서 반복하지 마라.
        /// </summary>
        public static void GlassCluster(ProcMeshBuilder b, PortholeSpec spec, float uvPerMeter = 1f)
        {
            PortholeSpec s = spec.Clamped();
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                GlassAt(b, s, CellMouth(s, col, row), uvPerMeter);
        }

        /// <summary>유리 한 장. 원점 = 유리 **뒷면** 중심, 볼록한 쪽이 +Z.</summary>
        public static void Glass(ProcMeshBuilder b, PortholeSpec spec, float uvPerMeter = 1f)
        {
            PortholeSpec s = spec.Clamped();
            GlassAt(b, s, new Vector3(0f, 0f, s.GlassThickness), uvPerMeter);
        }

        private static void GlassAt(ProcMeshBuilder b, in PortholeSpec s, Vector3 mouth, float uv)
        {
            float rg = s.OpeningRadius - GlassBoreClearance;
            float th = s.GlassThickness;
            int tiers = s.GlassTiers;
            float tierH = th / tiers;
            // 뒷면이 입 평면보다 유리 두께만큼 안으로 들어간다 — 앞면이 베젤 립과 나란해진다.
            float z0 = mouth.z - th;
            float r = rg;
            for (int i = 0; i < tiers; i++)
            {
                float r1 = rg * Mathf.Lerp(1f, 0.55f, (i + 1f) / tiers);
                b.AddPrism(new Vector3(mouth.x, mouth.y, z0 + tierH * i + tierH * 0.5f),
                           r, r1, tierH, s.GlassSides, MeshAxis.Z, 45f,
                           true, true, false, uv);
                r = r1;
            }
        }

        // ══ C. 우물 ═══════════════════════════════════════════════════════════

        public static Mesh WellClusterMesh(PortholeSpec spec, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(1024);
            WellCluster(b, spec, uvPerMeter);
            return b.ToMesh("PM_PortholeWell");
        }

        /// <summary>
        /// 발광체가 앉는 우물 9개 — <see cref="Panel"/> 과 같은 좌표계.
        /// **발광체 자체가 아니라 그것을 감싸는 그릇**이다.
        /// </summary>
        public static void WellCluster(ProcMeshBuilder b, PortholeSpec spec, float uvPerMeter = 1f)
        {
            PortholeSpec s = spec.Clamped();
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                WellAt(b, s, CellMouth(s, col, row), uvPerMeter);
        }

        /// <summary>우물 하나. 원점 = **입** 중심, 몸통이 −Z 로 들어간다.</summary>
        public static void Well(ProcMeshBuilder b, PortholeSpec spec, float uvPerMeter = 1f)
        {
            PortholeSpec s = spec.Clamped();
            WellAt(b, s, Vector3.zero, uvPerMeter);
        }

        /// <summary>우물 바닥의 로컬 z(판 좌표계). 발광 구체를 여기 위에 앉힌다.</summary>
        public static float WellFloorZ(PortholeSpec spec)
        {
            PortholeSpec s = spec.Clamped();
            return s.PlateThickness + s.BezelProtrusion - (s.WellDepth - s.WellFloorThickness);
        }

        private static void WellAt(ProcMeshBuilder b, in PortholeSpec s, Vector3 mouth, float uv)
        {
            int n = s.OpeningSides;
            float step = 360f / n;
            float offset = 45f - Mathf.Floor(45f / step) * step;

            float ro = s.OpeningRadius - WellBoreClearance;
            float ri = ro - s.WellWallThickness;
            float d = s.WellDepth;
            float ft = s.WellFloorThickness;
            float floorZ = -(d - ft);

            var dir = new Vector2[n];
            for (int i = 0; i < n; i++) dir[i] = Dir(offset + step * i);

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 rad = Radial(dir[i], dir[j]);

                Vector3 ii0 = mouth + At(dir[i], ri, 0f);
                Vector3 ij0 = mouth + At(dir[j], ri, 0f);
                Vector3 oi0 = mouth + At(dir[i], ro, 0f);
                Vector3 oj0 = mouth + At(dir[j], ro, 0f);
                Vector3 ii1 = mouth + At(dir[i], ri, floorZ);
                Vector3 ij1 = mouth + At(dir[j], ri, floorZ);
                Vector3 oi1 = mouth + At(dir[i], ro, -d);
                Vector3 oj1 = mouth + At(dir[j], ro, -d);

                b.AddQuad(ii0, ij0, oj0, oi0, Vector3.forward, uv);   // 입 테
                b.AddQuad(ii0, ij0, ij1, ii1, -rad, uv);              // **안쪽 벽 — 법선이 안으로**
                b.AddQuad(oi0, oj0, oj1, oi1, rad, uv);               // 바깥 벽
            }

            var floor = new Vector3[n];
            var back = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                floor[i] = mouth + At(dir[i], ri, floorZ);
                back[i] = mouth + At(dir[i], ro, -d);
            }
            for (int i = 1; i < n - 1; i++)
                b.AddTriangle(floor[0], floor[i], floor[i + 1], Vector3.forward, uv);
            for (int i = 1; i < n - 1; i++)
                b.AddTriangle(back[0], back[i], back[i + 1], Vector3.back, uv);
        }

        // ══ 공통 도우미 ══════════════════════════════════════════════════════

        /// <summary>
        /// 각도 → 단위 방향. **축과 대각의 부동소수 찌꺼기를 정리한다.**
        ///
        /// 이웃 셀은 공유 모서리를 서로 다른 각도(0° vs 180°)에서 만든다. 정리하지 않으면
        /// 두 점이 1e-7 m 어긋나고, 그 어긋남은 닫힘 검사에서는 양자화에 묻히지만
        /// 화면에서는 조명 계단이 한 줄 갈라지는 것으로 나타난다.
        /// </summary>
        private static Vector2 Dir(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(a);
            float s = Mathf.Sin(a);
            float ac = Mathf.Abs(c);
            float as_ = Mathf.Abs(s);
            const float snap = 1e-5f;
            const float k = 0.70710678f;

            if (ac < snap) return new Vector2(0f, s < 0f ? -1f : 1f);
            if (as_ < snap) return new Vector2(c < 0f ? -1f : 1f, 0f);
            if (Mathf.Abs(ac - as_) < snap)
                return new Vector2(c < 0f ? -k : k, s < 0f ? -k : k);
            return new Vector2(c, s);
        }

        /// <summary>방향 <paramref name="d"/> 의 반직선이 반폭 <paramref name="half"/> 정사각형과 만나는 점.</summary>
        private static Vector2 SquareEdge(Vector2 d, float half)
        {
            float ax = Mathf.Abs(d.x);
            float ay = Mathf.Abs(d.y);
            float m = Mathf.Max(ax, ay);
            float t = half / m;
            // 지배축은 **정확히** ±half 로 놓는다. t 를 곱하면 1 ulp 가 남는다.
            float x = ax >= m ? (d.x < 0f ? -half : half) : d.x * t;
            float y = ay >= m ? (d.y < 0f ? -half : half) : d.y * t;
            return new Vector2(x, y);
        }

        private static Vector3 At(Vector2 d, float radius, float z)
            => new Vector3(d.x * radius, d.y * radius, z);

        private static Vector3 Radial(Vector2 a, Vector2 b)
            => new Vector3(a.x + b.x, a.y + b.y, 0f).normalized;

        /// <summary>이 사각 테두리 조각이 3×3 격자의 **바깥 둘레**에 있는가.</summary>
        private static bool OnGridBoundary(Vector2 a, Vector2 b, float half, int col, int row)
        {
            const float e = 1e-4f;
            if (col == 2 && a.x >= half - e && b.x >= half - e) return true;
            if (col == 0 && a.x <= -half + e && b.x <= -half + e) return true;
            if (row == 0 && a.y >= half - e && b.y >= half - e) return true;   // row 0 = 위
            if (row == 2 && a.y <= -half + e && b.y <= -half + e) return true;
            return false;
        }
    }
}
