namespace PoTool.Core.Domain.Cdc.Sprints;

/// <summary>
/// Canonical committed sprint membership for one work item at the commitment boundary.
/// </summary>
public sealed record SprintCommitment(
    int SprintId,
    int WorkItemId,
    DateTimeOffset CommitmentTimestamp);

/// <summary>
/// Canonical scope-added signal for one work item entering a sprint after commitment.
/// </summary>
public sealed record SprintScopeAdded(
    int SprintId,
    int WorkItemId,
    DateTimeOffset AddedAt);

/// <summary>
/// Canonical scope-removed signal for one work item leaving a sprint after commitment.
/// </summary>
public sealed record SprintScopeRemoved(
    int SprintId,
    int WorkItemId,
    DateTimeOffset RemovedAt);

/// <summary>
/// Canonical completion signal for the first Done transition inside a sprint window.
/// </summary>
public sealed record SprintCompletion(
    int SprintId,
    int WorkItemId,
    DateTimeOffset CompletedAt);

/// <summary>
/// Canonical spillover signal for one committed work item moving directly into the next sprint.
/// </summary>
public sealed record SprintSpillover(
    int SprintId,
    int WorkItemId,
    DateTimeOffset SpilloverAt);
