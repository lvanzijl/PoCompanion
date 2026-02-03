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
/// [OBSOLETE] Client model for severity options.
/// This class is deprecated and should not be used.
/// Severity values should come directly from TFS field allowed values via the API.
/// See WorkItemService.GetBugSeverityOptionsAsync() for the correct approach.
/// </summary>
[Obsolete("Do not use hardcoded severity values. Get severity options from TFS via WorkItemService.GetBugSeverityOptionsAsync()", error: true)]
public static class BugSeverity
{
    [Obsolete("Do not use hardcoded severity values", error: true)]
    public const string Critical = "Critical";
    
    [Obsolete("Do not use hardcoded severity values", error: true)]
    public const string High = "High";
    
    [Obsolete("Do not use hardcoded severity values", error: true)]
    public const string Medium = "Medium";
    
    [Obsolete("Do not use hardcoded severity values", error: true)]
    public const string Low = "Low";

    [Obsolete("Do not use hardcoded severity values", error: true)]
    public static readonly List<string> AllValues = new()
    {
        Critical,
        High,
        Medium,
        Low
    };

    /// <summary>
    /// Gets the display order for a severity (lower number = higher priority).
    /// </summary>
    [Obsolete("Do not use hardcoded severity values", error: true)]
    public static int GetOrder(string severity)
    {
        return severity switch
        {
            Critical => 1,
            High => 2,
            Medium => 3,
            Low => 4,
            _ => 5
        };
    }
}
