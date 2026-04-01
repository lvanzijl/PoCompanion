using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Settings.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for loading persisted planning projections for roadmap epics in one product.
/// </summary>
public sealed class GetProductPlanningProjectionsQueryHandler
    : IQueryHandler<GetProductPlanningProjectionsQuery, IReadOnlyList<PlanningEpicProjectionDto>?>
{
    private readonly PoToolDbContext _context;

    public GetProductPlanningProjectionsQueryHandler(PoToolDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<PlanningEpicProjectionDto>?> Handle(
        GetProductPlanningProjectionsQuery query,
        CancellationToken cancellationToken)
    {
        var productExists = await _context.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == query.ProductId, cancellationToken);

        if (!productExists)
        {
            return null;
        }

        var candidates = await (
            from resolved in _context.ResolvedWorkItems.AsNoTracking()
            join workItem in _context.WorkItems.AsNoTracking() on resolved.WorkItemId equals workItem.TfsId
            join projection in _context.ForecastProjections.AsNoTracking() on workItem.TfsId equals projection.WorkItemId into projectionJoin
            from projection in projectionJoin.DefaultIfEmpty()
            where resolved.ResolvedProductId == query.ProductId
                && workItem.Type == WorkItemType.Epic
            select new
            {
                workItem.TfsId,
                workItem.Title,
                workItem.BacklogPriority,
                workItem.Tags,
                Projection = projection
            })
            .ToListAsync(cancellationToken);

        var roadmapEpics = candidates
            .Where(candidate => HasRoadmapTag(candidate.Tags))
            .OrderBy(candidate => candidate.BacklogPriority ?? double.MaxValue)
            .ThenBy(candidate => candidate.TfsId)
            .ToList();

        return roadmapEpics
            .Select((candidate, index) => new PlanningEpicProjectionDto(
                candidate.TfsId,
                candidate.Title,
                index + 1,
                candidate.Projection?.SprintsRemaining,
                candidate.Projection?.EstimatedCompletionDate,
                ParseConfidence(candidate.Projection?.Confidence),
                candidate.Projection is not null,
                candidate.Projection?.LastUpdated))
            .ToList();
    }

    private static bool HasRoadmapTag(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return false;
        }

        return tags
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(tag => string.Equals(tag, "roadmap", StringComparison.OrdinalIgnoreCase));
    }

    private static ForecastConfidence? ParseConfidence(string? confidence)
    {
        return Enum.TryParse<ForecastConfidence>(confidence, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }
}
