using Mediator;

using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines.Queries;

/// <summary>
/// Query to retrieve aggregated metrics for pipelines, optionally filtered by products.
/// </summary>
/// <param name="ProductIds">Optional list of product IDs to filter by</param>
public sealed record GetPipelineMetricsQuery(List<int>? ProductIds = null) : IQuery<IEnumerable<PipelineMetricsDto>>;
