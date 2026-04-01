using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get persisted planning projections for roadmap epics in one product.
/// </summary>
public sealed record GetProductPlanningProjectionsQuery(int ProductId) : IQuery<IReadOnlyList<PlanningEpicProjectionDto>?>;
