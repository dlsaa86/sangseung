using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype;

/// <summary>
/// T-08 entry point: runs a batch of headless simulated runs and writes JSON + CSV logs.
/// Editor-only; the simulator itself lives in the runtime assembly so it stays scene-free.
/// </summary>
public static class PlaytestSimRunner
{
    private const int RunsPerPolicy = 4;   // 3 policies x 4 = 12 runs, above the 10-run minimum

    [MenuItem("Ascend/Run Playtest Simulation")]
    public static void Run()
    {
        var config = LoadOne<PrototypeConfig>("PrototypeConfig");
        var balls = LoadOne<BallDatabase>("BallDatabase");
        var combo = LoadOne<CombinationConfig>("CombinationConfig");
        var effectSettings = LoadOne<EffectResolverSettings>("EffectResolverSettings");
        var passengers = LoadAll<PassengerDefinition>("PassengerDefinition");

        var missing = new List<string>();
        if (config == null) missing.Add("PrototypeConfig");
        if (balls == null) missing.Add("BallDatabase");
        if (combo == null) missing.Add("CombinationConfig");
        if (effectSettings == null) missing.Add("EffectResolverSettings");
        if (passengers.Count == 0) missing.Add("PassengerDefinition (0개)");
        if (missing.Count > 0)
        {
            Debug.LogError("[상승] 시뮬레이션 중단 — 에셋 누락: " + string.Join(", ", missing));
            return;
        }

        passengers.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        var simulator = new RunSimulator(config, balls, combo, effectSettings, passengers);
        var policies = new List<SimPolicy> { SimPolicy.Light(), SimPolicy.Balanced(), SimPolicy.Overload() };

        var batch = new SimBatchResult
        {
            generatedAtUtc = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)
        };

        int runIndex = 0;
        foreach (SimPolicy policy in policies)
        {
            for (int i = 0; i < RunsPerPolicy; i++)
            {
                int seed = config.randomSeed + runIndex;
                batch.runs.Add(simulator.RunOnce(seed, policy, runIndex));
                runIndex++;
            }
        }

        batch.runCount = batch.runs.Count;
        float floorSum = 0f, moneySum = 0f, accidentSum = 0f;
        foreach (SimRunRecord r in batch.runs)
        {
            if (r.outcome == "Success") batch.successCount++;
            floorSum += r.highestFloor;
            moneySum += r.finalMoney;
            accidentSum += r.totalAccidents;
        }
        batch.averageHighestFloor = floorSum / Mathf.Max(1, batch.runCount);
        batch.averageMoney = moneySum / Mathf.Max(1, batch.runCount);
        batch.averageAccidents = accidentSum / Mathf.Max(1, batch.runCount);

        string dir = Path.Combine(Directory.GetCurrentDirectory(), "PlaytestLogs");
        Directory.CreateDirectory(dir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var utf8Bom = new UTF8Encoding(true);

        string jsonPath = Path.Combine(dir, $"sim_{stamp}.json");
        File.WriteAllText(jsonPath, JsonUtility.ToJson(batch, true), utf8Bom);

        string floorsPath = Path.Combine(dir, $"sim_{stamp}_floors.csv");
        File.WriteAllText(floorsPath, BuildFloorsCsv(batch), utf8Bom);

        string turnsPath = Path.Combine(dir, $"sim_{stamp}_turns.csv");
        File.WriteAllText(turnsPath, BuildTurnsCsv(batch), utf8Bom);

        Debug.Log(BuildSummary(batch, policies, jsonPath, floorsPath, turnsPath));
        AssetDatabase.Refresh();
    }

    // ── CSV ──

    private static string Esc(string s)
    {
        s = s ?? string.Empty;
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string F(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

    private static string BuildFloorsCsv(SimBatchResult batch)
    {
        var sb = new StringBuilder();
        sb.AppendLine("run,seed,policy,outcome,floor,candidates,boarded,weight,allowed,overloaded," +
                      "accidentChance,accidentOccurred,accidentLoss,requiredPower,finalPower,success," +
                      "retries,surplus,overchargeChoice,floorsClimbed");
        foreach (SimRunRecord r in batch.runs)
            foreach (SimFloorRecord f in r.floors)
                sb.AppendLine(string.Join(",",
                    r.runIndex.ToString(), r.seed.ToString(), Esc(r.policyName), Esc(r.outcome),
                    f.floorIndex.ToString(), Esc(f.candidatesOffered), Esc(f.passengerBoarded),
                    F(f.totalWeight), F(f.allowedWeight), f.overloaded ? "1" : "0",
                    F(f.accidentChance), f.accidentOccurred ? "1" : "0", F(f.accidentLoss),
                    F(f.requiredPower), F(f.finalPower), f.success ? "1" : "0",
                    f.retries.ToString(), F(f.surplus), Esc(f.overchargeChoice),
                    f.floorsClimbed.ToString()));
        return sb.ToString();
    }

    private static string BuildTurnsCsv(SimBatchResult batch)
    {
        var sb = new StringBuilder();
        sb.AppendLine("run,policy,floor,turn,ball0,grade0,ball1,grade1,ball2,grade2," +
                      "perfectStop,combination,powerBeforeEffects,powerAfterEffects,moneyDelta,effectLog");
        foreach (SimRunRecord r in batch.runs)
            foreach (SimFloorRecord f in r.floors)
                foreach (SimTurnRecord t in f.turns)
                    sb.AppendLine(string.Join(",",
                        r.runIndex.ToString(), Esc(r.policyName), f.floorIndex.ToString(), t.turnIndex.ToString(),
                        Esc(t.ball0), Esc(t.grade0), Esc(t.ball1), Esc(t.grade1), Esc(t.ball2), Esc(t.grade2),
                        t.perfectStop ? "1" : "0", Esc(t.combination),
                        F(t.powerBeforeEffects), F(t.powerAfterEffects), F(t.moneyDelta), Esc(t.effectLog)));
        return sb.ToString();
    }

    // ── Console summary ──

    private static string BuildSummary(SimBatchResult batch, List<SimPolicy> policies,
                                       string jsonPath, string floorsPath, string turnsPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[상승] === 플레이테스트 시뮬레이션 {batch.runCount}런 ===");
        sb.AppendLine($"전체 성공 {batch.successCount}/{batch.runCount}  " +
                      $"평균 최고층 {batch.averageHighestFloor:F1}  " +
                      $"평균 돈 {batch.averageMoney:F0}  평균 사고 {batch.averageAccidents:F1}회");
        sb.AppendLine();
        sb.AppendLine("정책별:");
        foreach (SimPolicy p in policies)
        {
            int n = 0, ok = 0, acc = 0, retries = 0;
            float fl = 0f, money = 0f;
            foreach (SimRunRecord r in batch.runs)
            {
                if (r.policyName != p.name) continue;
                n++;
                if (r.outcome == "Success") ok++;
                fl += r.highestFloor; money += r.finalMoney;
                acc += r.totalAccidents; retries += r.totalRetries;
            }
            if (n == 0) continue;
            sb.AppendLine($"  {p.name,-6} 성공 {ok}/{n}  평균 최고층 {fl / n,5:F1}  " +
                          $"평균 돈 {money / n,6:F0}  평균 사고 {(float)acc / n,4:F1}  평균 재시도 {(float)retries / n,4:F1}");
        }
        sb.AppendLine();
        var reasons = new Dictionary<string, int>();
        foreach (SimRunRecord r in batch.runs)
        {
            if (string.IsNullOrEmpty(r.failureReason)) continue;
            reasons.TryGetValue(r.failureReason, out int c);
            reasons[r.failureReason] = c + 1;
        }
        if (reasons.Count > 0)
        {
            sb.AppendLine("실패 원인 분포:");
            foreach (var kv in reasons) sb.AppendLine($"  {kv.Value}회  {kv.Key}");
        }
        else sb.AppendLine("실패 없음");
        sb.AppendLine();
        sb.AppendLine("산출물:");
        sb.AppendLine("  " + jsonPath);
        sb.AppendLine("  " + floorsPath);
        sb.AppendLine("  " + turnsPath);
        return sb.ToString();
    }

    // ── Asset loading ──

    public static T LoadOne<T>(string typeName) where T : ScriptableObject
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeName))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) return asset;
        }
        return null;
    }

    public static List<T> LoadAll<T>(string typeName) where T : ScriptableObject
    {
        var list = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeName))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) list.Add(asset);
        }
        return list;
    }
}
