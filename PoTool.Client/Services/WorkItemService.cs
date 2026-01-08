using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for interacting with the Work Items API.
/// Wraps the generated API client.
/// </summary>
public class WorkItemService
{
    private readonly IWorkItemsClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkItemService"/> class.
    /// </summary>
    /// <param name="client">The work items API client.</param>
    public WorkItemService(IWorkItemsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Gets all cached work items.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetAllAsync()
    {
        return await _client.GetAllAsync();
    }

    /// <summary>
    /// Gets filtered work items.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter)
    {
        return await _client.GetFilterAsync(filter);
    }

    /// <summary>
    /// Gets a specific work item by TFS ID.
    /// </summary>
    public async Task<WorkItemDto?> GetByTfsIdAsync(int tfsId)
    {
        return await _client.GetWorkItemsAsync(tfsId);
    }

    /// <summary>
    /// Gets all goals (work items of type Goal).
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetAllGoalsAsync()
    {
        return await _client.GetGoalsAsync(null);
    }

    /// <summary>
    /// Gets work items for specific Goal IDs (full hierarchy).
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetGoalHierarchyAsync(List<int> goalIds)
    {
        var goalIdsParam = string.Join(",", goalIds);
        return await _client.GetGoalsAsync(goalIdsParam);
    }

    /// <summary>
    /// Gets all cached work items with validation results.
    /// </summary>
    public async Task<IEnumerable<WorkItemWithValidationDto>> GetAllWithValidationAsync()
    {
        return await _client.GetValidatedAsync();
    }

    /// <summary>
    /// Gets the revision history for a specific work item.
    /// </summary>
    public async Task<IEnumerable<WorkItemRevisionDto>> GetRevisionsAsync(int workItemId)
    {
        return await _client.GetRevisionsAsync(workItemId);
    }

    /// <summary>
    /// Gets all distinct area paths from cached work items.
    /// </summary>
    public async Task<IEnumerable<string>> GetDistinctAreaPathsAsync()
    {
        var allWorkItems = await _client.GetAllAsync();
        return allWorkItems
            .Select(wi => wi.AreaPath)
            .Distinct()
            .OrderBy(ap => ap)
            .ToList();
    }

    /// <summary>
    /// Gets all area paths directly from TFS/Azure DevOps server.
    /// This method retrieves the full area path hierarchy from the project classification nodes,
    /// independent of cached work items. Useful when creating profiles before any work items are synced.
    /// </summary>
    public async Task<IEnumerable<string>> GetAreaPathsFromTfsAsync()
    {
        // TODO: Uncomment after API client regeneration
        // return await _client.GetAreaPathsFromTfsAsync();
        
        // Temporary implementation: Make direct HTTP call to the API
        // This will be replaced with the generated client method after regeneration
        throw new NotImplementedException("API client needs to be regenerated. Run: pwsh tools/generate-openapi.ps1");
    }
}

