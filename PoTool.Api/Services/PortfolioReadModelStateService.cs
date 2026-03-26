using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

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
    private readonly IPortfolioSnapshotCaptureDataService _captureDataService;
    private readonly IPortfolioSnapshotFactory _portfolioSnapshotFactory;
    private readonly IPortfolioSnapshotPersistenceService _persistenceService;
    private readonly IPortfolioSnapshotSelectionService _selectionService;
    private readonly IProductAggregationService _productAggregationService;

    public PortfolioReadModelStateService(
        PoToolDbContext context,
        IPortfolioSnapshotCaptureDataService captureDataService,
        IPortfolioSnapshotFactory portfolioSnapshotFactory,
        IPortfolioSnapshotPersistenceService persistenceService,
        IPortfolioSnapshotSelectionService selectionService,
        IProductAggregationService productAggregationService)
    {
        _context = context;
        _captureDataService = captureDataService;
        _portfolioSnapshotFactory = portfolioSnapshotFactory;
        _persistenceService = persistenceService;
        _selectionService = selectionService;
        _productAggregationService = productAggregationService;
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

        await EnsureLatestSourcesPersistedAsync(productOwnerId, productIds, cancellationToken);

        var current = await _selectionService.GetLatestPortfolioSnapshotAsync(productIds, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var previous = await _selectionService.GetPreviousPortfolioSnapshotAsync(productIds, cancellationToken);

        var portfolioAggregation = _productAggregationService.Compute(new ProductAggregationRequest(
            current.Snapshot.Items
                .Where(item => item.LifecycleState == WorkPackageLifecycleState.Active)
                .Select(item => new ProductAggregationEpicInput(
                    item.Progress * 100d,
                    EpicForecastConsumed: null,
                    EpicForecastRemaining: null,
                    item.TotalWeight,
                    IsExcluded: item.TotalWeight <= 0d))
                .ToList()));

        return new PortfolioReadModelState(
            current.Snapshot,
            current.Source,
            previous?.Snapshot,
            previous?.Source,
            portfolioAggregation.ProductProgress,
            portfolioAggregation.TotalWeight,
            productNames);
    }

    private async Task EnsureLatestSourcesPersistedAsync(
        int productOwnerId,
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken)
    {
        var snapshotSources = await _captureDataService.GetLatestSourcesAsync(productIds, cancellationToken);

        foreach (var source in snapshotSources
                     .OrderBy(snapshotSource => snapshotSource.EndDateUtc)
                     .ThenBy(snapshotSource => snapshotSource.SprintId))
        {
            var inputsByProduct = await _captureDataService.BuildSnapshotInputsByProductAsync(productOwnerId, source, cancellationToken);

            foreach (var productGroup in inputsByProduct.OrderBy(group => group.Key))
            {
                var existing = await _persistenceService.GetBySourceAsync(
                    productGroup.Key,
                    source.Source,
                    source.Timestamp,
                    cancellationToken);
                if (existing is not null)
                {
                    continue;
                }

                var previousSnapshot = await _selectionService.GetLatestBeforeAsync(
                    productGroup.Key,
                    source.Timestamp,
                    cancellationToken,
                    includeArchived: true);

                var snapshot = _portfolioSnapshotFactory.Create(new PortfolioSnapshotFactoryRequest(
                    source.Timestamp,
                    productGroup.Value,
                    previousSnapshot?.Snapshot));

                await _persistenceService.PersistAsync(
                    productGroup.Key,
                    source.Source,
                    createdBy: null,
                    snapshot,
                    cancellationToken);
            }
        }
    }
}
