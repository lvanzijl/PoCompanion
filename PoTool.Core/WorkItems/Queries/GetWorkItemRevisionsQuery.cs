using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve revision history for a specific work item.
/// </summary>
public sealed record GetWorkItemRevisionsQuery(int WorkItemId) : IQuery<IEnumerable<WorkItemRevisionDto>>;
