namespace PoTool.Shared.PullRequests;

/// <summary>
/// Immutable DTO for pull request iteration data.
/// Represents a rework cycle within a pull request.
/// </summary>
public sealed record PullRequestIterationDto(
    int PullRequestId,
    int IterationNumber,
    DateTimeOffset CreatedDate,
    DateTimeOffset UpdatedDate,
    int CommitCount,
    int ChangeCount
);
