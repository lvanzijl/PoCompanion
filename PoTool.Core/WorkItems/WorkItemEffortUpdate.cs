namespace PoTool.Core.WorkItems;

/// <summary>
/// Represents an effort update for a single work item in a bulk operation.
/// </summary>
public sealed record WorkItemEffortUpdate(
    int WorkItemId,
    int EffortValue
);
