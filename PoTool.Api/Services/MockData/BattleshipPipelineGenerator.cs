using PoTool.Core.Pipelines;
using Microsoft.Extensions.Logging;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Generates mock pipeline data for the Battleship Incident Handling system.
/// Creates realistic build and release pipelines with varied run histories.
/// </summary>
public class BattleshipPipelineGenerator
{
    private readonly ILogger<BattleshipPipelineGenerator> _logger;
    private readonly Random _random;

    // Pipeline names themed around the Battleship system
    private static readonly string[] BuildPipelineNames = new[]
    {
        "Battleship.Core.CI",
        "Battleship.IncidentDetection.CI",
        "Battleship.DamageControl.CI",
        "Battleship.CrewSafety.CI",
        "Battleship.HullIntegrity.CI",
        "Battleship.FireSuppression.CI",
        "Battleship.CommunicationSystems.CI",
        "Battleship.SensorNetwork.CI",
        "Battleship.AlertManagement.CI",
        "Battleship.ReportingDashboard.CI",
        "Battleship.Mobile.Android.CI",
        "Battleship.Mobile.iOS.CI",
        "Battleship.API.Gateway.CI",
        "Battleship.Database.Migrations.CI",
        "Battleship.Infrastructure.CI"
    };

    private static readonly string[] ReleasePipelineNames = new[]
    {
        "Battleship.Core.Release",
        "Battleship.IncidentDetection.Deploy",
        "Battleship.DamageControl.Deploy",
        "Battleship.Production.Release",
        "Battleship.Staging.Deploy",
        "Battleship.QA.Deploy",
        "Battleship.Integration.Deploy"
    };

    private static readonly string[] Branches = new[]
    {
        "main",
        "develop",
        "release/v1.0",
        "release/v1.1",
        "release/v2.0",
        "feature/fire-detection",
        "feature/hull-monitoring",
        "feature/crew-tracking",
        "hotfix/sensor-calibration",
        "hotfix/alert-timing"
    };

    private static readonly string[] TriggerUsers = new[]
    {
        "alice.johnson@battleship.mil",
        "bob.smith@battleship.mil",
        "charlie.davis@battleship.mil",
        "diana.wilson@battleship.mil",
        "edward.brown@battleship.mil",
        "fiona.taylor@battleship.mil",
        "george.martinez@battleship.mil",
        "helen.anderson@battleship.mil",
        "ivan.thomas@battleship.mil",
        "julia.white@battleship.mil"
    };

    public BattleshipPipelineGenerator(ILogger<BattleshipPipelineGenerator> logger)
    {
        _logger = logger;
        _random = new Random(42); // Fixed seed for reproducibility
    }

    /// <summary>
    /// Generates all pipeline definitions.
    /// </summary>
    public List<PipelineDto> GeneratePipelines()
    {
        _logger.LogInformation("Generating mock pipelines...");
        var pipelines = new List<PipelineDto>();
        var id = 1;

        // Generate build pipelines
        foreach (var name in BuildPipelineNames)
        {
            pipelines.Add(new PipelineDto(
                Id: id++,
                Name: name,
                Type: PipelineType.Build,
                Path: "\\Battleship\\Builds",
                RetrievedAt: DateTimeOffset.UtcNow
            ));
        }

        // Generate release pipelines
        foreach (var name in ReleasePipelineNames)
        {
            pipelines.Add(new PipelineDto(
                Id: id++,
                Name: name,
                Type: PipelineType.Release,
                Path: "\\Battleship\\Releases",
                RetrievedAt: DateTimeOffset.UtcNow
            ));
        }

        _logger.LogInformation("Generated {Count} pipelines", pipelines.Count);
        return pipelines;
    }

    /// <summary>
    /// Generates pipeline runs for all pipelines with realistic patterns.
    /// </summary>
    public List<PipelineRunDto> GenerateRuns(List<PipelineDto> pipelines, int runsPerPipeline = 50)
    {
        _logger.LogInformation("Generating mock pipeline runs ({RunsPerPipeline} per pipeline)...", runsPerPipeline);
        var runs = new List<PipelineRunDto>();
        var runId = 1000;

        foreach (var pipeline in pipelines)
        {
            // Determine pipeline characteristics
            var failureRate = GetPipelineFailureRate(pipeline.Name);
            var avgDuration = GetPipelineAverageDuration(pipeline.Type);
            var durationVariance = avgDuration.TotalMinutes * 0.3; // 30% variance

            var runCount = _random.Next(runsPerPipeline - 10, runsPerPipeline + 10);
            var consecutiveFailures = 0;

            for (int i = 0; i < runCount; i++)
            {
                var startTime = DateTimeOffset.UtcNow.AddDays(-_random.Next(1, 90)).AddHours(-_random.Next(0, 24));
                var duration = TimeSpan.FromMinutes(avgDuration.TotalMinutes + (_random.NextDouble() - 0.5) * durationVariance * 2);
                if (duration < TimeSpan.FromMinutes(1)) duration = TimeSpan.FromMinutes(1);

                var result = DetermineRunResult(failureRate, consecutiveFailures);
                
                if (result == PipelineRunResult.Failed)
                {
                    consecutiveFailures++;
                }
                else
                {
                    consecutiveFailures = 0;
                }

                var trigger = DetermineTrigger(pipeline.Type);
                var finishTime = result == PipelineRunResult.Canceled 
                    ? startTime.Add(TimeSpan.FromMinutes(_random.Next(1, (int)duration.TotalMinutes)))
                    : startTime.Add(duration);

                runs.Add(new PipelineRunDto(
                    RunId: runId++,
                    PipelineId: pipeline.Id,
                    PipelineName: pipeline.Name,
                    StartTime: startTime,
                    FinishTime: finishTime,
                    Duration: finishTime - startTime,
                    Result: result,
                    Trigger: trigger,
                    TriggerInfo: GetTriggerInfo(trigger),
                    Branch: Branches[_random.Next(Branches.Length)],
                    RequestedFor: TriggerUsers[_random.Next(TriggerUsers.Length)],
                    RetrievedAt: DateTimeOffset.UtcNow
                ));
            }
        }

        // Sort runs by start time descending (most recent first)
        runs = runs.OrderByDescending(r => r.StartTime).ToList();

        _logger.LogInformation("Generated {Count} pipeline runs", runs.Count);
        return runs;
    }

    private double GetPipelineFailureRate(string pipelineName)
    {
        // Some pipelines are more stable than others
        if (pipelineName.Contains("Infrastructure") || pipelineName.Contains("Database"))
            return 0.15; // Higher failure rate for infrastructure
        if (pipelineName.Contains("Mobile"))
            return 0.12; // Mobile builds can be flaky
        if (pipelineName.Contains("Integration"))
            return 0.10; // Integration tests sometimes fail
        if (pipelineName.Contains("Core"))
            return 0.05; // Core is well-tested
        return 0.08; // Default failure rate
    }

    private TimeSpan GetPipelineAverageDuration(PipelineType type)
    {
        return type switch
        {
            PipelineType.Build => TimeSpan.FromMinutes(_random.Next(5, 25)),
            PipelineType.Release => TimeSpan.FromMinutes(_random.Next(15, 45)),
            _ => TimeSpan.FromMinutes(10)
        };
    }

    private PipelineRunResult DetermineRunResult(double failureRate, int consecutiveFailures)
    {
        var roll = _random.NextDouble();

        // Increase failure probability after consecutive failures (simulates flaky behavior)
        var adjustedFailureRate = failureRate + (consecutiveFailures * 0.05);

        if (roll < 0.02) return PipelineRunResult.Canceled; // 2% canceled
        if (roll < adjustedFailureRate + 0.02) return PipelineRunResult.Failed;
        if (roll < adjustedFailureRate + 0.07) return PipelineRunResult.PartiallySucceeded; // 5% partial
        return PipelineRunResult.Succeeded;
    }

    private PipelineRunTrigger DetermineTrigger(PipelineType type)
    {
        var roll = _random.NextDouble();

        if (type == PipelineType.Build)
        {
            if (roll < 0.60) return PipelineRunTrigger.ContinuousIntegration;
            if (roll < 0.75) return PipelineRunTrigger.PullRequest;
            if (roll < 0.90) return PipelineRunTrigger.Manual;
            return PipelineRunTrigger.Schedule;
        }
        else // Release
        {
            if (roll < 0.50) return PipelineRunTrigger.BuildCompletion;
            if (roll < 0.80) return PipelineRunTrigger.Manual;
            if (roll < 0.95) return PipelineRunTrigger.Schedule;
            return PipelineRunTrigger.ResourceTrigger;
        }
    }

    private string? GetTriggerInfo(PipelineRunTrigger trigger)
    {
        return trigger switch
        {
            PipelineRunTrigger.ContinuousIntegration => "Triggered by push to branch",
            PipelineRunTrigger.PullRequest => $"PR #{_random.Next(100, 500)}",
            PipelineRunTrigger.Manual => "Manual trigger",
            PipelineRunTrigger.Schedule => "Scheduled run (nightly)",
            PipelineRunTrigger.BuildCompletion => $"Build #{_random.Next(1000, 2000)} completed",
            PipelineRunTrigger.ResourceTrigger => "Resource trigger",
            _ => null
        };
    }
}
