using Microsoft.Extensions.DependencyInjection;
using PoTool.Client.Components.WorkItems.SubComponents;
using PoTool.Client.Models;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemTreeNode component
/// </summary>
[TestClass]
public class WorkItemTreeNodeTests : BunitTestContext
{
    [TestMethod]
    public void WorkItemTreeNode_RendersNodeTitle()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 1,
            Type = "Epic",
            Title = "Test Epic",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<WorkItemTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsSelected, false));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("Test Epic"));
        Assert.IsTrue(cut.Markup.Contains("Epic"));
    }

    [TestMethod]
    public void WorkItemTreeNode_ExpandButton_ShowsWhenHasChildren()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 1,
            Type = "Epic",
            Title = "Parent",
            State = "Active",
            Level = 0,
            IsExpanded = false,
            Children = new List<TreeNode>
            {
                new TreeNode { Id = 2, Type = "Feature", Title = "Child", State = "Active", Level = 1, Children = new List<TreeNode>() }
            }
        };

        // Act
        var cut = RenderComponent<WorkItemTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsSelected, false));

        // Assert
        var expandButton = cut.Find("button.expand-button");
        Assert.IsNotNull(expandButton);
    }

    [TestMethod]
    public void WorkItemTreeNode_ExpandButton_InvokesCallback()
    {
        // Arrange
        var expandToggled = false;
        var node = new TreeNode
        {
            Id = 1,
            Type = "Epic",
            Title = "Parent",
            State = "Active",
            Level = 0,
            IsExpanded = false,
            Children = new List<TreeNode>
            {
                new TreeNode { Id = 2, Type = "Feature", Title = "Child", State = "Active", Level = 1, Children = new List<TreeNode>() }
            }
        };

        var cut = RenderComponent<WorkItemTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsSelected, false)
            .Add(p => p.OnToggleExpand, () => { expandToggled = true; }));

        // Act
        var expandButton = cut.Find("button.expand-button");
        expandButton.Click();

        // Assert
        Assert.IsTrue(expandToggled, "Toggle expand callback should have been invoked");
    }

    [TestMethod]
    public void WorkItemTreeNode_Click_InvokesSelectionCallback()
    {
        // Arrange
        var selectionChanged = false;
        TreeNode? selectedNode = null;
        var node = new TreeNode
        {
            Id = 1,
            Type = "Epic",
            Title = "Test",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };

        var cut = RenderComponent<WorkItemTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsSelected, false)
            .Add(p => p.OnNodeSelected, (n) => 
            { 
                selectionChanged = true;
                selectedNode = n;
            }));

        // Act
        var nodeDiv = cut.Find("div.tree-node-content");
        nodeDiv.Click();

        // Assert
        Assert.IsTrue(selectionChanged, "Selection callback should have been invoked");
        Assert.AreEqual(node, selectedNode);
    }

    [TestMethod]
    public void WorkItemTreeNode_SelectedState_AppliesCorrectClass()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 1,
            Type = "Epic",
            Title = "Test",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<WorkItemTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsSelected, true));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("selected"), "Selected node should have 'selected' CSS class");
    }

    [TestMethod]
    public void WorkItemTreeNode_PlaceholderNode_RendersCorrectly()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 999,
            Type = "Unknown",
            Title = "Missing Parent",
            State = "",
            Level = 0,
            IsPlaceholder = true,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<WorkItemTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsSelected, false));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("placeholder"), "Placeholder node should have 'placeholder' indicator");
    }

    [TestMethod]
    public void WorkItemTreeNode_RendersChildren_WhenExpanded()
    {
        // Arrange
        var childNode = new TreeNode
        {
            Id = 2,
            Type = "Feature",
            Title = "Child Feature",
            State = "Active",
            Level = 1,
            Children = new List<TreeNode>()
        };

        var parentNode = new TreeNode
        {
            Id = 1,
            Type = "Epic",
            Title = "Parent Epic",
            State = "Active",
            Level = 0,
            IsExpanded = true,
            Children = new List<TreeNode> { childNode }
        };

        // Act
        var cut = RenderComponent<WorkItemTreeNode>(parameters => parameters
            .Add(p => p.Node, parentNode)
            .Add(p => p.IsSelected, false));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("Child Feature"), "Expanded node should render children");
    }
}
