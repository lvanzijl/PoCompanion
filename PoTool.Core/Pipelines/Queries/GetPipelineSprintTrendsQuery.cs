using Mediator;
using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to aggregate pipeline runs per sprint for a set of sprint IDs.
/// </summary>
public sealed record GetPipelineSprintTrendsQuery(
    int ProductOwnerId,
    IReadOnlyList<int> SprintIds,
    List<int>? ProductIds = null
) : IQuery<GetPipelineSprintTrendsResponse>;
