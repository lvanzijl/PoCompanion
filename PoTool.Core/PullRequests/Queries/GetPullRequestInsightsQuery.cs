using Mediator;
using PoTool.Core.PullRequests.Filters;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to compute Pull Request Insights for a team over a date range.
/// Uses only cached data — no live TFS/Azure DevOps calls.
/// </summary>
public sealed record GetPullRequestInsightsQuery(
    PullRequestEffectiveFilter EffectiveFilter
) : IQuery<PoTool.Shared.PullRequests.PullRequestInsightsDto>;
