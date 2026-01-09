using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get metrics for a specific sprint by iteration path.
/// </summary>
public sealed record GetSprintMetricsQuery(
    string IterationPath
) : IQuery<SprintMetricsDto?>;
