namespace PoTool.Client.Models;

/// <summary>
/// Helper for work item type information.
/// NOTE: This mirrors Core.WorkItems.WorkItemType but exists in Client for architectural boundaries.
/// Keep color mappings and type constants in sync with Core definition.
/// </summary>
public static class WorkItemTypeHelper
{
    public const string Goal = "Goal";
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
}
