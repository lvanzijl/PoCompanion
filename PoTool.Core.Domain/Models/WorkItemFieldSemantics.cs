using PoTool.Core.Domain.WorkItems;

namespace PoTool.Core.Domain.Models;

/// <summary>
/// Canonical field-relevance rules for CDC/domain work item models.
/// </summary>
public static class WorkItemFieldSemantics
{
    public static bool IsProjectNumberRelevant(string workItemType)
        => IsEpic(workItemType);

    public static bool IsProjectElementRelevant(string workItemType)
        => IsEpic(workItemType);

    public static bool IsTimeCriticalityRelevant(string workItemType)
        => CanonicalWorkItemTypes.IsFeature(workItemType);

    public static string? NormalizeProjectNumber(string workItemType, string? projectNumber)
        => IsProjectNumberRelevant(workItemType) ? projectNumber : null;

    public static string? NormalizeProjectElement(string workItemType, string? projectElement)
        => IsProjectElementRelevant(workItemType) ? projectElement : null;

    public static double? NormalizeTimeCriticality(string workItemType, double? timeCriticality)
        => IsTimeCriticalityRelevant(workItemType) && IsValidTimeCriticality(timeCriticality)
            ? timeCriticality
            : null;

    public static bool IsValidTimeCriticality(double? timeCriticality)
        => !timeCriticality.HasValue || (timeCriticality.Value >= 0d && timeCriticality.Value <= 100d);

    private static bool IsEpic(string workItemType)
        => workItemType == CanonicalWorkItemTypes.Epic;
}
