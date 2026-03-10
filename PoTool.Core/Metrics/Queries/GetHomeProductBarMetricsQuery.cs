using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query for compact Home product bar metrics.
/// </summary>
public sealed record GetHomeProductBarMetricsQuery(
    int ProductOwnerId,
    int? ProductId = null
) : IQuery<HomeProductBarMetricsDto>;
