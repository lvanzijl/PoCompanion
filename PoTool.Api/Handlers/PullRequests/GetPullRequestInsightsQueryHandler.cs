using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Api.Services;
using PoTool.Core.PullRequests.Queries;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Statistics;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestInsightsQuery.
/// Computes PO-focused PR health metrics from cached data only.
///
/// Scope rules:
///   - When TeamId is supplied, PRs are scoped to products linked to that team via ProductTeamLinks.
///   - Date range filters by PR.CreatedDateUtc.
///
/// "Changes requested" proxy:
///   Since reviewer vote history is not stored in the cache, a PR is classified as
///   "merged after rework" when it has more than one iteration (iteration count > 1).
///
/// Lifetime definition:
///   - completed / abandoned PRs: CompletedDate - CreatedDate.
///   - active PRs: UtcNow - CreatedDate.
/// </summary>
public sealed class GetPullRequestInsightsQueryHandler
    : IQueryHandler<GetPullRequestInsightsQuery, PullRequestInsightsDto>
{
    private readonly IPullRequestQueryStore _queryStore;
    private readonly ILogger<GetPullRequestInsightsQueryHandler> _logger;

    public GetPullRequestInsightsQueryHandler(
        IPullRequestQueryStore queryStore,
        ILogger<GetPullRequestInsightsQueryHandler> logger)
    {
        _queryStore = queryStore;
        _logger = logger;
    }

    public async ValueTask<PullRequestInsightsDto> Handle(
        GetPullRequestInsightsQuery query,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = query.EffectiveFilter;

        _logger.LogInformation(
            "Handling GetPullRequestInsightsQuery with repository scope {RepositoryCount}, range [{RangeStartUtc}, {RangeEndUtc}]",
            filter.RepositoryScope.Count,
            filter.RangeStartUtc,
            filter.RangeEndUtc);

        var queryData = await _queryStore.GetInsightsDataAsync(filter, cancellationToken);
        string? tfsBaseUrl = BuildTfsBaseUrl(queryData.Configuration);
        var teamName = queryData.TeamName;
        var prs = queryData.PullRequests;

        if (prs.Count == 0)
        {
            _logger.LogDebug(
                "PR Insights: no PRs found in effective range [{From}, {To}]",
                filter.RangeStartUtc,
                filter.RangeEndUtc);
            return EmptyResult(
                filter.Context.TeamIds.IsAll ? null : filter.Context.TeamIds.Values.FirstOrDefault(),
                teamName,
                filter.RangeStartUtc ?? DateTimeOffset.MinValue,
                filter.RangeEndUtc ?? DateTimeOffset.MaxValue);
        }

        var iterationsByPr = queryData.IterationsByPullRequestId;
        var commentsByPr = queryData.CommentsByPullRequestId;
        var filesByPr = queryData.DistinctFilesByPullRequestId;

        // ── 4. Build enriched records ──────────────────────────────────────────
        var enriched = prs.Select(pr =>
        {
            var reviewCycles  = iterationsByPr.GetValueOrDefault(pr.Id, 0);
            var commentCount  = commentsByPr.GetValueOrDefault(pr.Id, 0);
            var filesChanged  = filesByPr.GetValueOrDefault(pr.Id, 0);
            var lifetimeHours = ComputeLifetimeHours(pr, now);
            var colorCategory = ComputeColorCategory(pr.Status, reviewCycles);

            return new PrRecord(
                Id:            pr.Id,
                Title:         pr.Title,
                Repository:    pr.RepositoryName,
                Author:        pr.CreatedBy,
                CreatedDate:   pr.CreatedDate,
                Status:        pr.Status,
                LifetimeHours: lifetimeHours,
                ReviewCycles:  reviewCycles,
                FilesChanged:  filesChanged,
                CommentCount:  commentCount,
                ColorCategory: colorCategory);
        }).ToList();

        // ── 5. Compute summary ─────────────────────────────────────────────────
        var total     = enriched.Count;
        var merged    = enriched.Count(r => IsCompleted(r.Status));
        var abandoned = enriched.Count(r => IsAbandoned(r.Status));
        var rework    = enriched.Count(r => IsCompleted(r.Status) && r.ReviewCycles > 1);

        var lifetimes = enriched
            .Where(r => r.LifetimeHours > 0)
            .Select(r => r.LifetimeHours)
            .OrderBy(h => h)
            .ToList();

        var summary = new PrInsightsSummaryDto
        {
            TotalPrs                = total,
            MergeRatePct            = total > 0 ? Math.Round(100.0 * merged / total, 1)    : 0,
            AbandonRatePct          = total > 0 ? Math.Round(100.0 * abandoned / total, 1) : 0,
            ChangesRequestedRatePct = total > 0 ? Math.Round(100.0 * rework / total, 1)    : 0,
            MedianLifetimeHours     = Median(lifetimes),
            P90LifetimeHours        = lifetimes.Count >= 3 ? PercentileMath.LinearInterpolation(lifetimes, 90) : null
        };

        // ── 6. Scatter points ──────────────────────────────────────────────────
        var scatterPoints = enriched
            .Select(r => new PrScatterPointDto
            {
                Id            = r.Id,
                Title         = r.Title,
                Repository    = r.Repository,
                Author        = r.Author,
                CreatedDate   = r.CreatedDate,
                LifetimeHours = r.LifetimeHours,
                ReviewCycles  = r.ReviewCycles,
                FilesChanged  = r.FilesChanged,
                CommentCount  = r.CommentCount,
                ColorCategory = r.ColorCategory,
                 Url           = BuildPrUrl(tfsBaseUrl, queryData.Configuration?.Project, r.Repository, r.Id)
            })
            .ToList();

        // ── 7. Top 3 problematic ───────────────────────────────────────────────
        var maxLifetime = enriched.Count > 0 ? enriched.Max(r => r.LifetimeHours) : 1.0;
        var maxCycles   = enriched.Count > 0 ? (double)enriched.Max(r => r.ReviewCycles) : 1.0;
        var maxFiles    = enriched.Count > 0 ? (double)enriched.Max(r => r.FilesChanged) : 1.0;
        var maxComments = enriched.Count > 0 ? (double)enriched.Max(r => r.CommentCount) : 1.0;

        var top3 = enriched
            .Select(r => BuildProblematicEntry(r, maxLifetime, maxCycles, maxFiles, maxComments,
                BuildPrUrl(tfsBaseUrl, queryData.Configuration?.Project, r.Repository, r.Id)))
            .OrderByDescending(e => e.RankingScore)
            .Take(3)
            .ToList();

        // ── 8. Longest PRs table (top 20) ─────────────────────────────────────
        var longestPrs = enriched
            .Select(r => BuildProblematicEntry(r, maxLifetime, maxCycles, maxFiles, maxComments,
                BuildPrUrl(tfsBaseUrl, queryData.Configuration?.Project, r.Repository, r.Id)))
            .OrderByDescending(e => e.LifetimeHours)
            .Take(20)
            .ToList();

        // ── 9. Repository breakdown ────────────────────────────────────────────
        var repoBreakdown = enriched
            .GroupBy(r => r.Repository)
            .Select(g =>
            {
                var repoTotal    = g.Count();
                var repoMerged   = g.Count(r => IsCompleted(r.Status));
                var repoAbandoned = g.Count(r => IsAbandoned(r.Status));

                var completedLifetimes = g
                    .Where(r => IsCompleted(r.Status))
                    .Select(r => r.LifetimeHours)
                    .OrderBy(h => h)
                    .ToList();

                var allCycles = g
                    .Where(r => r.ReviewCycles > 0)
                    .Select(r => (double)r.ReviewCycles)
                    .ToList();

                return new PrRepositoryBreakdownDto
                {
                    Repository         = g.Key,
                    PrCount            = repoTotal,
                    MergePct           = repoTotal > 0 ? Math.Round(100.0 * repoMerged / repoTotal, 1)    : 0,
                    AbandonPct         = repoTotal > 0 ? Math.Round(100.0 * repoAbandoned / repoTotal, 1) : 0,
                    MedianLifetimeHours = Median(completedLifetimes),
                    P90LifetimeHours    = completedLifetimes.Count >= 3 ? PercentileMath.LinearInterpolation(completedLifetimes, 90) : null,
                    AvgReviewCycles     = allCycles.Count > 0 ? Math.Round(allCycles.Average(), 2) : null
                };
            })
            .OrderByDescending(r => r.PrCount)
            .ToList();

        // ── 10. Author breakdown ───────────────────────────────────────────────
        var authorBreakdown = enriched
            .GroupBy(r => r.Author)
            .Select(g =>
            {
                var authorTotal    = g.Count();
                var authorMerged   = g.Count(r => IsCompleted(r.Status));
                var authorAbandoned = g.Count(r => IsAbandoned(r.Status));
                var authorRework    = g.Count(r => IsCompleted(r.Status) && r.ReviewCycles > 1);

                var allLifetimes = g
                    .Where(r => r.LifetimeHours > 0)
                    .Select(r => r.LifetimeHours)
                    .OrderBy(h => h)
                    .ToList();

                var allCycles = g
                    .Where(r => r.ReviewCycles > 0)
                    .Select(r => (double)r.ReviewCycles)
                    .ToList();

                return new PrAuthorBreakdownDto
                {
                    Author             = g.Key,
                    PrCount            = authorTotal,
                    MergePct           = authorTotal > 0 ? Math.Round(100.0 * authorMerged / authorTotal, 1)    : 0,
                    AbandonPct         = authorTotal > 0 ? Math.Round(100.0 * authorAbandoned / authorTotal, 1) : 0,
                    ReworkPct          = authorTotal > 0 ? Math.Round(100.0 * authorRework / authorTotal, 1)    : 0,
                    MedianLifetimeHours = Median(allLifetimes),
                    AvgReviewCycles    = allCycles.Count > 0 ? Math.Round(allCycles.Average(), 2) : null
                };
            })
            .OrderByDescending(a => a.PrCount)
            .ToList();

        // ── 11. Assemble result ────────────────────────────────────────────────
        return new PullRequestInsightsDto
        {
            TeamId              = filter.Context.TeamIds.IsAll ? null : filter.Context.TeamIds.Values.FirstOrDefault(),
            TeamName            = teamName,
            FromDate            = filter.RangeStartUtc ?? DateTimeOffset.MinValue,
            ToDate              = filter.RangeEndUtc ?? DateTimeOffset.MaxValue,
            Summary             = summary,
            Top3Problematic     = top3,
            ScatterPoints       = scatterPoints,
            LongestPrs          = longestPrs,
            RepositoryBreakdown = repoBreakdown,
            AuthorBreakdown     = authorBreakdown
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static double ComputeLifetimeHours(
        PullRequestDto pr,
        DateTimeOffset now)
    {
        var end = pr.CompletedDate ?? now;
        var hours = (end - pr.CreatedDate).TotalHours;
        return Math.Max(0, Math.Round(hours, 2));
    }

    private static string ComputeColorCategory(string status, int reviewCycles) =>
        status.ToLowerInvariant() switch
        {
            "completed" when reviewCycles <= 1 => "merged-clean",
            "completed"                         => "merged-rework",
            "abandoned"                         => "abandoned",
            _                                   => "active"
        };

    private static bool IsCompleted(string status) =>
        string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

    private static bool IsAbandoned(string status) =>
        string.Equals(status, "abandoned", StringComparison.OrdinalIgnoreCase);

    private static PrProblematicEntryDto BuildProblematicEntry(
        PrRecord r,
        double maxLifetime,
        double maxCycles,
        double maxFiles,
        double maxComments,
        string? url = null)
    {
        // Normalise each dimension to [0,1] then compute weighted score
        double normLifetime  = maxLifetime  > 0 ? r.LifetimeHours       / maxLifetime  : 0;
        double normCycles    = maxCycles    > 0 ? r.ReviewCycles         / maxCycles    : 0;
        double normFiles     = maxFiles     > 0 ? r.FilesChanged         / maxFiles     : 0;
        double normComments  = maxComments  > 0 ? r.CommentCount         / maxComments  : 0;

        var score = normLifetime * 0.40
                  + normCycles   * 0.30
                  + normFiles    * 0.20
                  + normComments * 0.10;

        return new PrProblematicEntryDto
        {
            Id            = r.Id,
            Title         = r.Title,
            Repository    = r.Repository,
            Author        = r.Author,
            LifetimeHours = r.LifetimeHours,
            ReviewCycles  = r.ReviewCycles,
            FilesChanged  = r.FilesChanged,
            CommentCount  = r.CommentCount,
            Status        = r.Status,
            RankingScore  = Math.Round(score, 4),
            Url           = url
        };
    }

    private static double? Median(List<double> sorted)
    {
        if (sorted.Count == 0) return null;
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static PullRequestInsightsDto EmptyResult(
        int? teamId,
        string? teamName,
        DateTimeOffset from,
        DateTimeOffset to) =>
        new()
        {
            TeamId   = teamId,
            TeamName = teamName,
            FromDate = from,
            ToDate   = to,
            Summary  = new PrInsightsSummaryDto()
        };

    /// <summary>
    /// Returns the base URL used to construct PR deep links, or null if config is unavailable.
    /// Strips trailing slashes so the URL is ready for concatenation.
    /// </summary>
    private static string? BuildTfsBaseUrl(PullRequestConfigurationInfo? config)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.Url))
            return null;

        return config.Url.TrimEnd('/');
    }

    /// <summary>
    /// Constructs a direct link to a pull request in Azure DevOps / TFS.
    /// Format: {baseUrl}/{project}/_git/{repositoryName}/pullrequest/{prId}
    /// Returns null if baseUrl or project are unavailable.
    /// </summary>
    private static string? BuildPrUrl(
        string? baseUrl,
        string? project,
        string repositoryName,
        int prId)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(project))
            return null;

        return $"{baseUrl}/{project}/_git/{repositoryName}/pullrequest/{prId}";
    }

    // ── Internal projection record ────────────────────────────────────────────

    private sealed record PrRecord(
        int Id,
        string Title,
        string Repository,
        string Author,
        DateTimeOffset CreatedDate,
        string Status,
        double LifetimeHours,
        int ReviewCycles,
        int FilesChanged,
        int CommentCount,
        string ColorCategory);
}
