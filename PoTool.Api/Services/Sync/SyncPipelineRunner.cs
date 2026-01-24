using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
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

            // Track results from each stage for finalization
            SyncStageResult? workItemResult = null;
            SyncStageResult? pullRequestResult = null;
            SyncStageResult? pipelineResult = null;

            // ============================================
            // Stage 1: Sync Work Items
            // ============================================
            var workItemStage = scope.ServiceProvider.GetRequiredService<WorkItemSyncStage>();
            var stage1Update = await ExecuteStageAsync(workItemStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage1Update;

            if (stage1Update.HasFailed)
            {
                yield break;
            }
            workItemResult = await workItemStage.ExecuteAsync(syncContext, _ => { }, cts.Token);

            // ============================================
            // Stage 2: Sync Pull Requests
            // ============================================
            var pullRequestStage = scope.ServiceProvider.GetRequiredService<PullRequestSyncStage>();
            var stage2Update = await ExecuteStageAsync(pullRequestStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage2Update;

            if (stage2Update.HasFailed)
            {
                // Stage 1 watermark should still be committed
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, null, null, cts.Token);
                yield break;
            }
            pullRequestResult = await pullRequestStage.ExecuteAsync(syncContext, _ => { }, cts.Token);

            // ============================================
            // Stage 3: Sync Pipelines
            // ============================================
            var pipelineStage = scope.ServiceProvider.GetRequiredService<PipelineSyncStage>();
            var stage3Update = await ExecuteStageAsync(pipelineStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage3Update;

            if (stage3Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, pullRequestResult?.NewWatermark, null, cts.Token);
                yield break;
            }
            pipelineResult = await pipelineStage.ExecuteAsync(syncContext, _ => { }, cts.Token);

            // ============================================
            // Stage 4: Compute Validations
            // ============================================
            var validationStage = scope.ServiceProvider.GetRequiredService<ValidationComputeStage>();
            var stage4Update = await ExecuteStageAsync(validationStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage4Update;

            if (stage4Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, pullRequestResult?.NewWatermark, pipelineResult?.NewWatermark, cts.Token);
                yield break;
            }

            // ============================================
            // Stage 5: Compute Metrics
            // ============================================
            var metricsStage = scope.ServiceProvider.GetRequiredService<MetricsComputeStage>();
            var stage5Update = await ExecuteStageAsync(metricsStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage5Update;

            if (stage5Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, pullRequestResult?.NewWatermark, pipelineResult?.NewWatermark, cts.Token);
                yield break;
            }

            // ============================================
            // Stage 6: Finalize Cache
            // ============================================
            var finalizeStage = scope.ServiceProvider.GetRequiredService<FinalizeCacheStage>();

            // Set finalization data
            var workItemCount = await context.WorkItems.CountAsync(cts.Token);
            var pullRequestCount = await context.PullRequests.CountAsync(cts.Token);
            var pipelineCount = await context.CachedPipelineRuns
                .Where(p => p.ProductOwnerId == productOwnerId)
                .CountAsync(cts.Token);

            finalizeStage.WorkItemCount = workItemCount;
            finalizeStage.PullRequestCount = pullRequestCount;
            finalizeStage.PipelineCount = pipelineCount;
            finalizeStage.WorkItemWatermark = workItemResult?.NewWatermark;
            finalizeStage.PullRequestWatermark = pullRequestResult?.NewWatermark;
            finalizeStage.PipelineWatermark = pipelineResult?.NewWatermark;

            var stage6Update = await ExecuteStageAsync(finalizeStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage6Update;

            if (stage6Update.HasFailed)
            {
                yield break;
            }

            _logger.LogInformation(
                "Sync completed for ProductOwner {ProductOwnerId}: {WorkItems} work items, {PRs} PRs, {Pipelines} pipelines",
                productOwnerId,
                workItemCount,
                pullRequestCount,
                pipelineCount);

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

    private async Task<SyncProgressUpdate> ExecuteStageAsync(
        ISyncStage stage,
        SyncContext syncContext,
        ICacheStateRepository cacheStateRepo,
        CancellationToken cancellationToken)
    {
        await cacheStateRepo.UpdateSyncStatusAsync(
            syncContext.ProductOwnerId,
            CacheSyncStatusDto.InProgress,
            stage.StageName,
            0,
            cancellationToken);

        var lastProgress = 0;
        var result = await stage.ExecuteAsync(
            syncContext,
            progress => lastProgress = progress,
            cancellationToken);

        if (!result.Success)
        {
            await cacheStateRepo.MarkSyncFailedAsync(
                syncContext.ProductOwnerId,
                result.ErrorMessage ?? "Unknown error",
                stage.StageName,
                cancellationToken);

            return new SyncProgressUpdate
            {
                CurrentStage = stage.StageName,
                StageProgressPercent = lastProgress,
                IsComplete = true,
                HasFailed = true,
                ErrorMessage = result.ErrorMessage,
                StageNumber = stage.StageNumber,
                TotalStages = TotalStages
            };
        }

        return new SyncProgressUpdate
        {
            CurrentStage = stage.StageName,
            StageProgressPercent = 100,
            IsComplete = false,
            HasFailed = false,
            StageNumber = stage.StageNumber,
            TotalStages = TotalStages
        };
    }

    private async Task CommitPartialSuccessAsync(
        ICacheStateRepository cacheStateRepo,
        int productOwnerId,
        PoToolDbContext context,
        DateTimeOffset? workItemWatermark,
        DateTimeOffset? pullRequestWatermark,
        DateTimeOffset? pipelineWatermark,
        CancellationToken cancellationToken)
    {
        // Commit successful watermarks even if later stages failed
        var workItemCount = await context.WorkItems.CountAsync(cancellationToken);
        var pullRequestCount = await context.PullRequests.CountAsync(cancellationToken);
        var pipelineCount = await context.CachedPipelineRuns
            .Where(p => p.ProductOwnerId == productOwnerId)
            .CountAsync(cancellationToken);

        await cacheStateRepo.MarkSyncSuccessAsync(
            productOwnerId,
            workItemCount,
            pullRequestCount,
            pipelineCount,
            workItemWatermark,
            pullRequestWatermark,
            pipelineWatermark,
            cancellationToken);
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
