using Mediator;
using PoTool.Core.Pipelines.Filters;
using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to retrieve all pipeline runs for the resolved canonical pipeline scope.
/// </summary>
public sealed record GetPipelineRunsForProductsQuery(PipelineEffectiveFilter EffectiveFilter) : IQuery<IEnumerable<PipelineRunDto>>;
