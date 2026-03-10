using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Aggregates compact Home product bar metrics for the active Product Owner context.
/// </summary>
public sealed class GetHomeProductBarMetricsQueryHandler
    : IQueryHandler<GetHomeProductBarMetricsQuery, HomeProductBarMetricsDto>
{
    private static readonly string[] FallbackDoneStates = ["Done", "Closed", "Resolved", "Removed"];
    private const string BugType = "Bug";

    private readonly PoToolDbContext _context;

    public GetHomeProductBarMetricsQueryHandler(PoToolDbContext context)
    {
        _context = context;
    }

    public async ValueTask<HomeProductBarMetricsDto> Handle(
        GetHomeProductBarMetricsQuery query,
        CancellationToken cancellationToken)
    {
        var ownerProductIds = await _context.Products
            .AsNoTracking()
            .Where(product => product.ProductOwnerId == query.ProductOwnerId)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        if (ownerProductIds.Count == 0)
        {
            return new HomeProductBarMetricsDto();
        }

        var targetProductIds = query.ProductId.HasValue
            ? ownerProductIds.Where(productId => productId == query.ProductId.Value).ToList()
            : ownerProductIds;

        var sprintProgressPercentage = await LoadSprintProgressPercentageAsync(ownerProductIds, cancellationToken);

        if (targetProductIds.Count == 0)
        {
            return new HomeProductBarMetricsDto
            {
                SprintProgressPercentage = sprintProgressPercentage
            };
        }

        var doneStates = await _context.WorkItemStateClassifications
            .AsNoTracking()
            .Where(classification =>
                classification.WorkItemType == BugType &&
                classification.Classification == (int)StateClassification.Done)
            .Select(classification => classification.StateName)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (doneStates.Count == 0)
        {
            doneStates = FallbackDoneStates
                .Select(state => state.ToUpperInvariant())
                .ToList();
        }
        else
        {
            doneStates = doneStates
                .Select(state => state.ToUpperInvariant())
                .ToList();
        }

        var doneStateSet = doneStates.ToHashSet();

        var resolvedWorkItemIdsQuery = _context.ResolvedWorkItems
            .AsNoTracking()
            .Where(item =>
                item.ResolvedProductId != null &&
                targetProductIds.Contains(item.ResolvedProductId.Value))
            .Select(item => item.WorkItemId)
            .Distinct();

        var bugStates = await _context.WorkItems
            .AsNoTracking()
            .Where(workItem =>
                workItem.Type == BugType &&
                resolvedWorkItemIdsQuery.Contains(workItem.TfsId))
            .Select(workItem => workItem.State)
            .ToListAsync(cancellationToken);

        var openBugCount = bugStates.Count(state => !doneStateSet.Contains((state ?? string.Empty).ToUpperInvariant()));

        var startOfTodayUtc = DateTime.UtcNow.Date;
        var startOfTomorrowUtc = startOfTodayUtc.AddDays(1);

        var changesTodayCount = await _context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.ProductOwnerId == query.ProductOwnerId &&
                entry.EventTimestampUtc >= startOfTodayUtc &&
                entry.EventTimestampUtc < startOfTomorrowUtc &&
                resolvedWorkItemIdsQuery.Contains(entry.WorkItemId))
            .Select(entry => entry.WorkItemId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new HomeProductBarMetricsDto
        {
            SprintProgressPercentage = sprintProgressPercentage,
            BugCount = openBugCount,
            ChangesTodayCount = changesTodayCount
        };
    }

    private async Task<int?> LoadSprintProgressPercentageAsync(
        IReadOnlyCollection<int> ownerProductIds,
        CancellationToken cancellationToken)
    {
        var ownerTeamIds = await _context.ProductTeamLinks
            .AsNoTracking()
            .Where(link => ownerProductIds.Contains(link.ProductId))
            .Select(link => link.TeamId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (ownerTeamIds.Count == 0)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        var currentSprints = await _context.Sprints
            .AsNoTracking()
            .Where(sprint =>
                ownerTeamIds.Contains(sprint.TeamId) &&
                sprint.StartDateUtc != null &&
                sprint.EndDateUtc != null &&
                sprint.StartDateUtc <= nowUtc &&
                sprint.EndDateUtc >= nowUtc)
            .Select(sprint => new { sprint.StartUtc, sprint.EndUtc })
            .ToListAsync(cancellationToken);

        if (currentSprints.Count == 0)
        {
            return null;
        }

        var progressValues = currentSprints
            .Select(sprint => CalculateSprintProgressPercentage(sprint.StartUtc, sprint.EndUtc))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (progressValues.Count == 0)
        {
            return null;
        }

        return (int)Math.Round(progressValues.Average(), MidpointRounding.AwayFromZero);
    }

    private static int? CalculateSprintProgressPercentage(DateTimeOffset? startUtc, DateTimeOffset? endUtc)
    {
        if (!startUtc.HasValue || !endUtc.HasValue || endUtc <= startUtc)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (now <= startUtc.Value)
        {
            return 0;
        }

        if (now >= endUtc.Value)
        {
            return 100;
        }

        var elapsed = now - startUtc.Value;
        var duration = endUtc.Value - startUtc.Value;
        var percentage = elapsed.TotalSeconds / duration.TotalSeconds * 100d;

        return (int)Math.Round(Math.Clamp(percentage, 0d, 100d), MidpointRounding.AwayFromZero);
    }
}
