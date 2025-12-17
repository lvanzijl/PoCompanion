using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve filtered work items.
/// </summary>
public sealed record GetFilteredWorkItemsQuery(string Filter) : IQuery<IEnumerable<WorkItemDto>>;
