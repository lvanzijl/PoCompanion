using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using System.Text.Json;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing work item selection state (single and multi-select).
/// </summary>
public class WorkItemSelectionService
{
    /// <summary>
    /// Represents the current selection state.
    /// </summary>
    public class SelectionState
    {
        public HashSet<int> SelectedIds { get; set; } = new();
        public List<WorkItemDto> SelectedWorkItems { get; set; } = new();
        public WorkItemDto? PrimarySelectedWorkItem { get; set; }
    }

    /// <summary>
    /// Toggles selection of a work item node.
    /// </summary>
    /// <param name="node">Node to toggle.</param>
    /// <param name="currentState">Current selection state.</param>
    /// <param name="ctrlKey">Whether Ctrl key is pressed (for multi-select toggle).</param>
    /// <param name="shiftKey">Whether Shift key is pressed (for range select).</param>
    /// <returns>Updated selection state.</returns>
    public SelectionState ToggleNodeSelection(TreeNode node, SelectionState currentState, bool ctrlKey = false, bool shiftKey = false)
    {
        if (string.IsNullOrEmpty(node.JsonPayload))
            return currentState;

        var workItem = JsonSerializer.Deserialize<WorkItemDto>(node.JsonPayload);
        if (workItem == null)
            return currentState;

        // If neither Ctrl nor Shift is pressed, do single selection (clear others)
        if (!ctrlKey && !shiftKey)
        {
            var newState = new SelectionState
            {
                SelectedIds = new HashSet<int> { node.Id },
                SelectedWorkItems = new List<WorkItemDto> { workItem },
                PrimarySelectedWorkItem = workItem
            };
            return newState;
        }

        // Ctrl key: toggle individual selection (add/remove from multi-selection)
        if (ctrlKey && !shiftKey)
        {
            var newState = new SelectionState
            {
                SelectedIds = new HashSet<int>(currentState.SelectedIds),
                SelectedWorkItems = new List<WorkItemDto>(currentState.SelectedWorkItems)
            };

            // Toggle selection: if already selected, remove; otherwise add
            if (newState.SelectedIds.Contains(node.Id))
            {
                newState.SelectedIds.Remove(node.Id);
                newState.SelectedWorkItems.RemoveAll(w => w.TfsId == node.Id);
                newState.PrimarySelectedWorkItem = newState.SelectedWorkItems.FirstOrDefault();
            }
            else
            {
                newState.SelectedIds.Add(node.Id);
                if (!newState.SelectedWorkItems.Any(w => w.TfsId == node.Id))
                {
                    newState.SelectedWorkItems.Add(workItem);
                }
                newState.PrimarySelectedWorkItem = workItem;
            }

            return newState;
        }

        // Shift key: range selection (select from last selected to current)
        // For now, just add to selection without range logic (would need flat list context)
        if (shiftKey)
        {
            var newState = new SelectionState
            {
                SelectedIds = new HashSet<int>(currentState.SelectedIds),
                SelectedWorkItems = new List<WorkItemDto>(currentState.SelectedWorkItems)
            };

            if (!newState.SelectedIds.Contains(node.Id))
            {
                newState.SelectedIds.Add(node.Id);
                if (!newState.SelectedWorkItems.Any(w => w.TfsId == node.Id))
                {
                    newState.SelectedWorkItems.Add(workItem);
                }
            }
            newState.PrimarySelectedWorkItem = workItem;

            return newState;
        }

        return currentState;
    }

    /// <summary>
    /// Selects all visible nodes.
    /// </summary>
    /// <param name="flatNodeList">Flat list of visible nodes.</param>
    /// <returns>Selection state with all nodes selected.</returns>
    public SelectionState SelectAllNodes(List<TreeNode> flatNodeList)
    {
        var newState = new SelectionState();

        foreach (var node in flatNodeList)
        {
            newState.SelectedIds.Add(node.Id);
            if (!string.IsNullOrEmpty(node.JsonPayload))
            {
                var workItem = JsonSerializer.Deserialize<WorkItemDto>(node.JsonPayload);
                if (workItem != null && !newState.SelectedWorkItems.Any(w => w.TfsId == node.Id))
                {
                    newState.SelectedWorkItems.Add(workItem);
                }
            }
        }

        // Set primary selection to first item
        newState.PrimarySelectedWorkItem = newState.SelectedWorkItems.FirstOrDefault();

        return newState;
    }

    /// <summary>
    /// Clears all selections.
    /// </summary>
    /// <returns>Empty selection state.</returns>
    public SelectionState ClearSelection()
    {
        return new SelectionState();
    }

    /// <summary>
    /// Navigates selection using keyboard (arrow keys, enter, etc.).
    /// </summary>
    /// <param name="key">Key pressed.</param>
    /// <param name="currentState">Current selection state.</param>
    /// <param name="flatNodeList">Flat list of visible nodes.</param>
    /// <returns>Updated selection state and the node to toggle (if any).</returns>
    public (SelectionState newState, TreeNode? nodeToToggle, bool shouldToggle) HandleKeyboardNavigation(
        string key,
        SelectionState currentState,
        List<TreeNode> flatNodeList)
    {
        if (flatNodeList.Count == 0)
            return (currentState, null, false);

        var currentIndex = -1;
        if (currentState.PrimarySelectedWorkItem != null)
        {
            currentIndex = flatNodeList.FindIndex(n => n.Id == currentState.PrimarySelectedWorkItem.TfsId);
        }

        TreeNode? targetNode = null;

        switch (key)
        {
            case "ArrowDown":
                if (currentIndex < flatNodeList.Count - 1)
                {
                    targetNode = flatNodeList[currentIndex + 1];
                }
                break;

            case "ArrowUp":
                if (currentIndex > 0)
                {
                    targetNode = flatNodeList[currentIndex - 1];
                }
                else if (currentIndex == -1 && flatNodeList.Count > 0)
                {
                    targetNode = flatNodeList[0];
                }
                break;

            case "ArrowRight":
                if (currentIndex >= 0)
                {
                    var node = flatNodeList[currentIndex];
                    if (node.Children.Any() && !node.IsExpanded)
                    {
                        return (currentState, node, true);
                    }
                }
                break;

            case "ArrowLeft":
                if (currentIndex >= 0)
                {
                    var node = flatNodeList[currentIndex];
                    if (node.Children.Any() && node.IsExpanded)
                    {
                        return (currentState, node, true);
                    }
                }
                break;

            case "Enter":
            case " ":
                if (currentIndex >= 0)
                {
                    var node = flatNodeList[currentIndex];
                    if (node.Children.Any())
                    {
                        return (currentState, node, true);
                    }
                }
                break;
        }

        // If we have a target node to select, toggle its selection
        if (targetNode != null)
        {
            var newState = ToggleNodeSelection(targetNode, currentState);
            return (newState, null, false);
        }

        return (currentState, null, false);
    }

    /// <summary>
    /// Builds a flat list of nodes from tree roots for keyboard navigation.
    /// </summary>
    /// <param name="treeRoots">Tree root nodes.</param>
    /// <returns>Flat list of visible nodes.</returns>
    public List<TreeNode> BuildFlatNodeList(List<TreeNode> treeRoots)
    {
        var flatList = new List<TreeNode>();
        foreach (var root in treeRoots)
        {
            AddNodeToFlatList(root, flatList);
        }
        return flatList;
    }

    private void AddNodeToFlatList(TreeNode node, List<TreeNode> flatList)
    {
        flatList.Add(node);
        if (node.IsExpanded)
        {
            foreach (var child in node.Children)
            {
                AddNodeToFlatList(child, flatList);
            }
        }
    }
}
