using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that ingests work item revisions from TFS.
/// Revisions are required for sprint trend metrics and activity detection.
/// </summary>
public class RevisionSyncStage : ISyncStage
{
    private readonly RevisionIngestionService _revisionIngestionService;
    private readonly ILogger<RevisionSyncStage> _logger;

    public string StageName => "SyncRevisions";
    public int StageNumber => 3;

    public RevisionSyncStage(
        RevisionIngestionService revisionIngestionService,
        ILogger<RevisionSyncStage> logger)
    {
        _revisionIngestionService = revisionIngestionService;
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
                "Starting revision ingestion for ProductOwner {ProductOwnerId}",
                context.ProductOwnerId);

            // Ingest revisions using the RevisionIngestionService
            var result = await _revisionIngestionService.IngestRevisionsAsync(
                context.ProductOwnerId,
                progress =>
                {
                    // Map ingestion progress to sync stage progress
                    progressCallback(progress.PercentComplete);
                },
                cancellationToken);

            if (!result.Success)
            {
                if (result.IsAlreadyRunning)
                {
                    // If ingestion is already running, treat as success to avoid blocking the sync
                    _logger.LogInformation(
                        "Revision ingestion already in progress for ProductOwner {ProductOwnerId}, continuing with sync",
                        context.ProductOwnerId);
                    return SyncStageResult.CreateSuccess(0);
                }

                if (result.WasCancelled)
                {
                    throw new OperationCanceledException("Revision ingestion was cancelled");
                }

                var errorMessage = result.ErrorMessage ?? result.Message ?? "Unknown error during revision ingestion";
                _logger.LogWarning(
                    "Revision ingestion stage failed for ProductOwner {ProductOwnerId}. Outcome={Outcome} Error={ErrorMessage}",
                    context.ProductOwnerId,
                    result.RunOutcome,
                    errorMessage);
                return SyncStageResult.CreateFailure(errorMessage);
            }

            progressCallback(100);

            if (result.WasTerminatedEarly)
            {
                _logger.LogWarning(
                    "Revision ingestion terminated early for ProductOwner {ProductOwnerId}. Reason={Reason} Message={Message}",
                    context.ProductOwnerId,
                    result.TerminationReason,
                    result.TerminationMessage);
            }

            _logger.LogInformation(
                "Successfully ingested {RevisionCount} revisions in {PageCount} pages for ProductOwner {ProductOwnerId}",
                result.RevisionsIngested,
                result.PagesProcessed,
                context.ProductOwnerId);

            return SyncStageResult.CreateSuccess(
                result.RevisionsIngested,
                hasWarnings: result.HasWarnings,
                warningMessage: result.WarningMessage);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Revision ingestion cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revision ingestion failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }
}
