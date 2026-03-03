using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineSprintTrendsQuery.
/// Aggregates pipeline run metrics per sprint by mapping each run's FinishedDate
/// to the sprint whose [StartDateUtc, EndDateUtc) boundary it falls within.
/// Sprint mapping rule: run belongs to sprint S when StartDateUtc is less-than-or-equal-to
/// run.FinishedDateUtc and run.FinishedDateUtc is less-than S.EndDateUtc.
/// </summary>
public sealed class GetPipelineSprintTrendsQueryHandler
    : IQueryHandler<GetPipelineSprintTrendsQuery, GetPipelineSprintTrendsResponse>
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<GetPipelineSprintTrendsQueryHandler> _logger;

    public GetPipelineSprintTrendsQueryHandler(
        PoToolDbContext context,
        ILogger<GetPipelineSprintTrendsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async ValueTask<GetPipelineSprintTrendsResponse> Handle(
        GetPipelineSprintTrendsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SprintIds.Count == 0)
        {
            return new GetPipelineSprintTrendsResponse
            {
                Success = false,
                ErrorMessage = "At least one sprint ID is required."
            };
        }

        // 1. Load sprints ordered by start date
        var sprintIdList = query.SprintIds.Distinct().ToList();
        var sprints = await _context.Sprints
            .Where(s => sprintIdList.Contains(s.Id) && s.StartDateUtc.HasValue && s.EndDateUtc.HasValue)
            .OrderBy(s => s.StartDateUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (sprints.Count == 0)
        {
            _logger.LogWarning("No sprints with valid dates found for IDs: {SprintIds}", string.Join(",", sprintIdList));
            return new GetPipelineSprintTrendsResponse
            {
                Success = true,
                Sprints = Array.Empty<PipelineSprintMetricsDto>()
            };
        }

        // 2. Determine allowed pipeline definition IDs (filtered by product if requested)
        List<int> allowedPipelineDefIds;
        if (query.ProductIds != null && query.ProductIds.Count > 0)
        {
            allowedPipelineDefIds = await _context.PipelineDefinitions
                .Where(d => query.ProductIds.Contains(d.ProductId))
                .Select(d => d.PipelineDefinitionId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (allowedPipelineDefIds.Count == 0)
            {
                return new GetPipelineSprintTrendsResponse
                {
                    Success = true,
                    Sprints = sprints.Select(s => new PipelineSprintMetricsDto
                    {
                        SprintId = s.Id,
                        SprintName = s.Name,
                        StartUtc = s.StartUtc,
                        EndUtc = s.EndUtc
                    }).ToList()
                };
            }
        }
        else
        {
            allowedPipelineDefIds = await _context.PipelineDefinitions
                .Select(d => d.PipelineDefinitionId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        // 3. Load all runs whose FinishedDateUtc falls within the overall sprint range
        var rangeStart = sprints.Min(s => s.StartDateUtc!.Value);
        var rangeEnd = sprints.Max(s => s.EndDateUtc!.Value);

        var runs = await _context.CachedPipelineRuns
            .Where(r => allowedPipelineDefIds.Contains(r.PipelineDefinitionId)
                        && r.FinishedDateUtc.HasValue
                        && r.FinishedDateUtc.Value >= rangeStart
                        && r.FinishedDateUtc.Value < rangeEnd)
            .Select(r => new
            {
                r.PipelineDefinitionId,
                r.FinishedDateUtc,
                r.CreatedDateUtc,
                r.Result
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Pipeline sprint trends: loaded {RunCount} runs for {SprintCount} sprints", runs.Count, sprints.Count);

        // 4. Assign each run to a sprint based on FinishedDateUtc
        var sprintSlots = sprints.Select(s => (Sprint: s, Runs: new List<(int PipelineId, DateTime? Finished, DateTime? Started, string? Result)>())).ToList();

        int unmappedCount = 0;
        foreach (var run in runs)
        {
            var matched = false;
            foreach (var slot in sprintSlots)
            {
                if (run.FinishedDateUtc!.Value >= slot.Sprint.StartDateUtc!.Value &&
                    run.FinishedDateUtc.Value < slot.Sprint.EndDateUtc!.Value)
                {
                    slot.Runs.Add((run.PipelineDefinitionId, run.FinishedDateUtc, run.CreatedDateUtc, run.Result));
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                unmappedCount++;
            }
        }

        // 5. Compute per-sprint metrics
        var sprintMetrics = sprintSlots.Select(slot =>
        {
            var sprintRuns = slot.Runs;
            return ComputeSprintMetrics(slot.Sprint.Id, slot.Sprint.Name, slot.Sprint.StartUtc, slot.Sprint.EndUtc, sprintRuns);
        }).ToList();

        var totalRuns = runs.Count;
        var mappedRuns = totalRuns - unmappedCount;
        var coveragePercent = totalRuns > 0 ? (double)mappedRuns / totalRuns * 100 : 100.0;

        return new GetPipelineSprintTrendsResponse
        {
            Success = true,
            Sprints = sprintMetrics,
            UnmappedRunCount = unmappedCount,
            SprintCoveragePercent = Math.Round(coveragePercent, 1)
        };
    }

    private static PipelineSprintMetricsDto ComputeSprintMetrics(
        int sprintId,
        string sprintName,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        List<(int PipelineId, DateTime? Finished, DateTime? Started, string? Result)> runs)
    {
        var dto = new PipelineSprintMetricsDto
        {
            SprintId = sprintId,
            SprintName = sprintName,
            StartUtc = startUtc,
            EndUtc = endUtc,
            TotalRuns = runs.Count
        };

        if (runs.Count == 0)
        {
            return dto;
        }

        // Completed runs (exclude Unknown and None / null result)
        var completed = runs.Where(r => IsCompletedResult(r.Result)).ToList();
        dto.CompletedRuns = completed.Count;

        if (completed.Count > 0)
        {
            var successful = completed.Count(r => IsSuccessResult(r.Result));
            var failed = completed.Count(r => IsFailedResult(r.Result));
            dto.SuccessRate = (double)successful / completed.Count * 100;
            dto.FailureRate = (double)failed / completed.Count * 100;
        }

        // Duration (only runs with both start and finish)
        var durations = runs
            .Where(r => r.Started.HasValue && r.Finished.HasValue && r.Finished.Value > r.Started.Value)
            .Select(r => (r.Finished!.Value - r.Started!.Value).TotalHours)
            .OrderBy(x => x)
            .ToList();

        if (durations.Count > 0)
        {
            dto.MedianDurationHours = Median(durations);
            dto.P75DurationHours = Percentile(durations, 75);
        }

        // MTTR — per-pipeline: time from failure start to next success start
        var mttrValues = new List<double>();
        foreach (var pipelineGroup in runs.GroupBy(r => r.PipelineId))
        {
            var ordered = pipelineGroup
                .Where(r => r.Started.HasValue)
                .OrderBy(r => r.Started!.Value)
                .ToList();

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                if (!IsFailedResult(ordered[i].Result))
                    continue;

                for (int j = i + 1; j < ordered.Count; j++)
                {
                    if (IsSuccessResult(ordered[j].Result) && ordered[j].Started.HasValue)
                    {
                        var mttr = (ordered[j].Started!.Value - ordered[i].Started!.Value).TotalHours;
                        if (mttr > 0)
                            mttrValues.Add(mttr);
                        break;
                    }
                }
            }
        }

        if (mttrValues.Count > 0)
        {
            var sortedMttr = mttrValues.OrderBy(x => x).ToList();
            dto.MedianMttrHours = Median(sortedMttr);
            dto.P75MttrHours = Percentile(sortedMttr, 75);
        }

        // Time to first failure detection (duration of failed runs)
        var failureTimes = runs
            .Where(r => IsFailedResult(r.Result) && r.Started.HasValue && r.Finished.HasValue && r.Finished.Value > r.Started.Value)
            .Select(r => (r.Finished!.Value - r.Started!.Value).TotalHours)
            .OrderBy(x => x)
            .ToList();

        if (failureTimes.Count > 0)
        {
            dto.MedianTimeToFirstFailureHours = Median(failureTimes);
        }

        // Flakiness: % of pipelines with both success and failure
        var byPipeline = runs.GroupBy(r => r.PipelineId).ToList();
        if (byPipeline.Count > 0)
        {
            var flakyCount = byPipeline.Count(g =>
                g.Any(r => IsSuccessResult(r.Result)) && g.Any(r => IsFailedResult(r.Result)));
            dto.FlakinessRate = (double)flakyCount / byPipeline.Count * 100;
        }

        return dto;
    }

    private static bool IsCompletedResult(string? result) =>
        !string.IsNullOrEmpty(result) &&
        !result.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
        !result.Equals("None", StringComparison.OrdinalIgnoreCase);

    private static bool IsSuccessResult(string? result) =>
        result != null && result.Equals("Succeeded", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailedResult(string? result) =>
        result != null && (
            result.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
            result.Equals("PartiallySucceeded", StringComparison.OrdinalIgnoreCase));

    private static double Median(List<double> sorted)
    {
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    private static double Percentile(List<double> sorted, int p)
    {
        double rank = (p / 100.0) * (sorted.Count - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
    }
}
