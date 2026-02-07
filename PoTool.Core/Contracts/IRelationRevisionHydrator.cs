namespace PoTool.Core.Contracts;

/// <summary>
/// Interface for hydrating relation deltas for work items.
/// Relations are NOT available from the reporting revisions endpoint,
/// so they must be fetched separately via the per-item revisions endpoint.
/// </summary>
public interface IRelationRevisionHydrator
{
    /// <summary>
    /// Hydrates relation deltas for the specified work items.
    /// Fetches revisions with relations for each work item and persists relation deltas.
    /// </summary>
    /// <param name="workItemIds">Collection of work item IDs to hydrate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing count of work items and revisions hydrated.</returns>
    Task<RelationHydrationResult> HydrateAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a relation hydration operation.
/// </summary>
public record RelationHydrationResult
{
    /// <summary>
    /// Whether the hydration was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Number of work items processed.
    /// </summary>
    public int WorkItemsProcessed { get; init; }

    /// <summary>
    /// Number of revisions hydrated.
    /// </summary>
    public int RevisionsHydrated { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
