using PoTool.Client.ApiClient;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Typed client abstraction for BuildQuality API consumption.
/// </summary>
public interface IBuildQualityService
{
    Task<CanonicalClientResponse<BuildQualityPageDto>> GetRollingWindowAsync(
        int productOwnerId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default);

    Task<CanonicalClientResponse<DeliveryBuildQualityDto>> GetSprintAsync(
        int productOwnerId,
        int sprintId,
        CancellationToken cancellationToken = default);

    Task<PipelineBuildQualityDto> GetPipelineAsync(
        int productOwnerId,
        int sprintId,
        int? pipelineDefinitionId = null,
        int? repositoryId = null,
        CancellationToken cancellationToken = default);
}
