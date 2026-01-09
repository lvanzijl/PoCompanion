namespace PoTool.Shared.WorkItems;

/// <summary>
/// Immutable DTO for work item revision history.
/// Represents a single change to a work item at a specific point in time.
/// </summary>
public sealed record WorkItemRevisionDto(
    int RevisionNumber,
    int WorkItemId,
    string ChangedBy,
    DateTimeOffset ChangedDate,
    IReadOnlyDictionary<string, WorkItemFieldChange> FieldChanges,
    string? Comment
);

/// <summary>
/// Represents a change to a single field in a work item.
/// </summary>
public sealed record WorkItemFieldChange(
    string FieldName,
    string? OldValue,
    string? NewValue
);
