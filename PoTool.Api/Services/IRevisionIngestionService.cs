namespace PoTool.Api.Services;

/// <summary>
/// Abstraction for revision ingestion that dispatches to V1 or V2 based on configuration.
/// </summary>
public interface IRevisionIngestionService
{
    /// <summary>
    /// Ingests work item revisions for a ProductOwner.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="progressCallback">Optional callback for progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the ingestion operation.</returns>
    Task<RevisionIngestionResult> IngestRevisionsAsync(
        int productOwnerId,
        Action<RevisionIngestionProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);
}
