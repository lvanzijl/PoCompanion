using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.Components.WorkItems.SubComponents;
using PoTool.Client.Models;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemTreeView component
/// </summary>
[TestClass]
public class WorkItemTreeViewTests : BunitTestContext
{
    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services (required for MudChip and IKeyInterceptorService)
        Services.AddMudServices();

        // Mock IKeyInterceptorService explicitly (required by MudChip in bUnit tests)
        var mockKeyInterceptorService = new Mock<IKeyInterceptorService>();
        Services.AddSingleton(mockKeyInterceptorService.Object);

        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TestMethod]
    public void WorkItemTreeView_ShowsLoading_WhenLoading()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemTreeView>(parameters => parameters
            .Add(p => p.IsLoading, true)
            .Add(p => p.TreeRoots, new List<TreeNode>()));

        // Assert
        Assert.Contains("Loading", cut.Markup, "Should show loading message");
    }

    [TestMethod]
    public void WorkItemTreeView_ShowsEmptyMessage_WhenNoNodes()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemTreeView>(parameters => parameters
            .Add(p => p.IsLoading, false)
            .Add(p => p.TreeRoots, new List<TreeNode>()));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("No work items") || cut.Markup.Contains("empty"),
            "Should show empty state message");
    }

    [TestMethod]
    public void WorkItemTreeView_RendersRootNodes()
    {
        // Arrange
        var nodes = new List<TreeNode>
        {
            new TreeNode
            {
                Id = 1,
                Type = "Epic",
                Title = "Epic 1",
                State = "Active",
                Level = 0,
                Children = new List<TreeNode>()
            },
            new TreeNode
            {
                Id = 2,
                Type = "Epic",
                Title = "Epic 2",
                State = "Active",
                Level = 0,
                Children = new List<TreeNode>()
            }
        };

        // Act
        var cut = RenderComponent<WorkItemTreeView>(parameters => parameters
            .Add(p => p.IsLoading, false)
            .Add(p => p.TreeRoots, nodes));

        // Assert
        Assert.Contains("Epic 1", cut.Markup, "Should render first root node");
        Assert.Contains("Epic 2", cut.Markup, "Should render second root node");
    }

    [TestMethod]
    public void WorkItemTreeView_PassesSelectedNodeCorrectly()
    {
        // Arrange
        var node1 = new TreeNode
        {
            Id = 1,
            Type = "Epic",
            Title = "Epic 1",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };
        var node2 = new TreeNode
        {
            Id = 2,
            Type = "Epic",
            Title = "Epic 2",
            State = "Active",
            Level = 0,
            Children = new List<TreeNode>()
        };
        var nodes = new List<TreeNode> { node1, node2 };

        // Act
        var cut = RenderComponent<WorkItemTreeView>(parameters => parameters
            .Add(p => p.IsLoading, false)
            .Add(p => p.TreeRoots, nodes)
            .Add(p => p.SelectedId, 1));

        // Assert
        // The selected node should have the 'selected' class
        Assert.Contains("selected", cut.Markup, "Selected node should be highlighted");
    }
}
