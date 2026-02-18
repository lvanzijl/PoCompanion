namespace PoTool.Core.Configuration;

/// <summary>
/// Pagination safety options for reporting revisions ingestion.
/// </summary>
public sealed class RevisionIngestionPaginationOptions
{
    /// <summary>
    /// Enables bounded incremental retry after pagination anomalies.
    /// </summary>
    public bool RetryEnabled { get; init; } = false;

    /// <summary>
    /// Maximum retry iterations executed after a pagination anomaly.
    /// </summary>
    public int RetryMaxIterations { get; init; } = 3;

    /// <summary>
    /// Overlap window in minutes applied when computing retry cursors.
    /// </summary>
    public int RetryOverlapMinutes { get; init; } = 60;

    /// <summary>
    /// Minimum ChangedDate delta (in seconds) required to treat retry progress as monotonic.
    /// </summary>
    public int ProgressEpsilonSeconds { get; init; } = 1;

    /// <summary>
    /// Maximum number of empty pages allowed per ingestion run (minimum 1).
    /// Default is 50 to tolerate extended empty bursts before terminating the stream.
    /// </summary>
    public int MaxEmptyPages { get; init; } = 50;

    /// <summary>
    /// Maximum number of pages that advance the token without returning data (minimum 1).
    /// Default is 3 to allow limited progress without data before stopping.
    /// </summary>
    public int MaxProgressWithoutDataPages { get; init; } = 3;

    /// <summary>
    /// Maximum number of pages allowed per ingestion run (minimum 1).
    /// </summary>
    public int MaxTotalPages { get; init; } = 5000;

    /// <summary>
    /// Maximum number of retries for a pagination anomaly on the same page.
    /// Default is 2 to allow quick retry before falling back or failing.
    /// </summary>
    public int MaxPageRetries { get; init; } = 2;

    /// <summary>
    /// Base backoff delay in seconds applied between retries.
    /// </summary>
    public int RetryBackoffSeconds { get; init; } = 2;

    /// <summary>
    /// Jitter range in seconds added to retry backoff (0 disables jitter).
    /// </summary>
    public int RetryBackoffJitterSeconds { get; init; } = 1;

    /// <summary>
    /// How pagination anomalies are handled after retries are exhausted.
    /// </summary>
    public PaginationAnomalyPolicy AnomalyPolicy { get; init; } = PaginationAnomalyPolicy.FailFast;

    /// <summary>
    /// Batch size for fallback per-work-item retrieval.
    /// </summary>
    public int FallbackBatchSize { get; init; } = 25;
}

public enum PaginationAnomalyPolicy
{
    /// <summary>
    /// Stop ingestion and report an anomaly when pagination cannot progress.
    /// </summary>
    FailFast = 0,

    /// <summary>
    /// Activate fallback per-work-item revision retrieval when pagination cannot progress.
    /// </summary>
    Fallback = 1
}
