using UnityEngine;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 정화 표식이 **어디에 어떤 모양으로** 놓이는지를 계산한다. 씬도 GameObject 도 필요 없다.
    ///
    /// 왜 별도 파일인가 — 두 가지 때문이다.
    ///
    /// ① **판정 가능해야 한다.** 15차 독립 시각 평가가 「셀을 잇는 막대」를 스타일 1점의
    ///    유일한 원인으로 10라운드 연속 지목하면서, 색이 아니라 **형상**을 재는 지표
    ///    `G-SLOT-A` 를 정의했다 — ROI(아홉 칸의 화면 AABB) 안에서 ① |ΔL| ≥ 25 이진화
    ///    ② 장축/단축 ≥ 4 ③ 장축 ≥ ROI 폭의 35% ④ **칸 경계 2개 이상 횡단**, 넷을 모두
    ///    만족하는 성분을 「띠」로 세고 **통과선은 0개**다. 「색을 바꿔도 안 속는다 —
    ///    흰 막대도 페이라인이다.」
    ///
    ///    표식이 MonoBehaviour 안에 갇혀 있으면 그 판정을 화면을 찍기 전에는 못 한다.
    ///    여기 있는 값은 전부 **칸 피치 단위의 순수 좌표**라, 씬 없이 「모든 표식이 칸 하나
    ///    안에 든다」를 단정할 수 있다(`Tests/PurifyMarkerLayoutTests`).
    ///
    /// ② **구조적으로 페이라인을 만들 수 없어야 한다.** 길이 상한 <see cref="MaxSpan"/> 이
    ///    칸 피치의 0.6 배이므로 ③(ROI 폭의 35% ≈ 칸 피치의 1.05 배)에 **닿을 수 없고**,
    ///    모든 표식이 칸 중심에서 <see cref="CellHalfLimit"/> 안에 들어가므로 ④(경계 횡단)
    ///    도 **0 이다.** 네 조건 중 둘이 구조적으로 불가능하면 지표는 절대 발화하지 않는다.
    ///    인스펙터 값으로도 못 깬다 — 두께는 <see cref="ThicknessInPitch"/> 가 잘라낸다.
    ///
    /// **`UP-CORE-12`(판정 원인 시각화)는 죽지 않는다.** 원인 세 종류가 서로 다른 **형태
    /// 계통**을 갖는다. 회색조·정지 화면에서도 갈린다(`visual-criteria` B-2 #6:
    /// 「전부 같은 이펙트면 실패다」).
    ///
    ///   <see cref="PurifyMarkerShape.Outline"/> — 인접 최소 개수(<see cref="PatternKind.Scattered"/>).
    ///       덩어리의 **바깥 경계에 닿는 변만** 칸 안쪽으로 들여 그린다. 닫힌 사각 테두리 계통이고
    ///       「어디까지가 한 덩어리인가」가 그대로 남는다. 예전에는 이 원인에 표식이 **아예 없었다.**
    ///   <see cref="PurifyMarkerShape.Chevron"/> — 직선 3(<see cref="PatternKind.Line"/>).
    ///       줄 방향을 가리키는 V. **방향이 형상 안에 있다.** 줄과 평행한 긴 조각을 절대 만들지
    ///       않는다 — 그것이 예전의 관통 막대였다.
    ///   <see cref="PurifyMarkerShape.Stub"/> — 직교 연결 4개 이상(<see cref="PatternKind.Cluster"/>·
    ///       <see cref="PatternKind.FullBoard"/>). 칸 중심에서 **연결된 이웃 쪽으로만** 뻗다가
    ///       칸 경계 한참 전에 멈추는 팔. 규칙(직교 인접)을 그대로 그리되 이웃 칸의 팔과는
    ///       <see cref="StubGapInPitch"/> 만큼 떨어져 있어 하나의 띠로 붙지 않는다.
    ///
    /// 순차 공개 셔터(<see cref="PurifyMarkerShape.ShutterSealed"/>·
    /// <see cref="PurifyMarkerShape.ShutterOpen"/>)도 여기서 계산한다. 셔터는 원래부터 칸
    /// 단위였지만 **폭이 칸의 0.78 배**여서 화면에서는 결과판을 가로지르는 굵은 막대로 읽혔다
    /// (`21_board_and_gauge` 의 「금색 가로 막대 6개」가 정화 표식이 아니라 이 셔터다 —
    /// y 좌표 6개가 3행 × 위아래 두 짝과 정확히 일치한다). 그래서 폭을 0.42 로 줄이고
    /// 두께를 키워 **가늘고 긴 것**이 아니라 **뭉툭한 빗장**이 되게 했다.
    /// 세 단계를 가르는 축(개수 1/2/0 · 위치 중앙/가장자리 · 두께 · 밝기)은 전부 그대로다.
    ///
    /// 좌표계: 칸 중심이 원점이고 **칸 피치 1** 이 단위다. x 는 열 축(<c>_columnStep</c>),
    /// y 는 행 축(<c>_rowStep</c>). 월드 변환은 <see cref="PurifyMarkerView"/> 가 한다 —
    /// 여기서는 씬을 모른다.
    /// </summary>
    public static class PurifyMarkerLayout
    {
        // ── 불변식 (G-SLOT-A 를 구조적으로 불가능하게 만드는 두 수) ───────────────

        /// <summary>
        /// 표식의 어떤 부분도 칸 중심에서 이 거리(칸 피치 단위)를 넘지 않는다.
        /// 0.5 가 칸 경계이므로 0.06 피치의 여유가 남는다 — `G-SLOT-A` ④(경계 횡단)가 0 이 된다.
        /// </summary>
        public const float CellHalfLimit = 0.44f;

        /// <summary>
        /// 표식 하나의 장축 상한(칸 피치 단위). ROI 폭이 약 3 피치이므로
        /// `G-SLOT-A` ③(ROI 폭의 35% = 약 1.05 피치)에 **닿을 수 없다.**
        /// </summary>
        public const float MaxSpan = 0.60f;

        /// <summary>
        /// 장축/단축 상한. `G-SLOT-A` ② 의 4 보다 낮게 잡아 두께로도 한 번 더 막는다.
        /// 셋 중 하나만 못 넘어도 띠로 세지 않으므로 이건 세 겹째 안전장치다.
        /// </summary>
        public const float MaxAspect = 3.6f;

        /// <summary>두께 상한(칸 피치 단위). 칸을 두께로 채워 버리는 것을 막는다.</summary>
        public const float MaxThickness = 0.28f;

        // ── 형태별 치수 (전부 칸 피치 단위) ──────────────────────────────────────

        /// <summary>외곽선 변이 칸 중심에서 물러난 거리.</summary>
        public const float OutlineInset = 0.30f;

        /// <summary>외곽선 변 하나의 길이. 이웃 칸의 같은 변과 <c>1 - 이 값</c> 만큼 벌어진다.</summary>
        public const float OutlineSpan = 0.56f;

        /// <summary>셰브런 팔 하나의 길이.</summary>
        public const float ChevronArm = 0.36f;

        /// <summary>셰브런 꼭짓점이 칸 중심에서 줄 방향으로 나간 거리.</summary>
        public const float ChevronApex = 0.19f;

        /// <summary>셰브런 팔이 줄 방향과 이루는 각(40°)의 코사인.</summary>
        public const float ChevronCos = 0.76604444f;

        /// <summary>같은 각의 사인.</summary>
        public const float ChevronSin = 0.64278761f;

        /// <summary>연결 팔이 칸 중심에서 이웃 쪽으로 뻗는 길이.</summary>
        public const float StubReach = 0.26f;

        /// <summary>
        /// 이웃한 두 칸의 마주 보는 연결 팔 사이에 남는 빈 틈. 이 값이 0 이 되는 순간
        /// 두 팔이 한 성분으로 붙어 **경계를 넘는 막대**가 된다 — 그것이 예전 구현이었다.
        /// </summary>
        public const float StubGapInPitch = 1f - 2f * StubReach;

        /// <summary>셔터 막대의 폭. 예전 0.78 은 결과판 폭의 1/4 를 덮어 페이라인으로 읽혔다.</summary>
        public const float ShutterSpan = 0.42f;

        /// <summary>열리는 셔터의 두 짝이 칸 중심에서 물러나는 거리(행 축).</summary>
        public const float ShutterOpenGap = 0.32f;

        /// <summary>
        /// 한 정화 사건이 요구하는 표식 수의 상한. 9칸 잭팟의 연결 팔 24개가 최댓값이고,
        /// 서로 다른 두 사건이 동시에 떠도 합이 이 값을 넘지 않는다
        /// (`PurifyMarkerLayoutTests.TestPoolCeiling` 이 512개 부분집합 쌍을 전수로 확인한다).
        /// </summary>
        public const int MaxPlacementsPerEvent = 24;

        /// <summary>닫힌 칸 하나가 쓰는 표식 수.</summary>
        public const int SealedBarsPerCell = 1;

        /// <summary>열리는 칸 하나가 쓰는 표식 수(갈라진 두 짝).</summary>
        public const int OpeningBarsPerCell = 2;

        // ── 방향 표 ────────────────────────────────────────────────────────────
        // 순서를 바꾸면 표식 배치 순서가 바뀐다. 그림은 같지만 풀 슬롯 배정이 달라져
        // 캡처 비교가 흔들린다 — 결정론을 위해 고정한다.

        private static readonly Vector2[] Dirs =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(0f, 1f), new Vector2(0f, -1f),
        };

        private static readonly int[] DirColumn = { 1, -1, 0, 0 };
        private static readonly int[] DirRow    = { 0, 0, 1, -1 };

        /// <summary>
        /// 이 원인이 어떤 형태 계통을 쓰는가. **세 값이 서로 달라야 `UP-CORE-12` 가 산다** —
        /// 같은 값을 돌려주기 시작하면 「전부 같은 이펙트」가 되고 그것이 B-2 #6 의 실패 조건이다.
        /// </summary>
        public static PurifyMarkerShape ShapeFor(PatternKind pattern)
        {
            switch (pattern)
            {
                case PatternKind.Scattered: return PurifyMarkerShape.Outline;
                case PatternKind.Line:      return PurifyMarkerShape.Chevron;
                case PatternKind.Cluster:
                case PatternKind.FullBoard: return PurifyMarkerShape.Stub;
                default:                    return PurifyMarkerShape.None;
            }
        }

        /// <summary>
        /// 한 정화 사건의 표식을 <paramref name="buffer"/> 에 채우고 개수를 돌려준다.
        /// **할당하지 않는다** — 버퍼는 호출자가 미리 잡는다.
        ///
        /// <paramref name="cells"/> 의 **순서에 의존하지 않는다.** 9비트 마스크로 접은 뒤
        /// 칸 인덱스 오름차순으로만 훑기 때문이다. 같은 칸 집합은 배열 순서가 어떻든 같은
        /// 그림을 낸다 — 시드 재현이 표식 배치까지 닿는다(`TECH_SPEC` §7).
        /// </summary>
        public static int Build(PatternKind pattern, int[] cells, PurifyMarkerPlacement[] buffer)
        {
            if (buffer == null || cells == null || cells.Length < 2) return 0;
            int mask = MaskOf(cells);

            switch (ShapeFor(pattern))
            {
                case PurifyMarkerShape.Outline: return BuildOutline(mask, buffer);
                case PurifyMarkerShape.Chevron: return BuildChevron(mask, buffer);
                case PurifyMarkerShape.Stub:    return BuildStubs(mask, buffer);
                default:                        return 0;
            }
        }

        /// <summary>
        /// <see cref="Build"/> 가 만들 표식 수. 버퍼 없이 물을 수 있어 풀 크기 검증에 쓴다.
        /// 풀이 모자라면 표식이 **조용히** 빠지고 정지 캡처가 사실과 다른 말을 한다.
        /// </summary>
        public static int Needed(PatternKind pattern, int[] cells)
        {
            if (cells == null || cells.Length < 2) return 0;
            int mask = MaskOf(cells);

            switch (ShapeFor(pattern))
            {
                case PurifyMarkerShape.Outline: return CountEdges(mask, outer: true);
                case PurifyMarkerShape.Chevron: return CountBits(mask) >= 2 ? CountBits(mask) * 2 : 0;
                case PurifyMarkerShape.Stub:    return CountEdges(mask, outer: false);
                default:                        return 0;
            }
        }

        /// <summary>
        /// 순차 공개 셔터를 채운다(`UP-FIX-20`). 표식 계통이 정화와 다르고 시간대도 겹치지
        /// 않지만, **칸 안에 든다는 불변식은 같은 곳에서 지킨다** — 두 군데서 지키면 한 쪽만
        /// 고쳐지고 나머지가 남는다. 실제로 `21` 의 금색 막대 6개가 그 「나머지」였다.
        /// </summary>
        public static int BuildRevealShutters(int revealedColumns, PurifyMarkerPlacement[] buffer)
        {
            if (buffer == null) return 0;
            int n = 0;

            for (int column = 0; column < SpinBoard.Columns; column++)
            {
                SpinPresenter.RevealStage stage = SpinPresenter.StageOfColumn(column, revealedColumns);
                if (stage == SpinPresenter.RevealStage.Open) continue;

                for (int row = 0; row < SpinBoard.Rows; row++)
                {
                    int cell = SpinBoard.Index(column, row);

                    if (stage == SpinPresenter.RevealStage.Sealed)
                    {
                        if (n >= buffer.Length) return n;
                        buffer[n++] = new PurifyMarkerPlacement(
                            cell, PurifyMarkerShape.ShutterSealed,
                            Vector2.zero, Dirs[0], ShutterSpan);
                        continue;
                    }

                    if (n + 1 >= buffer.Length) return n;
                    buffer[n++] = new PurifyMarkerPlacement(
                        cell, PurifyMarkerShape.ShutterOpen,
                        new Vector2(0f, ShutterOpenGap), Dirs[0], ShutterSpan);
                    buffer[n++] = new PurifyMarkerPlacement(
                        cell, PurifyMarkerShape.ShutterOpen,
                        new Vector2(0f, -ShutterOpenGap), Dirs[0], ShutterSpan);
                }
            }
            return n;
        }

        /// <summary>
        /// 주어진 진행도에서 셔터가 쓰는 표식 수. <see cref="BuildRevealShutters"/> 와
        /// **다른 길로** 같은 답을 내야 한다 — 하나는 배치를 만들고 하나는 단계에서 세므로,
        /// 어긋나면 풀 검증이 실제와 다른 수를 믿고 있다는 뜻이다.
        /// </summary>
        public static int RevealBarsNeeded(int revealedColumns)
        {
            int bars = 0;
            for (int column = 0; column < SpinBoard.Columns; column++)
            {
                switch (SpinPresenter.StageOfColumn(column, revealedColumns))
                {
                    case SpinPresenter.RevealStage.Sealed:
                        bars += SpinBoard.Rows * SealedBarsPerCell;
                        break;
                    case SpinPresenter.RevealStage.Opening:
                        bars += SpinBoard.Rows * OpeningBarsPerCell;
                        break;
                }
            }
            return bars;
        }

        // ── 두께와 담김 ────────────────────────────────────────────────────────

        /// <summary>
        /// 이 표식이 실제로 쓸 두께(칸 피치 단위).
        ///
        /// 세 힘이 겹친다. ① 요청 두께(인스펙터의 미터 값을 피치로 환산한 것),
        /// ② `G-SLOT-A` ② 를 못 넘게 하는 하한 <c>길이 / <see cref="MaxAspect"/></c>,
        /// ③ 칸 밖으로 나가지 못하게 하는 상한 <see cref="MaxThicknessFor"/>.
        /// **상한이 마지막에 이긴다** — 인스펙터를 아무리 키워도 칸을 못 넘는다.
        /// </summary>
        public static float ThicknessInPitch(in PurifyMarkerPlacement placement, float requestedInPitch)
        {
            float wanted = Mathf.Max(requestedInPitch, placement.Length / MaxAspect);
            return Mathf.Clamp(wanted, 0f, MaxThicknessFor(in placement));
        }

        /// <summary>이 표식이 칸을 넘지 않고 가질 수 있는 최대 두께(칸 피치 단위).</summary>
        public static float MaxThicknessFor(in PurifyMarkerPlacement placement)
        {
            float limit = MaxThickness;
            limit = Mathf.Min(limit, AxisLimit(placement.Center.x, placement.Direction.x,
                                               Mathf.Abs(placement.Direction.y), placement.Length));
            limit = Mathf.Min(limit, AxisLimit(placement.Center.y, placement.Direction.y,
                                               Mathf.Abs(placement.Direction.x), placement.Length));
            return Mathf.Max(0f, limit);
        }

        // |중심| + |장축성분|·길이/2 + |단축성분|·두께/2 ≤ CellHalfLimit 를 두께에 대해 푼다.
        private static float AxisLimit(float center, float along, float across, float length)
        {
            float room = CellHalfLimit - Mathf.Abs(center) - Mathf.Abs(along) * length * 0.5f;
            if (across <= 1e-5f) return MaxThickness;   // 이 축으로는 두께가 자라지 않는다
            return 2f * room / across;
        }

        /// <summary>
        /// 이 표식이 (주어진 두께로) 칸 하나 안에 완전히 드는가.
        /// **`G-SLOT-A` ④ 를 화면 없이 판정하는 함수다.**
        /// </summary>
        public static bool WithinCell(in PurifyMarkerPlacement placement, float thicknessInPitch)
        {
            float dx = Mathf.Abs(placement.Direction.x);
            float dy = Mathf.Abs(placement.Direction.y);
            float half = placement.Length * 0.5f;
            float t = Mathf.Max(0f, thicknessInPitch) * 0.5f;

            float extentX = dx * half + dy * t;
            float extentY = dy * half + dx * t;

            const float epsilon = 1e-4f;
            return Mathf.Abs(placement.Center.x) + extentX <= CellHalfLimit + epsilon
                && Mathf.Abs(placement.Center.y) + extentY <= CellHalfLimit + epsilon;
        }

        // ── 형태 생성 ──────────────────────────────────────────────────────────

        private static int BuildOutline(int mask, PurifyMarkerPlacement[] buffer)
        {
            int n = 0;
            for (int cell = 0; cell < SpinBoard.Cells; cell++)
            {
                if (!Has(mask, cell)) continue;
                for (int d = 0; d < 4; d++)
                {
                    int neighbour = Neighbour(cell, d);
                    if (neighbour >= 0 && Has(mask, neighbour)) continue;   // 안쪽 변은 그리지 않는다
                    if (n >= buffer.Length) return n;

                    Vector2 normal = Dirs[d];
                    buffer[n++] = new PurifyMarkerPlacement(
                        cell, PurifyMarkerShape.Outline,
                        normal * OutlineInset,
                        new Vector2(-normal.y, normal.x),
                        OutlineSpan);
                }
            }
            return n;
        }

        private static int BuildChevron(int mask, PurifyMarkerPlacement[] buffer)
        {
            int first = -1, last = -1;
            for (int cell = 0; cell < SpinBoard.Cells; cell++)
            {
                if (!Has(mask, cell)) continue;
                if (first < 0) first = cell;
                last = cell;
            }
            if (first < 0 || first == last) return 0;

            // 줄의 양 끝은 인덱스 최소·최대다 — 세로(0,1,2)·가로(0,3,6)·대각(0,4,8)·
            // 역대각(2,4,6) 넷 모두에서 참이라 정렬이 필요 없다.
            var direction = new Vector2(
                SpinBoard.ColumnOf(last) - SpinBoard.ColumnOf(first),
                SpinBoard.RowOf(last) - SpinBoard.RowOf(first));
            float magnitude = direction.magnitude;
            if (magnitude < 1e-5f) return 0;
            direction /= magnitude;

            var perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 apex = direction * ChevronApex;
            Vector2 back = apex - direction * (ChevronArm * ChevronCos);
            Vector2 wing = perpendicular * (ChevronArm * ChevronSin);

            int n = 0;
            for (int cell = 0; cell < SpinBoard.Cells; cell++)
            {
                if (!Has(mask, cell)) continue;
                for (int side = 0; side < 2; side++)
                {
                    if (n >= buffer.Length) return n;
                    Vector2 end = side == 0 ? back + wing : back - wing;
                    Vector2 segment = end - apex;
                    float length = segment.magnitude;
                    if (length < 1e-5f) continue;

                    buffer[n++] = new PurifyMarkerPlacement(
                        cell, PurifyMarkerShape.Chevron,
                        (apex + end) * 0.5f, segment / length, length);
                }
            }
            return n;
        }

        private static int BuildStubs(int mask, PurifyMarkerPlacement[] buffer)
        {
            int n = 0;
            for (int cell = 0; cell < SpinBoard.Cells; cell++)
            {
                if (!Has(mask, cell)) continue;
                for (int d = 0; d < 4; d++)
                {
                    int neighbour = Neighbour(cell, d);
                    if (neighbour < 0 || !Has(mask, neighbour)) continue;   // 이어진 쪽으로만 뻗는다
                    if (n >= buffer.Length) return n;

                    buffer[n++] = new PurifyMarkerPlacement(
                        cell, PurifyMarkerShape.Stub,
                        Dirs[d] * (StubReach * 0.5f), Dirs[d], StubReach);
                }
            }
            return n;
        }

        // ── 비트 마스크 도우미 ─────────────────────────────────────────────────

        private static int MaskOf(int[] cells)
        {
            int mask = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                int cell = cells[i];
                if (cell < 0 || cell >= SpinBoard.Cells) continue;
                mask |= 1 << cell;
            }
            return mask;
        }

        private static bool Has(int mask, int cell) => (mask & (1 << cell)) != 0;

        private static int CountBits(int mask)
        {
            int n = 0;
            for (int cell = 0; cell < SpinBoard.Cells; cell++) if (Has(mask, cell)) n++;
            return n;
        }

        private static int CountEdges(int mask, bool outer)
        {
            int n = 0;
            for (int cell = 0; cell < SpinBoard.Cells; cell++)
            {
                if (!Has(mask, cell)) continue;
                for (int d = 0; d < 4; d++)
                {
                    int neighbour = Neighbour(cell, d);
                    bool inside = neighbour >= 0 && Has(mask, neighbour);
                    if (inside != outer) n++;
                }
            }
            return n;
        }

        private static int Neighbour(int cell, int direction)
        {
            int column = SpinBoard.ColumnOf(cell) + DirColumn[direction];
            int row = SpinBoard.RowOf(cell) + DirRow[direction];
            if (column < 0 || column >= SpinBoard.Columns) return -1;
            if (row < 0 || row >= SpinBoard.Rows) return -1;
            return SpinBoard.Index(column, row);
        }
    }

    /// <summary>
    /// 표식의 형태 계통. **색이 아니라 이것이 원인을 나른다** — 15차 평가가
    /// 「막대의 알베도가 금색인 한 화면은 금색이다」로 색 채널을 폐기했다.
    /// </summary>
    public enum PurifyMarkerShape
    {
        None = 0,

        /// <summary>덩어리 외곽선 — 인접 최소 개수 정화.</summary>
        Outline = 1,

        /// <summary>방향 셰브런 — 직선 3.</summary>
        Chevron = 2,

        /// <summary>연결 팔 — 직교 연결 4개 이상, 잭팟.</summary>
        Stub = 3,

        /// <summary>순차 공개 — 아직 안 열린 칸의 빗장 하나.</summary>
        ShutterSealed = 4,

        /// <summary>순차 공개 — 지금 열리는 칸의 갈라진 두 짝.</summary>
        ShutterOpen = 5,
    }

    /// <summary>
    /// 표식 하나의 배치. 좌표는 **칸 중심이 원점이고 칸 피치가 1** 인 칸 로컬 평면 좌표다.
    /// 월드로 옮기는 것은 <see cref="PurifyMarkerView"/> 의 일이고, 여기 값만으로
    /// 「칸을 넘는가」를 판정할 수 있어야 한다.
    /// </summary>
    public readonly struct PurifyMarkerPlacement
    {
        /// <summary>이 표식이 속한 칸. <see cref="SpinBoard.Index"/> 순서다.</summary>
        public readonly int Cell;

        /// <summary>형태 계통. 뷰가 색·두께를 여기서 고른다.</summary>
        public readonly PurifyMarkerShape Shape;

        /// <summary>칸 중심 기준 위치(x = 열 축, y = 행 축).</summary>
        public readonly Vector2 Center;

        /// <summary>장축 방향(단위 벡터).</summary>
        public readonly Vector2 Direction;

        /// <summary>장축 길이(칸 피치 단위).</summary>
        public readonly float Length;

        public PurifyMarkerPlacement(int cell, PurifyMarkerShape shape,
                                     Vector2 center, Vector2 direction, float length)
        {
            Cell = cell;
            Shape = shape;
            Center = center;
            Direction = direction;
            Length = length;
        }
    }
}
