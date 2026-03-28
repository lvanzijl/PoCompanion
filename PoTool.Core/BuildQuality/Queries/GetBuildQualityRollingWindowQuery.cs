using Mediator;
using PoTool.Core.Delivery.Filters;
using PoTool.Shared.BuildQuality;

namespace PoTool.Core.BuildQuality.Queries;

/// <summary>
/// Retrieves rolling-window BuildQuality for the selected product owner scope.
/// </summary>
public sealed record GetBuildQualityRollingWindowQuery(
    int ProductOwnerId,
    DeliveryEffectiveFilter EffectiveFilter) : IQuery<BuildQualityPageDto>;
