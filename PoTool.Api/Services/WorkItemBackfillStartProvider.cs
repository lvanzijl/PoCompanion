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

      var createdDates = await context.WorkItems
          .Where(w => workItemIds.Contains(w.TfsId) && w.CreatedDate != null)
          .Select(w => w.CreatedDate!.Value)
          .ToListAsync(cancellationToken);

      var earliest = createdDates.Min();

      return earliest;
    }
}
