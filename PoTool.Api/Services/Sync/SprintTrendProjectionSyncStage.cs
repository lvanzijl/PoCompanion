using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that refreshes sprint trend projections.
/// </summary>
public class SprintTrendProjectionSyncStage : ISyncStage
{
    private readonly PoToolDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SprintTrendProjectionService _projectionService;
    private readonly ILogger<SprintTrendProjectionSyncStage> _logger;

    public SprintTrendProjectionSyncStage(
        PoToolDbContext context,
        IServiceScopeFactory scopeFactory,
        SprintTrendProjectionService projectionService,
        ILogger<SprintTrendProjectionSyncStage> logger)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _projectionService = projectionService;
        _logger = logger;
    }

    public string StageName => "ComputeSprintTrends";

    public int StageNumber => 6;

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);

            var productIds = await _context.Products
                .Where(p => p.ProductOwnerId == context.ProductOwnerId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            if (productIds.Count == 0)
            {
                return SyncStageResult.CreateSuccess(0);
            }

            var sprintIds = await _context.Sprints
                .Where(s => s.Team.ProductTeamLinks.Any(ptl => productIds.Contains(ptl.ProductId)))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            if (sprintIds.Count == 0)
            {
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0);
            }

            var projections = await _projectionService.ComputeProjectionsAsync(
                context.ProductOwnerId,
                sprintIds,
                null,
                async (progress, token) =>
                {
                    progressCallback(progress);
                    await PersistProgressAsync(context.ProductOwnerId, progress, token);
                },
                cancellationToken);

            var cacheState = await _context.ProductOwnerCacheStates
                .OrderBy(state => state.Id)
                .FirstOrDefaultAsync(state => state.ProductOwnerId == context.ProductOwnerId, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            if (cacheState != null)
            {
                cacheState.SprintTrendProjectionAsOfUtc = now;
                await _context.SaveChangesAsync(cancellationToken);
            }

            progressCallback(100);
            return SyncStageResult.CreateSuccess(projections.Count, now);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Sprint trend projection compute cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sprint trend projection compute failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }

    private async Task PersistProgressAsync(int productOwnerId, int progressPercent, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var cacheState = await context.ProductOwnerCacheStates
            .OrderBy(state => state.Id)
            .FirstOrDefaultAsync(state => state.ProductOwnerId == productOwnerId, cancellationToken);

        if (cacheState is null)
        {
            return;
        }

        if (cacheState.StageProgressPercent == progressPercent)
        {
            return;
        }

        cacheState.CurrentSyncStage = StageName;
        cacheState.StageProgressPercent = progressPercent;
        cacheState.LastAttemptSync = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
