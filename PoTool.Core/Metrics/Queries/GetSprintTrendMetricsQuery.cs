using Mediator;
using PoTool.Core.Metrics.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get sprint trend metrics for a range of sprints.
/// </summary>
public record GetSprintTrendMetricsQuery(
    int ProductOwnerId,
    SprintEffectiveFilter EffectiveFilter,
    bool Recompute = false,
    bool IncludeDetails = true
) : IQuery<GetSprintTrendMetricsResponse>
{
    public GetSprintTrendMetricsQuery(
        int ProductOwnerId,
        IReadOnlyList<int> SprintIds,
        bool Recompute = false,
        bool IncludeDetails = true)
        : this(ProductOwnerId, SprintFilterFactory.ForSprintIds(SprintIds), Recompute, IncludeDetails)
    {
    }
}
