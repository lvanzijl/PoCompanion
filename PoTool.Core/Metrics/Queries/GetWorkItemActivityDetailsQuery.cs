using Mediator;
using PoTool.Core.Metrics.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to retrieve activity details for a selected work item and its descendants.
/// </summary>
public sealed record GetWorkItemActivityDetailsQuery(
    int ProductOwnerId,
    int WorkItemId,
    SprintEffectiveFilter EffectiveFilter
) : IQuery<WorkItemActivityDetailsDto?>
{
    public GetWorkItemActivityDetailsQuery(
        int ProductOwnerId,
        int WorkItemId,
        DateTimeOffset? PeriodStartUtc,
        DateTimeOffset? PeriodEndUtc)
        : this(ProductOwnerId, WorkItemId, SprintFilterFactory.ForDateRange(PeriodStartUtc, PeriodEndUtc))
    {
    }
}
