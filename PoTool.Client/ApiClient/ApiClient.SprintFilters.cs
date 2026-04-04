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
        return response.ToCacheBackedResult().RequireData(nameof(GetSprintMetricsEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<BacklogHealthDto>> GetBacklogHealthEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken)
    {
        var response = await GetBacklogHealthAsync(iterationPath, productOwnerId, productIds, sprintId, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetBacklogHealthEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<MultiIterationBacklogHealthDto>> GetMultiIterationBacklogHealthEnvelopeAsync(
        int? productOwnerId,
        IEnumerable<int>? productIds,
        string? areaPath,
        int? maxIterations,
        CancellationToken cancellationToken)
    {
        var response = await GetMultiIterationBacklogHealthAsync(productOwnerId, productIds, areaPath, maxIterations, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetMultiIterationBacklogHealthEnvelopeAsync));
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
        return response.ToCacheBackedResult().RequireData(nameof(GetSprintCapacityPlanEnvelopeAsync));
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
        return response.ToCacheBackedResult().RequireData(nameof(GetSprintTrendMetricsEnvelopeAsync));
    }

    public async Task<SprintQueryResponseDto<SprintExecutionDto>> GetSprintExecutionEnvelopeAsync(
        int productOwnerId,
        int sprintId,
        int? productId,
        CancellationToken cancellationToken)
    {
        var response = await GetSprintExecutionAsync(productOwnerId, sprintId, productId, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetSprintExecutionEnvelopeAsync));
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
        return response.ToCacheBackedResult().RequireData(nameof(GetWorkItemActivityDetailsEnvelopeAsync));
    }
}
