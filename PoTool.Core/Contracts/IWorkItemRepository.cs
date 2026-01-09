using PoTool.Shared.WorkItems;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for work item persistence.
/// </summary>
public interface IWorkItemRepository
{
    /// <summary>
    /// Retrieves all cached work items.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items matching the specified filter.
    /// </summary>
    /// <param name="filter">Text filter to apply to titles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items matching the specified area paths.
    /// </summary>
    /// <param name="areaPaths">List of area paths to filter by (hierarchical matching).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a work item by its TFS ID.
    /// </summary>
    /// <param name="tfsId">The TFS work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work item DTO or null if not found.</returns>
    Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all cached work items atomically.
    /// </summary>
    /// <param name="workItems">New collection of work items to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReplaceAllAsync(IEnumerable<WorkItemDto> workItems, CancellationToken cancellationToken = default);
}
