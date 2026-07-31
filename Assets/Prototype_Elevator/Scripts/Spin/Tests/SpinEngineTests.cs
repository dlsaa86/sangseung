using System;
using System.Text;

namespace Ascend.Prototype.Spin.Tests
{
    /// <summary>
    /// SpinEngine의 헤드리스 검증 모음. 프로젝트에 테스트 어셈블리가 없어도 메뉴에서
    /// 실행할 수 있도록 NUnit에 의존하지 않고, 각 케이스가 실패 사유를 반환한다.
    /// </summary>
    public static class SpinEngineTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("같은 저항 3개 흩어짐 → Scattered", TestScattered, ref passed, ref failed, report);
            Run("정화된 칸은 서로 붙어 있다", TestPurifiedCellsAreContiguous, ref passed, ref failed, report);
            Run("붙어 있으면 모양을 가리지 않는다 (V자·ㄴ자)", TestBentShapesPurify, ref passed, ref failed, report);
            Run("같은 저항 3개 직선 3종 → LineKind", TestLines, ref passed, ref failed, report);
            Run("직교 연결 4개 → Cluster와 재충전", TestClusterRefill, ref passed, ref failed, report);
            Run("대각 연결 규칙", TestDiagonalConnectivity, ref passed, ref failed, report);
            Run("저항 2개 → 정화 없음", TestBelowThreshold, ref passed, ref failed, report);
            Run("9칸 동일 저항 → FullBoard", TestFullBoard, ref passed, ref failed, report);
            Run("같은 시드 → 완전 동일", TestDeterminism, ref passed, ref failed, report);
            Run("흡수체 잔류 → NetPower 차감", TestAbsorberResidual, ref passed, ref failed, report);
            Run("MaxCascadeDepth 상한", TestMaxCascadeDepth, ref passed, ref failed, report);
            Run("MaxCascadeDepth 마지막 정상 영혼 수확", TestMaxDepthHarvest, ref passed, ref failed, report);

            // ── 명세 정합 (MASTER_PRD §6, TECH_SPEC §7·§9·§11) ──
            Run("하드 캡 기본값이 20", TestHardCapIsTwenty, ref passed, ref failed, report);
            Run("하드 캡 도달이 플래그로 남음", TestCascadeCapFlag, ref passed, ref failed, report);
            Run("자연 종료는 캡 플래그가 서지 않음", TestNaturalEndNoCapFlag, ref passed, ref failed, report);
            Run("시드 파생이 층·스핀 좌표로 갈림", TestSeedDerivationSeparates, ref passed, ref failed, report);
            Run("좌표 시드가 앞선 진행과 무관하게 재현", TestSeedCoordinateReproduction, ref passed, ref failed, report);
            Run("다른 시드 → 결과가 고정되지 않음", TestDifferentSeedsDiffer, ref passed, ref failed, report);
            Run("가중치 0인 종류는 뽑히지 않음", TestWeightBoundaries, ref passed, ref failed, report);
            Run("제거 대상이 해당 종류 칸과 정확히 일치", TestRemovalCells, ref passed, ref failed, report);
            Run("재충전이 생존 칸을 건드리지 않음", TestRefillPreservesSurvivors, ref passed, ref failed, report);
            Run("증식체 잔류가 다음 스핀 가중치를 올림", TestProliferatorResidualWeight, ref passed, ref failed, report);
            Run("계약이 네 값을 함께 움직임", TestContractApplication, ref passed, ref failed, report);
            Run("로그 한 줄로 스핀 재현", TestLogLineReproduction, ref passed, ref failed, report);
            Run("직선 패턴이 그 줄의 3칸을 보고", TestLinePatternCells, ref passed, ref failed, report);
            Run("연결 패턴이 덩어리 칸을 보고", TestClusterPatternCells, ref passed, ref failed, report);
            Run("개수 정화는 모양 칸이 없다", TestScatteredHasNoPatternCells, ref passed, ref failed, report);
            Run("잭팟은 9칸을 모양으로 보고", TestFullBoardPatternCells, ref passed, ref failed, report);
            Run("패턴 칸 보고가 SpinBoard.Lines 를 오염시키지 않음", TestPatternCellsDoNotAliasLines, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Spin Engine Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        private static void Run(string name,
                                Func<string> test,
                                ref int passed,
                                ref int failed,
                                StringBuilder report)
        {
            try
            {
                string failure = test();
                if (string.IsNullOrEmpty(failure))
                {
                    passed++;
                    report.AppendLine($"  PASS  {name}");
                }
                else
                {
                    failed++;
                    report.AppendLine($"  FAIL  {name} — {failure}");
                }
            }
            catch (Exception exception)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외: {exception.Message}");
            }
        }

        /// <summary>
        /// 흩어진 세 칸. 배치는 (0,0)·(1,1)·(2,1) 로 **서로 붙어 있지 않다.**
        ///
        /// 기본 규칙(`RequireAdjacencyToPurify = true`)에서는 정화가 성립하지 않아야 한다 —
        /// 판 반대쪽 칸들이 아무 관계 없이 함께 사라지는 것이 어색하다는 지적에서 나온 규칙이다.
        /// 스위치를 끄면 예전대로 `Scattered` 로 성립한다. **두 경우를 모두 지킨다** —
        /// 한쪽만 검사하면 스위치가 실수로 뒤집혀도 통과한다.
        /// </summary>
        private static string TestScattered()
        {
            SpinBoard board = Board(
                SymbolKind.Absorber, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.NormalSoul);

            SpinRuleSet adjacent = BoardRules();
            adjacent.RequireAdjacencyToPurify = true;
            SpinResolution strict = Resolve(board, adjacent);

            // 단계 수로 세면 안 된다 — 정화가 하나도 없어도 정상 영혼 수확 단계는 남는다.
            // 처음에 `Steps.Length != 0` 으로 썼다가 이 함정에 걸렸다. 세야 할 것은 정화다.
            int strictPurifies = 0;
            foreach (CascadeStep step in strict.Steps)
                if (step.Purifies != null) strictPurifies += step.Purifies.Length;
            if (strictPurifies != 0)
                return $"인접을 요구하는데 흩어진 3개가 정화됐다 — 정화 {strictPurifies}건";

            SpinRuleSet loose = BoardRules();
            loose.RequireAdjacencyToPurify = false;
            SpinResolution result = Resolve(board, loose);
            if (result.Steps.Length != 1) return $"스위치를 껐는데 단계 수 {result.Steps.Length}";
            PurifyEvent eventData = result.Steps[0].Purifies[0];
            if (eventData.Pattern != PatternKind.Scattered) return $"패턴 {eventData.Pattern}";
            if (eventData.PatternMultiplier != 1f) return $"배수 {eventData.PatternMultiplier}";
            if (eventData.Cells.Length != 3) return $"정화 칸 수 {eventData.Cells.Length}";
            return null;
        }

        /// <summary>
        /// 정화된 칸은 같은 사건의 다른 칸과 **적어도 대각으로는 닿아 있어야** 한다.
        ///
        /// 판정에 인접을 요구해도 제거가 인접을 무시하면 소용이 없었다 —
        /// 직선 3개가 성립하는 순간 `CellsOf(board, kind)` 가 판에 있는 그 문양을
        /// 전부 지워서, 반대쪽 구석의 떨어진 칸까지 함께 사라졌다.
        /// "여전히 인접하지 않은 것도 같이 인정되는 것 같다"는 지적이 이것이었다.
        ///
        /// 인위적인 판 하나로는 못 잡는다. 실제 추첨 2000스핀을 전수로 훑어
        /// **한 건이라도** 떨어진 칸이 섞이면 실패한다.
        /// </summary>
        private static string TestPurifiedCellsAreContiguous()
        {
            int checkedEvents = 0;
            foreach (int seed in new[] { 1337, 4242, 271828, 8675309 })
            {
                FloorPlan plan = PrototypeCurriculum.For(8);
                SpinRuleSet rules = PrototypeCurriculum.BuildRules(in plan);
                var engine = new SpinEngine(seed);
                ResistanceContract none = ResistanceContract.None;
                ResidualState residual = ResidualState.Empty;

                for (int i = 0; i < 500; i++)
                {
                    SpinResolution res = engine.SpinWithSeed(
                        SpinSeed.Derive(seed, 8, i), rules, in none, in residual, 8, i);
                    if (res.Steps == null) continue;

                    foreach (CascadeStep step in res.Steps)
                    {
                        if (step.Purifies == null) continue;
                        foreach (PurifyEvent purify in step.Purifies)
                        {
                            checkedEvents++;
                            int[] cells = purify.Cells;
                            if (cells == null || cells.Length < 2) continue;

                            foreach (int cell in cells)
                            {
                                bool touches = false;
                                foreach (int other in cells)
                                {
                                    if (other == cell) continue;
                                    int dx = Math.Abs(cell / SpinBoard.Rows - other / SpinBoard.Rows);
                                    int dy = Math.Abs(cell % SpinBoard.Rows - other % SpinBoard.Rows);
                                    if (Math.Max(dx, dy) == 1) { touches = true; break; }
                                }
                                if (!touches)
                                    return $"시드 {seed} 스핀 {i}: {purify.Pattern} 정화에 " +
                                           $"떨어진 칸 {cell} 이 섞였다 " +
                                           $"(칸 [{string.Join(",", cells)}])";
                            }
                        }
                    }
                }
            }
            if (checkedEvents == 0) return "정화 사건이 하나도 없어 검사가 성립하지 않았다";
            return null;
        }

        /// <summary>
        /// 직선도 아니고 4칸도 안 되지만 **붙어 있는** 세 칸이 정화되는가.
        ///
        /// 인접을 요구하게 만들고 나니 V자와 작은 ㄴ자가 통째로 빠졌다.
        /// 직선 3개와 연결 4개 이상만 등급이 있었고, 그 사이가 비어 있었기 때문이다.
        /// "어떻게든 인접만 되어 있으면 인정해 달라"는 요청이 그 빈칸을 메운다.
        ///
        /// 판은 열 우선이다 — 인덱스 0·1·2 가 통관 0 이다.
        /// </summary>
        private static string TestBentShapesPurify()
        {
            // ㄴ자: (통관0,행0)·(통관0,행1)·(통관1,행1) = 인덱스 0·1·4
            var shapes = new (string name, int[] cells)[]
            {
                ("ㄴ자", new[] { 0, 1, 4 }),
                ("V자",  new[] { 0, 4, 6 }),   // (0,0)·(1,1)·(2,0) — 대각으로만 닿는다
            };

            foreach (var shape in shapes)
            {
                var cells = new SymbolKind[SpinBoard.Cells];
                for (int i = 0; i < cells.Length; i++) cells[i] = SymbolKind.NormalSoul;
                foreach (int c in shape.cells) cells[c] = SymbolKind.Absorber;

                SpinRuleSet rules = BoardRules();
                rules.DiagonalCountsAsConnected = true;   // V자는 대각 인접이다
                SpinResolution result = Resolve(Board(cells), rules);

                int purified = 0;
                foreach (CascadeStep step in result.Steps)
                    if (step.Purifies != null)
                        foreach (PurifyEvent p in step.Purifies)
                            if (p.Kind == SymbolKind.Absorber) purified += p.Cells.Length;

                if (purified != shape.cells.Length)
                    return $"{shape.name}: 정화된 칸 {purified}, 기대 {shape.cells.Length}";
            }
            return null;
        }

        private static string TestLines()
        {
            var cases = new[]
            {
                new LineCase(
                    new[] { 0, 1, 2 }, LineKind.Column),
                new LineCase(
                    new[] { 0, 3, 6 }, LineKind.Row),
                new LineCase(
                    new[] { 0, 4, 8 }, LineKind.Diagonal),
            };

            for (int c = 0; c < cases.Length; c++)
            {
                SymbolKind[] cells = NormalBoard();
                for (int i = 0; i < cases[c].Indices.Length; i++)
                    cells[cases[c].Indices[i]] = SymbolKind.Absorber;

                SpinResolution result = Resolve(Board(cells), BoardRules());
                if (result.Steps.Length != 1) return $"케이스 {c} 단계 수 {result.Steps.Length}";
                PurifyEvent eventData = result.Steps[0].Purifies[0];
                if (eventData.Pattern != PatternKind.Line)
                    return $"케이스 {c} 패턴 {eventData.Pattern}";
                if (eventData.Line != cases[c].Line)
                    return $"케이스 {c} 라인 {eventData.Line}, 기대 {cases[c].Line}";
            }
            return null;
        }

        private static string TestClusterRefill()
        {
            SpinRuleSet rules = BoardRules();
            rules.MaxCascadeDepth = 2;
            rules.Weights[SymbolKind.NormalSoul] = 0f;
            rules.Weights[SymbolKind.Absorber] = 1f;
            rules.Weights[SymbolKind.Proliferator] = 0f;
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul), rules);

            if (result.Steps.Length < 2) return $"단계 수 {result.Steps.Length}";
            if (result.Steps[0].Purifies[0].Pattern != PatternKind.Cluster)
                return $"첫 패턴 {result.Steps[0].Purifies[0].Pattern}";
            if (result.Steps[1].BoardBefore.CountOf(SymbolKind.Absorber) != 9)
                return "재충전 보드가 흡수체 9칸이 아님";
            return null;
        }

        private static string TestDiagonalConnectivity()
        {
            SpinBoard board = Board(
                SymbolKind.Absorber, SymbolKind.NormalSoul, SymbolKind.Absorber,
                SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.Absorber, SymbolKind.NormalSoul, SymbolKind.NormalSoul);

            SpinRuleSet orthogonalRules = BoardRules();
            SpinResolution orthogonal = Resolve(board, orthogonalRules);
            if (orthogonal.Steps[0].Purifies[0].Pattern == PatternKind.Cluster)
                return "직교 기본값에서 Cluster가 됨";

            SpinRuleSet diagonalRules = BoardRules();
            diagonalRules.DiagonalCountsAsConnected = true;
            SpinResolution diagonal = Resolve(board, diagonalRules);
            if (diagonal.Steps[0].Purifies[0].Pattern != PatternKind.Cluster)
                return $"대각 연결 패턴 {diagonal.Steps[0].Purifies[0].Pattern}";
            return null;
        }

        private static string TestBelowThreshold()
        {
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul), BoardRules());
            if (result.Steps.Length != 1) return $"단계 수 {result.Steps.Length}";
            if (result.Steps[0].Purifies.Length != 0) return "정화 이벤트가 발생함";
            if (result.PurifyPower != 0f) return $"정화 전력 {result.PurifyPower}";
            if (result.Residual.AbsorberCount != 2) return $"잔류 흡수체 {result.Residual.AbsorberCount}";
            return null;
        }

        private static string TestFullBoard()
        {
            SpinRuleSet rules = BoardRules();
            rules.MaxCascadeDepth = 1;
            rules.Weights[SymbolKind.NormalSoul] = 1f;
            rules.Weights[SymbolKind.Absorber] = 0f;
            rules.Weights[SymbolKind.Proliferator] = 0f;
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber,
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber,
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber), rules);
            if (result.Steps.Length != 1) return $"단계 수 {result.Steps.Length}";
            if (result.Steps[0].Purifies[0].Pattern != PatternKind.FullBoard)
                return $"패턴 {result.Steps[0].Purifies[0].Pattern}";
            if (result.Steps[0].Purifies[0].Cells.Length != 9) return "정화 칸 수가 9가 아님";
            return null;
        }

        private static string TestDeterminism()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            ResidualState residual = new ResidualState
            {
                ProliferatorCount = 2,
                NextProliferatorWeightAdd = 1.25f,
            };
            var firstEngine = new SpinEngine(7341);
            var secondEngine = new SpinEngine(7341);
            SpinResolution first = firstEngine.Spin(rules, ResistanceContract.None, residual);
            SpinResolution second = firstEngine.Spin(rules, ResistanceContract.None, residual);
            SpinResolution firstRepeat = secondEngine.Spin(rules, ResistanceContract.None, residual);
            SpinResolution secondRepeat = secondEngine.Spin(rules, ResistanceContract.None, residual);
            if (first.Seed == second.Seed)
                return "연속 스핀 시드가 같음";
            if (!Equivalent(first, firstRepeat) || !Equivalent(second, secondRepeat))
                return "동일 시드 결과가 다름";
            SpinResolution replay = new SpinEngine(0).SpinWithSeed(
                first.Seed, rules, ResistanceContract.None, residual);
            if (!Equivalent(first, replay))
                return "기록된 스핀 시드로 단일 스핀 재현 실패";
            if (rules.WeightOf(SymbolKind.Proliferator) != 2.5f)
                return "입력 rules가 변형됨";
            return null;
        }

        private static string TestAbsorberResidual()
        {
            SpinRuleSet rules = BoardRules();
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul), rules);
            float expectedLoss = 2f * rules.AbsorberResidualPowerLoss;
            if (result.Residual.StoredPowerLoss != expectedLoss)
                return $"차감 {result.Residual.StoredPowerLoss}, 기대 {expectedLoss}";
            if (result.NetPower != result.GrossPower - expectedLoss)
                return $"NetPower {result.NetPower}, Gross {result.GrossPower}";
            return null;
        }

        private static string TestMaxCascadeDepth()
        {
            SpinRuleSet rules = BoardRules();
            rules.MaxCascadeDepth = 3;
            rules.Weights[SymbolKind.NormalSoul] = 0f;
            rules.Weights[SymbolKind.Absorber] = 0f;
            rules.Weights[SymbolKind.Proliferator] = 1000000f;
            SpinResolution result = Resolve(Board(
                SymbolKind.Proliferator, SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.Proliferator, SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul), rules);
            if (result.Steps.Length != rules.MaxCascadeDepth)
                return $"단계 수 {result.Steps.Length}, 상한 {rules.MaxCascadeDepth}";
            return null;
        }

        private static string TestMaxDepthHarvest()
        {
            SpinRuleSet rules = BoardRules();
            rules.MaxCascadeDepth = 1;
            rules.Weights[SymbolKind.NormalSoul] = 1f;
            rules.Weights[SymbolKind.Absorber] = 0f;
            rules.Weights[SymbolKind.Proliferator] = 0f;
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul), rules);

            // 최초 보드의 정상 영혼 5개와 최대 깊이 재충전 정상 영혼 9개 모두
            // 1단계 연쇄 배수로 수확되어야 한다.
            if (result.Steps.Length != 1) return $"단계 수 {result.Steps.Length}";
            if (result.Steps[0].NormalSoulsHarvested != 14)
                return $"수확 수 {result.Steps[0].NormalSoulsHarvested}, 기대 14";
            // 상수 140 을 박아 두었다가 `NormalSoulValue` 가 10→14 로 바뀌자 깨졌다.
            // 이 테스트가 지키려는 것은 "14개가 1단계 배수로 수확된다"이지 특정 숫자가
            // 아니다. 규칙에서 값을 끌어와야 밸런스 조정이 테스트를 깨지 않는다.
            float expectedPower = 14 * rules.NormalSoulValue;
            if (Math.Abs(result.NormalSoulPower - expectedPower) > 0.001f)
                return $"정상 영혼 전력 {result.NormalSoulPower}, 기대 {expectedPower}";
            if (result.FinalBoard.CountOf(SymbolKind.NormalSoul) != 0)
                return "최대 깊이 종료 후 정상 영혼이 남음";
            return null;
        }

        // ── 명세 정합 테스트 ──

        private static string TestHardCapIsTwenty()
        {
            // MASTER_PRD §6 / TECH_SPEC §9가 못박은 값이다. 밸런스 조정으로 낮추면
            // "20회까지 간다"는 명세를 다시는 검증할 수 없게 된다.
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            if (rules.MaxCascadeDepth != 20)
                return $"기본 하드 캡 {rules.MaxCascadeDepth}, 명세 20";
            if (rules.Clone().MaxCascadeDepth != 20)
                return "Clone이 하드 캡을 옮기지 않음";
            return null;
        }

        private static string TestCascadeCapFlag()
        {
            // 재충전이 항상 증식체 9칸을 만들어 스스로를 다시 터뜨리는 판. 규칙이 끊지
            // 않으면 영원히 돈다 — 무한 루프 방지선이 실제로 작동하는지 보는 유일한 케이스다.
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.Weights[SymbolKind.NormalSoul] = 0f;
            rules.Weights[SymbolKind.Absorber] = 0f;
            rules.Weights[SymbolKind.Proliferator] = 1000000f;
            SpinResolution result = Resolve(Board(
                SymbolKind.Proliferator, SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.Proliferator, SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul), rules);

            if (result.Steps.Length != 20)
                return $"단계 수 {result.Steps.Length}, 기대 20";
            if (!result.CascadeCapReached)
                return "캡에 걸렸는데 CascadeCapReached=false — 자연 종료와 구분 불가";
            if (!result.ToLogLine().Contains("cap"))
                return "로그 한 줄에 캡 표시가 없음";
            if (string.IsNullOrEmpty(result.DescribeCascade()))
                return "캡 진단 로그가 비어 있음";
            return null;
        }

        private static string TestNaturalEndNoCapFlag()
        {
            // 하드 캡을 20으로 열어둔 채 1단계에서 끝나는 판. 캡 플래그가 여기서도 서면
            // 플래그가 아무것도 구분하지 못한다는 뜻이다.
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.NormalSoul), rules);

            if (result.Steps.Length != 1) return $"단계 수 {result.Steps.Length}";
            if (result.CascadeCapReached) return "자연 종료인데 캡 플래그가 섬";
            return null;
        }

        private static string TestSeedDerivationSeparates()
        {
            // (층 2, 스핀 0)과 (층 1, 스핀 1)이 겹치면 좌표가 좌표 구실을 못 한다.
            const int runSeed = 4242;
            var seen = new System.Collections.Generic.Dictionary<int, string>();
            for (int floor = 1; floor <= 10; floor++)
            {
                for (int spin = 0; spin < 12; spin++)
                {
                    int seed = SpinSeed.Derive(runSeed, floor, spin);
                    if (seed == int.MinValue) return $"({floor},{spin})에서 int.MinValue 반환";
                    string key = $"{floor}/{spin}";
                    if (seen.TryGetValue(seed, out string other))
                        return $"시드 충돌 {seed}: {other} vs {key}";
                    seen[seed] = key;

                    if (SpinSeed.Derive(runSeed, floor, spin) != seed)
                        return $"({floor},{spin}) 파생이 불안정";
                }
            }
            if (SpinSeed.Derive(runSeed, 1, 0) == SpinSeed.Derive(runSeed + 1, 1, 0))
                return "런 시드가 달라도 같은 시드가 나옴";
            return null;
        }

        private static string TestSeedCoordinateReproduction()
        {
            // 좌표 파생의 존재 이유: 앞선 층에서 스핀을 몇 번 했든 "3층 2번째 스핀"이
            // 같아야 한다. 순차 스트림이면 여기서 깨진다.
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            const int runSeed = 991;
            int target = SpinSeed.Derive(runSeed, 3, 2);

            var clean = new SpinEngine(runSeed);
            SpinResolution expected = clean.SpinWithSeed(
                target, rules, ResistanceContract.None, ResidualState.Empty, 3, 2);

            // 앞선 진행을 흉내 내 엔진 내부 난수를 소모시킨다.
            var used = new SpinEngine(runSeed);
            for (int i = 0; i < 7; i++)
                used.Spin(rules, ResistanceContract.None, ResidualState.Empty);
            SpinResolution actual = used.SpinWithSeed(
                target, rules, ResistanceContract.None, ResidualState.Empty, 3, 2);

            if (!Equivalent(expected, actual))
                return "앞선 스핀 횟수에 따라 같은 좌표의 결과가 달라짐";
            if (actual.Floor != 3 || actual.SpinIndex != 2 || actual.RunSeed != runSeed)
                return $"좌표 기록 오류 floor={actual.Floor} idx={actual.SpinIndex} run={actual.RunSeed}";
            return null;
        }

        private static string TestDifferentSeedsDiffer()
        {
            // TECH_SPEC §11 "다른 시드에서 결과가 고정되지 않음".
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            var boards = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                SpinResolution r = new SpinEngine(0).SpinWithSeed(
                    SpinSeed.Derive(5000 + i, 1, 0), rules,
                    ResistanceContract.None, ResidualState.Empty);
                boards.Add(r.InitialBoard.ToString());
            }
            // 40개 시드가 3종 심볼 9칸에서 전부 같은 판을 낼 확률은 사실상 0이다.
            if (boards.Count < 20)
                return $"서로 다른 초기 보드 {boards.Count}종 / 40시드 — 시드가 결과를 가르지 못함";
            return null;
        }

        private static string TestWeightBoundaries()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.Weights[SymbolKind.NormalSoul] = 0f;
            rules.Weights[SymbolKind.Absorber] = 1f;
            rules.Weights[SymbolKind.Proliferator] = 0f;

            for (int i = 0; i < 200; i++)
            {
                SpinBoard board = new SpinEngine(0).SpinWithSeed(
                    SpinSeed.Derive(i, 1, 0), rules,
                    ResistanceContract.None, ResidualState.Empty).InitialBoard;
                if (board.CountOf(SymbolKind.Absorber) != 9)
                    return $"시드 {i}: 가중치 0인 종류가 뽑힘 — {board}";
            }

            // 모든 가중치가 0이면 조용히 아무거나 뽑지 말고 터져야 한다.
            SpinRuleSet dead = SpinRuleSet.CreateDefault();
            dead.Weights[SymbolKind.NormalSoul] = 0f;
            dead.Weights[SymbolKind.Absorber] = 0f;
            dead.Weights[SymbolKind.Proliferator] = 0f;
            try
            {
                new SpinEngine(0).SpinWithSeed(1, dead, ResistanceContract.None, ResidualState.Empty);
                return "가중치 총합 0인데 예외가 없음";
            }
            catch (InvalidOperationException) { }
            return null;
        }

        private static string TestRemovalCells()
        {
            // 두 저항체가 동시에 정화될 때 서로의 칸을 섞어 가져가면 UI 하이라이트가
            // 거짓말을 하게 된다.
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.Absorber,     SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.Absorber,     SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.Absorber,     SymbolKind.Proliferator, SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            if (result.Steps[0].Purifies.Length != 2)
                return $"정화 이벤트 {result.Steps[0].Purifies.Length}종, 기대 2";

            foreach (PurifyEvent p in result.Steps[0].Purifies)
            {
                if (p.Cells.Length != 3) return $"{p.Kind} 정화 칸 {p.Cells.Length}, 기대 3";
                foreach (int cell in p.Cells)
                {
                    if (board[cell] != p.Kind)
                        return $"{p.Kind} 이벤트가 {board[cell]} 칸({cell})을 가져감";
                    if (result.Steps[0].BoardAfter[cell] != SymbolKind.Empty)
                        return $"정화 칸 {cell}이 비워지지 않음";
                }
            }
            return null;
        }

        private static string TestRefillPreservesSurvivors()
        {
            // 재충전은 빈칸만 채운다. 살아남은 칸이 흔들리면 "무엇이 새로 들어왔는가"를
            // 플레이어가 따라갈 수 없다(visual-criteria B-2.7).
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.MaxCascadeDepth = 2;
            rules.Weights[SymbolKind.NormalSoul] = 0f;
            rules.Weights[SymbolKind.Absorber] = 0f;
            rules.Weights[SymbolKind.Proliferator] = 1f;

            // 흡수체 4개가 직교로 붙어 Cluster. 증식체 2개는 문턱(3) 아래라 살아남는다.
            SpinBoard before = Board(
                SymbolKind.Absorber,     SymbolKind.Absorber,     SymbolKind.Proliferator,
                SymbolKind.Absorber,     SymbolKind.Absorber,     SymbolKind.Proliferator,
                SymbolKind.NormalSoul,   SymbolKind.NormalSoul,   SymbolKind.NormalSoul);
            SpinResolution result = Resolve(before, rules);

            if (result.Steps.Length < 2) return $"캐스케이드가 열리지 않음 (단계 {result.Steps.Length})";
            if (result.Steps[0].Purifies[0].Pattern != PatternKind.Cluster)
                return $"첫 패턴 {result.Steps[0].Purifies[0].Pattern}";

            SpinBoard after = result.Steps[0].BoardAfter;
            // 증식체 2칸(인덱스 2, 5)은 정화도 수확도 되지 않았으므로 그대로여야 한다.
            if (after[2] != SymbolKind.Proliferator || after[5] != SymbolKind.Proliferator)
                return $"생존 칸이 재충전으로 덮어써짐 — {after}";
            if (after.HasEmpty())
                return $"재충전 후에도 빈칸이 남음 — {after}";

            // 재충전 결과 자체도 시드에 대해 결정론적이어야 한다.
            SpinResolution again = Resolve(before, rules);
            if (!after.Equals(again.Steps[0].BoardAfter))
                return "같은 시드에서 재충전 결과가 다름";
            return null;
        }

        private static string TestProliferatorResidualWeight()
        {
            SpinRuleSet rules = BoardRules();
            SpinResolution result = Resolve(Board(
                SymbolKind.Proliferator, SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul,   SymbolKind.NormalSoul,   SymbolKind.NormalSoul,
                SymbolKind.NormalSoul,   SymbolKind.NormalSoul,   SymbolKind.NormalSoul), rules);

            float expected = 2f * rules.ProliferatorResidualWeightAdd;
            if (Math.Abs(result.Residual.NextProliferatorWeightAdd - expected) > 0.0001f)
                return $"가중치 가산 {result.Residual.NextProliferatorWeightAdd}, 기대 {expected}";
            if (result.Residual.ProliferatorCount != 2)
                return $"잔류 증식체 {result.Residual.ProliferatorCount}";

            // 가산이 실제로 다음 스핀 추첨에 들어가는지. 증식체 기본 가중치를 0으로 두고
            // 잔류만 크게 주면, 잔류가 반영될 때만 증식체가 나온다.
            SpinRuleSet next = SpinRuleSet.CreateDefault();
            next.Weights[SymbolKind.NormalSoul] = 1f;
            next.Weights[SymbolKind.Absorber] = 0f;
            next.Weights[SymbolKind.Proliferator] = 0f;
            var carried = new ResidualState { ProliferatorCount = 2, NextProliferatorWeightAdd = 1000000f };

            SpinBoard withResidual = new SpinEngine(0)
                .SpinWithSeed(77, next, ResistanceContract.None, carried).InitialBoard;
            SpinBoard without = new SpinEngine(0)
                .SpinWithSeed(77, next, ResistanceContract.None, ResidualState.Empty).InitialBoard;

            if (withResidual.CountOf(SymbolKind.Proliferator) != 9)
                return $"잔류 가중치가 추첨에 반영되지 않음 — {withResidual}";
            if (without.CountOf(SymbolKind.NormalSoul) != 9)
                return $"잔류 없는 대조군이 오염됨 — {without}";
            return null;
        }

        private static string TestContractApplication()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            float baseWeight = rules.WeightOf(SymbolKind.Absorber);
            ResistanceContract contract = PrototypeCurriculum.AbsorberContract;
            rules.Apply(in contract);

            if (Math.Abs(rules.WeightOf(SymbolKind.Absorber) - baseWeight * contract.AppearanceMultiplier) > 0.0001f)
                return "출현 가중치 보정이 적용되지 않음";
            if (Math.Abs(rules.PurifyRewardFor(SymbolKind.Absorber) - contract.PurifyRewardMultiplier) > 0.0001f)
                return "정화 보상 보정이 적용되지 않음";
            if (Math.Abs(rules.PatternBonusFor(SymbolKind.Absorber) - contract.PatternBonusAdd) > 0.0001f)
                return "패턴 가산이 적용되지 않음";
            if (Math.Abs(rules.ResidualPenaltyFor(SymbolKind.Absorber) - contract.ResidualPenaltyMultiplier) > 0.0001f)
                return "잔류 대가 보정이 적용되지 않음";

            // 대상이 아닌 저항체는 건드리지 않는다.
            if (Math.Abs(rules.PurifyRewardFor(SymbolKind.Proliferator) - 1f) > 0.0001f)
                return "계약 대상이 아닌 저항체가 함께 변함";

            // 계약이 보상만 올리는 "그냥 좋은 버프"가 되면 선택이 성립하지 않는다.
            if (contract.PurifyRewardMultiplier <= 1f || contract.ResidualPenaltyMultiplier <= 1f ||
                contract.AppearanceMultiplier <= 1f)
                return "계약이 보상·위험을 함께 올리지 않음";

            // 잔류 대가가 실제 차감으로 이어지는지.
            SpinRuleSet penalised = BoardRules();
            penalised.Apply(in contract);
            SpinBoard board = Board(
                SymbolKind.Absorber,   SymbolKind.Absorber,   SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul);
            float plainLoss = Resolve(board, BoardRules()).Residual.StoredPowerLoss;
            float contractLoss = Resolve(board, penalised).Residual.StoredPowerLoss;
            if (contractLoss <= plainLoss)
                return $"계약 잔류 대가 {contractLoss} ≤ 무계약 {plainLoss}";
            return null;
        }

        private static string TestLogLineReproduction()
        {
            // TECH_SPEC §11 "SpinResult 직렬화 또는 로그 재현".
            // 로그 한 줄에서 좌표만 뽑아 같은 스핀을 다시 만들 수 있어야 한다.
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            const int runSeed = 20260730;
            SpinResolution original = new SpinEngine(runSeed).SpinWithSeed(
                SpinSeed.Derive(runSeed, 4, 1), rules,
                ResistanceContract.None, ResidualState.Empty, 4, 1);

            string line = original.ToLogLine();
            int parsedRun = ParseInt(line, "run=");
            int parsedFloor = ParseInt(line, "floor=");
            int parsedIndex = ParseInt(line, "idx=");
            int parsedSeed = ParseInt(line, "seed=");

            if (parsedRun != runSeed || parsedFloor != 4 || parsedIndex != 1)
                return $"로그 좌표 파싱 실패 — {line}";
            if (SpinSeed.Derive(parsedRun, parsedFloor, parsedIndex) != parsedSeed)
                return "로그의 좌표에서 기록된 스핀 시드를 다시 만들 수 없음";

            SpinResolution replay = new SpinEngine(parsedRun).SpinWithSeed(
                parsedSeed, rules, ResistanceContract.None, ResidualState.Empty,
                parsedFloor, parsedIndex);
            if (!Equivalent(original, replay))
                return "로그로 재현한 스핀이 원본과 다름";
            if (replay.ToLogLine() != line)
                return $"재현 로그가 원본과 다름\n  원본 {line}\n  재현 {replay.ToLogLine()}";
            return null;
        }

        private static int ParseInt(string line, string key)
        {
            int start = line.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return int.MinValue;
            start += key.Length;
            int end = start;
            while (end < line.Length && (char.IsDigit(line[end]) || (end == start && line[end] == '-'))) end++;
            return int.TryParse(line.Substring(start, end - start), out int value) ? value : int.MinValue;
        }


        // ── 패턴 칸 보고 (화면이 "왜 터졌는가"를 형태로 그리기 위한 데이터) ──

        private static string TestLinePatternCells()
        {
            // 세로줄(통관 0)에 흡수체 3개 + **떨어진 곳에 1개**.
            //
            // 예전에는 줄이 터지면서 떨어진 1개까지 4개가 사라졌다. 판정에 인접을
            // 요구해도 제거가 인접을 무시했기 때문이다. 이제는 줄의 3칸만 터지고
            // 떨어진 것은 판에 남는다 — 그것이 "붙은 것만 터진다"의 뜻이다.
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.Absorber,   SymbolKind.Absorber,   SymbolKind.Absorber,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.Absorber);
            SpinResolution result = Resolve(board, rules);

            PurifyEvent purify = result.Steps[0].Purifies[0];
            if (purify.Pattern != PatternKind.Line) return $"패턴 {purify.Pattern}";
            if (purify.Cells.Length != 3)
                return $"정화 칸 {purify.Cells.Length}, 기대 3 (떨어진 흡수체가 함께 터졌다)";
            if (result.FinalBoard.CountOf(SymbolKind.Absorber) != 1)
                return $"떨어진 흡수체가 판에 남지 않았다 — 남은 수 " +
                       $"{result.FinalBoard.CountOf(SymbolKind.Absorber)}";
            if (purify.PatternCells == null) return "PatternCells 가 null";
            if (purify.PatternCells.Length != 3)
                return $"모양 칸 {purify.PatternCells.Length}, 기대 3";

            foreach (int cell in purify.PatternCells)
            {
                if (board[cell] != SymbolKind.Absorber) return $"모양 칸 {cell} 이 흡수체가 아님";
                if (SpinBoard.ColumnOf(cell) != 0) return $"모양 칸 {cell} 이 통관 0 이 아님";
            }
            if (purify.Line != LineKind.Column) return $"라인 종류 {purify.Line}";
            return null;
        }

        private static string TestClusterPatternCells()
        {
            // 흡수체 4개가 직교로 붙은 덩어리 + 떨어진 1개. 덩어리 칸만 보고해야 한다.
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.MaxCascadeDepth = 1;
            SpinBoard board = Board(
                SymbolKind.Absorber,   SymbolKind.Absorber,   SymbolKind.NormalSoul,
                SymbolKind.Absorber,   SymbolKind.Absorber,   SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.Absorber);
            SpinResolution result = Resolve(board, rules);

            PurifyEvent purify = result.Steps[0].Purifies[0];
            if (purify.Pattern != PatternKind.Cluster) return $"패턴 {purify.Pattern}";
            // 덩어리 4칸만 터진다. 떨어진 1개는 판에 남는다(재충전이 덮어쓸 수 있으므로
            // 여기서는 정화 칸 수만 본다).
            if (purify.Cells.Length != 4)
                return $"정화 칸 {purify.Cells.Length}, 기대 4 (떨어진 흡수체가 함께 터졌다)";
            if (purify.PatternCells == null) return "PatternCells 가 null";
            if (purify.PatternCells.Length != 4)
                return $"덩어리 칸 {purify.PatternCells.Length}, 기대 4";

            // 인덱스 0,1,3,4 가 붙어 있는 네 칸. 8 번은 떨어져 있으므로 들어오면 안 된다.
            var expected = new[] { 0, 1, 3, 4 };
            for (int i = 0; i < expected.Length; i++)
                if (purify.PatternCells[i] != expected[i])
                    return $"덩어리 칸 {string.Join(",", purify.PatternCells)}, 기대 {string.Join(",", expected)}";
            return null;
        }

        private static string TestScatteredHasNoPatternCells()
        {
            SpinRuleSet rules = BoardRules();
            rules.RequireAdjacencyToPurify = false;   // 흩어진 정화가 성립해야 검사할 수 있다
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber,   SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Absorber,   SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Absorber,   SymbolKind.NormalSoul), rules);

            PurifyEvent purify = result.Steps[0].Purifies[0];
            if (purify.Pattern != PatternKind.Scattered) return $"패턴 {purify.Pattern}";
            // 흩어진 정화는 그릴 모양이 없다. 빈 값이어야 화면이 선을 잘못 긋지 않는다.
            if (purify.PatternCells != null && purify.PatternCells.Length > 0)
                return $"모양 칸이 {purify.PatternCells.Length}개 보고됨";
            return null;
        }

        private static string TestFullBoardPatternCells()
        {
            SpinRuleSet rules = BoardRules();
            rules.Weights[SymbolKind.NormalSoul] = 1f;
            rules.Weights[SymbolKind.Absorber] = 0f;
            rules.Weights[SymbolKind.Proliferator] = 0f;
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber,
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber,
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber), rules);

            PurifyEvent purify = result.Steps[0].Purifies[0];
            if (purify.Pattern != PatternKind.FullBoard) return $"패턴 {purify.Pattern}";
            if (purify.PatternCells == null || purify.PatternCells.Length != 9)
                return $"모양 칸 {purify.PatternCells?.Length ?? 0}, 기대 9";
            return null;
        }

        private static string TestPatternCellsDoNotAliasLines()
        {
            // 보고된 배열이 SpinBoard.Lines 원본이면, 소비자가 정렬만 해도
            // 이후 모든 판정의 직선 정의가 조용히 바뀐다.
            SpinRuleSet rules = BoardRules();
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber,   SymbolKind.Absorber,   SymbolKind.Absorber,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul), rules);

            int[] reported = result.Steps[0].Purifies[0].PatternCells;
            foreach (int[] line in SpinBoard.Lines)
                if (ReferenceEquals(reported, line))
                    return "보고된 배열이 SpinBoard.Lines 원본과 같은 참조다";

            // 실제로 망가뜨려 본 뒤 원본이 멀쩡한지 확인한다.
            int[] before = { SpinBoard.Lines[0][0], SpinBoard.Lines[0][1], SpinBoard.Lines[0][2] };
            reported[0] = -999;
            for (int i = 0; i < 3; i++)
                if (SpinBoard.Lines[0][i] != before[i])
                    return "보고 배열을 수정했더니 SpinBoard.Lines 가 바뀌었다";
            return null;
        }

        private static SpinResolution Resolve(SpinBoard board, SpinRuleSet rules)
        {
            var engine = new SpinEngine(1234);
            return engine.ResolveBoard(rules, ResistanceContract.None, ResidualState.Empty, board);
        }

        private static SpinRuleSet BoardRules()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.MaxCascadeDepth = 1;
            return rules;
        }

        private static SpinBoard Board(params SymbolKind[] cells)
        {
            return SpinBoard.FromArray(cells);
        }

        private static SymbolKind[] NormalBoard()
        {
            return new[]
            {
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
            };
        }

        private static bool Equivalent(SpinResolution a, SpinResolution b)
        {
            // 좌표(RunSeed/Floor/SpinIndex)는 출처 정보라 비교하지 않는다. 같은 스핀 시드를
            // 다른 엔진에서 재생해도 결과 자체는 같아야 한다는 것이 이 함수의 주장이다.
            if (a.Seed != b.Seed || !a.InitialBoard.Equals(b.InitialBoard) || !a.FinalBoard.Equals(b.FinalBoard))
                return false;
            if (a.CascadeCapReached != b.CascadeCapReached) return false;
            if (a.NormalSoulPower != b.NormalSoulPower || a.PurifyPower != b.PurifyPower ||
                a.GrossPower != b.GrossPower || a.NetPower != b.NetPower)
                return false;
            if (a.Residual.AbsorberCount != b.Residual.AbsorberCount ||
                a.Residual.ProliferatorCount != b.Residual.ProliferatorCount ||
                a.Residual.StoredPowerLoss != b.Residual.StoredPowerLoss ||
                a.Residual.NextProliferatorWeightAdd != b.Residual.NextProliferatorWeightAdd)
                return false;
            if (a.Steps.Length != b.Steps.Length) return false;

            for (int i = 0; i < a.Steps.Length; i++)
            {
                CascadeStep left = a.Steps[i];
                CascadeStep right = b.Steps[i];
                if (left.Depth != right.Depth || !left.BoardBefore.Equals(right.BoardBefore) ||
                    !left.BoardAfter.Equals(right.BoardAfter) ||
                    left.NormalSoulsHarvested != right.NormalSoulsHarvested ||
                    left.NormalSoulPower != right.NormalSoulPower ||
                    left.ChainMultiplier != right.ChainMultiplier ||
                    left.StepPower != right.StepPower ||
                    left.Purifies.Length != right.Purifies.Length)
                    return false;
                for (int p = 0; p < left.Purifies.Length; p++)
                {
                    PurifyEvent lp = left.Purifies[p];
                    PurifyEvent rp = right.Purifies[p];
                    if (lp.Kind != rp.Kind || lp.Pattern != rp.Pattern || lp.Line != rp.Line ||
                        lp.Power != rp.Power || lp.PatternMultiplier != rp.PatternMultiplier ||
                        lp.Cells.Length != rp.Cells.Length)
                        return false;
                    for (int c = 0; c < lp.Cells.Length; c++)
                        if (lp.Cells[c] != rp.Cells[c]) return false;
                }
            }
            return true;
        }

        private readonly struct LineCase
        {
            public readonly int[] Indices;
            public readonly LineKind Line;

            public LineCase(int[] indices, LineKind line)
            {
                Indices = indices;
                Line = line;
            }
        }
    }
}
