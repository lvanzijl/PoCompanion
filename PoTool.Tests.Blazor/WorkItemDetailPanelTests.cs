using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Client.Components.WorkItems.SubComponents;
using PoTool.Client.ApiClient;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemDetailPanel component
/// </summary>
[TestClass]
public class WorkItemDetailPanelTests : BunitTestContext
{
    [TestMethod]
    public void WorkItemDetailPanel_ShowsNothing_WhenNoSelection()
    {
        // Arrange & Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedWorkItem, null));

        // Assert
        // Component renders nothing when no item is selected
        Assert.IsTrue(string.IsNullOrWhiteSpace(cut.Markup) || !cut.Markup.Contains("detail-panel"));
    }

    [TestMethod]
    public void WorkItemDetailPanel_DisplaysWorkItemDetails_WhenSelected()
    {
        // Arrange
        var workItem = new WorkItemDto(
            TfsId: 123,
            Type: "User Story",
            Title: "Implement feature X",
            ParentTfsId: 100,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.Now
        );

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedWorkItem, workItem));

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
        var workItem = new WorkItemDto(
            TfsId: 123,
            Type: "Task",
            Title: "Test task",
            ParentTfsId: 100,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.Now
        );

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedWorkItem, workItem));

        // Assert
        Assert.IsTrue(cut.Markup.Contains("100"), "Should display parent ID");
        Assert.IsTrue(cut.Markup.Contains("Parent"), "Should show parent label");
    }

    [TestMethod]
    public void WorkItemDetailPanel_HidesParentId_WhenNull()
    {
        // Arrange
        var workItem = new WorkItemDto(
            TfsId: 1,
            Type: "Epic",
            Title: "Top level epic",
            ParentTfsId: null,
            AreaPath: "Project",
            IterationPath: "Release 1",
            State: "Active",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.Now
        );

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedWorkItem, workItem));

        // Assert
        // Should show node details but not parent section
        Assert.IsTrue(cut.Markup.Contains("Top level epic"));
        Assert.IsFalse(cut.Markup.Contains("Parent ID"), 
            "Should not show parent section for top-level items");
    }
}
