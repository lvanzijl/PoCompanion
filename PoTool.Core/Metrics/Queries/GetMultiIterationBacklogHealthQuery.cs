using Mediator;

using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get aggregated backlog health across multiple iterations.
/// Supports filtering by area path and limiting the number of iterations.
/// </summary>
public sealed record GetMultiIterationBacklogHealthQuery(
    string? AreaPath = null,
    int MaxIterations = 5
) : IQuery<MultiIterationBacklogHealthDto>;
