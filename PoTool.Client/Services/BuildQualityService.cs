using PoTool.Client.ApiClient;

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

    public async Task<BuildQualityPageDto> GetRollingWindowAsync(
        int productOwnerId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var response = await _buildQualityClient.GetRollingEnvelopeAsync(productOwnerId, windowStartUtc, windowEndUtc, cancellationToken);
        return response.Data;
    }

    public async Task<DeliveryBuildQualityDto> GetSprintAsync(
        int productOwnerId,
        int sprintId,
        CancellationToken cancellationToken = default)
    {
        var response = await _buildQualityClient.GetSprintEnvelopeAsync(productOwnerId, sprintId, cancellationToken);
        return response.Data;
    }

    public async Task<PipelineBuildQualityDto> GetPipelineAsync(
        int productOwnerId,
        int sprintId,
        int? pipelineDefinitionId = null,
        int? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        return await _buildQualityClient.GetPipelineAsync(
            productOwnerId,
            sprintId,
            pipelineDefinitionId,
            repositoryId,
            cancellationToken);
    }
}
