namespace PoTool.Core.Domain.WorkItems;

/// <summary>
/// Canonical work item type names used by CDC services.
/// </summary>
public static class CanonicalWorkItemTypes
{
    public const string Epic = "Epic";
    public const string Feature = "Feature";
    public const string ProductBacklogItem = "Product Backlog Item";
    public const string PbiShort = "PBI";
    public const string UserStory = "User Story";
    public const string Bug = "Bug";
    public const string Task = "Task";

    public static bool IsFeature(string workItemType)
    {
        return workItemType.Equals(Feature, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAuthoritativePbi(string workItemType)
    {
        return workItemType.Equals(ProductBacklogItem, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(PbiShort, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(UserStory, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFeatureProgressContributor(string workItemType)
    {
        return IsAuthoritativePbi(workItemType)
            || workItemType.Equals(Bug, StringComparison.OrdinalIgnoreCase);
    }
}
