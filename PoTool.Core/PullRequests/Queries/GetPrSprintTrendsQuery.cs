using Mediator;
using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to aggregate PR metrics per sprint for a set of sprint IDs.
/// Filtering priority: ProductIds (explicit) takes precedence over TeamId (implicit via ProductTeamLinks).
/// If neither is set, all PRs are included.
/// Sprint mapping rule: a PR belongs to a sprint if its CreatedDateUtc falls within [StartDateUtc, EndDateUtc).
/// </summary>
public sealed record GetPrSprintTrendsQuery(
    IReadOnlyList<int> SprintIds,
    List<int>? ProductIds = null,
    int? TeamId = null
) : IQuery<GetPrSprintTrendsResponse>;
