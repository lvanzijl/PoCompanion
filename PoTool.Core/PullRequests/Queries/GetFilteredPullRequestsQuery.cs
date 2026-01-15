using Mediator;

using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve filtered pull requests based on various criteria.
/// </summary>
public sealed record GetFilteredPullRequestsQuery(
    List<int>? ProductIds = null,
    string? IterationPath = null,
    string? CreatedBy = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? Status = null
) : IQuery<IEnumerable<PullRequestDto>>;
