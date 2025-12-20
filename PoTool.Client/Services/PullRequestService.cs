using System.Net.Http.Json;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Service for interacting with the Pull Requests API.
/// </summary>
public class PullRequestService
{
    private readonly HttpClient _httpClient;

    public PullRequestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets all cached pull requests.
    /// </summary>
    public async Task<IEnumerable<PullRequestDto>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PullRequestDto>>("/api/pullrequests") 
            ?? Array.Empty<PullRequestDto>();
    }

    /// <summary>
    /// Gets a specific pull request by ID.
    /// </summary>
    public async Task<PullRequestDto?> GetByIdAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<PullRequestDto>($"/api/pullrequests/{id}");
    }

    /// <summary>
    /// Gets aggregated metrics for all pull requests.
    /// </summary>
    public async Task<IEnumerable<PullRequestMetricsDto>> GetMetricsAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PullRequestMetricsDto>>("/api/pullrequests/metrics") 
            ?? Array.Empty<PullRequestMetricsDto>();
    }

    /// <summary>
    /// Gets filtered pull requests.
    /// </summary>
    public async Task<IEnumerable<PullRequestDto>> GetFilteredAsync(
        string? iterationPath = null,
        string? createdBy = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        string? status = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(iterationPath))
            query.Add($"iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (!string.IsNullOrWhiteSpace(createdBy))
            query.Add($"createdBy={Uri.EscapeDataString(createdBy)}");
        if (fromDate.HasValue)
            query.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}");
        if (toDate.HasValue)
            query.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}");
        if (!string.IsNullOrWhiteSpace(status))
            query.Add($"status={Uri.EscapeDataString(status)}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await _httpClient.GetFromJsonAsync<IEnumerable<PullRequestDto>>($"/api/pullrequests/filter{queryString}") 
            ?? Array.Empty<PullRequestDto>();
    }

    /// <summary>
    /// Synchronizes pull requests from TFS.
    /// </summary>
    public async Task<int> SyncAsync()
    {
        var response = await _httpClient.PostAsync("/api/pullrequests/sync", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        return result?.SyncedCount ?? 0;
    }

    private class SyncResult
    {
        public int SyncedCount { get; set; }
    }
}
