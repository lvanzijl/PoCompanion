using Mediator;
using PoTool.Shared.BuildQuality;

namespace PoTool.Core.BuildQuality.Queries;

/// <summary>
/// Retrieves pipeline- or repository-scoped BuildQuality detail for a sprint window.
/// </summary>
public sealed record GetBuildQualityPipelineDetailQuery(
    int ProductOwnerId,
    int SprintId,
    int? PipelineDefinitionId = null,
    int? RepositoryId = null) : IQuery<PipelineBuildQualityDto>;
