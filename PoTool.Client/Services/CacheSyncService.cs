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
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            // Handle network errors and invalid JSON responses (e.g., empty response)
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

    /// <summary>
    /// Gets detailed cache insights for a Product Owner.
    /// </summary>
    public async Task<CacheInsightsDto?> GetCacheInsightsAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CacheInsightsDto>(
                $"api/CacheSync/{productOwnerId}/insights",
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
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
            var queryParts = new List<string> { $"workItemId={workItemId}" };
            if (fromChangedDate.HasValue)
            {
                queryParts.Add($"fromChangedDate={Uri.EscapeDataString(fromChangedDate.Value.ToString("O"))}");
            }

            if (toChangedDate.HasValue)
            {
                queryParts.Add($"toChangedDate={Uri.EscapeDataString(toChangedDate.Value.ToString("O"))}");
            }

            var requestUri = $"api/CacheSync/{productOwnerId}/activity-ledger-validation?{string.Join("&", queryParts)}";
            return await _httpClient.GetFromJsonAsync<ActivityLedgerValidationDto>(requestUri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
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
            return await _httpClient.GetFromJsonAsync<SyncChangesSummaryDto>(
                $"api/CacheSync/{productOwnerId}/changes-since-sync",
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
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

/// <summary>
/// Result of a sync status check.
/// </summary>
public record SyncStatusResult
{
    public bool IsSyncing { get; init; }
}
