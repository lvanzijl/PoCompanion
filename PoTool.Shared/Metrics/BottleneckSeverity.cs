namespace PoTool.Shared.Metrics;

/// <summary>
/// Indicates the severity of a bottleneck in the workflow.
/// </summary>
public enum BottleneckSeverity
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
