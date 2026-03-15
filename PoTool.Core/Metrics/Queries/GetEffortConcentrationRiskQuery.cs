using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to identify concentration risk in effort distribution.
/// Uses fixed concentration bands on share of total effort:
/// None &lt; 25%, Low 25-40%, Medium 40-60%, High 60-80%, Critical ≥ 80%.
/// ConcentrationThreshold is retained for backward-compatible callers but is ignored by the stable subset.
/// </summary>
public sealed record GetEffortConcentrationRiskQuery(
    string? AreaPathFilter = null,
    int MaxIterations = 10,
    double ConcentrationThreshold = 0.5
) : IQuery<EffortConcentrationRiskDto>;
