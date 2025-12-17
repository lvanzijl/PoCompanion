namespace PoTool.Client.ApiClient;

/// <summary>
/// API client interface for work items endpoints.
/// Generated from OpenAPI specification.
/// </summary>
public interface IWorkItemsClient
{
    /// <summary>
    /// Gets all cached work items.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of work items</returns>
    Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets filtered work items.
    /// </summary>
    /// <param name="filter">Filter string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Filtered work items</returns>
    Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific work item by TFS ID.
    /// </summary>
    /// <param name="tfsId">TFS work item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Work item or null</returns>
    Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default);
}
