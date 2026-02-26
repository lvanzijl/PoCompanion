namespace PoTool.Shared.Metrics;

/// <summary>
/// Activity details for a selected work item and its descendant work items.
/// </summary>
public record WorkItemActivityDetailsDto
{
    /// <summary>
    /// Selected root work item TFS ID.
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// Selected root work item title.
    /// </summary>
    public required string WorkItemTitle { get; init; }

    /// <summary>
    /// Selected root work item type.
    /// </summary>
    public required string WorkItemType { get; init; }

    /// <summary>
    /// Inclusive period start used for the activity query.
    /// </summary>
    public DateTimeOffset? PeriodStartUtc { get; init; }

    /// <summary>
    /// Inclusive period end used for the activity query.
    /// </summary>
    public DateTimeOffset? PeriodEndUtc { get; init; }

    /// <summary>
    /// Activity rows that explain why this item was active.
    /// </summary>
    public required IReadOnlyList<WorkItemActivityEntryDto> Activities { get; init; }
}

/// <summary>
/// A single activity event row for the selected item or one of its descendants.
/// </summary>
public record WorkItemActivityEntryDto
{
    /// <summary>
    /// Work item TFS ID that emitted the activity event.
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// Work item title that emitted the activity event.
    /// </summary>
    public required string WorkItemTitle { get; init; }

    /// <summary>
    /// Work item type that emitted the activity event.
    /// </summary>
    public required string WorkItemType { get; init; }

    /// <summary>
    /// Whether this event belongs to the selected root item.
    /// </summary>
    public required bool IsSelectedWorkItem { get; init; }

    /// <summary>
    /// Changed field reference name.
    /// </summary>
    public required string FieldRefName { get; init; }

    /// <summary>
    /// Previous value.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// New value.
    /// </summary>
    public string? NewValue { get; init; }

    /// <summary>
    /// Event timestamp in UTC.
    /// </summary>
    public required DateTimeOffset EventTimestampUtc { get; init; }
}
