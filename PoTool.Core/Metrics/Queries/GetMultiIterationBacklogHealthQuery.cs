using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get aggregated backlog health across multiple iterations.
/// Supports filtering by product IDs (preferred), area path, and limiting the number of iterations.
/// When multiple product IDs are provided, work items are deduplicated by TfsId to prevent double-counting.
/// </summary>
public sealed record GetMultiIterationBacklogHealthQuery(
    int[]? ProductIds = null,
    string? AreaPath = null,
    int MaxIterations = 5
) : IQuery<MultiIterationBacklogHealthDto>;
