using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve all Goals (work items of type Goal).
/// </summary>
public sealed record GetAllGoalsQuery : IQuery<IEnumerable<WorkItemDto>>;
