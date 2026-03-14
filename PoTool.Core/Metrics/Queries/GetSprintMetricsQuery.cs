using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get historical sprint metrics for a specific sprint path.
/// The sprint path selects the sprint window and the commitment reconstruction target.
/// </summary>
public sealed record GetSprintMetricsQuery(
    string IterationPath
) : IQuery<SprintMetricsDto?>;
