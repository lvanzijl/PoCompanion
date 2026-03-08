namespace PoTool.Client.Services;

public static class RoadmapWorkItemRules
{
    public static bool IsEpic(string? workItemType) =>
        IsWorkItemType(workItemType, "Epic");

    public static bool IsObjective(string? workItemType) =>
        IsWorkItemType(workItemType, "Objective");

    public static bool HasRoadmapTag(string? tags)
    {
        return GetTagList(tags)
            .Any(t => t.Equals("roadmap", StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> GetTagList(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return [];

        return tags.Split(';')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
    }

    // TFS runtime data can include incidental whitespace, but roadmap discovery should still require
    // an explicit exact type/tag match after normalization so overview and editor stay aligned.
    public static string? NormalizeWorkItemType(string? workItemType) =>
        string.IsNullOrWhiteSpace(workItemType) ? null : workItemType.Trim();

    private static bool IsWorkItemType(string? workItemType, string expectedType) =>
        string.Equals(NormalizeWorkItemType(workItemType), expectedType, StringComparison.OrdinalIgnoreCase);
}
