using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to detect effort imbalance across teams and sprints.
/// Identifies disproportionate allocations that may lead to bottlenecks.
/// </summary>
public sealed record GetEffortImbalanceQuery(
    string? AreaPathFilter = null,
    int MaxIterations = 10,
    int? DefaultCapacityPerIteration = null,
    double ImbalanceThreshold = 0.3
) : IQuery<EffortImbalanceDto>;
