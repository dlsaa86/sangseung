using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype;

/// <summary>
/// T-08 automated checks. Deliberately framework-free — the Test Framework package may not be
/// present, and these need to run from a menu item in any project state.
/// </summary>
public static class PrototypeSelfTest
{
    private static int _pass;
    private static int _fail;
    private static StringBuilder _log;

    [MenuItem("Ascend/Run Self Tests")]
    public static void RunAll()
    {
        string report = RunAllToString();
        if (_fail > 0) Debug.LogError(report);
        else Debug.Log(report);
    }

    /// <summary>Runs every check and returns the full report, so automation can read it directly.</summary>
    public static string RunAllToString()
    {
        _pass = 0; _fail = 0;
        _log = new StringBuilder();
        _log.AppendLine("[상승] === 자동 검증 ===");

        var config = PlaytestSimRunner.LoadOne<PrototypeConfig>("PrototypeConfig");
        var balls = PlaytestSimRunner.LoadOne<BallDatabase>("BallDatabase");
        var combo = PlaytestSimRunner.LoadOne<CombinationConfig>("CombinationConfig");
        var effectSettings = PlaytestSimRunner.LoadOne<EffectResolverSettings>("EffectResolverSettings");
        var passengers = PlaytestSimRunner.LoadAll<PassengerDefinition>("PassengerDefinition");
        passengers.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        // ── UP-TEST-11 웨이브 0 ───────────────────────────────────────────────
        // 위 셋은 **옛 스택**(구슬 조합·타이밍)의 에셋이고 웨이브 8이 지운다.
        // 여기서 `return` 하면 아래 신 스택 스위트 10종이 통째로 실행되지 않은 채
        // fail=1 로 끝나고, `commit-gate.sh` 와 `verify-topdown.ps1` 이 **모든 커밋을 막는다.**
        // 레거시 삭제 순서에서 「자체 검사 편집이 반드시 먼저」인 이유가 정확히 이것이다.
        //
        // 부재는 실패가 아니라 **대상 없음**이다. 다만 조용히 넘기지 않는다 —
        // 합계만 보면 사라진 9건이 눈에 띄지 않고, 그러면 커버리지 손실이 통과로 읽힌다.
        var legacyAssetsPresent = config != null && balls != null && combo != null;
        if (legacyAssetsPresent)
        {
            Test1_BallProbabilitySum(balls);
            Test2_GradeDistribution(balls);
            Test3_RequiredPower(config);
            Test4_OverchargeMath(config);
            Test5_EffectOrder(effectSettings);
            Test6_RepeatGuard(effectSettings);
            Test7_StateReset(config);
            Test8_SeedReproducibility(config, balls, combo, effectSettings, passengers);
            Test9_TimingIsNotATax(config, effectSettings);
        }
        else
        {
            _log.AppendLine("  SKIP  옛 스택 검사 9건 — PrototypeConfig/BallDatabase/CombinationConfig 부재 (UP-TEST-11 진행 중)");
        }

        // 노션 재설계 이후의 코어. 위 1~9번은 폐기 예정인 옛 구조(구슬 조합·타이밍)를 지키고
        // 있어서, 새 룰렛·층 진행이 깨져도 전부 통과한다. 커밋 게이트가 그 상태를 승인하면
        // "검증했다"는 기록이 거짓이 되므로 여기에 편입한다.
        FoldInSuite("자동 룰렛 판정", Ascend.Prototype.Spin.Tests.SpinEngineTests.RunAll());
        FoldInSuite("층 진행·계약·앤티", Ascend.Prototype.Run.Tests.RunTests.RunAll());
        // 같은 이유로 위험·빌드도 편입한다. 여기 없던 동안 커밋 게이트가 지키던 것은
        // 45건이었고 `BuildTests` 34건 · `RiskEvaluatorTests` 11건은 깨져도 커밋이 통과했다 —
        // 10층 진행·적재·과적·다층 상승 클램프가 전부 그 34건 안에 있다.
        // "자체 검증 통과"가 `Ascend/Run All EditMode Tests` 와 같은 것을 뜻해야 한다.
        FoldInSuite("위험 상태", Ascend.Prototype.Risk.Tests.RiskEvaluatorTests.RunAll());
        FoldInSuite("적재·빌드·10층 진행", Ascend.Prototype.Build.Tests.BuildTests.RunAll());

        // Pass 1 에서 들어온 스위트들. 여기 편입하지 않으면 커밋 게이트가 지키는 숫자에
        // 잡히지 않고, 그러면 "자체 검증 통과"가 새 코드에 대해 아무 말도 하지 않는다 —
        // 바로 위 두 줄이 같은 이유로 뒤늦게 편입됐다.
        FoldInSuite("가중치 방어·발동 순서", Ascend.Prototype.Spin.Tests.SpinRuleSetTests.RunAll());
        FoldInSuite("텔레메트리", Ascend.Prototype.Telemetry.Tests.TelemetryTests.RunAll());
        FoldInSuite("데이터 프로파일", Ascend.Prototype.Data.Profiles.Tests.ProfileTests.RunAll());
        FoldInSuite("승객 반응", Ascend.Prototype.Npc.Tests.PassengerReactionTests.RunAll());
        FoldInSuite("사운드 매핑·정적 구간", Ascend.Prototype.Audio.Tests.AudioTests.RunAll());
        FoldInSuite("풀링·메모리 추세", Ascend.Prototype.Perf.Tests.PerfTests.RunAll());

        // 2026-08-02 배치. 등록하지 않으면 **돌지 않으므로 없는 것과 같다** —
        // 바로 위 세 줄이 전부 같은 이유로 뒤늦게 편입됐고, 그 사이 커밋 게이트는
        // 그 스위트들에 대해 아무 말도 하지 않고 있었다.
        FoldInSuite("배선 진단", Ascend.Prototype.Diagnostics.Tests.WiringDiagnosticsTests.RunAll());
        FoldInSuite("런 요약 9종", Ascend.Prototype.UI.Tests.RunSummaryBuilderTests.RunAll());
        FoldInSuite("연출 프로파일 배선", Ascend.Prototype.Effects.Tests.PresentationBindingTests.RunAll());
        FoldInSuite("과수확 5단계 연출", Ascend.Prototype.View.Tests.OverharvestStageTests.RunAll());
        FoldInSuite("통관 공통 잠금 전달", Ascend.Prototype.View.Tests.CustomsLockViewTests.RunAll());
        FoldInSuite("실행 레버 상태 기계", Ascend.Prototype.View.Tests.LeverStateMachineTests.RunAll());
        FoldInSuite("계기판 줄 충돌", Ascend.Prototype.View.Tests.InstrumentPanelLineTests.RunAll());
        // 표현 계층 물리. 등록처가 둘인 것은 실수가 아니라 구조다 —
        // `AscendTestMenu` 는 사람이 누르는 전체 실행, 이쪽은 커밋 게이트가 부르는
        // 스모크다. 한쪽에만 넣으면 게이트가 못 보는 테스트가 생긴다.
        FoldInSuite("표현 계층 물리", Ascend.Prototype.Physics.Tests.PresentationPhysicsTests.RunAll());
        FoldInSuite("절차적 메시", Ascend.Prototype.Art.Tests.ProcMeshTests.RunAll());
        FoldInSuite("원형 현창 3×3", Ascend.Prototype.Art.Tests.PortholeMeshTests.RunAll());
        FoldInSuite("레퍼런스 룸 치수 명세", Ascend.Prototype.Art.Tests.ReferenceRoomSpecTests.RunAll());
        FoldInSuite("남은 스핀 정산", Ascend.Prototype.Data.Profiles.Tests.SettlementTests.RunAll());

        _log.AppendLine();
        _log.AppendLine($"결과: {_pass} PASS / {_fail} FAIL");
        WriteMarker();
        return _log.ToString();
    }

    /// <summary>
    /// 다른 파일에 사는 테스트 묶음의 결과를 이 리포트의 집계에 합친다.
    /// 실패한 케이스 이름은 그대로 옮겨 적는다 — 어느 것이 깨졌는지 로그만 보고 알아야 한다.
    /// </summary>
    private static void FoldInSuite(string label, (int passed, int failed, string report) result)
    {
        _pass += result.passed;
        _fail += result.failed;
        _log.AppendLine();
        _log.AppendLine($"  [{label}] {result.passed} PASS / {result.failed} FAIL");
        if (result.failed > 0 && !string.IsNullOrEmpty(result.report))
        {
            foreach (string line in result.report.Split('\n'))
                if (line.Contains("FAIL")) _log.AppendLine("    " + line.Trim());
        }
    }

    /// <summary>
    /// Records when the suite last ran and whether it passed.
    /// The pre-commit hook compares this file's timestamp against the newest .cs file, so a
    /// commit cannot claim verification it never did.
    /// </summary>
    private static void WriteMarker()
    {
        try
        {
            string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".claude", "state");
            System.IO.Directory.CreateDirectory(dir);
            string body = $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\tpass={_pass}\tfail={_fail}\n";
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "last-selftest.txt"), body);
        }
        catch (System.Exception e)
        {
            // A marker failure must never take the test run down with it.
            Debug.LogWarning("[상승] 자체 검증 마커 기록 실패: " + e.Message);
        }
    }

    // ── assert helpers ──

    private static void Check(bool ok, string name, string detail = "")
    {
        if (ok) { _pass++; _log.AppendLine($"  PASS  {name}"); }
        else { _fail++; _log.AppendLine($"  FAIL  {name}   {detail}"); }
    }

    private static bool Near(float a, float b, float tol = 0.001f) => Mathf.Abs(a - b) <= tol;

    // ── 1. 구슬 확률 합계 ──
    private static void Test1_BallProbabilitySum(BallDatabase db)
    {
        float sum = BallDrawer.SumProbabilities(db);
        Check(Near(sum, 100f, 0.01f), "1. 구슬 확률 합계 == 100", $"실측 {sum:F2}");
    }

    // ── 2. 등급 분포 3/3/2/1 ──
    private static void Test2_GradeDistribution(BallDatabase db)
    {
        var counts = new Dictionary<BallGrade, int>();
        foreach (BallDefinition b in db.balls)
        {
            if (b == null) continue;
            counts.TryGetValue(b.grade, out int c);
            counts[b.grade] = c + 1;
        }
        int com = counts.TryGetValue(BallGrade.Common, out int a) ? a : 0;
        int adv = counts.TryGetValue(BallGrade.Advanced, out int b2) ? b2 : 0;
        int rar = counts.TryGetValue(BallGrade.Rare, out int c2) ? c2 : 0;
        int leg = counts.TryGetValue(BallGrade.Legendary, out int d) ? d : 0;
        Check(com == 3 && adv == 3 && rar == 2 && leg == 1,
              "2. 등급 분포 일반3/고급3/희귀2/전설1",
              $"실측 {com}/{adv}/{rar}/{leg}");
    }

    // ── 3. 요구 전력 공식 ──
    private static void Test3_RequiredPower(PrototypeConfig cfg)
    {
        float actual = FloorMath.ComputeRequiredPower(cfg, 4, 7f, false);
        float expected = cfg.baseRequiredPower + 4 * cfg.requiredPowerGrowthPerFloor + 7f * cfg.weightToPowerFactor;
        Check(Near(actual, expected), "3a. 요구 전력 (정상)", $"{actual:F2} vs {expected:F2}");

        float actualO = FloorMath.ComputeRequiredPower(cfg, 4, 7f, true);
        Check(Near(actualO, expected * cfg.overloadRequiredPowerMultiplier),
              "3b. 요구 전력 (과적 배수)",
              $"{actualO:F2} vs {expected * cfg.overloadRequiredPowerMultiplier:F2}");

        Check(FloorMath.ComputeRequiredPower(cfg, 0, 10f, false) >
              FloorMath.ComputeRequiredPower(cfg, 0, 0f, false),
              "3c. 무게가 요구 전력을 실제로 올린다");
    }

    // ── 4. 초과 전력 계산 ──
    private static void Test4_OverchargeMath(PrototypeConfig cfg)
    {
        bool conserved = true, capped = true;
        foreach (float s in new[] { 0f, 17f, 59f, 60f, 61f, 125f, 180f, 999f })
        {
            OverchargeOption asc = FloorMath.BuildAscendOption(cfg, s);
            if (!Near(asc.SurplusUsed + asc.PowerCarried, s)) conserved = false;
            if (asc.FloorsGained < 0 || asc.FloorsGained > cfg.maxExtraFloorsPerAllocation) capped = false;
        }
        Check(conserved, "4a. 추가 상승: 사용분 + 이월분 == 초과 전력");
        Check(capped, "4b. 추가 상승 층수가 상한 이내");

        OverchargeOption money = FloorMath.BuildMoneyOption(cfg, 100f);
        Check(Near(money.MoneyGained, 100f * cfg.powerToMoneyRatio) && money.FloorsGained == 0,
              "4c. 돈 선택: 전량 변환 + 추가 상승 0층");
    }

    // ── 5. 효과 적용 순서: (base + add) * mul 이어야 한다 ──
    private static void Test5_EffectOrder(EffectResolverSettings settings)
    {
        var add = ScriptableObject.CreateInstance<EffectDefinition>();
        add.id = "SELFTEST_ADD"; add.type = EffectType.Add; add.value = 10f;
        add.probability = 1f; add.condition = EffectCondition.None;

        var mul = ScriptableObject.CreateInstance<EffectDefinition>();
        mul.id = "SELFTEST_MUL"; mul.type = EffectType.Multiply; mul.value = 3f;
        mul.probability = 1f; mul.condition = EffectCondition.None;

        var pipeline = new EffectPipeline(settings, new SystemEffectRandom(1));
        var ctx = new GenerationContext { CombinationBaseScore = 20f, CombinationMultiplier = 1f };
        // Multiply listed first on purpose: fixed type order must still apply Add before Multiply.
        pipeline.Run(ctx, new List<EffectDefinition> { mul, add });

        float addThenMul = (20f + 10f) * 3f;   // 90 — correct
        float mulThenAdd = 20f * 3f + 10f;     // 70 — wrong
        Check(Near(ctx.FinalPower, addThenMul),
              "5. 효과 순서: 가산 후 배수 ((base+add)*mul)",
              $"실측 {ctx.FinalPower:F2}, 기대 {addThenMul:F2} (배수우선이면 {mulThenAdd:F2})");

        Object.DestroyImmediate(add); Object.DestroyImmediate(mul);
    }

    // ── 6. 무한 Repeat 방지 ──
    private static void Test6_RepeatGuard(EffectResolverSettings settings)
    {
        var runaway = ScriptableObject.CreateInstance<EffectDefinition>();
        runaway.id = "SELFTEST_RUNAWAY"; runaway.type = EffectType.Repeat;
        runaway.repeatCount = 5; runaway.probability = 1f; runaway.condition = EffectCondition.None;

        var add = ScriptableObject.CreateInstance<EffectDefinition>();
        add.id = "SELFTEST_ADD2"; add.type = EffectType.Add; add.value = 5f;
        add.probability = 1f; add.condition = EffectCondition.None;

        int maxDepth = settings != null ? settings.maxRecursionDepth : 3;
        int maxActivations = settings != null ? settings.maxTotalActivations : 64;

        var pipeline = new EffectPipeline(settings, new SystemEffectRandom(1));
        var ctx = new GenerationContext { CombinationBaseScore = 10f, CombinationMultiplier = 1f };
        pipeline.Run(ctx, new List<EffectDefinition> { runaway, add });

        int applied = 0;
        foreach (EffectLogEntry e in ctx.Log) if (e.Applied && e.EffectId != null && e.EffectId.StartsWith("SELFTEST")) applied++;

        Check(applied <= maxActivations,
              "6a. 총 발동 횟수가 상한 이내 (무한 루프 방지)",
              $"발동 {applied}회, 상한 {maxActivations}");

        // repeatCount 5 requested but depth is capped, so the bonus must not exceed maxDepth passes.
        float singlePass = (10f + 5f);
        Check(ctx.FinalPower <= singlePass * (maxDepth + 1) + 0.001f,
              "6b. Repeat 결과가 재귀 깊이 상한 이내",
              $"final {ctx.FinalPower:F2}, 상한 {singlePass * (maxDepth + 1):F2}");

        Object.DestroyImmediate(runaway); Object.DestroyImmediate(add);
    }

    // ── 7. 재시작 후 상태 초기화 ──
    private static void Test7_StateReset(PrototypeConfig cfg)
    {
        var st = new ElevatorState
        {
            Power = 999f, Money = 999f, Weight = 999f, AllowedWeight = 1f, CurrentTurn = 9,
            BankedPower = 99f, LastRollSummary = "dirty", LastGenerationPower = 77f,
            LastEffectLog = "dirty", AccidentChance = 0.9f, LastAccidentOccurred = true,
            LastAccidentLoss = 50f, LastAccidentCause = "dirty", BoardedCount = 4,
            RetriesThisFloor = 5, TotalRetries = 7, HighestFloorReached = 8,
            TotalMoneyEarned = 500f, TotalAccidents = 3, LastFailureReason = "dirty"
        };
        st.Initialize(cfg);

        bool clean =
            Near(st.Power, cfg.startingPower) && Near(st.Money, cfg.startingMoney) &&
            Near(st.Weight, cfg.startingWeight) && st.CurrentTurn == 0 && Near(st.BankedPower, 0f) &&
            string.IsNullOrEmpty(st.LastRollSummary) && Near(st.LastGenerationPower, 0f) &&
            string.IsNullOrEmpty(st.LastEffectLog) && Near(st.AccidentChance, 0f) &&
            !st.LastAccidentOccurred && Near(st.LastAccidentLoss, 0f) &&
            string.IsNullOrEmpty(st.LastAccidentCause) && st.BoardedCount == 0 &&
            st.RetriesThisFloor == 0 && st.TotalRetries == 0 && st.HighestFloorReached == 0 &&
            Near(st.TotalMoneyEarned, 0f) && st.TotalAccidents == 0 &&
            string.IsNullOrEmpty(st.LastFailureReason);

        Check(clean, "7. 재시작 후 상태 완전 초기화");
    }

    // ── 9. 타이밍이 출력에 영향을 주지 않는가 ──
    //
    // The design deliberately reversed here. Press precision used to scale power, which turned
    // the game into a reflex test — players reported eye strain and said the pressure to be
    // exact was just irritating. The press now only decides WHICH ball is taken. These checks
    // lock that in so a future tuning pass cannot quietly reintroduce a timing tax.
    private static void Test9_TimingIsNotATax(PrototypeConfig cfg, EffectResolverSettings settings)
    {
        Check(Near(cfg.perfectStopPowerMultiplier, cfg.goodStopPowerMultiplier)
              && Near(cfg.goodStopPowerMultiplier, cfg.missStopPowerMultiplier),
              "9a. 정확도 배수가 전부 동일 (타이밍이 출력에 영향 없음)",
              $"{cfg.perfectStopPowerMultiplier}/{cfg.goodStopPowerMultiplier}/{cfg.missStopPowerMultiplier}");

        // Whatever the player's timing, expected output must be the same.
        const int N = 2000;
        var rng = new System.Random(11);
        float sum = 0f;
        for (int i = 0; i < N; i++)
        {
            float err = Mathf.Abs((float)rng.NextDouble() - 0.5f) * cfg.ballSpacing;
            sum += err <= cfg.perfectStopTolerance ? cfg.perfectStopPowerMultiplier
                 : err <= cfg.goodStopTolerance ? cfg.goodStopPowerMultiplier
                 : cfg.missStopPowerMultiplier;
        }
        float mashed = sum / N;
        Check(Near(mashed, cfg.perfectStopPowerMultiplier, 0.01f),
              "9b. 막누르기와 정밀 입력의 기대 출력이 같다",
              $"막누르기 {mashed:F3} vs 완벽 {cfg.perfectStopPowerMultiplier:F3}");

        // Latency compensation must still be live — it is what makes the intended ball the one
        // you actually get, which matters more now that the ball choice is the whole skill.
        Check(cfg.inputLatencyCompensation > 0f,
              "9c. 입력 지연 보정이 켜져 있다",
              $"현재 {cfg.inputLatencyCompensation:F3}s");

        float shift = cfg.ballMoveSpeed * cfg.inputLatencyCompensation;
        Check(shift < cfg.ballSpacing * 0.5f,
              "9d. 지연 보정이 구슬 하나를 넘기지 않는다",
              $"보정 거리 {shift:F3} vs 전환 경계 {cfg.ballSpacing * 0.5f:F3}");

        // Reading the stream has to be physically possible: a ball must linger long enough to
        // recognise. Below ~200ms players cannot read grade and are forced to stare.
        float periodMs = cfg.ballSpacing / Mathf.Max(0.0001f, cfg.ballMoveSpeed) * 1000f;
        Check(periodMs >= 200f,
              "9e. 구슬 주기가 판독 가능한 범위 (>=200ms)",
              $"현재 {periodMs:F0}ms — 이보다 빠르면 응시를 강요하고 눈이 피로해진다");
    }

    // ── 8. 같은 시드 재현성 ──
    private static void Test8_SeedReproducibility(
        PrototypeConfig cfg, BallDatabase balls, CombinationConfig combo,
        EffectResolverSettings settings, List<PassengerDefinition> passengers)
    {
        var sim = new RunSimulator(cfg, balls, combo, settings, passengers);
        SimRunRecord a = sim.RunOnce(4242, SimPolicy.Balanced(), 0);
        SimRunRecord b = sim.RunOnce(4242, SimPolicy.Balanced(), 0);

        if (a.outcome != b.outcome)
        { Check(false, "8. 같은 시드 재현성", $"outcome {a.outcome} vs {b.outcome}"); return; }
        if (a.highestFloor != b.highestFloor)
        { Check(false, "8. 같은 시드 재현성", $"highestFloor {a.highestFloor} vs {b.highestFloor}"); return; }
        if (!Near(a.finalMoney, b.finalMoney))
        { Check(false, "8. 같은 시드 재현성", $"finalMoney {a.finalMoney:F2} vs {b.finalMoney:F2}"); return; }
        if (a.floors.Count != b.floors.Count)
        { Check(false, "8. 같은 시드 재현성", $"층 수 {a.floors.Count} vs {b.floors.Count}"); return; }

        for (int i = 0; i < a.floors.Count; i++)
        {
            if (!Near(a.floors[i].finalPower, b.floors[i].finalPower))
            {
                Check(false, "8. 같은 시드 재현성",
                      $"층 인덱스 {i} finalPower {a.floors[i].finalPower:F2} vs {b.floors[i].finalPower:F2}");
                return;
            }
        }

        // Sanity: a different seed should not produce an identical trace.
        //
        // highestFloor·finalMoney·floors.Count만 보면 안 된다. 밸런스가 잡힌 뒤로는
        // 어떤 시드든 10층을 클리어하므로 그 세 값이 항상 같아지고, 시드가 완전히
        // 무시되는 상황조차 통과한다. 실제로 그렇게 잘못 실패하고 있었다.
        // 층별 전력은 시드마다 반드시 달라지므로 그것을 본다.
        SimRunRecord c = sim.RunOnce(9999, SimPolicy.Balanced(), 0);
        bool differs = c.highestFloor != a.highestFloor
                    || !Near(c.finalMoney, a.finalMoney)
                    || c.floors.Count != a.floors.Count;

        if (!differs)
        {
            int compared = System.Math.Min(a.floors.Count, c.floors.Count);
            for (int i = 0; i < compared; i++)
            {
                if (!Near(a.floors[i].finalPower, c.floors[i].finalPower)) { differs = true; break; }
            }
        }

        Check(true, "8a. 같은 시드 → 같은 결과");
        Check(differs, "8b. 다른 시드 → 다른 결과", "동일하다면 시드가 실제로 쓰이지 않는 것");
    }
}
