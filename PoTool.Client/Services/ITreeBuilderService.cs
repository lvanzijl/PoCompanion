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
    /// Builds a tree structure from work items with validation issues.
    /// </summary>
    /// <param name="items">The work items with validation results.</param>
    /// <param name="expandedState">Dictionary of node IDs to their expanded state.</param>
    /// <returns>List of root tree nodes with validation issues populated.</returns>
    List<TreeNode> BuildTreeWithValidation(IEnumerable<WorkItemWithValidationDto> items, Dictionary<int, bool> expandedState);

    /// <summary>
    /// Filters work items by title, including all ancestors of matches.
    /// </summary>
    /// <param name="items">All work items.</param>
    /// <param name="filterText">The filter text to search for.</param>
    /// <returns>Filtered list of work items with their ancestors.</returns>
    List<WorkItemDto> FilterWithAncestors(List<WorkItemDto> items, string filterText);

    /// <summary>
    /// Builds a product-based tree structure with Product nodes at the top level and an Unparented node.
    /// </summary>
    /// <param name="items">The work items with validation results.</param>
    /// <param name="products">List of products with their root work item IDs.</param>
    /// <param name="expandedState">Dictionary of node IDs to their expanded state.</param>
    /// <returns>List of top-level tree nodes (Product nodes + Unparented node).</returns>
    List<TreeNode> BuildProductBasedTreeWithValidation(
        IEnumerable<WorkItemWithValidationDto> items,
        IEnumerable<ProductDto> products,
        Dictionary<int, bool> expandedState);

    /// <summary>
    /// Applies visibility filtering to a tree, hiding completed items that meet all hiding criteria.
    /// </summary>
    /// <param name="roots">The root nodes of the tree.</param>
    /// <param name="visibilityService">Service to determine node visibility.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered list of root nodes with hidden nodes removed.</returns>
    Task<List<TreeNode>> ApplyVisibilityFilterAsync(
        List<TreeNode> roots,
        WorkItemVisibilityService visibilityService,
        CancellationToken cancellationToken = default);
}
