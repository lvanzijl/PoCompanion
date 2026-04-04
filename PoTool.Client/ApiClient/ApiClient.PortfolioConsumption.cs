using PoTool.Shared.Metrics;
using PoTool.Client.Helpers;

namespace PoTool.Client.ApiClient;

/// <summary>
/// Extends IMetricsClient with the read-only portfolio consumption endpoints.
/// Handcrafted extension because the checked-in swagger client is not regenerated automatically during development.
/// </summary>
public partial interface IMetricsClient
{
    Task<PortfolioProgressDto> GetPortfolioProgressAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default);

    Task<PortfolioSnapshotDto> GetPortfolioSnapshotsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default);

    Task<PortfolioComparisonDto> GetPortfolioComparisonAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken = default);

    Task<PortfolioTrendDto> GetPortfolioTrendsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        CancellationToken cancellationToken = default);

    Task<PortfolioSignalsDto> GetPortfolioSignalsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Partial MetricsClient implementation for the read-only portfolio consumption endpoints.
/// </summary>
public partial class MetricsClient
{
    public async Task<PortfolioProgressDto> GetPortfolioProgressAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default)
    {
        var response = await GetPortfolioProgressAsync(
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            (PortfolioReadSortBy?)sortBy,
            (PortfolioReadSortDirection?)sortDirection,
            (PortfolioReadGroupBy?)groupBy,
            cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetPortfolioProgressAsync));
    }

    public async Task<PortfolioSnapshotDto> GetPortfolioSnapshotsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default)
    {
        var response = await GetPortfolioSnapshotsAsync(
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            (PortfolioReadSortBy?)sortBy,
            (PortfolioReadSortDirection?)sortDirection,
            (PortfolioReadGroupBy?)groupBy,
            cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetPortfolioSnapshotsAsync));
    }

    public async Task<PortfolioComparisonDto> GetPortfolioComparisonAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetPortfolioComparisonAsync(
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            (PortfolioReadSortBy?)sortBy,
            (PortfolioReadSortDirection?)sortDirection,
            (PortfolioReadGroupBy?)groupBy,
            rangeStartUtc,
            rangeEndUtc,
            includeArchivedSnapshots,
            compareToSnapshotId,
            cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetPortfolioComparisonAsync));
    }

    public async Task<PortfolioTrendDto> GetPortfolioTrendsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        CancellationToken cancellationToken = default)
    {
        var response = await GetPortfolioTrendsAsync(
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            (PortfolioReadSortBy?)sortBy,
            (PortfolioReadSortDirection?)sortDirection,
            (PortfolioReadGroupBy?)groupBy,
            snapshotCount,
            rangeStartUtc,
            rangeEndUtc,
            includeArchivedSnapshots,
            cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetPortfolioTrendsAsync));
    }

    public async Task<PortfolioSignalsDto> GetPortfolioSignalsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetPortfolioSignalsAsync(
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            (PortfolioReadSortBy?)sortBy,
            (PortfolioReadSortDirection?)sortDirection,
            (PortfolioReadGroupBy?)groupBy,
            snapshotCount,
            rangeStartUtc,
            rangeEndUtc,
            includeArchivedSnapshots,
            compareToSnapshotId,
            cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetPortfolioSignalsAsync));
    }
}
