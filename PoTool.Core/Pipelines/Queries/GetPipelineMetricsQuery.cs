using Mediator;
using PoTool.Core.Pipelines.Filters;

using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to retrieve aggregated metrics for pipelines using the resolved canonical filter.
/// </summary>
public sealed record GetPipelineMetricsQuery(PipelineEffectiveFilter EffectiveFilter) : IQuery<IEnumerable<PipelineMetricsDto>>;
