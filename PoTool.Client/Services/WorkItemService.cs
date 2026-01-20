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
    private readonly WorkItemLoadCoordinatorService _loadCoordinator;

    // API endpoint paths for direct TFS calls (bypassing cache)
    private const string AreaPathsFromTfsEndpoint = "/api/workitems/area-paths/from-tfs";
    private const string GoalsFromTfsEndpoint = "/api/workitems/goals/from-tfs";
    private const string ByRootIdsEndpoint = "/api/workitems/by-root-ids";

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
    /// <param name="loadCoordinator">Service to coordinate and deduplicate load operations.</param>
    public WorkItemService(IWorkItemsClient client, HttpClient httpClient, WorkItemLoadCoordinatorService loadCoordinator)
    {
        _client = client;
        _httpClient = httpClient;
        _loadCoordinator = loadCoordinator;
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
        return await _client.GetAllWithValidationAsync(null);
    }

    /// <summary>
    /// Gets cached work items with validation results filtered by product IDs.
    /// </summary>
    /// <param name="productIds">Optional list of product IDs to filter by. If null or empty, loads all products for the active profile.</param>
    public async Task<IEnumerable<WorkItemWithValidationDto>> GetAllWithValidationAsync(int[]? productIds)
    {
        string? productIdsParam = null;
        if (productIds != null && productIds.Length > 0)
        {
            productIdsParam = string.Join(",", productIds);
        }
        return await _client.GetAllWithValidationAsync(productIdsParam);
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

    /// <summary>
    /// Gets work items by root IDs (hierarchical tree loading).
    /// Loads the complete hierarchy starting from specified root work item IDs.
    /// </summary>
    /// <param name="rootIds">The root work item IDs to load hierarchies from.</param>
    /// <returns>Collection of work items including roots and their descendants.</returns>
    public async Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootIds)
    {
        if (rootIds == null || rootIds.Length == 0)
        {
            return Enumerable.Empty<WorkItemDto>();
        }

        var rootIdsParam = string.Join(",", rootIds);
        var url = $"{ByRootIdsEndpoint}?rootIds={rootIdsParam}";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var workItems = await response.Content.ReadFromJsonAsync<IEnumerable<WorkItemDto>>(_jsonOptions);
        return workItems ?? Enumerable.Empty<WorkItemDto>();
    }

    /// <summary>
    /// Ensures work items are loaded for the specified root IDs.
    /// Uses the coordinator to prevent duplicate in-flight requests for the same root set.
    /// Returns the loaded work items.
    /// </summary>
    /// <param name="rootIds">The root work item IDs to load hierarchies from.</param>
    /// <returns>Collection of loaded work items.</returns>
    public async Task<IEnumerable<WorkItemDto>> EnsureLoadedForRootsAsync(int[] rootIds)
    {
        if (rootIds == null || rootIds.Length == 0)
        {
            return Enumerable.Empty<WorkItemDto>();
        }

        IEnumerable<WorkItemDto>? loadedItems = null;

        await _loadCoordinator.EnsureLoadedAsync(rootIds, async () =>
        {
            loadedItems = await GetByRootIdsAsync(rootIds);
        });

        // If loadedItems is still null, another call performed the loading
        // So we need to fetch the data again (it should be fast since it's already in TFS's cache)
        if (loadedItems == null)
        {
            loadedItems = await GetByRootIdsAsync(rootIds);
        }

        return loadedItems;
    }
}

