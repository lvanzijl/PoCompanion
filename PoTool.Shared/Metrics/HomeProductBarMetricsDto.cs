namespace PoTool.Shared.Metrics;

/// <summary>
/// Compact contextual metrics shown in the Home product bar.
/// </summary>
public record HomeProductBarMetricsDto
{
    /// <summary>
    /// Team-wide current sprint progress as elapsed time percentage.
    /// Null when no current sprint with dates is available.
    /// </summary>
    public int? SprintProgressPercentage { get; init; }

    /// <summary>
    /// Open bug count for the selected product scope.
    /// </summary>
    public int BugCount { get; init; }

    /// <summary>
    /// Number of distinct work items with activity events today for the selected product scope.
    /// </summary>
    public int ChangesTodayCount { get; init; }
}
