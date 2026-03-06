using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Core.PullRequests.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPrDeliveryInsightsQuery.
/// Classifies PRs by linked work items and produces Epic/Feature aggregations.
///
/// Classification rules (priority order):
///   DeliveryMapped — any linked work item traces to a Feature or Epic in the hierarchy.
///   Bug            — a linked Bug with no Feature/Epic ancestor.
///   Disturbance    — a linked PBI without a Feature parent.
///   Unmapped       — no usable work item link.
///
/// Scope rules:
///   - When TeamId is supplied, PRs are scoped to products linked to that team via ProductTeamLinks.
///   - When SprintId is supplied, FromDate/ToDate override the query parameter date range.
///   - Date range filters by PR.CreatedDateUtc.
/// </summary>
public sealed class GetPrDeliveryInsightsQueryHandler
    : IQueryHandler<GetPrDeliveryInsightsQuery, PrDeliveryInsightsDto>
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<GetPrDeliveryInsightsQueryHandler> _logger;

    private const string CategoryDeliveryMapped = "DeliveryMapped";
    private const string CategoryBug            = "Bug";
    private const string CategoryDisturbance    = "Disturbance";
    private const string CategoryUnmapped       = "Unmapped";

    public GetPrDeliveryInsightsQueryHandler(
        PoToolDbContext context,
        ILogger<GetPrDeliveryInsightsQueryHandler> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async ValueTask<PrDeliveryInsightsDto> Handle(
        GetPrDeliveryInsightsQuery query,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // ── 1. Resolve team name ───────────────────────────────────────────────
        string? teamName = null;
        if (query.TeamId.HasValue)
        {
            teamName = await _context.Teams
                .AsNoTracking()
                .Where(t => t.Id == query.TeamId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // ── 2. Resolve sprint boundaries ──────────────────────────────────────
        DateTimeOffset fromDate = query.FromDate;
        DateTimeOffset toDate   = query.ToDate;
        string? sprintName      = null;

        if (query.SprintId.HasValue)
        {
            var sprint = await _context.Sprints
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == query.SprintId.Value, cancellationToken);

            if (sprint != null)
            {
                fromDate   = sprint.StartDateUtc.HasValue
                    ? new DateTimeOffset(sprint.StartDateUtc.Value, TimeSpan.Zero)
                    : fromDate;
                toDate     = sprint.EndDateUtc.HasValue
                    ? new DateTimeOffset(sprint.EndDateUtc.Value, TimeSpan.Zero)
                    : toDate;
                sprintName = sprint.Name;
            }
        }

        // ── 3. Resolve allowed repository names (team-scoped) ─────────────────
        List<string>? allowedRepositoryNames = null;

        if (query.TeamId.HasValue)
        {
            var linkedProductIds = await _context.ProductTeamLinks
                .AsNoTracking()
                .Where(l => l.TeamId == query.TeamId.Value)
                .Select(l => l.ProductId)
                .ToListAsync(cancellationToken);

            if (linkedProductIds.Count > 0)
            {
                allowedRepositoryNames = await _context.Repositories
                    .AsNoTracking()
                    .Where(r => linkedProductIds.Contains(r.ProductId))
                    .Select(r => r.Name)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }
            else
            {
                _logger.LogDebug(
                    "PR Delivery Insights: team {TeamId} has no linked products; returning empty",
                    query.TeamId.Value);
                return EmptyResult(query, teamName, sprintName, fromDate, toDate);
            }

            if (allowedRepositoryNames.Count == 0)
            {
                _logger.LogDebug(
                    "PR Delivery Insights: team {TeamId} has products but no repository configurations",
                    query.TeamId.Value);
                return EmptyResult(query, teamName, sprintName, fromDate, toDate);
            }
        }

        // ── 4. Load PRs in date range ──────────────────────────────────────────
        var fromUtc = fromDate.UtcDateTime;
        var toUtc   = toDate.UtcDateTime;

        var prQuery = _context.PullRequests
            .AsNoTracking()
            .Where(pr => pr.CreatedDateUtc >= fromUtc && pr.CreatedDateUtc <= toUtc);

        if (allowedRepositoryNames != null)
            prQuery = prQuery.Where(pr => allowedRepositoryNames.Contains(pr.RepositoryName));

        var prs = await prQuery.ToListAsync(cancellationToken);

        if (prs.Count == 0)
        {
            _logger.LogDebug("PR Delivery Insights: no PRs found in date range [{From}, {To}]", fromUtc, toUtc);
            return EmptyResult(query, teamName, sprintName, fromDate, toDate);
        }

        var prIds = prs.Select(pr => pr.Id).ToList();

        // ── 5. Batch-load enrichment data ──────────────────────────────────────
        var iterationRows = await _context.PullRequestIterations
            .AsNoTracking()
            .Where(i => prIds.Contains(i.PullRequestId))
            .Select(i => i.PullRequestId)
            .ToListAsync(cancellationToken);

        var iterationsByPr = iterationRows
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var fileRows = await _context.PullRequestFileChanges
            .AsNoTracking()
            .Where(f => prIds.Contains(f.PullRequestId))
            .Select(f => new { f.PullRequestId, f.FilePath })
            .ToListAsync(cancellationToken);

        var filesByPr = fileRows
            .GroupBy(f => f.PullRequestId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.FilePath).Distinct().Count());

        // ── 6. Load work item links ────────────────────────────────────────────
        var wiLinkRows = await _context.PullRequestWorkItemLinks
            .AsNoTracking()
            .Where(l => prIds.Contains(l.PullRequestId))
            .Select(l => new { l.PullRequestId, l.WorkItemId })
            .ToListAsync(cancellationToken);

        var linkedWorkItemIdsByPr = wiLinkRows
            .GroupBy(l => l.PullRequestId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.WorkItemId).ToList());

        // ── 7. Load all work items needed for hierarchy traversal ──────────────
        var allLinkedWorkItemIds = wiLinkRows.Select(l => l.WorkItemId).Distinct().ToList();

        // Load the directly linked work items and their ancestors (up to 50 levels)
        // We build the full map by loading all work items; we rely on the cache being
        // populated already (the work item sync stage populates WorkItems).
        var workItemMap = await _context.WorkItems
            .AsNoTracking()
            .ToDictionaryAsync(wi => wi.TfsId, cancellationToken);

        // ── 8. Classify each PR ────────────────────────────────────────────────
        var classified = prs.Select(pr =>
        {
            var lifetimeHours = ComputeLifetimeHours(pr, now);
            var reviewCycles  = iterationsByPr.GetValueOrDefault(pr.Id, 0);
            var filesChanged  = filesByPr.GetValueOrDefault(pr.Id, 0);

            if (!linkedWorkItemIdsByPr.TryGetValue(pr.Id, out var linkedIds) || linkedIds.Count == 0)
            {
                return new ClassifiedPr(
                    pr.Id, pr.Title, pr.RepositoryName, pr.Status,
                    lifetimeHours, reviewCycles, filesChanged,
                    CategoryUnmapped,
                    EpicId: null, EpicName: null,
                    FeatureId: null, FeatureName: null,
                    AllEpics: Array.Empty<(int, string)>(),
                    AllFeatures: Array.Empty<(int, string)>());
            }

            // Resolve hierarchy for all linked work items
            var allEpics    = new List<(int Id, string Name)>();
            var allFeatures = new List<(int Id, string Name)>();
            bool hasBugLink        = false;
            bool hasPbiWithoutFeature = false;

            foreach (var wiId in linkedIds)
            {
                if (!workItemMap.TryGetValue(wiId, out var wi))
                    continue;

                var (epicId, featureId) = PoTool.Api.Services.WorkItemResolutionService.ResolveAncestry(
                    wiId, workItemMap);

                if (epicId.HasValue && workItemMap.TryGetValue(epicId.Value, out var epicWi))
                {
                    if (!allEpics.Any(e => e.Id == epicId.Value))
                        allEpics.Add((epicId.Value, epicWi.Title));
                }

                if (featureId.HasValue && workItemMap.TryGetValue(featureId.Value, out var featWi))
                {
                    if (!allFeatures.Any(f => f.Id == featureId.Value))
                        allFeatures.Add((featureId.Value, featWi.Title));
                }

                // Check type-specific flags for lower-priority categories
                if (string.Equals(wi.Type, WorkItemType.Bug, StringComparison.OrdinalIgnoreCase)
                    && !epicId.HasValue && !featureId.HasValue)
                {
                    hasBugLink = true;
                }

                if (string.Equals(wi.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase)
                    && !featureId.HasValue && !epicId.HasValue)
                {
                    hasPbiWithoutFeature = true;
                }
            }

            string category;
            if (allEpics.Count > 0 || allFeatures.Count > 0)
                category = CategoryDeliveryMapped;
            else if (hasBugLink)
                category = CategoryBug;
            else if (hasPbiWithoutFeature)
                category = CategoryDisturbance;
            else
                category = CategoryUnmapped;

            var primaryEpic    = allEpics.FirstOrDefault();
            var primaryFeature = allFeatures.FirstOrDefault();

            return new ClassifiedPr(
                pr.Id, pr.Title, pr.RepositoryName, pr.Status,
                lifetimeHours, reviewCycles, filesChanged,
                category,
                EpicId:    primaryEpic.Id    != 0 ? primaryEpic.Id    : (int?)null,
                EpicName:  primaryEpic.Name,
                FeatureId: primaryFeature.Id != 0 ? primaryFeature.Id : (int?)null,
                FeatureName: primaryFeature.Name,
                AllEpics:    allEpics,
                AllFeatures: allFeatures);
        }).ToList();

        // ── 9. Global category summary ─────────────────────────────────────────
        var total             = classified.Count;
        var deliveryMapped    = classified.Count(c => c.Category == CategoryDeliveryMapped);
        var bugCount          = classified.Count(c => c.Category == CategoryBug);
        var disturbanceCount  = classified.Count(c => c.Category == CategoryDisturbance);
        var unmappedCount     = classified.Count(c => c.Category == CategoryUnmapped);

        var categorySummary = new PRCategorySummaryDto
        {
            TotalPrs             = total,
            DeliveryMappedCount  = deliveryMapped,
            DeliveryMappedPct    = Pct(deliveryMapped, total),
            BugCount             = bugCount,
            BugPct               = Pct(bugCount, total),
            DisturbanceCount     = disturbanceCount,
            DisturbancePct       = Pct(disturbanceCount, total),
            UnmappedCount        = unmappedCount,
            UnmappedPct          = Pct(unmappedCount, total)
        };

        // ── 10. Epic breakdown ─────────────────────────────────────────────────
        // Collect all (epicId → epicName, prLifetime, reviewCycles, status) entries
        var epicEntries = new List<(int EpicId, string EpicName, double LifetimeHours, int ReviewCycles, string Status)>();

        foreach (var cp in classified)
        {
            foreach (var (epicId, epicName) in cp.AllEpics)
            {
                epicEntries.Add((epicId, epicName, cp.LifetimeHours, cp.ReviewCycles, cp.Status));
            }
        }

        var epicBreakdown = epicEntries
            .GroupBy(e => e.EpicId)
            .Select(g =>
            {
                var epicName    = g.First().EpicName;
                var epicPrCount = g.Count();

                var lifetimesSorted = g
                    .Where(e => e.LifetimeHours > 0)
                    .Select(e => e.LifetimeHours)
                    .OrderBy(h => h)
                    .ToList();

                var cycles = g
                    .Where(e => e.ReviewCycles > 0)
                    .Select(e => (double)e.ReviewCycles)
                    .ToList();

                var abandoned = g.Count(e => IsAbandoned(e.Status));

                return new EpicFrictionSummaryDto
                {
                    EpicId             = g.Key,
                    EpicName           = epicName,
                    PrCount            = epicPrCount,
                    MedianLifetimeHours = Median(lifetimesSorted),
                    P90LifetimeHours   = lifetimesSorted.Count >= 3
                        ? Percentile(lifetimesSorted, 0.90)
                        : null,
                    AbandonedPct       = epicPrCount > 0
                        ? Math.Round(100.0 * abandoned / epicPrCount, 1)
                        : null,
                    AvgReviewCycles    = cycles.Count > 0
                        ? Math.Round(cycles.Average(), 2)
                        : null
                };
            })
            .OrderByDescending(e => e.PrCount)
            .ToList();

        // ── 11. Feature breakdown ──────────────────────────────────────────────
        var featureEntries = new List<(int FeatureId, string FeatureName, int? EpicId, string? EpicName, double LifetimeHours)>();

        foreach (var cp in classified)
        {
            foreach (var (featureId, featureName) in cp.AllFeatures)
            {
                // Find the Epic for this Feature (if any)
                int? epicId   = null;
                string? epicName = null;

                if (workItemMap.TryGetValue(featureId, out var featWi))
                {
                    var (resolvedEpicId, _) = PoTool.Api.Services.WorkItemResolutionService.ResolveAncestry(
                        featureId, workItemMap);

                    if (resolvedEpicId.HasValue && workItemMap.TryGetValue(resolvedEpicId.Value, out var epicWi))
                    {
                        epicId   = resolvedEpicId.Value;
                        epicName = epicWi.Title;
                    }
                }

                featureEntries.Add((featureId, featureName, epicId, epicName, cp.LifetimeHours));
            }
        }

        // Build PBI count per Feature for the ratio
        var pbiCountByFeatureId = await _context.WorkItems
            .AsNoTracking()
            .Where(wi => string.Equals(wi.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase))
            .Select(wi => new { wi.ParentTfsId })
            .ToListAsync(cancellationToken);

        var pbisByFeature = pbiCountByFeatureId
            .Where(r => r.ParentTfsId.HasValue)
            .GroupBy(r => r.ParentTfsId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var featureBreakdown = featureEntries
            .GroupBy(f => f.FeatureId)
            .Select(g =>
            {
                var first     = g.First();
                var fPrCount  = g.Count();

                var lifetimesSorted = g
                    .Where(f => f.LifetimeHours > 0)
                    .Select(f => f.LifetimeHours)
                    .OrderBy(h => h)
                    .ToList();

                var pbiCount = pbisByFeature.GetValueOrDefault(g.Key, 0);

                return new FeatureComplexitySummaryDto
                {
                    FeatureId           = g.Key,
                    FeatureName         = first.FeatureName,
                    EpicId              = first.EpicId,
                    EpicName            = first.EpicName,
                    PrCount             = fPrCount,
                    PrPerPbiRatio       = pbiCount > 0
                        ? Math.Round((double)fPrCount / pbiCount, 2)
                        : null,
                    MedianLifetimeHours = Median(lifetimesSorted)
                };
            })
            .OrderByDescending(f => f.PrCount)
            .ToList();

        // ── 12. Scatter chart data ─────────────────────────────────────────────
        var scatterPoints = classified
            .Select(cp => new PrDeliveryScatterPointDto
            {
                Id           = cp.Id,
                Title        = cp.Title,
                CreatedDate  = prs.First(p => p.Id == cp.Id).CreatedDate,
                LifetimeHours = cp.LifetimeHours,
                Status       = cp.Status,
                Category     = cp.Category,
                EpicId       = cp.EpicId,
                EpicName     = cp.EpicName,
                FeatureId    = cp.FeatureId,
                FeatureName  = cp.FeatureName
            })
            .ToList();

        // ── 13. Outliers (top 20 by lifetime) ────────────────────────────────
        var outliers = classified
            .Where(cp => cp.LifetimeHours > 0)
            .OrderByDescending(cp => cp.LifetimeHours)
            .Take(20)
            .Select(cp => new PrOutlierDto
            {
                Id           = cp.Id,
                Title        = cp.Title,
                Repository   = cp.Repository,
                Status       = cp.Status,
                LifetimeHours = cp.LifetimeHours,
                FilesChanged = cp.FilesChanged,
                ReviewCycles = cp.ReviewCycles,
                EpicId       = cp.EpicId,
                EpicName     = cp.EpicName,
                FeatureId    = cp.FeatureId,
                FeatureName  = cp.FeatureName,
                Category     = cp.Category
            })
            .ToList();

        // ── 14. Team improvement tips (signal detection) ─────────────────────────
        var improvementTips = DetectImprovementTips(classified, categorySummary, epicBreakdown);

        // ── 15. Assemble result ────────────────────────────────────────────────
        return new PrDeliveryInsightsDto
        {
            TeamId          = query.TeamId,
            TeamName        = teamName,
            SprintId        = query.SprintId,
            SprintName      = sprintName,
            FromDate        = fromDate,
            ToDate          = toDate,
            CategorySummary = categorySummary,
            EpicBreakdown   = epicBreakdown,
            FeatureBreakdown = featureBreakdown,
            ScatterPoints   = scatterPoints,
            Outliers        = outliers,
            ImprovementTips = improvementTips
        };
    }

    // ── Signal detection ─────────────────────────────────────────────────────

    /// <summary>
    /// Detects up to three team improvement signals from classified PR data.
    /// Rules are evaluated in priority order; only the strongest signals are returned.
    /// </summary>
    private static IReadOnlyList<TeamImprovementTipDto> DetectImprovementTips(
        List<ClassifiedPr> classified,
        PRCategorySummaryDto categorySummary,
        IReadOnlyList<EpicFrictionSummaryDto> epicBreakdown)
    {
        const int MaxTips = 3;

        var tips = new List<TeamImprovementTipDto>();

        if (classified.Count == 0)
            return tips;

        // ── Signal 1: Long PR lifetimes ───────────────────────────────────────
        // Threshold: global median > 24 h (1 business day)
        var completedLifetimes = classified
            .Where(cp => cp.LifetimeHours > 0 &&
                   !string.Equals(cp.Status, "active", StringComparison.OrdinalIgnoreCase))
            .Select(cp => cp.LifetimeHours)
            .OrderBy(h => h)
            .ToList();

        var globalMedian = Median(completedLifetimes);
        const double LongLifetimeThresholdHours = 24.0;

        if (globalMedian.HasValue && globalMedian.Value > LongLifetimeThresholdHours)
        {
            var days = Math.Round(globalMedian.Value / 24.0, 1);
            tips.Add(new TeamImprovementTipDto
            {
                Signal         = $"Long PR Lifetimes (median {days}d)",
                Interpretation = "PRs may contain large changes or mixed refactoring and feature work, slowing review and integration.",
                PoMessage      = "Consider splitting large PRs into smaller, focused changes. Smaller PRs are easier to review and merge faster."
            });
        }

        if (tips.Count >= MaxTips) return tips;

        // ── Signal 2: High review churn ───────────────────────────────────────
        // Threshold: > 30 % of completed PRs have more than one review cycle
        if (completedLifetimes.Count > 0)
        {
            var completedPrs        = classified.Where(cp =>
                !string.Equals(cp.Status, "active", StringComparison.OrdinalIgnoreCase)).ToList();
            var highChurnCount      = completedPrs.Count(cp => cp.ReviewCycles > 1);
            var highChurnPct        = completedPrs.Count > 0
                ? 100.0 * highChurnCount / completedPrs.Count : 0;
            const double ChurnThresholdPct = 30.0;

            if (highChurnPct > ChurnThresholdPct)
            {
                var pctLabel = Math.Round(highChurnPct, 0);
                tips.Add(new TeamImprovementTipDto
                {
                    Signal         = $"High Review Churn ({pctLabel}% of PRs required multiple review rounds)",
                    Interpretation = "Coding standards or early validation may be inconsistent, causing repeated review cycles.",
                    PoMessage      = "Encourage the team to use Definition of Done checklists and pair-review before submitting PRs to reduce back-and-forth."
                });
            }
        }

        if (tips.Count >= MaxTips) return tips;

        // ── Signal 3: High bug PR share ───────────────────────────────────────
        // Threshold: BugPct > 20 %
        const double BugPctThreshold = 20.0;
        if (categorySummary.BugPct > BugPctThreshold)
        {
            var pctLabel = categorySummary.BugPct.ToString("F0");
            tips.Add(new TeamImprovementTipDto
            {
                Signal         = $"High Bug PR Share ({pctLabel}% of PRs linked to Bug work items)",
                Interpretation = "Team capacity may be spent on corrective work rather than planned delivery.",
                PoMessage      = "Review defect root causes with the team. Consider a short retrospective focused on reducing recurring bug types."
            });
        }

        if (tips.Count >= MaxTips) return tips;

        // ── Signal 4: High disturbance share ─────────────────────────────────
        // Threshold: DisturbancePct > 20 %
        const double DisturbancePctThreshold = 20.0;
        if (categorySummary.DisturbancePct > DisturbancePctThreshold)
        {
            var pctLabel = categorySummary.DisturbancePct.ToString("F0");
            tips.Add(new TeamImprovementTipDto
            {
                Signal         = $"High Disturbance Share ({pctLabel}% of PRs linked to unplanned PBIs)",
                Interpretation = "Unplanned work (verstoringen) may be interrupting planned delivery work.",
                PoMessage      = "Work with the team to classify unplanned work and track interruption patterns. Consider reserving capacity for unplanned work in sprint planning."
            });
        }

        if (tips.Count >= MaxTips) return tips;

        // ── Signal 5: Epic-specific friction ─────────────────────────────────
        // Condition: one Epic has median lifetime > 2× the global median
        // and that Epic has at least 3 PRs (sufficient sample)
        const int EpicNameMaxLength = 40;

        if (globalMedian.HasValue && globalMedian.Value > 0 && epicBreakdown.Count > 1)
        {
            var frictionEpic = epicBreakdown
                .Where(e => e.PrCount >= 3 && e.MedianLifetimeHours.HasValue &&
                            e.MedianLifetimeHours.Value > 2.0 * globalMedian.Value)
                .OrderByDescending(e => e.MedianLifetimeHours)
                .FirstOrDefault();

            if (frictionEpic != null)
            {
                var epicDays  = Math.Round(frictionEpic.MedianLifetimeHours!.Value / 24.0, 1);
                var epicLabel = TruncateForSignal(frictionEpic.EpicName, EpicNameMaxLength);
                tips.Add(new TeamImprovementTipDto
                {
                    Signal         = $"Epic-Specific Friction — \"{epicLabel}\" (median {epicDays}d)",
                    Interpretation = "This Epic's PRs take significantly longer than average, which may indicate architectural complexity or unclear scope.",
                    PoMessage      = $"Discuss complexity and delivery blockers for \"{epicLabel}\" with the team. Consider breaking down remaining work further."
                });
            }
        }

        return tips;
    }

    private static string TruncateForSignal(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    private static double ComputeLifetimeHours(
        PoTool.Api.Persistence.Entities.PullRequestEntity pr,
        DateTimeOffset now)
    {
        var end   = pr.CompletedDate ?? now;
        var hours = (end - pr.CreatedDate).TotalHours;
        return Math.Max(0, Math.Round(hours, 2));
    }

    private static bool IsAbandoned(string status) =>
        string.Equals(status, "abandoned", StringComparison.OrdinalIgnoreCase);

    private static double Pct(int part, int total) =>
        total > 0 ? Math.Round(100.0 * part / total, 1) : 0;

    private static double? Median(List<double> sorted)
    {
        if (sorted.Count == 0) return null;
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static double Percentile(List<double> sorted, double p)
    {
        int idx = (int)Math.Ceiling(p * sorted.Count) - 1;
        idx = Math.Clamp(idx, 0, sorted.Count - 1);
        return sorted[idx];
    }

    private static PrDeliveryInsightsDto EmptyResult(
        GetPrDeliveryInsightsQuery query,
        string? teamName,
        string? sprintName,
        DateTimeOffset fromDate,
        DateTimeOffset toDate) =>
        new()
        {
            TeamId     = query.TeamId,
            TeamName   = teamName,
            SprintId   = query.SprintId,
            SprintName = sprintName,
            FromDate   = fromDate,
            ToDate     = toDate,
            CategorySummary = new PRCategorySummaryDto()
        };

    // ── Internal projection record ────────────────────────────────────────────

    private sealed record ClassifiedPr(
        int Id,
        string Title,
        string Repository,
        string Status,
        double LifetimeHours,
        int ReviewCycles,
        int FilesChanged,
        string Category,
        int? EpicId,
        string? EpicName,
        int? FeatureId,
        string? FeatureName,
        IReadOnlyList<(int Id, string Name)> AllEpics,
        IReadOnlyList<(int Id, string Name)> AllFeatures);
}
