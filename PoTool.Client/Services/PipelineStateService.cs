using System.Net.Http.Json;
using PoTool.Shared.DataState;
using PoTool.Shared.Pipelines;

namespace PoTool.Client.Services;

public sealed class PipelineStateService
{
    private readonly HttpClient _httpClient;

    public PipelineStateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<DataStateResponseDto<PipelineQueryResponseDto<PipelineInsightsDto>>?> GetInsightsStateAsync(
        int productOwnerId,
        int sprintId,
        bool includePartiallySucceeded,
        bool includeCanceled,
        CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DataStateResponseDto<PipelineQueryResponseDto<PipelineInsightsDto>>>(
            $"/api/pipelines/insights?productOwnerId={productOwnerId}&sprintId={sprintId}&includePartiallySucceeded={includePartiallySucceeded}&includeCanceled={includeCanceled}",
            cancellationToken);
}
