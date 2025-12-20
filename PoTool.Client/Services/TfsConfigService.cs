using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing TFS configuration via the API.
/// </summary>
public class TfsConfigService
{
    private readonly IClient _apiClient;

    public TfsConfigService(IClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Gets the current TFS configuration.
    /// </summary>
    public async Task<TfsConfigDto?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _apiClient.GetApiTfsconfigAsync(cancellationToken);
            // The API returns 204 NoContent when config doesn't exist, which causes an exception
            // For now, return a placeholder - this needs API to return proper response
            return null;
        }
        catch (ApiException ex) when (ex.StatusCode == 204)
        {
            return null;
        }
        catch (ApiException)
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
        var request = new TfsConfigRequest
        {
            Url = url,
            Project = project,
            Pat = pat
        };
        
        await _apiClient.PostApiTfsconfigAsync(request, cancellationToken);
    }

    /// <summary>
    /// Validates the TFS connection.
    /// </summary>
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _apiClient.GetApiTfsvalidateAsync(cancellationToken);
            return true;
        }
        catch (ApiException)
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
            await _apiClient.PostApiWorkitemsSyncAsync(cancellationToken);
            return true;
        }
        catch (ApiException)
        {
            return false;
        }
    }

    /// <summary>
    /// Requests an incremental work item sync operation (only changed items since last sync).
    /// </summary>
    public async Task<bool> RequestIncrementalSyncAsync(CancellationToken cancellationToken = default)
    {
        // Note: The generated client doesn't support the incremental parameter yet
        // This will need to be handled differently or the API needs to expose a separate endpoint
        try
        {
            await _apiClient.PostApiWorkitemsSyncAsync(cancellationToken);
            return true;
        }
        catch (ApiException)
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
