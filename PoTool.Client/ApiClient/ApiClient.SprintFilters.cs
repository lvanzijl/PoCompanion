using PoTool.Shared.Metrics;
using PoTool.Client.Helpers;

namespace PoTool.Client.ApiClient;

public partial interface IMetricsClient
{
    Task<SprintQueryResponseDto<SprintMetricsDto>> GetSprintMetricsEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<BacklogHealthDto>> GetBacklogHealthEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<MultiIterationBacklogHealthDto>> GetMultiIterationBacklogHealthEnvelopeAsync(
        int? productOwnerId,
        IEnumerable<int>? productIds,
        string? areaPath,
        int? maxIterations,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<SprintCapacityPlanDto>> GetSprintCapacityPlanEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        int? defaultCapacity,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<GetSprintTrendMetricsResponse>> GetSprintTrendMetricsEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        bool? recompute,
        bool? includeDetails,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<SprintExecutionDto>> GetSprintExecutionEnvelopeAsync(
        int productOwnerId,
        int sprintId,
        int? productId,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<WorkItemActivityDetailsDto>> GetWorkItemActivityDetailsEnvelopeAsync(
        int workItemId,
        int productOwnerId,
        int? sprintId,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        CancellationToken cancellationToken);
}

public partial class MetricsClient
{
    public async Task<SprintQueryResponseDto<SprintMetricsDto>> GetSprintMetricsEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken)
    {
        var response = await GetSprintMetricsAsync(iterationPath, productOwnerId, productIds, sprintId, cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetSprintMetricsEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<BacklogHealthDto>> GetBacklogHealthEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken)
    {
        var response = await GetBacklogHealthAsync(iterationPath, productOwnerId, productIds, sprintId, cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetBacklogHealthEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<MultiIterationBacklogHealthDto>> GetMultiIterationBacklogHealthEnvelopeAsync(
        int? productOwnerId,
        IEnumerable<int>? productIds,
        string? areaPath,
        int? maxIterations,
        CancellationToken cancellationToken)
    {
        var response = await GetMultiIterationBacklogHealthAsync(productOwnerId, productIds, areaPath, maxIterations, cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetMultiIterationBacklogHealthEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<SprintCapacityPlanDto>> GetSprintCapacityPlanEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        int? defaultCapacity,
        CancellationToken cancellationToken)
    {
        var response = await GetSprintCapacityPlanAsync(iterationPath, productOwnerId, productIds, sprintId, defaultCapacity, cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetSprintCapacityPlanEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<GetSprintTrendMetricsResponse>> GetSprintTrendMetricsEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        bool? recompute,
        bool? includeDetails,
        CancellationToken cancellationToken)
    {
        var response = await GetSprintTrendMetricsAsync(productOwnerId, sprintIds, productIds, recompute, includeDetails, cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetSprintTrendMetricsEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<SprintExecutionDto>> GetSprintExecutionEnvelopeAsync(
        int productOwnerId,
        int sprintId,
        int? productId,
        CancellationToken cancellationToken)
    {
        var response = await GetSprintExecutionAsync(productOwnerId, sprintId, productId, cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetSprintExecutionEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<WorkItemActivityDetailsDto>> GetWorkItemActivityDetailsEnvelopeAsync(
        int workItemId,
        int productOwnerId,
        int? sprintId,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        CancellationToken cancellationToken)
    {
        var response = await GetWorkItemActivityDetailsAsync(workItemId, productOwnerId, sprintId, periodStartUtc, periodEndUtc, cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetWorkItemActivityDetailsEnvelopeAsync));
    }
}
