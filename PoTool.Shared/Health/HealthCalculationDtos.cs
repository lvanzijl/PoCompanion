namespace PoTool.Shared.Health;

/// <summary>
/// Request to calculate health score for an iteration.
/// </summary>
public class CalculateHealthScoreRequest
{
    /// <summary>
    /// Total number of work items in the iteration.
    /// </summary>
    public required int TotalWorkItems { get; set; }

    /// <summary>
    /// Number of work items without effort estimates.
    /// </summary>
    public required int WorkItemsWithoutEffort { get; set; }

    /// <summary>
    /// Number of in-progress work items without effort.
    /// </summary>
    public required int WorkItemsInProgressWithoutEffort { get; set; }

    /// <summary>
    /// Number of parent progress issues.
    /// </summary>
    public required int ParentProgressIssues { get; set; }

    /// <summary>
    /// Number of blocked items.
    /// </summary>
    public required int BlockedItems { get; set; }
}

/// <summary>
/// Response containing calculated health score.
/// </summary>
public class CalculateHealthScoreResponse
{
    /// <summary>
    /// Health score from 0 to 100.
    /// </summary>
    public required int HealthScore { get; set; }
}
