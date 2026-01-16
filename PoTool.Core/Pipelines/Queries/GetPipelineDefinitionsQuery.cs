using Mediator;
using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to retrieve pipeline definitions.
/// Supports filtering by product or repository.
/// </summary>
public sealed record GetPipelineDefinitionsQuery(
    int? ProductId = null,
    int? RepositoryId = null
) : IQuery<IEnumerable<PipelineDefinitionDto>>;
