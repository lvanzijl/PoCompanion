using System.Net.Http.Json;
using Core.WorkItems;

namespace Client.Services;

/// <summary>
/// Client service for work item API operations.
/// </summary>
public sealed class WorkItemClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WorkItemClient> _logger;

    public WorkItemClient(HttpClient httpClient, ILogger<WorkItemClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all cached work items from the API.
    /// </summary>
    public async Task<IReadOnlyCollection<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching all work items from API");
            var result = await _httpClient.GetFromJsonAsync<IReadOnlyCollection<WorkItemDto>>(
                "api/workitems",
                cancellationToken);
            return result ?? Array.Empty<WorkItemDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching work items");
            throw;
        }
    }

    /// <summary>
    /// Gets a work item by its TFS ID.
    /// </summary>
    public async Task<WorkItemDto?> GetByIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching work item {TfsId} from API", tfsId);
            return await _httpClient.GetFromJsonAsync<WorkItemDto>(
                $"api/workitems/{tfsId}",
                cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Work item {TfsId} not found", tfsId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching work item {TfsId}", tfsId);
            throw;
        }
    }

    /// <summary>
    /// Gets work items by area path.
    /// </summary>
    public async Task<IReadOnlyCollection<WorkItemDto>> GetByAreaPathAsync(
        string areaPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching work items for area path {AreaPath}", areaPath);
            var result = await _httpClient.GetFromJsonAsync<IReadOnlyCollection<WorkItemDto>>(
                $"api/workitems/by-area/{Uri.EscapeDataString(areaPath)}",
                cancellationToken);
            return result ?? Array.Empty<WorkItemDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching work items for area path {AreaPath}", areaPath);
            throw;
        }
    }

    /// <summary>
    /// Gets the timestamp of the last cache update.
    /// </summary>
    public async Task<DateTimeOffset?> GetLastUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching last update timestamp");
            return await _httpClient.GetFromJsonAsync<DateTimeOffset?>(
                "api/workitems/last-update",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching last update timestamp");
            throw;
        }
    }
}
