namespace PoTool.Core.PullRequests;

/// <summary>
/// Immutable DTO for aggregated pull request metrics.
/// Provides calculated analytics for a pull request.
/// </summary>
public sealed record PullRequestMetricsDto(
    int PullRequestId,
    string Title,
    string CreatedBy,
    DateTimeOffset CreatedDate,
    DateTimeOffset? CompletedDate,
    string Status,
    string IterationPath,
    TimeSpan TotalTimeOpen,
    TimeSpan? EffectiveWorkTime,
    int IterationCount,
    int CommentCount,
    int UnresolvedCommentCount,
    int TotalFileCount,
    int TotalLinesAdded,
    int TotalLinesDeleted,
    double AverageLinesPerFile
);
