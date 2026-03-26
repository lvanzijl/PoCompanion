using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

internal static class PortfolioReadModelFiltering
{
    public static IReadOnlyList<PortfolioSnapshotItemDto> Apply(
        IEnumerable<PortfolioSnapshotItemDto> items,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);

        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        var filtered = items.Where(item =>
                (!effectiveOptions.ProductId.HasValue || item.ProductId == effectiveOptions.ProductId.Value)
                && (string.IsNullOrWhiteSpace(effectiveOptions.ProjectNumber)
                    || string.Equals(item.ProjectNumber, effectiveOptions.ProjectNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(effectiveOptions.WorkPackage)
                    || string.Equals(item.WorkPackage, effectiveOptions.WorkPackage.Trim(), StringComparison.OrdinalIgnoreCase))
                && (!effectiveOptions.LifecycleState.HasValue || item.LifecycleState == effectiveOptions.LifecycleState.Value))
            .ToList();

        return Order(filtered, effectiveOptions).ToList();
    }

    public static IReadOnlyList<PortfolioComparisonItemDto> Apply(
        IEnumerable<PortfolioComparisonItemDto> items,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);

        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        var filtered = items.Where(item =>
                (!effectiveOptions.ProductId.HasValue || item.ProductId == effectiveOptions.ProductId.Value)
                && (string.IsNullOrWhiteSpace(effectiveOptions.ProjectNumber)
                    || string.Equals(item.ProjectNumber, effectiveOptions.ProjectNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(effectiveOptions.WorkPackage)
                    || string.Equals(item.WorkPackage, effectiveOptions.WorkPackage.Trim(), StringComparison.OrdinalIgnoreCase))
                && (!effectiveOptions.LifecycleState.HasValue
                    || item.CurrentLifecycleState == effectiveOptions.LifecycleState.Value
                    || item.PreviousLifecycleState == effectiveOptions.LifecycleState.Value))
            .ToList();

        return Order(filtered, effectiveOptions).ToList();
    }

    public static IReadOnlyList<PortfolioScopedTrendDto> Apply(
        IEnumerable<PortfolioScopedTrendDto> items,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);

        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        var filtered = items.Where(item =>
                (!effectiveOptions.ProductId.HasValue || item.ProductId == effectiveOptions.ProductId.Value)
                && (string.IsNullOrWhiteSpace(effectiveOptions.ProjectNumber)
                    || string.Equals(item.ProjectNumber, effectiveOptions.ProjectNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(effectiveOptions.WorkPackage)
                    || string.Equals(item.WorkPackage, effectiveOptions.WorkPackage.Trim(), StringComparison.OrdinalIgnoreCase))
                && (!effectiveOptions.LifecycleState.HasValue
                    || item.CurrentLifecycleState == effectiveOptions.LifecycleState.Value
                    || item.PreviousLifecycleState == effectiveOptions.LifecycleState.Value))
            .ToList();

        return Order(filtered, effectiveOptions).ToList();
    }

    public static IReadOnlyList<PortfolioDecisionSignalDto> Apply(
        IEnumerable<PortfolioDecisionSignalDto> items,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);

        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        return items.Where(item =>
                (!effectiveOptions.ProductId.HasValue || item.ProductId == effectiveOptions.ProductId.Value)
                && (string.IsNullOrWhiteSpace(effectiveOptions.ProjectNumber)
                    || string.Equals(item.ProjectNumber, effectiveOptions.ProjectNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(effectiveOptions.WorkPackage)
                    || string.Equals(item.WorkPackage, effectiveOptions.WorkPackage.Trim(), StringComparison.OrdinalIgnoreCase))
                && (!effectiveOptions.LifecycleState.HasValue || item.LifecycleState == effectiveOptions.LifecycleState.Value))
            .OrderBy(item => item.Type)
            .ThenBy(item => item.ProductId ?? int.MinValue)
            .ThenBy(item => item.ProjectNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.SnapshotTimestamp ?? DateTimeOffset.MinValue)
            .ToList();
    }

    private static IOrderedEnumerable<PortfolioSnapshotItemDto> Order(
        IEnumerable<PortfolioSnapshotItemDto> items,
        PortfolioReadQueryOptions options)
    {
        var primary = options.GroupBy switch
        {
            PortfolioReadGroupBy.Product => items.OrderBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase),
            PortfolioReadGroupBy.Project => items.OrderBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase),
            PortfolioReadGroupBy.WorkPackage => items.OrderBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => options.SortBy switch
            {
                PortfolioReadSortBy.Progress => ApplyDirection(items, item => item.Progress, options.SortDirection),
                PortfolioReadSortBy.Weight => ApplyDirection(items, item => item.Weight, options.SortDirection),
                _ => items.OrderBy(item => item.ProductId)
            }
        };

        return (options.GroupBy, options.SortBy) switch
        {
            (PortfolioReadGroupBy.None, _) => primary
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (_, PortfolioReadSortBy.Progress) => ApplyDirection(primary, item => item.Progress, options.SortDirection)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (_, PortfolioReadSortBy.Weight) => ApplyDirection(primary, item => item.Weight, options.SortDirection)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => primary
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IOrderedEnumerable<PortfolioComparisonItemDto> Order(
        IEnumerable<PortfolioComparisonItemDto> items,
        PortfolioReadQueryOptions options)
    {
        var primary = options.GroupBy switch
        {
            PortfolioReadGroupBy.Product => items.OrderBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase),
            PortfolioReadGroupBy.Project => items.OrderBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase),
            PortfolioReadGroupBy.WorkPackage => items.OrderBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => options.SortBy switch
            {
                PortfolioReadSortBy.Progress => ApplyDirection(items, item => item.CurrentProgress ?? item.PreviousProgress ?? double.MinValue, options.SortDirection),
                PortfolioReadSortBy.Weight => ApplyDirection(items, item => item.CurrentWeight ?? item.PreviousWeight ?? double.MinValue, options.SortDirection),
                PortfolioReadSortBy.Delta => ApplyDirection(items, item => item.ProgressDelta ?? double.MinValue, options.SortDirection),
                _ => items.OrderBy(item => item.ProductId)
            }
        };

        return (options.GroupBy, options.SortBy) switch
        {
            (PortfolioReadGroupBy.None, _) => primary
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (_, PortfolioReadSortBy.Progress) => ApplyDirection(primary, item => item.CurrentProgress ?? item.PreviousProgress ?? double.MinValue, options.SortDirection)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (_, PortfolioReadSortBy.Weight) => ApplyDirection(primary, item => item.CurrentWeight ?? item.PreviousWeight ?? double.MinValue, options.SortDirection)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (_, PortfolioReadSortBy.Delta) => ApplyDirection(primary, item => item.ProgressDelta ?? double.MinValue, options.SortDirection)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => primary
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IOrderedEnumerable<PortfolioScopedTrendDto> Order(
        IEnumerable<PortfolioScopedTrendDto> items,
        PortfolioReadQueryOptions options)
    {
        var primary = options.GroupBy switch
        {
            PortfolioReadGroupBy.Product => items.OrderBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase),
            PortfolioReadGroupBy.Project => items.OrderBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase),
            PortfolioReadGroupBy.WorkPackage => items.OrderBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => options.SortBy switch
            {
                PortfolioReadSortBy.Progress => ApplyDirection(items, item => item.ProgressTrend.CurrentValue ?? double.MinValue, options.SortDirection),
                PortfolioReadSortBy.Weight => ApplyDirection(items, item => item.WeightTrend.CurrentValue ?? double.MinValue, options.SortDirection),
                PortfolioReadSortBy.Delta => ApplyDirection(items, item => item.ProgressTrend.Delta ?? double.MinValue, options.SortDirection),
                _ => items.OrderBy(item => item.ProductId)
            }
        };

        return (options.GroupBy, options.SortBy) switch
        {
            (PortfolioReadGroupBy.None, _) => primary
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (_, PortfolioReadSortBy.Progress) => ApplyDirection(primary, item => item.ProgressTrend.CurrentValue ?? double.MinValue, options.SortDirection)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (_, PortfolioReadSortBy.Weight) => ApplyDirection(primary, item => item.WeightTrend.CurrentValue ?? double.MinValue, options.SortDirection)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            (_, PortfolioReadSortBy.Delta) => ApplyDirection(primary, item => item.ProgressTrend.Delta ?? double.MinValue, options.SortDirection)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => primary
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IOrderedEnumerable<TItem> ApplyDirection<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, double> selector,
        PortfolioReadSortDirection direction)
        => direction == PortfolioReadSortDirection.Asc
            ? items.OrderBy(selector)
            : items.OrderByDescending(selector);

    private static IOrderedEnumerable<TItem> ApplyDirection<TItem>(
        IOrderedEnumerable<TItem> items,
        Func<TItem, double> selector,
        PortfolioReadSortDirection direction)
        => direction == PortfolioReadSortDirection.Asc
            ? items.ThenBy(selector)
            : items.ThenByDescending(selector);
}
