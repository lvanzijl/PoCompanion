using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Client.Pages;
using PoTool.Client.Models;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for BugTriageTreeNode component
/// </summary>
[TestClass]
public class BugTriageTreeNodeTests : BunitTestContext
{
    [TestMethod]
    public void BugTriageTreeNode_RendersBugTitle()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 123,
            Type = "Bug",
            Title = "Test Bug Title",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<BugTriageTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.SelectedId, (int?)null));

        // Assert
        Assert.Contains("Test Bug Title", cut.Markup);
    }

    [TestMethod]
    public void BugTriageTreeNode_RendersStatusColumn()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 123,
            Type = "Bug",
            Title = "Test Bug",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<BugTriageTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.SelectedId, (int?)null));

        // Assert
        var statusColumn = cut.Find(".status-column");
        Assert.IsNotNull(statusColumn);
        Assert.AreEqual("Active", statusColumn.TextContent);
    }

    [TestMethod]
    public void BugTriageTreeNode_AppliesSelectedClass()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 123,
            Type = "Bug",
            Title = "Test Bug",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<BugTriageTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.SelectedId, 123));

        // Assert
        var titleElement = cut.Find(".workitem-title");
        Assert.IsTrue(titleElement.ClassList.Contains("selected"));
    }

    [TestMethod]
    public void BugTriageTreeNode_ShowsTypeIndicator()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 123,
            Type = "Bug",
            Title = "Test Bug",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<BugTriageTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.SelectedId, (int?)null));

        // Assert
        var typeIndicator = cut.Find(".type-indicator");
        Assert.IsNotNull(typeIndicator);
    }

    [TestMethod]
    public void BugTriageTreeNode_ShowsWorkItemTypeBadge()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 123,
            Type = "Bug",
            Title = "Test Bug",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<BugTriageTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.SelectedId, (int?)null));

        // Assert
        var typeBadge = cut.Find(".workitem-type");
        Assert.IsNotNull(typeBadge);
        Assert.AreEqual("Bug", typeBadge.TextContent);
    }

    [TestMethod]
    public void BugTriageTreeNode_ExpandButton_ShowsWhenHasChildren()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 1,
            Type = "Bug",
            Title = "Parent Bug",
            State = "Active",
            Level = 0,
            IsExpanded = false,
            Children = new List<TreeNode>
            {
                new TreeNode { Id = 2, Type = "Bug", Title = "Child Bug", State = "Active", Level = 1, Children = new List<TreeNode>() }
            }
        };

        // Act
        var cut = RenderComponent<BugTriageTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.SelectedId, (int?)null));

        // Assert
        var expandButton = cut.Find("button.caret-button");
        Assert.IsNotNull(expandButton);
    }

    [TestMethod]
    public void BugTriageTreeNode_GroupNode_ShowsGroupLabel()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 1,
            Type = "(group)",
            Title = "Untriaged Bugs",
            State = "",
            Level = 0,
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<BugTriageTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.SelectedId, (int?)null));

        // Assert
        Assert.Contains("Untriaged Bugs", cut.Markup);
        var groupLabel = cut.Find(".group-label");
        Assert.IsNotNull(groupLabel);
    }

    [TestMethod]
    public void BugTriageTreeNode_ValidationIcon_ShowsWhenHasIssues()
    {
        // Arrange
        var node = new TreeNode
        {
            Id = 123,
            Type = "Bug",
            Title = "Bug with issues",
            State = "Active",
            Level = 0,
            ValidationIssues = new List<string> { "Validation issue 1" },
            Children = new List<TreeNode>()
        };

        // Act
        var cut = RenderComponent<BugTriageTreeNode>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.SelectedId, (int?)null));

        // Assert
        var validationIcon = cut.Find(".validation-icon");
        Assert.IsNotNull(validationIcon);
    }
}
