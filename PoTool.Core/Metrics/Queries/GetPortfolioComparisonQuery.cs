using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to read the comparison between the latest two available portfolio snapshots.
/// </summary>
public sealed record GetPortfolioComparisonQuery(
    int ProductOwnerId,
    PortfolioReadQueryOptions? Options = null) : IQuery<PortfolioComparisonDto>;
