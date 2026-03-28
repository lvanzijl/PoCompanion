using Mediator;
using PoTool.Core.Metrics.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get backlog health metrics for a specific iteration.
/// </summary>
public sealed record GetBacklogHealthQuery(
    SprintEffectiveFilter EffectiveFilter
) : IQuery<BacklogHealthDto?>
{
    public GetBacklogHealthQuery(string iterationPath)
        : this(SprintFilterFactory.ForIterationPath(iterationPath))
    {
    }
}
