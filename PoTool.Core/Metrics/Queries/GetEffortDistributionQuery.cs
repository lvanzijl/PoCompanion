using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get effort distribution heat map data.
/// </summary>
public sealed record GetEffortDistributionQuery(
    string? AreaPathFilter = null,
    int MaxIterations = 10,
    int? DefaultCapacityPerIteration = null
) : IQuery<EffortDistributionDto>;
