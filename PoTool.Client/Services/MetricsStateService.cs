using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

public sealed class MetricsStateService
{
    private readonly IMetricsClient _metricsClient;

    public MetricsStateService(IMetricsClient metricsClient)
    {
        _metricsClient = metricsClient;
    }

    public Task<DataStateResponseDto<SprintQueryResponseDto<MultiIterationBacklogHealthDto>>?> GetMultiIterationBacklogHealthStateAsync(
        int? productOwnerId,
        IEnumerable<int>? productIds,
        string? areaPath,
        int maxIterations,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfSprintQueryResponseDtoOfMultiIterationBacklogHealthDto, SprintQueryResponseDtoOfMultiIterationBacklogHealthDto, SprintQueryResponseDto<MultiIterationBacklogHealthDto>>(
            _metricsClient.GetMultiIterationBacklogHealthAsync(productOwnerId, productIds, areaPath, maxIterations, cancellationToken),
            static data => data.ToShared());

    public Task<DataStateResponseDto<EffortDistributionDto>?> GetEffortDistributionStateAsync(
        string? areaPathFilter,
        int maxIterations,
        int? defaultCapacity,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfEffortDistributionDto, EffortDistributionDto>(
            _metricsClient.GetEffortDistributionAsync(areaPathFilter, maxIterations, defaultCapacity, cancellationToken));

    public Task<DataStateResponseDto<EpicCompletionForecastDto>?> GetEpicForecastStateAsync(
        int epicId,
        int maxSprintsForVelocity,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfEpicCompletionForecastDto, EpicCompletionForecastDto>(
            _metricsClient.GetEpicForecastAsync(epicId, maxSprintsForVelocity, cancellationToken));

    public Task<DataStateResponseDto<DeliveryQueryResponseDto<CapacityCalibrationDto>>?> GetCapacityCalibrationStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfDeliveryQueryResponseDtoOfCapacityCalibrationDto, DeliveryQueryResponseDtoOfCapacityCalibrationDto, DeliveryQueryResponseDto<CapacityCalibrationDto>>(
            _metricsClient.GetCapacityCalibrationAsync(productOwnerId, sprintIds, productIds, cancellationToken),
            static data => data.ToShared());

    public Task<DataStateResponseDto<DeliveryQueryResponseDto<PortfolioDeliveryDto>>?> GetPortfolioDeliveryStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioDeliveryDto, DeliveryQueryResponseDtoOfPortfolioDeliveryDto, DeliveryQueryResponseDto<PortfolioDeliveryDto>>(
            _metricsClient.GetPortfolioDeliveryAsync(productOwnerId, sprintIds, productIds, cancellationToken),
            static data => data.ToShared());

    public Task<DataStateResponseDto<SprintQueryResponseDto<GetSprintTrendMetricsResponse>>?> GetSprintTrendMetricsStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        int? productId,
        int? teamId,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfSprintQueryResponseDtoOfGetSprintTrendMetricsResponse, SprintQueryResponseDtoOfGetSprintTrendMetricsResponse, SprintQueryResponseDto<GetSprintTrendMetricsResponse>>(
            _metricsClient.GetSprintTrendMetricsAsync(
                productOwnerId,
                sprintIds,
                productId.HasValue ? [productId.Value] : null,
                recompute: null,
                includeDetails: null,
                cancellationToken),
            static data => data.ToShared());

    public Task<DataStateResponseDto<SprintQueryResponseDto<SprintExecutionDto>>?> GetSprintExecutionStateAsync(
        int productOwnerId,
        int sprintId,
        int? productId,
        CancellationToken cancellationToken = default)
        => GetAsync<DataStateResponseDtoOfSprintQueryResponseDtoOfSprintExecutionDto, SprintQueryResponseDtoOfSprintExecutionDto, SprintQueryResponseDto<SprintExecutionDto>>(
            _metricsClient.GetSprintExecutionAsync(productOwnerId, sprintId, productId, cancellationToken),
            static data => data.ToShared());

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
        => GetAsync<DataStateResponseDtoOfPortfolioProgressDto, PortfolioProgressDto>(
            _metricsClient.GetPortfolioProgressAsync(
                productOwnerId,
                productId,
                projectNumber,
                workPackage,
                lifecycleState,
                (PortfolioReadSortBy?)sortBy,
                (PortfolioReadSortDirection?)sortDirection,
                (PortfolioReadGroupBy?)groupBy,
                cancellationToken));

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
        => GetAsync<DataStateResponseDtoOfPortfolioSnapshotDto, PortfolioSnapshotDto>(
            _metricsClient.GetPortfolioSnapshotsAsync(
                productOwnerId,
                productId,
                projectNumber,
                workPackage,
                lifecycleState,
                (PortfolioReadSortBy?)sortBy,
                (PortfolioReadSortDirection?)sortDirection,
                (PortfolioReadGroupBy?)groupBy,
                cancellationToken));

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
        => GetAsync<DataStateResponseDtoOfPortfolioComparisonDto, PortfolioComparisonDto>(
            _metricsClient.GetPortfolioComparisonAsync(
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
                cancellationToken));

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
        => GetAsync<DataStateResponseDtoOfPortfolioTrendDto, PortfolioTrendDto>(
            _metricsClient.GetPortfolioTrendsAsync(
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
                cancellationToken));

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
        => GetAsync<DataStateResponseDtoOfPortfolioSignalsDto, PortfolioSignalsDto>(
            _metricsClient.GetPortfolioSignalsAsync(
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
                cancellationToken));

    public async Task<DataStateResponseDto<SprintQueryResponseDto<WorkItemActivityDetailsDto>>?> GetWorkItemActivityDetailsStateAsync(
        int workItemId,
        int productOwnerId,
        int? sprintId,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<DataStateResponseDtoOfSprintQueryResponseDtoOfWorkItemActivityDetailsDto, SprintQueryResponseDtoOfWorkItemActivityDetailsDto, SprintQueryResponseDto<WorkItemActivityDetailsDto>>(
            _metricsClient.GetWorkItemActivityDetailsAsync(
                workItemId,
                productOwnerId,
                sprintId,
                periodStartUtc,
                periodEndUtc,
                cancellationToken),
            static data => data.ToShared());
    }

    public async Task<DataStateResponseDto<DeliveryQueryResponseDto<PortfolioProgressTrendDto>>?> GetPortfolioProgressTrendStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<DataStateResponseDtoOfDeliveryQueryResponseDtoOfPortfolioProgressTrendDto, DeliveryQueryResponseDtoOfPortfolioProgressTrendDto, DeliveryQueryResponseDto<PortfolioProgressTrendDto>>(
            _metricsClient.GetPortfolioProgressTrendAsync(productOwnerId, sprintIds, productIds, cancellationToken),
            static data => data.ToShared());
    }

    private static async Task<DataStateResponseDto<TData>?> GetAsync<TEnvelope, TData>(Task<TEnvelope> requestTask)
        where TEnvelope : class, IGeneratedDataStateEnvelope<TData>
    {
        return GeneratedCacheEnvelopeHelper.ToDataStateResponse(await requestTask.ConfigureAwait(false));
    }

    private static async Task<DataStateResponseDto<TMapped>?> GetAsync<TEnvelope, TSource, TMapped>(
        Task<TEnvelope> requestTask,
        Func<TSource, TMapped?> mapper)
        where TEnvelope : class, IGeneratedDataStateEnvelope<TSource>
    {
        return GeneratedCacheEnvelopeHelper.ToDataStateResponse(
            await requestTask.ConfigureAwait(false),
            mapper);
    }
}
