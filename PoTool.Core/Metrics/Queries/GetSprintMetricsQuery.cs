using Mediator;
using PoTool.Core.Metrics.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get historical sprint metrics for a specific sprint path.
/// The sprint path selects the sprint window and the commitment reconstruction target.
/// </summary>
public sealed record GetSprintMetricsQuery(
    SprintEffectiveFilter EffectiveFilter
) : IQuery<SprintMetricsDto?>
{
    public string? IterationPath => EffectiveFilter.IterationPath;

    public GetSprintMetricsQuery(string iterationPath)
        : this(SprintFilterFactory.ForIterationPath(iterationPath))
    {
    }
}
