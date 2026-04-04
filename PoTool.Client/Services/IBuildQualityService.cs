using PoTool.Client.Models;
using PoTool.Shared.BuildQuality;

namespace PoTool.Client.Services;

/// <summary>
/// Typed client abstraction for BuildQuality API consumption.
/// </summary>
public interface IBuildQualityService
{
    Task<CacheBackedClientResult<CanonicalClientResponse<BuildQualityPageDto>>> GetRollingWindowAsync(
        int productOwnerId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default);

    Task<CacheBackedClientResult<CanonicalClientResponse<DeliveryBuildQualityDto>>> GetSprintAsync(
        int productOwnerId,
        int sprintId,
        CancellationToken cancellationToken = default);

    Task<CacheBackedClientResult<PipelineBuildQualityDto>> GetPipelineAsync(
        int productOwnerId,
        int sprintId,
        int? pipelineDefinitionId = null,
        int? repositoryId = null,
        CancellationToken cancellationToken = default);
}
