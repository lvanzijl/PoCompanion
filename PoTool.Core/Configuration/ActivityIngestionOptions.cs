namespace PoTool.Core.Configuration;

/// <summary>
/// Configuration options for work item update-based activity ingestion.
/// </summary>
public sealed class ActivityIngestionOptions
{
    /// <summary>
    /// Number of days to backfill on first run.
    /// 0 means full history.
    /// </summary>
    public int ActivityBackfillDays { get; set; } = 0;
}
