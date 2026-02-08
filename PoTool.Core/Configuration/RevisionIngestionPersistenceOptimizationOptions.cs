namespace PoTool.Core.Configuration;

/// <summary>
/// Options controlling revision ingestion persistence optimizations.
/// </summary>
public sealed class RevisionIngestionPersistenceOptimizationOptions
{
    /// <summary>
    /// Enables revision ingestion persistence optimizations.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Enables SQLite PRAGMAs for faster ingestion.
    /// </summary>
    public bool SqlitePragmasEnabled { get; init; } = true;

    /// <summary>
    /// SQLite synchronous mode (NORMAL default, OFF allowed in development only).
    /// </summary>
    public string SqliteSynchronous { get; init; } = "NORMAL";

    /// <summary>
    /// SQLite cache size in KB (negative values are interpreted as KB by SQLite).
    /// </summary>
    public int SqliteCacheSizeKb { get; init; } = 100_000;

    /// <summary>
    /// Enables temp_store=MEMORY for SQLite.
    /// </summary>
    public bool SqliteTempStoreMemory { get; init; } = true;

    /// <summary>
    /// Number of pages to include per transaction (default 1). Currently reserved; ingestion persists one page per transaction.
    /// </summary>
    public int BatchPagesPerTransaction { get; init; } = 1;

    /// <summary>
    /// Safeguard for maximum entities per flush (reserved for future enforcement).
    /// </summary>
    public int MaxEntitiesPerFlush { get; init; } = 5000;
}
