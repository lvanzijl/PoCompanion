using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to read the latest available portfolio snapshot progress summary.
/// </summary>
public sealed record GetPortfolioProgressQuery(
    int ProductOwnerId,
    PortfolioReadQueryOptions? Options = null) : IQuery<PortfolioProgressDto>;
