using Mediator;
using PoTool.Core.PullRequests.Filters;
using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to compute PR Delivery Insights for a team over a date range.
/// PRs are classified by their linked work items and traced to Features and Epics.
/// Uses only cached data — no live TFS/Azure DevOps calls.
/// </summary>
public sealed record GetPrDeliveryInsightsQuery(
    PullRequestEffectiveFilter EffectiveFilter
) : IQuery<PrDeliveryInsightsDto>;
