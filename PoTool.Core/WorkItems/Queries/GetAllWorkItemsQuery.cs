using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve all cached work items.
/// </summary>
public sealed record GetAllWorkItemsQuery : IQuery<IEnumerable<WorkItemDto>>;
