namespace PoTool.Client.Models;

/// <summary>
/// Client-side DTO for pull request data.
/// </summary>
public sealed record PullRequestDto(
    int Id,
    string RepositoryName,
    string Title,
    string CreatedBy,
    DateTimeOffset CreatedDate,
    DateTimeOffset? CompletedDate,
    string Status,
    string IterationPath,
    string SourceBranch,
    string TargetBranch,
    DateTimeOffset RetrievedAt
);
