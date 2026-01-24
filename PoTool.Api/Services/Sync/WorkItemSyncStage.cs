using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that fetches and upserts work items from TFS.
/// </summary>
public class WorkItemSyncStage : ISyncStage
{
    private readonly ITfsClient _tfsClient;
    private readonly PoToolDbContext _context;
    private readonly ILogger<WorkItemSyncStage> _logger;

    public string StageName => "SyncWorkItems";
    public int StageNumber => 1;

    public WorkItemSyncStage(
        ITfsClient tfsClient,
        PoToolDbContext context,
        ILogger<WorkItemSyncStage> logger)
    {
        _tfsClient = tfsClient;
        _context = context;
        _logger = logger;
    }

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (context.RootWorkItemIds.Length == 0)
            {
                _logger.LogWarning("No root work item IDs provided for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
                return SyncStageResult.CreateSuccess(0);
            }

            progressCallback(0);

            _logger.LogInformation(
                "Starting work item sync for ProductOwner {ProductOwnerId} with {RootCount} roots, watermark: {Watermark}",
                context.ProductOwnerId,
                context.RootWorkItemIds.Length,
                context.WorkItemWatermark?.ToString("O") ?? "null (full sync)");

            // Fetch work items from TFS
            var workItems = await _tfsClient.GetWorkItemsByRootIdsAsync(
                context.RootWorkItemIds,
                context.WorkItemWatermark,
                (fetched, total, message) =>
                {
                    // Map TFS progress to 0-80% (leave 20% for database upsert)
                    var percent = total > 0 ? (int)((fetched / (double)total) * 80) : 50;
                    progressCallback(percent);
                },
                cancellationToken);

            var workItemList = workItems.ToList();

            _logger.LogInformation(
                "Fetched {Count} work items from TFS for ProductOwner {ProductOwnerId}",
                workItemList.Count,
                context.ProductOwnerId);

            progressCallback(80);

            if (workItemList.Count == 0)
            {
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0, context.WorkItemWatermark);
            }

            // Upsert work items to database in batches
            var maxChangedDate = await UpsertWorkItemsAsync(workItemList, context.ProductOwnerId, progressCallback, cancellationToken);

            progressCallback(100);

            _logger.LogInformation(
                "Successfully synced {Count} work items for ProductOwner {ProductOwnerId}, new watermark: {Watermark}",
                workItemList.Count,
                context.ProductOwnerId,
                maxChangedDate?.ToString("O") ?? "none");

            return SyncStageResult.CreateSuccess(workItemList.Count, maxChangedDate);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Work item sync cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Work item sync failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }

    private async Task<DateTimeOffset?> UpsertWorkItemsAsync(
        List<WorkItemDto> workItems,
        int productOwnerId,
        Action<int> progressCallback,
        CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        DateTimeOffset? maxChangedDate = null;

        var tfsIds = workItems.Select(w => w.TfsId).ToList();
        var existingIds = await _context.WorkItems
            .Where(w => tfsIds.Contains(w.TfsId))
            .Select(w => w.TfsId)
            .ToListAsync(cancellationToken);

        var existingSet = existingIds.ToHashSet();
        var totalBatches = (int)Math.Ceiling(workItems.Count / (double)batchSize);
        var processedBatches = 0;

        foreach (var batch in workItems.Chunk(batchSize))
        {
            foreach (var dto in batch)
            {
                // Track max retrieved date for watermark
                if (maxChangedDate == null || dto.RetrievedAt > maxChangedDate)
                {
                    maxChangedDate = dto.RetrievedAt;
                }

                if (existingSet.Contains(dto.TfsId))
                {
                    // Update existing
                    var entity = await _context.WorkItems
                        .FirstAsync(w => w.TfsId == dto.TfsId, cancellationToken);
                    UpdateEntity(entity, dto);
                }
                else
                {
                    // Insert new
                    var entity = MapToEntity(dto);
                    await _context.WorkItems.AddAsync(entity, cancellationToken);
                    existingSet.Add(dto.TfsId);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            processedBatches++;
            // Map batch progress to 80-100%
            var percent = 80 + (int)((processedBatches / (double)totalBatches) * 20);
            progressCallback(Math.Min(percent, 99));
        }

        return maxChangedDate;
    }

    private static void UpdateEntity(WorkItemEntity entity, WorkItemDto dto)
    {
        entity.ParentTfsId = dto.ParentTfsId;
        entity.Type = dto.Type;
        entity.Title = dto.Title;
        entity.AreaPath = dto.AreaPath;
        entity.IterationPath = dto.IterationPath;
        entity.State = dto.State;
        entity.JsonPayload = dto.JsonPayload;
        entity.RetrievedAt = dto.RetrievedAt;
        entity.Effort = dto.Effort;
        entity.Description = dto.Description;
        // Note: TfsRevision, TfsChangedDate, TfsETag are set during write-back operations, not during sync
    }

    private static WorkItemEntity MapToEntity(WorkItemDto dto)
    {
        return new WorkItemEntity
        {
            TfsId = dto.TfsId,
            ParentTfsId = dto.ParentTfsId,
            Type = dto.Type,
            Title = dto.Title,
            AreaPath = dto.AreaPath,
            IterationPath = dto.IterationPath,
            State = dto.State,
            JsonPayload = dto.JsonPayload,
            RetrievedAt = dto.RetrievedAt,
            Effort = dto.Effort,
            Description = dto.Description
        };
    }
}
