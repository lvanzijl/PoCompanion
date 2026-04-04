using PoTool.Client.Helpers;

namespace PoTool.Client.ApiClient;

public partial interface IPipelinesClient
{
    Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>> GetMetricsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>> GetRunsForProductsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<PipelineInsightsDto>> GetInsightsEnvelopeAsync(
        int? productOwnerId,
        int? sprintId,
        bool? includePartiallySucceeded,
        bool? includeCanceled,
        CancellationToken cancellationToken);
}

public partial class PipelinesClient
{
    public async Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>> GetMetricsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        var response = await GetMetricsAsync(productIds, fromDate, toDate, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetMetricsEnvelopeAsync));
    }

    public async Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>> GetRunsForProductsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        var response = await GetRunsForProductsAsync(productIds, fromDate, toDate, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetRunsForProductsEnvelopeAsync));
    }

    public async Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<PipelineInsightsDto>> GetInsightsEnvelopeAsync(
        int? productOwnerId,
        int? sprintId,
        bool? includePartiallySucceeded,
        bool? includeCanceled,
        CancellationToken cancellationToken)
    {
        var response = await GetInsightsAsync(productOwnerId, sprintId, includePartiallySucceeded, includeCanceled, cancellationToken);
        return response.ToCacheBackedResult().RequireData(nameof(GetInsightsEnvelopeAsync));
    }
}
