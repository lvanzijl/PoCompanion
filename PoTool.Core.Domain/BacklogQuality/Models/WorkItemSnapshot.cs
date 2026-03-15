using PoTool.Core.Domain.Models;

namespace PoTool.Core.Domain.BacklogQuality.Models;

/// <summary>
/// Current-state work item data required by the backlog-quality domain slice.
/// </summary>
public sealed record WorkItemSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkItemSnapshot"/> class.
    /// </summary>
    public WorkItemSnapshot(
        int workItemId,
        string workItemType,
        int? parentWorkItemId,
        string? description,
        decimal? effort,
        StateClassification stateClassification)
    {
        if (workItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workItemId), "Work item ID must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(workItemType))
        {
            throw new ArgumentException("Work item type is required.", nameof(workItemType));
        }

        WorkItemId = workItemId;
        WorkItemType = workItemType;
        ParentWorkItemId = parentWorkItemId;
        Description = description;
        Effort = effort;
        StateClassification = stateClassification;
    }

    /// <summary>
    /// Gets the work item identifier.
    /// </summary>
    public int WorkItemId { get; }

    /// <summary>
    /// Gets the canonical work item type name.
    /// </summary>
    public string WorkItemType { get; }

    /// <summary>
    /// Gets the parent work item identifier, when one exists in the current snapshot.
    /// </summary>
    public int? ParentWorkItemId { get; }

    /// <summary>
    /// Gets the current description text.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the current effort estimate, when present.
    /// </summary>
    public decimal? Effort { get; }

    /// <summary>
    /// Gets the canonical lifecycle classification for the current snapshot state.
    /// </summary>
    public StateClassification StateClassification { get; }
}
