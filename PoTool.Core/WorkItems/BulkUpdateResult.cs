namespace PoTool.Core.WorkItems;

/// <summary>
/// Result of a bulk work item update operation.
/// Contains success/failure status for each work item update.
/// This prevents N+1 queries by batching multiple updates.
/// </summary>
public sealed record BulkUpdateResult(
    int TotalRequested,
    int SuccessfulUpdates,
    int FailedUpdates,
    IReadOnlyList<BulkUpdateItemResult> Results,
    int TfsCallCount // Performance instrumentation: tracks actual TFS API calls made
);

/// <summary>
/// Result of an individual work item update within a bulk operation.
/// </summary>
public sealed record BulkUpdateItemResult(
    int WorkItemId,
    bool Success,
    string? ErrorMessage = null
);
