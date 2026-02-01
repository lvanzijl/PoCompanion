using System.Net.Http.Json;
using PoTool.Shared.BugTriage;

namespace PoTool.Client.Services;

/// <summary>
/// Client service for bug triage state management.
/// Communicates with the BugTriageController via generated API client.
/// </summary>
public class BugTriageService
{
    // TODO: Inject IBugTriageClient once NSwag regenerates
    // private readonly IBugTriageClient _bugTriageClient;
    
    // Temporary: Use HttpClient directly until NSwag client is generated
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "api/BugTriage";

    public BugTriageService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets triage state for a specific bug.
    /// </summary>
    public async Task<BugTriageStateDto?> GetTriageStateAsync(int bugId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/{bugId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<BugTriageStateDto>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets triage states for multiple bugs.
    /// </summary>
    public async Task<List<BugTriageStateDto>> GetTriageStatesAsync(
        List<int> bugIds,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/states", bugIds, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<BugTriageStateDto>>(cancellationToken)
            ?? new List<BugTriageStateDto>();
    }

    /// <summary>
    /// Gets IDs of all untriaged bugs from a given list.
    /// </summary>
    public async Task<HashSet<int>> GetUntriagedBugIdsAsync(
        List<int> bugIds,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/untriaged", bugIds, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HashSet<int>>(cancellationToken)
            ?? new HashSet<int>();
    }

    /// <summary>
    /// Records that a bug was first seen in the triage UI.
    /// </summary>
    public async Task RecordFirstSeenAsync(
        int bugId,
        string currentCriticality,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/first-seen?bugId={bugId}&currentCriticality={Uri.EscapeDataString(currentCriticality)}",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Marks a bug as triaged due to a user action.
    /// Logs what would be saved to TFS (criticality and/or tags).
    /// </summary>
    public async Task<UpdateBugTriageStateResponse> MarkAsTriagedAsync(
        UpdateBugTriageStateRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/mark-triaged", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UpdateBugTriageStateResponse>(cancellationToken)
            ?? new UpdateBugTriageStateResponse(false, "Unknown error");
    }
}
