using Mediator;
using PoTool.Core.Delivery.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query for compact Home product bar metrics.
/// </summary>
public sealed record GetHomeProductBarMetricsQuery(
    int ProductOwnerId,
    DeliveryEffectiveFilter EffectiveFilter
) : IQuery<HomeProductBarMetricsDto>;
