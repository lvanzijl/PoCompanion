namespace PoTool.Core.Domain.BacklogQuality.Models;

/// <summary>
/// Represents the canonical readiness score for one backlog scope item.
/// </summary>
public sealed record BacklogReadinessScore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BacklogReadinessScore"/> class.
    /// </summary>
    public BacklogReadinessScore(
        int workItemId,
        string workItemType,
        ReadinessScore score,
        string scoreReason,
        ReadinessOwnerState? ownerState = null)
    {
        if (workItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workItemId), "Work item ID must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(workItemType))
        {
            throw new ArgumentException("Work item type is required.", nameof(workItemType));
        }

        if (string.IsNullOrWhiteSpace(scoreReason))
        {
            throw new ArgumentException("A score reason is required.", nameof(scoreReason));
        }

        WorkItemId = workItemId;
        WorkItemType = workItemType;
        Score = score;
        ScoreReason = scoreReason;
        OwnerState = ownerState;
    }

    /// <summary>
    /// Gets the affected work item identifier.
    /// </summary>
    public int WorkItemId { get; }

    /// <summary>
    /// Gets the affected work item type.
    /// </summary>
    public string WorkItemType { get; }

    /// <summary>
    /// Gets the numeric readiness score.
    /// </summary>
    public ReadinessScore Score { get; }

    /// <summary>
    /// Gets the semantic reason for the score.
    /// </summary>
    public string ScoreReason { get; }

    /// <summary>
    /// Gets the owner state when the score applies to a Feature.
    /// </summary>
    public ReadinessOwnerState? OwnerState { get; }
}
