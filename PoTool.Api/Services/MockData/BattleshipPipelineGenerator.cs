using PoTool.Shared.Pipelines;
using Microsoft.Extensions.Logging;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Generates mock pipeline data for the Battleship Incident Handling system.
/// Creates realistic build and release pipelines with varied run histories.
/// 
/// DEMO DATA DESIGN:
/// - Includes pipelines with different health states for demonstration:
///   * Currently failing pipelines (consecutive failures) - shows alerting
///   * Flaky pipelines (high failure rate) - shows health issues  
///   * Stable pipelines (high success rate) - shows healthy state
///   * Recently active pipelines - shows real-time activity
///   * Varied durations - demonstrates duration charts
/// </summary>
public class BattleshipPipelineGenerator
{
    private readonly ILogger<BattleshipPipelineGenerator> _logger;
    private readonly Random _random;

    // Pipeline names themed around the Battleship system
    // Organized by health status for demonstration purposes
    private static readonly string[] BuildPipelineNames = new[]
    {
        // STABLE PIPELINES (low failure rate ~3-5%)
        "Battleship.Core.CI",                    // Very stable, critical path
        "Battleship.API.Gateway.CI",             // Well-tested API
        "Battleship.ReportingDashboard.CI",      // Stable UI builds
        
        // NORMAL PIPELINES (moderate failure rate ~8-10%)
        "Battleship.IncidentDetection.CI",
        "Battleship.DamageControl.CI",
        "Battleship.CrewSafety.CI",
        "Battleship.HullIntegrity.CI",
        "Battleship.AlertManagement.CI",
        
        // FLAKY PIPELINES (high failure rate ~15-25%)
        "Battleship.Mobile.Android.CI",          // Flaky mobile builds
        "Battleship.Mobile.iOS.CI",              // Flaky iOS builds
        "Battleship.SensorNetwork.CI",           // Hardware integration issues
        
        // PROBLEMATIC PIPELINES (currently broken, consecutive failures)
        "Battleship.FireSuppression.CI",         // Currently failing!
        "Battleship.CommunicationSystems.CI",    // Infrastructure issues
        "Battleship.Database.Migrations.CI",     // Schema conflicts
        "Battleship.Infrastructure.CI"           // Config problems
    };

    private static readonly string[] ReleasePipelineNames = new[]
    {
        // STABLE RELEASES
        "Battleship.Core.Release",
        "Battleship.QA.Deploy",
        
        // NORMAL RELEASES  
        "Battleship.IncidentDetection.Deploy",
        "Battleship.Staging.Deploy",
        
        // PROBLEMATIC RELEASES
        "Battleship.DamageControl.Deploy",       // Deployment issues
        "Battleship.Production.Release",         // Approval bottlenecks
        "Battleship.Integration.Deploy"          // Environment issues
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
    /// Creates demo-friendly data with:
    /// - Recent runs (within last few hours) for active pipelines
    /// - Consecutive failures for problematic pipelines
    /// - Varied success/failure patterns across pipeline types
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
            var avgDuration = GetPipelineAverageDuration(pipeline.Type, pipeline.Name);
            var durationVariance = avgDuration.TotalMinutes * 0.3; // 30% variance

            var runCount = _random.Next(runsPerPipeline - 10, runsPerPipeline + 10);

            // For problematic pipelines, force consecutive failures at the end (most recent runs)
            var forceFailuresForLastN = GetForcedConsecutiveFailures(pipeline.Name);

            for (int i = 0; i < runCount; i++)
            {
                // Generate start times - ensure some very recent runs
                DateTimeOffset startTime;
                if (i < 3)
                {
                    // First 3 runs are very recent (within last 24 hours) for demo
                    startTime = DateTimeOffset.UtcNow.AddHours(-_random.Next(1, 24));
                }
                else if (i < 10)
                {
                    // Next 7 runs within last week
                    startTime = DateTimeOffset.UtcNow.AddDays(-_random.Next(1, 7)).AddHours(-_random.Next(0, 24));
                }
                else
                {
                    // Rest spread over last 90 days
                    startTime = DateTimeOffset.UtcNow.AddDays(-_random.Next(7, 90)).AddHours(-_random.Next(0, 24));
                }

                var duration = TimeSpan.FromMinutes(avgDuration.TotalMinutes + (_random.NextDouble() - 0.5) * durationVariance * 2);
                if (duration < TimeSpan.FromMinutes(1)) duration = TimeSpan.FromMinutes(1);

                // Force failures for the most recent N runs of problematic pipelines
                PipelineRunResult result;
                if (i < forceFailuresForLastN)
                {
                    result = PipelineRunResult.Failed;
                }
                else
                {
                    result = DetermineRunResult(failureRate, 0);
                }

                var trigger = DetermineTrigger(pipeline.Type);
                var finishTime = result == PipelineRunResult.Canceled
                    ? startTime.Add(TimeSpan.FromMinutes(_random.Next(1, Math.Max(2, (int)duration.TotalMinutes))))
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
        runs = runs.OrderByDescending(r => r.StartTime.HasValue ? r.StartTime.Value.UtcDateTime : DateTime.MinValue).ToList();

        _logger.LogInformation("Generated {Count} pipeline runs", runs.Count);
        return runs;
    }

    private double GetPipelineFailureRate(string pipelineName)
    {
        // PROBLEMATIC PIPELINES - Currently broken with high failure rates
        if (pipelineName.Contains("FireSuppression"))
            return 0.85; // Critical! Almost always failing - demonstrates urgent issue
        if (pipelineName.Contains("Database.Migrations"))
            return 0.70; // Schema conflicts causing frequent failures
        if (pipelineName.Contains("Infrastructure"))
            return 0.55; // Config problems causing many failures
        if (pipelineName.Contains("CommunicationSystems"))
            return 0.45; // Network issues
        if (pipelineName.Contains("DamageControl.Deploy"))
            return 0.40; // Deployment environment issues
        if (pipelineName.Contains("Integration.Deploy"))
            return 0.35; // Integration test failures

        // FLAKY PIPELINES - Intermittent issues
        if (pipelineName.Contains("Mobile.Android"))
            return 0.25; // Android emulator flakiness
        if (pipelineName.Contains("Mobile.iOS"))
            return 0.22; // iOS simulator issues
        if (pipelineName.Contains("SensorNetwork"))
            return 0.20; // Hardware timing issues
        if (pipelineName.Contains("Production.Release"))
            return 0.18; // Approval bottlenecks

        // NORMAL PIPELINES - Acceptable failure rate
        if (pipelineName.Contains("IncidentDetection") || pipelineName.Contains("DamageControl.CI"))
            return 0.10;
        if (pipelineName.Contains("CrewSafety") || pipelineName.Contains("HullIntegrity"))
            return 0.08;
        if (pipelineName.Contains("AlertManagement") || pipelineName.Contains("Staging"))
            return 0.07;

        // STABLE PIPELINES - Very low failure rate
        if (pipelineName.Contains("Core"))
            return 0.03; // Core is rock solid
        if (pipelineName.Contains("API.Gateway"))
            return 0.04; // Well-tested API
        if (pipelineName.Contains("ReportingDashboard"))
            return 0.05; // Stable UI
        if (pipelineName.Contains("QA.Deploy"))
            return 0.05; // QA environment is stable

        return 0.08; // Default moderate failure rate
    }

    /// <summary>
    /// Returns how many consecutive failures to force for the most recent runs.
    /// This ensures demo-friendly data where problematic pipelines clearly show issues.
    /// </summary>
    private int GetForcedConsecutiveFailures(string pipelineName)
    {
        // Critical issues - many consecutive failures
        if (pipelineName.Contains("FireSuppression"))
            return 8; // 8 consecutive failures - urgent!
        if (pipelineName.Contains("Database.Migrations"))
            return 5; // 5 consecutive failures
        if (pipelineName.Contains("Infrastructure"))
            return 4; // 4 consecutive failures
        if (pipelineName.Contains("CommunicationSystems"))
            return 3; // 3 consecutive failures

        // Moderate issues
        if (pipelineName.Contains("DamageControl.Deploy"))
            return 2;
        if (pipelineName.Contains("Integration.Deploy"))
            return 2;

        return 0; // No forced failures
    }

    private TimeSpan GetPipelineAverageDuration(PipelineType type, string pipelineName)
    {
        // Give specific pipelines characteristic durations for demo variety
        if (pipelineName.Contains("Core"))
            return TimeSpan.FromMinutes(3); // Fast, well-optimized
        if (pipelineName.Contains("API.Gateway"))
            return TimeSpan.FromMinutes(5); // Quick API tests
        if (pipelineName.Contains("Mobile"))
            return TimeSpan.FromMinutes(35); // Slow mobile builds
        if (pipelineName.Contains("Database"))
            return TimeSpan.FromMinutes(25); // Migration scripts take time
        if (pipelineName.Contains("Infrastructure"))
            return TimeSpan.FromMinutes(45); // Infrastructure provisioning
        if (pipelineName.Contains("Production.Release"))
            return TimeSpan.FromMinutes(60); // Full production deployment
        if (pipelineName.Contains("Integration"))
            return TimeSpan.FromMinutes(40); // Integration tests

        return type switch
        {
            PipelineType.Build => TimeSpan.FromMinutes(_random.Next(8, 20)),
            PipelineType.Release => TimeSpan.FromMinutes(_random.Next(15, 35)),
            _ => TimeSpan.FromMinutes(12)
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
