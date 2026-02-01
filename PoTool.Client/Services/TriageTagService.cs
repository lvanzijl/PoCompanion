using System.Net.Http.Json;
using PoTool.Shared.BugTriage;

namespace PoTool.Client.Services;

/// <summary>
/// Client service for triage tag catalog management.
/// Communicates with the TriageTagsController via generated API client.
/// </summary>
public class TriageTagService
{
    // TODO: Inject ITriageTagsClient once NSwag regenerates
    // private readonly ITriageTagsClient _triageTagsClient;
    
    // Temporary: Use HttpClient directly until NSwag client is generated
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "api/TriageTags";

    public TriageTagService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets all triage tags.
    /// </summary>
    public async Task<List<TriageTagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(_baseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TriageTagDto>>(cancellationToken)
            ?? new List<TriageTagDto>();
    }

    /// <summary>
    /// Gets only enabled triage tags.
    /// </summary>
    public async Task<List<TriageTagDto>> GetEnabledTagsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/enabled", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TriageTagDto>>(cancellationToken)
            ?? new List<TriageTagDto>();
    }

    /// <summary>
    /// Creates a new triage tag.
    /// </summary>
    public async Task<TriageTagOperationResponse> CreateTagAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateTriageTagRequest(name);
        var response = await _httpClient.PostAsJsonAsync(_baseUrl, request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TriageTagOperationResponse>(cancellationToken)
            ?? new TriageTagOperationResponse(false, "Unknown error");
    }

    /// <summary>
    /// Updates a triage tag.
    /// </summary>
    public async Task<TriageTagOperationResponse> UpdateTagAsync(
        int id,
        string? name = null,
        bool? isEnabled = null,
        int? displayOrder = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateTriageTagRequest(id, name, isEnabled, displayOrder);
        var response = await _httpClient.PutAsJsonAsync($"{_baseUrl}/{id}", request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TriageTagOperationResponse>(cancellationToken)
            ?? new TriageTagOperationResponse(false, "Unknown error");
    }

    /// <summary>
    /// Deletes a triage tag.
    /// </summary>
    public async Task<TriageTagOperationResponse> DeleteTagAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"{_baseUrl}/{id}", cancellationToken);
        return await response.Content.ReadFromJsonAsync<TriageTagOperationResponse>(cancellationToken)
            ?? new TriageTagOperationResponse(false, "Unknown error");
    }

    /// <summary>
    /// Reorders triage tags.
    /// </summary>
    public async Task<TriageTagOperationResponse> ReorderTagsAsync(
        List<int> tagIds,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/reorder", tagIds, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TriageTagOperationResponse>(cancellationToken)
            ?? new TriageTagOperationResponse(false, "Unknown error");
    }
}
