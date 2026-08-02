using System;
using System.Text;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View.Tests
{
    /// <summary>
    /// 정화 표식이 **칸 하나 안에 머무는가**를 씬 없이 판정한다.
    ///
    /// **무엇을 반증하려는가**: 15차 독립 시각 평가가 「셀을 잇는 막대」를
    /// 「카지노 슬롯머신 페이라인」(`VISUAL_SPEC §1` 명시 금지)으로 **10라운드 연속**
    /// 지목했고, 색을 바꾼 직전 시도는 화면에 도달조차 못 했다. 평가자가 색에 속지 않는
    /// 지표를 새로 정의했다 — `G-SLOT-A`:
    ///
    /// > ROI(결과판 아홉 칸의 화면 AABB 합집합) 안에서 ① 주변 대비 |ΔL| ≥ 25 로 이진화
    /// > ② **장축/단축 ≥ 4** ③ **장축 길이 ≥ ROI 폭의 35%** ④ **칸 경계를 2개 이상 횡단** —
    /// > 넷을 **모두** 만족하는 연결 성분을 「띠」로 센다. **통과선: 띠 개수 = 0.**
    ///
    /// 화면을 찍어 재는 것은 캡처 하네스의 일이지만, **찍기 전에 구조적으로 불가능함을
    /// 단정할 수 있다.** 넷 중 하나만 못 만족해도 띠가 아니기 때문이다. 여기서 두 개를 막는다:
    ///
    ///   ③ — 모든 표식의 장축이 칸 피치의 <see cref="PurifyMarkerLayout.MaxSpan"/> 배 이하다.
    ///        ROI 폭이 약 3 피치이므로 35%(≈ 1.05 피치)에 **닿을 수 없다.**
    ///   ④ — 모든 표식이 칸 중심에서 <see cref="PurifyMarkerLayout.CellHalfLimit"/> 안에 든다.
    ///        경계(0.5)를 **한 개도** 넘지 않는다. 인스펙터 두께를 아무리 키워도 그렇다.
    ///
    /// 그리고 반대 방향의 회귀도 막는다 — 막대를 지우기만 하면 그건 개선이 아니라 후퇴다.
    /// `UP-CORE-12`(판정 원인 시각화)가 요구하는 「원인 세 종류가 구분된다」와
    /// 「정화된 칸이 어디였는지 읽힌다」를 각각 단정한다.
    ///
    /// 씬을 열지 않는다. 좌표 계산은 <see cref="PurifyMarkerLayout"/> 이 전부 들고 있고
    /// MonoBehaviour 는 그것을 Transform 에 바르기만 한다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class PurifyMarkerLayoutTests
    {
        /// <summary>독립 스위트로 등록됐을 때의 진입점.</summary>
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Append(ref passed, ref failed, report);

            report.Insert(0, "[상승] === 정화 표식 칸 단위화 Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        /// <summary>
        /// 다른 스위트의 집계에 이어 붙인다.
        ///
        /// 스위트 하나를 새로 만들면 `AscendTestMenu.AllSuites()` 와 `PrototypeSelfTest`
        /// 두 곳에 등록이 필요하고, **등록을 빠뜨린 검사는 돌지 않으면서 통과한 것처럼
        /// 보인다.** 이 배치에서 실제로 두 번 났다. 이어 붙일 수 있게 열어 둔다.
        /// </summary>
        public static void Append(ref int passed, ref int failed, StringBuilder report)
        {
            Run("표식이 칸 경계를 넘지 않는다 (G-SLOT-A ④ 구조적 불가)",
                TestNeverCrossesCellBoundary, ref passed, ref failed, report);
            Run("표식 장축이 ROI 폭의 35%에 닿을 수 없다 (G-SLOT-A ③ 구조적 불가)",
                TestSpanCannotReachPayline, ref passed, ref failed, report);
            Run("인스펙터 두께를 아무리 키워도 칸을 못 넘는다",
                TestThicknessIsClampedIntoCell, ref passed, ref failed, report);
            Run("이웃 칸의 표식 사이에 빈 틈이 남는다 (성분이 붙지 않는다)",
                TestNeighbourMarkersStayApart, ref passed, ref failed, report);

            Run("원인 세 종류가 서로 다른 형상 계통이다 (UP-CORE-12)",
                TestThreeCausesUseDifferentShapes, ref passed, ref failed, report);
            Run("원인 세 종류가 실제로 다른 표식 배치를 만든다",
                TestThreeCausesProduceDifferentLayouts, ref passed, ref failed, report);
            Run("정화된 칸이 어디였는지 표식으로 읽힌다 (UP-CORE-12)",
                TestEveryPurifiedCellIsMarked, ref passed, ref failed, report);
            Run("표식은 정화된 칸 밖으로 나가지 않는다",
                TestNoMarkerOnUnrelatedCell, ref passed, ref failed, report);

            Run("한 사건의 표식이 풀 상한을 넘지 않는다",
                TestSingleEventFitsInPool, ref passed, ref failed, report);
            Run("동시에 뜬 두 사건의 표식 합도 풀 상한을 넘지 않는다",
                TestConcurrentEventsFitInPool, ref passed, ref failed, report);
            Run("Needed 가 Build 와 같은 수를 말한다",
                TestNeededMatchesBuild, ref passed, ref failed, report);

            Run("결정론 — 칸 배열 순서가 달라도 같은 표식이 나온다",
                TestOrderIndependent, ref passed, ref failed, report);
            Run("할당 0 — 정화 시퀀스를 반복해도 힙 델타가 0이다",
                TestBuildAllocatesNothing, ref passed, ref failed, report);

            Run("셔터도 칸 안에 들고 페이라인이 아니다",
                TestShuttersStayInsideCells, ref passed, ref failed, report);
            Run("셔터 세 단계가 여전히 형태로 갈린다 (UP-FIX-20)",
                TestShutterStagesStillDiffer, ref passed, ref failed, report);
            Run("셔터 수가 PurifyMarkerView 의 상수와 일치한다",
                TestShutterCountMatchesConstants, ref passed, ref failed, report);
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

        // ── G-SLOT-A 를 구조적으로 불가능하게 만드는 두 축 ─────────────────────

        /// <summary>
        /// 「칸 경계를 2개 이상 횡단」은 **0개 횡단**이면 절대 성립하지 않는다.
        /// 3^9 이 아니라 2^9 개의 칸 집합 전부에 대해 세 형상을 다 만들어 확인한다 —
        /// 실제 판정이 어떤 집합을 줄지 추측하지 않기 위해서다.
        /// </summary>
        private static string TestNeverCrossesCellBoundary()
        {
            var buffer = NewBuffer();
            // 피치가 짧으면 두께가 칸에서 차지하는 비율이 커진다. 실제 씬(≈0.3 m)보다
            // 훨씬 빡빡한 값까지 넣어 본다 — 판을 줄이는 것은 씬 오너의 자유다.
            float[] pitches = { 0.12f, 0.2f, 0.35f, 0.8f };
            const float barThickness = 0.045f;

            foreach (PatternKind pattern in AllShapedPatterns())
            for (int mask = 0; mask < 512; mask++)
            {
                int[] cells = CellsOf(mask);
                if (cells.Length < 2) continue;

                int count = PurifyMarkerLayout.Build(pattern, cells, buffer);
                for (int i = 0; i < count; i++)
                foreach (float pitch in pitches)
                {
                    float thickness = PurifyMarkerLayout.ThicknessInPitch(in buffer[i], barThickness / pitch);
                    if (!PurifyMarkerLayout.WithinCell(in buffer[i], thickness))
                        return $"{pattern} · 칸집합 0x{mask:X3} · 표식 {i} 가 칸을 넘는다 " +
                               $"(중심 {buffer[i].Center} · 길이 {buffer[i].Length:0.###} · " +
                               $"두께 {thickness:0.###} · 피치 {pitch})";
                }
            }
            return null;
        }

        private static string TestSpanCannotReachPayline()
        {
            var buffer = NewBuffer();

            // ROI 폭 = 아홉 칸의 AABB 합집합 ≈ 3 피치. 통과선의 35% 는 1.05 피치다.
            float paylineSpan = 3f * 0.35f;
            float maxSpan = PurifyMarkerLayout.MaxSpan;
            if (maxSpan >= paylineSpan)
                return $"장축 상한 {maxSpan} 이 페이라인 기준 {paylineSpan:0.##} 에 닿는다";

            foreach (PatternKind pattern in AllShapedPatterns())
            for (int mask = 0; mask < 512; mask++)
            {
                int[] cells = CellsOf(mask);
                if (cells.Length < 2) continue;

                int count = PurifyMarkerLayout.Build(pattern, cells, buffer);
                for (int i = 0; i < count; i++)
                    if (buffer[i].Length > PurifyMarkerLayout.MaxSpan + 1e-4f)
                        return $"{pattern} · 칸집합 0x{mask:X3} · 표식 {i} 의 장축이 " +
                               $"{buffer[i].Length:0.###} 피치다 (상한 {PurifyMarkerLayout.MaxSpan})";
            }
            return null;
        }

        private static string TestThicknessIsClampedIntoCell()
        {
            var buffer = NewBuffer();

            foreach (PatternKind pattern in AllShapedPatterns())
            {
                int[] cells = CellsOf(0x1FF);
                int count = PurifyMarkerLayout.Build(pattern, cells, buffer);
                if (count == 0) continue;

                for (int i = 0; i < count; i++)
                {
                    // 인스펙터가 칸 열 배 두께를 요구해도 결과는 칸 안이어야 한다.
                    float thickness = PurifyMarkerLayout.ThicknessInPitch(in buffer[i], 10f);
                    if (!PurifyMarkerLayout.WithinCell(in buffer[i], thickness))
                        return $"{pattern} 표식 {i} 가 두께 요청 10 피치에서 칸을 넘는다 ({thickness:0.###})";
                    if (thickness > PurifyMarkerLayout.MaxThickness + 1e-4f)
                        return $"{pattern} 표식 {i} 의 두께 {thickness:0.###} 가 상한 " +
                               $"{PurifyMarkerLayout.MaxThickness} 를 넘는다";
                }
            }
            return null;
        }

        private static string TestNeighbourMarkersStayApart()
        {
            // 이웃 칸의 마주 보는 표식이 붙으면 그 순간 하나의 성분이 경계를 넘는다 —
            // 그것이 예전 「연결봉」이었다. 두 계통 모두에 틈이 남아야 한다.
            float stubGap = PurifyMarkerLayout.StubGapInPitch;
            if (stubGap < 0.35f)
                return $"연결 팔 사이 틈이 {stubGap:0.###} 피치뿐이다 — 두 팔이 하나로 붙는다";

            float outlineGap = 1f - PurifyMarkerLayout.OutlineSpan;
            if (outlineGap < 0.35f)
                return $"이웃 칸의 같은 변 사이 틈이 {outlineGap:0.###} 피치뿐이다";

            // 셰브런은 줄 방향과 평행한 조각을 아예 만들지 않으므로 이어붙을 대상이 없다.
            // 그래도 앞뒤 여유는 확인한다.
            float chevronFront = PurifyMarkerLayout.ChevronApex;
            float chevronBack = PurifyMarkerLayout.ChevronArm * PurifyMarkerLayout.ChevronCos
                              - PurifyMarkerLayout.ChevronApex;
            float chevronGap = 1f - chevronFront - chevronBack;
            if (chevronGap < 0.35f)
                return $"이웃 칸 셰브런 사이 틈이 {chevronGap:0.###} 피치뿐이다";

            return null;
        }

        // ── UP-CORE-12 가 죽지 않았는가 ───────────────────────────────────────

        private static string TestThreeCausesUseDifferentShapes()
        {
            PurifyMarkerShape scattered = PurifyMarkerLayout.ShapeFor(PatternKind.Scattered);
            PurifyMarkerShape line = PurifyMarkerLayout.ShapeFor(PatternKind.Line);
            PurifyMarkerShape cluster = PurifyMarkerLayout.ShapeFor(PatternKind.Cluster);
            PurifyMarkerShape jackpot = PurifyMarkerLayout.ShapeFor(PatternKind.FullBoard);

            if (scattered == PurifyMarkerShape.None) return "인접 개수 정화에 형상이 없다";
            if (line == PurifyMarkerShape.None) return "직선에 형상이 없다";
            if (cluster == PurifyMarkerShape.None) return "연결 붕괴에 형상이 없다";

            if (scattered == line || line == cluster || scattered == cluster)
                return $"원인 셋이 같은 형상을 쓴다 ({scattered}/{line}/{cluster}) — " +
                       "「전부 같은 이펙트면 실패다」(visual-criteria B-2 #6)";

            // 잭팟은 연결의 극단이므로 같은 계통을 쓴다. 맥동 횟수(3 vs 4)가 그 둘을 가른다.
            if (jackpot != cluster)
                return $"잭팟이 연결과 다른 계통({jackpot})이 됐다 — 의도한 설계가 아니다. " +
                       "바꾸려면 SpinPresenter.PulseCountFor 와 함께 봐야 한다";
            return null;
        }

        private static string TestThreeCausesProduceDifferentLayouts()
        {
            var a = NewBuffer();
            var b = NewBuffer();
            var c = NewBuffer();

            int[] elbow = { SpinBoard.Index(0, 0), SpinBoard.Index(1, 0), SpinBoard.Index(1, 1) };
            int[] row = { SpinBoard.Index(0, 0), SpinBoard.Index(1, 0), SpinBoard.Index(2, 0) };
            int[] block = { SpinBoard.Index(0, 0), SpinBoard.Index(0, 1),
                            SpinBoard.Index(1, 0), SpinBoard.Index(1, 1) };

            int na = PurifyMarkerLayout.Build(PatternKind.Scattered, elbow, a);
            int nb = PurifyMarkerLayout.Build(PatternKind.Line, row, b);
            int nc = PurifyMarkerLayout.Build(PatternKind.Cluster, block, c);

            if (na == 0) return "인접 개수 정화가 표식을 하나도 안 만든다";
            if (nb == 0) return "직선이 표식을 하나도 안 만든다";
            if (nc == 0) return "연결 붕괴가 표식을 하나도 안 만든다";

            if (Same(a, na, b, nb)) return "인접 정화와 직선의 표식 배치가 같다";
            if (Same(b, nb, c, nc)) return "직선과 연결 붕괴의 표식 배치가 같다";
            if (Same(a, na, c, nc)) return "인접 정화와 연결 붕괴의 표식 배치가 같다";

            // 형상 열거값만 다르고 치수가 같으면 회색조 정지 화면에서는 갈리지 않는다.
            // 세 계통의 조각 길이가 실제로 달라야 한다 — 외곽선 0.56 · 셰브런 0.36 · 팔 0.26.
            float la = MeanLength(a, na), lb = MeanLength(b, nb), lc = MeanLength(c, nc);
            if (Near(la, lb) || Near(lb, lc) || Near(la, lc))
                return $"세 계통의 조각 길이가 사실상 같다 ({la:0.###}/{lb:0.###}/{lc:0.###})";

            // 「칸 중심에서 얼마나 떨어져 있는가」도 외곽선만은 확실히 갈려야 한다.
            if (MeanRadius(a, na) <= MeanRadius(c, nc) + 0.05f)
                return "외곽선이 연결 팔보다 바깥에 있지 않다 — 테두리로 읽히지 않는다";
            return null;
        }

        private static string TestEveryPurifiedCellIsMarked()
        {
            var buffer = NewBuffer();

            for (int mask = 0; mask < 512; mask++)
            {
                PatternKind pattern = ClassifyOrNone(mask);
                if (pattern == PatternKind.None) continue;

                int[] cells = CellsOf(mask);
                int count = PurifyMarkerLayout.Build(pattern, cells, buffer);

                for (int c = 0; c < cells.Length; c++)
                {
                    bool marked = false;
                    for (int i = 0; i < count && !marked; i++) marked = buffer[i].Cell == cells[c];
                    if (!marked)
                        return $"{pattern} · 칸집합 0x{mask:X3} 의 칸 {cells[c]} 에 표식이 없다 — " +
                               "정화된 칸이 어디였는지 못 읽는다";
                }
            }
            return null;
        }

        private static string TestNoMarkerOnUnrelatedCell()
        {
            var buffer = NewBuffer();

            foreach (PatternKind pattern in AllShapedPatterns())
            for (int mask = 0; mask < 512; mask++)
            {
                int[] cells = CellsOf(mask);
                if (cells.Length < 2) continue;

                int count = PurifyMarkerLayout.Build(pattern, cells, buffer);
                for (int i = 0; i < count; i++)
                    if ((mask & (1 << buffer[i].Cell)) == 0)
                        return $"{pattern} · 칸집합 0x{mask:X3} 이 정화되지 않은 칸 " +
                               $"{buffer[i].Cell} 에 표식을 세웠다";
            }
            return null;
        }

        // ── 풀 ────────────────────────────────────────────────────────────────

        private static string TestSingleEventFitsInPool()
        {
            int peak = 0;
            PatternKind peakPattern = PatternKind.None;
            int peakMask = 0;

            for (int mask = 0; mask < 512; mask++)
            {
                PatternKind pattern = ClassifyOrNone(mask);
                if (pattern == PatternKind.None) continue;

                int need = PurifyMarkerLayout.Needed(pattern, CellsOf(mask));
                if (need > peak) { peak = need; peakPattern = pattern; peakMask = mask; }
            }

            if (peak > PurifyMarkerLayout.MaxPlacementsPerEvent)
                return $"최대 {peak}개({peakPattern} · 0x{peakMask:X3})가 상한 " +
                       $"{PurifyMarkerLayout.MaxPlacementsPerEvent}개를 넘는다";
            if (peak != PurifyMarkerLayout.MaxPlacementsPerEvent)
                return $"상한이 {PurifyMarkerLayout.MaxPlacementsPerEvent}인데 실제 최대는 {peak}다 " +
                       $"({peakPattern} · 0x{peakMask:X3}) — 상수가 실제를 재지 않으면 다음 사람이 그것을 믿고 줄인다";
            return null;
        }

        private static string TestConcurrentEventsFitInPool()
        {
            // 한 프레임에 저항체 두 종류가 각각 정화될 수 있다. 칸은 겹치지 않는다.
            var patterns = new PatternKind[512];
            var needs = new int[512];
            for (int mask = 0; mask < 512; mask++)
            {
                patterns[mask] = ClassifyOrNone(mask);
                needs[mask] = patterns[mask] == PatternKind.None
                    ? 0 : PurifyMarkerLayout.Needed(patterns[mask], CellsOf(mask));
            }

            int peak = 0;
            int peakA = 0, peakB = 0;
            for (int a = 0; a < 512; a++)
            {
                if (patterns[a] == PatternKind.None) continue;
                for (int b = a + 1; b < 512; b++)
                {
                    if (patterns[b] == PatternKind.None) continue;
                    if ((a & b) != 0) continue;               // 같은 칸을 두 종류가 차지할 수 없다
                    int total = needs[a] + needs[b];
                    if (total > peak) { peak = total; peakA = a; peakB = b; }
                }
            }

            if (peak > PurifyMarkerView.PoolRequirement)
                return $"동시 두 사건의 표식이 {peak}개(0x{peakA:X3} + 0x{peakB:X3})로 " +
                       $"풀 요구량 {PurifyMarkerView.PoolRequirement}개를 넘는다";
            return null;
        }

        private static string TestNeededMatchesBuild()
        {
            var buffer = NewBuffer();

            foreach (PatternKind pattern in AllShapedPatterns())
            for (int mask = 0; mask < 512; mask++)
            {
                int[] cells = CellsOf(mask);
                if (cells.Length < 2) continue;

                int needed = PurifyMarkerLayout.Needed(pattern, cells);
                int built = PurifyMarkerLayout.Build(pattern, cells, buffer);
                if (needed != built)
                    return $"{pattern} · 칸집합 0x{mask:X3}: Needed {needed} ≠ Build {built} — " +
                           "풀 검증이 실제와 다른 수를 믿게 된다";
            }
            return null;
        }

        // ── 결정론 · 할당 ─────────────────────────────────────────────────────

        private static string TestOrderIndependent()
        {
            var straight = NewBuffer();
            var shuffled = NewBuffer();

            foreach (PatternKind pattern in AllShapedPatterns())
            for (int mask = 0; mask < 512; mask++)
            {
                int[] cells = CellsOf(mask);
                if (cells.Length < 2) continue;

                int a = PurifyMarkerLayout.Build(pattern, cells, straight);

                // 결정론적 뒤섞기 — `UnityEngine.Random` 을 쓰면 판정 RNG 와 스트림을
                // 공유해 시드 재현이 깨진다(`TECH_SPEC` §7). 회전과 반전이면 충분하다.
                for (int rotation = 1; rotation < cells.Length; rotation++)
                {
                    int[] rotated = Rotate(cells, rotation);
                    int b = PurifyMarkerLayout.Build(pattern, rotated, shuffled);
                    if (a != b || !Same(straight, a, shuffled, b))
                        return $"{pattern} · 칸집합 0x{mask:X3} 이 배열 순서 {rotation} 에서 다른 그림을 낸다";
                }

                int[] reversed = Reverse(cells);
                int r = PurifyMarkerLayout.Build(pattern, reversed, shuffled);
                if (a != r || !Same(straight, a, shuffled, r))
                    return $"{pattern} · 칸집합 0x{mask:X3} 이 역순 배열에서 다른 그림을 낸다";
            }
            return null;
        }

        private static string TestBuildAllocatesNothing()
        {
            var buffer = NewBuffer();
            int[] elbow = { SpinBoard.Index(0, 0), SpinBoard.Index(1, 0), SpinBoard.Index(1, 1) };
            int[] row = { SpinBoard.Index(0, 0), SpinBoard.Index(1, 0), SpinBoard.Index(2, 0) };
            int[] full = CellsOf(0x1FF);

            // 워밍업 — 정적 방향 표의 최초 생성은 한 번뿐이고 그것까지 세면 판정이 오염된다.
            for (int i = 0; i < 4; i++)
            {
                PurifyMarkerLayout.Build(PatternKind.Scattered, elbow, buffer);
                PurifyMarkerLayout.Build(PatternKind.Line, row, buffer);
                PurifyMarkerLayout.Build(PatternKind.FullBoard, full, buffer);
                PurifyMarkerLayout.BuildRevealShutters(i, buffer);
                PurifyMarkerLayout.Needed(PatternKind.Cluster, full);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++)
            {
                PurifyMarkerLayout.Build(PatternKind.Scattered, elbow, buffer);
                PurifyMarkerLayout.Build(PatternKind.Line, row, buffer);
                PurifyMarkerLayout.Build(PatternKind.FullBoard, full, buffer);
                PurifyMarkerLayout.BuildRevealShutters(i % 5, buffer);
                PurifyMarkerLayout.Needed(PatternKind.Cluster, full);

                for (int p = 0; p < 24; p++)
                {
                    PurifyMarkerLayout.ThicknessInPitch(in buffer[p], 0.15f);
                    PurifyMarkerLayout.WithinCell(in buffer[p], 0.12f);
                }
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            long delta = after - before;
            if (delta != 0)
                return $"1,280회 반복에 {delta} B 를 할당했다 — 정화 연출이 도는 동안 " +
                       "매 프레임 이만큼 쌓인다 (UP-TECH-05: 워밍업 후 매 프레임 0 B)";
            return null;
        }

        // ── 순차 공개 셔터 ────────────────────────────────────────────────────

        private static string TestShuttersStayInsideCells()
        {
            var buffer = NewBuffer();
            float[] pitches = { 0.12f, 0.2f, 0.35f, 0.8f };
            const float barThickness = 0.045f;
            const float sealedMultiplier = 3f;   // 인스펙터 Range 상한

            for (int revealed = -2; revealed <= SpinPresenter.RevealComplete + 2; revealed++)
            {
                int count = PurifyMarkerLayout.BuildRevealShutters(revealed, buffer);
                for (int i = 0; i < count; i++)
                {
                    if (buffer[i].Length > PurifyMarkerLayout.MaxSpan + 1e-4f)
                        return $"진행도 {revealed} 의 셔터 {i} 장축이 {buffer[i].Length:0.###} 피치다";

                    foreach (float pitch in pitches)
                    {
                        float requested = barThickness *
                            (buffer[i].Shape == PurifyMarkerShape.ShutterSealed ? sealedMultiplier : 1f);
                        float thickness = PurifyMarkerLayout.ThicknessInPitch(in buffer[i], requested / pitch);
                        if (!PurifyMarkerLayout.WithinCell(in buffer[i], thickness))
                            return $"진행도 {revealed} 의 셔터 {i} 가 피치 {pitch} 에서 칸을 넘는다";
                    }
                }
            }
            return null;
        }

        private static string TestShutterStagesStillDiffer()
        {
            var buffer = NewBuffer();

            // 2열까지 열린 상태 — 한 프레임에 Open · Opening · Sealed 가 동시에 있다.
            int count = PurifyMarkerLayout.BuildRevealShutters(2, buffer);

            int sealedCount = 0, openCount = 0;
            float sealedOffset = 0f, openOffset = 0f;
            for (int i = 0; i < count; i++)
            {
                if (buffer[i].Shape == PurifyMarkerShape.ShutterSealed)
                {
                    sealedCount++;
                    sealedOffset = Math.Max(sealedOffset, Math.Abs(buffer[i].Center.y));
                }
                else if (buffer[i].Shape == PurifyMarkerShape.ShutterOpen)
                {
                    openCount++;
                    openOffset = Math.Max(openOffset, Math.Abs(buffer[i].Center.y));
                }
            }

            if (sealedCount != SpinBoard.Rows * PurifyMarkerView.SealedBarsPerCell)
                return $"닫힌 열의 표식이 {sealedCount}개다";
            if (openCount != SpinBoard.Rows * PurifyMarkerView.OpeningBarsPerCell)
                return $"열리는 열의 표식이 {openCount}개다";
            if (sealedCount == openCount)
                return "닫힌 열과 열리는 열의 표식 수가 같다 — 정지 화면에서 같은 그림이다";
            if (openOffset <= sealedOffset + 0.05f)
                return $"갈라진 짝이 중심에서 물러나지 않았다 (닫힘 {sealedOffset:0.##} / 열림 {openOffset:0.##})";
            return null;
        }

        private static string TestShutterCountMatchesConstants()
        {
            var buffer = NewBuffer();
            for (int revealed = -2; revealed <= SpinPresenter.RevealComplete + 2; revealed++)
            {
                int built = PurifyMarkerLayout.BuildRevealShutters(revealed, buffer);
                int declared = PurifyMarkerLayout.RevealBarsNeeded(revealed);
                if (built != declared)
                    return $"진행도 {revealed}: 실제 {built}개 ≠ RevealBarsNeeded {declared}개 — " +
                           "풀 검증이 실제와 다른 수를 믿게 된다";
            }
            return null;
        }

        // ── 도우미 ────────────────────────────────────────────────────────────

        private static PurifyMarkerPlacement[] NewBuffer()
            => new PurifyMarkerPlacement[PurifyMarkerView.PoolRequirement];

        private static PatternKind[] AllShapedPatterns()
            => new[] { PatternKind.Scattered, PatternKind.Line, PatternKind.Cluster, PatternKind.FullBoard };

        private static int[] CellsOf(int mask)
        {
            int n = 0;
            for (int cell = 0; cell < SpinBoard.Cells; cell++) if ((mask & (1 << cell)) != 0) n++;
            var cells = new int[n];
            int w = 0;
            for (int cell = 0; cell < SpinBoard.Cells; cell++) if ((mask & (1 << cell)) != 0) cells[w++] = cell;
            return cells;
        }

        /// <summary>
        /// 이 칸 집합이 실제 판정에서 어떤 패턴으로 나올 수 있는가.
        /// <c>SpinEngine</c> 의 등급 규칙을 **독립 재구현**한 것이다 — 복제가 아니라
        /// 재구현이어서, 규칙이 바뀌면 여기와 어긋나 눈에 띈다.
        /// </summary>
        private static PatternKind ClassifyOrNone(int mask)
        {
            int size = 0;
            for (int cell = 0; cell < SpinBoard.Cells; cell++) if ((mask & (1 << cell)) != 0) size++;
            if (size < 3) return PatternKind.None;
            if (size == SpinBoard.Cells) return PatternKind.FullBoard;

            bool connected = IsConnected(mask, size);
            if (connected && size >= 4) return PatternKind.Cluster;
            if (size == 3 && IsLine(mask)) return PatternKind.Line;
            if (connected && size == 3) return PatternKind.Scattered;
            return PatternKind.None;
        }

        private static bool IsLine(int mask)
        {
            foreach (int[] line in SpinBoard.Lines)
            {
                int lineMask = 0;
                foreach (int cell in line) lineMask |= 1 << cell;
                if (lineMask == mask) return true;
            }
            return false;
        }

        private static bool IsConnected(int mask, int size)
        {
            int start = -1;
            for (int cell = 0; cell < SpinBoard.Cells && start < 0; cell++)
                if ((mask & (1 << cell)) != 0) start = cell;
            if (start < 0) return false;

            var stack = new int[SpinBoard.Cells];
            var neighbours = new int[4];
            int top = 0;
            int seen = 1 << start;
            int count = 1;
            stack[top++] = start;

            while (top > 0)
            {
                int cell = stack[--top];
                int n = SpinBoard.OrthogonalNeighbours(cell, neighbours);
                for (int i = 0; i < n; i++)
                {
                    int next = neighbours[i];
                    if ((mask & (1 << next)) == 0) continue;
                    if ((seen & (1 << next)) != 0) continue;
                    seen |= 1 << next;
                    count++;
                    stack[top++] = next;
                }
            }
            return count == size;
        }

        private static int[] Rotate(int[] source, int by)
        {
            var result = new int[source.Length];
            for (int i = 0; i < source.Length; i++) result[i] = source[(i + by) % source.Length];
            return result;
        }

        private static int[] Reverse(int[] source)
        {
            var result = new int[source.Length];
            for (int i = 0; i < source.Length; i++) result[i] = source[source.Length - 1 - i];
            return result;
        }

        private static bool Same(PurifyMarkerPlacement[] a, int countA,
                                 PurifyMarkerPlacement[] b, int countB)
        {
            if (countA != countB) return false;
            for (int i = 0; i < countA; i++)
            {
                if (a[i].Cell != b[i].Cell) return false;
                if (a[i].Shape != b[i].Shape) return false;
                if (a[i].Center != b[i].Center) return false;
                if (a[i].Direction != b[i].Direction) return false;
                if (Math.Abs(a[i].Length - b[i].Length) > 1e-6f) return false;
            }
            return true;
        }

        /// <summary>표식이 칸 중심에서 떨어진 평균 거리.</summary>
        private static float MeanRadius(PurifyMarkerPlacement[] buffer, int count)
        {
            if (count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += buffer[i].Center.magnitude;
            return sum / count;
        }

        /// <summary>조각 하나의 평균 길이. 계통끼리 실제로 다른지 재는 값이다.</summary>
        private static float MeanLength(PurifyMarkerPlacement[] buffer, int count)
        {
            if (count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += buffer[i].Length;
            return sum / count;
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.05f;
    }
}
