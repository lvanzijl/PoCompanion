using Mediator;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get effort estimation quality metrics.
/// Compares historical estimates vs actuals to measure estimation accuracy.
/// </summary>
public sealed record GetEffortEstimationQualityQuery(
    string? AreaPath = null,
    int MaxIterations = 10
) : IQuery<EffortEstimationQualityDto>;
