using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Shared.DataState;
using PoTool.Shared.Pipelines;

namespace PoTool.Client.Services;

public sealed class PipelineStateService
{
    private readonly IPipelinesClient _pipelinesClient;

    public PipelineStateService(IPipelinesClient pipelinesClient)
    {
        _pipelinesClient = pipelinesClient;
    }

    public async Task<DataStateResponseDto<PipelineQueryResponseDto<PipelineInsightsDto>>?> GetInsightsStateAsync(
        int productOwnerId,
        int sprintId,
        IEnumerable<int>? productIds,
        bool includePartiallySucceeded,
        bool includeCanceled,
        CancellationToken cancellationToken = default)
        => (await _pipelinesClient.GetInsightsAsync(productOwnerId, sprintId, productIds, includePartiallySucceeded, includeCanceled, cancellationToken))
            .ToDataStateResponse();
}
