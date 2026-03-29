using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines.Filters;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineMetricsQuery.
/// Calculates and returns aggregated metrics for pipelines, optionally filtered by products.
/// Uses the cached analytical pipeline read provider.
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
        var filter = query.EffectiveFilter;
        if (filter.PipelineIds.Count == 0 || filter.RepositoryScope.Count == 0)
        {
            return Enumerable.Empty<PipelineMetricsDto>();
        }

        var allRuns = await _pipelineReadProvider.GetRunsForPipelinesAsync(
            filter.PipelineIds,
            branchName: null,
            minStartTime: filter.RangeStartUtc,
            maxStartTime: filter.RangeEndUtc,
            branchScope: filter.BranchScope,
            top: 100,
            cancellationToken);
        var scopedRuns = PipelineFiltering.ApplyRunScope(allRuns, filter);

        // Group runs by pipeline
        var runsByPipeline = scopedRuns.GroupBy(r => r.PipelineId).ToDictionary(g => g.Key, g => g.ToList());

        // Get pipeline information for those that have runs
        var pipelinesWithRuns = await _pipelineReadProvider.GetByIdsAsync(runsByPipeline.Keys, cancellationToken);
        var pipelineDict = pipelinesWithRuns.ToDictionary(p => p.Id);

        var metrics = new List<PipelineMetricsDto>();

        foreach (var (pipelineId, runs) in runsByPipeline)
        {
            if (runs.Count == 0)
            {
                continue;
            }

            // Get pipeline info
            if (!pipelineDict.TryGetValue(pipelineId, out var pipeline))
            {
                // Pipeline not found, skip
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
