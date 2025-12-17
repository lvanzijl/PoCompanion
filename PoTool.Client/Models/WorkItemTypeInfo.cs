namespace PoTool.Client.Models;

/// <summary>
/// Provides information about work item types including colors and hierarchy.
/// </summary>
public static class WorkItemTypeInfo
{
    public const string Goal = "Goal";
    public const string Objective = "Objective";
    public const string Epic = "Epic";
    public const string Feature = "Feature";
    public const string Pbi = "Product Backlog Item";
    public const string Task = "Task";

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
            Task => 5,
            _ => -1
        };
    }

    /// <summary>
    /// Gets a short abbreviation for the work item type.
    /// </summary>
    public static string GetAbbreviation(string type)
    {
        return type switch
        {
            Goal => "G",
            Objective => "O",
            Epic => "E",
            Feature => "F",
            Pbi => "PBI",
            Task => "T",
            _ => "?"
        };
    }
}
