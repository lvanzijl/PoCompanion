using Microsoft.EntityFrameworkCore;
using PoTool.Api.Exceptions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for ProductOwner cache state persistence.
/// </summary>
public class CacheStateRepository : ICacheStateRepository
{
    private readonly PoToolDbContext _context;

    public CacheStateRepository(PoToolDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<CacheStateDto> GetOrCreateCacheStateAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ProductOwnerCacheStates
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        if (entity == null)
        {
            // Validate that the ProductOwner exists before creating cache state
            await ValidateProductOwnerExistsAsync(productOwnerId, cancellationToken);

            entity = new ProductOwnerCacheStateEntity
            {
                ProductOwnerId = productOwnerId,
                SyncStatus = CacheSyncStatus.Idle
            };
            _context.ProductOwnerCacheStates.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<CacheStateDto?> GetCacheStateAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ProductOwnerCacheStates
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task UpdateSyncStatusAsync(
        int productOwnerId,
        CacheSyncStatusDto syncStatus,
        string? currentStage = null,
        int stageProgressPercent = 0,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(productOwnerId, cancellationToken);

        entity.SyncStatus = (CacheSyncStatus)syncStatus;
        entity.CurrentSyncStage = currentStage;
        entity.StageProgressPercent = stageProgressPercent;

        if (syncStatus == CacheSyncStatusDto.InProgress)
        {
            entity.LastAttemptSync = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkSyncSuccessAsync(
        int productOwnerId,
        int workItemCount,
        int pullRequestCount,
        int pipelineCount,
        DateTimeOffset? workItemWatermark,
        DateTimeOffset? pullRequestWatermark,
        DateTimeOffset? pipelineWatermark,
        DateTimeOffset? pipelineFinishWatermark,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(productOwnerId, cancellationToken);

        entity.SyncStatus = CacheSyncStatus.Success;
        entity.PreviousSuccessfulSync = entity.LastSuccessfulSync;
        entity.LastSuccessfulSync = DateTimeOffset.UtcNow;
        entity.LastAttemptSync = DateTimeOffset.UtcNow;
        entity.WorkItemCount = workItemCount;
        entity.PullRequestCount = pullRequestCount;
        entity.PipelineCount = pipelineCount;
        entity.WorkItemWatermark = workItemWatermark;
        entity.PullRequestWatermark = pullRequestWatermark;
        entity.PipelineWatermark = pipelineWatermark;
        entity.PipelineFinishWatermark = pipelineFinishWatermark;
        entity.LastErrorMessage = null;
        entity.CurrentSyncStage = null;
        entity.StageProgressPercent = 0;

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkSyncSuccessWithWarningsAsync(
        int productOwnerId,
        int workItemCount,
        int pullRequestCount,
        int pipelineCount,
        DateTimeOffset? workItemWatermark,
        DateTimeOffset? pullRequestWatermark,
        DateTimeOffset? pipelineWatermark,
        DateTimeOffset? pipelineFinishWatermark,
        string? warningMessage,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(productOwnerId, cancellationToken);

        entity.SyncStatus = CacheSyncStatus.SuccessWithWarnings;
        entity.PreviousSuccessfulSync = entity.LastSuccessfulSync;
        entity.LastSuccessfulSync = DateTimeOffset.UtcNow;
        entity.LastAttemptSync = DateTimeOffset.UtcNow;
        entity.WorkItemCount = workItemCount;
        entity.PullRequestCount = pullRequestCount;
        entity.PipelineCount = pipelineCount;
        entity.WorkItemWatermark = workItemWatermark;
        entity.PullRequestWatermark = pullRequestWatermark;
        entity.PipelineWatermark = pipelineWatermark;
        entity.PipelineFinishWatermark = pipelineFinishWatermark;
        entity.LastErrorMessage = warningMessage?.Length > 2000 ? warningMessage[..1997] + "..." : warningMessage;
        entity.CurrentSyncStage = null;
        entity.StageProgressPercent = 0;

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkSyncFailedAsync(
        int productOwnerId,
        string errorMessage,
        string failedStage,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(productOwnerId, cancellationToken);

        entity.SyncStatus = CacheSyncStatus.Failed;
        entity.LastAttemptSync = DateTimeOffset.UtcNow;
        entity.LastErrorMessage = errorMessage.Length > 2000 ? errorMessage[..1997] + "..." : errorMessage;
        entity.CurrentSyncStage = failedStage;
        // Note: Watermarks and LastSuccessfulSync remain unchanged on failure

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetCacheStateAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(productOwnerId, cancellationToken);

        await ClearCachedDataAsync(productOwnerId, cancellationToken);

        entity.SyncStatus = CacheSyncStatus.Idle;
        entity.LastAttemptSync = null;
        entity.LastSuccessfulSync = null;
        entity.PreviousSuccessfulSync = null;
        entity.WorkItemCount = 0;
        entity.PullRequestCount = 0;
        entity.PipelineCount = 0;
        entity.WorkItemWatermark = null;
        entity.PullRequestWatermark = null;
        entity.PipelineWatermark = null;
        entity.PipelineFinishWatermark = null;
        entity.RelationshipsSnapshotAsOfUtc = null;
        entity.RelationshipsSnapshotWorkItemWatermark = null;
        entity.ResolutionAsOfUtc = null;
        entity.SprintTrendProjectionAsOfUtc = null;
        entity.ActivityEventWatermark = null;
        entity.LastErrorMessage = null;
        entity.CurrentSyncStage = null;
        entity.StageProgressPercent = 0;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearCachedDataAsync(int productOwnerId, CancellationToken cancellationToken)
    {
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        var productIds = await _context.Products
            .Where(product => product.ProductOwnerId == productOwnerId)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
        var pullRequestIds = await _context.PullRequests
            .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
            .Select(pr => pr.Id)
            .ToListAsync(cancellationToken);

        if (isInMemory)
        {
            var resolvedWorkItems = await _context.ResolvedWorkItems
                .Where(item => item.ResolvedProductId.HasValue && productIds.Contains(item.ResolvedProductId.Value))
                .ToListAsync(cancellationToken);
            var sprintMetrics = await _context.SprintMetricsProjections
                .Where(metric => productIds.Contains(metric.ProductId))
                .ToListAsync(cancellationToken);
            // Cached validation results are global and not product-owner scoped.
            var cachedValidationResults = await _context.CachedValidationResults.ToListAsync(cancellationToken);
            var cachedMetrics = await _context.CachedMetrics
                .Where(m => m.ProductOwnerId == productOwnerId)
                .ToListAsync(cancellationToken);
            var cachedPipelineRuns = await _context.CachedPipelineRuns
                .Where(r => r.ProductOwnerId == productOwnerId)
                .ToListAsync(cancellationToken);
            var relationshipEdges = await _context.WorkItemRelationshipEdges
                .Where(e => e.ProductOwnerId == productOwnerId)
                .ToListAsync(cancellationToken);
            var activityEvents = await _context.ActivityEventLedgerEntries
                .Where(e => e.ProductOwnerId == productOwnerId)
                .ToListAsync(cancellationToken);
            var pullRequestFileChanges = await _context.PullRequestFileChanges
                .Where(change => pullRequestIds.Contains(change.PullRequestId))
                .ToListAsync(cancellationToken);
            var pullRequestComments = await _context.PullRequestComments
                .Where(comment => pullRequestIds.Contains(comment.PullRequestId))
                .ToListAsync(cancellationToken);
            var pullRequestIterations = await _context.PullRequestIterations
                .Where(iteration => pullRequestIds.Contains(iteration.PullRequestId))
                .ToListAsync(cancellationToken);
            var pullRequests = await _context.PullRequests
                .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
                .ToListAsync(cancellationToken);
            // Work items and sprints are global caches and not product-owner scoped.
            var workItems = await _context.WorkItems.ToListAsync(cancellationToken);
            var sprints = await _context.Sprints.ToListAsync(cancellationToken);

            _context.ResolvedWorkItems.RemoveRange(resolvedWorkItems);
            _context.SprintMetricsProjections.RemoveRange(sprintMetrics);
            _context.CachedValidationResults.RemoveRange(cachedValidationResults);
            _context.CachedMetrics.RemoveRange(cachedMetrics);
            _context.CachedPipelineRuns.RemoveRange(cachedPipelineRuns);
            _context.WorkItemRelationshipEdges.RemoveRange(relationshipEdges);
            _context.ActivityEventLedgerEntries.RemoveRange(activityEvents);
            _context.PullRequestFileChanges.RemoveRange(pullRequestFileChanges);
            _context.PullRequestComments.RemoveRange(pullRequestComments);
            _context.PullRequestIterations.RemoveRange(pullRequestIterations);
            _context.PullRequests.RemoveRange(pullRequests);
            _context.WorkItems.RemoveRange(workItems);
            _context.Sprints.RemoveRange(sprints);

            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        await _context.ResolvedWorkItems
            .Where(item => item.ResolvedProductId.HasValue && productIds.Contains(item.ResolvedProductId.Value))
            .ExecuteDeleteAsync(cancellationToken);
        await _context.SprintMetricsProjections
            .Where(metric => productIds.Contains(metric.ProductId))
            .ExecuteDeleteAsync(cancellationToken);
        // Cached validation results are global and not product-owner scoped.
            await _context.CachedValidationResults.ExecuteDeleteAsync(cancellationToken);
            await _context.CachedMetrics
                .Where(m => m.ProductOwnerId == productOwnerId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.CachedPipelineRuns
                .Where(r => r.ProductOwnerId == productOwnerId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.WorkItemRelationshipEdges
                .Where(e => e.ProductOwnerId == productOwnerId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.ActivityEventLedgerEntries
                .Where(e => e.ProductOwnerId == productOwnerId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.PullRequestFileChanges
                .Where(change => pullRequestIds.Contains(change.PullRequestId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.PullRequestComments
            .Where(comment => pullRequestIds.Contains(comment.PullRequestId))
            .ExecuteDeleteAsync(cancellationToken);
        await _context.PullRequestIterations
            .Where(iteration => pullRequestIds.Contains(iteration.PullRequestId))
            .ExecuteDeleteAsync(cancellationToken);
        await _context.PullRequests
            .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
            .ExecuteDeleteAsync(cancellationToken);
        // Work items and sprints are global caches and not product-owner scoped.
        await _context.WorkItems.ExecuteDeleteAsync(cancellationToken);
        await _context.Sprints.ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(DateTimeOffset? WorkItem, DateTimeOffset? PullRequest, DateTimeOffset? Pipeline, DateTimeOffset? PipelineFinish)> GetWatermarksAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ProductOwnerCacheStates
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        if (entity == null)
        {
            return (null, null, null, null);
        }

        return (
            entity.WorkItemWatermark,
            entity.PullRequestWatermark,
            entity.PipelineWatermark,
            entity.PipelineFinishWatermark ?? entity.PipelineWatermark);
    }

    private async Task<ProductOwnerCacheStateEntity> GetOrCreateEntityAsync(int productOwnerId, CancellationToken cancellationToken)
    {
        var entity = await _context.ProductOwnerCacheStates
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        if (entity == null)
        {
            // Validate that the ProductOwner exists before creating cache state
            await ValidateProductOwnerExistsAsync(productOwnerId, cancellationToken);

            entity = new ProductOwnerCacheStateEntity
            {
                ProductOwnerId = productOwnerId,
                SyncStatus = CacheSyncStatus.Idle
            };
            _context.ProductOwnerCacheStates.Add(entity);
        }

        return entity;
    }

    /// <summary>
    /// Validates that a ProductOwner exists in the database.
    /// </summary>
    /// <exception cref="ProductOwnerNotFoundException">Thrown when the ProductOwner does not exist.</exception>
    private async Task ValidateProductOwnerExistsAsync(int productOwnerId, CancellationToken cancellationToken)
    {
        var exists = await _context.Profiles
            .AnyAsync(p => p.Id == productOwnerId, cancellationToken);

        if (!exists)
        {
            throw new ProductOwnerNotFoundException(productOwnerId);
        }
    }

    private static CacheStateDto MapToDto(ProductOwnerCacheStateEntity entity)
    {
        return new CacheStateDto
        {
            ProductOwnerId = entity.ProductOwnerId,
            SyncStatus = (CacheSyncStatusDto)entity.SyncStatus,
            LastAttemptSync = entity.LastAttemptSync,
            LastSuccessfulSync = entity.LastSuccessfulSync,
            PreviousSuccessfulSync = entity.PreviousSuccessfulSync,
            WorkItemCount = entity.WorkItemCount,
            PullRequestCount = entity.PullRequestCount,
            PipelineCount = entity.PipelineCount,
            LastErrorMessage = entity.LastErrorMessage,
            WorkItemWatermark = entity.WorkItemWatermark,
            PullRequestWatermark = entity.PullRequestWatermark,
            PipelineFinishWatermark = entity.PipelineFinishWatermark ?? entity.PipelineWatermark,
            CurrentSyncStage = entity.CurrentSyncStage,
            StageProgressPercent = entity.StageProgressPercent
        };
    }
}
