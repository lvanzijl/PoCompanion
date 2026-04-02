using PoTool.Client.ApiClient;
using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Shared.DataState;
using PoTool.Shared.Health;
using SharedWorkItemDto = PoTool.Shared.WorkItems.WorkItemDto;
using SharedValidateWorkItemResponse = PoTool.Shared.WorkItems.ValidateWorkItemResponse;
using SharedValidateWorkItemRequest = PoTool.Shared.WorkItems.ValidateWorkItemRequest;
using SharedValidationTriageSummaryDto = PoTool.Shared.WorkItems.ValidationTriageSummaryDto;
using SharedValidationQueueDto = PoTool.Shared.WorkItems.ValidationQueueDto;
using SharedValidationFixSessionDto = PoTool.Shared.WorkItems.ValidationFixSessionDto;

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
    private const string RefreshByRootIdsFromTfsEndpoint = "/api/workitems/by-root-ids/refresh-from-tfs";
    private const string ValidationTriageEndpoint = "/api/workitems/validation-triage";
    private const string ValidationQueueEndpoint = "/api/workitems/validation-queue";
    private const string ValidationFixEndpoint = "/api/workitems/validation-fix";
    private const string WorkItemStateEndpoint = "/api/workitems/state";

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

    public async Task<DataStateResponseDto<WorkItemDto>?> GetByTfsIdStateAsync(int tfsId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<DataStateResponseDto<WorkItemDto>>(
            $"{WorkItemStateEndpoint}/{tfsId}",
            _jsonOptions,
            cancellationToken);

    /// <summary>
    /// Gets a specific work item with validation by TFS ID from cache.
    /// This retrieves a single work item from the cached data efficiently via a dedicated endpoint.
    /// Much more efficient than fetching all work items and filtering client-side.
    /// </summary>
    public async Task<WorkItemWithValidationDto?> GetByTfsIdWithValidationAsync(int tfsId, int[]? productIds = null)
    {
        try
        {
            // Build the URL with optional product IDs
            var url = $"/api/workitems/validated/{tfsId}";
            if (productIds != null && productIds.Length > 0)
            {
                var productIdsParam = string.Join(",", productIds);
                url += $"?productIds={productIdsParam}";
            }

            // Make the HTTP request to get a single work item with validation
            var response = await _httpClient.GetAsync(url);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            
            var workItem = await response.Content.ReadFromJsonAsync<WorkItemWithValidationDto>(_jsonOptions);
            return workItem;
        }
        catch (Exception ex)
        {
            // Log and rethrow - let caller handle the error
            throw new InvalidOperationException($"Failed to retrieve work item {tfsId} with validation", ex);
        }
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
    /// Gets cached work items with validation results filtered by product IDs.
    /// Supports cancellation for progressively loaded dashboard widgets.
    /// </summary>
    public async Task<IEnumerable<WorkItemWithValidationDto>> GetAllWithValidationAsync(
        int[]? productIds,
        CancellationToken cancellationToken)
    {
        string? productIdsParam = null;
        if (productIds != null && productIds.Length > 0)
        {
            productIdsParam = string.Join(",", productIds);
        }

        return await _client.GetAllWithValidationAsync(productIdsParam, cancellationToken);
    }

    public async Task<DataStateResponseDto<IReadOnlyList<WorkItemWithValidationDto>>?> GetAllWithValidationStateAsync(
        int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        var url = "/api/workitems/state/validated";
        if (productIds != null && productIds.Length > 0)
        {
            url += $"?productIds={string.Join(",", productIds)}";
        }

        return await _httpClient.GetFromJsonAsync<DataStateResponseDto<IReadOnlyList<WorkItemWithValidationDto>>>(
            url,
            _jsonOptions,
            cancellationToken);
    }

    /// <summary>
    /// Gets the grouped validation triage summary used by the Validation Triage page.
    /// Returns per-category item counts and top rule groups (SI, RR, RC, EFF).
    /// </summary>
    /// <param name="productIds">Optional list of product IDs to filter by.</param>
    public async Task<SharedValidationTriageSummaryDto?> GetValidationTriageSummaryAsync(
        int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        var url = ValidationTriageEndpoint;
        if (productIds != null && productIds.Length > 0)
        {
            url += $"?productIds={string.Join(",", productIds)}";
        }

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SharedValidationTriageSummaryDto>(_jsonOptions, cancellationToken);
    }

    public async Task<DataStateResponseDto<SharedValidationTriageSummaryDto>?> GetValidationTriageSummaryStateAsync(
        int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        var url = "/api/workitems/state/validation-triage";
        if (productIds != null && productIds.Length > 0)
        {
            url += $"?productIds={string.Join(",", productIds)}";
        }

        return await _httpClient.GetFromJsonAsync<DataStateResponseDto<SharedValidationTriageSummaryDto>>(
            url,
            _jsonOptions,
            cancellationToken);
    }

    /// <summary>
    /// Gets the lightweight Health workspace summary for a single product card.
    /// </summary>
    public async Task<HealthWorkspaceProductSummaryDto?> GetHealthWorkspaceProductSummaryAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/workitems/health-summary/{productId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HealthWorkspaceProductSummaryDto>(_jsonOptions, cancellationToken);
    }

    /// <summary>
    /// Gets the validation queue for a specific category.
    /// Returns rule groups with item counts sorted by count descending.
    /// Used by the Validation Queue page.
    /// </summary>
    /// <param name="categoryKey">Category key: "SI", "RR", "RC", or "EFF".</param>
    /// <param name="productIds">Optional list of product IDs to filter by.</param>
    public async Task<SharedValidationQueueDto?> GetValidationQueueAsync(string categoryKey, int[]? productIds = null)
    {
        var url = $"{ValidationQueueEndpoint}?category={Uri.EscapeDataString(categoryKey)}";
        if (productIds != null && productIds.Length > 0)
        {
            url += $"&productIds={string.Join(",", productIds)}";
        }

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SharedValidationQueueDto>(_jsonOptions);
    }

    public async Task<DataStateResponseDto<SharedValidationQueueDto>?> GetValidationQueueStateAsync(
        string categoryKey,
        int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/workitems/state/validation-queue?category={Uri.EscapeDataString(categoryKey)}";
        if (productIds != null && productIds.Length > 0)
        {
            url += $"&productIds={string.Join(",", productIds)}";
        }

        return await _httpClient.GetFromJsonAsync<DataStateResponseDto<SharedValidationQueueDto>>(
            url,
            _jsonOptions,
            cancellationToken);
    }

    /// <summary>
    /// Gets the validation fix session for a specific rule.
    /// Returns all work items that violate the rule, ordered by TFS ID.
    /// Used by the Validation Fix Session page.
    /// </summary>
    /// <param name="ruleId">Rule identifier (e.g. "SI-1", "RC-2").</param>
    /// <param name="categoryKey">Category key: "SI", "RR", "RC", or "EFF".</param>
    /// <param name="productIds">Optional list of product IDs to filter by.</param>
    public async Task<SharedValidationFixSessionDto?> GetValidationFixSessionAsync(
        string ruleId, string categoryKey, int[]? productIds = null)
    {
        var url = $"{ValidationFixEndpoint}?ruleId={Uri.EscapeDataString(ruleId)}&category={Uri.EscapeDataString(categoryKey)}";
        if (productIds != null && productIds.Length > 0)
        {
            url += $"&productIds={string.Join(",", productIds)}";
        }

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SharedValidationFixSessionDto>(_jsonOptions);
    }

    public async Task<DataStateResponseDto<SharedValidationFixSessionDto>?> GetValidationFixSessionStateAsync(
        string ruleId,
        string categoryKey,
        int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/workitems/state/validation-fix?ruleId={Uri.EscapeDataString(ruleId)}&category={Uri.EscapeDataString(categoryKey)}";
        if (productIds != null && productIds.Length > 0)
        {
            url += $"&productIds={string.Join(",", productIds)}";
        }

        return await _httpClient.GetFromJsonAsync<DataStateResponseDto<SharedValidationFixSessionDto>>(
            url,
            _jsonOptions,
            cancellationToken);
    }

    /// <summary>
    /// Gets the revision history for a specific work item.
    /// </summary>
    public async Task<IEnumerable<WorkItemRevisionDto>> GetRevisionsAsync(int workItemId)
    {
        var revisions = await _client.GetWorkItemRevisionsAsync(workItemId);
        return revisions;
    }

    /// <summary>
    /// Re-fetches a work item from TFS and updates the local DB cache.
    /// Returns true if the work item was found and updated, false if not found in TFS.
    /// </summary>
    /// <param name="tfsId">TFS work item ID to refresh.</param>
    public async Task<bool> RefreshWorkItemFromTfsAsync(int tfsId)
    {
        var response = await _httpClient.PostAsync($"/api/workitems/{tfsId}/refresh-from-tfs", null);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Re-fetches a product-scoped work item hierarchy from TFS and updates the local DB cache.
    /// Returns the number of refreshed work items.
    /// </summary>
    /// <param name="rootIds">Root work item IDs that define the hierarchy to refresh.</param>
    public async Task<int> RefreshWorkItemsByRootIdsFromTfsAsync(int[] rootIds)
    {
        if (rootIds.Length == 0)
            return 0;

        var response = await _httpClient.PostAsync(
            $"{RefreshByRootIdsFromTfsEndpoint}?rootIds={string.Join(",", rootIds)}",
            null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(_jsonOptions);
    }

    /// <summary>
    /// Updates the BacklogPriority of a work item in TFS and refreshes the local cache.
    /// Used by Product Roadmaps to reorder product lanes (Objectives).
    /// </summary>
    /// <param name="tfsId">TFS work item ID.</param>
    /// <param name="priority">New backlog priority value.</param>
    public async Task<bool> UpdateBacklogPriorityAsync(int tfsId, double priority)
    {
        try
        {
            await _client.UpdateBacklogPriorityAsync(tfsId, new UpdateBacklogPriorityRequest { Priority = priority });
            return true;
        }
        catch (ApiException)
        {
            return false;
        }
    }

    /// <summary>
    /// Updates the IterationPath (sprint assignment) of a work item in TFS and refreshes the local cache.
    /// Used by the Plan Board to move PBIs and bugs between sprints.
    /// </summary>
    /// <param name="tfsId">TFS work item ID.</param>
    /// <param name="iterationPath">New iteration path value.</param>
    public async Task<bool> UpdateIterationPathAsync(int tfsId, string iterationPath)
    {
        try
        {
            await _client.UpdateIterationPathAsync(tfsId, new UpdateIterationPathRequest { IterationPath = iterationPath });
            return true;
        }
        catch (ApiException)
        {
            return false;
        }
    }

    /// <summary>
    /// Updates the tags of a work item in TFS and refreshes the local cache.
    /// Returns the updated work item DTO, or null if the update failed.
    /// </summary>
    public async Task<SharedWorkItemDto?> UpdateTagsAsync(int tfsId, List<string> tags)
    {
        var json = JsonSerializer.Serialize(new { Tags = tags });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"/api/workitems/{tfsId}/tags", content);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<SharedWorkItemDto>(_jsonOptions);
    }

    /// <summary>
    /// Updates the title and/or description of a work item in TFS and refreshes the local cache.
    /// Returns the updated work item DTO, or null if the update failed.
    /// </summary>
    public async Task<SharedWorkItemDto?> UpdateTitleDescriptionAsync(int tfsId, string? title, string? description)
    {
        var json = JsonSerializer.Serialize(new { Title = title, Description = description });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"/api/workitems/{tfsId}/title-description", content);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<SharedWorkItemDto>(_jsonOptions);
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

    /// <summary>
    /// Gets available bug severity options from the server.
    /// Returns severity values in TFS format (e.g., "1 - Critical", "2 - High", "3 - Medium", "4 - Low").
    /// </summary>
    /// <returns>Collection of severity option strings.</returns>
    public async Task<IEnumerable<string>> GetBugSeverityOptionsAsync()
    {
        var url = "/api/workitems/bug-severity-options";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var severityOptions = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(_jsonOptions);
        return severityOptions ?? Enumerable.Empty<string>();
    }
}
