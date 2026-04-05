using System.Globalization;
using PoTool.Client.ApiClient;
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
    private readonly IBuildQualityClient _buildQualityClient;
    private readonly HttpClient _httpClient;

    public BuildQualityService(IBuildQualityClient buildQualityClient, HttpClient httpClient)
    {
        _buildQualityClient = buildQualityClient;
        _httpClient = httpClient;
    }

    public async Task<CacheBackedClientResult<CanonicalClientResponse<BuildQualityPageDto>>> GetRollingWindowAsync(
        int productOwnerId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var response = await DataStateHttpClientHelper.GetDataStateAsync<DeliveryQueryResponseDto<BuildQualityPageDto>>(
            _httpClient,
            $"/api/buildquality/rolling?productOwnerId={productOwnerId}&windowStartUtc={FormatDateTime(windowStartUtc)}&windowEndUtc={FormatDateTime(windowEndUtc)}",
            cancellationToken);

        return response.Map(CanonicalClientResponseFactory.Create);
    }

    public async Task<CacheBackedClientResult<CanonicalClientResponse<DeliveryBuildQualityDto>>> GetSprintAsync(
        int productOwnerId,
        int sprintId,
        CancellationToken cancellationToken = default)
    {
        var response = await DataStateHttpClientHelper.GetDataStateAsync<DeliveryQueryResponseDto<DeliveryBuildQualityDto>>(
            _httpClient,
            $"/api/buildquality/sprint?productOwnerId={productOwnerId}&sprintId={sprintId}",
            cancellationToken);

        return response.Map(CanonicalClientResponseFactory.Create);
    }

    public async Task<CacheBackedClientResult<PipelineBuildQualityDto>> GetPipelineAsync(
        int productOwnerId,
        int sprintId,
        int? pipelineDefinitionId = null,
        int? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return GeneratedCacheEnvelopeHelper.ToCacheBackedResult<PipelineBuildQualityDto>(
                await _buildQualityClient.GetPipelineAsync(productOwnerId, sprintId, pipelineDefinitionId, repositoryId, cancellationToken));
        }
        catch (ApiException ex)
        {
            return CacheBackedClientResult<PipelineBuildQualityDto>.Unavailable(
                GeneratedClientErrorTranslator.ToHttpRequestException(ex).Message);
        }
    }

    private static string FormatDateTime(DateTimeOffset value)
        => Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));
}
