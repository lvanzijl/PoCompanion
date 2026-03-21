using Mediator;
using PoTool.Shared.BuildQuality;

namespace PoTool.Core.BuildQuality.Queries;

/// <summary>
/// Retrieves sprint-window BuildQuality for Delivery consumers.
/// </summary>
public sealed record GetBuildQualitySprintQuery(
    int ProductOwnerId,
    int SprintId) : IQuery<DeliveryBuildQualityDto>;
