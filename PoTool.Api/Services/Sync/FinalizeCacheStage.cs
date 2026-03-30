using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Final sync stage that commits all watermarks and updates the cache state.
/// </summary>
public class FinalizeCacheStage : ISyncStage
{
    private readonly ICacheStateRepository _cacheStateRepo;
    private readonly ILogger<FinalizeCacheStage> _logger;

    public string StageName => "FinalizeCache";
    public int StageNumber => 11;

    // These are set by the pipeline runner before execution
    public int WorkItemCount { get; set; }
    public int PullRequestCount { get; set; }
    public int PipelineCount { get; set; }
    public DateTimeOffset? WorkItemWatermark { get; set; }
    public DateTimeOffset? PullRequestWatermark { get; set; }
    public DateTimeOffset? PipelineWatermark { get; set; }
    public DateTimeOffset? PipelineFinishWatermark { get; set; }
    public bool HasWarnings { get; set; }
    public string? WarningMessage { get; set; }

    public FinalizeCacheStage(
        ICacheStateRepository cacheStateRepo,
        ILogger<FinalizeCacheStage> logger)
    {
        _cacheStateRepo = cacheStateRepo;
        _logger = logger;
    }

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);

            _logger.LogInformation(
                "Finalizing cache for ProductOwner {ProductOwnerId}",
                context.ProductOwnerId);

            progressCallback(50);

            // Commit all watermarks and update counts
            if (HasWarnings)
            {
                await _cacheStateRepo.MarkSyncSuccessWithWarningsAsync(
                    context.ProductOwnerId,
                    WorkItemCount,
                    PullRequestCount,
                    PipelineCount,
                    WorkItemWatermark,
                    PullRequestWatermark,
                    PipelineWatermark,
                    PipelineFinishWatermark,
                    WarningMessage,
                    cancellationToken);
            }
            else
            {
                await _cacheStateRepo.MarkSyncSuccessAsync(
                    context.ProductOwnerId,
                    WorkItemCount,
                    PullRequestCount,
                    PipelineCount,
                    WorkItemWatermark,
                    PullRequestWatermark,
                    PipelineWatermark,
                    PipelineFinishWatermark,
                    cancellationToken);
            }

            progressCallback(100);

            _logger.LogInformation(
                "Successfully finalized cache for ProductOwner {ProductOwnerId}: {WorkItems} work items, {PRs} PRs, {Pipelines} pipelines",
                context.ProductOwnerId,
                WorkItemCount,
                PullRequestCount,
                PipelineCount);

            return SyncStageResult.CreateSuccess(1, hasWarnings: HasWarnings, warningMessage: WarningMessage);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cache finalization cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache finalization failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }
}
