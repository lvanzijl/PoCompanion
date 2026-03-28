using Mediator;
using PoTool.Core.Delivery.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to get an aggregated portfolio delivery snapshot across products for a sprint range.
/// Returns composition and distribution data — no time-series information.
/// </summary>
public record GetPortfolioDeliveryQuery(
    DeliveryEffectiveFilter EffectiveFilter
) : IQuery<PortfolioDeliveryDto>;
