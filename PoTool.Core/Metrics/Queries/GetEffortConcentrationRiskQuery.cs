using Mediator;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to identify concentration risk in effort distribution.
/// Flags scenarios where too much effort is concentrated in a single feature or area.
/// </summary>
public sealed record GetEffortConcentrationRiskQuery(
    string? AreaPathFilter = null,
    int MaxIterations = 10,
    double ConcentrationThreshold = 0.5
) : IQuery<EffortConcentrationRiskDto>;
