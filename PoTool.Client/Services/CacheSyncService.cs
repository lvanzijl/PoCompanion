using System.Net.Http.Json;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

/// <summary>
/// Service for interacting with cache sync operations.
/// </summary>
public class CacheSyncService
{
    private readonly HttpClient _httpClient;

    public CacheSyncService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the cache status for a Product Owner.
    /// </summary>
    public async Task<CacheStateDto?> GetCacheStatusAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CacheStateDto>(
                $"api/CacheSync/{productOwnerId}",
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Triggers a cache sync for a Product Owner.
    /// </summary>
    public async Task<SyncTriggerResult> TriggerSyncAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"api/CacheSync/{productOwnerId}/sync",
                null,
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                return new SyncTriggerResult { Success = true };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return new SyncTriggerResult { Success = false, Message = "Sync already in progress" };
            }
            else
            {
                return new SyncTriggerResult { Success = false, Message = $"Unexpected status: {response.StatusCode}" };
            }
        }
        catch (HttpRequestException ex)
        {
            return new SyncTriggerResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Cancels a running sync for a Product Owner.
    /// </summary>
    public async Task<bool> CancelSyncAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"api/CacheSync/{productOwnerId}/cancel",
                null,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes/resets the cache for a Product Owner.
    /// </summary>
    public async Task<bool> DeleteCacheAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"api/CacheSync/{productOwnerId}",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a sync is currently running for a Product Owner.
    /// </summary>
    public async Task<bool> IsSyncRunningAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<SyncStatusResult>(
                $"api/CacheSync/{productOwnerId}/status",
                cancellationToken);
            return response?.IsSyncing ?? false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}

/// <summary>
/// Result of a sync trigger operation.
/// </summary>
public record SyncTriggerResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

/// <summary>
/// Result of a sync status check.
/// </summary>
public record SyncStatusResult
{
    public bool IsSyncing { get; init; }
}
