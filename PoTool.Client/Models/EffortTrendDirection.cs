namespace PoTool.Client.Models;

/// <summary>
/// Direction of effort distribution trend.
/// Client-side copy that matches PoTool.Core.Metrics.EffortTrendDirection.
/// </summary>
public enum EffortTrendDirection
{
    Stable = 0,
    Increasing = 1,
    Decreasing = 2,
    Volatile = 3
}
