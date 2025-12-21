using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to get historical state timeline for a specific work item.
/// </summary>
public sealed record GetWorkItemStateTimelineQuery(
    int WorkItemId
) : IQuery<WorkItemStateTimelineDto?>;
