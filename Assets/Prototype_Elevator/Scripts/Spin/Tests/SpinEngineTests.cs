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

            // ── PD-30 (2026-08-06) — 판정 규칙 층위 2종 ──
            Run("검침원: 잔류를 장부에서만 지운다 (판은 그대로)", TestResidualForgiveIsLedgerOnly, ref passed, ref failed, report);
            Run("검침원: 면제 0이면 옛 값과 한 자리도 안 다르다", TestResidualForgiveRollsBack, ref passed, ref failed, report);
            Run("연쇄 코일: 칸 0이면 난수를 건드리지 않는다", TestExtraRerollZeroIsBitIdentical, ref passed, ref failed, report);
            Run("연쇄 코일: 칸을 켜면 판이 실제로 달라진다", TestExtraRerollChangesBoard, ref passed, ref failed, report);

            // ── 상대 배치 패턴 Duo·Cross (2026-08-09, PLAN_BUILD_DEPENDENCY.md §C-2·C-7 1단) ──
            Run("Cross: 중심이 다른 저항체·바퀴 4칸이면 성립", TestCrossDetectsWheelAroundDifferentCenter, ref passed, ref failed, report);
            Run("Cross: 중심이 저항체가 아니면 성립하지 않음 (거짓 양성 방지)", TestCrossRequiresResistantCenter, ref passed, ref failed, report);
            Run("Cross 가 Cluster 와 동시 성립해도 중복 계상 없이 Cross 가 이김", TestCrossOutranksClusterWhenBothQualify, ref passed, ref failed, report);
            Run("Cross: 같은 판을 두 번 풀어도 같은 결과 (결정론)", TestCrossIsDeterministic, ref passed, ref failed, report);
            Run("Duo: 연결 쌍이 다른 저항체와 인접하면 성립", TestDuoDetectsPairAdjacentToOtherKind, ref passed, ref failed, report);
            Run("Duo: 다른 저항체가 없으면 성립하지 않음 (거짓 양성 방지)", TestDuoRequiresAdjacencyToOtherKind, ref passed, ref failed, report);
            Run("Duo 가 Line 과 동시 성립해도 중복 계상 없이 Duo 가 이김", TestDuoOutranksLineWhenBothQualify, ref passed, ref failed, report);
            Run("Cluster 가 Duo 와 동시 성립해도 중복 계상 없이 Cluster 가 이김", TestClusterOutranksDuoWhenBothQualify, ref passed, ref failed, report);
            Run("Duo: 실전 가중치 시드 스윕에서도 결정론 (공허한 통과 아님)", TestDuoIsDeterministicAcrossRandomSpins, ref passed, ref failed, report);

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

        /// <summary>
        /// 🔴 **검침원은 판을 바꾸지 않는다** (PD-30).
        ///
        /// 판에서 저항을 지우면 다음 스핀의 저항 밀도가 내려간다. 이 게임은 저항 밀도를
        /// **보상**하므로(PD-29 §원인 ② 실측: 영혼 가중치를 깎았더니 완주율이 올라갔다)
        /// 그건 완화가 아니라 약화다. 그래서 대가만 면제하고 개수는 그대로 보고한다.
        ///
        /// 이 검사가 없으면 「판에서도 지우자」가 언젠가 「더 자연스러운 구현」으로
        /// 들어오고, 그때 방향이 조용히 뒤집힌다.
        /// </summary>
        private static string TestResidualForgiveIsLedgerOnly()
        {
            SpinBoard board = Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul);

            SpinRuleSet plain = BoardRules();
            SpinResolution before = Resolve(board, plain);

            SpinRuleSet metered = BoardRules();
            metered.ResidualForgiveCount = 1;
            SpinResolution after = Resolve(board, metered);

            if (after.Residual.AbsorberCount != before.Residual.AbsorberCount)
                return $"판의 흡수체 개수가 달라졌다 {before.Residual.AbsorberCount} → " +
                       $"{after.Residual.AbsorberCount} — 검침원이 판을 건드렸다";
            if (after.Residual.ForgivenCount != 1)
                return $"면제 개수가 기록되지 않았다 ({after.Residual.ForgivenCount})";

            float expected = SpinRuleSet.ResidualLoadOf(1, metered.ResidualEscalation)
                           * metered.AbsorberResidualPowerLoss;
            if (Math.Abs(after.Residual.StoredPowerLoss - expected) > 0.0001f)
                return $"면제 뒤 차감 {after.Residual.StoredPowerLoss}, 기대 {expected}";
            if (after.Residual.StoredPowerLoss >= before.Residual.StoredPowerLoss)
                return "면제했는데 대가가 줄지 않았다";
            return null;
        }

        private static string TestResidualForgiveRollsBack()
        {
            SpinBoard board = Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Proliferator,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul);

            SpinRuleSet zero = BoardRules();
            zero.ResidualForgiveCount = 0;
            SpinResolution a = Resolve(board, zero);
            SpinResolution b = Resolve(board, BoardRules());

            if (Math.Abs(a.Residual.StoredPowerLoss - b.Residual.StoredPowerLoss) > 0.0001f ||
                Math.Abs(a.Residual.NextProliferatorWeightAdd - b.Residual.NextProliferatorWeightAdd) > 0.0001f ||
                a.Residual.ForgivenCount != 0)
                return "면제 0인데 옛 경로와 값이 갈렸다 — 롤백 경로가 깨졌다";
            return null;
        }

        /// <summary>
        /// 🔴 **0이면 `Random` 을 한 번도 건드리지 않는다 — 못 박은 값으로 확인한다.**
        ///
        /// 처음엔 「칸 0인 규칙 둘을 비교」로 썼다가 **변이 검사에서 잡혔다.** 두 쪽 다
        /// 같은 함수를 지나므로, 칸 0에서 난수를 한 번 뽑도록 망가뜨려도 **양쪽이 똑같이**
        /// 어긋나 검사가 통과했다. 함수 안의 결함은 그 함수를 지나는 두 값을 비교해서
        /// 잡을 수 없다.
        ///
        /// 그래서 바깥에 기준을 둔다. 아래 값은 연쇄 코일이 **없던 시점**의 결과다.
        /// 난수 소비 횟수가 달라지면 이 값이 깨진다 — 그리고 그때 실제로 깨지는 것은
        /// 이 품목이 아니라 **재현성**이다. 캡처 베이스라인·시드 재현·「로그 한 줄로 스핀
        /// 재현」이 전부 소비 횟수 위에 서 있다.
        ///
        /// ⚠ 이 값을 고쳐야 한다면 **의도한 변경일 때만** 고친다. 그리고 그때는
        /// `Captures/baseline.txt` 도 함께 새로 세운다.
        ///
        /// 규칙 다발을 이 검사 안에서 직접 만든다. `BoardRules()` 를 쓰면 밸런스 프로파일
        /// 변경이 이 못을 흔들어, 재현성 경보가 밸런스 경보와 섞인다.
        ///
        /// 🔴 **2026-08-09 — `NormalSoulValue` 를 이제 이 안에서 직접 못 박는다.** 이 검사가
        /// `BoardRules()`를 피한 것은 **프로파일** 변경을 막기 위해서였는데, `NormalSoulValue`는
        /// 프로파일이 아니라 `SpinRuleSet`의 C# 필드 기본값이라 그 방어를 그냥 통과했다 —
        /// Duo·Cross 패턴 작업에서 그 기본값을 14→11.5로 내리자 이 못이 실제로 흔들렸다.
        /// 값을 여기서 직접 지정해 두면 앞으로 그 필드 기본값이 다시 바뀌어도 이 검사는
        /// 흔들리지 않는다 — 이 검사 안에서 만드는 규칙 다발이 지켜야 했던 원래 의도를
        /// 마저 지킨 것뿐이다.
        ///
        /// 못값 자체도 갱신했다(4566.7999 → 5786.8999). **난수 소비가 달라져서가 아니다** —
        /// 재충전 여부(`refilled`)가 두 값에서 똑같이 6/40 이라는 것과, `NormalSoulValue`를
        /// 14로 고정한 채 Duo·Cross 판정만 제거하면 옛 값을 소수점까지 정확히 재현한다는
        /// 것을 별도 콘솔 스크래치로 확인했다(`docs/runtime/PATTERN_IMPL_NOTES.md` 참고).
        /// 차이(+1220.10)는 이 40시드 표본에서 Duo가 28번 새로 발동해 정화 전력이 늘어난
        /// 만큼이다 — Duo가 "예전엔 문턱 미달로 버려지던 2칸짜리 저항 쌍"을 정화하기
        /// 시작한 것이 정확히 이 패턴이 하려던 일이다. Cross는 이 40시드에서 한 번도
        /// 안 떴다(실측 발생률 0.02~0.04%, 같은 문서 §4).
        /// </summary>
        private static string TestExtraRerollZeroIsBitIdentical()
        {
            // 🔴 **시드 하나로는 못 잡는다.** 처음엔 시드 77 하나를 못 박았는데, 그 스핀은
            //    재충전이 걸리지 않아 `RerollExtraCells` 를 **아예 지나가지 않았다.**
            //    지나가지 않는 코드에 낸 결함은 그 못이 잡지 못한다 — 변이 검사가 그걸 잡았다.
            //    재충전이 걸리는 스핀이 섞이도록 시드를 넓게 훑어 합계를 못 박는다.
            double total = 0.0;
            int refilled = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                // NormalSoulValue를 여기서 직접 고정한다 — 위 2026-08-09 주석 참고.
                var rules = new SpinRuleSet
                    { MaxCascadeDepth = 2, ExtraCascadeRerollCells = 0, NormalSoulValue = 14f };
                rules.Weights[SymbolKind.NormalSoul]   = 5f;
                rules.Weights[SymbolKind.Absorber]     = 3f;
                rules.Weights[SymbolKind.Proliferator] = 2f;

                var engine = new SpinEngine(4242);
                SpinResolution r = engine.SpinWithSeed(seed, rules, ResistanceContract.None, ResidualState.Empty);
                total += r.GrossPower;
                if (r.Steps.Length > 1) refilled++;
            }

            // 재충전이 한 번도 안 걸리면 이 검사는 아무것도 지키지 못한다. 그 사실을 말한다.
            if (refilled == 0)
                return "시드 40개에서 재충전이 한 번도 안 걸렸다 — 이 못은 코일 경로를 지나지 않는다";

            const double Pinned = 5786.8999;
            if (Math.Abs(total - Pinned) > 0.001)
                return $"칸 0인데 옛 결과와 다르다 — 난수 소비가 바뀌었다\n" +
                       $"    실제 {total:F4} (재충전 {refilled}회)\n    못박은 값 {Pinned:F4}";
            return null;
        }

        private static string TestExtraRerollChangesBoard()
        {
            var engine = new SpinEngine(4242);
            SpinRuleSet off = BoardRules();
            SpinResolution baseline = engine.SpinWithSeed(77, off, ResistanceContract.None, ResidualState.Empty);

            // 여러 시드를 본다. 재충전이 안 걸리는 스핀에서는 이 품목이 아무 일도 안 하는 것이
            // **정상**이므로, 한 시드만 보고 「효과 없음」이라 판정하면 검사가 거짓말을 한다.
            for (int seed = 1; seed <= 40; seed++)
            {
                var e1 = new SpinEngine(4242);
                SpinRuleSet plain = BoardRules();
                SpinResolution a = e1.SpinWithSeed(seed, plain, ResistanceContract.None, ResidualState.Empty);

                var e2 = new SpinEngine(4242);
                SpinRuleSet coil = BoardRules();
                coil.ExtraCascadeRerollCells = 1;
                SpinResolution b = e2.SpinWithSeed(seed, coil, ResistanceContract.None, ResidualState.Empty);

                if (a.FinalBoard.ToString() != b.FinalBoard.ToString() ||
                    Math.Abs(a.GrossPower - b.GrossPower) > 0.0001f)
                    return null;   // 한 시드라도 갈리면 배선이 살아 있다
            }
            return "시드 40개에서 한 번도 판이 달라지지 않았다 — 연쇄 코일이 배선되지 않았다";
        }

        private static string TestAbsorberResidual()
        {
            SpinRuleSet rules = BoardRules();
            SpinResolution result = Resolve(Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul), rules);
            // 판정식을 여기서 다시 쓰지 않는다. 개수 → 대가 단위 변환은
            // `SpinRuleSet.ResidualLoadOf` 한 곳에만 있고, 이 검사는 그것을 부른다.
            // 직전 판본은 `2f * AbsorberResidualPowerLoss` 라고 **선형식을 복제**해서
            // 적었고, 그래서 잔류 대가의 모양이 바뀌자 게임이 아니라 검사가 깨졌다.
            // 값이 아니라 식이 두 벌이었던 것이 원인이다.
            float expectedLoss = SpinRuleSet.ResidualLoadOf(2, rules.ResidualEscalation)
                               * rules.AbsorberResidualPowerLoss;
            if (Math.Abs(result.Residual.StoredPowerLoss - expectedLoss) > 0.0001f)
                return $"차감 {result.Residual.StoredPowerLoss}, 기대 {expectedLoss}";
            if (Math.Abs(result.NetPower - (result.GrossPower - expectedLoss)) > 0.0001f)
                return $"NetPower {result.NetPower}, Gross {result.GrossPower}";

            // **모양 자체를 붙잡는다.** 값 하나만 보면 볼록도가 0으로 되돌아가도
            // (즉 「실을수록 싸진다」가 부활해도) 이 검사는 통과한다.
            // 남긴 개수가 늘 때 **개당 대가**가 함께 올라가야 한다.
            if (rules.ResidualEscalation > 0f)
            {
                float perUnitAt2 = SpinRuleSet.ResidualLoadOf(2, rules.ResidualEscalation) / 2f;
                float perUnitAt6 = SpinRuleSet.ResidualLoadOf(6, rules.ResidualEscalation) / 6f;
                if (perUnitAt6 <= perUnitAt2)
                    return $"잔류 대가가 볼록하지 않다 — 2개일 때 개당 {perUnitAt2:0.###}, "
                         + $"6개일 때 개당 {perUnitAt6:0.###}. 많이 남길수록 싸다";
            }
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

            // 흡수체 4개(0,1,3,4)가 직교로 붙어 Cluster. 증식체 2개(6,8)는 서로 붙어 있지
            // 않은 채 흩어져 있다 — 개수(3 미만)로도, 2026-08-09에 추가된 Duo(연결 2칸 +
            // 다른 종류 인접)로도 안 걸린다. 원래는 인덱스 2·5를 썼는데, 그 두 칸은 서로
            // 붙어 있어 Duo 추가 이후 이 자리에서 Cluster와 함께 Duo로도 정화됐다 —
            // 재충전 가중치가 증식체 100%라 정화 뒤 같은 심볼로 다시 채워져 아래 값
            // 비교는 우연히 통과했지만 "정화도 수확도 되지 않았다"는 이 테스트의 전제가
            // 깨져 있었다. 6·8은 서로 인접하지 않아(각각 고립 성분, 크기 1) Duo의
            // "연결 2칸 이상" 조건 자체가 성립하지 않으므로 진짜 생존 칸이다.
            SpinBoard before = Board(
                SymbolKind.Absorber,     SymbolKind.Absorber,     SymbolKind.NormalSoul,
                SymbolKind.Absorber,     SymbolKind.Absorber,     SymbolKind.NormalSoul,
                SymbolKind.Proliferator, SymbolKind.NormalSoul,   SymbolKind.Proliferator);
            SpinResolution result = Resolve(before, rules);

            if (result.Steps.Length < 2) return $"캐스케이드가 열리지 않음 (단계 {result.Steps.Length})";
            if (result.Steps[0].Purifies[0].Pattern != PatternKind.Cluster)
                return $"첫 패턴 {result.Steps[0].Purifies[0].Pattern}";
            if (result.Steps[0].Purifies.Length != 1)
                return $"1단계 발동 {result.Steps[0].Purifies.Length}건, 기대 1건 (증식체가 함께 터지면 안 된다)";

            SpinBoard after = result.Steps[0].BoardAfter;
            // 증식체 2칸(인덱스 6, 8)은 정화도 수확도 되지 않았으므로 그대로여야 한다.
            if (after[6] != SymbolKind.Proliferator || after[8] != SymbolKind.Proliferator)
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

        // ── 상대 배치 패턴 Duo·Cross (2026-08-09, PLAN_BUILD_DEPENDENCY.md §C-2·C-7 1단) ──
        //
        // 모든 배치는 손으로 유도하기 전에 임시 콘솔 스크래치(SpinEngine.FindMatches 와
        // 같은 우선순위 사슬을 옮겨 심은 것)로 먼저 돌려 확인했다 — 3×3 인접 기하는
        // 눈대중으로 틀리기 쉽다(모서리 칸이 중심의 대각 이웃일 뿐 아니라 "바퀴" 칸
        // 두 개와는 직교로 붙어 있다는 것이 이 검증에서 드러난 실수였다). 세부 근거는
        // `docs/runtime/PATTERN_IMPL_NOTES.md` 참조.

        /// <summary>
        /// 중심(인덱스4)이 증식체, 바퀴 4칸(1,3,5,7)이 흡수체, 모서리(0,2,6,8)는 관계없는
        /// 정상 영혼. 가장 단순한 Cross 성립 배치 — 중심이 배제되고 바퀴만 정화되는지까지 본다.
        /// </summary>
        private static string TestCrossDetectsWheelAroundDifferentCenter()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.NormalSoul, SymbolKind.Absorber,     SymbolKind.NormalSoul,
                SymbolKind.Absorber,   SymbolKind.Proliferator, SymbolKind.Absorber,
                SymbolKind.NormalSoul, SymbolKind.Absorber,     SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            if (result.Steps.Length == 0 || result.Steps[0].Purifies.Length == 0)
                return "정화 이벤트가 없다 — Cross가 성립하지 않았다";
            PurifyEvent purify = result.Steps[0].Purifies[0];
            if (purify.Kind != SymbolKind.Absorber) return $"발동 종류 {purify.Kind}, 기대 흡수체";
            if (purify.Pattern != PatternKind.Cross) return $"패턴 {purify.Pattern}, 기대 Cross";
            if (Math.Abs(purify.PatternMultiplier - rules.CrossMultiplier) > 0.0001f)
                return $"배수 {purify.PatternMultiplier}, 기대 {rules.CrossMultiplier}";
            if (purify.Cells == null || purify.Cells.Length != 4)
                return $"정화 칸 {purify.Cells?.Length ?? -1}, 기대 4 (바퀴만, 중심은 제외)";
            if (purify.PatternCells == null || purify.PatternCells.Length != 4)
                return $"모양 칸 {purify.PatternCells?.Length ?? -1}, 기대 4";

            var expected = new[] { 1, 3, 5, 7 };
            for (int i = 0; i < expected.Length; i++)
                if (purify.Cells[i] != expected[i])
                    return $"정화 칸 {string.Join(",", purify.Cells)}, 기대 {string.Join(",", expected)}";
            foreach (int cell in purify.Cells)
                if (cell == CrossCenterIndexForTests) return "중심 칸(증식체)이 정화 대상에 섞였다";
            return null;
        }

        /// <summary>
        /// 바퀴 4칸은 흡수체지만 중심이 저항체가 아니라 정상 영혼이면 Cross가 성립하지
        /// 않는다. 바퀴 칸들은 직교로는 중심을 거쳐야만 서로 닿으므로(모서리를 통하면
        /// 닿지만 모서리도 정상 영혼이다), 이 배치에서는 Cluster·Duo·Line·Scattered
        /// 무엇도 성립하지 않아야 한다 — Cross 가드뿐 아니라 "바퀴가 고립된다"는
        /// 전제 자체를 함께 확인한다(거짓 양성 방지).
        /// </summary>
        private static string TestCrossRequiresResistantCenter()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.NormalSoul, SymbolKind.Absorber,   SymbolKind.NormalSoul,
                SymbolKind.Absorber,   SymbolKind.NormalSoul, SymbolKind.Absorber,
                SymbolKind.NormalSoul, SymbolKind.Absorber,   SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            int purifies = 0;
            foreach (CascadeStep step in result.Steps)
                if (step.Purifies != null) purifies += step.Purifies.Length;
            if (purifies != 0)
                return $"중심이 저항체가 아닌데 정화가 {purifies}건 발생했다 (거짓 양성)";
            return null;
        }

        /// <summary>
        /// 모서리 두 칸(0,6)이 바퀴와 같은 흡수체라 직교로 다리를 놓아, 이 배치는
        /// Cross(바퀴 1,3,5,7)와 Cluster({0,1,3,6,7}, 5칸 연결)를 **동시에** 만족한다 —
        /// 대각 연결 스위치 없이도 충돌한다는 것이 스크래치 검증에서 나온 사실이다.
        /// Cross(5.0×)가 Cluster(3.0×)보다 배수가 커서 우선순위 사슬에서 이겨야 하고,
        /// 흡수체 발동은 정확히 한 건이어야 한다(중복 계상되면 두 건이 잡힌다).
        /// </summary>
        private static string TestCrossOutranksClusterWhenBothQualify()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.Absorber, SymbolKind.Absorber,     SymbolKind.NormalSoul,
                SymbolKind.Absorber, SymbolKind.Proliferator, SymbolKind.Absorber,
                SymbolKind.Absorber, SymbolKind.Absorber,     SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            if (result.Steps.Length == 0 || result.Steps[0].Purifies.Length == 0)
                return "정화 이벤트가 없다";
            int absorberEvents = 0;
            PurifyEvent onlyAbsorberEvent = default;
            foreach (PurifyEvent p in result.Steps[0].Purifies)
                if (p.Kind == SymbolKind.Absorber) { absorberEvents++; onlyAbsorberEvent = p; }
            if (absorberEvents != 1)
                return $"흡수체 발동 {absorberEvents}건, 기대 1건 (Cross·Cluster가 중복 계상됐다)";
            if (onlyAbsorberEvent.Pattern != PatternKind.Cross)
                return $"패턴 {onlyAbsorberEvent.Pattern}, 기대 Cross (Cluster에 우선순위를 뺏겼다)";
            if (onlyAbsorberEvent.Cells.Length != 4)
                return $"정화 칸 {onlyAbsorberEvent.Cells.Length}, 기대 4 (바퀴만, Cluster의 5칸이 아니다)";
            return null;
        }

        /// <summary>
        /// 같은 판·같은 규칙이면 Cross 판정도 결정론적이어야 한다. 실측 발생률이
        /// 0.02~0.04%(`PATTERN_IMPL_NOTES.md`)라 무작위 스윕으로는 좀처럼 안 걸리므로,
        /// 직접 만든 판을 두 번 풀어 비교하는 쪽이 훨씬 안정적이다.
        /// </summary>
        private static string TestCrossIsDeterministic()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.NormalSoul, SymbolKind.Absorber,     SymbolKind.NormalSoul,
                SymbolKind.Absorber,   SymbolKind.Proliferator, SymbolKind.Absorber,
                SymbolKind.NormalSoul, SymbolKind.Absorber,     SymbolKind.NormalSoul);

            SpinResolution a = Resolve(board, rules);
            SpinResolution b = Resolve(board, rules);
            if (!Equivalent(a, b)) return "같은 판·같은 규칙인데 Cross 결과가 갈렸다";
            if (a.Steps.Length == 0 || a.Steps[0].Purifies.Length == 0 ||
                a.Steps[0].Purifies[0].Pattern != PatternKind.Cross)
                return "전제가 깨졌다 — 이 보드는 Cross가 성립해야 한다";
            return null;
        }

        /// <summary>
        /// 흡수체 2개(인덱스0,1 — 연결)가 증식체(인덱스4)와 인접. 2칸은 Scattered 기본
        /// 최소(3)에도 못 미치므로, Duo가 없었다면 이 배치는 정화가 전혀 없어야 정상이다.
        /// </summary>
        private static string TestDuoDetectsPairAdjacentToOtherKind()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.Absorber,   SymbolKind.Absorber,     SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul,   SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            if (result.Steps.Length == 0 || result.Steps[0].Purifies.Length == 0)
                return "정화 이벤트가 없다 — Duo가 성립하지 않았다";
            PurifyEvent purify = result.Steps[0].Purifies[0];
            if (purify.Kind != SymbolKind.Absorber) return $"발동 종류 {purify.Kind}";
            if (purify.Pattern != PatternKind.Duo) return $"패턴 {purify.Pattern}, 기대 Duo";
            if (Math.Abs(purify.PatternMultiplier - rules.DuoMultiplier) > 0.0001f)
                return $"배수 {purify.PatternMultiplier}, 기대 {rules.DuoMultiplier}";
            if (purify.Cells == null || purify.Cells.Length != 2)
                return $"정화 칸 {purify.Cells?.Length ?? -1}, 기대 2 (증식체 칸은 포함되지 않는다)";
            if (purify.Cells[0] != 0 || purify.Cells[1] != 1)
                return $"정화 칸 {string.Join(",", purify.Cells)}, 기대 0,1";
            return null;
        }

        /// <summary>
        /// 흡수체 2개가 붙어 있지만 판 전체에 다른 저항체가 하나도 없다 — Duo의 "다른
        /// 종류와 인접" 조건이 실제로 걸러내는지를 본다. 거짓 양성이 거짓 음성보다
        /// 위험하다는 지시에 따라 이 방향의 검사를 반드시 남긴다.
        /// </summary>
        private static string TestDuoRequiresAdjacencyToOtherKind()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.Absorber,   SymbolKind.Absorber,   SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            int purifies = 0;
            foreach (CascadeStep step in result.Steps)
                if (step.Purifies != null) purifies += step.Purifies.Length;
            if (purifies != 0)
                return $"다른 저항체가 없는데 정화가 {purifies}건 발생했다 (거짓 양성)";
            return null;
        }

        /// <summary>
        /// row0(인덱스0,3,6)이 흡수체 직선이고, idx0이 증식체(idx1)와 인접 — Line과 Duo가
        /// 동시에 성립한다. Duo(2.5×)가 Line(2.0×)보다 배수가 커서 우선순위 사슬에서
        /// 이겨야 한다. "직선이어도 다른 종류 옆이면 더 쳐준다"가 사용자가 요청한 상대
        /// 배치 보상의 핵심이라 이 역전이 의도한 동작이다.
        /// </summary>
        private static string TestDuoOutranksLineWhenBothQualify()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.Absorber, SymbolKind.Proliferator, SymbolKind.NormalSoul,
                SymbolKind.Absorber, SymbolKind.NormalSoul,   SymbolKind.NormalSoul,
                SymbolKind.Absorber, SymbolKind.NormalSoul,   SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            if (result.Steps.Length == 0 || result.Steps[0].Purifies.Length == 0)
                return "정화 이벤트가 없다";
            int absorberEvents = 0;
            PurifyEvent onlyAbsorberEvent = default;
            foreach (PurifyEvent p in result.Steps[0].Purifies)
                if (p.Kind == SymbolKind.Absorber) { absorberEvents++; onlyAbsorberEvent = p; }
            if (absorberEvents != 1)
                return $"흡수체 발동 {absorberEvents}건, 기대 1건 (Line·Duo가 중복 계상됐다)";
            if (onlyAbsorberEvent.Pattern != PatternKind.Duo)
                return $"패턴 {onlyAbsorberEvent.Pattern}, 기대 Duo (Line에 우선순위를 뺏겼다)";
            if (onlyAbsorberEvent.Cells.Length != 3)
                return $"정화 칸 {onlyAbsorberEvent.Cells.Length}, 기대 3";
            return null;
        }

        /// <summary>
        /// 2×2 흡수체 덩어리(인덱스0,1,3,4)가 증식체(인덱스5, 인덱스4와 인접)와 닿아
        /// 있다 — Cluster와 Duo가 동시에 성립한다. Cluster(3.0×)가 Duo(2.5×)보다 커서
        /// 이겨야 하고, 흡수체 발동은 정확히 한 건이어야 한다.
        /// </summary>
        private static string TestClusterOutranksDuoWhenBothQualify()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.Absorber, SymbolKind.Absorber,     SymbolKind.NormalSoul,
                SymbolKind.Absorber, SymbolKind.Absorber,     SymbolKind.Proliferator,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            if (result.Steps.Length == 0 || result.Steps[0].Purifies.Length == 0)
                return "정화 이벤트가 없다";
            int absorberEvents = 0;
            PurifyEvent onlyAbsorberEvent = default;
            foreach (PurifyEvent p in result.Steps[0].Purifies)
                if (p.Kind == SymbolKind.Absorber) { absorberEvents++; onlyAbsorberEvent = p; }
            if (absorberEvents != 1)
                return $"흡수체 발동 {absorberEvents}건, 기대 1건 (Cluster·Duo가 중복 계상됐다)";
            if (onlyAbsorberEvent.Pattern != PatternKind.Cluster)
                return $"패턴 {onlyAbsorberEvent.Pattern}, 기대 Cluster (Duo에 우선순위를 뺏겼다)";
            if (onlyAbsorberEvent.Cells.Length != 4)
                return $"정화 칸 {onlyAbsorberEvent.Cells.Length}, 기대 4";
            return null;
        }

        /// <summary>
        /// Duo는 실측 발생률이 약 17.8%(`PATTERN_IMPL_NOTES.md`)라 무작위 스핀 표본에서도
        /// 안정적으로 나온다. 실제 커리큘럼 가중치로 4개 시드 × 500스핀을 두 엔진에 각각
        /// 돌려 매 스핀 결과가 일치하는지 보고, Duo가 최소 한 번은 나왔는지도 확인한다 —
        /// 손으로 만든 판 하나가 아니라 실전 분포로도 결정론이 흔들리지 않는지가 목적이다.
        /// `TestPurifiedCellsAreContiguous`와 같은 시드·스핀 수를 써서 기존 스윕과
        /// 같은 신뢰도로 맞췄다.
        /// </summary>
        private static string TestDuoIsDeterministicAcrossRandomSpins()
        {
            int duoSeen = 0;
            foreach (int seed in new[] { 1337, 4242, 271828, 8675309 })
            {
                FloorPlan plan = PrototypeCurriculum.For(8);
                SpinRuleSet rules = PrototypeCurriculum.BuildRules(in plan);
                ResistanceContract none = ResistanceContract.None;
                ResidualState residual = ResidualState.Empty;

                var engineA = new SpinEngine(seed);
                var engineB = new SpinEngine(seed);

                for (int i = 0; i < 500; i++)
                {
                    int spinSeed = SpinSeed.Derive(seed, 8, i);
                    SpinResolution a = engineA.SpinWithSeed(spinSeed, rules, in none, in residual, 8, i);
                    SpinResolution b = engineB.SpinWithSeed(spinSeed, rules, in none, in residual, 8, i);
                    if (!Equivalent(a, b))
                        return $"시드 {seed} 스핀 {i}: 같은 스핀 시드인데 결과가 다르다";

                    if (a.Steps == null) continue;
                    foreach (CascadeStep step in a.Steps)
                    {
                        if (step.Purifies == null) continue;
                        foreach (PurifyEvent p in step.Purifies)
                            if (p.Pattern == PatternKind.Duo) duoSeen++;
                    }
                }
            }
            if (duoSeen == 0) return "2000스핀을 돌았는데 Duo가 한 번도 안 나왔다 — 검사가 공허하게 통과했다";
            return null;
        }

        /// <summary>Cross 판정 중심 칸 인덱스. SpinEngine 내부 상수(CrossCenterIndex)와 값이 같아야
        /// 의미가 있지만, private const라 테스트에서 직접 참조할 수 없어 값만 복제해 둔다.</summary>
        private const int CrossCenterIndexForTests = 4;

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
