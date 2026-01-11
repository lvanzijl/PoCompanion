using PoTool.Client.ApiClient;
using System.Net.Http.Json;
using System.Text.Json;
using SharedWorkItemDto = PoTool.Shared.WorkItems.WorkItemDto;
using SharedValidateWorkItemResponse = PoTool.Shared.WorkItems.ValidateWorkItemResponse;
using SharedValidateWorkItemRequest = PoTool.Shared.WorkItems.ValidateWorkItemRequest;

namespace PoTool.Client.Services;

/// <summary>
/// Service for interacting with the Work Items API.
/// Wraps the generated API client.
/// </summary>
public class WorkItemService
{
    private readonly IWorkItemsClient _client;
    private readonly HttpClient _httpClient;

    // API endpoint paths for direct TFS calls (bypassing cache)
    private const string AreaPathsFromTfsEndpoint = "/api/workitems/area-paths/from-tfs";
    private const string GoalsFromTfsEndpoint = "/api/workitems/goals/from-tfs";

    // JSON options for case-insensitive deserialization of API responses
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkItemService"/> class.
    /// </summary>
    /// <param name="client">The work items API client.</param>
    /// <param name="httpClient">HTTP client for direct API calls.</param>
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
    /// Validates a work item by ID directly from TFS (bypasses cache).
    /// Used specifically for validating backlog root work item IDs in product creation/editing.
    /// </summary>
    public async Task<SharedValidateWorkItemResponse> ValidateWorkItemAsync(int workItemId)
    {
        var request = new ValidateWorkItemRequest { WorkItemId = workItemId };
        var response = await _client.ValidateWorkItemAsync(request);
        
        // Map from generated client type to shared type
        return new SharedValidateWorkItemResponse
        {
            Exists = response.Exists,
            Id = response.Id,
            Title = response.Title,
            Type = response.Type,
            ErrorMessage = response.ErrorMessage
        };
    }

    /// <summary>
    /// Gets all goals (work items of type Goal).
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetAllGoalsAsync()
    {
        return await _client.GetAllGoalsAsync();
    }

    /// <summary>
    /// Gets work items for specific Goal IDs (full hierarchy).
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetGoalHierarchyAsync(List<int> goalIds)
    {
        var goalIdsParam = string.Join(",", goalIds);
        return await _client.GetGoalHierarchyAsync(goalIdsParam);
    }

    /// <summary>
    /// Gets all cached work items with validation results.
    /// </summary>
    public async Task<IEnumerable<WorkItemWithValidationDto>> GetAllWithValidationAsync()
    {
        return await _client.GetAllWithValidationAsync();
    }

    /// <summary>
    /// Gets the revision history for a specific work item.
    /// </summary>
    public async Task<IEnumerable<WorkItemRevisionDto>> GetRevisionsAsync(int workItemId)
    {
        return await _client.GetWorkItemRevisionsAsync(workItemId);
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
    /// Gets area paths directly from TFS, bypassing the cache.
    /// Used specifically for the Add Profile flow where cache is not yet populated.
    /// </summary>
    public async Task<IEnumerable<string>> GetAreaPathsFromTfsAsync()
    {
        // Direct TFS call to avoid relying on empty cache during Add Profile flow
        var response = await _httpClient.GetAsync(AreaPathsFromTfsEndpoint);
        response.EnsureSuccessStatusCode();

        var areaPaths = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(_jsonOptions);
        return areaPaths ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// Gets goals directly from TFS, bypassing the cache.
    /// Used specifically for the Add Profile flow where cache is not yet populated.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetGoalsFromTfsAsync()
    {
        // Direct TFS call to avoid relying on empty cache during Add Profile flow
        var response = await _httpClient.GetAsync(GoalsFromTfsEndpoint);
        response.EnsureSuccessStatusCode();

        var goals = await response.Content.ReadFromJsonAsync<IEnumerable<WorkItemDto>>(_jsonOptions);
        return goals ?? Enumerable.Empty<WorkItemDto>();
    }
}

