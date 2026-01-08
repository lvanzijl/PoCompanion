namespace PoTool.Client.Models;

/// <summary>
/// Direction of a trend across sprints.
/// Client-side copy that matches PoTool.Core.Metrics.TrendDirection.
/// </summary>
public enum TrendDirection
{
    Improving = 0,
    Stable = 1,
    Degrading = 2,
    Unknown = 3
}
