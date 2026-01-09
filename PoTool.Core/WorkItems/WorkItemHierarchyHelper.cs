using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems;

/// <summary>
/// Helper class for work item hierarchy traversal operations.
/// </summary>
public static class WorkItemHierarchyHelper
{
    /// <summary>
    /// Recursively adds an item and all its descendants to the result set.
    /// </summary>
    /// <param name="itemId">The ID of the item to add.</param>
    /// <param name="allItems">All available work items.</param>
    /// <param name="result">The set to add item IDs to.</param>
    public static void AddItemAndDescendants(int itemId, IEnumerable<WorkItemDto> allItems, HashSet<int> result)
    {
        var itemsList = allItems as List<WorkItemDto> ?? allItems.ToList();
        var item = itemsList.FirstOrDefault(i => i.TfsId == itemId);
        
        if (item == null) return;

        result.Add(itemId);
        var children = itemsList.Where(i => i.ParentTfsId == itemId);
        
        foreach (var child in children)
        {
            AddItemAndDescendants(child.TfsId, itemsList, result);
        }
    }

    /// <summary>
    /// Filters work items to include only specified root items and their descendants.
    /// </summary>
    /// <param name="rootIds">The IDs of root items to include.</param>
    /// <param name="allItems">All available work items.</param>
    /// <returns>Filtered list containing only the specified roots and their descendants.</returns>
    public static List<WorkItemDto> FilterDescendants(List<int> rootIds, IEnumerable<WorkItemDto> allItems)
    {
        var itemsToInclude = new HashSet<int>();
        var itemsList = allItems as List<WorkItemDto> ?? allItems.ToList();

        foreach (var rootId in rootIds)
        {
            AddItemAndDescendants(rootId, itemsList, itemsToInclude);
        }

        return itemsList.Where(i => itemsToInclude.Contains(i.TfsId)).ToList();
    }
}
