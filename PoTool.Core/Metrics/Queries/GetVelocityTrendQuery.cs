using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get velocity trend data across multiple sprints.
/// Supports filtering by product IDs (preferred) or area path.
/// When multiple product IDs are provided, work items are deduplicated by TfsId to prevent double-counting.
/// </summary>
public sealed record GetVelocityTrendQuery(
    int[]? ProductIds = null,
    string? AreaPath = null,
    int MaxSprints = 10
) : IQuery<VelocityTrendDto>;
