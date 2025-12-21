using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing TFS configuration via the API.
/// PAT is stored client-side in secure storage, not on the server.
/// </summary>
public class TfsConfigService
{
    private readonly IClient _apiClient;
    private readonly ISecureStorageService _secureStorage;
    private const string PatStorageKey = "tfs_pat";

    public TfsConfigService(IClient apiClient, ISecureStorageService secureStorage)
    {
        _apiClient = apiClient;
        _secureStorage = secureStorage;
    }

    /// <summary>
    /// Gets the current TFS configuration (without PAT).
    /// </summary>
    public virtual async Task<TfsConfigDto?> GetConfigAsync(CancellationToken cancellationToken = default)
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
    /// PAT is stored client-side in secure storage, other config is sent to API.
    /// </summary>
    public virtual async Task SaveConfigAsync(string url, string project, string pat, TfsAuthMode authMode = TfsAuthMode.Pat, 
        bool useDefaultCredentials = false, int timeoutSeconds = 30, string apiVersion = "7.0", CancellationToken cancellationToken = default)
    {
        // Store PAT in secure storage (client-side only)
        if (!string.IsNullOrEmpty(pat))
        {
            await _secureStorage.SetAsync(PatStorageKey, pat);
        }

        // Send non-sensitive config to API (PAT is NOT sent to server)
        var request = new TfsConfigRequest
        {
            Url = url,
            Project = project,
            Pat = null // PAT is never sent to server for storage
        };
        
        await _apiClient.PostApiTfsconfigAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets the stored PAT from secure storage.
    /// </summary>
    public virtual async Task<string?> GetPatAsync()
    {
        return await _secureStorage.GetAsync(PatStorageKey);
    }

    /// <summary>
    /// Validates the TFS connection using the provided PAT.
    /// </summary>
    public virtual async Task<bool> ValidateConnectionAsync(string? pat = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // If no PAT provided, try to get from secure storage
            pat ??= await GetPatAsync();

            if (string.IsNullOrEmpty(pat))
            {
                return false;
            }

            // TODO: Update API to accept PAT for validation
            // For now, use existing endpoint
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
    public virtual async Task<bool> RequestSyncAsync(CancellationToken cancellationToken = default)
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
    public virtual async Task<bool> RequestIncrementalSyncAsync(CancellationToken cancellationToken = default)
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
