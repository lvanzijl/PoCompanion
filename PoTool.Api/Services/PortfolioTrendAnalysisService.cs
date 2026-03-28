using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public interface IPortfolioTrendAnalysisService
{
    PortfolioTrendDto BuildTrend(
        IReadOnlyList<PortfolioSnapshotGroupSelection> snapshots,
        IReadOnlyDictionary<int, string> productNames,
        FilterContext filter,
        PortfolioReadQueryOptions? options);
}

public sealed class PortfolioTrendAnalysisService : IPortfolioTrendAnalysisService
{
    private readonly IProductAggregationService _productAggregationService;
    private readonly IPortfolioReadModelMapper _mapper;

    public PortfolioTrendAnalysisService(
        IProductAggregationService productAggregationService,
        IPortfolioReadModelMapper mapper)
    {
        _productAggregationService = productAggregationService;
        _mapper = mapper;
    }

    public PortfolioTrendDto BuildTrend(
        IReadOnlyList<PortfolioSnapshotGroupSelection> snapshots,
        IReadOnlyDictionary<int, string> productNames,
        FilterContext filter,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(productNames);
        ArgumentNullException.ThrowIfNull(filter);

        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        if (snapshots.Count == 0)
        {
            return new PortfolioTrendDto
            {
                Snapshots = Array.Empty<PortfolioHistoricalSnapshotDto>(),
                PortfolioProgressTrend = EmptyMetric(),
                TotalWeightTrend = EmptyMetric(),
                Projects = Array.Empty<PortfolioScopedTrendDto>(),
                WorkPackages = Array.Empty<PortfolioScopedTrendDto>(),
                SnapshotCount = effectiveOptions.SnapshotCount,
                RangeStartUtc = effectiveOptions.RangeStartUtc,
                RangeEndUtc = effectiveOptions.RangeEndUtc,
                IncludesArchivedSnapshots = effectiveOptions.IncludeArchivedSnapshots,
                ArchivedSnapshotsExcludedByDefault = true,
                ArchivedSnapshotsExcludedNotice = false,
                HasData = false,
                Filter = PortfolioFilterResolutionService.EmptyMetadata()
            };
        }

        var orderedSnapshots = snapshots
            .OrderByDescending(snapshot => snapshot.Snapshot.Timestamp)
            .ThenByDescending(snapshot => snapshot.SnapshotId)
            .ToArray();

        var snapshotDtos = orderedSnapshots
            .Select(snapshot => new PortfolioHistoricalSnapshotDto
            {
                SnapshotId = snapshot.SnapshotId,
                SnapshotLabel = snapshot.Source,
                Timestamp = snapshot.Snapshot.Timestamp,
                IncludesArchivedSnapshot = snapshot.IncludesArchivedSnapshot
            })
            .ToArray();

        var projectSeries = BuildScopedTrends(
            orderedSnapshots,
            productNames,
            item => new PortfolioScopeKey(item.ProductId, item.ProjectNumber, null),
            includeWorkPackages: false);
        var workPackageSeries = BuildScopedTrends(
            orderedSnapshots,
            productNames,
            item => item.WorkPackage is null
                ? null
                : new PortfolioScopeKey(item.ProductId, item.ProjectNumber, item.WorkPackage),
            includeWorkPackages: true);

        return new PortfolioTrendDto
        {
            Snapshots = snapshotDtos,
            PortfolioProgressTrend = BuildPortfolioMetric(orderedSnapshots, useProgress: true),
            TotalWeightTrend = BuildPortfolioMetric(orderedSnapshots, useProgress: false),
            Projects = PortfolioReadModelFiltering.Apply(projectSeries, filter, effectiveOptions),
            WorkPackages = PortfolioReadModelFiltering.Apply(workPackageSeries, filter, effectiveOptions),
            SnapshotCount = effectiveOptions.SnapshotCount,
            RangeStartUtc = effectiveOptions.RangeStartUtc,
            RangeEndUtc = effectiveOptions.RangeEndUtc,
            IncludesArchivedSnapshots = effectiveOptions.IncludeArchivedSnapshots,
            ArchivedSnapshotsExcludedByDefault = true,
            ArchivedSnapshotsExcludedNotice = false,
            HasData = true,
            Filter = PortfolioFilterResolutionService.EmptyMetadata()
        };
    }

    private IReadOnlyList<PortfolioScopedTrendDto> BuildScopedTrends(
        IReadOnlyList<PortfolioSnapshotGroupSelection> snapshots,
        IReadOnlyDictionary<int, string> productNames,
        Func<PortfolioSnapshotItem, PortfolioScopeKey?> keySelector,
        bool includeWorkPackages)
    {
        var allKeys = snapshots
            .SelectMany(snapshot => snapshot.Snapshot.Items)
            .Select(keySelector)
            .Where(key => key is not null)
            .Distinct()
            .Cast<PortfolioScopeKey>()
            .OrderBy(key => key.ProductId)
            .ThenBy(key => key.ProjectNumber, StringComparer.Ordinal)
            .ThenBy(key => key.WorkPackage ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var results = new List<PortfolioScopedTrendDto>(allKeys.Length);
        foreach (var key in allKeys)
        {
            if (!productNames.TryGetValue(key.ProductId, out var productName) || string.IsNullOrWhiteSpace(productName))
            {
                throw new InvalidOperationException($"Product name mapping is missing for ProductId {key.ProductId}.");
            }

            var progressPoints = new List<PortfolioTrendPointDto>(snapshots.Count);
            var weightPoints = new List<PortfolioTrendPointDto>(snapshots.Count);
            var lifecyclePoints = new List<PortfolioLifecycleState?>(snapshots.Count);

            foreach (var snapshot in snapshots)
            {
                var matchingItems = snapshot.Snapshot.Items
                    .Where(item => item.ProductId == key.ProductId
                                   && string.Equals(item.ProjectNumber, key.ProjectNumber, StringComparison.Ordinal)
                                   && string.Equals(item.WorkPackage, key.WorkPackage, StringComparison.Ordinal))
                    .ToArray();

                lifecyclePoints.Add(GetLifecycleState(matchingItems));

                var activeItems = matchingItems
                    .Where(item => item.LifecycleState == WorkPackageLifecycleState.Active)
                    .ToArray();

                double? progress = null;
                double? weight = null;
                if (includeWorkPackages)
                {
                    var activeItem = activeItems.SingleOrDefault();
                    progress = activeItem?.Progress;
                    weight = activeItem?.TotalWeight;
                }
                else if (activeItems.Length > 0)
                {
                    var aggregation = _productAggregationService.Compute(new ProductAggregationRequest(
                        activeItems
                            .Select(item => new ProductAggregationEpicInput(
                                item.Progress * 100d,
                                EpicForecastConsumed: null,
                                EpicForecastRemaining: null,
                                item.TotalWeight,
                                IsExcluded: item.TotalWeight <= 0d))
                            .ToList()));
                    progress = aggregation.ProductProgress / 100d;
                    weight = aggregation.TotalWeight;
                }

                progressPoints.Add(CreatePoint(snapshot, progress));
                weightPoints.Add(CreatePoint(snapshot, weight));
            }

            results.Add(new PortfolioScopedTrendDto
            {
                ProductId = key.ProductId,
                ProductName = productName,
                ProjectNumber = key.ProjectNumber,
                WorkPackage = key.WorkPackage,
                CurrentLifecycleState = lifecyclePoints.FirstOrDefault(),
                PreviousLifecycleState = lifecyclePoints.Skip(1).FirstOrDefault(),
                ProgressTrend = BuildMetric(progressPoints),
                WeightTrend = BuildMetric(weightPoints)
            });
        }

        return results;
    }

    private PortfolioMetricTrendDto BuildPortfolioMetric(
        IReadOnlyList<PortfolioSnapshotGroupSelection> snapshots,
        bool useProgress)
    {
        var points = snapshots
            .Select(snapshot =>
            {
                var aggregation = _productAggregationService.Compute(new ProductAggregationRequest(
                    snapshot.Snapshot.Items
                        .Where(item => item.LifecycleState == WorkPackageLifecycleState.Active)
                        .Select(item => new ProductAggregationEpicInput(
                            item.Progress * 100d,
                            EpicForecastConsumed: null,
                            EpicForecastRemaining: null,
                            item.TotalWeight,
                            IsExcluded: item.TotalWeight <= 0d))
                        .ToList()));

                var value = useProgress
                    ? aggregation.ProductProgress / 100d
                    : aggregation.TotalWeight;
                return CreatePoint(snapshot, value);
            })
            .ToArray();

        return BuildMetric(points);
    }

    private static PortfolioTrendPointDto CreatePoint(PortfolioSnapshotGroupSelection snapshot, double? value)
        => new()
        {
            SnapshotId = snapshot.SnapshotId,
            SnapshotLabel = snapshot.Source,
            Timestamp = snapshot.Snapshot.Timestamp,
            Value = value,
            IncludesArchivedSnapshot = snapshot.IncludesArchivedSnapshot
        };

    private static PortfolioMetricTrendDto BuildMetric(IEnumerable<PortfolioTrendPointDto> points)
    {
        var orderedPoints = points.ToArray();
        var current = orderedPoints.FirstOrDefault();
        var previous = orderedPoints.Skip(1).FirstOrDefault();
        double? delta = current?.Value.HasValue == true && previous?.Value.HasValue == true
            ? current.Value!.Value - previous.Value!.Value
            : null;

        return new PortfolioMetricTrendDto
        {
            CurrentValue = current?.Value,
            PreviousValue = previous?.Value,
            Delta = delta,
            Direction = delta.HasValue ? GetDirection(delta.Value) : null,
            Points = orderedPoints
        };
    }

    private static PortfolioMetricTrendDto EmptyMetric()
        => new()
        {
            CurrentValue = null,
            PreviousValue = null,
            Delta = null,
            Direction = null,
            Points = Array.Empty<PortfolioTrendPointDto>()
        };

    private PortfolioLifecycleState? GetLifecycleState(IReadOnlyList<PortfolioSnapshotItem> items)
    {
        if (items.Count == 0)
        {
            return null;
        }

        if (items.Any(item => item.LifecycleState == WorkPackageLifecycleState.Active))
        {
            return _mapper.ToLifecycleState(WorkPackageLifecycleState.Active);
        }

        return _mapper.ToLifecycleState(WorkPackageLifecycleState.Retired);
    }

    private static PortfolioTrendDirection GetDirection(double delta)
        => delta switch
        {
            > 0d => PortfolioTrendDirection.Increasing,
            < 0d => PortfolioTrendDirection.Decreasing,
            _ => PortfolioTrendDirection.Stable
        };

    private sealed record PortfolioScopeKey(
        int ProductId,
        string ProjectNumber,
        string? WorkPackage);
}
