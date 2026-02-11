using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that resolves work item hierarchies using the relationships snapshot.
/// </summary>
public class WorkItemResolutionSyncStage : ISyncStage
{
    private readonly WorkItemResolutionService _resolutionService;
    private readonly ILogger<WorkItemResolutionSyncStage> _logger;

    public WorkItemResolutionSyncStage(
        WorkItemResolutionService resolutionService,
        ILogger<WorkItemResolutionSyncStage> logger)
    {
        _resolutionService = resolutionService;
        _logger = logger;
    }

    public string StageName => "ResolveWorkItems";

    public int StageNumber => 5;

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);
            var result = await _resolutionService.ResolveAllAsync(context.ProductOwnerId, cancellationToken);
            if (!result.Success)
            {
                var message = result.Message ?? "Work item resolution failed.";
                _logger.LogWarning("Work item resolution failed for ProductOwner {ProductOwnerId}: {Message}", context.ProductOwnerId, message);
                return SyncStageResult.CreateFailure(message);
            }

            var warning = result.OrphanCount > 0
                ? $"Resolution identified {result.OrphanCount} orphaned work items."
                : null;

            progressCallback(100);
            return SyncStageResult.CreateSuccess(
                result.ResolvedCount + result.OrphanCount,
                DateTimeOffset.UtcNow,
                hasWarnings: warning != null,
                warningMessage: warning);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Work item resolution cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Work item resolution failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }
}
