using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

/// <summary>
/// Service for cache insights and granular reset operations.
/// </summary>
public class CacheManagementService
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<CacheManagementService> _logger;

    public CacheManagementService(PoToolDbContext context, ILogger<CacheManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CacheInsightsDto> GetInsightsAsync(int productOwnerId, CancellationToken cancellationToken)
    {
        var cacheState = await _context.ProductOwnerCacheStates
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        var productIds = await _context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var counts = new List<CacheEntityCountDto>
        {
            new() { EntityType = CacheEntityTypes.WorkItems, TotalCount = await _context.WorkItems.CountAsync(cancellationToken) },
            new()
            {
                EntityType = CacheEntityTypes.PullRequests,
                TotalCount = await _context.PullRequests
                    .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
                    .CountAsync(cancellationToken)
            },
            new()
            {
                EntityType = CacheEntityTypes.Pipelines,
                TotalCount = await _context.CachedPipelineRuns
                    .Where(r => r.ProductOwnerId == productOwnerId)
                    .CountAsync(cancellationToken)
            },
            new()
            {
                EntityType = CacheEntityTypes.Metrics,
                TotalCount = await _context.CachedMetrics
                    .Where(m => m.ProductOwnerId == productOwnerId)
                    .CountAsync(cancellationToken)
            },
            new() { EntityType = CacheEntityTypes.Validations, TotalCount = await _context.CachedValidationResults.CountAsync(cancellationToken) },
            new()
            {
                EntityType = CacheEntityTypes.SprintProjections,
                TotalCount = await _context.SprintMetricsProjections
                    .Where(s => productIds.Contains(s.ProductId))
                    .CountAsync(cancellationToken)
            },
            new()
            {
                EntityType = CacheEntityTypes.Relationships,
                TotalCount = await _context.WorkItemRelationshipEdges
                    .Where(e => e.ProductOwnerId == productOwnerId)
                    .CountAsync(cancellationToken)
            }
        };

        return new CacheInsightsDto
        {
            ProductOwnerId = productOwnerId,
            EntityCounts = counts,
            SyncStatus = cacheState != null ? (CacheSyncStatusDto)cacheState.SyncStatus : CacheSyncStatusDto.Idle,
            LastSuccessfulSync = cacheState?.LastSuccessfulSync,
            LastAttemptSync = cacheState?.LastAttemptSync,
            LastErrorMessage = cacheState?.LastErrorMessage,
            CurrentSyncStage = cacheState?.CurrentSyncStage
        };
    }

    public async Task<CacheResetResponse> ResetSelectiveAsync(
        int productOwnerId,
        CacheResetRequest request,
        CancellationToken cancellationToken)
    {
        var typesToReset = request.EntityTypes;
        if (typesToReset == null || typesToReset.Count == 0)
        {
            typesToReset = CacheEntityTypes.All.ToList();
        }

        var productIds = await _context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var deletedCounts = new List<CacheEntityCountDto>();

        foreach (var entityType in typesToReset)
        {
            var count = await ResetEntityTypeAsync(productOwnerId, entityType, productIds, cancellationToken);
            deletedCounts.Add(new CacheEntityCountDto { EntityType = entityType, TotalCount = count });
            _logger.LogInformation("Reset {EntityType}: deleted {Count} records for ProductOwner {ProductOwnerId}",
                entityType, count, productOwnerId);
        }

        if (typesToReset.Count == CacheEntityTypes.All.Count)
        {
            var cacheState = await _context.ProductOwnerCacheStates
                .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);
            if (cacheState != null)
            {
                cacheState.SyncStatus = CacheSyncStatus.Idle;
                cacheState.LastAttemptSync = null;
                cacheState.LastSuccessfulSync = null;
                cacheState.WorkItemCount = 0;
                cacheState.PullRequestCount = 0;
                cacheState.PipelineCount = 0;
                cacheState.WorkItemWatermark = null;
                cacheState.PullRequestWatermark = null;
                cacheState.PipelineWatermark = null;
                cacheState.LastErrorMessage = null;
                cacheState.CurrentSyncStage = null;
                cacheState.StageProgressPercent = 0;
                cacheState.RelationshipsSnapshotAsOfUtc = null;
                cacheState.RelationshipsSnapshotWorkItemWatermark = null;
                cacheState.ResolutionAsOfUtc = null;
                cacheState.SprintTrendProjectionAsOfUtc = null;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return new CacheResetResponse
        {
            Success = true,
            Message = $"Reset {typesToReset.Count} entity type(s) for ProductOwner {productOwnerId}",
            DeletedCounts = deletedCounts
        };
    }

    private async Task<int> ResetEntityTypeAsync(
        int productOwnerId,
        string entityType,
        List<int> productIds,
        CancellationToken cancellationToken)
    {
        return entityType switch
        {
            CacheEntityTypes.WorkItems => await DeleteWorkItemsAsync(cancellationToken),
            CacheEntityTypes.PullRequests => await DeletePullRequestsAsync(productIds, cancellationToken),
            CacheEntityTypes.Pipelines => await DeletePipelinesAsync(productOwnerId, cancellationToken),
            CacheEntityTypes.Metrics => await DeleteMetricsAsync(productOwnerId, cancellationToken),
            CacheEntityTypes.Validations => await DeleteValidationsAsync(cancellationToken),
            CacheEntityTypes.SprintProjections => await DeleteSprintProjectionsAsync(productIds, cancellationToken),
            CacheEntityTypes.Relationships => await DeleteRelationshipsAsync(productOwnerId, cancellationToken),
            _ => 0
        };
    }

    private async Task<int> DeleteWorkItemsAsync(CancellationToken ct)
    {
        var count = await _context.WorkItems.CountAsync(ct);
        await _context.WorkItems.ExecuteDeleteAsync(ct);
        await _context.Sprints.ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeletePullRequestsAsync(List<int> productIds, CancellationToken ct)
    {
        var pullRequestIds = await _context.PullRequests
            .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
            .Select(pr => pr.Id)
            .ToListAsync(ct);

        var count = pullRequestIds.Count;
        if (count > 0)
        {
            await _context.PullRequestFileChanges
                .Where(c => pullRequestIds.Contains(c.PullRequestId))
                .ExecuteDeleteAsync(ct);
            await _context.PullRequestComments
                .Where(c => pullRequestIds.Contains(c.PullRequestId))
                .ExecuteDeleteAsync(ct);
            await _context.PullRequestIterations
                .Where(i => pullRequestIds.Contains(i.PullRequestId))
                .ExecuteDeleteAsync(ct);
            await _context.PullRequests
                .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
                .ExecuteDeleteAsync(ct);
        }
        return count;
    }

    private async Task<int> DeletePipelinesAsync(int productOwnerId, CancellationToken ct)
    {
        var count = await _context.CachedPipelineRuns
            .Where(r => r.ProductOwnerId == productOwnerId)
            .CountAsync(ct);
        await _context.CachedPipelineRuns
            .Where(r => r.ProductOwnerId == productOwnerId)
            .ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteMetricsAsync(int productOwnerId, CancellationToken ct)
    {
        var count = await _context.CachedMetrics
            .Where(m => m.ProductOwnerId == productOwnerId)
            .CountAsync(ct);
        await _context.CachedMetrics
            .Where(m => m.ProductOwnerId == productOwnerId)
            .ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteValidationsAsync(CancellationToken ct)
    {
        var count = await _context.CachedValidationResults.CountAsync(ct);
        await _context.CachedValidationResults.ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteSprintProjectionsAsync(List<int> productIds, CancellationToken ct)
    {
        var count = await _context.SprintMetricsProjections
            .Where(s => productIds.Contains(s.ProductId))
            .CountAsync(ct);
        await _context.SprintMetricsProjections
            .Where(s => productIds.Contains(s.ProductId))
            .ExecuteDeleteAsync(ct);
        await _context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId.HasValue && productIds.Contains(r.ResolvedProductId.Value))
            .ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteRelationshipsAsync(int productOwnerId, CancellationToken ct)
    {
        var count = await _context.WorkItemRelationshipEdges
            .Where(e => e.ProductOwnerId == productOwnerId)
            .CountAsync(ct);
        await _context.WorkItemRelationshipEdges
            .Where(e => e.ProductOwnerId == productOwnerId)
            .ExecuteDeleteAsync(ct);
        return count;
    }
}
