using Mediator;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get backlog health metrics for a specific iteration.
/// </summary>
public sealed record GetBacklogHealthQuery(
    string IterationPath
) : IQuery<BacklogHealthDto?>;
