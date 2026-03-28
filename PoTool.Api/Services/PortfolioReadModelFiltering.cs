using PoTool.Core.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

internal static class PortfolioReadModelFiltering
{
    public static IReadOnlyList<PortfolioSnapshotItemDto> Apply(
        IEnumerable<PortfolioSnapshotItemDto> items,
        FilterContext filter,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(filter);

        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        var filtered = items.Where(item =>
                MatchesValue(filter.ProductIds, item.ProductId)
                && MatchesString(filter.ProjectNumbers, item.ProjectNumber)
                && MatchesString(filter.WorkPackages, item.WorkPackage)
                && MatchesValue(filter.LifecycleStates, item.LifecycleState))
            .ToList();

        return Order(filtered, effectiveOptions).ToList();
    }

    public static IReadOnlyList<PortfolioComparisonItemDto> Apply(
        IEnumerable<PortfolioComparisonItemDto> items,
        FilterContext filter,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(filter);

        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        var filtered = items.Where(item =>
                MatchesValue(filter.ProductIds, item.ProductId)
                && MatchesString(filter.ProjectNumbers, item.ProjectNumber)
                && MatchesString(filter.WorkPackages, item.WorkPackage)
                && MatchesAny(filter.LifecycleStates, item.CurrentLifecycleState, item.PreviousLifecycleState))
            .ToList();

        return Order(filtered, effectiveOptions).ToList();
    }

    public static IReadOnlyList<PortfolioScopedTrendDto> Apply(
        IEnumerable<PortfolioScopedTrendDto> items,
        FilterContext filter,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(filter);

        var effectiveOptions = options ?? new PortfolioReadQueryOptions();
        var filtered = items.Where(item =>
                MatchesValue(filter.ProductIds, item.ProductId)
                && MatchesString(filter.ProjectNumbers, item.ProjectNumber)
                && MatchesString(filter.WorkPackages, item.WorkPackage)
                && MatchesAny(filter.LifecycleStates, item.CurrentLifecycleState, item.PreviousLifecycleState))
            .ToList();

        return Order(filtered, effectiveOptions).ToList();
    }

    public static IReadOnlyList<PortfolioDecisionSignalDto> Apply(
        IEnumerable<PortfolioDecisionSignalDto> items,
        FilterContext filter,
        PortfolioReadQueryOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(filter);

        return items.Where(item =>
                MatchesNullableValue(filter.ProductIds, item.ProductId)
                && MatchesString(filter.ProjectNumbers, item.ProjectNumber)
                && MatchesString(filter.WorkPackages, item.WorkPackage)
                && MatchesNullableValue(filter.LifecycleStates, item.LifecycleState))
            .OrderBy(item => item.Type)
            .ThenBy(item => item.ProductId ?? int.MinValue)
            .ThenBy(item => item.ProjectNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.SnapshotTimestamp ?? DateTimeOffset.MinValue)
            .ToList();
    }

    private static bool MatchesValue<T>(FilterSelection<T> selection, T value)
    {
        if (selection.IsAll)
        {
            return true;
        }

        return selection.Values.Contains(value);
    }

    private static bool MatchesNullableValue<T>(FilterSelection<T> selection, T? value)
        where T : struct
    {
        if (selection.IsAll)
        {
            return true;
        }

        return value.HasValue && selection.Values.Contains(value.Value);
    }

    private static bool MatchesString(FilterSelection<string> selection, string? value)
    {
        if (selection.IsAll)
        {
            return true;
        }

        var selectedValues = selection.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !string.IsNullOrWhiteSpace(value)
            && selectedValues.Contains(value.Trim());
    }

    private static bool MatchesAny<T>(FilterSelection<T> selection, params T?[] candidateValues)
        where T : struct
    {
        if (selection.IsAll)
        {
            return true;
        }

        return candidateValues
            .Where(candidateValue => candidateValue.HasValue)
            .Select(candidateValue => candidateValue!.Value)
            .Any(candidate => selection.Values.Contains(candidate));
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
