using Mediator;

using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve aggregated metrics for pull requests.
/// </summary>
/// <param name="ProductIds">Optional list of product IDs to filter by. If null or empty, returns metrics for all products.</param>
public sealed record GetPullRequestMetricsQuery(
    List<int>? ProductIds = null
) : IQuery<IEnumerable<PullRequestMetricsDto>>;
