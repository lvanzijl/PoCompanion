using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public interface IPortfolioReadModelStateService
{
    Task<PortfolioReadModelState?> GetLatestStateAsync(int productOwnerId, CancellationToken cancellationToken);
}

public sealed record PortfolioReadModelState(
    PortfolioSnapshot CurrentSnapshot,
    string CurrentSnapshotLabel,
    PortfolioSnapshot? PreviousSnapshot,
    string? PreviousSnapshotLabel,
    double? PortfolioProgress,
    double TotalWeight,
    IReadOnlyDictionary<int, string> ProductNames);

public sealed class PortfolioReadModelStateService : IPortfolioReadModelStateService
{
    private readonly PoToolDbContext _context;
    private readonly SprintTrendProjectionService _projectionService;
    private readonly IPortfolioSnapshotFactory _portfolioSnapshotFactory;
    private readonly IProductAggregationService _productAggregationService;
    private readonly ILogger<PortfolioReadModelStateService> _logger;

    public PortfolioReadModelStateService(
        PoToolDbContext context,
        SprintTrendProjectionService projectionService,
        IPortfolioSnapshotFactory portfolioSnapshotFactory,
        IProductAggregationService productAggregationService,
        ILogger<PortfolioReadModelStateService> logger)
    {
        _context = context;
        _projectionService = projectionService;
        _portfolioSnapshotFactory = portfolioSnapshotFactory;
        _productAggregationService = productAggregationService;
        _logger = logger;
    }

    public async Task<PortfolioReadModelState?> GetLatestStateAsync(int productOwnerId, CancellationToken cancellationToken)
    {
        var products = await _context.Products
            .AsNoTracking()
            .Where(product => product.ProductOwnerId == productOwnerId)
            .Select(product => new { product.Id, product.Name })
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return null;
        }

        var productNames = products.ToDictionary(product => product.Id, product => product.Name);
        var productIds = productNames.Keys.ToList();

        var snapshotSources = (await _context.ResolvedWorkItems
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
            .Select(source => new PortfolioSnapshotSource(
                source.sprintId,
                source.Name,
                DateTime.SpecifyKind(source.StartDateUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(source.EndDateUtc, DateTimeKind.Utc)))
            .ToList();

        if (snapshotSources.Count == 0)
        {
            return null;
        }

        var orderedSources = snapshotSources
            .OrderBy(source => source.EndDateUtc)
            .ThenBy(source => source.SprintId)
            .ToList();

        PortfolioSnapshotBuildResult? previous = null;
        PortfolioSnapshotBuildResult? current = null;

        foreach (var source in orderedSources)
        {
            var build = await BuildSnapshotAsync(productOwnerId, source, previous?.Snapshot, cancellationToken);
            if (build is null)
            {
                continue;
            }

            previous = current;
            current = build;
        }

        if (current is null)
        {
            return null;
        }

        var portfolioAggregation = _productAggregationService.Compute(new ProductAggregationRequest(
            current.EpicInputs
                .Select(input => new ProductAggregationEpicInput(
                    input.Progress * 100d,
                    EpicForecastConsumed: null,
                    EpicForecastRemaining: null,
                    input.Weight,
                    IsExcluded: input.Weight <= 0d))
                .ToList()));

        return new PortfolioReadModelState(
            current.Snapshot,
            current.Label,
            previous?.Snapshot,
            previous?.Label,
            portfolioAggregation.ProductProgress,
            portfolioAggregation.TotalWeight,
            productNames);
    }

    private async Task<PortfolioSnapshotBuildResult?> BuildSnapshotAsync(
        int productOwnerId,
        PortfolioSnapshotSource source,
        PortfolioSnapshot? previousSnapshot,
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
            return null;
        }

        var epicProgress = await _projectionService.ComputeEpicProgressAsync(
            productOwnerId,
            featureProgress,
            cancellationToken);

        if (epicProgress.Count == 0)
        {
            return null;
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

        var snapshotInputs = epicProgress
            .Select(progress =>
            {
                if (!epicsById.TryGetValue(progress.EpicId, out var epic) || string.IsNullOrWhiteSpace(epic.ProjectNumber))
                {
                    _logger.LogWarning(
                        "Skipping portfolio snapshot row for Epic {EpicId} because ProjectNumber is unavailable.",
                        progress.EpicId);
                    return null;
                }

                return new PortfolioSnapshotFactoryEpicInput(
                    progress.ProductId,
                    epic.ProjectNumber.Trim(),
                    string.IsNullOrWhiteSpace(epic.ProjectElement) ? null : epic.ProjectElement.Trim(),
                    (progress.AggregatedProgress ?? 0d) / 100d,
                    progress.TotalWeight);
            })
            .OfType<PortfolioSnapshotFactoryEpicInput>()
            .ToList();

        if (snapshotInputs.Count == 0)
        {
            return null;
        }

        var snapshot = _portfolioSnapshotFactory.Create(new PortfolioSnapshotFactoryRequest(
            new DateTimeOffset(source.EndDateUtc, TimeSpan.Zero),
            snapshotInputs,
            previousSnapshot));

        return new PortfolioSnapshotBuildResult(snapshot, source.Name, snapshotInputs);
    }

    private readonly record struct PortfolioSnapshotSource(
        int SprintId,
        string Name,
        DateTime StartDateUtc,
        DateTime EndDateUtc);

    private sealed record PortfolioSnapshotBuildResult(
        PortfolioSnapshot Snapshot,
        string Label,
        IReadOnlyList<PortfolioSnapshotFactoryEpicInput> EpicInputs);
}
