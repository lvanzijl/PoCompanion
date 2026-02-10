using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that captures the current work item relationship graph.
/// </summary>
public class WorkItemRelationshipSnapshotStage : ISyncStage
{
    private readonly WorkItemRelationshipSnapshotService _snapshotService;
    private readonly ILogger<WorkItemRelationshipSnapshotStage> _logger;

    public WorkItemRelationshipSnapshotStage(
        WorkItemRelationshipSnapshotService snapshotService,
        ILogger<WorkItemRelationshipSnapshotStage> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }

    public string StageName => "SyncWorkItemRelationships";

    public int StageNumber => 4;

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);
            var result = await _snapshotService.BuildSnapshotAsync(context.ProductOwnerId, cancellationToken);

            if (!result.Success)
            {
                var message = result.ErrorMessage ?? "Failed to build work item relationships snapshot.";
                _logger.LogWarning("Relationships snapshot failed for ProductOwner {ProductOwnerId}: {Message}", context.ProductOwnerId, message);
                return SyncStageResult.CreateFailure(message);
            }

            progressCallback(100);
            return SyncStageResult.CreateSuccess(result.EdgeCount, result.SnapshotAsOfUtc);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Relationships snapshot cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Relationships snapshot failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }
}
