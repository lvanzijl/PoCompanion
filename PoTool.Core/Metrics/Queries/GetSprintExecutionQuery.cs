using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get sprint execution analysis for internal diagnostics.
/// Reconstructs sprint backlog evolution from cached work items and activity events.
/// </summary>
public record GetSprintExecutionQuery(
    int ProductOwnerId,
    int SprintId,
    int? ProductId = null
) : IQuery<SprintExecutionDto>;
