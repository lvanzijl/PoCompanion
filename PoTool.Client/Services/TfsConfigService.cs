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
    
    // JSON options for case-insensitive deserialization of API responses (camelCase to PascalCase)
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            // Use HttpClient directly since the generated client doesn't return the response
            var response = await _httpClient.GetAsync("/api/tfsconfig", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            var config = await response.Content.ReadFromJsonAsync<TfsConfigDto>(_jsonOptions, cancellationToken);
            return config;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the TFS configuration.
    /// PAT is stored client-side in secure storage, other config is sent to API.
    /// </summary>
    public virtual async Task SaveConfigAsync(string url, string project, string pat, TfsAuthMode authMode = TfsAuthMode.Ntlm, 
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
            Pat = null, // PAT is never sent to server for storage
            AuthMode = (int)authMode,
            UseDefaultCredentials = useDefaultCredentials,
            TimeoutSeconds = timeoutSeconds,
            ApiVersion = apiVersion
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

            // Create request with PAT header if available (for PAT auth mode)
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/tfsvalidate");
            if (!string.IsNullOrEmpty(pat))
            {
                request.Headers.Add("X-TFS-PAT", pat);
            }

            // Call the validation endpoint
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                // Try to read error details from response
                string errorMessage = $"Connection test failed with HTTP {(int)response.StatusCode} ({response.StatusCode})";
                string? detailsText = null;
                
                try
                {
                    var errorJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(errorJson))
                    {
                        var errorDetails = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(errorJson, _jsonOptions);
                        if (errorDetails != null)
                        {
                            // Try to get message first, then details
                            if (errorDetails.TryGetValue("message", out var message))
                            {
                                errorMessage = message.GetString() ?? errorMessage;
                            }
                            if (errorDetails.TryGetValue("details", out var details))
                            {
                                detailsText = details.GetString();
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Failed to parse error response, use generic message with status code
                }
                
                // Always throw an exception with details so the UI can display them
                var fullMessage = !string.IsNullOrWhiteSpace(detailsText) 
                    ? $"{errorMessage}. {detailsText}" 
                    : errorMessage;
                throw new InvalidOperationException(fullMessage);
            }
        }
        catch (ApiException ex)
        {
            throw new InvalidOperationException($"Connection test failed: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Connection test failed: Unable to reach server. {ex.Message}", ex);
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
    public virtual async Task<TfsVerificationReport?> VerifyTfsApiAsync(
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

            // Use direct HttpClient call with request-specific headers for thread safety
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/tfsverify")
            {
                Content = JsonContent.Create(new TfsVerifyRequest
                {
                    IncludeWriteChecks = includeWriteChecks,
                    WorkItemIdForWriteCheck = workItemIdForWriteCheck
                })
            };
            requestMessage.Headers.Add("X-TFS-PAT", pat);

            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var report = await response.Content.ReadFromJsonAsync<TfsVerificationReport>(_jsonOptions, cancellationToken);
            return report;
        }
        catch (ApiException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
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
    public TfsAuthMode AuthMode { get; set; } = TfsAuthMode.Ntlm;
    public bool UseDefaultCredentials { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiVersion { get; set; } = "7.0";
    public DateTimeOffset? LastValidated { get; set; }
}
