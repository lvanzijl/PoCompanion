using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Executes the sync pipeline for ProductOwners.
/// Enforces one-sync-per-ProductOwner rule using semaphores.
/// </summary>
public class SyncPipelineRunner : ISyncPipeline
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncPipelineRunner> _logger;

    // Concurrency control: one sync per ProductOwner
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _syncLocks = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeSyncs = new();

    // Total stages in the full pipeline as per TFS_CACHE_IMPLEMENTATION_PLAN.md
    // Phase 2 implements only Stage 1 (WorkItems). Stages 2-6 will be added in Phase 3.
    private const int TotalStages = 6;

    public SyncPipelineRunner(
        IServiceScopeFactory scopeFactory,
        ILogger<SyncPipelineRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async IAsyncEnumerable<SyncProgressUpdate> ExecuteAsync(
        int productOwnerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var semaphore = _syncLocks.GetOrAdd(productOwnerId, _ => new SemaphoreSlim(1, 1));

        // Try to acquire lock - if sync is already running, report and exit
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Sync already in progress for ProductOwner {ProductOwnerId}", productOwnerId);
            yield return new SyncProgressUpdate
            {
                CurrentStage = "Already syncing",
                StageProgressPercent = 0,
                IsComplete = false,
                HasFailed = false,
                StageNumber = 0,
                TotalStages = TotalStages
            };
            yield break;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeSyncs[productOwnerId] = cts;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            var cacheStateRepo = scope.ServiceProvider.GetRequiredService<ICacheStateRepository>();

            // Update status to InProgress
            await cacheStateRepo.UpdateSyncStatusAsync(
                productOwnerId,
                CacheSyncStatusDto.InProgress,
                "Initializing",
                0,
                cts.Token);

            yield return new SyncProgressUpdate
            {
                CurrentStage = "Initializing",
                StageProgressPercent = 0,
                IsComplete = false,
                HasFailed = false,
                StageNumber = 0,
                TotalStages = TotalStages
            };

            // Load context data
            var syncContext = await BuildSyncContextAsync(productOwnerId, context, cacheStateRepo, cts.Token);

            if (syncContext.RootWorkItemIds.Length == 0)
            {
                _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", productOwnerId);
                await cacheStateRepo.MarkSyncFailedAsync(
                    productOwnerId,
                    "No products configured for this Product Owner",
                    "Initialization",
                    cts.Token);

                yield return new SyncProgressUpdate
                {
                    CurrentStage = "Initialization",
                    StageProgressPercent = 0,
                    IsComplete = true,
                    HasFailed = true,
                    ErrorMessage = "No products configured for this Product Owner",
                    StageNumber = 0,
                    TotalStages = TotalStages
                };
                yield break;
            }

            // Execute Stage 1: Work Items
            var workItemStage = scope.ServiceProvider.GetRequiredService<WorkItemSyncStage>();
            
            await cacheStateRepo.UpdateSyncStatusAsync(
                productOwnerId,
                CacheSyncStatusDto.InProgress,
                workItemStage.StageName,
                0,
                cts.Token);

            yield return new SyncProgressUpdate
            {
                CurrentStage = workItemStage.StageName,
                StageProgressPercent = 0,
                IsComplete = false,
                HasFailed = false,
                StageNumber = workItemStage.StageNumber,
                TotalStages = TotalStages
            };

            var lastProgress = 0;
            var workItemResult = await workItemStage.ExecuteAsync(
                syncContext,
                progress =>
                {
                    lastProgress = progress;
                },
                cts.Token);

            if (!workItemResult.Success)
            {
                await cacheStateRepo.MarkSyncFailedAsync(
                    productOwnerId,
                    workItemResult.ErrorMessage ?? "Unknown error",
                    workItemStage.StageName,
                    cts.Token);

                yield return new SyncProgressUpdate
                {
                    CurrentStage = workItemStage.StageName,
                    StageProgressPercent = lastProgress,
                    IsComplete = true,
                    HasFailed = true,
                    ErrorMessage = workItemResult.ErrorMessage,
                    StageNumber = workItemStage.StageNumber,
                    TotalStages = TotalStages
                };
                yield break;
            }

            yield return new SyncProgressUpdate
            {
                CurrentStage = workItemStage.StageName,
                StageProgressPercent = 100,
                IsComplete = false,
                HasFailed = false,
                StageNumber = workItemStage.StageNumber,
                TotalStages = TotalStages
            };

            // Finalize (Stage 6) - Commit watermarks and update counts
            // Note: Stages 2-5 will be implemented in Phase 3
            var workItemCount = await context.WorkItems.CountAsync(cts.Token);
            var pullRequestCount = await context.PullRequests.CountAsync(cts.Token);
            var pipelineCount = await context.CachedPipelineRuns
                .Where(p => p.ProductOwnerId == productOwnerId)
                .CountAsync(cts.Token);

            await cacheStateRepo.MarkSyncSuccessAsync(
                productOwnerId,
                workItemCount,
                pullRequestCount,
                pipelineCount,
                workItemResult.NewWatermark,
                syncContext.PullRequestWatermark, // Unchanged until Stage 2 implemented
                syncContext.PipelineWatermark,    // Unchanged until Stage 3 implemented
                cts.Token);

            _logger.LogInformation(
                "Sync completed for ProductOwner {ProductOwnerId}: {WorkItems} work items",
                productOwnerId,
                workItemResult.ItemCount);

            yield return new SyncProgressUpdate
            {
                CurrentStage = "Complete",
                StageProgressPercent = 100,
                IsComplete = true,
                HasFailed = false,
                StageNumber = TotalStages,
                TotalStages = TotalStages
            };
        }
        finally
        {
            _activeSyncs.TryRemove(productOwnerId, out _);
            semaphore.Release();
        }
    }

    public void CancelSync(int productOwnerId)
    {
        if (_activeSyncs.TryGetValue(productOwnerId, out var cts))
        {
            _logger.LogInformation("Cancelling sync for ProductOwner {ProductOwnerId}", productOwnerId);
            cts.Cancel();
        }
    }

    public bool IsSyncRunning(int productOwnerId)
    {
        return _activeSyncs.ContainsKey(productOwnerId);
    }

    private async Task<SyncContext> BuildSyncContextAsync(
        int productOwnerId,
        PoToolDbContext context,
        ICacheStateRepository cacheStateRepo,
        CancellationToken cancellationToken)
    {
        // Get products for this ProductOwner
        var products = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .ToListAsync(cancellationToken);

        var rootWorkItemIds = products
            .Select(p => p.BacklogRootWorkItemId)
            .ToArray();

        // Get repositories for these products
        var productIds = products.Select(p => p.Id).ToList();
        var repositories = await context.Repositories
            .Where(r => productIds.Contains(r.ProductId))
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        // Get pipeline definitions for these products
        var pipelineDefinitionIds = await context.PipelineDefinitions
            .Where(pd => productIds.Contains(pd.ProductId))
            .Select(pd => pd.PipelineDefinitionId)
            .ToListAsync(cancellationToken);

        // Get current watermarks
        var watermarks = await cacheStateRepo.GetWatermarksAsync(productOwnerId, cancellationToken);

        return new SyncContext
        {
            ProductOwnerId = productOwnerId,
            RootWorkItemIds = rootWorkItemIds,
            WorkItemWatermark = watermarks.WorkItem,
            PullRequestWatermark = watermarks.PullRequest,
            PipelineWatermark = watermarks.Pipeline,
            RepositoryNames = repositories.ToArray(),
            PipelineDefinitionIds = pipelineDefinitionIds.ToArray()
        };
    }
}
