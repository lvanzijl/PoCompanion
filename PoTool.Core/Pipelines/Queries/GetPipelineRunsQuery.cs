using Mediator;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to retrieve pipeline runs for a specific pipeline.
/// </summary>
public sealed record GetPipelineRunsQuery(int PipelineId, int Top = 100) : IQuery<IEnumerable<PipelineRunDto>>;
