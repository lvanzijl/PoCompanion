using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineInsightsQuery.
/// Computes pipeline health metrics per product for a single sprint using only cached data.
///
/// Sprint mapping rule: a run belongs to the sprint when its FinishedDateUtc falls
/// within [SprintStartDateUtc, SprintEndDateUtc).
///
/// Previous sprint: the sprint immediately preceding the selected sprint in the same
/// team's ordered sprint list (by StartDateUtc ascending).
/// </summary>
public sealed class GetPipelineInsightsQueryHandler
    : IQueryHandler<GetPipelineInsightsQuery, PipelineInsightsDto>
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<GetPipelineInsightsQueryHandler> _logger;

    public GetPipelineInsightsQueryHandler(
        PoToolDbContext context,
        ILogger<GetPipelineInsightsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async ValueTask<PipelineInsightsDto> Handle(
        GetPipelineInsightsQuery query,
        CancellationToken cancellationToken)
    {
        // ── 1. Load the selected sprint ───────────────────────────────────────
        var sprint = await _context.Sprints
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.SprintId, cancellationToken);

        if (sprint is null || !sprint.StartDateUtc.HasValue || !sprint.EndDateUtc.HasValue)
        {
            _logger.LogWarning("Sprint {SprintId} not found or has no date boundaries", query.SprintId);
            return EmptyResult(query.SprintId, "Unknown sprint");
        }

        // ── 2. Find the previous sprint (same team, immediately preceding) ────
        var previousSprint = await _context.Sprints
            .AsNoTracking()
            .Where(s => s.TeamId == sprint.TeamId
                        && s.StartDateUtc.HasValue
                        && s.StartDateUtc.Value < sprint.StartDateUtc.Value)
            .OrderByDescending(s => s.StartDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // ── 3. Load all products belonging to the active PO ───────────────────
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.ProductOwnerId == query.ProductOwnerId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            _logger.LogDebug("No products found for ProductOwner {ProductOwnerId}", query.ProductOwnerId);
            return new PipelineInsightsDto
            {
                SprintId = sprint.Id,
                SprintName = sprint.Name,
                PreviousSprintId = previousSprint?.Id,
                PreviousSprintName = previousSprint?.Name
            };
        }

        var productIds = products.Select(p => p.Id).ToList();

        // ── 4. Load pipeline definitions per product ──────────────────────────
        // Key: PipelineDefinitionEntity.Id (DB PK) → (ProductId, Name)
        var pipelineDefs = await _context.PipelineDefinitions
            .AsNoTracking()
            .Where(d => productIds.Contains(d.ProductId))
            .Select(d => new PipelineDefRecord(d.Id, d.ProductId, d.Name))
            .ToListAsync(cancellationToken);

        if (pipelineDefs.Count == 0)
        {
            _logger.LogDebug("No pipeline definitions found for PO {ProductOwnerId}", query.ProductOwnerId);
            return BuildResultWithEmptyProducts(sprint, previousSprint, products);
        }

        var allDefIds = pipelineDefs.Select(d => d.Id).ToList();

        // ── 5. Load runs in the selected sprint window ────────────────────────
        var sprintStart = sprint.StartDateUtc!.Value;
        var sprintEnd   = sprint.EndDateUtc!.Value;

        var currentRuns = await LoadRunsAsync(allDefIds, sprintStart, sprintEnd, cancellationToken);

        // ── 6. Load runs in the previous sprint window (for delta) ────────────
        List<RunRecord> previousRuns = new();
        if (previousSprint?.StartDateUtc.HasValue == true && previousSprint.EndDateUtc.HasValue)
        {
            previousRuns = await LoadRunsAsync(
                allDefIds,
                previousSprint.StartDateUtc!.Value,
                previousSprint.EndDateUtc!.Value,
                cancellationToken);
        }

        _logger.LogDebug(
            "PipelineInsights: sprint={SprintId} runs={Current}, prevSprint={PrevSprintId} runs={Prev}",
            sprint.Id, currentRuns.Count, previousSprint?.Id, previousRuns.Count);

        // ── 7. Build lookup maps ──────────────────────────────────────────────
        var defByDbId    = pipelineDefs.ToDictionary(d => d.Id);
        var productByDbId = products.ToDictionary(p => p.Id, p => p.Name);

        // ── 8. Compute per-product sections ──────────────────────────────────
        var productSections = new List<ProductPipelineInsightsDto>(products.Count);
        var globalCurrentRunsByPipeline  = new Dictionary<int, List<RunRecord>>();
        var globalPreviousRunsByPipeline = new Dictionary<int, List<RunRecord>>();

        foreach (var product in products)
        {
            var productDefIds = pipelineDefs
                .Where(d => d.ProductId == product.Id)
                .Select(d => d.Id)
                .ToHashSet();

            var productCurrentRuns  = currentRuns .Where(r => productDefIds.Contains(r.DefId)).ToList();
            var productPreviousRuns = previousRuns.Where(r => productDefIds.Contains(r.DefId)).ToList();

            // Accumulate for global top-3
            foreach (var defId in productDefIds)
            {
                var cruns = productCurrentRuns .Where(r => r.DefId == defId).ToList();
                var pruns = productPreviousRuns.Where(r => r.DefId == defId).ToList();

                if (!globalCurrentRunsByPipeline.ContainsKey(defId))
                    globalCurrentRunsByPipeline[defId] = cruns;
                else
                    globalCurrentRunsByPipeline[defId].AddRange(cruns);

                if (!globalPreviousRunsByPipeline.ContainsKey(defId))
                    globalPreviousRunsByPipeline[defId] = pruns;
                else
                    globalPreviousRunsByPipeline[defId].AddRange(pruns);
            }

            var section = BuildProductSection(
                product.Id,
                product.Name,
                productCurrentRuns,
                productPreviousRuns,
                productDefIds,
                defByDbId,
                productByDbId,
                query.IncludePartiallySucceeded,
                query.IncludeCanceled,
                hasPreviousSprint: previousSprint is not null,
                sprintStart: sprintStart,
                sprintEnd:   sprintEnd);

            productSections.Add(section);
        }

        // ── 9. Compute global top-3 ───────────────────────────────────────────
        var hasPrevious = previousSprint is not null && previousRuns.Count > 0;

        var globalTop3 = BuildTop3(
            globalCurrentRunsByPipeline,
            globalPreviousRunsByPipeline,
            defByDbId,
            productByDbId,
            query.IncludePartiallySucceeded,
            query.IncludeCanceled,
            hasPreviousSprint: previousSprint is not null);

        // ── 10. Compute global summary chips ─────────────────────────────────
        var (globalCompleted, globalFailed, globalWarning, globalSucceeded) =
            ClassifyRuns(currentRuns, query.IncludePartiallySucceeded, query.IncludeCanceled);

        double globalFailureRate = globalCompleted > 0
            ? (double)globalFailed / globalCompleted * 100.0 : 0.0;
        double globalWarningRate = globalCompleted > 0
            ? (double)globalWarning / globalCompleted * 100.0 : 0.0;

        var globalDurations = GetDurationsMinutes(currentRuns);
        double? globalP90 = globalDurations.Count >= 3
            ? Percentile(globalDurations, 90) : null;

        return new PipelineInsightsDto
        {
            SprintId              = sprint.Id,
            SprintName            = sprint.Name,
            SprintStart           = sprint.StartUtc,
            SprintEnd             = sprint.EndUtc,
            PreviousSprintId      = previousSprint?.Id,
            PreviousSprintName    = previousSprint?.Name,
            TotalBuilds           = currentRuns.Count,
            CompletedBuilds       = globalCompleted,
            FailedBuilds          = globalFailed,
            FailureRate           = Math.Round(globalFailureRate, 1),
            WarningBuilds         = globalWarning,
            WarningRate           = Math.Round(globalWarningRate, 1),
            P90DurationMinutes    = globalP90.HasValue ? Math.Round(globalP90.Value, 1) : null,
            GlobalTop3InTrouble   = globalTop3,
            Products              = productSections
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<RunRecord>> LoadRunsAsync(
        List<int> defIds,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken)
    {
        return await _context.CachedPipelineRuns
            .AsNoTracking()
            .Where(r => defIds.Contains(r.PipelineDefinitionId)
                        && r.FinishedDateUtc.HasValue
                        && r.FinishedDateUtc.Value >= rangeStart
                        && r.FinishedDateUtc.Value < rangeEnd)
            .Select(r => new RunRecord(
                r.Id,
                r.TfsRunId,
                r.PipelineDefinitionId,
                r.Result,
                r.RunName,
                r.CreatedDateUtc,
                r.FinishedDateUtc,
                r.CreatedDate,
                r.FinishedDate,
                r.SourceBranch,
                r.Url))
            .ToListAsync(cancellationToken);
    }

    private static ProductPipelineInsightsDto BuildProductSection(
        int productId,
        string productName,
        List<RunRecord> currentRuns,
        List<RunRecord> previousRuns,
        HashSet<int> productDefIds,
        Dictionary<int, PipelineDefRecord> defByDbId,
        Dictionary<int, string> productByDbId,
        bool includePartial,
        bool includeCanceled,
        bool hasPreviousSprint,
        DateTime sprintStart,
        DateTime sprintEnd)
    {
        if (currentRuns.Count == 0)
        {
            return new ProductPipelineInsightsDto
            {
                ProductId   = productId,
                ProductName = productName,
                HasData     = false
            };
        }

        var (completed, failed, warning, succeeded) =
            ClassifyRuns(currentRuns, includePartial, includeCanceled);

        double failureRate = completed > 0 ? (double)failed    / completed * 100.0 : 0.0;
        double warningRate = completed > 0 ? (double)warning   / completed * 100.0 : 0.0;
        double successRate = completed > 0 ? (double)succeeded / completed * 100.0 : 0.0;

        var durations = GetDurationsMinutes(currentRuns);
        double? median = durations.Count > 0 ? Median(durations) : null;
        double? p90    = durations.Count >= 3 ? Percentile(durations, 90) : null;

        // Group runs by pipeline for top-3 and breakdown
        var currentByDef  = currentRuns .GroupBy(r => r.DefId).ToDictionary(g => g.Key, g => g.ToList());
        var previousByDef = previousRuns.GroupBy(r => r.DefId).ToDictionary(g => g.Key, g => g.ToList());

        // Only include definitions that belong to this product
        var filteredCurrent  = currentByDef .Where(kvp => productDefIds.Contains(kvp.Key)).ToDictionary(k => k.Key, k => k.Value);
        var filteredPrevious = previousByDef.Where(kvp => productDefIds.Contains(kvp.Key)).ToDictionary(k => k.Key, k => k.Value);

        var top3 = BuildTop3(
            filteredCurrent,
            filteredPrevious,
            defByDbId,
            productByDbId,
            includePartial,
            includeCanceled,
            hasPreviousSprint);

        // ── Build scatter points (all runs, ordered by start time) ────────────
        var scatterPoints = BuildScatterPoints(currentRuns, defByDbId);

        // ── Build per-pipeline breakdown table ────────────────────────────────
        var sprintMid = sprintStart + TimeSpan.FromTicks((sprintEnd - sprintStart).Ticks / 2);
        var breakdown = BuildPipelineBreakdown(
            filteredCurrent,
            filteredPrevious,
            defByDbId,
            includePartial,
            includeCanceled,
            hasPreviousSprint,
            sprintStart,
            sprintMid,
            sprintEnd);

        return new ProductPipelineInsightsDto
        {
            ProductId              = productId,
            ProductName            = productName,
            HasData                = true,
            TotalBuilds            = currentRuns.Count,
            CompletedBuilds        = completed,
            FailedBuilds           = failed,
            FailureRate            = Math.Round(failureRate, 1),
            WarningBuilds          = warning,
            WarningRate            = Math.Round(warningRate, 1),
            SucceededBuilds        = succeeded,
            SuccessRate            = Math.Round(successRate, 1),
            MedianDurationMinutes  = median.HasValue ? Math.Round(median.Value, 1) : null,
            P90DurationMinutes     = p90.HasValue    ? Math.Round(p90.Value,    1) : null,
            Top3InTrouble          = top3,
            ScatterPoints          = scatterPoints,
            PipelineBreakdown      = breakdown
        };
    }

    private static IReadOnlyList<PipelineTroubleEntryDto> BuildTop3(
        Dictionary<int, List<RunRecord>> currentByDef,
        Dictionary<int, List<RunRecord>> previousByDef,
        Dictionary<int, PipelineDefRecord> defByDbId,
        Dictionary<int, string> productByDbId,
        bool includePartial,
        bool includeCanceled,
        bool hasPreviousSprint)
    {
        var candidates = new List<(int DefId, int Completed, int Failed, double FailureRate, int Warning, double WarningRate, double? PrevFailureRate, double? PrevWarningRate)>();

        foreach (var (defId, runs) in currentByDef)
        {
            var (completed, failed, warning, _) = ClassifyRuns(runs, includePartial, includeCanceled);
            if (completed == 0) continue;

            double failureRate = (double)failed  / completed * 100.0;
            double warningRate = (double)warning / completed * 100.0;

            double? prevFailureRate = null;
            double? prevWarningRate = null;

            if (hasPreviousSprint && previousByDef.TryGetValue(defId, out var prevRuns) && prevRuns.Count > 0)
            {
                var (prevCompleted, prevFailed, prevWarning, _) = ClassifyRuns(prevRuns, includePartial, includeCanceled);
                if (prevCompleted > 0)
                {
                    prevFailureRate = (double)prevFailed  / prevCompleted * 100.0;
                    prevWarningRate = (double)prevWarning / prevCompleted * 100.0;
                }
            }

            candidates.Add((defId, completed, failed, failureRate, warning, warningRate, prevFailureRate, prevWarningRate));
        }

        // Sort: failure rate desc, completed desc, name asc
        var sorted = candidates
            .OrderByDescending(c => c.FailureRate)
            .ThenByDescending(c => c.Completed)
            .ThenBy(c => defByDbId.TryGetValue(c.DefId, out var def) ? def.Name : string.Empty)
            .Take(3)
            .ToList();

        return sorted.Select((c, idx) =>
        {
            defByDbId.TryGetValue(c.DefId, out var def);
            int productId   = def is not null ? def.ProductId : 0;
            string pipeName = def is not null ? def.Name      : $"Pipeline {c.DefId}";
            productByDbId.TryGetValue(productId, out var productName);

            return new PipelineTroubleEntryDto
            {
                Rank                  = idx + 1,
                PipelineDefinitionId  = c.DefId,
                PipelineName          = pipeName,
                ProductId             = productId,
                ProductName           = productName ?? string.Empty,
                CompletedBuilds       = c.Completed,
                FailedBuilds          = c.Failed,
                FailureRate           = Math.Round(c.FailureRate,  1),
                WarningBuilds         = c.Warning,
                WarningRate           = Math.Round(c.WarningRate,  1),
                DeltaFailureRate      = c.PrevFailureRate.HasValue
                    ? Math.Round(c.FailureRate - c.PrevFailureRate.Value, 1)
                    : null,
                DeltaWarningRate      = c.PrevWarningRate.HasValue
                    ? Math.Round(c.WarningRate - c.PrevWarningRate.Value, 1)
                    : null
            };
        }).ToList();
    }

    private static IReadOnlyList<PipelineScatterPointDto> BuildScatterPoints(
        IEnumerable<RunRecord> runs,
        Dictionary<int, PipelineDefRecord> defByDbId)
    {
        // Filter: only runs that have a start time (CreatedDateOffset) for X-axis positioning.
        // DurationMinutes is computed from the UTC fields and is nullable:
        //   null = run has no start or finish UTC → rendered at Y=0 in scatter (no duration data).
        return runs
            .Where(r => r.CreatedDateOffset.HasValue)
            .OrderBy(r => r.CreatedDateOffset)
            .Select(r =>
            {
                defByDbId.TryGetValue(r.DefId, out var def);
                double? durationMinutes = null;
                if (r.CreatedDateUtc.HasValue && r.FinishedDateUtc.HasValue
                    && r.FinishedDateUtc.Value > r.CreatedDateUtc.Value)
                {
                    durationMinutes = Math.Round(
                        (r.FinishedDateUtc.Value - r.CreatedDateUtc.Value).TotalMinutes, 2);
                }

                return new PipelineScatterPointDto
                {
                    Id                   = r.DbId,
                    TfsRunId             = r.TfsRunId,
                    PipelineDefinitionId = r.DefId,
                    PipelineName         = def?.Name ?? $"Pipeline {r.DefId}",
                    BuildNumber          = r.RunName,
                    Result               = r.Result,
                    StartTime            = r.CreatedDateOffset,
                    FinishTime           = r.FinishedDateOffset,
                    DurationMinutes      = durationMinutes,
                    Branch               = r.SourceBranch,
                    Url                  = r.Url
                };
            })
            .ToList();
    }

    /// <summary>
    /// Builds the per-pipeline breakdown table for a product section.
    /// Ordered by failure rate descending (worst pipeline first).
    /// </summary>
    private static IReadOnlyList<PipelineBreakdownEntryDto> BuildPipelineBreakdown(
        Dictionary<int, List<RunRecord>> currentByDef,
        Dictionary<int, List<RunRecord>> previousByDef,
        Dictionary<int, PipelineDefRecord> defByDbId,
        bool includePartial,
        bool includeCanceled,
        bool hasPreviousSprint,
        DateTime sprintStart,
        DateTime sprintMid,
        DateTime sprintEnd)
    {
        var entries = new List<PipelineBreakdownEntryDto>(currentByDef.Count);

        foreach (var (defId, runs) in currentByDef)
        {
            defByDbId.TryGetValue(defId, out var def);

            var (completed, failed, warning, succeeded) =
                ClassifyRuns(runs, includePartial, includeCanceled);

            double failureRate = completed > 0 ? (double)failed    / completed * 100.0 : 0.0;
            double warningRate = completed > 0 ? (double)warning   / completed * 100.0 : 0.0;
            double successRate = completed > 0 ? (double)succeeded / completed * 100.0 : 0.0;

            var durations = GetDurationsMinutes(runs);

            // ── Delta vs. previous sprint ──────────────────────────────────
            double? deltaFailureRate = null;
            if (hasPreviousSprint && previousByDef.TryGetValue(defId, out var prevRuns) && prevRuns.Count > 0)
            {
                var (prevCompleted, prevFailed, _, _) = ClassifyRuns(prevRuns, includePartial, includeCanceled);
                if (prevCompleted > 0)
                {
                    double prevFailureRate = (double)prevFailed / prevCompleted * 100.0;
                    deltaFailureRate = Math.Round(failureRate - prevFailureRate, 1);
                }
            }

            // ── Half-sprint trend ──────────────────────────────────────────
            // First half:  FinishedDateUtc in [sprintStart, sprintMid)
            // Second half: FinishedDateUtc in [sprintMid,   sprintEnd)
            var firstHalf  = runs.Where(r => r.FinishedDateUtc.HasValue
                                             && r.FinishedDateUtc.Value >= sprintStart
                                             && r.FinishedDateUtc.Value <  sprintMid).ToList();
            var secondHalf = runs.Where(r => r.FinishedDateUtc.HasValue
                                             && r.FinishedDateUtc.Value >= sprintMid
                                             && r.FinishedDateUtc.Value <  sprintEnd).ToList();

            var (fhCompleted, fhFailed, _, _) = ClassifyRuns(firstHalf,  includePartial, includeCanceled);
            var (shCompleted, shFailed, _, _) = ClassifyRuns(secondHalf, includePartial, includeCanceled);

            double? firstHalfFailureRate  = null;
            double? secondHalfFailureRate = null;
            var trend = PipelineHalfSprintTrend.Insufficient;

            if (fhCompleted >= 2 && shCompleted >= 2)
            {
                firstHalfFailureRate  = Math.Round((double)fhFailed / fhCompleted * 100.0, 1);
                secondHalfFailureRate = Math.Round((double)shFailed / shCompleted * 100.0, 1);
                double diff = secondHalfFailureRate.Value - firstHalfFailureRate.Value;

                trend = diff <= -10.0 ? PipelineHalfSprintTrend.Improving
                      : diff >=  10.0 ? PipelineHalfSprintTrend.Degrading
                      : PipelineHalfSprintTrend.Stable;
            }

            entries.Add(new PipelineBreakdownEntryDto
            {
                PipelineDefinitionId  = defId,
                PipelineName          = def?.Name ?? $"Pipeline {defId}",
                TotalRuns             = runs.Count,
                CompletedRuns         = completed,
                SucceededRuns         = succeeded,
                FailedRuns            = failed,
                WarningRuns           = warning,
                SuccessRate           = Math.Round(successRate,  1),
                FailureRate           = Math.Round(failureRate,  1),
                WarningRate           = Math.Round(warningRate,  1),
                MedianDurationMinutes = durations.Count > 0    ? Math.Round(Median(durations),              1) : null,
                P90DurationMinutes    = durations.Count >= 3   ? Math.Round(Percentile(durations, 90),      1) : null,
                DeltaFailureRate      = deltaFailureRate,
                HalfSprintTrend       = trend,
                FirstHalfFailureRate  = firstHalfFailureRate,
                SecondHalfFailureRate = secondHalfFailureRate
            });
        }

        // Order: failure rate descending, then pipeline name ascending
        return entries
            .OrderByDescending(e => e.FailureRate)
            .ThenBy(e => e.PipelineName)
            .ToList();
    }

    /// <summary>
    /// Classifies runs according to the include-partial and include-canceled toggles.
    /// Returns (completed, failed, warning, succeeded) counts.
    /// </summary>
    private static (int Completed, int Failed, int Warning, int Succeeded) ClassifyRuns(
        IEnumerable<RunRecord> runs,
        bool includePartial,
        bool includeCanceled)
    {
        int completed = 0, failed = 0, warning = 0, succeeded = 0;

        foreach (var run in runs)
        {
            var result = run.Result;

            if (string.IsNullOrEmpty(result)
                || string.Equals(result, "Unknown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result, "None",    StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(result, "Canceled", StringComparison.OrdinalIgnoreCase))
            {
                if (includeCanceled) completed++;
                continue;
            }

            if (string.Equals(result, "PartiallySucceeded", StringComparison.OrdinalIgnoreCase))
            {
                if (includePartial)
                {
                    completed++;
                    warning++;
                }
                continue;
            }

            if (string.Equals(result, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                completed++;
                failed++;
                continue;
            }

            if (string.Equals(result, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                completed++;
                succeeded++;
            }
        }

        return (completed, failed, warning, succeeded);
    }

    private static List<double> GetDurationsMinutes(IEnumerable<RunRecord> runs)
    {
        return runs
            .Where(r => r.CreatedDateUtc.HasValue
                        && r.FinishedDateUtc.HasValue
                        && r.FinishedDateUtc.Value > r.CreatedDateUtc.Value)
            .Select(r => (r.FinishedDateUtc!.Value - r.CreatedDateUtc!.Value).TotalMinutes)
            .OrderBy(x => x)
            .ToList();
    }

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

    private static PipelineInsightsDto EmptyResult(int sprintId, string sprintName)
        => new()
        {
            SprintId   = sprintId,
            SprintName = sprintName
        };

    private static PipelineInsightsDto BuildResultWithEmptyProducts(
        SprintEntity sprint,
        SprintEntity? previousSprint,
        IReadOnlyList<ProductEntity> products)
        => new()
        {
            SprintId           = sprint.Id,
            SprintName         = sprint.Name,
            PreviousSprintId   = previousSprint?.Id,
            PreviousSprintName = previousSprint?.Name,
            Products           = products.Select(p => new ProductPipelineInsightsDto
            {
                ProductId   = p.Id,
                ProductName = p.Name,
                HasData     = false
            }).ToList()
        };

    // ── Projection record (avoids materialising full entity) ─────────────
    private sealed record RunRecord(
        int       DbId,
        int       TfsRunId,
        int       DefId,
        string?   Result,
        string?   RunName,
        DateTime? CreatedDateUtc,
        DateTime? FinishedDateUtc,
        DateTimeOffset? CreatedDateOffset,
        DateTimeOffset? FinishedDateOffset,
        string?   SourceBranch,
        string?   Url);

    private sealed record PipelineDefRecord(int Id, int ProductId, string Name);
}
