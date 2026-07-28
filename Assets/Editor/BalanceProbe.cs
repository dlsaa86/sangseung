using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype;

/// <summary>
/// Answers one question: does timing skill change the outcome?
///
/// Runs the same seeds under a button-masher and an expert and compares. If the masher clears
/// the run at a similar rate, the timing pillar is not carrying any weight and the numbers
/// need to move — that is a balance verdict, not a taste call, so it belongs in a tool.
/// </summary>
public static class BalanceProbe
{
    private const int RunsPerPolicy = 40;

    [MenuItem("Ascend/Probe Skill Gap")]
    public static void Run()
    {
        var config = PlaytestSimRunner.LoadOne<PrototypeConfig>("PrototypeConfig");
        var balls = PlaytestSimRunner.LoadOne<BallDatabase>("BallDatabase");
        var combo = PlaytestSimRunner.LoadOne<CombinationConfig>("CombinationConfig");
        var settings = PlaytestSimRunner.LoadOne<EffectResolverSettings>("EffectResolverSettings");
        var passengers = PlaytestSimRunner.LoadAll<PassengerDefinition>("PassengerDefinition");
        passengers.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        if (config == null || balls == null || combo == null)
        { Debug.LogError("[상승] 에셋 누락"); return; }

        Debug.Log(Probe(config, balls, combo, settings, passengers));
    }

    /// <summary>Runs both policies over identical seeds and formats the comparison.</summary>
    public static string Probe(PrototypeConfig config, BallDatabase balls, CombinationConfig combo,
                               EffectResolverSettings settings, List<PassengerDefinition> passengers)
    {
        var sim = new RunSimulator(config, balls, combo, settings, passengers);
        var policies = new[] { SimPolicy.Masher(), SimPolicy.Balanced(), SimPolicy.Expert() };

        var sb = new StringBuilder();
        sb.AppendLine("[상승] === 실력 격차 진단 ===");
        sb.AppendLine($"요구전력 기본 {config.baseRequiredPower} +{config.requiredPowerGrowthPerFloor}/층, " +
                      $"층당 턴 {config.generationsPerFloor}, 재시도 {config.maxRetriesPerFloor}, 목표 {config.targetFloor}층");
        sb.AppendLine($"정확도 배수 완벽 {config.perfectStopPowerMultiplier} / 양호 {config.goodStopPowerMultiplier} / 빗나감 {config.missStopPowerMultiplier}");
        sb.AppendLine();
        sb.AppendLine($"{"정책",-8} {"성공률",8} {"평균최고층",10} {"평균재시도",10} {"층당평균전력",12}");

        var successRates = new Dictionary<string, float>();
        foreach (SimPolicy p in policies)
        {
            int ok = 0, retries = 0;
            float floors = 0f, powerSum = 0f;
            int floorRows = 0;

            for (int i = 0; i < RunsPerPolicy; i++)
            {
                SimRunRecord r = sim.RunOnce(config.randomSeed + i, p, i);
                if (r.outcome == "Success") ok++;
                floors += r.highestFloor;
                retries += r.totalRetries;
                foreach (SimFloorRecord f in r.floors) { powerSum += f.finalPower; floorRows++; }
            }

            float rate = (float)ok / RunsPerPolicy;
            successRates[p.name] = rate;
            sb.AppendLine($"{p.name,-8} {rate,7:P0} {floors / RunsPerPolicy,10:F1} " +
                          $"{(float)retries / RunsPerPolicy,10:F1} {powerSum / Mathf.Max(1, floorRows),12:F0}");
        }

        sb.AppendLine();
        float masher = successRates["막누르기"];
        float expert = successRates["숙련형"];
        sb.AppendLine($"막누르기 {masher:P0} → 숙련형 {expert:P0}   격차 {expert - masher:P0}");

        if (masher >= 0.5f)
            sb.AppendLine("판정: 실패 — 막눌러도 절반 이상 클리어된다. 긴장이 생길 수 없다.");
        else if (expert - masher < 0.25f)
            sb.AppendLine("판정: 실패 — 실력이 결과를 충분히 바꾸지 못한다.");
        else if (expert < 0.5f)
            sb.AppendLine("판정: 주의 — 숙련해도 절반을 못 넘긴다. 지나치게 가혹할 수 있다.");
        else
            sb.AppendLine("판정: 통과 — 막누르기는 실패하고 숙련은 보상받는다.");

        return sb.ToString();
    }
}
