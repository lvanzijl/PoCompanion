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
    public async Task SaveConfigAsync(string url, string project, string pat, TfsAuthMode authMode = TfsAuthMode.Pat, 
        bool useDefaultCredentials = false, int timeoutSeconds = 30, string apiVersion = "7.0", CancellationToken cancellationToken = default)
    {
        var payload = new 
        { 
            Url = url, 
            Project = project, 
            Pat = pat,
            AuthMode = authMode,
            UseDefaultCredentials = useDefaultCredentials,
            TimeoutSeconds = timeoutSeconds,
            ApiVersion = apiVersion
        };
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

    /// <summary>
    /// Requests an incremental work item sync operation (only changed items since last sync).
    /// </summary>
    public async Task<bool> RequestIncrementalSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/workitems/sync?incremental=true", null, cancellationToken);
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
    public TfsAuthMode AuthMode { get; set; } = TfsAuthMode.Pat;
    public bool UseDefaultCredentials { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiVersion { get; set; } = "7.0";
    public DateTimeOffset? LastValidated { get; set; }
}

/// <summary>
/// Authentication mode for TFS/Azure DevOps.
/// </summary>
public enum TfsAuthMode
{
    /// <summary>
    /// Personal Access Token authentication.
    /// </summary>
    Pat = 0,
    
    /// <summary>
    /// NTLM/Windows authentication (on-premises TFS only).
    /// </summary>
    Ntlm = 1
}
