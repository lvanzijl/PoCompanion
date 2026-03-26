using PoTool.Core.Domain.WorkItems;

namespace PoTool.Core.Domain.Models;

/// <summary>
/// Minimal canonical work item data required by CDC/domain rollup services.
/// Irrelevant field combinations are normalized away in the domain model.
/// </summary>
public sealed record CanonicalWorkItem
{
    public CanonicalWorkItem(
        int workItemId,
        string workItemType,
        int? parentWorkItemId,
        int? businessValue,
        int? storyPoints,
        double? timeCriticality = null,
        string? projectNumber = null,
        string? projectElement = null,
        double? effort = null)
    {
        CanonicalWorkItemTypes.EnsureCanonical(workItemType, nameof(workItemType));

        WorkItemId = workItemId;
        WorkItemType = workItemType;
        ParentWorkItemId = parentWorkItemId;
        BusinessValue = businessValue;
        StoryPoints = storyPoints;
        TimeCriticality = WorkItemFieldSemantics.NormalizeTimeCriticality(workItemType, timeCriticality);
        ProjectNumber = WorkItemFieldSemantics.NormalizeProjectNumber(workItemType, projectNumber);
        ProjectElement = WorkItemFieldSemantics.NormalizeProjectElement(workItemType, projectElement);
        Effort = effort;
    }

    public int WorkItemId { get; }

    public string WorkItemType { get; }

    public int? ParentWorkItemId { get; }

    public int? BusinessValue { get; }

    public int? StoryPoints { get; }

    public double? TimeCriticality { get; }

    public string? ProjectNumber { get; }

    public string? ProjectElement { get; }

    public double? Effort { get; }
}
