using Mediator;
using PoTool.Core.PullRequests.Filters;
using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve aggregated metrics for pull requests.
/// </summary>
public sealed record GetPullRequestMetricsQuery(
    PullRequestEffectiveFilter EffectiveFilter
) : IQuery<IEnumerable<PullRequestMetricsDto>>;
