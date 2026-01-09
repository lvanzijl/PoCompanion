namespace PoTool.Shared.PullRequests;

/// <summary>
/// DTO representing PR review bottleneck analysis across multiple pull requests.
/// Identifies slow reviewers and review process inefficiencies.
/// </summary>
public sealed record PRReviewBottleneckDto(
    IReadOnlyList<ReviewerPerformance> ReviewerPerformances,
    IReadOnlyList<PRWaitingForReview> PRsWaitingLongest,
    ReviewMetricsSummary Summary,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Performance metrics for a specific reviewer.
/// </summary>
public sealed record ReviewerPerformance(
    string ReviewerName,
    int TotalReviewsAssigned,
    int ReviewsCompleted,
    double AverageResponseTimeHours,
    double MedianResponseTimeHours,
    int PRsWaitingForReview,
    ReviewerStatus Status
);

/// <summary>
/// Pull request waiting for review with timing information.
/// </summary>
public sealed record PRWaitingForReview(
    int PullRequestId,
    string Title,
    string Author,
    DateTimeOffset CreatedDate,
    double HoursWaiting,
    IReadOnlyList<string> PendingReviewers
);

/// <summary>
/// Summary statistics for the review process.
/// </summary>
public sealed record ReviewMetricsSummary(
    double AverageTimeToFirstReviewHours,
    double AverageTimeToCompleteReviewsHours,
    int TotalPRsPendingReview,
    string BottleneckReviewer,
    string FastestReviewer
);

/// <summary>
/// Status of a reviewer based on their performance.
/// </summary>
public enum ReviewerStatus
{
    Fast,       // Average response < 4 hours
    Normal,     // Average response 4-24 hours
    Slow,       // Average response 24-48 hours
    Bottleneck  // Average response > 48 hours
}
