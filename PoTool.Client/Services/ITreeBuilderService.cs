using PoTool.Client.ApiClient;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Service for building hierarchical tree structures from flat work item lists.
/// </summary>
public interface ITreeBuilderService
{
    /// <summary>
    /// Builds a tree structure from a flat list of work items.
    /// </summary>
    /// <param name="items">The work items to build the tree from.</param>
    /// <param name="expandedState">Dictionary of node IDs to their expanded state.</param>
    /// <returns>List of root tree nodes.</returns>
    List<TreeNode> BuildTree(IEnumerable<WorkItemDto> items, Dictionary<int, bool> expandedState);

    /// <summary>
    /// Filters work items by title, including all ancestors of matches.
    /// </summary>
    /// <param name="items">All work items.</param>
    /// <param name="filterText">The filter text to search for.</param>
    /// <returns>Filtered list of work items with their ancestors.</returns>
    List<WorkItemDto> FilterWithAncestors(List<WorkItemDto> items, string filterText);
}
