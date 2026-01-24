using Microsoft.EntityFrameworkCore;
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
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        if (entity == null)
        {
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
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(productOwnerId, cancellationToken);

        entity.SyncStatus = CacheSyncStatus.Success;
        entity.LastSuccessfulSync = DateTimeOffset.UtcNow;
        entity.LastAttemptSync = DateTimeOffset.UtcNow;
        entity.WorkItemCount = workItemCount;
        entity.PullRequestCount = pullRequestCount;
        entity.PipelineCount = pipelineCount;
        entity.WorkItemWatermark = workItemWatermark;
        entity.PullRequestWatermark = pullRequestWatermark;
        entity.PipelineWatermark = pipelineWatermark;
        entity.LastErrorMessage = null;
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
        entity.LastErrorMessage = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
        entity.CurrentSyncStage = failedStage;
        // Note: Watermarks and LastSuccessfulSync remain unchanged on failure

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetCacheStateAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(productOwnerId, cancellationToken);

        entity.SyncStatus = CacheSyncStatus.Idle;
        entity.LastAttemptSync = null;
        entity.LastSuccessfulSync = null;
        entity.WorkItemCount = 0;
        entity.PullRequestCount = 0;
        entity.PipelineCount = 0;
        entity.WorkItemWatermark = null;
        entity.PullRequestWatermark = null;
        entity.PipelineWatermark = null;
        entity.LastErrorMessage = null;
        entity.CurrentSyncStage = null;
        entity.StageProgressPercent = 0;

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(DateTimeOffset? WorkItem, DateTimeOffset? PullRequest, DateTimeOffset? Pipeline)> GetWatermarksAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ProductOwnerCacheStates
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        if (entity == null)
        {
            return (null, null, null);
        }

        return (entity.WorkItemWatermark, entity.PullRequestWatermark, entity.PipelineWatermark);
    }

    private async Task<ProductOwnerCacheStateEntity> GetOrCreateEntityAsync(int productOwnerId, CancellationToken cancellationToken)
    {
        var entity = await _context.ProductOwnerCacheStates
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        if (entity == null)
        {
            entity = new ProductOwnerCacheStateEntity
            {
                ProductOwnerId = productOwnerId,
                SyncStatus = CacheSyncStatus.Idle
            };
            _context.ProductOwnerCacheStates.Add(entity);
        }

        return entity;
    }

    private static CacheStateDto MapToDto(ProductOwnerCacheStateEntity entity)
    {
        return new CacheStateDto
        {
            ProductOwnerId = entity.ProductOwnerId,
            SyncStatus = (CacheSyncStatusDto)entity.SyncStatus,
            LastAttemptSync = entity.LastAttemptSync,
            LastSuccessfulSync = entity.LastSuccessfulSync,
            WorkItemCount = entity.WorkItemCount,
            PullRequestCount = entity.PullRequestCount,
            PipelineCount = entity.PipelineCount,
            LastErrorMessage = entity.LastErrorMessage,
            CurrentSyncStage = entity.CurrentSyncStage,
            StageProgressPercent = entity.StageProgressPercent
        };
    }
}
