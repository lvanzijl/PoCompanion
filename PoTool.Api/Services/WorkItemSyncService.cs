using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Api.Repositories;
using System.Linq;

namespace PoTool.Api.Services;

/// <summary>
/// Background service for work item synchronization with two-level progress reporting.
/// </summary>
public class WorkItemSyncService : BackgroundService
{
    /// <summary>
    /// Safety overlap in minutes for incremental sync to account for clock drift
    /// and ensure no items are missed between syncs.
    /// </summary>
    private const int IncrementalSyncOverlapMinutes = 5;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkItemSyncService> _logger;
    private readonly IHubContext<WorkItemHub> _hubContext;

    public WorkItemSyncService(
        IServiceProvider serviceProvider,
        ILogger<WorkItemSyncService> logger,
        IHubContext<WorkItemHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Work Item Sync Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Stub: Future implementation will poll for sync requests
            // or respond to triggers from the UI via SignalR
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("Work Item Sync Service is stopping");
    }

    /// <summary>
    /// Triggers a manual sync of work items from TFS using area path.
    /// </summary>
    public async Task TriggerSyncAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        // Fail fast if area path is not configured
        if (string.IsNullOrWhiteSpace(areaPath))
        {
            var errorMessage = "Default Area Path is not configured. Configure this in TFS settings.";
            _logger.LogError(errorMessage);

            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Failed",
                Message = errorMessage,
                MajorStep = 0,
                MajorStepTotal = 0
            }, cancellationToken);

            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformation("Manual sync triggered for area path: {AreaPath}", areaPath);

        using var scope = _serviceProvider.CreateScope();
        var tfsClient = scope.ServiceProvider.GetService<ITfsClient>();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();

        try
        {
            await SendProgressAsync(new SyncProgressDto
            {
                Status = "InProgress",
                Message = "Retrieving work items...",
                MajorStep = 1,
                MajorStepTotal = 2,
                MajorStepLabel = "Querying TFS"
            }, cancellationToken);

            IEnumerable<WorkItemDto> workItems;

            if (tfsClient != null)
            {
                workItems = await tfsClient.GetWorkItemsAsync(areaPath, cancellationToken);
            }
            else
            {
                // No ITfsClient available (development scenario). Use repository contents.
                _logger.LogInformation("No ITfsClient registered; using repository contents for sync");
                workItems = await repository.GetAllAsync(cancellationToken);
            }

            await SendProgressAsync(new SyncProgressDto
            {
                Status = "InProgress",
                Message = $"Saving {workItems.Count()} work items to cache...",
                MajorStep = 2,
                MajorStepTotal = 2,
                MajorStepLabel = "Saving to Cache",
                ProcessedCount = workItems.Count(),
                TotalCount = workItems.Count()
            }, cancellationToken);

            await repository.ReplaceAllAsync(workItems, cancellationToken);

            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Completed",
                Message = $"Successfully synced {workItems.Count()} work items",
                MajorStep = 2,
                MajorStepTotal = 2,
                MajorStepLabel = "Complete",
                ProcessedCount = workItems.Count(),
                TotalCount = workItems.Count()
            }, cancellationToken);

            _logger.LogInformation("Sync completed successfully for area path: {AreaPath}", areaPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Sync was cancelled for area path: {AreaPath}", areaPath);
            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Cancelled",
                Message = "Sync was cancelled"
            }, cancellationToken);
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not user cancellation)
            _logger.LogError(ex, "Sync timed out for area path: {AreaPath}", areaPath);
            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Failed",
                Message = "Sync timed out. Try reducing scope or increasing timeout."
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync for area path: {AreaPath}", areaPath);

            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Failed",
                Message = $"Sync failed: {ex.Message}"
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Triggers a sync of work items starting from specified root work item IDs.
    /// This is the preferred method for product-scoped sync operations.
    /// </summary>
    public async Task TriggerSyncByRootIdsAsync(
        int[] rootWorkItemIds,
        bool incremental = false,
        CancellationToken cancellationToken = default)
    {
        if (rootWorkItemIds == null || rootWorkItemIds.Length == 0)
        {
            var errorMessage = "No root work items configured for this product/profile.";
            _logger.LogError(errorMessage);

            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Failed",
                Message = errorMessage,
                MajorStep = 0,
                MajorStepTotal = 0
            }, cancellationToken);

            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformation(">>> FULL SYNC STARTED for {Count} root work items: [{Ids}], incremental={Incremental}",
            rootWorkItemIds.Length, string.Join(", ", rootWorkItemIds), incremental);

        using var scope = _serviceProvider.CreateScope();
        var tfsClient = scope.ServiceProvider.GetService<ITfsClient>();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();

        try
        {
            _logger.LogInformation(">>> Sync Phase: Initializing - Sending initial progress update");
            await SendProgressAsync(new SyncProgressDto
            {
                Status = "InProgress",
                Message = "Starting sync...",
                MajorStep = 1,
                MajorStepTotal = 3,
                MajorStepLabel = "Initializing",
                RootWorkItemIds = rootWorkItemIds
            }, cancellationToken);

            IEnumerable<WorkItemDto> workItems;
            DateTimeOffset? since = null;

            if (incremental)
            {
                // For incremental sync, determine "since" time from product-specific last sync
                var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
                var products = await dbContext.Products
                    .Where(p => rootWorkItemIds.Contains(p.BacklogRootWorkItemId))
                    .ToListAsync(cancellationToken);

                // Get the OLDEST LastSyncedAt across all products being synced
                // This ensures we capture changes for any product that may have been synced longer ago
                var oldestSyncTime = products
                    .Where(p => p.LastSyncedAt.HasValue)
                    .OrderBy(p => p.LastSyncedAt)
                    .FirstOrDefault()?.LastSyncedAt;

                since = oldestSyncTime?.AddMinutes(-IncrementalSyncOverlapMinutes);

                if (since.HasValue)
                {
                    _logger.LogInformation(
                        "Incremental sync: using oldest product sync time {SinceTime} (with {OverlapMinutes}min overlap) across {ProductCount} products",
                        since.Value, IncrementalSyncOverlapMinutes, products.Count);
                }
                else
                {
                    _logger.LogInformation(
                        "Incremental sync requested but no products have been synced. Performing full sync.");
                }
            }

            if (tfsClient != null)
            {
                _logger.LogInformation(">>> Sync Phase 2: Fetching from TFS - Calling GetWorkItemsByRootIdsAsync");
                // Use the new root-based sync method with progress callback
                workItems = await tfsClient.GetWorkItemsByRootIdsAsync(
                    rootWorkItemIds,
                    since,
                    (step, total, label) => 
                    {
                        // This callback fires during TFS retrieval
                        _logger.LogInformation(">>> TFS Fetch Progress: Step {Step}/{Total} - {Label}", step, total, label);
                        _ = SendProgressAsync(new SyncProgressDto
                        {
                            Status = "InProgress",
                            Message = label,
                            MajorStep = 2,
                            MajorStepTotal = 3,
                            MajorStepLabel = "Fetching from TFS",
                            MinorStep = step,
                            MinorStepTotal = total,
                            MinorStepLabel = label,
                            RootWorkItemIds = rootWorkItemIds
                        }, cancellationToken);
                    },
                    cancellationToken);
                _logger.LogInformation(">>> GetWorkItemsByRootIdsAsync completed - Retrieved {Count} work items", workItems.Count());
            }
            else
            {
                // No ITfsClient available (development scenario). Use repository contents.
                _logger.LogInformation("No ITfsClient registered; using repository contents for sync");
                workItems = await repository.GetAllAsync(cancellationToken);
            }

            var workItemList = workItems.ToList();

            _logger.LogInformation(">>> Sync Phase 3: Saving to Cache - {Count} work items", workItemList.Count);
            await SendProgressAsync(new SyncProgressDto
            {
                Status = "InProgress",
                Message = $"Saving {workItemList.Count} work items to cache...",
                MajorStep = 3,
                MajorStepTotal = 3,
                MajorStepLabel = "Saving to Cache",
                ProcessedCount = workItemList.Count,
                TotalCount = workItemList.Count,
                RootWorkItemIds = rootWorkItemIds
            }, cancellationToken);

            if (incremental && since.HasValue)
            {
                // For incremental sync, merge with existing items
                _logger.LogInformation(">>> Performing incremental upsert for {Count} work items", workItemList.Count);
                await repository.UpsertManyAsync(workItemList, cancellationToken);
            }
            else
            {
                // For full sync, replace all
                _logger.LogInformation(">>> Performing full replace for {Count} work items", workItemList.Count);
                await repository.ReplaceAllAsync(workItemList, cancellationToken);
            }

            _logger.LogInformation(">>> Sync Phase 3: Complete - Sending completion progress");
            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Completed",
                Message = $"Successfully synced {workItemList.Count} work items",
                MajorStep = 3,
                MajorStepTotal = 3,
                MajorStepLabel = "Complete",
                ProcessedCount = workItemList.Count,
                TotalCount = workItemList.Count,
                RootWorkItemIds = rootWorkItemIds
            }, cancellationToken);

            // Update LastSyncedAt for all products that were synced
            await UpdateProductLastSyncedAtAsync(rootWorkItemIds, cancellationToken);

            _logger.LogInformation(">>> FULL SYNC COMPLETED SUCCESSFULLY for {Count} root work items", rootWorkItemIds.Length);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(">>> SYNC CANCELLED for root work items: [{Ids}]", string.Join(", ", rootWorkItemIds));
            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Cancelled",
                Message = "Sync was cancelled",
                RootWorkItemIds = rootWorkItemIds
            }, cancellationToken);
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not user cancellation)
            _logger.LogError(ex, ">>> SYNC TIMED OUT for root work items: [{Ids}]", string.Join(", ", rootWorkItemIds));
            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Failed",
                Message = "Sync timed out. The operation took too long. Try syncing with fewer products.",
                RootWorkItemIds = rootWorkItemIds
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> SYNC FAILED for root work items: [{Ids}]", string.Join(", ", rootWorkItemIds));

            await SendProgressAsync(new SyncProgressDto
            {
                Status = "Failed",
                Message = $"Sync failed: {ex.Message}",
                RootWorkItemIds = rootWorkItemIds
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Updates LastSyncedAt timestamp for products with the given root work item IDs.
    /// </summary>
    private async Task UpdateProductLastSyncedAtAsync(int[] rootWorkItemIds, CancellationToken cancellationToken)
    {
        if (rootWorkItemIds == null || rootWorkItemIds.Length == 0)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        try
        {
            var now = DateTimeOffset.UtcNow;
            var products = await dbContext.Products
                .Where(p => rootWorkItemIds.Contains(p.BacklogRootWorkItemId))
                .ToListAsync(cancellationToken);

            foreach (var product in products)
            {
                product.LastSyncedAt = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated LastSyncedAt for {Count} products", products.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update LastSyncedAt for products");
            // Don't throw - sync was successful, this is just metadata
        }
    }

    /// <summary>
    /// Sends a progress update to all connected clients via SignalR.
    /// </summary>
    private async Task SendProgressAsync(SyncProgressDto progress, CancellationToken cancellationToken)
    {
        try
        {
            // Send structured progress for two-level progress bars
            await _hubContext.Clients.All.SendAsync("SyncProgress", progress, cancellationToken);

            // Also send legacy SyncStatus for backward compatibility
            await _hubContext.Clients.All.SendAsync(
                "SyncStatus",
                new { Status = progress.Status, Message = progress.Message },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send sync progress update");
        }
    }
}
