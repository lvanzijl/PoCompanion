using Mediator;
using PoTool.Core.Metrics.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get sprint capacity planning analysis for a specific iteration.
/// </summary>
public sealed record GetSprintCapacityPlanQuery(
    SprintEffectiveFilter EffectiveFilter,
    int? DefaultCapacityPerPerson = null
) : IQuery<SprintCapacityPlanDto?>
{
    public GetSprintCapacityPlanQuery(
        string iterationPath,
        int? DefaultCapacityPerPerson = null)
        : this(SprintFilterFactory.ForIterationPath(iterationPath), DefaultCapacityPerPerson)
    {
    }
}
