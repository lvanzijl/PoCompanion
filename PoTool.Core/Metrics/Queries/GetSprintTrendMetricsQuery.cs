using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get sprint trend metrics for a range of sprints.
/// </summary>
public record GetSprintTrendMetricsQuery(
    int ProductOwnerId,
    IReadOnlyList<int> SprintIds,
    bool Recompute = false
) : IQuery<GetSprintTrendMetricsResponse>;
