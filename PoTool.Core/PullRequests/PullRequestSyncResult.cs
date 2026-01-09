using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests;

/// <summary>
/// Result of a bulk pull request sync operation.
/// Contains all pull requests with their related iterations, comments, and file changes.
/// This prevents N+1 queries by returning all related data in a single call.
/// </summary>
public sealed record PullRequestSyncResult(
    IReadOnlyList<PullRequestDto> PullRequests,
    IReadOnlyList<PullRequestIterationDto> Iterations,
    IReadOnlyList<PullRequestCommentDto> Comments,
    IReadOnlyList<PullRequestFileChangeDto> FileChanges,
    int TfsCallCount // Performance instrumentation: tracks actual TFS API calls made
);
