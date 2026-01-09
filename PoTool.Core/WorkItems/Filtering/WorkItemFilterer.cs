using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Filtering;

/// <summary>
/// Core business logic for filtering work items with validation and hierarchy considerations.
/// Uses generic type parameters to avoid coupling to specific DTO implementations.
/// </summary>
public class WorkItemFilterer
{
    /// <summary>
    /// Interface for work items that can be filtered.
    /// </summary>
    public interface IFilterableWorkItem
    {
        int TfsId { get; }
        int? ParentTfsId { get; }
        IEnumerable<IValidationIssue> ValidationIssues { get; }
    }

    /// <summary>
    /// Interface for validation issues.
    /// </summary>
    public interface IValidationIssue
    {
        string Message { get; }
    }

    /// <summary>
    /// Filters work items by validation issues, including their ancestors for hierarchy visibility.
    /// </summary>
    /// <typeparam name="T">Type of work item that implements IFilterableWorkItem.</typeparam>
    /// <param name="items">Work items to filter.</param>
    /// <param name="targetIds">IDs of work items with validation issues to include.</param>
    /// <returns>Filtered work items including ancestors.</returns>
    public IEnumerable<T> FilterByValidationWithAncestors<T>(
        IEnumerable<T> items,
        HashSet<int> targetIds) where T : IFilterableWorkItem
    {
        var itemsList = items.ToList();
        var itemLookup = itemsList.ToDictionary(w => w.TfsId);
        var toInclude = new Dictionary<int, T>();

        foreach (var targetId in targetIds)
        {
            if (!itemLookup.TryGetValue(targetId, out var item))
                continue;

            // Include the target item
            if (!toInclude.ContainsKey(item.TfsId))
            {
                toInclude[item.TfsId] = item;
            }

            // Include all ancestors
            var current = item;
            while (current.ParentTfsId.HasValue)
            {
                var parentId = current.ParentTfsId.Value;
                if (itemLookup.TryGetValue(parentId, out var parent))
                {
                    if (!toInclude.ContainsKey(parent.TfsId))
                    {
                        toInclude[parent.TfsId] = parent;
                    }
                    current = parent;
                }
                else
                {
                    break;
                }
            }
        }

        return toInclude.Values.ToList();
    }

    /// <summary>
    /// Extracts work item IDs that match the given validation filter.
    /// </summary>
    /// <typeparam name="T">Type of work item that implements IFilterableWorkItem.</typeparam>
    /// <param name="workItems">Work items to search.</param>
    /// <param name="filterId">Filter identifier (e.g., "parentProgress", "missingEffort").</param>
    /// <returns>IDs of work items matching the filter.</returns>
    public IEnumerable<int> GetWorkItemIdsByValidationFilter<T>(
        IEnumerable<T> workItems,
        string filterId) where T : IFilterableWorkItem
    {
        return filterId switch
        {
            "parentProgress" => workItems
                .Where(wi => wi.ValidationIssues.Any(issue =>
                    issue.Message.Contains("Parent") || issue.Message.Contains("Ancestor")))
                .Select(wi => wi.TfsId),
            "missingEffort" => workItems
                .Where(wi => wi.ValidationIssues.Any(issue =>
                    issue.Message.Contains("effort")))
                .Select(wi => wi.TfsId),
            _ => Enumerable.Empty<int>()
        };
    }

    /// <summary>
    /// Counts work items matching the given validation filter.
    /// </summary>
    /// <typeparam name="T">Type of work item that implements IFilterableWorkItem.</typeparam>
    /// <param name="workItems">Work items to count.</param>
    /// <param name="filterId">Filter identifier.</param>
    /// <returns>Count of matching work items.</returns>
    public int CountWorkItemsByValidationFilter<T>(
        IEnumerable<T> workItems,
        string filterId) where T : IFilterableWorkItem
    {
        return GetWorkItemIdsByValidationFilter(workItems, filterId).Count();
    }

    /// <summary>
    /// Checks if a work item is a descendant of any configured goals.
    /// </summary>
    /// <typeparam name="T">Type of work item that implements IFilterableWorkItem.</typeparam>
    /// <param name="item">Work item to check.</param>
    /// <param name="goalIds">List of goal IDs.</param>
    /// <param name="allWorkItems">All work items for lookup.</param>
    /// <returns>True if item is a goal or descendant of a goal.</returns>
    public bool IsDescendantOfGoals<T>(
        T item,
        List<int> goalIds,
        IEnumerable<T> allWorkItems) where T : IFilterableWorkItem
    {
        if (goalIds == null || goalIds.Count == 0)
            return true;

        // Check if this item is itself a goal
        if (goalIds.Contains(item.TfsId))
            return true;

        // Create lookup dictionary for efficient parent traversal
        var itemLookup = allWorkItems.ToDictionary(wi => wi.TfsId);

        // Traverse up the parent chain to see if any ancestor is a configured goal
        var current = item;
        while (current.ParentTfsId.HasValue)
        {
            if (goalIds.Contains(current.ParentTfsId.Value))
                return true;

            // Find parent in lookup dictionary
            if (!itemLookup.TryGetValue(current.ParentTfsId.Value, out current))
                break;
        }

        return false;
    }
}
