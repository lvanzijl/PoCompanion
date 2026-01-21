using PoTool.Shared.WorkItems;

namespace PoTool.Client.Models;

/// <summary>
/// Represents a validation filter option in the UI.
/// </summary>
public class ValidationFilter
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Count { get; set; }
    
    /// <summary>
    /// The validation category this filter represents, if applicable.
    /// </summary>
    public ValidationCategory? Category { get; set; }
}
