using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Exceptions;
using PoTool.Api.Hubs;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for cache sync operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CacheSyncController : ControllerBase
{
    private readonly ICacheStateRepository _cacheStateRepository;
    private readonly ISyncPipeline _syncPipeline;
    private readonly ISyncProgressBroadcaster _broadcaster;
    private readonly ILogger<CacheSyncController> _logger;

    public CacheSyncController(
        ICacheStateRepository cacheStateRepository,
        ISyncPipeline syncPipeline,
        ISyncProgressBroadcaster broadcaster,
        ILogger<CacheSyncController> logger)
    {
        _cacheStateRepository = cacheStateRepository;
        _syncPipeline = syncPipeline;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <summary>
    /// Gets the cache status for a Product Owner.
    /// </summary>
    [HttpGet("{productOwnerId}")]
    [ProducesResponseType(typeof(CacheStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CacheStateDto>> GetCacheStatus(int productOwnerId, CancellationToken cancellationToken)
    {
        try
        {
            // Return cache state (create if doesn't exist for valid ProductOwner)
            var cacheState = await _cacheStateRepository.GetOrCreateCacheStateAsync(productOwnerId, cancellationToken);
            return Ok(cacheState);
        }
        catch (ProductOwnerNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Triggers a cache sync for a Product Owner.
    /// Returns immediately - use SignalR or polling for progress updates.
    /// </summary>
    [HttpPost("{productOwnerId}/sync")]
    [ProducesResponseType(typeof(SyncTriggerResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(SyncTriggerResponse), StatusCodes.Status409Conflict)]
    public ActionResult<SyncTriggerResponse> TriggerSync(int productOwnerId)
    {
        if (_syncPipeline.IsSyncRunning(productOwnerId))
        {
            return Conflict(new SyncTriggerResponse
            {
                Accepted = false,
                Message = "Sync is already running for this Product Owner"
            });
        }

        _logger.LogInformation("Triggering cache sync for ProductOwner {ProductOwnerId}", productOwnerId);

        // Fire and forget - the sync runs in the background
        // Progress updates are broadcast via SignalR
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var update in _syncPipeline.ExecuteAsync(productOwnerId))
                {
                    // Broadcast progress via SignalR
                    await _broadcaster.BroadcastProgressAsync(productOwnerId, update);
                    
                    _logger.LogDebug(
                        "Sync progress for {ProductOwnerId}: Stage={Stage}, Progress={Progress}%",
                        productOwnerId,
                        update.CurrentStage,
                        update.StageProgressPercent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed for ProductOwner {ProductOwnerId}", productOwnerId);
            }
        });

        return Accepted(new SyncTriggerResponse
        {
            Accepted = true,
            Message = "Sync started"
        });
    }

    /// <summary>
    /// Cancels a running sync for a Product Owner.
    /// </summary>
    [HttpPost("{productOwnerId}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult CancelSync(int productOwnerId)
    {
        if (!_syncPipeline.IsSyncRunning(productOwnerId))
        {
            return NotFound(new { message = "No sync is running for this Product Owner" });
        }

        _syncPipeline.CancelSync(productOwnerId);
        return Ok(new { message = "Sync cancellation requested" });
    }

    /// <summary>
    /// Deletes/resets the cache for a Product Owner.
    /// </summary>
    [HttpDelete("{productOwnerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteCache(int productOwnerId, CancellationToken cancellationToken)
    {
        if (_syncPipeline.IsSyncRunning(productOwnerId))
        {
            return Conflict(new { message = "Cannot delete cache while sync is running" });
        }

        try
        {
            await _cacheStateRepository.ResetCacheStateAsync(productOwnerId, cancellationToken);
            
            _logger.LogInformation("Cache reset for ProductOwner {ProductOwnerId}", productOwnerId);
            
            return Ok(new { message = "Cache reset successfully" });
        }
        catch (ProductOwnerNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Checks if a sync is currently running for a Product Owner.
    /// </summary>
    [HttpGet("{productOwnerId}/status")]
    [ProducesResponseType(typeof(SyncStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<SyncStatusResponse> GetSyncStatus(int productOwnerId)
    {
        return Ok(new SyncStatusResponse
        {
            IsSyncing = _syncPipeline.IsSyncRunning(productOwnerId)
        });
    }
}

/// <summary>
/// Response for sync trigger operation.
/// </summary>
public record SyncTriggerResponse
{
    public bool Accepted { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Response for sync status check.
/// </summary>
public record SyncStatusResponse
{
    public bool IsSyncing { get; init; }
}
