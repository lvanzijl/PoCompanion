using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Api.Services;
using PoTool.Core.PullRequests.Queries;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Statistics;

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
    private sealed record PrSizeMetrics(int LinesChanged, int FileCount);

    private readonly IPullRequestQueryStore _queryStore;
    private readonly ILogger<GetPrSprintTrendsQueryHandler> _logger;

    public GetPrSprintTrendsQueryHandler(
        IPullRequestQueryStore queryStore,
        ILogger<GetPrSprintTrendsQueryHandler> logger)
    {
        _queryStore = queryStore;
        _logger = logger;
    }

    public async ValueTask<GetPrSprintTrendsResponse> Handle(
        GetPrSprintTrendsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.EffectiveFilter.SprintIds.Count == 0)
        {
            return new GetPrSprintTrendsResponse
            {
                Success = false,
                ErrorMessage = "At least one sprint ID is required."
            };
        }

        _logger.LogInformation(
            "Handling GetPrSprintTrendsQuery with repository scope {RepositoryCount}, sprint count {SprintCount}, range [{RangeStartUtc}, {RangeEndUtc}]",
            query.EffectiveFilter.RepositoryScope.Count,
            query.EffectiveFilter.SprintIds.Count,
            query.EffectiveFilter.RangeStartUtc,
            query.EffectiveFilter.RangeEndUtc);

        var sprintIdList = query.EffectiveFilter.SprintIds.Distinct().ToList();
        var queryData = await _queryStore.GetSprintTrendDataAsync(query.EffectiveFilter, cancellationToken);
        var sprints = queryData.Sprints;

        if (sprints.Count == 0)
        {
            _logger.LogWarning("No sprints with valid dates found for IDs: {SprintIds}", string.Join(",", sprintIdList));
            return new GetPrSprintTrendsResponse
            {
                Success = true,
                Sprints = Array.Empty<PrSprintMetricsDto>()
            };
        }

        var prs = queryData.PullRequests;

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

        var fileChanges = queryData.FileChanges;
        var comments = queryData.Comments;

        // 6. Build lookups (kept as anonymous-type collections to avoid EF projection overhead)
        var fileChangesLookup = fileChanges.ToLookup(fc => fc.PullRequestId);
        var commentsLookup = comments.ToLookup(c => c.PullRequestId);

        // 7. Assign each PR to a sprint
        var sprintSlots = sprints
            .Select(s => (Sprint: s, PrItems: new List<PrSprintTrendPullRequest>()))
            .ToList();

        foreach (var pr in prs)
        {
            foreach (var slot in sprintSlots)
            {
                if (pr.CreatedDateUtc >= slot.Sprint.StartDateUtc &&
                    pr.CreatedDateUtc < slot.Sprint.EndDateUtc)
                {
                    slot.PrItems.Add(pr);
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
                    return new PrSizeMetrics(0, 0);
                var lines = changes.Sum(fc => fc.LinesAdded + fc.LinesDeleted);
                var files = changes.GroupBy(fc => fc.FilePath).Count();
                return new PrSizeMetrics(lines, files);
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
                    dto.P90TimeToMergeHours = PercentileMath.LinearInterpolation(timeToMergeValues, 90);
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
}
