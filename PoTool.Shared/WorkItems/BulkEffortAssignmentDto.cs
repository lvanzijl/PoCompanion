namespace PoTool.Shared.WorkItems;

/// <summary>
/// DTO for bulk effort assignment operation.
/// </summary>
public sealed record BulkEffortAssignmentDto(
    int WorkItemId,
    int EffortValue
);

/// <summary>
/// Result DTO for bulk effort assignment operation.
/// </summary>
public sealed record BulkEffortAssignmentResultDto(
    int TotalRequested,
    int SuccessfulUpdates,
    int FailedUpdates,
    IReadOnlyList<BulkEffortAssignmentItemResult> Results
);

/// <summary>
/// Individual result for a single work item in bulk effort assignment.
/// </summary>
public sealed record BulkEffortAssignmentItemResult(
    int WorkItemId,
    bool Success,
    string? ErrorMessage = null
);
