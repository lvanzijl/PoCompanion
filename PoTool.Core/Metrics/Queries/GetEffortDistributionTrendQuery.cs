using Mediator;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to analyze effort distribution trends over time.
/// Shows how distribution patterns change sprint by sprint.
/// </summary>
public sealed record GetEffortDistributionTrendQuery(
    string? AreaPathFilter = null,
    int MaxIterations = 10,
    int? DefaultCapacityPerIteration = null
) : IQuery<EffortDistributionTrendDto>;
