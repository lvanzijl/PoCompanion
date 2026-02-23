namespace PoTool.Core.Configuration;

/// <summary>
/// Configuration options for the V2 revision ingestor.
/// </summary>
public sealed class RevisionIngestionV2Options
{
    /// <summary>
    /// Ingestion mode: "V1" uses the existing service, "V2" uses the new streaming ingestor.
    /// Default is "V1" for backward compatibility.
    /// </summary>
    public string RevisionIngestionMode { get; set; } = "V1";

    /// <summary>
    /// Number of revisions to request per OData page in V2.
    /// </summary>
    public int V2PageSize { get; set; } = 200;

    /// <summary>
    /// Whether to use time-based windowing in V2.
    /// When true, ingestion is split into time windows.
    /// When false, a single open-ended window is used.
    /// </summary>
    public bool V2EnableWindowing { get; set; } = true;

    /// <summary>
    /// Size of each time window in days when windowing is enabled.
    /// </summary>
    public int V2WindowDays { get; set; } = 30;

    /// <summary>
    /// Maximum number of retries for empty pages that still return a continuation token.
    /// </summary>
    public int V2MaxEmptyPageRetries { get; set; } = 2;

    /// <summary>
    /// Maximum number of consecutive empty pages before the window is considered stalled.
    /// </summary>
    public int V2MaxConsecutiveEmptyPages { get; set; } = 10;

    /// <summary>
    /// Number of consecutive empty pages with continuation token before emitting a stall dump warning.
    /// </summary>
    public int V2EmptyWithTokenDumpThreshold { get; set; } = 200;

    /// <summary>
    /// Repeat interval for additional stall dump warnings while empty-with-token paging continues.
    /// </summary>
    public int V2EmptyWithTokenDumpRepeatInterval { get; set; } = 200;

    /// <summary>
    /// Whether V2 mode is active.
    /// </summary>
    public bool IsV2 => string.Equals(RevisionIngestionMode, "V2", StringComparison.OrdinalIgnoreCase);
}
