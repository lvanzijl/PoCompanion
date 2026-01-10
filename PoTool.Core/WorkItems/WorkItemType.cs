namespace PoTool.Core.WorkItems;

/// <summary>
/// Represents the type of work item in the hierarchy.
/// </summary>
public static class WorkItemType
{
    public const string Goal = "goal"; // someone made a typo when introducing the goal work item type
    public const string Objective = "Objective";
    public const string Epic = "Epic";
    public const string Feature = "Feature";
    public const string Pbi = "Product Backlog Item";
    public const string Bug = "Bug";
    public const string Task = "Task";

    /// <summary>
    /// Gets all valid work item types in hierarchical order.
    /// </summary>
    public static readonly string[] AllTypes = new[]
    {
        Goal,
        Objective,
        Epic,
        Feature,
        Pbi,
        Bug,
        Task
    };

    /// <summary>
    /// Gets the color associated with a work item type.
    /// </summary>
    public static string GetColor(string type)
    {
        return type switch
        {
            Goal => "#4CAF50",       // Green
            Objective => "#8BC34A",  // Light green
            Epic => "#FF9800",       // Orange
            Feature => "#9C27B0",    // Purple
            Pbi => "#2196F3",        // Blue
            Bug => "#F44336",        // Red
            Task => "#FFEB3B",       // Yellow
            _ => "#757575"           // Grey for unknown
        };
    }

    /// <summary>
    /// Gets the hierarchical level of a work item type (0 = Goal, 5 = Task).
    /// </summary>
    public static int GetLevel(string type)
    {
        return type switch
        {
            Goal => 0,
            Objective => 1,
            Epic => 2,
            Feature => 3,
            Pbi => 4,
            Bug => 4,                // Same level as PBI
            Task => 5,
            _ => -1
        };
    }
}
