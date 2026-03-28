using Mediator;
using PoTool.Core.Metrics.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get sprint execution analysis for internal diagnostics.
/// Reconstructs sprint backlog evolution from cached work items and activity events.
/// </summary>
public record GetSprintExecutionQuery(
    int ProductOwnerId,
    SprintEffectiveFilter EffectiveFilter
) : IQuery<SprintExecutionDto>
{
    public GetSprintExecutionQuery(
        int ProductOwnerId,
        int SprintId,
        int? ProductId = null)
        : this(ProductOwnerId, SprintFilterFactory.ForSprintId(SprintId, ProductId.HasValue ? [ProductId.Value] : null))
    {
    }
}
