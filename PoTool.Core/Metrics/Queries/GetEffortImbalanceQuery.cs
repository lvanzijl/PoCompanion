using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to detect effort imbalance across teams and sprints.
/// Uses deviation from mean effort per bucket, with threshold-relative risk bands.
/// DefaultCapacityPerIteration adds sprint utilization context only and does not change classification.
/// </summary>
public sealed record GetEffortImbalanceQuery(
    string? AreaPathFilter = null,
    int MaxIterations = 10,
    int? DefaultCapacityPerIteration = null,
    double ImbalanceThreshold = 0.3
) : IQuery<EffortImbalanceDto>;
