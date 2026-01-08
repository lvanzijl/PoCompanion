namespace PoTool.Client.Models;

/// <summary>
/// Indicates the severity of a bottleneck in the workflow.
/// Client-side copy that matches PoTool.Core.WorkItems.BottleneckSeverity.
/// </summary>
public enum BottleneckSeverity
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
