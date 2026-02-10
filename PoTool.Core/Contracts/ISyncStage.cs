namespace PoTool.Core.Contracts;

/// <summary>
/// Represents a single stage in the sync pipeline.
/// </summary>
public interface ISyncStage
{
    /// <summary>
    /// The name of this stage for progress reporting.
    /// </summary>
    string StageName { get; }

    /// <summary>
    /// The stage number (1-based) in the pipeline sequence.
    /// </summary>
    int StageNumber { get; }

    /// <summary>
    /// Executes this sync stage.
    /// </summary>
    /// <param name="context">The sync context containing ProductOwner ID and watermarks.</param>
    /// <param name="progressCallback">Callback for reporting progress (0-100 percent).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stage result with data for the next stage or finalization.</returns>
    Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to sync stages containing input data.
/// </summary>
public class SyncContext
{
    /// <summary>
    /// The ProductOwner ID being synced.
    /// </summary>
    public required int ProductOwnerId { get; init; }

    /// <summary>
    /// Root work item IDs for the ProductOwner's products.
    /// </summary>
    public required int[] RootWorkItemIds { get; init; }

    /// <summary>
    /// Current work item watermark (null = full sync).
    /// </summary>
    public DateTimeOffset? WorkItemWatermark { get; init; }

    /// <summary>
    /// Current pull request watermark (null = full sync).
    /// </summary>
    public DateTimeOffset? PullRequestWatermark { get; init; }

    /// <summary>
    /// Current pipeline watermark (null = full sync).
    /// </summary>
    public DateTimeOffset? PipelineWatermark { get; init; }

    /// <summary>
    /// Repository names linked to ProductOwner's products.
    /// </summary>
    public string[] RepositoryNames { get; init; } = [];

    /// <summary>
    /// Pipeline definition IDs linked to ProductOwner's products.
    /// </summary>
    public int[] PipelineDefinitionIds { get; init; } = [];
}

/// <summary>
/// Result from executing a sync stage.
/// </summary>
public class SyncStageResult
{
    /// <summary>
    /// Whether the stage completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Whether the stage completed with warnings.
    /// </summary>
    public bool HasWarnings { get; init; }

    /// <summary>
    /// Error message if the stage failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Warning message if the stage completed with warnings.
    /// </summary>
    public string? WarningMessage { get; init; }

    /// <summary>
    /// Number of items processed in this stage.
    /// </summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// New watermark value after successful sync (to be committed in finalization).
    /// </summary>
    public DateTimeOffset? NewWatermark { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static SyncStageResult CreateSuccess(
        int itemCount,
        DateTimeOffset? newWatermark = null,
        bool hasWarnings = false,
        string? warningMessage = null)
        => new()
        {
            Success = true,
            ItemCount = itemCount,
            NewWatermark = newWatermark,
            HasWarnings = hasWarnings,
            WarningMessage = warningMessage
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static SyncStageResult CreateFailure(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };
}
