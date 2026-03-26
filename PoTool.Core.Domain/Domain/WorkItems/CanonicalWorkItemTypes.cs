namespace PoTool.Core.Domain.WorkItems;

/// <summary>
/// Canonical work item type names used by CDC services.
/// </summary>
public static class CanonicalWorkItemTypes
{
    public const string Goal = "Goal";
    public const string Objective = "Objective";
    public const string Epic = "Epic";
    public const string Feature = "Feature";
    public const string Pbi = "PBI";
    public const string ProductBacklogItem = Pbi;
    public const string PbiShort = Pbi;
    public const string UserStory = Pbi;
    public const string Bug = "Bug";
    public const string Task = "Task";
    public const string Other = "Other";

    public static bool IsFeature(string workItemType)
    {
        return workItemType == Feature;
    }

    public static bool IsEpic(string workItemType)
    {
        return workItemType == Epic;
    }

    public static bool IsAuthoritativePbi(string workItemType)
    {
        return workItemType == Pbi;
    }

    public static bool IsFeatureProgressContributor(string workItemType)
    {
        return IsAuthoritativePbi(workItemType)
            || workItemType == Bug;
    }

    public static bool IsCanonical(string workItemType)
    {
        return workItemType is Goal or Objective or Epic or Feature or Pbi or Bug or Task or Other;
    }

    public static void EnsureCanonical(string workItemType, string paramName)
    {
        if (!IsCanonical(workItemType))
        {
            throw new ArgumentException(
                $"Work item type '{workItemType}' is not a canonical domain work item type.",
                paramName);
        }
    }
}
