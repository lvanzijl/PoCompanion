using PoTool.Shared.WorkItems;

namespace PoTool.Core.Contracts;

/// <summary>
/// Provider for reading work item data from the configured data source.
/// Implementations select between Live (TFS direct) or Cached (repository) based on mode.
/// </summary>
public interface IWorkItemReadProvider
{
    /// <summary>
    /// Retrieves all work items from the configured data source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items matching the specified filter from the configured data source.
    /// </summary>
    /// <param name="filter">Text filter to apply to titles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items matching the specified area paths from the configured data source.
    /// </summary>
    /// <param name="areaPaths">List of area paths to filter by (hierarchical matching).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a work item by its TFS ID from the configured data source.
    /// </summary>
    /// <param name="tfsId">The TFS work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work item DTO or null if not found.</returns>
    Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items starting from specified root work item IDs and their entire hierarchy.
    /// Used for product-scoped loading operations.
    /// </summary>
    /// <param name="rootWorkItemIds">The root work item IDs to start from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item DTOs including root items and their descendants.</returns>
    Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default);
}
