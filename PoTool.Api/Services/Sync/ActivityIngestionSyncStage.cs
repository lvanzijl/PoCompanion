using PoTool.Api.Services;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that ingests append-only activity events from work item updates.
/// </summary>
public class ActivityIngestionSyncStage : ISyncStage
{
    private readonly ActivityEventIngestionService _ingestionService;
    private readonly ILogger<ActivityIngestionSyncStage> _logger;

    public ActivityIngestionSyncStage(
        ActivityEventIngestionService ingestionService,
        ILogger<ActivityIngestionSyncStage> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    public string StageName => "IngestActivityEvents";
    public int StageNumber => 3;

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);
            var result = await _ingestionService.IngestAsync(context.ProductOwnerId, cancellationToken);
            progressCallback(100);
            return SyncStageResult.CreateSuccess(result.PersistedEventCount, result.Watermark);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Activity ingestion cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Activity ingestion failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }
}
