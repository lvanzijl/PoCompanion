using Mediator;
using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to retrieve all pipeline runs for specific products.
/// Returns runs from the last 6 months for main branch by default.
/// </summary>
public sealed record GetPipelineRunsForProductsQuery(List<int>? ProductIds) : IQuery<IEnumerable<PipelineRunDto>>;
