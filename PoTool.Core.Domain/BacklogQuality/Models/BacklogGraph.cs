namespace PoTool.Core.Domain.BacklogQuality.Models;

/// <summary>
/// Canonical current-state backlog graph for backlog-quality evaluation.
/// </summary>
public sealed class BacklogGraph
{
    private readonly IReadOnlyDictionary<int, WorkItemSnapshot> _itemsById;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<WorkItemSnapshot>> _childrenByParentId;

    /// <summary>
    /// Initializes a new instance of the <see cref="BacklogGraph"/> class.
    /// </summary>
    /// <param name="workItems">All work items in the loaded graph, including removed items.</param>
    public BacklogGraph(IEnumerable<WorkItemSnapshot> workItems)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        var snapshots = workItems.ToList();
        var duplicateId = snapshots
            .GroupBy(item => item.WorkItemId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateId is not null)
        {
            throw new ArgumentException(
                $"Duplicate work item ID '{duplicateId.Key}' is not allowed in the backlog graph.",
                nameof(workItems));
        }

        _itemsById = snapshots.ToDictionary(item => item.WorkItemId);
        _childrenByParentId = snapshots
            .Where(item => item.ParentWorkItemId.HasValue)
            .GroupBy(item => item.ParentWorkItemId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<WorkItemSnapshot>)group.OrderBy(item => item.WorkItemId).ToArray());

        Items = snapshots.AsReadOnly();
        RootItems = snapshots
            .Where(item => !item.ParentWorkItemId.HasValue || !_itemsById.ContainsKey(item.ParentWorkItemId.Value))
            .OrderBy(item => item.WorkItemId)
            .ToArray();
    }

    /// <summary>
    /// Gets all work items in the canonical graph, including removed items.
    /// </summary>
    public IReadOnlyList<WorkItemSnapshot> Items { get; }

    /// <summary>
    /// Gets graph roots, including orphans whose current parent is outside the loaded graph.
    /// </summary>
    public IReadOnlyList<WorkItemSnapshot> RootItems { get; }

    /// <summary>
    /// Gets a value indicating whether the graph contains the specified work item.
    /// </summary>
    public bool Contains(int workItemId) => _itemsById.ContainsKey(workItemId);

    /// <summary>
    /// Returns the specified work item.
    /// </summary>
    public WorkItemSnapshot GetWorkItem(int workItemId)
    {
        if (!_itemsById.TryGetValue(workItemId, out var workItem))
        {
            throw new KeyNotFoundException($"Work item '{workItemId}' was not found in the backlog graph.");
        }

        return workItem;
    }

    /// <summary>
    /// Gets direct children for the specified parent work item.
    /// </summary>
    public IReadOnlyList<WorkItemSnapshot> GetChildren(int workItemId)
    {
        return _childrenByParentId.TryGetValue(workItemId, out var children)
            ? children
            : Array.Empty<WorkItemSnapshot>();
    }
}
