using PoTool.Client.ApiClient;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public static class PlanBoardWorkItemRules
{
    public static bool IsPlanBoardItem(string? workItemType)
    {
        var normalized = NormalizeWorkItemType(workItemType);
        return string.Equals(normalized, WorkItemTypeHelper.Pbi, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "PBI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, WorkItemTypeHelper.Bug, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ResolveFeatureTitle(WorkItemDto workItem, IReadOnlyDictionary<int, WorkItemDto> workItemLookup)
    {
        if (!workItem.ParentTfsId.HasValue)
            return null;

        return workItemLookup.TryGetValue(workItem.ParentTfsId.Value, out var parent)
            ? parent.Title
            : null;
    }

    public static string GetTypeLabel(string? workItemType)
    {
        var normalized = NormalizeWorkItemType(workItemType);
        if (string.Equals(normalized, WorkItemTypeHelper.Bug, StringComparison.OrdinalIgnoreCase))
            return WorkItemTypeHelper.Bug;

        return "PBI";
    }

    private static string? NormalizeWorkItemType(string? workItemType) =>
        string.IsNullOrWhiteSpace(workItemType) ? null : workItemType.Trim();
}
