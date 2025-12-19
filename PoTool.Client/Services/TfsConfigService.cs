using System.Net.Http.Json;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing TFS configuration via the API.
/// </summary>
public class TfsConfigService
{
    private readonly HttpClient _httpClient;

    public TfsConfigService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the current TFS configuration.
    /// </summary>
    public async Task<TfsConfigDto?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tfsconfig", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TfsConfigDto>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the TFS configuration.
    /// </summary>
    public async Task SaveConfigAsync(string url, string project, string pat, CancellationToken cancellationToken = default)
    {
        var payload = new { Url = url, Project = project, Pat = pat };
        var response = await _httpClient.PostAsJsonAsync("/api/tfsconfig", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Validates the TFS connection.
    /// </summary>
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tfsvalidate", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>
    /// Requests a work item sync operation.
    /// </summary>
    public async Task<bool> RequestSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/workitems/sync", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}

/// <summary>
/// DTO for TFS configuration.
/// </summary>
public class TfsConfigDto
{
    public string? Url { get; set; }
    public string? Project { get; set; }
}
