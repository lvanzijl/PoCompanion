namespace PoTool.Core.Configuration;

/// <summary>
/// Options controlling diagnostic logging for the revision ingestion pipeline.
/// </summary>
public sealed class RevisionIngestionDiagnosticsOptions
{
    /// <summary>
    /// Enables revision ingestion diagnostics logging.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Logs a per-page summary when enabled.
    /// </summary>
    public bool LogPerPageSummary { get; init; } = true;

    /// <summary>
    /// Logs per-work-item relation hydration details (can be noisy).
    /// </summary>
    public bool LogPerWorkItemHydration { get; init; } = false;

    /// <summary>
    /// Logs EF Core SaveChanges details during persistence.
    /// </summary>
    public bool LogEfSaveChangesDetails { get; init; } = true;

    /// <summary>
    /// Logs GC and change tracker stats every N pages. Set to 0 to disable.
    /// </summary>
    public int LogGcStatsEveryNPages { get; init; } = 50;

    /// <summary>
    /// Maximum number of parse warnings to log per page.
    /// </summary>
    public int MaxParseWarningsPerPage { get; init; } = 10;

    /// <summary>
    /// Threshold in milliseconds for marking a page as slow.
    /// </summary>
    public int SlowPageThresholdMs { get; init; } = 2000;

    /// <summary>
    /// Threshold in milliseconds for marking a DB persist as slow.
    /// </summary>
    public int SlowDbThresholdMs { get; init; } = 1000;

    /// <summary>
    /// Threshold in milliseconds for marking an HTTP call as slow.
    /// </summary>
    public int SlowHttpThresholdMs { get; init; } = 1000;

    /// <summary>
    /// Sampling rate from 0 to 1 when diagnostics are enabled.
    /// </summary>
    public double SampleRate { get; init; } = 1.0;
}
