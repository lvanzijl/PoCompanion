using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.Portfolio;

namespace PoTool.Api.Services;

public sealed record PortfolioSnapshotCaptureSource(
    int SprintId,
    string Source,
    DateTime StartDateUtc,
    DateTime EndDateUtc)
{
    public DateTimeOffset Timestamp => new(DateTime.SpecifyKind(EndDateUtc, DateTimeKind.Utc), TimeSpan.Zero);
}

public interface IPortfolioSnapshotCaptureDataService
{
    Task<IReadOnlyList<PortfolioSnapshotCaptureSource>> GetLatestSourcesAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>> BuildSnapshotInputsByProductAsync(
        int productOwnerId,
        PortfolioSnapshotCaptureSource source,
        CancellationToken cancellationToken);
}

public sealed class PortfolioSnapshotCaptureDataService : IPortfolioSnapshotCaptureDataService
{
    private readonly PoToolDbContext _context;
    private readonly SprintTrendProjectionService _projectionService;

    public PortfolioSnapshotCaptureDataService(
        PoToolDbContext context,
        SprintTrendProjectionService projectionService)
    {
        _context = context;
        _projectionService = projectionService;
    }

    public async Task<IReadOnlyList<PortfolioSnapshotCaptureSource>> GetLatestSourcesAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        if (productIds.Count == 0)
        {
            return [];
        }

        return (await _context.ResolvedWorkItems
                .AsNoTracking()
                .Where(item =>
                    item.ResolvedProductId != null
                    && productIds.Contains(item.ResolvedProductId.Value)
                    && item.ResolvedSprintId != null)
                .Select(item => item.ResolvedSprintId!.Value)
                .Distinct()
                .Join(
                    _context.Sprints.AsNoTracking().Where(sprint => sprint.StartDateUtc != null && sprint.EndDateUtc != null),
                    sprintId => sprintId,
                    sprint => sprint.Id,
                    (sprintId, sprint) => new
                    {
                        sprintId,
                        sprint.Name,
                        StartDateUtc = sprint.StartDateUtc!.Value,
                        EndDateUtc = sprint.EndDateUtc!.Value
                    })
                .OrderByDescending(source => source.EndDateUtc)
                .ThenByDescending(source => source.sprintId)
                .Take(2)
                .ToListAsync(cancellationToken))
            .Select(source => new PortfolioSnapshotCaptureSource(
                source.sprintId,
                source.Name,
                DateTime.SpecifyKind(source.StartDateUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(source.EndDateUtc, DateTimeKind.Utc)))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>> BuildSnapshotInputsByProductAsync(
        int productOwnerId,
        PortfolioSnapshotCaptureSource source,
        CancellationToken cancellationToken)
    {
        var featureProgress = await _projectionService.ComputeFeatureProgressAsync(
            productOwnerId,
            FeatureProgressMode.StoryPoints,
            source.StartDateUtc,
            source.EndDateUtc,
            cancellationToken,
            source.SprintId);

        if (featureProgress.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>();
        }

        var epicProgress = await _projectionService.ComputeEpicProgressAsync(
            productOwnerId,
            featureProgress,
            cancellationToken);

        if (epicProgress.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>();
        }

        var epicIds = epicProgress
            .Select(progress => progress.EpicId)
            .Distinct()
            .ToList();

        var epicsById = await _context.WorkItems
            .AsNoTracking()
            .Where(workItem => epicIds.Contains(workItem.TfsId))
            .Select(workItem => new
            {
                workItem.TfsId,
                workItem.ProjectNumber,
                workItem.ProjectElement
            })
            .ToDictionaryAsync(workItem => workItem.TfsId, cancellationToken);

        var missingProjectNumberEpicIds = epicProgress
            .Where(progress =>
                !epicsById.TryGetValue(progress.EpicId, out var epic)
                || string.IsNullOrWhiteSpace(epic.ProjectNumber))
            .Select(progress => progress.EpicId)
            .Distinct()
            .OrderBy(epicId => epicId)
            .ToArray();

        if (missingProjectNumberEpicIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Portfolio snapshot source '{source.Source}' cannot be captured because required ProjectNumber is missing for Epic IDs: {string.Join(", ", missingProjectNumberEpicIds)}.");
        }

        return epicProgress
            .Select(progress =>
            {
                var epic = epicsById[progress.EpicId];
                return new PortfolioSnapshotFactoryEpicInput(
                    progress.ProductId,
                    epic.ProjectNumber!.Trim(),
                    string.IsNullOrWhiteSpace(epic.ProjectElement) ? null : epic.ProjectElement.Trim(),
                    (progress.AggregatedProgress ?? 0d) / 100d,
                    progress.TotalWeight);
            })
            .GroupBy(input => input.ProductId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PortfolioSnapshotFactoryEpicInput>)group
                    .OrderBy(input => input.ProjectNumber, StringComparer.Ordinal)
                    .ThenBy(input => input.WorkPackage is null ? 0 : 1)
                    .ThenBy(input => input.WorkPackage, StringComparer.Ordinal)
                    .ToArray());
    }
}
