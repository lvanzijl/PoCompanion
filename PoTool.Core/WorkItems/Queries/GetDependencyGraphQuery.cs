using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to get dependency graph for work items.
/// Can filter by area path or specific work item IDs.
/// </summary>
public sealed record GetDependencyGraphQuery(
    string? AreaPathFilter = null,
    IReadOnlyList<int>? WorkItemIds = null
) : IQuery<DependencyGraphDto>;
