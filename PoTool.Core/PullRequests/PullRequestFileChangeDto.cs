namespace PoTool.Core.PullRequests;

/// <summary>
/// Immutable DTO for pull request file change data.
/// Tracks file-level changes and line statistics.
/// </summary>
public sealed record PullRequestFileChangeDto(
    int PullRequestId,
    int IterationId,
    string FilePath,
    string ChangeType,
    int LinesAdded,
    int LinesDeleted,
    int LinesModified
);
