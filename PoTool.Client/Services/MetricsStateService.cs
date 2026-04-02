using System.Net.Http.Json;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

public sealed class MetricsStateService
{
    private readonly HttpClient _httpClient;

    public MetricsStateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<DataStateResponseDto<SprintQueryResponseDto<MultiIterationBacklogHealthDto>>?> GetMultiIterationBacklogHealthStateAsync(
        int? productOwnerId,
        IEnumerable<int>? productIds,
        string? areaPath,
        int maxIterations,
        CancellationToken cancellationToken = default)
        => GetAsync<SprintQueryResponseDto<MultiIterationBacklogHealthDto>>(
            BuildUrl(
                "/api/metrics/multi-iteration-backlog-health",
                ("productOwnerId", productOwnerId),
                ("productIds", productIds),
                ("areaPath", areaPath),
                ("maxIterations", maxIterations)),
            cancellationToken);

    public Task<DataStateResponseDto<EffortDistributionDto>?> GetEffortDistributionStateAsync(
        string? areaPathFilter,
        int maxIterations,
        int? defaultCapacity,
        CancellationToken cancellationToken = default)
        => GetAsync<EffortDistributionDto>(
            BuildUrl(
                "/api/metrics/effort-distribution",
                ("areaPathFilter", areaPathFilter),
                ("maxIterations", maxIterations),
                ("defaultCapacity", defaultCapacity)),
            cancellationToken);

    public Task<DataStateResponseDto<EpicCompletionForecastDto>?> GetEpicForecastStateAsync(
        int epicId,
        int maxSprintsForVelocity,
        CancellationToken cancellationToken = default)
        => GetAsync<EpicCompletionForecastDto>(
            BuildUrl($"/api/metrics/epic-forecast/{epicId}", ("maxSprintsForVelocity", maxSprintsForVelocity)),
            cancellationToken);

    public Task<DataStateResponseDto<DeliveryQueryResponseDto<CapacityCalibrationDto>>?> GetCapacityCalibrationStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken = default)
        => GetAsync<DeliveryQueryResponseDto<CapacityCalibrationDto>>(
            BuildUrl(
                "/api/metrics/capacity-calibration",
                ("productOwnerId", productOwnerId),
                ("sprintIds", sprintIds),
                ("productIds", productIds)),
            cancellationToken);

    public Task<DataStateResponseDto<DeliveryQueryResponseDto<PortfolioDeliveryDto>>?> GetPortfolioDeliveryStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken = default)
        => GetAsync<DeliveryQueryResponseDto<PortfolioDeliveryDto>>(
            BuildUrl(
                "/api/metrics/portfolio-delivery",
                ("productOwnerId", productOwnerId),
                ("sprintIds", sprintIds),
                ("productIds", productIds)),
            cancellationToken);

    public Task<DataStateResponseDto<SprintQueryResponseDto<GetSprintTrendMetricsResponse>>?> GetSprintTrendMetricsStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        int? productId,
        int? teamId,
        CancellationToken cancellationToken = default)
        => GetAsync<SprintQueryResponseDto<GetSprintTrendMetricsResponse>>(
            BuildUrl(
                "/api/metrics/sprint-trend-metrics",
                ("productOwnerId", productOwnerId),
                ("sprintIds", sprintIds),
                ("productId", productId),
                ("teamId", teamId)),
            cancellationToken);

    public Task<DataStateResponseDto<SprintQueryResponseDto<SprintExecutionDto>>?> GetSprintExecutionStateAsync(
        int productOwnerId,
        int sprintId,
        int? productId,
        CancellationToken cancellationToken = default)
        => GetAsync<SprintQueryResponseDto<SprintExecutionDto>>(
            BuildUrl(
                "/api/metrics/sprint-execution",
                ("productOwnerId", productOwnerId),
                ("sprintId", sprintId),
                ("productId", productId)),
            cancellationToken);

    public Task<DataStateResponseDto<PortfolioProgressDto>?> GetPortfolioProgressStateAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default)
        => GetAsync<PortfolioProgressDto>(
            BuildUrl(
                "/api/portfolio/progress",
                ("productOwnerId", productOwnerId),
                ("productId", productId),
                ("projectNumber", projectNumber),
                ("workPackage", workPackage),
                ("lifecycleState", lifecycleState),
                ("sortBy", sortBy),
                ("sortDirection", sortDirection),
                ("groupBy", groupBy)),
            cancellationToken);

    public Task<DataStateResponseDto<PortfolioSnapshotDto>?> GetPortfolioSnapshotsStateAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default)
        => GetAsync<PortfolioSnapshotDto>(
            BuildUrl(
                "/api/portfolio/snapshots",
                ("productOwnerId", productOwnerId),
                ("productId", productId),
                ("projectNumber", projectNumber),
                ("workPackage", workPackage),
                ("lifecycleState", lifecycleState),
                ("sortBy", sortBy),
                ("sortDirection", sortDirection),
                ("groupBy", groupBy)),
            cancellationToken);

    public Task<DataStateResponseDto<PortfolioComparisonDto>?> GetPortfolioComparisonStateAsync(
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
        => GetAsync<PortfolioComparisonDto>(
            BuildUrl(
                "/api/portfolio/comparison",
                ("productOwnerId", productOwnerId),
                ("productId", productId),
                ("projectNumber", projectNumber),
                ("workPackage", workPackage),
                ("lifecycleState", lifecycleState),
                ("sortBy", sortBy),
                ("sortDirection", sortDirection),
                ("groupBy", groupBy),
                ("rangeStartUtc", rangeStartUtc),
                ("rangeEndUtc", rangeEndUtc),
                ("includeArchivedSnapshots", includeArchivedSnapshots),
                ("compareToSnapshotId", compareToSnapshotId)),
            cancellationToken);

    public Task<DataStateResponseDto<PortfolioTrendDto>?> GetPortfolioTrendsStateAsync(
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
        => GetAsync<PortfolioTrendDto>(
            BuildUrl(
                "/api/portfolio/trends",
                ("productOwnerId", productOwnerId),
                ("productId", productId),
                ("projectNumber", projectNumber),
                ("workPackage", workPackage),
                ("lifecycleState", lifecycleState),
                ("sortBy", sortBy),
                ("sortDirection", sortDirection),
                ("groupBy", groupBy),
                ("snapshotCount", snapshotCount),
                ("rangeStartUtc", rangeStartUtc),
                ("rangeEndUtc", rangeEndUtc),
                ("includeArchivedSnapshots", includeArchivedSnapshots)),
            cancellationToken);

    public Task<DataStateResponseDto<PortfolioSignalsDto>?> GetPortfolioSignalsStateAsync(
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
        => GetAsync<PortfolioSignalsDto>(
            BuildUrl(
                "/api/portfolio/signals",
                ("productOwnerId", productOwnerId),
                ("productId", productId),
                ("projectNumber", projectNumber),
                ("workPackage", workPackage),
                ("lifecycleState", lifecycleState),
                ("sortBy", sortBy),
                ("sortDirection", sortDirection),
                ("groupBy", groupBy),
                ("snapshotCount", snapshotCount),
                ("rangeStartUtc", rangeStartUtc),
                ("rangeEndUtc", rangeEndUtc),
                ("includeArchivedSnapshots", includeArchivedSnapshots),
                ("compareToSnapshotId", compareToSnapshotId)),
            cancellationToken);

    public async Task<DataStateResponseDto<SprintQueryResponseDto<WorkItemActivityDetailsDto>>?> GetWorkItemActivityDetailsStateAsync(
        int workItemId,
        int productOwnerId,
        int? sprintId,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        var queryParts = new List<string> { $"productOwnerId={productOwnerId}" };
        if (sprintId.HasValue)
        {
            queryParts.Add($"sprintId={sprintId.Value}");
        }

        if (periodStartUtc.HasValue)
        {
            queryParts.Add($"periodStartUtc={Uri.EscapeDataString(periodStartUtc.Value.ToString("O"))}");
        }

        if (periodEndUtc.HasValue)
        {
            queryParts.Add($"periodEndUtc={Uri.EscapeDataString(periodEndUtc.Value.ToString("O"))}");
        }

        return await _httpClient.GetFromJsonAsync<DataStateResponseDto<SprintQueryResponseDto<WorkItemActivityDetailsDto>>>(
            $"/api/metrics/work-item-activity/{workItemId}?{string.Join("&", queryParts)}",
            cancellationToken);
    }

    public async Task<DataStateResponseDto<DeliveryQueryResponseDto<PortfolioProgressTrendDto>>?> GetPortfolioProgressTrendStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken = default)
    {
        var queryParts = new List<string> { $"productOwnerId={productOwnerId}" };
        queryParts.AddRange(sprintIds.Select(sprintId => $"sprintIds={sprintId}"));

        if (productIds != null)
        {
            queryParts.AddRange(productIds.Select(productId => $"productIds={productId}"));
        }

        return await _httpClient.GetFromJsonAsync<DataStateResponseDto<DeliveryQueryResponseDto<PortfolioProgressTrendDto>>>(
            $"/api/metrics/portfolio-progress-trend?{string.Join("&", queryParts)}",
            cancellationToken);
    }

    private Task<DataStateResponseDto<T>?> GetAsync<T>(string url, CancellationToken cancellationToken)
        => _httpClient.GetFromJsonAsync<DataStateResponseDto<T>>(url, cancellationToken);

    private static string BuildUrl(string path, params (string Key, object? Value)[] parameters)
    {
        var query = new List<string>();

        foreach (var (key, value) in parameters)
        {
            if (value is null)
            {
                continue;
            }

            if (value is string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(text)}");
                }

                continue;
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(dateTimeOffset.ToString("O"))}");
                continue;
            }

            if (value is IEnumerable<int> intValues && value is not string)
            {
                query.AddRange(intValues.Select(item => $"{Uri.EscapeDataString(key)}={item}"));
                continue;
            }

            query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.ToString()!)}");
        }

        return query.Count == 0 ? path : $"{path}?{string.Join("&", query)}";
    }
}
