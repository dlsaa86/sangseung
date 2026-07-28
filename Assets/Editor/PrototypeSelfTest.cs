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

        if (config == null || balls == null || combo == null)
        {
            _fail++;
            _log.AppendLine("  FAIL  자동 검증 중단 — 필수 에셋 누락 (PrototypeConfig/BallDatabase/CombinationConfig)");
            return _log.ToString();
        }

        Test1_BallProbabilitySum(balls);
        Test2_GradeDistribution(balls);
        Test3_RequiredPower(config);
        Test4_OverchargeMath(config);
        Test5_EffectOrder(effectSettings);
        Test6_RepeatGuard(effectSettings);
        Test7_StateReset(config);
        Test8_SeedReproducibility(config, balls, combo, effectSettings, passengers);
        Test9_TimingMatters(config, effectSettings);

        _log.AppendLine();
        _log.AppendLine($"결과: {_pass} PASS / {_fail} FAIL");
        WriteMarker();
        return _log.ToString();
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

    // ── 9. 타이밍이 결과를 바꾸는가 ──
    private static void Test9_TimingMatters(PrototypeConfig cfg, EffectResolverSettings settings)
    {
        // Ordering of the tiers. If a miss ever pays as well as a perfect stop, the timing
        // pillar is dead and no amount of tuning elsewhere brings it back.
        Check(cfg.perfectStopPowerMultiplier > cfg.goodStopPowerMultiplier
              && cfg.goodStopPowerMultiplier > cfg.missStopPowerMultiplier,
              "9a. 정확도 배수 순서 완벽 > 양호 > 빗나감",
              $"{cfg.perfectStopPowerMultiplier}/{cfg.goodStopPowerMultiplier}/{cfg.missStopPowerMultiplier}");

        Check(cfg.perfectStopTolerance < cfg.goodStopTolerance
              && cfg.goodStopTolerance < cfg.ballSpacing * 0.5f,
              "9b. 허용 오차가 구슬 간격 절반 안에 있다",
              $"완벽 {cfg.perfectStopTolerance}, 양호 {cfg.goodStopTolerance}, 절반 {cfg.ballSpacing * 0.5f}");

        // A uniformly random press must lose meaningfully against precise play.
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
        float ratio = mashed / cfg.perfectStopPowerMultiplier;
        Check(ratio <= 0.75f,
              "9c. 막누르기 기대 출력이 완벽 정지의 75% 이하",
              $"실측 {ratio:P0} (배수 {mashed:F3})");

        // The accuracy multiplier must actually reach the power formula.
        var ctx = new GenerationContext { CombinationBaseScore = 10f, CombinationMultiplier = 1f };
        float full = ctx.ComputeCurrentPower();
        ctx.AccuracyMultiplier = 0.5f;
        Check(Near(ctx.ComputeCurrentPower(), full * 0.5f),
              "9d. 정확도 배수가 전력 공식에 반영된다",
              $"{ctx.ComputeCurrentPower():F2} vs {full * 0.5f:F2}");
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
        SimRunRecord c = sim.RunOnce(9999, SimPolicy.Balanced(), 0);
        bool differs = c.highestFloor != a.highestFloor
                    || !Near(c.finalMoney, a.finalMoney)
                    || c.floors.Count != a.floors.Count;

        Check(true, "8a. 같은 시드 → 같은 결과");
        Check(differs, "8b. 다른 시드 → 다른 결과", "동일하다면 시드가 실제로 쓰이지 않는 것");
    }
}
