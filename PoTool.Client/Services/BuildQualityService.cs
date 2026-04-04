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

    public BuildQualityService(IBuildQualityClient buildQualityClient)
    {
        _buildQualityClient = buildQualityClient;
    }

    public async Task<CacheBackedClientResult<CanonicalClientResponse<BuildQualityPageDto>>> GetRollingWindowAsync(
        int productOwnerId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = (await _buildQualityClient.GetRollingAsync(productOwnerId, windowStartUtc, windowEndUtc, cancellationToken))
                .ToCacheBackedResult();
            return response.Map(CanonicalClientResponseFactory.Create);
        }
        catch (ApiException ex)
        {
            return CacheBackedClientResult<CanonicalClientResponse<BuildQualityPageDto>>.Unavailable(
                GeneratedClientErrorTranslator.ToHttpRequestException(ex).Message);
        }
    }

    public async Task<CacheBackedClientResult<CanonicalClientResponse<DeliveryBuildQualityDto>>> GetSprintAsync(
        int productOwnerId,
        int sprintId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = (await _buildQualityClient.GetSprintAsync(productOwnerId, sprintId, cancellationToken))
                .ToCacheBackedResult();
            return response.Map(CanonicalClientResponseFactory.Create);
        }
        catch (ApiException ex)
        {
            return CacheBackedClientResult<CanonicalClientResponse<DeliveryBuildQualityDto>>.Unavailable(
                GeneratedClientErrorTranslator.ToHttpRequestException(ex).Message);
        }
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
}
