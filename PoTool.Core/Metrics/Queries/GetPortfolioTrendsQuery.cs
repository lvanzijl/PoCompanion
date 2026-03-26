using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to read persisted multi-snapshot portfolio trend history.
/// </summary>
public sealed record GetPortfolioTrendsQuery(
    int ProductOwnerId,
    PortfolioReadQueryOptions? Options = null) : IQuery<PortfolioTrendDto>;
