using System;
using System.Collections.Generic;

namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 자동 3×3 스핀의 추첨·판정·캐스케이드 엔진.
    ///
    /// 이 타입은 Unity 런타임에 묶이지 않도록 순수 C#으로 유지한다. 호출자는 계약을
    /// 규칙 다발에 먼저 적용해 넘기며, 엔진은 전달받은 규칙을 절대 변경하지 않는다.
    /// </summary>
    public sealed class SpinEngine
    {
        private readonly int[] _neighbourBuffer = new int[8];
        private readonly HashSet<int> _issuedSpinSeeds = new HashSet<int>();
        private Random _random;
        private int _seed;

        public SpinEngine(int seed)
        {
            Reseed(seed);
        }

        /// <summary>
        /// 이 엔진이 시작한 런 시드. 호출자가 <see cref="SpinSeed.Derive"/>로 층·스핀
        /// 좌표를 붙일 때 필요하다. 엔진이 시드를 숨기고 있으면 파생 규칙을 단일 출처로
        /// 둘 수 없어 각 호출부가 자기 시드를 따로 들고 다니게 된다.
        /// </summary>
        public int RunSeed => _seed;

        public void Reseed(int seed)
        {
            _seed = seed;
            _random = new Random(seed);
            _issuedSpinSeeds.Clear();
        }

        /// <summary>
        /// 레버 1회. carriedResidual은 직전 스핀에서 넘어온 잔류 상태다.
        /// </summary>
        public SpinResolution Spin(SpinRuleSet rules,
                                    in ResistanceContract contract,
                                    in ResidualState carriedResidual)
        {
            int spinSeed = NextSpinSeed();
            return SpinWithSeed(spinSeed, rules, contract, carriedResidual);
        }

        /// <summary>
        /// 기록된 SpinResolution.Seed로 단일 스핀을 재현한다. 일반 게임 흐름은 Spin을
        /// 호출하고, 로그 재현·헤드리스 디버깅은 이 메서드에 해당 시드만 넣어 호출한다.
        /// </summary>
        public SpinResolution SpinWithSeed(int spinSeed,
                                            SpinRuleSet rules,
                                            in ResistanceContract contract,
                                            in ResidualState carriedResidual)
            => SpinWithSeed(spinSeed, rules, in contract, in carriedResidual, 0, 0);

        /// <summary>
        /// 층·스핀 좌표까지 기록하는 스핀. 좌표는 판정에 영향을 주지 않고
        /// <see cref="SpinResolution"/>에 그대로 실려 나간다 — 로그 한 줄만 보고
        /// 같은 스핀을 다시 만들 수 있어야 하기 때문이다.
        /// </summary>
        public SpinResolution SpinWithSeed(int spinSeed,
                                            SpinRuleSet rules,
                                            in ResistanceContract contract,
                                            in ResidualState carriedResidual,
                                            int floor,
                                            int spinIndex)
        {
            SpinRuleSet effectiveRules = PrepareRules(rules, carriedResidual);
            var spinRandom = new Random(spinSeed);
            SpinBoard initial = DrawBoard(effectiveRules, false, spinRandom);
            ApplyGuaranteedNormalSouls(ref initial, effectiveRules.GuaranteedNormalSouls);
            SpinResolution resolution =
                ResolveBoardInternal(effectiveRules, contract, spinRandom, spinSeed, initial);
            resolution.RunSeed = _seed;
            resolution.Floor = floor;
            resolution.SpinIndex = spinIndex;
            return resolution;
        }

        /// <summary>
        /// 추첨을 건너뛰고 주어진 보드를 판정한다. 테스트·헤드리스 시뮬레이터에서
        /// 패턴과 캐스케이드 규칙만 독립적으로 검증할 수 있도록 공개한다.
        /// </summary>
        public SpinResolution ResolveBoard(SpinRuleSet rules,
                                            in ResistanceContract contract,
                                            in ResidualState carriedResidual,
                                            in SpinBoard board)
        {
            int spinSeed = NextSpinSeed();
            SpinRuleSet effectiveRules = PrepareRules(rules, carriedResidual);
            var spinRandom = new Random(spinSeed);
            return ResolveBoardInternal(effectiveRules, contract, spinRandom, spinSeed, board);
        }

        private int NextSpinSeed()
        {
            int spinSeed;
            do
            {
                spinSeed = _random.Next();
            }
            while (!_issuedSpinSeeds.Add(spinSeed));
            return spinSeed;
        }

        private SpinRuleSet PrepareRules(SpinRuleSet rules, in ResidualState carriedResidual)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            SpinRuleSet copy = rules.Clone();
            if (carriedResidual.NextProliferatorWeightAdd != 0f)
            {
                copy.Weights[SymbolKind.Proliferator] =
                    Math.Max(0f, copy.WeightOf(SymbolKind.Proliferator) +
                              carriedResidual.NextProliferatorWeightAdd);
            }
            return copy;
        }

        private static SpinBoard DrawBoard(SpinRuleSet rules, bool refill, Random spinRandom)
        {
            var board = default(SpinBoard);
            for (int column = 0; column < SpinBoard.Columns; column++)
            {
                for (int row = 0; row < SpinBoard.Rows; row++)
                {
                    board[column, row] = DrawSymbol(rules, refill, spinRandom);
                }
            }
            return board;
        }

        private static void FillEmptyCells(ref SpinBoard board, SpinRuleSet rules, Random spinRandom)
        {
            // 열 0의 위쪽 칸부터 열 2의 아래쪽 칸까지 채운다. 최초 추첨과 동일한
            // 순서를 유지해야 시드 재현과 UI 로그가 어긋나지 않는다.
            for (int index = 0; index < SpinBoard.Cells; index++)
            {
                if (board[index] == SymbolKind.Empty)
                    board[index] = DrawSymbol(rules, true, spinRandom);
            }
        }

        private static SymbolKind DrawSymbol(SpinRuleSet rules, bool refill, Random spinRandom)
        {
            float normalWeight = rules.WeightOf(SymbolKind.NormalSoul);
            float absorberWeight = rules.WeightOf(SymbolKind.Absorber);
            float proliferatorWeight = rules.WeightOf(SymbolKind.Proliferator);

            if (refill && rules.RefillNormalSoulBias > 0f)
            {
                ApplyNormalSoulBias(ref normalWeight, ref absorberWeight,
                                    ref proliferatorWeight, rules.RefillNormalSoulBias);
            }

            float total = normalWeight + absorberWeight + proliferatorWeight;
            if (total <= 0f)
            {
                throw new InvalidOperationException("SpinRuleSet must contain at least one positive symbol weight.");
            }

            double roll = spinRandom.NextDouble() * total;
            if (roll < normalWeight) return SymbolKind.NormalSoul;
            roll -= normalWeight;
            if (roll < absorberWeight) return SymbolKind.Absorber;
            return SymbolKind.Proliferator;
        }

        private static void ApplyNormalSoulBias(ref float normalWeight,
                                                ref float absorberWeight,
                                                ref float proliferatorWeight,
                                                float bias)
        {
            float total = normalWeight + absorberWeight + proliferatorWeight;
            if (total <= 0f) return;

            float oldNormalProbability = normalWeight / total;
            float requestedProbability = Math.Min(1f, oldNormalProbability + Math.Max(0f, bias));
            float resistanceWeight = absorberWeight + proliferatorWeight;

            if (requestedProbability >= 1f || resistanceWeight <= 0f)
            {
                normalWeight = total;
                absorberWeight = 0f;
                proliferatorWeight = 0f;
                return;
            }

            // 저항체 둘의 상대 비율은 유지하고 정상 영혼 확률만 가산한다.
            normalWeight = resistanceWeight * requestedProbability / (1f - requestedProbability);
        }

        private static void ApplyGuaranteedNormalSouls(ref SpinBoard board, int guaranteed)
        {
            if (guaranteed <= 0) return;

            int normals = board.CountOf(SymbolKind.NormalSoul);
            for (int index = SpinBoard.Cells - 1;
                 index >= 0 && normals < guaranteed;
                 index--)
            {
                if (!board[index].IsResistance()) continue;
                board[index] = SymbolKind.NormalSoul;
                normals++;
            }
        }

        private SpinResolution ResolveBoardInternal(SpinRuleSet rules,
                                                     in ResistanceContract contract,
                                                     Random spinRandom,
                                                     int spinSeed,
                                                     SpinBoard initialBoard)
        {
            var steps = new List<CascadeStep>();
            SpinBoard board = initialBoard;
            float normalSoulPower = 0f;
            float purifyPower = 0f;
            int maxDepth = Math.Max(1, rules.MaxCascadeDepth);
            bool capReached = false;

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                SpinBoard before = board;
                List<PatternMatch> matches = FindMatches(before, rules);

                int normalSoulCount = before.CountOf(SymbolKind.NormalSoul);
                if (matches.Count == 0)
                {
                    float terminalNormalPower = normalSoulCount * rules.NormalSoulValue *
                                                 (1f + (depth - 1) * rules.CascadeMultiplierStep);
                    SpinBoard terminalAfter = before;
                    for (int index = 0; index < SpinBoard.Cells; index++)
                        if (terminalAfter[index] == SymbolKind.NormalSoul)
                            terminalAfter[index] = SymbolKind.Empty;

                    steps.Add(new CascadeStep
                    {
                        Depth = depth,
                        BoardBefore = before,
                        BoardAfter = terminalAfter,
                        NormalSoulsHarvested = normalSoulCount,
                        NormalSoulPower = terminalNormalPower,
                        Purifies = Array.Empty<PurifyEvent>(),
                        ChainMultiplier = 1f + (depth - 1) * rules.CascadeMultiplierStep,
                        StepPower = terminalNormalPower,
                    });
                    normalSoulPower += terminalNormalPower;
                    board = terminalAfter;
                    break;
                }

                float chainMultiplier = 1f + (depth - 1) * rules.CascadeMultiplierStep;
                float stepNormalPower = normalSoulCount * rules.NormalSoulValue * chainMultiplier;
                SpinBoard after = before;

                // 정상 영혼은 이 단계에서 수확되므로 같은 영혼을 다음 캐스케이드에서
                // 다시 세지 않는다. 정화된 저항체와 함께 빈칸이 되어, 열린 캐스케이드에서는
                // 모두 재추첨된다.
                for (int index = 0; index < SpinBoard.Cells; index++)
                    if (after[index] == SymbolKind.NormalSoul)
                        after[index] = SymbolKind.Empty;

                var events = new List<PurifyEvent>(matches.Count);
                float stepPurifyPower = 0f;
                bool triggersRefill = false;
                foreach (PatternMatch match in matches)
                {
                    int[] cells = CellsOf(before, match.Kind);
                    float patternMultiplier = rules.PatternMultiplierFor(match.Pattern, match.Kind);
                    float eventPower = cells.Length * rules.PurifyValuePerSymbol *
                                       rules.PurifyRewardFor(match.Kind) * patternMultiplier * chainMultiplier;

                    events.Add(new PurifyEvent
                    {
                        Kind = match.Kind,
                        Pattern = match.Pattern,
                        Cells = cells,
                        PatternCells = match.PatternCells,
                        Line = match.LineKind,
                        Power = eventPower,
                        PatternMultiplier = patternMultiplier,
                    });
                    stepPurifyPower += eventPower;
                    if (match.Pattern.TriggersRefill()) triggersRefill = true;

                    for (int i = 0; i < cells.Length; i++)
                        after[cells[i]] = SymbolKind.Empty;
                }

                if (triggersRefill)
                    FillEmptyCells(ref after, rules, spinRandom);

                int harvestedNormals = normalSoulCount;
                float totalStepNormalPower = stepNormalPower;
                if (triggersRefill && depth == maxDepth)
                {
                    // 여기가 하드 캡이다. 판이 아직 더 무너질 수 있는데 규칙이 끊은 것이므로
                    // 자연 종료와 구분해 기록한다(MASTER_PRD §6 "명확한 로그를 남긴 뒤 정상 종료").
                    capReached = true;

                    // 최대 깊이에서 재충전된 보드는 다음 판정을 수행하지 않으므로,
                    // 이 단계의 연쇄 배수로 정상 영혼을 즉시 수확하고 빈칸으로 만든다.
                    int terminalNormalCount = after.CountOf(SymbolKind.NormalSoul);
                    harvestedNormals += terminalNormalCount;
                    totalStepNormalPower += terminalNormalCount * rules.NormalSoulValue * chainMultiplier;
                    for (int index = 0; index < SpinBoard.Cells; index++)
                        if (after[index] == SymbolKind.NormalSoul)
                            after[index] = SymbolKind.Empty;
                }

                steps.Add(new CascadeStep
                {
                    Depth = depth,
                    BoardBefore = before,
                    BoardAfter = after,
                    NormalSoulsHarvested = harvestedNormals,
                    NormalSoulPower = totalStepNormalPower,
                    Purifies = events.ToArray(),
                    ChainMultiplier = chainMultiplier,
                    StepPower = totalStepNormalPower + stepPurifyPower,
                });

                normalSoulPower += totalStepNormalPower;
                purifyPower += stepPurifyPower;
                board = after;

                if (!triggersRefill)
                    break;
            }

            ResidualState residual = BuildResidual(board, rules);
            float grossPower = normalSoulPower + purifyPower;

            var resolution = new SpinResolution
            {
                Seed = spinSeed,
                RunSeed = _seed,
                InitialBoard = initialBoard,
                FinalBoard = board,
                Steps = steps.ToArray(),
                NormalSoulPower = normalSoulPower,
                PurifyPower = purifyPower,
                GrossPower = grossPower,
                Residual = residual,
                NetPower = grossPower - residual.StoredPowerLoss,
                Contract = contract,
                CascadeCapReached = capReached,
            };

            if (capReached) CascadeCapReached?.Invoke(resolution);
            return resolution;
        }

        /// <summary>
        /// 하드 캡으로 캐스케이드가 끊겼을 때 발생한다. 엔진은 순수 C#이라 `Debug.Log`를
        /// 직접 부르지 않는다 — 로그 채널은 호출자(Unity 어댑터·테스트)가 붙인다.
        /// 구독자가 없어도 캡 자체는 <see cref="SpinResolution.CascadeCapReached"/>에 남는다.
        /// </summary>
        public event Action<SpinResolution> CascadeCapReached;

        private ResidualState BuildResidual(SpinBoard board, SpinRuleSet rules)
        {
            int absorberCount = board.CountOf(SymbolKind.Absorber);
            int proliferatorCount = board.CountOf(SymbolKind.Proliferator);
            float storedPowerLoss = absorberCount * rules.AbsorberResidualPowerLoss *
                                    rules.ResidualPenaltyFor(SymbolKind.Absorber);
            float nextWeightAdd = proliferatorCount * rules.ProliferatorResidualWeightAdd *
                                  rules.ResidualPenaltyFor(SymbolKind.Proliferator);

            return new ResidualState
            {
                AbsorberCount = absorberCount,
                ProliferatorCount = proliferatorCount,
                StoredPowerLoss = storedPowerLoss,
                NextProliferatorWeightAdd = nextWeightAdd,
            };
        }

        private List<PatternMatch> FindMatches(SpinBoard board, SpinRuleSet rules)
        {
            var matches = new List<PatternMatch>(SymbolKinds.ResistanceKinds.Length);
            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds)
            {
                if (board.CountOf(kind) < rules.MinimumCountFor(kind)) continue;

                bool fullBoard = board.CountOf(kind) == SpinBoard.Cells;
                int[] clusterCells = FindConnectedComponent(board, kind, rules.DiagonalCountsAsConnected);
                bool cluster = clusterCells != null;
                LineKind lineKind;
                int[] lineCells = TryFindLine(board, kind, out lineKind);
                bool line = lineCells != null;

                // 패턴을 이룬 칸을 그대로 실어 보낸다. 화면이 "직선이라서 터졌다"를
                // 형태로 그리려면 어느 줄이었는지가 필요하고, 그건 판정만 알고 있다.
                int[] fullBoardCells = fullBoard ? CellsOf(board, kind) : null;

                if (!rules.AllowMultiplePatternsPerKind)
                {
                    if (fullBoard)
                        matches.Add(new PatternMatch(kind, PatternKind.FullBoard, lineKind, fullBoardCells));
                    else if (cluster)
                        matches.Add(new PatternMatch(kind, PatternKind.Cluster, lineKind, clusterCells));
                    else if (line)
                        matches.Add(new PatternMatch(kind, PatternKind.Line, lineKind, lineCells));
                    else
                        matches.Add(new PatternMatch(kind, PatternKind.Scattered, lineKind, null));
                    continue;
                }

                // 업그레이드로 중복 패턴이 열린 경우에도 종류별 개수의 정화는 한 번이며,
                // 각 성립 패턴을 별도 이벤트로 남겨 로그와 보상에 모두 반영한다.
                if (fullBoard)
                    matches.Add(new PatternMatch(kind, PatternKind.FullBoard, lineKind, fullBoardCells));
                if (cluster)
                    matches.Add(new PatternMatch(kind, PatternKind.Cluster, lineKind, clusterCells));
                if (line)
                    matches.Add(new PatternMatch(kind, PatternKind.Line, lineKind, lineCells));
                matches.Add(new PatternMatch(kind, PatternKind.Scattered, lineKind, null));
            }
            return matches;
        }

        /// <summary>
        /// 4칸 이상 직교(또는 대각) 연결 덩어리를 찾아 **그 칸들을** 돌려준다. 없으면 null.
        /// 여러 덩어리가 성립하면 가장 큰 것을 고른다 — 화면이 하나만 감쌀 수 있고,
        /// 가장 큰 덩어리가 곧 이 판이 무너진 이유이기 때문이다.
        /// </summary>
        private int[] FindConnectedComponent(SpinBoard board, SymbolKind kind, bool diagonal)
        {
            var visited = new bool[SpinBoard.Cells];
            var queue = new int[SpinBoard.Cells];
            int[] best = null;

            for (int start = 0; start < SpinBoard.Cells; start++)
            {
                if (visited[start] || board[start] != kind) continue;

                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                visited[start] = true;

                while (head < tail)
                {
                    int index = queue[head++];
                    int neighbourCount = diagonal
                        ? SpinBoard.AllNeighbours(index, _neighbourBuffer)
                        : SpinBoard.OrthogonalNeighbours(index, _neighbourBuffer);
                    for (int n = 0; n < neighbourCount; n++)
                    {
                        int neighbour = _neighbourBuffer[n];
                        if (visited[neighbour] || board[neighbour] != kind) continue;
                        visited[neighbour] = true;
                        queue[tail++] = neighbour;
                    }
                }

                if (tail < 4 || (best != null && tail <= best.Length)) continue;

                var component = new int[tail];
                Array.Copy(queue, component, tail);
                Array.Sort(component);   // 인덱스 오름차순 — 로그·테스트가 순서에 흔들리지 않게
                best = component;
            }
            return best;
        }

        /// <summary>
        /// 3칸 직선을 찾아 **그 줄의 칸들을** 돌려준다. 없으면 null.
        /// 여러 줄이 성립하면 <see cref="SpinBoard.Lines"/> 순서상 첫 줄을 쓴다(결정론).
        /// </summary>
        private static int[] TryFindLine(SpinBoard board, SymbolKind kind, out LineKind lineKind)
        {
            for (int i = 0; i < SpinBoard.Lines.Length; i++)
            {
                int[] line = SpinBoard.Lines[i];
                if (board[line[0]] != kind || board[line[1]] != kind || board[line[2]] != kind)
                    continue;
                lineKind = SpinBoard.LineKinds[i];
                // 원본 배열을 그대로 내보내면 소비자가 정렬·수정해 SpinBoard.Lines 를 오염시킨다.
                return new[] { line[0], line[1], line[2] };
            }

            lineKind = LineKind.Column;
            return null;
        }

        private static int[] CellsOf(SpinBoard board, SymbolKind kind)
        {
            var cells = new List<int>(SpinBoard.Cells);
            for (int index = 0; index < SpinBoard.Cells; index++)
                if (board[index] == kind) cells.Add(index);
            return cells.ToArray();
        }

        private readonly struct PatternMatch
        {
            public readonly SymbolKind Kind;
            public readonly PatternKind Pattern;
            public readonly LineKind LineKind;

            /// <summary>패턴을 이룬 칸. 개수 정화는 모양이 없으므로 null.</summary>
            public readonly int[] PatternCells;

            public PatternMatch(SymbolKind kind, PatternKind pattern, LineKind lineKind, int[] patternCells)
            {
                Kind = kind;
                Pattern = pattern;
                LineKind = lineKind;
                PatternCells = patternCells;
            }
        }
    }
}
