using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public interface IPortfolioReadModelStateService
{
    Task<PortfolioReadModelState?> GetLatestStateAsync(int productOwnerId, CancellationToken cancellationToken);

    Task<PortfolioReadModelHistoryState?> GetHistoryStateAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken);

    Task<PortfolioReadModelComparisonState?> GetComparisonStateAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken);
}

public sealed record PortfolioReadModelState(
    PortfolioSnapshot CurrentSnapshot,
    string CurrentSnapshotLabel,
    PortfolioSnapshot? PreviousSnapshot,
    string? PreviousSnapshotLabel,
    double? PortfolioProgress,
    double TotalWeight,
    IReadOnlyDictionary<int, string> ProductNames);

public sealed record PortfolioReadModelHistoryState(
    IReadOnlyList<PortfolioSnapshotGroupSelection> Snapshots,
    IReadOnlyDictionary<int, string> ProductNames,
    bool ArchivedSnapshotsExcludedNotice);

public sealed record PortfolioReadModelComparisonState(
    PortfolioSnapshotGroupSelection CurrentSnapshot,
    PortfolioSnapshotGroupSelection? ComparisonSnapshot,
    IReadOnlyDictionary<int, string> ProductNames,
    bool ArchivedSnapshotsExcludedNotice);

public sealed class PortfolioReadModelStateService : IPortfolioReadModelStateService
{
    private readonly PoToolDbContext _context;
    private readonly IPortfolioSnapshotSelectionService _selectionService;
    private readonly IProductAggregationService _productAggregationService;

    public PortfolioReadModelStateService(
        PoToolDbContext context,
        IPortfolioSnapshotSelectionService selectionService,
        IProductAggregationService productAggregationService)
    {
        _context = context;
        _selectionService = selectionService;
        _productAggregationService = productAggregationService;
    }

    public async Task<PortfolioReadModelState?> GetLatestStateAsync(int productOwnerId, CancellationToken cancellationToken)
    {
        var portfolioContext = await LoadPortfolioContextAsync(productOwnerId, cancellationToken);
        if (portfolioContext is null)
        {
            return null;
        }

        var current = await _selectionService.GetLatestPortfolioSnapshotAsync(portfolioContext.ProductIds, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var previous = await _selectionService.GetPreviousPortfolioSnapshotAsync(portfolioContext.ProductIds, cancellationToken);

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
            portfolioContext.ProductNames);
    }

    public async Task<PortfolioReadModelHistoryState?> GetHistoryStateAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        var portfolioContext = await LoadPortfolioContextAsync(productOwnerId, cancellationToken);
        if (portfolioContext is null)
        {
            return null;
        }

        var snapshots = await _selectionService.GetPortfolioSnapshotsAsync(
            portfolioContext.ProductIds,
            effectiveOptions.SnapshotCount,
            effectiveOptions.RangeStartUtc,
            effectiveOptions.RangeEndUtc,
            cancellationToken,
            effectiveOptions.IncludeArchivedSnapshots);

        if (snapshots.Count == 0)
        {
            return null;
        }

        var archivedExcludedNotice = !effectiveOptions.IncludeArchivedSnapshots
            && await _selectionService.HasArchivedPortfolioSnapshotsAsync(
                portfolioContext.ProductIds,
                effectiveOptions.RangeStartUtc,
                effectiveOptions.RangeEndUtc,
                cancellationToken);

        return new PortfolioReadModelHistoryState(
            snapshots,
            portfolioContext.ProductNames,
            archivedExcludedNotice);
    }

    public async Task<PortfolioReadModelComparisonState?> GetComparisonStateAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        var portfolioContext = await LoadPortfolioContextAsync(productOwnerId, cancellationToken);
        if (portfolioContext is null)
        {
            return null;
        }

        var current = (await _selectionService.GetPortfolioSnapshotsAsync(
                portfolioContext.ProductIds,
                count: 1,
                effectiveOptions.RangeStartUtc,
                effectiveOptions.RangeEndUtc,
                cancellationToken,
                effectiveOptions.IncludeArchivedSnapshots))
            .FirstOrDefault();
        if (current is null)
        {
            return null;
        }

        PortfolioSnapshotGroupSelection? comparison;
        if (effectiveOptions.CompareToSnapshotId.HasValue)
        {
            comparison = await _selectionService.GetPortfolioSnapshotByIdAsync(
                portfolioContext.ProductIds,
                effectiveOptions.CompareToSnapshotId.Value,
                cancellationToken,
                effectiveOptions.IncludeArchivedSnapshots);

            if (comparison is not null && !IsWithinRange(comparison.Snapshot.Timestamp, effectiveOptions.RangeStartUtc, effectiveOptions.RangeEndUtc))
            {
                comparison = null;
            }
        }
        else
        {
            comparison = (await _selectionService.GetPortfolioSnapshotsAsync(
                    portfolioContext.ProductIds,
                    count: 2,
                    effectiveOptions.RangeStartUtc,
                    effectiveOptions.RangeEndUtc,
                    cancellationToken,
                    effectiveOptions.IncludeArchivedSnapshots))
                .Skip(1)
                .FirstOrDefault();
        }

        var archivedExcludedNotice = !effectiveOptions.IncludeArchivedSnapshots
            && await _selectionService.HasArchivedPortfolioSnapshotsAsync(
                portfolioContext.ProductIds,
                effectiveOptions.RangeStartUtc,
                effectiveOptions.RangeEndUtc,
                cancellationToken);

        return new PortfolioReadModelComparisonState(
            current,
            comparison,
            portfolioContext.ProductNames,
            archivedExcludedNotice);
    }

    private async Task<PortfolioOwnerContext?> LoadPortfolioContextAsync(int productOwnerId, CancellationToken cancellationToken)
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
        return new PortfolioOwnerContext(productNames.Keys.OrderBy(productId => productId).ToArray(), productNames);
    }

    private static bool IsWithinRange(
        DateTimeOffset timestamp,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc)
        => (!rangeStartUtc.HasValue || timestamp >= rangeStartUtc.Value)
           && (!rangeEndUtc.HasValue || timestamp <= rangeEndUtc.Value);

    private sealed record PortfolioOwnerContext(
        IReadOnlyList<int> ProductIds,
        IReadOnlyDictionary<int, string> ProductNames);
}
