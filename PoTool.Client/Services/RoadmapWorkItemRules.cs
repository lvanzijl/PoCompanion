namespace PoTool.Client.Services;

public static class RoadmapWorkItemRules
{
    public static bool IsEpic(string? workItemType) =>
        string.Equals(workItemType, "Epic", StringComparison.OrdinalIgnoreCase);

    public static bool IsObjective(string? workItemType) =>
        string.Equals(workItemType, "Objective", StringComparison.OrdinalIgnoreCase);

    public static bool HasRoadmapTag(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return false;

        return tags.Split(';')
            .Select(t => t.Trim())
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
}
