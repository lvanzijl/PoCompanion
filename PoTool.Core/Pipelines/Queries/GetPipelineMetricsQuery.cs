using Mediator;

using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to retrieve aggregated metrics for all pipelines.
/// </summary>
public sealed record GetPipelineMetricsQuery : IQuery<IEnumerable<PipelineMetricsDto>>;
