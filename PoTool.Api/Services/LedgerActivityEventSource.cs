using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

public sealed class LedgerActivityEventSource : IActivityEventSource
{
    private readonly PoToolDbContext _context;

    public LedgerActivityEventSource(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ActivityEvent>> GetActivityEventsAsync(
        IReadOnlyCollection<int> workItemIds,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        if (workItemIds.Count == 0)
        {
            return Array.Empty<ActivityEvent>();
        }

        var query = _context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e => workItemIds.Contains(e.WorkItemId));

        if (from.HasValue)
        {
            var fromUtc = from.Value.UtcDateTime;
            query = query.Where(e => e.EventTimestampUtc >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.UtcDateTime;
            query = query.Where(e => e.EventTimestampUtc <= toUtc);
        }

        return await query
            .OrderBy(e => e.EventTimestampUtc)
            .Select(e => new ActivityEvent
            {
                Timestamp = e.EventTimestamp,
                WorkItemId = e.WorkItemId,
                Kind = "FieldChanged",
                IterationPath = e.IterationPath,
                ParentId = e.ParentId,
                FeatureId = e.FeatureId,
                EpicId = e.EpicId,
                FieldRefName = e.FieldRefName,
                OldValue = e.OldValue,
                NewValue = e.NewValue
            })
            .ToListAsync(cancellationToken);
    }
}
