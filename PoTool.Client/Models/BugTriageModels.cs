namespace PoTool.Client.Models;

/// <summary>
/// Client model for a triage tag filter.
/// </summary>
public class TriageTagFilter
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

/// <summary>
/// Enum for tag filter match mode.
/// </summary>
public enum TagMatchMode
{
    /// <summary>
    /// Bug must have ANY of the selected tags (OR logic).
    /// </summary>
    Any,
    
    /// <summary>
    /// Bug must have ALL of the selected tags (AND logic).
    /// </summary>
    All
}

/// <summary>
/// Client model for criticality options.
/// </summary>
public static class BugCriticality
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";

    public static readonly List<string> AllValues = new()
    {
        Critical,
        High,
        Medium,
        Low
    };

    /// <summary>
    /// Gets the display order for a criticality (lower number = higher priority).
    /// </summary>
    public static int GetOrder(string criticality)
    {
        return criticality switch
        {
            Critical => 1,
            High => 2,
            Medium => 3,
            Low => 4,
            _ => 5
        };
    }
}
