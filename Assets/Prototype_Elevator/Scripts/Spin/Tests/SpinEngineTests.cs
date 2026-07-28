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
            Run("같은 저항 3개 직선 3종 → LineKind", TestLines, ref passed, ref failed, report);
            Run("직교 연결 4개 → Cluster와 재충전", TestClusterRefill, ref passed, ref failed, report);
            Run("대각 연결 규칙", TestDiagonalConnectivity, ref passed, ref failed, report);
            Run("저항 2개 → 정화 없음", TestBelowThreshold, ref passed, ref failed, report);
            Run("9칸 동일 저항 → FullBoard", TestFullBoard, ref passed, ref failed, report);
            Run("같은 시드 → 완전 동일", TestDeterminism, ref passed, ref failed, report);
            Run("흡수체 잔류 → NetPower 차감", TestAbsorberResidual, ref passed, ref failed, report);
            Run("MaxCascadeDepth 상한", TestMaxCascadeDepth, ref passed, ref failed, report);
            Run("MaxCascadeDepth 마지막 정상 영혼 수확", TestMaxDepthHarvest, ref passed, ref failed, report);

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

        private static string TestScattered()
        {
            SpinRuleSet rules = BoardRules();
            SpinBoard board = Board(
                SymbolKind.Absorber, SymbolKind.NormalSoul, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.NormalSoul);
            SpinResolution result = Resolve(board, rules);

            if (result.Steps.Length != 1) return $"단계 수 {result.Steps.Length}";
            PurifyEvent eventData = result.Steps[0].Purifies[0];
            if (eventData.Pattern != PatternKind.Scattered) return $"패턴 {eventData.Pattern}";
            if (eventData.PatternMultiplier != 1f) return $"배수 {eventData.PatternMultiplier}";
            if (eventData.Cells.Length != 3) return $"정화 칸 수 {eventData.Cells.Length}";
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
            if (result.NormalSoulPower != 140f)
                return $"정상 영혼 전력 {result.NormalSoulPower}, 기대 140";
            if (result.FinalBoard.CountOf(SymbolKind.NormalSoul) != 0)
                return "최대 깊이 종료 후 정상 영혼이 남음";
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
            if (a.Seed != b.Seed || !a.InitialBoard.Equals(b.InitialBoard) || !a.FinalBoard.Equals(b.FinalBoard))
                return false;
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
