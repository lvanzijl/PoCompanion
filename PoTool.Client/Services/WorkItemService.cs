using System.Net.Http.Json;
using PoTool.Core.WorkItems;

namespace PoTool.Client.Services;

/// <summary>
/// Service for interacting with the Work Items API.
/// </summary>
public class WorkItemService
{
    private readonly HttpClient _httpClient;

    public WorkItemService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets all cached work items.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetAllAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<IEnumerable<WorkItemDto>>("api/workitems");
        return result ?? Enumerable.Empty<WorkItemDto>();
    }

    /// <summary>
    /// Gets filtered work items.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter)
    {
        var result = await _httpClient.GetFromJsonAsync<IEnumerable<WorkItemDto>>($"api/workitems/filter/{Uri.EscapeDataString(filter)}");
        return result ?? Enumerable.Empty<WorkItemDto>();
    }

    /// <summary>
    /// Gets a specific work item by TFS ID.
    /// </summary>
    public async Task<WorkItemDto?> GetByTfsIdAsync(int tfsId)
    {
        return await _httpClient.GetFromJsonAsync<WorkItemDto>($"api/workitems/{tfsId}");
    }
}
