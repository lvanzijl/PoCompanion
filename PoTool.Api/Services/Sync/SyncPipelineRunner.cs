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
    private const int TotalStages = 11;

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
            var hasWarnings = false;
            string? warningMessage = null;

            // ============================================
            // Stage 1: Sync Work Items
            // ============================================
            var workItemStage = scope.ServiceProvider.GetRequiredService<WorkItemSyncStage>();
            var (stage1Update, stage1Result) = await ExecuteStageAsync(workItemStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage1Update;

            if (stage1Update.HasFailed)
            {
                yield break;
            }
            workItemResult = stage1Result;
            if (workItemResult.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= workItemResult.WarningMessage ?? workItemResult.ErrorMessage;
            }

            // ============================================
            // Stage 2: Sync Team Sprints
            // ============================================
            var teamSprintStage = scope.ServiceProvider.GetRequiredService<TeamSprintSyncStage>();
            var (stage2Update, stage2Result) = await ExecuteStageAsync(teamSprintStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage2Update;

            if (stage2Update.HasFailed)
            {
                // Sprint sync failed, but preserve work item watermark to avoid re-syncing work items.
                // Sprint sync failures are non-critical - sprints can be re-synced in next run.
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, null, null, cts.Token);
                yield break;
            }
            if (stage2Result.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= stage2Result.WarningMessage ?? stage2Result.ErrorMessage;
            }

            // ============================================
            // Stage 3: Sync Revisions
            // ============================================
            var revisionStage = scope.ServiceProvider.GetRequiredService<RevisionSyncStage>();
            var (stage3Update, stage3Result) = await ExecuteStageAsync(revisionStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage3Update;

            if (stage3Update.HasFailed)
            {
                // Revision sync failed, but preserve work item watermark to avoid re-syncing work items.
                // Revision sync failures are non-critical - revisions can be re-synced in next run.
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, null, null, cts.Token);
                yield break;
            }
            if (stage3Result.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= stage3Result.WarningMessage ?? stage3Result.ErrorMessage;
            }

            // ============================================
            // Stage 4: Snapshot Work Item Relationships
            // ============================================
            var relationshipStage = scope.ServiceProvider.GetRequiredService<WorkItemRelationshipSnapshotStage>();
            var (stage4Update, stage4Result) = await ExecuteStageAsync(relationshipStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage4Update;

            if (stage4Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, null, null, cts.Token);
                yield break;
            }
            if (stage4Result.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= stage4Result.WarningMessage ?? stage4Result.ErrorMessage;
            }

            // ============================================
            // Stage 5: Resolve Work Items
            // ============================================
            var resolutionStage = scope.ServiceProvider.GetRequiredService<WorkItemResolutionSyncStage>();
            var (stage5Update, stage5Result) = await ExecuteStageAsync(resolutionStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage5Update;

            if (stage5Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, null, null, cts.Token);
                yield break;
            }
            if (stage5Result.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= stage5Result.WarningMessage ?? stage5Result.ErrorMessage;
            }

            // ============================================
            // Stage 6: Compute Sprint Trend Projections
            // ============================================
            var sprintTrendStage = scope.ServiceProvider.GetRequiredService<SprintTrendProjectionSyncStage>();
            var (stage6Update, stage6Result) = await ExecuteStageAsync(sprintTrendStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage6Update;

            if (stage6Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, null, null, cts.Token);
                yield break;
            }
            if (stage6Result.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= stage6Result.WarningMessage ?? stage6Result.ErrorMessage;
            }

            // ============================================
            // Stage 7: Sync Pull Requests
            // ============================================
            var pullRequestStage = scope.ServiceProvider.GetRequiredService<PullRequestSyncStage>();
            var (stage7Update, stage7Result) = await ExecuteStageAsync(pullRequestStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage7Update;

            if (stage7Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, null, null, cts.Token);
                yield break;
            }
            pullRequestResult = stage7Result;
            if (pullRequestResult.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= pullRequestResult.WarningMessage ?? pullRequestResult.ErrorMessage;
            }

            // ============================================
            // Stage 8: Sync Pipelines
            // ============================================
            var pipelineStage = scope.ServiceProvider.GetRequiredService<PipelineSyncStage>();
            var (stage8Update, stage8Result) = await ExecuteStageAsync(pipelineStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage8Update;

            if (stage8Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, pullRequestResult?.NewWatermark, null, cts.Token);
                yield break;
            }
            pipelineResult = stage8Result;
            if (pipelineResult.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= pipelineResult.WarningMessage ?? pipelineResult.ErrorMessage;
            }

            // ============================================
            // Stage 9: Compute Validations
            // ============================================
            var validationStage = scope.ServiceProvider.GetRequiredService<ValidationComputeStage>();
            var (stage9Update, stage9Result) = await ExecuteStageAsync(validationStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage9Update;

            if (stage9Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, pullRequestResult?.NewWatermark, pipelineResult?.NewWatermark, cts.Token);
                yield break;
            }
            if (stage9Result.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= stage9Result.WarningMessage ?? stage9Result.ErrorMessage;
            }

            // ============================================
            // Stage 10: Compute Metrics
            // ============================================
            var metricsStage = scope.ServiceProvider.GetRequiredService<MetricsComputeStage>();
            var (stage10Update, stage10Result) = await ExecuteStageAsync(metricsStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage10Update;

            if (stage10Update.HasFailed)
            {
                await CommitPartialSuccessAsync(cacheStateRepo, productOwnerId, context,
                    workItemResult?.NewWatermark, pullRequestResult?.NewWatermark, pipelineResult?.NewWatermark, cts.Token);
                yield break;
            }
            if (stage10Result.HasWarnings)
            {
                hasWarnings = true;
                warningMessage ??= stage10Result.WarningMessage ?? stage10Result.ErrorMessage;
            }

            if (hasWarnings && string.IsNullOrWhiteSpace(warningMessage))
            {
                warningMessage = "Sync completed with warnings.";
            }

            // ============================================
            // Stage 11: Finalize Cache
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
            finalizeStage.HasWarnings = hasWarnings;
            finalizeStage.WarningMessage = warningMessage;

            var (stage11Update, _) = await ExecuteStageAsync(finalizeStage, syncContext, cacheStateRepo, cts.Token);
            yield return stage11Update;

            if (stage11Update.HasFailed)
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
                HasWarnings = hasWarnings,
                WarningMessage = warningMessage,
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

    private async Task<(SyncProgressUpdate Update, SyncStageResult Result)> ExecuteStageAsync(
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

            return (new SyncProgressUpdate
            {
                CurrentStage = stage.StageName,
                StageProgressPercent = lastProgress,
                IsComplete = true,
                HasFailed = true,
                ErrorMessage = result.ErrorMessage,
                HasWarnings = result.HasWarnings,
                WarningMessage = result.WarningMessage,
                StageNumber = stage.StageNumber,
                TotalStages = TotalStages
            }, result);
        }

        return (new SyncProgressUpdate
        {
            CurrentStage = stage.StageName,
            StageProgressPercent = 100,
            IsComplete = false,
            HasFailed = false,
            HasWarnings = result.HasWarnings,
            WarningMessage = result.WarningMessage,
            StageNumber = stage.StageNumber,
            TotalStages = TotalStages
        }, result);
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
