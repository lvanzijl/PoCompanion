namespace PoTool.Core.Metrics.Models;

/// <summary>
/// Minimal work item data required by canonical story-point and hierarchy rollup services.
/// </summary>
public sealed record CanonicalWorkItem(
    int WorkItemId,
    string WorkItemType,
    int? ParentWorkItemId,
    int? BusinessValue,
    int? StoryPoints);
