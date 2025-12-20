using Mediator;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve all cached pull requests.
/// </summary>
public sealed record GetAllPullRequestsQuery : IQuery<IEnumerable<PullRequestDto>>;
