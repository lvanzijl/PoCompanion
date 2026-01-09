using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get velocity trend data across multiple sprints.
/// </summary>
public sealed record GetVelocityTrendQuery(
    string? AreaPath = null,
    int MaxSprints = 10
) : IQuery<VelocityTrendDto>;
