using Mediator;
using PoTool.Core.PullRequests.Filters;
using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to aggregate PR metrics per sprint for a set of sprint IDs.
/// Sprint mapping rule: a PR belongs to a sprint if its CreatedDateUtc falls within [StartDateUtc, EndDateUtc).
/// </summary>
public sealed record GetPrSprintTrendsQuery(
    PullRequestEffectiveFilter EffectiveFilter
) : IQuery<GetPrSprintTrendsResponse>;
