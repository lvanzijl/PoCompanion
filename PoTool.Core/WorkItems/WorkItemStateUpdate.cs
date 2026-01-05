namespace PoTool.Core.WorkItems;

/// <summary>
/// Represents a state update for a single work item in a bulk operation.
/// </summary>
public sealed record WorkItemStateUpdate(
    int WorkItemId,
    string NewState
);
