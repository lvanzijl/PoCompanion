namespace PoTool.Shared.Settings;

/// <summary>
/// Classification for work item states.
/// </summary>
public enum StateClassification
{
    /// <summary>
    /// Work has not started yet (e.g., "New", "Proposed").
    /// </summary>
    New = 0,

    /// <summary>
    /// Work is actively being done (e.g., "Active", "In Progress", "Committed").
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Work is complete (e.g., "Done", "Closed", "Removed", "Resolved").
    /// </summary>
    Done = 2
}

/// <summary>
/// Represents the classification of a specific state for a work item type.
/// </summary>
public record WorkItemStateClassificationDto
{
    /// <summary>
    /// The work item type name.
    /// </summary>
    public required string WorkItemType { get; init; }

    /// <summary>
    /// The state name.
    /// </summary>
    public required string StateName { get; init; }

    /// <summary>
    /// The classification of this state.
    /// </summary>
    public required StateClassification Classification { get; init; }
}

/// <summary>
/// Request to save work item state classifications.
/// </summary>
public record SaveStateClassificationsRequest
{
    /// <summary>
    /// The TFS project name these classifications apply to.
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// The list of state classifications to save.
    /// </summary>
    public required IReadOnlyList<WorkItemStateClassificationDto> Classifications { get; init; }
}

/// <summary>
/// Response containing current work item state classifications.
/// </summary>
public record GetStateClassificationsResponse
{
    /// <summary>
    /// The TFS project name these classifications apply to.
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// The list of current state classifications.
    /// </summary>
    public required IReadOnlyList<WorkItemStateClassificationDto> Classifications { get; init; }

    /// <summary>
    /// Whether this is using default classifications (not yet configured).
    /// </summary>
    public required bool IsDefault { get; init; }
}
