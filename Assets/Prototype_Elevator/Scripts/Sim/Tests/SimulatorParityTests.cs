using System;
using System.Text;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Sim.Tests
{
    /// <summary>
    /// 밸런스 시뮬레이터가 **게임과 같은 규칙으로 도는가**를 지킨다.
    ///
    /// 왜 이 스위트가 필요한가: 이 파일이 생기기 전까지 `Sim/RunSimulator` 는
    ///   ① 시드를 순차 스트림(`SpinEngine.Spin`)으로 뽑았고 — `Spin/SpinSeed` 의 클래스
    ///      주석이 **쓰지 말라고 명시적으로 적어 둔** 경로이며, 그 경고를 어긴 유일한
    ///      호출자가 이 시뮬레이터였다,
    ///   ② 판돈을 `0.12f * (1f + 0.35f * n)` 으로 **직접 써 두고** 있었다.
    /// 두 값이 우연히 프로파일 기본값과 같아서 **숫자로는 티가 나지 않았다.** 그래서
    /// 같은 시드를 넣어도 게임과 다른 판이 나왔고, 그 판에서 잰 완주율이 문서에
    /// 「측정된 사실」로 실렸다.
    ///
    /// 여기 있는 단정들은 그 상태로 **되돌아가면 즉시 빨개진다.** 값이 아니라
    /// 관계를 본다 — 상수를 박아 두면 밸런스를 고칠 때마다 테스트를 같이 고치게 되고,
    /// 그러면 테스트가 옛 버그를 고정하는 쪽으로 자란다.
    /// </summary>
    public static class SimulatorParityTests
    {
        /// <summary>
        /// 표본 시드. 하나로는 「우연히 통과」를 못 가른다. 잔류 이월 검사는
        /// 6층 이후(증식체가 처음 나오는 층)까지 살아남은 런이 필요해 더 넓게 훑는다.
        /// </summary>
        private static readonly int[] Seeds = { 1337, 4242, 90210, 20250805, 7 };

        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("스핀 시드가 (런 시드, 층, 스핀) 좌표에서 나온다",
                TestSeedIsCoordinateDerived, ref passed, ref failed, report);
            Run("스핀이 자기 좌표를 기록한다 (로그 한 줄 재현)",
                TestSpinStampsItsCoordinate, ref passed, ref failed, report);
            Run("게임(FloorSession)과 시뮬이 같은 시드로 같은 판·같은 전력",
                TestFloorSessionParity, ref passed, ref failed, report);
            Run("판돈이 프로파일 값에서 온다 (비율을 2배로 하면 판돈도 2배)",
                TestAnteComesFromProfile, ref passed, ref failed, report);
            Run("추가 스핀 상한 0이면 과수확이 일어나지 않는다",
                TestExtraSpinCapStopsOverharvest, ref passed, ref failed, report);
            Run("해금 임계를 올리면 과수확이 잠긴다",
                TestUnlockThresholdLocksOverharvest, ref passed, ref failed, report);
            Run("잔류 저항이 층을 건너 이월된다",
                TestResidualCarriesAcrossFloors, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Simulator Parity Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        private static void Run(string name, Func<string> test,
                                ref int passed, ref int failed, StringBuilder report)
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
                report.AppendLine($"  FAIL  {name} — 예외: {exception.GetType().Name}: {exception.Message}");
            }
        }

        // ────────────────────────────────────────────────────────────── 시드 파생

        /// <summary>
        /// 기록된 모든 스핀의 시드가 <see cref="SpinSeed.Derive"/> 와 정확히 같아야 한다.
        ///
        /// 순차 스트림으로 되돌리면 첫 스핀부터 어긋난다 — 즉 이 단정 하나가
        /// 「시뮬이 게임과 같은 판을 돌린다」의 필요조건 전부를 붙잡는다.
        /// </summary>
        private static string TestSeedIsCoordinateDerived()
        {
            int checkedSpins = 0;
            foreach (int seed in Seeds)
            {
                var sim = new RunSimulator(seed);
                SimRunRecord record = sim.RunOnce(seed, SimPolicy.Greedy(), 0);
                foreach (SimFloorRecord floor in record.floors)
                {
                    for (int i = 0; i < floor.spins.Count; i++)
                    {
                        int expected = SpinSeed.Derive(seed, floor.floor, i);
                        int actual = floor.spins[i].resolution.Seed;
                        if (expected != actual)
                            return $"시드 {seed} {floor.floor}층 {i}번째 스핀: " +
                                   $"기대 {expected}, 실제 {actual} — 순차 스트림으로 돌아갔다";
                        checkedSpins++;
                    }
                }
            }
            return checkedSpins > 0 ? null : "검사한 스핀이 0개다 — 이 단정이 아무것도 지키지 않는다";
        }

        /// <summary>
        /// 로그 한 줄만 보고 같은 스핀을 다시 만들 수 있어야 한다(`TECH_SPEC` §11).
        /// 좌표가 기록되지 않으면 시드가 맞아도 그 줄이 어느 층 어느 스핀인지 알 수 없다.
        /// </summary>
        private static string TestSpinStampsItsCoordinate()
        {
            foreach (int seed in Seeds)
            {
                var sim = new RunSimulator(seed);
                SimRunRecord record = sim.RunOnce(seed, SimPolicy.Greedy(), 0);
                foreach (SimFloorRecord floor in record.floors)
                {
                    for (int i = 0; i < floor.spins.Count; i++)
                    {
                        SpinResolution r = floor.spins[i].resolution;
                        if (r.RunSeed != seed)
                            return $"시드 {seed} {floor.floor}층 {i}: RunSeed 가 {r.RunSeed}";
                        if (r.Floor != floor.floor)
                            return $"시드 {seed} {floor.floor}층 {i}: Floor 가 {r.Floor}";
                        if (r.SpinIndex != i)
                            return $"시드 {seed} {floor.floor}층 {i}: SpinIndex 가 {r.SpinIndex}";
                    }
                }
            }
            return null;
        }

        // ────────────────────────────────────────────────────── 게임과의 판 일치

        /// <summary>
        /// **가장 중요한 단정.** 게임의 층 하나(`FloorSession`)와 시뮬의 같은 층을
        /// 같은 시드로 나란히 돌려 판·전력이 한 자리도 다르지 않아야 한다.
        ///
        /// 1층을 쓰는 이유: 계약 단계가 없어 정책의 선택이 끼어들지 않는다. 여기서
        /// 갈리면 원인이 시드 파생 아니면 판돈 둘 중 하나로 좁혀진다.
        /// 무게 0 이라 `WeightSnapshot.RequiredPowerFor` 가 층 계획의 요구 전력을 그대로 낸다 —
        /// 즉 시뮬이 적재를 모른다는 사실이 이 검사를 오염시키지 않는다.
        /// </summary>
        private static string TestFloorSessionParity()
        {
            foreach (int seed in Seeds)
            {
                var sim = new RunSimulator(seed);
                SimRunRecord record = sim.RunOnce(seed, SimPolicy.Greedy(), 0);
                if (record.floors.Count == 0) return $"시드 {seed}: 층 기록이 비어 있다";

                SimFloorRecord simFloor = record.floors[0];
                if (simFloor.floor != 1) return $"시드 {seed}: 첫 기록이 {simFloor.floor}층이다";
                if (simFloor.spins.Count == 0) return $"시드 {seed}: 1층에서 한 번도 안 돌렸다";

                FloorPlan plan = PrototypeCurriculum.For(1);
                var engine = new SpinEngine(seed);
                var session = new FloorSession(plan, engine, PowerThresholds.Default, 0f);

                if (Math.Abs(session.RequiredPower - simFloor.requiredPower) > 0.001f)
                    return $"시드 {seed}: 요구 전력이 게임 {session.RequiredPower:F2} vs " +
                           $"시뮬 {simFloor.requiredPower:F2}";

                for (int i = 0; i < simFloor.spins.Count; i++)
                {
                    if (session.Phase == FloorPhase.Decision && !session.PushYourLuck())
                        return $"시드 {seed}: 게임은 {i}회에서 멈추는데 시뮬은 {simFloor.spins.Count}회 돌았다";

                    SpinResolution game = session.Spin();
                    SpinResolution simSpin = simFloor.spins[i].resolution;

                    if (game.Seed != simSpin.Seed)
                        return $"시드 {seed} 스핀 {i}: 시드가 게임 {game.Seed} vs 시뮬 {simSpin.Seed}";

                    string boardDiff = FirstBoardDifference(game.InitialBoard, simSpin.InitialBoard);
                    if (boardDiff != null)
                        return $"시드 {seed} 스핀 {i}: 판이 다르다 — {boardDiff}";

                    if (Math.Abs(game.NetPower - simSpin.NetPower) > 0.001f)
                        return $"시드 {seed} 스핀 {i}: 순전력이 게임 {game.NetPower:F3} vs " +
                               $"시뮬 {simSpin.NetPower:F3}";
                }

                // 누적 전력까지 같아야 판돈 식이 같다는 뜻이다. 판만 같고 전력이 다르면
                // 앤티가 갈라진 것이고, 그게 정확히 옛 하드코딩이 만들던 상태다.
                if (Math.Abs(session.Power - simFloor.finalPower) > 0.01f)
                    return $"시드 {seed}: 층 전력이 게임 {session.Power:F3} vs " +
                           $"시뮬 {simFloor.finalPower:F3} (판돈 식이 갈렸다)";
            }
            return null;
        }

        // ────────────────────────────────────────────────────────────── 판돈

        /// <summary>
        /// 판돈 비율을 2배로 하면 **첫 추가 스핀의 판돈이 정확히 2배**여야 한다.
        ///
        /// 왜 「2배」라는 관계인가: 절대값을 박으면 밸런스를 고칠 때마다 이 파일을 함께
        /// 고쳐야 하고, 그러면 테스트가 값이 아니라 옛 값을 지키게 된다. 첫 추가 스핀을
        /// 고르는 이유는 그 시점의 전력이 두 런에서 **반드시 같기** 때문이다 —
        /// 판은 좌표 시드라 정책·판돈과 무관하고, 판돈은 아직 한 번도 안 나갔다.
        /// </summary>
        private static string TestAnteComesFromProfile()
        {
            OverharvestSnapshot baseline = OverharvestProfile.DefaultSnapshot;
            OverharvestSnapshot doubled = WithAnteRatio(baseline, baseline.AnteRatio * 2f);

            foreach (int seed in Seeds)
            {
                float a = FirstAnte(seed, baseline);
                float b = FirstAnte(seed, doubled);
                if (a <= 0f) continue;          // 이 시드는 추가 스핀이 없었다
                if (b <= 0f) return $"시드 {seed}: 비율을 올렸더니 판돈이 사라졌다";

                float ratio = b / a;
                if (Math.Abs(ratio - 2f) > 0.01f)
                    return $"시드 {seed}: 판돈 비율을 2배로 했는데 판돈은 {ratio:F3}배다 — " +
                           "시뮬이 프로파일이 아니라 자기 상수를 보고 있다";
                return null;
            }
            return "추가 스핀이 한 번도 일어나지 않았다 — 이 단정이 아무것도 지키지 못한다";
        }

        /// <summary>첫 추가 스핀에 실제로 나간 판돈. 없으면 0.</summary>
        private static float FirstAnte(int seed, OverharvestSnapshot overharvest)
        {
            var sim = new RunSimulator(seed, overharvest);
            SimRunRecord record = sim.RunOnce(seed, SimPolicy.Greedy(), 0);
            foreach (SimFloorRecord floor in record.floors)
                foreach (SimSpinRecord spin in floor.spins)
                    if (spin.wasAdditionalSpin) return spin.additionalSpinAnte;
            return 0f;
        }

        /// <summary>
        /// 추가 스핀 상한 0 — 게임에서는 `FloorSession.CanTakeExtraSpin` 이 거짓이 되어
        /// 레버가 아무것도 하지 않는다. 시뮬도 같아야 한다.
        ///
        /// **기본값 쪽이 실제로 과수확을 한다는 것을 함께 단정한다.** 그 확인이 없으면
        /// 「원래 아무도 안 했다」와 「상한이 막았다」가 구분되지 않는다.
        /// </summary>
        private static string TestExtraSpinCapStopsOverharvest()
        {
            OverharvestSnapshot baseline = OverharvestProfile.DefaultSnapshot;
            OverharvestSnapshot capped = WithMaxExtraSpins(baseline, 0);

            int baselineExtras = 0;
            foreach (int seed in Seeds) baselineExtras += CountExtraSpins(seed, baseline);
            if (baselineExtras == 0)
                return "기본값에서도 추가 스핀이 0이다 — 이 단정이 아무것도 지키지 못한다";

            foreach (int seed in Seeds)
            {
                int extras = CountExtraSpins(seed, capped);
                if (extras != 0)
                    return $"시드 {seed}: 상한 0인데 추가 스핀이 {extras}회 일어났다";
            }
            return null;
        }

        /// <summary>
        /// 해금 임계를 올리면 그 달성률에 **닿기 전까지는** 과수확이 잠긴다
        /// (`FloorSession.IsOverharvestUnlocked` = `OverharvestSnapshot.IsUnlocked`).
        ///
        /// ⚠ 「임계를 5배로 올리면 추가 스핀이 0이 된다」로 쓰면 **틀린 단정**이다.
        /// 큰 연쇄 한 번이면 요구의 5배를 넘기는 층이 실제로 있고(시드 20250805 에서
        /// 관측했다), 그때 잠금이 풀리는 것이 규칙대로다. 결과가 아니라 **규칙**을 본다 —
        /// 일어난 모든 추가 스핀에 대해, 판돈을 내기 직전의 전력이 임계 이상이었는가.
        ///
        /// 판돈 직전의 전력은 되계산한다: 판돈 = 전력 × `AnteRatioForPull(k)` 이므로
        /// 전력 = 판돈 ÷ `AnteRatioForPull(k)`. 시뮬이 자기 상수를 되찾아 쓰면 이 나눗셈이
        /// 어긋나 함께 걸린다.
        /// </summary>
        private static string TestUnlockThresholdLocksOverharvest()
        {
            OverharvestSnapshot baseline = OverharvestProfile.DefaultSnapshot;
            const float threshold = 5f;
            OverharvestSnapshot locked = WithUnlockThreshold(baseline, threshold);

            int baselineExtras = 0;
            int lockedExtras = 0;
            foreach (int seed in Seeds)
            {
                baselineExtras += CountExtraSpins(seed, baseline);
                lockedExtras += CountExtraSpins(seed, locked);
            }
            if (baselineExtras == 0)
                return "기본값에서도 추가 스핀이 0이다 — 이 단정이 아무것도 지키지 못한다";
            if (lockedExtras >= baselineExtras)
                return $"임계를 {threshold:F1} 로 올렸는데 추가 스핀이 줄지 않았다 " +
                       $"(기본 {baselineExtras}회 → 잠금 {lockedExtras}회) — 임계가 아무것도 막지 않는다";

            foreach (int seed in Seeds)
            {
                var sim = new RunSimulator(seed, locked);
                SimRunRecord record = sim.RunOnce(seed, SimPolicy.Greedy(), 0);
                foreach (SimFloorRecord floor in record.floors)
                {
                    int extraIndex = 0;
                    foreach (SimSpinRecord spin in floor.spins)
                    {
                        if (!spin.wasAdditionalSpin) continue;
                        float ratio = locked.AnteRatioForPull(extraIndex);
                        extraIndex++;
                        if (ratio <= 0f) return "판돈 비율이 0이라 전력을 되계산할 수 없다";

                        float powerBeforeAnte = spin.additionalSpinAnte / ratio;
                        float needed = threshold * floor.requiredPower;
                        // 되계산이 부동소수 왕복이라 상대 오차를 둔다. 임계를 어긴 경우는
                        // 배수 단위로 벌어지므로 이 여유가 실패를 삼키지 않는다.
                        if (powerBeforeAnte < needed * 0.999f)
                            return $"시드 {seed} {floor.floor}층: 달성률 " +
                                   $"{powerBeforeAnte / Math.Max(1f, floor.requiredPower):F2} 에서 " +
                                   $"과수확이 열렸다 (임계 {threshold:F1})";
                    }
                }
            }
            return null;
        }

        private static int CountExtraSpins(int seed, OverharvestSnapshot overharvest)
        {
            var sim = new RunSimulator(seed, overharvest);
            SimRunRecord record = sim.RunOnce(seed, SimPolicy.Greedy(), 0);
            int count = 0;
            foreach (SimFloorRecord floor in record.floors) count += floor.additionalSpinChoices;
            return count;
        }

        // ──────────────────────────────────────────────────────────── 잔류 이월

        /// <summary>
        /// 게임은 층이 끝날 때 잔류를 다음 층으로 넘긴다
        /// (`RunSession.CompleteFloor` → `FloorSession` 생성자). 시뮬은 층마다 비우고 있었다.
        ///
        /// **관측 가능한 표본만 판정한다.** 이월이 판을 바꾸는 것은 증식체 잔류가 있을
        /// 때뿐이므로(`SpinEngine.PrepareRules` 가 증식체 가중치만 올린다), 그런 층 경계를
        /// 찾아 「이월한 판」과 「비운 판」을 둘 다 만들어 보고 **실제로 갈리는** 경우에만
        /// 대조한다. 표본을 끝내 못 찾으면 통과가 아니라 **실패**다 — 아무것도 지키지
        /// 못하는 검사가 초록으로 남는 것이 이 저장소가 반복해서 당한 실패다.
        /// </summary>
        private static string TestResidualCarriesAcrossFloors()
        {
            for (int seed = 1000; seed < 1120; seed++)
            {
                var sim = new RunSimulator(seed);
                SimRunRecord record = sim.RunOnce(seed, SimPolicy.Greedy(), 0);

                for (int f = 0; f + 1 < record.floors.Count; f++)
                {
                    SimFloorRecord previous = record.floors[f];
                    SimFloorRecord next = record.floors[f + 1];
                    if (previous.spins.Count == 0 || next.spins.Count == 0) continue;

                    ResidualState carried = previous.spins[previous.spins.Count - 1].resolution.Residual;
                    if (carried.NextProliferatorWeightAdd <= 0f) continue;

                    FloorPlan plan = PrototypeCurriculum.For(next.floor);
                    ResistanceContract contract = next.selectedContract;
                    SpinRuleSet rules = PrototypeCurriculum.BuildRules(in plan);
                    rules.Apply(in contract);

                    int spinSeed = SpinSeed.Derive(seed, next.floor, 0);
                    ResidualState empty = ResidualState.Empty;
                    var engine = new SpinEngine(seed);
                    SpinResolution withCarry =
                        engine.SpinWithSeed(spinSeed, rules, in contract, in carried);
                    SpinResolution withoutCarry =
                        engine.SpinWithSeed(spinSeed, rules, in contract, in empty);

                    // 두 경로가 같은 판을 내면 이 표본으로는 아무것도 못 가린다.
                    if (FirstBoardDifference(withCarry.InitialBoard, withoutCarry.InitialBoard) == null)
                        continue;

                    SpinResolution actual = next.spins[0].resolution;
                    string diff = FirstBoardDifference(withCarry.InitialBoard, actual.InitialBoard);
                    if (diff != null)
                        return $"시드 {seed} {next.floor}층 첫 판이 이월된 잔류로 만든 판과 다르다 " +
                               $"({diff}) — 층 경계에서 잔류가 사라진다";
                    return null;
                }
            }
            return "이월 여부를 구분할 표본을 120개 시드에서 못 찾았다 — " +
                   "이 검사가 아무것도 지키지 못하고 있다";
        }

        // ────────────────────────────────────────────────────────────── 도우미

        /// <summary>두 판의 첫 불일치 칸. 같으면 null.</summary>
        private static string FirstBoardDifference(SpinBoard left, SpinBoard right)
        {
            for (int index = 0; index < SpinBoard.Cells; index++)
                if (left[index] != right[index])
                    return $"{index}번 칸 {left[index]} vs {right[index]}";
            return null;
        }

        private static OverharvestSnapshot WithAnteRatio(OverharvestSnapshot s, float anteRatio)
            => new OverharvestSnapshot(anteRatio, s.AnteEscalation, s.UnlockThreshold,
                s.ApproachMachineDuckScale, s.MinSilenceSeconds, s.MaxSilenceSeconds,
                s.PassengerGazeDelaySeconds, s.ResumeFadeSeconds, s.MaxExtraSpins);

        private static OverharvestSnapshot WithMaxExtraSpins(OverharvestSnapshot s, int maxExtraSpins)
            => new OverharvestSnapshot(s.AnteRatio, s.AnteEscalation, s.UnlockThreshold,
                s.ApproachMachineDuckScale, s.MinSilenceSeconds, s.MaxSilenceSeconds,
                s.PassengerGazeDelaySeconds, s.ResumeFadeSeconds, maxExtraSpins);

        private static OverharvestSnapshot WithUnlockThreshold(OverharvestSnapshot s, float unlockThreshold)
            => new OverharvestSnapshot(s.AnteRatio, s.AnteEscalation, unlockThreshold,
                s.ApproachMachineDuckScale, s.MinSilenceSeconds, s.MaxSilenceSeconds,
                s.PassengerGazeDelaySeconds, s.ResumeFadeSeconds, s.MaxExtraSpins);
    }
}
