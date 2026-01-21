using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Service for determining work item visibility based on completion status and validation.
/// </summary>
public class WorkItemVisibilityService
{
    private readonly StateClassificationService _stateClassificationService;

    public WorkItemVisibilityService(StateClassificationService stateClassificationService)
    {
        _stateClassificationService = stateClassificationService;
    }

    /// <summary>
    /// Determines if a work item should be hidden based on completion rules.
    /// </summary>
    /// <param name="node">The tree node to check.</param>
    /// <param name="allNodes">Map of all nodes in the tree for lookup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the node should be hidden, false otherwise.</returns>
    public async Task<bool> ShouldHideNodeAsync(TreeNode node, Dictionary<int, TreeNode> allNodes, CancellationToken cancellationToken = default)
    {
        // Rule: Root items (without a parent) must never be hidden
        if (!node.ParentId.HasValue)
        {
            return false;
        }

        // Rule 1: The work item itself must be in a "Done" state
        if (!await IsDoneStateAsync(node.Type, node.State, cancellationToken))
        {
            return false;
        }

        // Rule 2: Its parent must be in a "Done" state
        if (!allNodes.TryGetValue(node.ParentId.Value, out var parentNode))
        {
            // Parent doesn't exist in the tree - don't hide
            return false;
        }

        if (!await IsDoneStateAsync(parentNode.Type, parentNode.State, cancellationToken))
        {
            return false;
        }

        // Rule 3: All sibling work items under the same parent must be in a "Done" state
        var siblings = parentNode.Children;
        foreach (var sibling in siblings)
        {
            if (!await IsDoneStateAsync(sibling.Type, sibling.State, cancellationToken))
            {
                return false;
            }
        }

        // Rule 4: No validation issues on the work item itself or any of its children (recursively)
        if (HasValidationIssuesRecursive(node))
        {
            return false;
        }

        // All conditions met - hide this node
        return true;
    }

    /// <summary>
    /// Checks if a state is classified as "Done" for a given work item type.
    /// </summary>
    private async Task<bool> IsDoneStateAsync(string workItemType, string state, CancellationToken cancellationToken)
    {
        try
        {
            return await _stateClassificationService.IsDoneStateAsync(workItemType, state, cancellationToken);
        }
        catch (Exception ex)
        {
            // If classification fails, assume not done (fail safe)
            // Log for debugging but don't propagate the exception
            Console.WriteLine($"[WorkItemVisibilityService] Error checking state classification for {workItemType}/{state}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Recursively checks if a node or any of its descendants have validation issues.
    /// </summary>
    private bool HasValidationIssuesRecursive(TreeNode node)
    {
        // Check if this node has validation issues
        if (node.HasValidationIssues)
        {
            return true;
        }

        // Check if any descendant has validation issues using precomputed list
        if (node.HasDescendantIssues)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Filters a list of tree nodes to hide completed items based on visibility rules.
    /// </summary>
    /// <param name="nodes">The list of nodes to filter.</param>
    /// <param name="allNodes">Map of all nodes in the tree for lookup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered list with hidden nodes removed.</returns>
    public async Task<List<TreeNode>> FilterHiddenNodesAsync(List<TreeNode> nodes, Dictionary<int, TreeNode> allNodes, CancellationToken cancellationToken = default)
    {
        var visibleNodes = new List<TreeNode>();

        foreach (var node in nodes)
        {
            // Check if this node should be hidden
            if (await ShouldHideNodeAsync(node, allNodes, cancellationToken))
            {
                // Skip this node (hide it)
                continue;
            }

            // Recursively filter children
            if (node.Children.Any())
            {
                node.Children = await FilterHiddenNodesAsync(node.Children, allNodes, cancellationToken);
                node.ChildrenIds = node.Children.Select(c => c.Id).ToList();
            }

            visibleNodes.Add(node);
        }

        return visibleNodes;
    }
}
