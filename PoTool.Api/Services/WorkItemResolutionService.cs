using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

public class WorkItemResolutionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkItemResolutionService> _logger;

    public WorkItemResolutionService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkItemResolutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<ResolutionResult> ResolveAllAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var cacheState = await context.ProductOwnerCacheStates
            .FirstOrDefaultAsync(state => state.ProductOwnerId == productOwnerId, cancellationToken)
            ?? new ProductOwnerCacheStateEntity
               {
                   ProductOwnerId = productOwnerId,
                   SyncStatus = CacheSyncStatus.Idle
               };

        // REPLACE_WITH_ACTIVITY_SOURCE: resolve product/epic/feature/sprint lineage from activity events.
        if (cacheState.Id == 0)
        {
            context.ProductOwnerCacheStates.Add(cacheState);
        }

        cacheState.ResolutionAsOfUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Resolution skipped for ProductOwner {ProductOwnerId}: no activity source configured.", productOwnerId);
        return new ResolutionResult
        {
            Success = true,
            ResolvedCount = 0,
            OrphanCount = 0,
            ErrorCount = 0,
            Message = "Resolution skipped: no activity source configured."
        };
    }
}

public record ResolutionResult
{
    public bool Success { get; init; }
    public int ResolvedCount { get; init; }
    public int OrphanCount { get; init; }
    public int ErrorCount { get; init; }
    public required string Message { get; init; }
}
