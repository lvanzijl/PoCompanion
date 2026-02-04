namespace PoTool.Shared.WorkItems;

/// <summary>
/// Represents a relation between work items.
/// Extracted from TFS work item relations array.
/// </summary>
public sealed record WorkItemRelation(
    string LinkType,
    int? TargetWorkItemId,
    string? Url = null
);
