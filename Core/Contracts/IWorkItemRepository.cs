using Core.WorkItems;

namespace Core.Contracts;

/// <summary>
/// Repository interface for work item persistence operations.
/// </summary>
public interface IWorkItemRepository
{
    /// <summary>
    /// Gets all cached work items.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all cached work items.</returns>
    Task<IReadOnlyCollection<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a work item by its TFS ID.
    /// </summary>
    /// <param name="tfsId">The TFS work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work item if found, null otherwise.</returns>
    Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets work items by area path.
    /// </summary>
    /// <param name="areaPath">The area path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of work items matching the area path.</returns>
    Task<IReadOnlyCollection<WorkItemDto>> GetByAreaPathAsync(
        string areaPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all cached work items atomically.
    /// </summary>
    /// <param name="workItems">The new collection of work items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReplaceAllAsync(
        IEnumerable<WorkItemDto> workItems,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timestamp of the last cache update.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The timestamp of the last update, or null if never updated.</returns>
    Task<DateTimeOffset?> GetLastUpdateTimestampAsync(CancellationToken cancellationToken = default);
}
