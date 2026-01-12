namespace PoTool.Shared.WorkItems;

/// <summary>
/// DTO for sync progress reporting with two-level progress bars.
/// </summary>
public sealed record SyncProgressDto
{
    /// <summary>
    /// Current status of the sync operation.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Human-readable message describing current operation.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Current major step (1-based index).
    /// </summary>
    public int MajorStep { get; init; }

    /// <summary>
    /// Total number of major steps.
    /// </summary>
    public int MajorStepTotal { get; init; }

    /// <summary>
    /// Label for current major step.
    /// </summary>
    public string? MajorStepLabel { get; init; }

    /// <summary>
    /// Current minor step within major step (1-based index).
    /// </summary>
    public int MinorStep { get; init; }

    /// <summary>
    /// Total number of minor steps in current major step.
    /// </summary>
    public int MinorStepTotal { get; init; }

    /// <summary>
    /// Label for current minor step.
    /// </summary>
    public string? MinorStepLabel { get; init; }

    /// <summary>
    /// Number of work items processed so far.
    /// </summary>
    public int ProcessedCount { get; init; }

    /// <summary>
    /// Total number of work items to process.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Root work item IDs being synced (for context).
    /// </summary>
    public int[]? RootWorkItemIds { get; init; }
}

/// <summary>
/// Request for sync operation with optional product context.
/// </summary>
public sealed record SyncRequest
{
    /// <summary>
    /// Optional list of root work item IDs to sync.
    /// If not provided, syncs based on area path.
    /// </summary>
    public int[]? RootWorkItemIds { get; init; }

    /// <summary>
    /// Whether to perform incremental sync (only changed items since last sync).
    /// </summary>
    public bool Incremental { get; init; }
}
