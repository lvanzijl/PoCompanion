using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve work items starting from configured Goals.
/// </summary>
public sealed record GetGoalHierarchyQuery(
    List<int> GoalIds
) : IQuery<IEnumerable<WorkItemDto>>;
