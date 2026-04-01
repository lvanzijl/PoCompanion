using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that refreshes persisted forecast projections.
/// </summary>
public class ForecastProjectionSyncStage : ISyncStage
{
    private readonly ForecastProjectionMaterializationService _projectionService;
    private readonly ILogger<ForecastProjectionSyncStage> _logger;

    public ForecastProjectionSyncStage(
        ForecastProjectionMaterializationService projectionService,
        ILogger<ForecastProjectionSyncStage> logger)
    {
        _projectionService = projectionService;
        _logger = logger;
    }

    public string StageName => "ComputeForecastProjections";

    public int StageNumber => 6;

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);

            var projections = await _projectionService.ComputeProjectionsAsync(
                context.ProductOwnerId,
                cancellationToken);
            var now = DateTimeOffset.UtcNow;

            progressCallback(100);
            return SyncStageResult.CreateSuccess(projections.Count, now);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Forecast projection compute cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forecast projection compute failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }
}
