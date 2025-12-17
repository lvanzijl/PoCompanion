using Microsoft.Extensions.DependencyInjection;
using PoTool.Client.Components.WorkItems.SubComponents;
using PoTool.Client.Models;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemDetailPanel component
/// </summary>
[TestClass]
public class WorkItemDetailPanelTests : BunitTestContext
{
    [TestMethod]
    public void WorkItemDetailPanel_ShowsMessage_WhenNoSelection()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedNode, null));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("Select a work item"));
    }

    [TestMethod]
    public void WorkItemDetailPanel_DisplaysNodeDetails_WhenSelected()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 123,
            Type = "User Story",
            Title = "Implement feature X",
            State = "Active",
            Level = 2,
            ParentId = 100,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedNode, node));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("123"), "Should display work item ID");
        Assert.IsTrue(cut.Markup.Contains("User Story"), "Should display work item type");
        Assert.IsTrue(cut.Markup.Contains("Implement feature X"), "Should display title");
        Assert.IsTrue(cut.Markup.Contains("Active"), "Should display state");
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsParentId_WhenPresent()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 123,
            Type = "Task",
            Title = "Test task",
            State = "New",
            Level = 3,
            ParentId = 100,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedNode, node));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("100"), "Should display parent ID");
        Assert.IsTrue(cut.Markup.Contains("Parent"), "Should show parent label");
    }

    [TestMethod]
    public void WorkItemDetailPanel_HidesParentId_WhenNull()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 1,
            Type = "Epic",
            Title = "Top level epic",
            State = "Active",
            Level = 0,
            ParentId = null,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedNode, node));

        // Assert
        // Should show node details but not parent section
        Assert.IsTrue(cut.Markup.Contains("Top level epic"));
        Assert.IsFalse(cut.Markup.Contains("Parent:") || cut.Markup.Contains("parent-info"), 
            "Should not show parent section for top-level items");
    }
}
