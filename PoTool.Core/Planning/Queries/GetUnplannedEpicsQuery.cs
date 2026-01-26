using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Queries;

/// <summary>
/// Query to retrieve unplanned epics for a Product Owner.
/// </summary>
/// <param name="ProductOwnerId">The Product Owner ID.</param>
/// <param name="ProductId">Optional: filter to a specific product.</param>
public sealed record GetUnplannedEpicsQuery(
    int ProductOwnerId,
    int? ProductId = null) : IQuery<IReadOnlyList<UnplannedEpicDto>>;
