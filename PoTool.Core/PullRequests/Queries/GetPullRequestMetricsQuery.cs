using Mediator;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve aggregated metrics for all pull requests.
/// </summary>
public sealed record GetPullRequestMetricsQuery : IQuery<IEnumerable<PullRequestMetricsDto>>;
