using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Planning.Queries;

/// <summary>
/// Query to retrieve the complete Planning Board state for a Product Owner.
/// </summary>
/// <param name="ProductOwnerId">The Product Owner ID.</param>
public sealed record GetPlanningBoardQuery(int ProductOwnerId) : IQuery<PlanningBoardDto>;
