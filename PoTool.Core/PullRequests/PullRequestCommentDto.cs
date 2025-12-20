namespace PoTool.Core.PullRequests;

/// <summary>
/// Immutable DTO for pull request comment data.
/// Tracks comment lifecycle including resolution.
/// </summary>
public sealed record PullRequestCommentDto(
    int Id,
    int PullRequestId,
    int ThreadId,
    string Author,
    string Content,
    DateTimeOffset CreatedDate,
    DateTimeOffset? UpdatedDate,
    bool IsResolved,
    DateTimeOffset? ResolvedDate,
    string? ResolvedBy
);
