namespace PoTool.Core.Domain.Models;

/// <summary>
/// Minimal work item data required by canonical story-point and hierarchy rollup services.
/// </summary>
public sealed record CanonicalWorkItem(
    int WorkItemId,
    string WorkItemType,
    int? ParentWorkItemId,
    int? BusinessValue,
    int? StoryPoints,
    double? TimeCriticality = null,
    string? ProjectNumber = null,
    string? ProjectElement = null);
