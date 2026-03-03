using Mediator;
using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to aggregate pipeline runs per sprint for a set of sprint IDs.
/// Filtering priority: ProductIds (explicit) takes precedence over TeamId (implicit via ProductTeamLinks).
/// If neither is set, all pipeline definitions are included.
/// </summary>
public sealed record GetPipelineSprintTrendsQuery(
    int ProductOwnerId,
    IReadOnlyList<int> SprintIds,
    List<int>? ProductIds = null,
    int? TeamId = null
) : IQuery<GetPipelineSprintTrendsResponse>;
