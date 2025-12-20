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

    /// <summary>
    /// Gets all goals (work items of type Goal).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all goals</returns>
    Task<IEnumerable<WorkItemDto>> GetAllGoalsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets work items for specific Goal IDs (full hierarchy).
    /// </summary>
    /// <param name="goalIds">List of Goal IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Work items including goals and all descendants</returns>
    Task<IEnumerable<WorkItemDto>> GetGoalHierarchyAsync(IEnumerable<int> goalIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all cached work items with validation results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of work items with validation issues</returns>
    Task<IEnumerable<WorkItemWithValidationDto>> GetAllWithValidationAsync(CancellationToken cancellationToken = default);
}
