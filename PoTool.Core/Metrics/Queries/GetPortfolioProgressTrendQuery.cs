using Mediator;
using PoTool.Core.Delivery.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to compute portfolio progress trend metrics across a selected sprint range.
/// </summary>
public sealed record GetPortfolioProgressTrendQuery(
    DeliveryEffectiveFilter EffectiveFilter
) : IQuery<PortfolioProgressTrendDto>;
