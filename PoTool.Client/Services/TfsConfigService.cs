using PoTool.Client.ApiClient;
using System.Net.Http.Json;
using System.Text.Json;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing TFS configuration via the API.
/// PAT is stored client-side in secure storage, not on the server.
/// </summary>
public class TfsConfigService
{
    private readonly IClient _apiClient;
    private readonly ISecureStorageService _secureStorage;
    private readonly HttpClient _httpClient;
    private const string PatStorageKey = "tfs_pat";

    public TfsConfigService(IClient apiClient, ISecureStorageService secureStorage, HttpClient httpClient)
    {
        _apiClient = apiClient;
        _secureStorage = secureStorage;
        _httpClient = httpClient;
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

    /// <summary>
    /// Verifies TFS API capabilities by running diagnostic checks.
    /// </summary>
    /// <param name="includeWriteChecks">Whether to include write capability checks.</param>
    /// <param name="workItemIdForWriteCheck">Optional work item ID to use for write checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete verification report with check results.</returns>
    public virtual async Task<TfsVerificationReportDto?> VerifyTfsApiAsync(
        bool includeWriteChecks = false,
        int? workItemIdForWriteCheck = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get PAT from secure storage
            var pat = await GetPatAsync();
            if (string.IsNullOrEmpty(pat))
            {
                throw new InvalidOperationException("PAT is required for TFS API verification");
            }

            // Create request with PAT header
            var request = new HttpRequestMessage(HttpMethod.Post, "api/tfsverify");
            request.Headers.Add("X-TFS-PAT", pat);
            request.Content = JsonContent.Create(new
            {
                IncludeWriteChecks = includeWriteChecks,
                WorkItemIdForWriteCheck = workItemIdForWriteCheck
            });
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var report = await response.Content.ReadFromJsonAsync<TfsVerificationReportDto>(
                    cancellationToken: cancellationToken);
                return report;
            }
            
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

// DTOs for TFS Verification (temporarily here until NSwag client is regenerated)

/// <summary>
/// Complete report of TFS API capability verification.
/// </summary>
public class TfsVerificationReportDto
{
    public DateTimeOffset VerifiedAt { get; set; }
    public string ServerUrl { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public bool IncludedWriteChecks { get; set; }
    public bool Success { get; set; }
    public List<TfsCapabilityCheckResultDto> Checks { get; set; } = new();
    public string Summary => $"{Checks.Count(c => c.Success)}/{Checks.Count} checks passed";
    public List<string> ImpactedFunctionalities => 
        Checks.Where(c => !c.Success)
              .Select(c => c.ImpactedFunctionality)
              .Distinct()
              .ToList();
}

/// <summary>
/// Result of a single TFS capability verification check.
/// </summary>
public class TfsCapabilityCheckResultDto
{
    public string CapabilityId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ImpactedFunctionality { get; set; } = string.Empty;
    public string ExpectedBehavior { get; set; } = string.Empty;
    public string? ObservedBehavior { get; set; }
    public string? FailureCategory { get; set; }
    public string? RawEvidence { get; set; }
    public List<string>? LikelyCauses { get; set; }
    public List<string>? ResolutionGuidance { get; set; }
    public string? TargetScope { get; set; }
    public string? MutationType { get; set; }
    public string? CleanupStatus { get; set; }
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
