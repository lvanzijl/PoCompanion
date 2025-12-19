using System.Net.Http.Json;

namespace PoTool.Client.ApiClient;

/// <summary>
/// HTTP client implementation for work items API.
/// Generated from OpenAPI specification.
/// </summary>
public class WorkItemsClient : IWorkItemsClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkItemsClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for API communication.</param>
    public WorkItemsClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/workitems", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<WorkItemDto>>(cancellationToken: cancellationToken);
        return result ?? Enumerable.Empty<WorkItemDto>();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filter))
            throw new ArgumentException("Filter cannot be null or empty", nameof(filter));

        var response = await _httpClient.GetAsync($"api/workitems/filter/{Uri.EscapeDataString(filter)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<WorkItemDto>>(cancellationToken: cancellationToken);
        return result ?? Enumerable.Empty<WorkItemDto>();
    }

    /// <inheritdoc/>
    public async Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/workitems/{tfsId}", cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<WorkItemDto>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<WorkItemDto>> GetGoalHierarchyAsync(IEnumerable<int> goalIds, CancellationToken cancellationToken = default)
    {
        if (goalIds == null || !goalIds.Any())
            throw new ArgumentException("At least one goal ID must be provided", nameof(goalIds));

        var goalIdsParam = string.Join(",", goalIds);
        var response = await _httpClient.GetAsync($"api/workitems/goals?goalIds={goalIdsParam}", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<WorkItemDto>>(cancellationToken: cancellationToken);
        return result ?? Enumerable.Empty<WorkItemDto>();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<WorkItemWithValidationDto>> GetAllWithValidationAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/workitems/validated", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<WorkItemWithValidationDto>>(cancellationToken: cancellationToken);
        return result ?? Enumerable.Empty<WorkItemWithValidationDto>();
    }
}
