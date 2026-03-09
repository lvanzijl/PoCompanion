using PoTool.Client.ApiClient;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public static class PlanBoardWorkItemRules
{
    private static readonly string[] PbiAliases = [WorkItemTypeHelper.Pbi, "PBI"];

    public static bool IsPlanBoardItem(string? workItemType)
    {
        var normalized = NormalizeWorkItemType(workItemType);
        return IsPbi(normalized)
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

    public static PlanBoardWorkItemDescriptor CreateDescriptor(WorkItemDto workItem, IReadOnlyDictionary<int, WorkItemDto> workItemLookup)
    {
        return new PlanBoardWorkItemDescriptor(
            workItem.TfsId,
            workItem.Title,
            NormalizeWorkItemType(workItem.Type) ?? workItem.Type,
            ResolveFeatureTitle(workItem, workItemLookup),
            workItem.Effort,
            workItem.IterationPath);
    }

    public static string GetTypeLabel(string? workItemType)
    {
        var normalized = NormalizeWorkItemType(workItemType);
        if (string.Equals(normalized, WorkItemTypeHelper.Bug, StringComparison.OrdinalIgnoreCase))
            return WorkItemTypeHelper.Bug;

        return "PBI";
    }

    private static bool IsPbi(string? workItemType) =>
        PbiAliases.Any(alias => string.Equals(workItemType, alias, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeWorkItemType(string? workItemType) =>
        string.IsNullOrWhiteSpace(workItemType) ? null : workItemType.Trim();
}

public sealed record PlanBoardWorkItemDescriptor(
    int TfsId,
    string Title,
    string WorkItemType,
    string? FeatureTitle,
    int? Effort,
    string IterationPath);
