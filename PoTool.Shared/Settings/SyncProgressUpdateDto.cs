namespace PoTool.Shared.Settings;

/// <summary>
/// DTO for sync progress updates sent via SignalR.
/// </summary>
public record SyncProgressUpdateDto
{
    /// <summary>
    /// Name of the current sync stage.
    /// </summary>
    public string CurrentStage { get; init; } = string.Empty;

    /// <summary>
    /// Progress percentage within the current stage (0-100).
    /// </summary>
    public int StageProgressPercent { get; init; }

    /// <summary>
    /// Whether the sync has completed (successfully or with failure).
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Whether the sync failed.
    /// </summary>
    public bool HasFailed { get; init; }

    /// <summary>
    /// Whether the sync completed with warnings.
    /// </summary>
    public bool HasWarnings { get; init; }

    /// <summary>
    /// Error message if sync failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Warning message if sync completed with warnings.
    /// </summary>
    public string? WarningMessage { get; init; }

    /// <summary>
    /// Current stage number (1-based).
    /// </summary>
    public int StageNumber { get; init; }

    /// <summary>
    /// Total number of stages.
    /// </summary>
    public int TotalStages { get; init; }
}
