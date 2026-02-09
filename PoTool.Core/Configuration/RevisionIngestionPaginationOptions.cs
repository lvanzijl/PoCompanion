namespace PoTool.Core.Configuration;

/// <summary>
/// Pagination safety options for reporting revisions ingestion.
/// </summary>
public sealed class RevisionIngestionPaginationOptions
{
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
}
