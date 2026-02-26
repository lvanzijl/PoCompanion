using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Returns activity details for a selected work item and its descendants in a period.
/// </summary>
public sealed class GetWorkItemActivityDetailsQueryHandler
    : IQueryHandler<GetWorkItemActivityDetailsQuery, WorkItemActivityDetailsDto?>
{
    private readonly PoToolDbContext _context;

    public GetWorkItemActivityDetailsQueryHandler(PoToolDbContext context)
    {
        _context = context;
    }

    public async ValueTask<WorkItemActivityDetailsDto?> Handle(
        GetWorkItemActivityDetailsQuery query,
        CancellationToken cancellationToken)
    {
        var productIds = await _context.Products
            .Where(p => p.ProductOwnerId == query.ProductOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
        {
            return null;
        }

        var resolvedWorkItemIds = await _context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value))
            .Select(r => r.WorkItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var workItems = await _context.WorkItems
            .AsNoTracking()
            .Where(w => resolvedWorkItemIds.Contains(w.TfsId))
            .Select(w => new
            {
                w.TfsId,
                w.ParentTfsId,
                w.Title,
                w.Type
            })
            .ToListAsync(cancellationToken);

        var workItemsById = workItems.ToDictionary(w => w.TfsId);
        if (!workItemsById.TryGetValue(query.WorkItemId, out var root))
        {
            return null;
        }

        var childrenByParent = workItems
            .Where(w => w.ParentTfsId.HasValue)
            .GroupBy(w => w.ParentTfsId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TfsId).ToList());

        var relevantIds = new HashSet<int> { query.WorkItemId };
        var queue = new Queue<int>();
        queue.Enqueue(query.WorkItemId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var childId in children)
            {
                if (relevantIds.Add(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        var fromUtc = query.PeriodStartUtc?.UtcDateTime;
        var toUtc = query.PeriodEndUtc?.UtcDateTime;
        var eventQuery = _context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e => e.ProductOwnerId == query.ProductOwnerId && relevantIds.Contains(e.WorkItemId));

        if (fromUtc.HasValue)
        {
            eventQuery = eventQuery.Where(e => e.EventTimestampUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            eventQuery = eventQuery.Where(e => e.EventTimestampUtc <= toUtc.Value);
        }

        var eventRows = await eventQuery
            .Where(e => !new[] { "SYSTEM.CHANGEDBY", "SYSTEM.CHANGEDDATE" }
                .Contains((e.FieldRefName ?? string.Empty).ToUpper()))
            .OrderByDescending(e => e.EventTimestampUtc)
            .ToListAsync(cancellationToken);

        var activities = eventRows
            .Select(e =>
            {
                var wi = workItemsById.GetValueOrDefault(e.WorkItemId);
                return new WorkItemActivityEntryDto
                {
                    WorkItemId = e.WorkItemId,
                    WorkItemTitle = wi?.Title ?? $"Work Item {e.WorkItemId}",
                    WorkItemType = wi?.Type ?? "Unknown",
                    IsSelectedWorkItem = e.WorkItemId == query.WorkItemId,
                    FieldRefName = e.FieldRefName,
                    OldValue = e.OldValue,
                    NewValue = e.NewValue,
                    EventTimestampUtc = new DateTimeOffset(e.EventTimestampUtc, TimeSpan.Zero)
                };
            })
            .ToList();

        return new WorkItemActivityDetailsDto
        {
            WorkItemId = root.TfsId,
            WorkItemTitle = root.Title,
            WorkItemType = root.Type,
            PeriodStartUtc = query.PeriodStartUtc,
            PeriodEndUtc = query.PeriodEndUtc,
            Activities = activities
        };
    }
}
