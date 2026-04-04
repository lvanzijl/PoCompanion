using System.Globalization;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.BuildQuality;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Typed client service for BuildQuality API consumption.
/// </summary>
public sealed class BuildQualityService : IBuildQualityService
{
    private readonly HttpClient _httpClient;

    public BuildQualityService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CacheBackedClientResult<CanonicalClientResponse<BuildQualityPageDto>>> GetRollingWindowAsync(
        int productOwnerId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"api/buildquality/rolling?productOwnerId={productOwnerId}" +
                       $"&windowStartUtc={Uri.EscapeDataString(windowStartUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}" +
                       $"&windowEndUtc={Uri.EscapeDataString(windowEndUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}";

        var response = await DataStateHttpClientHelper.GetDataStateAsync<DeliveryQueryResponseDto<BuildQualityPageDto>>(
            _httpClient,
            endpoint,
            cancellationToken);
        return response.Map(CanonicalClientResponseFactory.Create);
    }

    public async Task<CacheBackedClientResult<CanonicalClientResponse<DeliveryBuildQualityDto>>> GetSprintAsync(
        int productOwnerId,
        int sprintId,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"api/buildquality/sprint?productOwnerId={productOwnerId}&sprintId={sprintId}";
        var response = await DataStateHttpClientHelper.GetDataStateAsync<DeliveryQueryResponseDto<DeliveryBuildQualityDto>>(
            _httpClient,
            endpoint,
            cancellationToken);
        return response.Map(CanonicalClientResponseFactory.Create);
    }

    public Task<CacheBackedClientResult<PipelineBuildQualityDto>> GetPipelineAsync(
        int productOwnerId,
        int sprintId,
        int? pipelineDefinitionId = null,
        int? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"productOwnerId={productOwnerId}",
            $"sprintId={sprintId}"
        };

        if (pipelineDefinitionId.HasValue)
        {
            query.Add($"pipelineDefinitionId={pipelineDefinitionId.Value}");
        }

        if (repositoryId.HasValue)
        {
            query.Add($"repositoryId={repositoryId.Value}");
        }

        return DataStateHttpClientHelper.GetDataStateAsync<PipelineBuildQualityDto>(
            _httpClient,
            $"api/buildquality/pipeline?{string.Join("&", query)}",
            cancellationToken);
    }
}
