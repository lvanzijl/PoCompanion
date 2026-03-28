using Mediator;
using PoTool.Core.PullRequests.Filters;
using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve filtered pull requests based on various criteria.
/// </summary>
public sealed record GetFilteredPullRequestsQuery(
    PullRequestEffectiveFilter EffectiveFilter
) : IQuery<IEnumerable<PullRequestDto>>;
