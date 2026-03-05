namespace PoTool.Shared.PullRequests;

/// <summary>
/// Response for the Pull Request Insights endpoint.
/// Contains aggregated PR health metrics for a selected team and date range.
/// All data is computed from the local cache — no live TFS/Azure DevOps calls.
/// </summary>
public sealed class PullRequestInsightsDto
{
    /// <summary>Team ID used for this result. Null when no team filter was applied.</summary>
    public int? TeamId { get; set; }

    /// <summary>Team display name.</summary>
    public string? TeamName { get; set; }

    /// <summary>Start of the date range.</summary>
    public DateTimeOffset FromDate { get; set; }

    /// <summary>End of the date range.</summary>
    public DateTimeOffset ToDate { get; set; }

    /// <summary>Global health summary chips (top section).</summary>
    public PrInsightsSummaryDto Summary { get; set; } = new();

    /// <summary>
    /// Top 3 PRs most likely causing workflow friction.
    /// Ranked by composite score: lifetime × review cycles × file changes × comment count.
    /// </summary>
    public IReadOnlyList<PrProblematicEntryDto> Top3Problematic { get; set; }
        = Array.Empty<PrProblematicEntryDto>();

    /// <summary>
    /// All PR scatter points for the team in the selected date range.
    /// Used to render the PullRequestScatterSvg component.
    /// </summary>
    public IReadOnlyList<PrScatterPointDto> ScatterPoints { get; set; }
        = Array.Empty<PrScatterPointDto>();

    /// <summary>
    /// Top 20 longest-lived PRs, sorted by lifetime descending.
    /// Used to render the Longest PR Table.
    /// </summary>
    public IReadOnlyList<PrProblematicEntryDto> LongestPrs { get; set; }
        = Array.Empty<PrProblematicEntryDto>();

    /// <summary>
    /// Per-repository workflow statistics.
    /// Sorted by PR count descending.
    /// </summary>
    public IReadOnlyList<PrRepositoryBreakdownDto> RepositoryBreakdown { get; set; }
        = Array.Empty<PrRepositoryBreakdownDto>();

    /// <summary>
    /// Per-author workflow statistics.
    /// Sorted by PR count descending.
    /// </summary>
    public IReadOnlyList<PrAuthorBreakdownDto> AuthorBreakdown { get; set; }
        = Array.Empty<PrAuthorBreakdownDto>();
}

/// <summary>
/// High-level PR health summary shown as chips at the top of the page.
/// All percentages are relative to TotalPrs.
/// </summary>
public sealed class PrInsightsSummaryDto
{
    /// <summary>Total PRs in scope.</summary>
    public int TotalPrs { get; set; }

    /// <summary>Percentage of PRs that were merged (status = "completed").</summary>
    public double MergeRatePct { get; set; }

    /// <summary>Percentage of PRs that were abandoned (status = "abandoned").</summary>
    public double AbandonRatePct { get; set; }

    /// <summary>
    /// Percentage of merged PRs that had more than one iteration
    /// (proxy for "changes requested" — reviewer required a rework cycle).
    /// </summary>
    public double ChangesRequestedRatePct { get; set; }

    /// <summary>Median PR lifetime in hours across all PRs in scope. Null when no data.</summary>
    public double? MedianLifetimeHours { get; set; }

    /// <summary>90th-percentile PR lifetime in hours. Null when sample &lt; 3 PRs.</summary>
    public double? P90LifetimeHours { get; set; }
}

/// <summary>
/// A single PR entry used in both the Top 3 problematic list and the Longest PR table.
/// </summary>
public sealed class PrProblematicEntryDto
{
    /// <summary>PR identifier.</summary>
    public int Id { get; set; }

    /// <summary>PR title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Repository name.</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>Author (CreatedBy).</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Lifetime in hours (CompletedDate - CreatedDate for completed/abandoned; Now - CreatedDate for active).</summary>
    public double LifetimeHours { get; set; }

    /// <summary>Number of review cycles (iteration count).</summary>
    public int ReviewCycles { get; set; }

    /// <summary>Number of distinct files changed across all iterations.</summary>
    public int FilesChanged { get; set; }

    /// <summary>Total comment count.</summary>
    public int CommentCount { get; set; }

    /// <summary>PR status: "completed", "abandoned", or "active".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Composite friction score used for Top 3 ranking.</summary>
    public double RankingScore { get; set; }
}

/// <summary>
/// A single data point for the PR scatter chart.
/// X = PR creation time; Y = PR lifetime in hours.
/// </summary>
public sealed class PrScatterPointDto
{
    /// <summary>PR identifier.</summary>
    public int Id { get; set; }

    /// <summary>PR title (shown in tooltip).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Repository name.</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>Author (CreatedBy).</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>PR creation time — X axis.</summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>PR lifetime in hours — Y axis.</summary>
    public double LifetimeHours { get; set; }

    /// <summary>Number of review cycles (iteration count).</summary>
    public int ReviewCycles { get; set; }

    /// <summary>Number of distinct files changed.</summary>
    public int FilesChanged { get; set; }

    /// <summary>Total comment count.</summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Point color category:
    ///   "merged-clean"    → green  (completed, ≤1 iteration)
    ///   "merged-rework"   → yellow (completed, &gt;1 iteration)
    ///   "abandoned"       → red    (abandoned)
    ///   "active"          → grey   (still open)
    /// </summary>
    public string ColorCategory { get; set; } = string.Empty;
}

/// <summary>
/// Per-repository PR workflow statistics for the repository breakdown table.
/// </summary>
public sealed class PrRepositoryBreakdownDto
{
    /// <summary>Repository name.</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>Total PR count in this repository.</summary>
    public int PrCount { get; set; }

    /// <summary>Percentage of PRs that were merged.</summary>
    public double MergePct { get; set; }

    /// <summary>Percentage of PRs that were abandoned.</summary>
    public double AbandonPct { get; set; }

    /// <summary>Median PR lifetime in hours. Null when no completed PRs.</summary>
    public double? MedianLifetimeHours { get; set; }

    /// <summary>P90 PR lifetime in hours. Null when sample &lt; 3 completed PRs.</summary>
    public double? P90LifetimeHours { get; set; }

    /// <summary>Average review cycles (iterations) across all PRs. Null when no iteration data.</summary>
    public double? AvgReviewCycles { get; set; }
}

/// <summary>
/// Per-author PR workflow statistics for the author breakdown table.
/// Sorted by PR count descending.
/// </summary>
public sealed class PrAuthorBreakdownDto
{
    /// <summary>Author display name (CreatedBy).</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Total PR count for this author.</summary>
    public int PrCount { get; set; }

    /// <summary>Percentage of PRs that were merged (status = "completed").</summary>
    public double MergePct { get; set; }

    /// <summary>Percentage of PRs that were abandoned.</summary>
    public double AbandonPct { get; set; }

    /// <summary>
    /// Percentage of merged PRs that had more than one iteration (rework proxy).
    /// Relative to TotalPrs (not just merged).
    /// </summary>
    public double ReworkPct { get; set; }

    /// <summary>Median PR lifetime in hours. Null when no data.</summary>
    public double? MedianLifetimeHours { get; set; }

    /// <summary>Average review cycles across all PRs. Null when no iteration data.</summary>
    public double? AvgReviewCycles { get; set; }
}
