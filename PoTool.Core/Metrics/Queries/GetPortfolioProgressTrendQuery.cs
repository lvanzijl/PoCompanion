using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to compute portfolio progress trend metrics across a selected sprint range.
/// </summary>
public sealed record GetPortfolioProgressTrendQuery(
    int ProductOwnerId,
    IReadOnlyList<int> SprintIds
) : IQuery<PortfolioProgressTrendDto>;
