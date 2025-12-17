using System.Net.Http.Json;
using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for interacting with the Work Items API.
/// Wraps the generated API client.
/// </summary>
public class WorkItemService
{
    private readonly IWorkItemsClient _client;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkItemService"/> class.
    /// </summary>
    /// <param name="client">The work items API client.</param>
    /// <param name="httpClient">The HTTP client for direct API calls.</param>
    public WorkItemService(IWorkItemsClient client, HttpClient httpClient)
    {
        _client = client;
        _httpClient = httpClient;
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
        return await _client.GetFilteredAsync(filter);
    }

    /// <summary>
    /// Gets a specific work item by TFS ID.
    /// </summary>
    public async Task<WorkItemDto?> GetByTfsIdAsync(int tfsId)
    {
        return await _client.GetByTfsIdAsync(tfsId);
    }

    /// <summary>
    /// Gets work items for specific Goal IDs (full hierarchy).
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetGoalHierarchyAsync(List<int> goalIds)
    {
        var goalIdsParam = string.Join(",", goalIds);
        var response = await _httpClient.GetFromJsonAsync<List<WorkItemDto>>($"/api/workitems/goals?goalIds={goalIdsParam}");
        return response ?? new List<WorkItemDto>();
    }
}
