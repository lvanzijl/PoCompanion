using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve work items by root IDs (hierarchical tree loading).
/// Loads the complete hierarchy starting from specified root work item IDs.
/// </summary>
public sealed record GetWorkItemsByRootIdsQuery(
    int[] RootIds
) : IQuery<IEnumerable<WorkItemDto>>;
