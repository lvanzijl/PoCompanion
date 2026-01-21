using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineMetricsQuery.
/// Calculates and returns aggregated metrics for pipelines, optionally filtered by products.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetPipelineMetricsQueryHandler : IQueryHandler<GetPipelineMetricsQuery, IEnumerable<PipelineMetricsDto>>
{
    private readonly IPipelineReadProvider _pipelineReadProvider;

    public GetPipelineMetricsQueryHandler(IPipelineReadProvider pipelineReadProvider)
    {
        _pipelineReadProvider = pipelineReadProvider;
    }

    public async ValueTask<IEnumerable<PipelineMetricsDto>> Handle(
        GetPipelineMetricsQuery query,
        CancellationToken cancellationToken)
    {
        // Live-only mode: use injected provider directly
        
        // Get all pipelines
        var pipelines = await _pipelineReadProvider.GetAllAsync(cancellationToken);
        
        // Filter by product IDs if specified
        if (query.ProductIds != null && query.ProductIds.Count > 0)
        {
            // Get pipeline definitions for the specified products
            var allowedPipelineIds = new HashSet<int>();
            foreach (var productId in query.ProductIds)
            {
                var definitions = await _pipelineReadProvider.GetDefinitionsByProductIdAsync(productId, cancellationToken);
                foreach (var def in definitions)
                {
                    allowedPipelineIds.Add(def.PipelineDefinitionId);
                }
            }
            
            // Filter pipelines to only those in the allowed set
            pipelines = pipelines.Where(p => allowedPipelineIds.Contains(p.Id)).ToList();
        }

        var allRuns = await _pipelineReadProvider.GetAllRunsAsync(cancellationToken);
        
        // Filter runs to last 6 months only
        var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-6);
        var filteredRuns = allRuns.Where(r => r.StartTime.HasValue && r.StartTime.Value >= sixMonthsAgo).ToList();

        var runsByPipeline = filteredRuns.GroupBy(r => r.PipelineId).ToDictionary(g => g.Key, g => g.ToList());

        var metrics = new List<PipelineMetricsDto>();

        foreach (var pipeline in pipelines)
        {
            if (!runsByPipeline.TryGetValue(pipeline.Id, out var runs) || runs.Count == 0)
            {
                // Skip pipelines with no runs in the last 6 months
                continue;
            }

            var successfulRuns = runs.Count(r => r.Result == PipelineRunResult.Succeeded);
            var failedRuns = runs.Count(r => r.Result == PipelineRunResult.Failed);
            var partiallySucceededRuns = runs.Count(r => r.Result == PipelineRunResult.PartiallySucceeded);
            var canceledRuns = runs.Count(r => r.Result == PipelineRunResult.Canceled);
            var failureRate = runs.Count > 0 ? (double)failedRuns / runs.Count : 0;

            var durations = runs.Where(r => r.Duration.HasValue).Select(r => r.Duration!.Value).ToList();
            TimeSpan? avgDuration = null;
            TimeSpan? minDuration = null;
            TimeSpan? maxDuration = null;
            double? durationVariance = null;

            if (durations.Count > 0)
            {
                avgDuration = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks));
                minDuration = durations.Min();
                maxDuration = durations.Max();

                if (durations.Count > 1)
                {
                    var avgTicks = durations.Average(d => d.Ticks);
                    durationVariance = durations.Sum(d => Math.Pow(d.Ticks - avgTicks, 2)) / (durations.Count - 1);
                }
            }

            var orderedRuns = runs.OrderByDescending(r => r.StartTime).ToList();
            var lastRun = orderedRuns.FirstOrDefault();

            // Count consecutive failures
            var consecutiveFailures = 0;
            foreach (var run in orderedRuns)
            {
                if (run.Result == PipelineRunResult.Failed)
                {
                    consecutiveFailures++;
                }
                else
                {
                    break;
                }
            }

            metrics.Add(new PipelineMetricsDto(
                PipelineId: pipeline.Id,
                PipelineName: pipeline.Name,
                Type: pipeline.Type,
                TotalRuns: runs.Count,
                SuccessfulRuns: successfulRuns,
                FailedRuns: failedRuns,
                PartiallySucceededRuns: partiallySucceededRuns,
                CanceledRuns: canceledRuns,
                FailureRate: failureRate,
                AverageDuration: avgDuration,
                MinDuration: minDuration,
                MaxDuration: maxDuration,
                DurationVariance: durationVariance,
                LastRunResult: lastRun?.Result,
                LastRunTime: lastRun?.StartTime,
                ConsecutiveFailures: consecutiveFailures
            ));
        }

        return metrics;
    }
}
