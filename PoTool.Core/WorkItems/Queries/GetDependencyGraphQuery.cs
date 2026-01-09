using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to get dependency graph for work items.
/// Can filter by area path, work item type, or specific work item IDs.
/// </summary>
public sealed record GetDependencyGraphQuery(
    string? AreaPathFilter = null,
    IReadOnlyList<int>? WorkItemIds = null,
    IReadOnlyList<string>? WorkItemTypes = null
) : IQuery<DependencyGraphDto>;
