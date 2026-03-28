using Mediator;
using PoTool.Core.Delivery.Filters;
using PoTool.Shared.BuildQuality;

namespace PoTool.Core.BuildQuality.Queries;

/// <summary>
/// Retrieves sprint-window BuildQuality for Delivery consumers.
/// </summary>
public sealed record GetBuildQualitySprintQuery(
    int ProductOwnerId,
    DeliveryEffectiveFilter EffectiveFilter) : IQuery<DeliveryBuildQualityDto>;
