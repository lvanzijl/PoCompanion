namespace PoTool.Core.Health;

/// <summary>
/// Core business logic for calculating backlog health scores.
/// </summary>
public class BacklogHealthCalculator
{
    /// <summary>
    /// Calculates the health score for an iteration based on validation issues.
    /// </summary>
    /// <param name="totalWorkItems">Total number of work items in the iteration.</param>
    /// <param name="workItemsWithoutEffort">Number of work items without effort estimates.</param>
    /// <param name="workItemsInProgressWithoutEffort">Number of in-progress work items without effort.</param>
    /// <param name="parentProgressIssues">Number of parent progress issues.</param>
    /// <param name="blockedItems">Number of blocked items.</param>
    /// <returns>Health score from 0 to 100.</returns>
    public int CalculateHealthScore(
        int totalWorkItems,
        int workItemsWithoutEffort,
        int workItemsInProgressWithoutEffort,
        int parentProgressIssues,
        int blockedItems)
    {
        if (totalWorkItems == 0) return 100;

        var issues = workItemsWithoutEffort +
                    workItemsInProgressWithoutEffort +
                    parentProgressIssues +
                    blockedItems;

        var issuePercentage = (double)issues / totalWorkItems;
        return (int)Math.Max(0, 100 - (issuePercentage * 100));
    }
}
