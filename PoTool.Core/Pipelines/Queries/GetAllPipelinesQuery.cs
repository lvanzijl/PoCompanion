using Mediator;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to retrieve all cached pipelines.
/// </summary>
public sealed record GetAllPipelinesQuery : IQuery<IEnumerable<PipelineDto>>;
