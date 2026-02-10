using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that fetches and upserts pipeline runs from TFS.
/// </summary>
public class PipelineSyncStage : ISyncStage
{
    private readonly ITfsClient _tfsClient;
    private readonly PoToolDbContext _context;
    private readonly ILogger<PipelineSyncStage> _logger;

    public string StageName => "SyncPipelines";
    public int StageNumber => 8;

    public PipelineSyncStage(
        ITfsClient tfsClient,
        PoToolDbContext context,
        ILogger<PipelineSyncStage> logger)
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
            if (context.PipelineDefinitionIds.Length == 0)
            {
                _logger.LogInformation("No pipeline definitions configured for ProductOwner {ProductOwnerId}, skipping pipeline sync", context.ProductOwnerId);
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0);
            }

            progressCallback(0);

            _logger.LogInformation(
                "Starting pipeline sync for ProductOwner {ProductOwnerId} with {PipelineCount} pipelines, watermark: {Watermark}",
                context.ProductOwnerId,
                context.PipelineDefinitionIds.Length,
                context.PipelineWatermark?.ToString("O") ?? "null (full sync)");

            // Fetch pipeline runs from TFS
            var pipelineRuns = await _tfsClient.GetPipelineRunsAsync(
                context.PipelineDefinitionIds,
                branchName: null,
                minStartTime: context.PipelineWatermark,
                top: 100,
                cancellationToken);

            var runList = pipelineRuns.ToList();

            _logger.LogInformation(
                "Fetched {Count} pipeline runs from TFS for ProductOwner {ProductOwnerId}",
                runList.Count,
                context.ProductOwnerId);

            progressCallback(80);

            if (runList.Count == 0)
            {
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0, context.PipelineWatermark);
            }

            // Get pipeline definition mapping (PipelineId -> internal PipelineDefinitionId)
            var pipelineIdMapping = await _context.PipelineDefinitions
                .Where(pd => context.PipelineDefinitionIds.Contains(pd.PipelineDefinitionId))
                .ToDictionaryAsync(pd => pd.PipelineDefinitionId, pd => pd.Id, cancellationToken);

            // Upsert pipeline runs to database
            var maxDate = await UpsertPipelineRunsAsync(runList, context.ProductOwnerId, pipelineIdMapping, progressCallback, cancellationToken);

            progressCallback(100);

            _logger.LogInformation(
                "Successfully synced {Count} pipeline runs for ProductOwner {ProductOwnerId}, new watermark: {Watermark}",
                runList.Count,
                context.ProductOwnerId,
                maxDate?.ToString("O") ?? "none");

            return SyncStageResult.CreateSuccess(runList.Count, maxDate);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline sync cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline sync failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }

    private async Task<DateTimeOffset?> UpsertPipelineRunsAsync(
        List<PipelineRunDto> runs,
        int productOwnerId,
        Dictionary<int, int> pipelineIdMapping,
        Action<int> progressCallback,
        CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        DateTimeOffset? maxDate = null;

        // Build unique key set for existing runs
        var runKeys = runs
            .Where(r => pipelineIdMapping.ContainsKey(r.PipelineId))
            .Select(r => (ProductOwnerId: productOwnerId, PipelineDefId: pipelineIdMapping[r.PipelineId], TfsRunId: r.RunId))
            .ToList();

        var existingKeys = await _context.CachedPipelineRuns
            .Where(r => r.ProductOwnerId == productOwnerId)
            .Select(r => new { r.PipelineDefinitionId, r.TfsRunId })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys.Select(k => (k.PipelineDefinitionId, k.TfsRunId)).ToHashSet();
        var totalBatches = (int)Math.Ceiling(runs.Count / (double)batchSize);
        var processedBatches = 0;

        foreach (var batch in runs.Chunk(batchSize))
        {
            // Get batch keys that should be updated
            var batchKeysToUpdate = batch
                .Where(dto => pipelineIdMapping.TryGetValue(dto.PipelineId, out _))
                .Select(dto => (PipelineDefId: pipelineIdMapping[dto.PipelineId], dto.RunId))
                .Where(k => existingSet.Contains(k))
                .ToList();

            // Load all existing entities for this batch in a single query
            var runIds = batchKeysToUpdate.Select(k => k.RunId).ToList();
            var existingEntities = await _context.CachedPipelineRuns
                .Where(r => r.ProductOwnerId == productOwnerId && runIds.Contains(r.TfsRunId))
                .ToDictionaryAsync(r => (r.PipelineDefinitionId, r.TfsRunId), cancellationToken);

            foreach (var dto in batch)
            {
                if (!pipelineIdMapping.TryGetValue(dto.PipelineId, out var internalPipelineDefId))
                {
                    continue; // Skip runs for unknown pipelines
                }

                // Track max finish date for watermark
                if (dto.FinishTime.HasValue && (maxDate == null || dto.FinishTime > maxDate))
                {
                    maxDate = dto.FinishTime;
                }

                var key = (internalPipelineDefId, dto.RunId);
                if (existingEntities.TryGetValue(key, out var entity))
                {
                    // Update existing
                    UpdateEntity(entity, dto);
                }
                else if (!existingSet.Contains(key))
                {
                    // Insert new
                    var newEntity = MapToEntity(dto, productOwnerId, internalPipelineDefId);
                    await _context.CachedPipelineRuns.AddAsync(newEntity, cancellationToken);
                    existingSet.Add(key);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            processedBatches++;
            var percent = 80 + (int)((processedBatches / (double)totalBatches) * 20);
            progressCallback(Math.Min(percent, 99));
        }

        return maxDate;
    }

    private static void UpdateEntity(CachedPipelineRunEntity entity, PipelineRunDto dto)
    {
        entity.RunName = dto.PipelineName;
        entity.State = dto.FinishTime.HasValue ? "completed" : "running";
        entity.Result = dto.Result.ToString();
        entity.CreatedDate = dto.StartTime;
        entity.FinishedDate = dto.FinishTime;
        entity.SourceBranch = dto.Branch;
        entity.CachedAt = DateTimeOffset.UtcNow;
    }

    private static CachedPipelineRunEntity MapToEntity(PipelineRunDto dto, int productOwnerId, int pipelineDefId)
    {
        return new CachedPipelineRunEntity
        {
            ProductOwnerId = productOwnerId,
            PipelineDefinitionId = pipelineDefId,
            TfsRunId = dto.RunId,
            RunName = dto.PipelineName,
            State = dto.FinishTime.HasValue ? "completed" : "running",
            Result = dto.Result.ToString(),
            CreatedDate = dto.StartTime,
            FinishedDate = dto.FinishTime,
            SourceBranch = dto.Branch,
            CachedAt = DateTimeOffset.UtcNow
        };
    }
}
