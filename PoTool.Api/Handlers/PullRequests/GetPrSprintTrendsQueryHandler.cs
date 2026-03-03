using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Core.PullRequests.Queries;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPrSprintTrendsQuery.
/// Aggregates PR metrics per sprint by mapping each PR's CreatedDateUtc to the sprint
/// whose [StartDateUtc, EndDateUtc) boundary it falls within.
///
/// Metrics computed:
///   1. Median PR size: primary = total lines changed (LinesAdded + LinesDeleted) per PR from
///      PullRequestFileChanges. Fallback to count of distinct files changed if all lines data is zero
///      (e.g. file changes not yet synced). Fallback is documented via PrSprintMetricsDto.PrSizeIsLinesChanged.
///
///   2. Median time to first review: earliest PullRequestComment whose author differs from the
///      PR's CreatedBy field, minus CreatedDate. PullRequestComments are the earliest non-author
///      activity available in the cached data model (before iterations, which track code pushes).
///
///   3. Median time to merge: CompletedDate - CreatedDate for completed/merged PRs.
///
///   4. P90 time to merge: 90th-percentile of time-to-merge. If the sprint has fewer than 3
///      completed PRs, the P90 is omitted (null) to avoid misleading percentiles on tiny samples.
/// </summary>
public sealed class GetPrSprintTrendsQueryHandler
    : IQueryHandler<GetPrSprintTrendsQuery, GetPrSprintTrendsResponse>
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<GetPrSprintTrendsQueryHandler> _logger;

    public GetPrSprintTrendsQueryHandler(
        PoToolDbContext context,
        ILogger<GetPrSprintTrendsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async ValueTask<GetPrSprintTrendsResponse> Handle(
        GetPrSprintTrendsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SprintIds.Count == 0)
        {
            return new GetPrSprintTrendsResponse
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
            return new GetPrSprintTrendsResponse
            {
                Success = true,
                Sprints = Array.Empty<PrSprintMetricsDto>()
            };
        }

        // 2. Determine allowed product IDs (same priority as pipeline sprint trends handler)
        // Priority: explicit ProductIds > TeamId via ProductTeamLinks > all PRs
        List<int>? allowedProductIds;
        if (query.ProductIds != null && query.ProductIds.Count > 0)
        {
            allowedProductIds = query.ProductIds;
        }
        else if (query.TeamId.HasValue)
        {
            allowedProductIds = await _context.ProductTeamLinks
                .Where(l => l.TeamId == query.TeamId.Value)
                .Select(l => l.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (allowedProductIds.Count == 0)
            {
                _logger.LogDebug("No products linked to team {TeamId}; returning empty sprint metrics", query.TeamId.Value);
                return new GetPrSprintTrendsResponse
                {
                    Success = true,
                    Sprints = sprints.Select(s => new PrSprintMetricsDto
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
            // No filter: include all products
            allowedProductIds = null;
        }

        // 3. Load PRs whose CreatedDateUtc falls within the overall sprint range
        var rangeStart = sprints.Min(s => s.StartDateUtc!.Value);
        var rangeEnd = sprints.Max(s => s.EndDateUtc!.Value);

        var prsQuery = _context.PullRequests
            .Where(pr => pr.CreatedDateUtc >= rangeStart && pr.CreatedDateUtc < rangeEnd);

        if (allowedProductIds != null)
        {
            prsQuery = prsQuery.Where(pr => pr.ProductId.HasValue && allowedProductIds.Contains(pr.ProductId.Value));
        }

        var prs = await prsQuery
            .Select(pr => new
            {
                pr.Id,
                pr.CreatedBy,
                pr.CreatedDateUtc,
                pr.CreatedDate,
                pr.CompletedDate,
                pr.Status
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        _logger.LogDebug("PR sprint trends: loaded {PrCount} PRs for {SprintCount} sprints", prs.Count, sprints.Count);

        if (prs.Count == 0)
        {
            return new GetPrSprintTrendsResponse
            {
                Success = true,
                Sprints = sprints.Select(s => new PrSprintMetricsDto
                {
                    SprintId = s.Id,
                    SprintName = s.Name,
                    StartUtc = s.StartUtc,
                    EndUtc = s.EndUtc
                }).ToList()
            };
        }

        var prIds = prs.Select(pr => pr.Id).ToList();

        // 4. Batch-load file changes for all PRs in range
        var fileChanges = await _context.PullRequestFileChanges
            .Where(fc => prIds.Contains(fc.PullRequestId))
            .Select(fc => new
            {
                fc.PullRequestId,
                fc.FilePath,
                fc.LinesAdded,
                fc.LinesDeleted
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // 5. Batch-load comments for all PRs in range
        var comments = await _context.PullRequestComments
            .Where(c => prIds.Contains(c.PullRequestId))
            .Select(c => new
            {
                c.PullRequestId,
                c.Author,
                c.CreatedDateUtc
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // 6. Build lookups (kept as anonymous-type collections to avoid EF projection overhead)
        var fileChangesLookup = fileChanges.ToLookup(fc => fc.PullRequestId);
        var commentsLookup = comments.ToLookup(c => c.PullRequestId);

        // 7. Assign each PR to a sprint
        var sprintSlots = sprints
            .Select(s => (Sprint: s, PrItems: new List<(int Id, string CreatedBy, DateTime CreatedDateUtc, DateTimeOffset? CompletedDate, string Status)>()))
            .ToList();

        foreach (var pr in prs)
        {
            foreach (var slot in sprintSlots)
            {
                if (pr.CreatedDateUtc >= slot.Sprint.StartDateUtc!.Value &&
                    pr.CreatedDateUtc < slot.Sprint.EndDateUtc!.Value)
                {
                    slot.PrItems.Add((pr.Id, pr.CreatedBy, pr.CreatedDateUtc, pr.CompletedDate, pr.Status));
                    break;
                }
            }
        }

        // 8. Compute per-sprint metrics
        var sprintMetrics = new List<PrSprintMetricsDto>(sprints.Count);
        foreach (var slot in sprintSlots)
        {
            var dto = new PrSprintMetricsDto
            {
                SprintId = slot.Sprint.Id,
                SprintName = slot.Sprint.Name,
                StartUtc = slot.Sprint.StartUtc,
                EndUtc = slot.Sprint.EndUtc,
                TotalPrs = slot.PrItems.Count
            };

            if (slot.PrItems.Count == 0)
            {
                sprintMetrics.Add(dto);
                continue;
            }

            // --- Metric 1: Median PR size ---
            // Primary: total lines changed per PR (LinesAdded + LinesDeleted).
            // Fallback: distinct files changed per PR when all lines data is zero
            // (e.g. file change sync has not yet run for these PRs).
            var prSizes = slot.PrItems.Select(pr =>
            {
                var changes = fileChangesLookup[pr.Id].ToList();
                if (changes.Count == 0)
                    return (LinesChanged: 0, FileCount: 0);
                var lines = changes.Sum(fc => fc.LinesAdded + fc.LinesDeleted);
                var files = changes.GroupBy(fc => fc.FilePath).Count();
                return (LinesChanged: lines, FileCount: files);
            }).ToList();

            var totalLines = prSizes.Sum(s => s.LinesChanged);
            if (totalLines > 0)
            {
                var sortedLines = prSizes.Select(s => (double)s.LinesChanged).OrderBy(x => x).ToList();
                dto.MedianPrSize = Median(sortedLines);
                dto.PrSizeIsLinesChanged = true;
            }
            else
            {
                var sortedFiles = prSizes.Select(s => (double)s.FileCount).OrderBy(x => x).ToList();
                if (sortedFiles.Sum() > 0)
                {
                    dto.MedianPrSize = Median(sortedFiles);
                    dto.PrSizeIsLinesChanged = false;
                }
            }

            // --- Metric 2: Median time to first review ---
            // Time from PR CreatedDate to earliest non-author comment CreatedDate.
            var timeToFirstReviewValues = new List<double>();
            foreach (var pr in slot.PrItems)
            {
                var firstReview = commentsLookup[pr.Id]
                    .Where(c => !string.Equals(c.Author, pr.CreatedBy, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.CreatedDateUtc)
                    .FirstOrDefault();

                if (firstReview == null)
                    continue;

                var hours = (firstReview.CreatedDateUtc - pr.CreatedDateUtc).TotalHours;
                if (hours >= 0)
                    timeToFirstReviewValues.Add(hours);
            }

            if (timeToFirstReviewValues.Count > 0)
            {
                dto.MedianTimeToFirstReviewHours = Median(timeToFirstReviewValues.OrderBy(x => x).ToList());
            }

            // --- Metrics 3 & 4: Time to merge ---
            var timeToMergeValues = slot.PrItems
                .Where(pr => pr.CompletedDate.HasValue &&
                             string.Equals(pr.Status, "completed", StringComparison.OrdinalIgnoreCase))
                .Select(pr => (pr.CompletedDate!.Value - pr.CreatedDateUtc).TotalHours)
                .Where(h => h >= 0)
                .OrderBy(h => h)
                .ToList();

            if (timeToMergeValues.Count > 0)
            {
                dto.MedianTimeToMergeHours = Median(timeToMergeValues);
                if (timeToMergeValues.Count >= 3)
                {
                    dto.P90TimeToMergeHours = Percentile(timeToMergeValues, 90);
                }
            }

            sprintMetrics.Add(dto);
        }

        return new GetPrSprintTrendsResponse
        {
            Success = true,
            Sprints = sprintMetrics
        };
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
}
