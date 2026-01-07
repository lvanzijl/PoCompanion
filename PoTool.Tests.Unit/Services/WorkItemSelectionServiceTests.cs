using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using System.Text.Json;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemSelectionServiceTests
{
    private WorkItemSelectionService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _service = new WorkItemSelectionService();
    }

    private TreeNode CreateTestNode(int id, string title, bool hasChildren = false)
    {
        var workItem = new WorkItemDto
        {
            TfsId = id,
            Title = title,
            Type = "Feature",
            State = "New"
        };

        var node = new TreeNode
        {
            Id = id,
            Title = title,
            Type = "Feature",
            State = "New",
            JsonPayload = JsonSerializer.Serialize(workItem),
            Children = new List<TreeNode>()
        };

        if (hasChildren)
        {
            node.Children.Add(CreateTestNode(id + 100, $"Child of {title}"));
        }

        return node;
    }

    [TestMethod]
    public void ToggleNodeSelection_SelectsNewNode()
    {
        // Arrange
        var node = CreateTestNode(1, "Test Item");
        var currentState = new WorkItemSelectionService.SelectionState();

        // Act
        var newState = _service.ToggleNodeSelection(node, currentState);

        // Assert
        Assert.HasCount(1, newState.SelectedIds);
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(newState.SelectedIds.Contains(1));
        Assert.HasCount(1, newState.SelectedWorkItems);
        Assert.IsNotNull(newState.PrimarySelectedWorkItem);
        Assert.AreEqual(1, newState.PrimarySelectedWorkItem.TfsId);
    }

    [TestMethod]
    public void SelectAllNodes_SelectsAllVisibleNodes()
    {
        // Arrange
        var flatNodeList = new List<TreeNode>
        {
            CreateTestNode(1, "Item 1"),
            CreateTestNode(2, "Item 2"),
            CreateTestNode(3, "Item 3")
        };

        // Act
        var state = _service.SelectAllNodes(flatNodeList);

        // Assert
        Assert.HasCount(3, state.SelectedIds);
        Assert.HasCount(3, state.SelectedWorkItems);
        Assert.IsNotNull(state.PrimarySelectedWorkItem);
        Assert.AreEqual(1, state.PrimarySelectedWorkItem.TfsId); // First item
    }

    [TestMethod]
    public void SelectAllNodes_EmptyList_ReturnsEmptyState()
    {
        // Arrange
        var flatNodeList = new List<TreeNode>();

        // Act
        var state = _service.SelectAllNodes(flatNodeList);

        // Assert
        Assert.IsEmpty(state.SelectedIds);
        Assert.IsEmpty(state.SelectedWorkItems);
        Assert.IsNull(state.PrimarySelectedWorkItem);
    }

    [TestMethod]
    public void ClearSelection_ClearsAllSelections()
    {
        // Arrange - state doesn't matter, ClearSelection always returns empty

        // Act
        var state = _service.ClearSelection();

        // Assert
        Assert.IsEmpty(state.SelectedIds);
        Assert.IsEmpty(state.SelectedWorkItems);
        Assert.IsNull(state.PrimarySelectedWorkItem);
    }

    [TestMethod]
    public void HandleKeyboardNavigation_ArrowDown_SelectsNextItem()
    {
        // Arrange
        var flatNodeList = new List<TreeNode>
        {
            CreateTestNode(1, "Item 1"),
            CreateTestNode(2, "Item 2"),
            CreateTestNode(3, "Item 3")
        };
        var currentState = new WorkItemSelectionService.SelectionState
        {
            SelectedIds = new HashSet<int> { 1 },
            SelectedWorkItems = new List<WorkItemDto>
            {
                new() { TfsId = 1, Title = "Item 1", Type = "Feature", State = "New" }
            },
            PrimarySelectedWorkItem = new WorkItemDto { TfsId = 1, Title = "Item 1", Type = "Feature", State = "New" }
        };

        // Act
        var (newState, _, _) = _service.HandleKeyboardNavigation("ArrowDown", currentState, flatNodeList);

        // Assert
        Assert.AreEqual(2, newState.PrimarySelectedWorkItem?.TfsId);
    }

    [TestMethod]
    public void HandleKeyboardNavigation_ArrowUp_SelectsPreviousItem()
    {
        // Arrange
        var flatNodeList = new List<TreeNode>
        {
            CreateTestNode(1, "Item 1"),
            CreateTestNode(2, "Item 2"),
            CreateTestNode(3, "Item 3")
        };
        var currentState = new WorkItemSelectionService.SelectionState
        {
            SelectedIds = new HashSet<int> { 2 },
            SelectedWorkItems = new List<WorkItemDto>
            {
                new() { TfsId = 2, Title = "Item 2", Type = "Feature", State = "New" }
            },
            PrimarySelectedWorkItem = new WorkItemDto { TfsId = 2, Title = "Item 2", Type = "Feature", State = "New" }
        };

        // Act
        var (newState, _, _) = _service.HandleKeyboardNavigation("ArrowUp", currentState, flatNodeList);

        // Assert
        Assert.AreEqual(1, newState.PrimarySelectedWorkItem?.TfsId);
    }

    [TestMethod]
    public void HandleKeyboardNavigation_ArrowRight_OnCollapsedNode_ReturnsNodeToToggle()
    {
        // Arrange
        var node = CreateTestNode(1, "Parent", hasChildren: true);
        node.IsExpanded = false;
        var flatNodeList = new List<TreeNode> { node };
        var currentState = new WorkItemSelectionService.SelectionState
        {
            PrimarySelectedWorkItem = new WorkItemDto { TfsId = 1, Title = "Parent", Type = "Feature", State = "New" }
        };

        // Act
        var (_, nodeToToggle, shouldToggle) = _service.HandleKeyboardNavigation("ArrowRight", currentState, flatNodeList);

        // Assert
        Assert.IsTrue(shouldToggle);
        Assert.IsNotNull(nodeToToggle);
        Assert.AreEqual(1, nodeToToggle.Id);
    }

    [TestMethod]
    public void HandleKeyboardNavigation_ArrowLeft_OnExpandedNode_ReturnsNodeToToggle()
    {
        // Arrange
        var node = CreateTestNode(1, "Parent", hasChildren: true);
        node.IsExpanded = true;
        var flatNodeList = new List<TreeNode> { node };
        var currentState = new WorkItemSelectionService.SelectionState
        {
            PrimarySelectedWorkItem = new WorkItemDto { TfsId = 1, Title = "Parent", Type = "Feature", State = "New" }
        };

        // Act
        var (_, nodeToToggle, shouldToggle) = _service.HandleKeyboardNavigation("ArrowLeft", currentState, flatNodeList);

        // Assert
        Assert.IsTrue(shouldToggle);
        Assert.IsNotNull(nodeToToggle);
        Assert.AreEqual(1, nodeToToggle.Id);
    }

    [TestMethod]
    public void BuildFlatNodeList_FlattensExpandedTree()
    {
        // Arrange
        var parent = CreateTestNode(1, "Parent");
        parent.IsExpanded = true;
        parent.Children = new List<TreeNode>
        {
            CreateTestNode(2, "Child 1"),
            CreateTestNode(3, "Child 2")
        };
        var treeRoots = new List<TreeNode> { parent };

        // Act
        var flatList = _service.BuildFlatNodeList(treeRoots);

        // Assert
        Assert.HasCount(3, flatList);
        Assert.AreEqual(1, flatList[0].Id);
        Assert.AreEqual(2, flatList[1].Id);
        Assert.AreEqual(3, flatList[2].Id);
    }

    [TestMethod]
    public void BuildFlatNodeList_ExcludesCollapsedChildren()
    {
        // Arrange
        var parent = CreateTestNode(1, "Parent");
        parent.IsExpanded = false; // Collapsed
        parent.Children = new List<TreeNode>
        {
            CreateTestNode(2, "Child 1"),
            CreateTestNode(3, "Child 2")
        };
        var treeRoots = new List<TreeNode> { parent };

        // Act
        var flatList = _service.BuildFlatNodeList(treeRoots);

        // Assert
        Assert.HasCount(1, flatList); // Only parent, no children
        Assert.AreEqual(1, flatList[0].Id);
    }
}
