using System.Net.Http.Json;
using PoTool.Client.ApiClient;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

/// <summary>
/// Abstraction for interacting with cache sync operations.
/// </summary>
public interface ICacheSyncService
{
    Task<CacheStateDto?> GetCacheStatusAsync(int productOwnerId, CancellationToken cancellationToken = default);
    Task<SyncTriggerResult> TriggerSyncAsync(int productOwnerId, CancellationToken cancellationToken = default);
    Task<bool> CancelSyncAsync(int productOwnerId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCacheAsync(int productOwnerId, CancellationToken cancellationToken = default);
    Task<bool> IsSyncRunningAsync(int productOwnerId, CancellationToken cancellationToken = default);
    Task<CacheInsightsDto?> GetCacheInsightsAsync(int productOwnerId, CancellationToken cancellationToken = default);
    Task<CacheResetResponse?> ResetCacheSelectiveAsync(int productOwnerId, CacheResetRequest request, CancellationToken cancellationToken = default);
    Task<ActivityLedgerValidationDto?> GetActivityLedgerValidationAsync(int productOwnerId, int workItemId, DateTimeOffset? fromChangedDate, DateTimeOffset? toChangedDate, CancellationToken cancellationToken = default);
    Task<SyncChangesSummaryDto?> GetChangesSinceSyncAsync(int productOwnerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for interacting with cache sync operations.
/// </summary>
public class CacheSyncService : ICacheSyncService
{
    private readonly HttpClient _httpClient;
    private readonly ICacheSyncClient _cacheSyncClient;

    public CacheSyncService(HttpClient httpClient, ICacheSyncClient cacheSyncClient)
    {
        _httpClient = httpClient;
        _cacheSyncClient = cacheSyncClient;
    }

    /// <summary>
    /// Gets the cache status for a Product Owner.
    /// </summary>
    public async Task<CacheStateDto?> GetCacheStatusAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cacheSyncClient.GetCacheStatusAsync(productOwnerId, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
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
            var response = await _cacheSyncClient.GetSyncStatusAsync(productOwnerId, cancellationToken);
            return response?.IsSyncing ?? false;
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets detailed cache insights for a Product Owner.
    /// </summary>
    public async Task<CacheInsightsDto?> GetCacheInsightsAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cacheSyncClient.GetCacheInsightsAsync(productOwnerId, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resets specific cache entity types.
    /// </summary>
    public async Task<CacheResetResponse?> ResetCacheSelectiveAsync(
        int productOwnerId,
        CacheResetRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/CacheSync/{productOwnerId}/reset",
                request,
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CacheResetResponse>(cancellationToken: cancellationToken);
            }
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<ActivityLedgerValidationDto?> GetActivityLedgerValidationAsync(
        int productOwnerId,
        int workItemId,
        DateTimeOffset? fromChangedDate,
        DateTimeOffset? toChangedDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cacheSyncClient.GetActivityLedgerValidationAsync(
                productOwnerId,
                workItemId,
                fromChangedDate,
                toChangedDate,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a summary of work-item and sprint changes detected in the last sync window.
    /// Returns null on error (network/JSON failure).
    /// </summary>
    public async Task<SyncChangesSummaryDto?> GetChangesSinceSyncAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cacheSyncClient.GetChangesSinceSyncAsync(productOwnerId, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            return null;
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
