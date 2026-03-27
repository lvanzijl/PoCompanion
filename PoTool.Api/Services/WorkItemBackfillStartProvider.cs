using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

/// <summary>
/// Default backfill start provider that derives the earliest changed date
/// from work items stored in the local database.
/// </summary>
public sealed class WorkItemBackfillStartProvider : IBackfillStartProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    public WorkItemBackfillStartProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<DateTimeOffset?> GetEarliestChangedDateUtcAsync(
        IReadOnlyCollection<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        if (workItemIds.Count == 0)
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var earliestCreatedDateUtc = await context.WorkItems
            .Where(w => workItemIds.Contains(w.TfsId) && w.CreatedDateUtc != null)
            .MinAsync(w => w.CreatedDateUtc, cancellationToken);

        if (!earliestCreatedDateUtc.HasValue)
        {
            return null;
        }

        return new DateTimeOffset(DateTime.SpecifyKind(earliestCreatedDateUtc.Value, DateTimeKind.Utc));
    }
}
