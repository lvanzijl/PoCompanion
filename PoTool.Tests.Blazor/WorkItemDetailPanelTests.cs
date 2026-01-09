using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.Components.WorkItems.SubComponents;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemDetailPanel component
/// </summary>
[TestClass]
public class WorkItemDetailPanelTests : BunitTestContext
{
    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();
        
        // Add required mock services
        var mockSnackbar = new Mock<ISnackbar>();
        Services.AddSingleton(mockSnackbar.Object);
        
        // Mock IWorkItemsClient for WorkItemService
        var mockWorkItemsClient = new Mock<IWorkItemsClient>();
        var mockHttpClient = new HttpClient { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(mockWorkItemsClient.Object);
        Services.AddSingleton(mockHttpClient);
        Services.AddSingleton<WorkItemService>();
        
        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

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
        var workItem = new WorkItemDto
        {
            TfsId = 123,
            Type = "User Story",
            Title = "Implement feature X",
            ParentTfsId = 100,
            AreaPath = "Project\\Team",
            IterationPath = "Sprint 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.Now,
            Effort = null
        };

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedWorkItem, workItem));

        // Assert
        Assert.Contains("123", cut.Markup, "Should display work item ID");
        Assert.Contains("User Story", cut.Markup, "Should display work item type");
        Assert.Contains("Implement feature X", cut.Markup, "Should display title");
        Assert.Contains("Active", cut.Markup, "Should display state");
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsParentId_WhenPresent()
    {
        // Arrange
        var workItem = new WorkItemDto
        {
            TfsId = 123,
            Type = "Task",
            Title = "Test task",
            ParentTfsId = 100,
            AreaPath = "Project\\Team",
            IterationPath = "Sprint 1",
            State = "New",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.Now,
            Effort = null
        };

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedWorkItem, workItem));

        // Assert
        Assert.Contains("100", cut.Markup, "Should display parent ID");
        Assert.Contains("Parent", cut.Markup, "Should show parent label");
    }

    [TestMethod]
    public void WorkItemDetailPanel_HidesParentId_WhenNull()
    {
        // Arrange
        var workItem = new WorkItemDto
        {
            TfsId = 1,
            Type = "Epic",
            Title = "Top level epic",
            ParentTfsId = null,
            AreaPath = "Project",
            IterationPath = "Release 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.Now,
            Effort = null
        };

        // Act
        var cut = RenderComponent<WorkItemDetailPanel>(parameters => parameters
            .Add(p => p.SelectedWorkItem, workItem));

        // Assert
        // Should show node details but not parent section
        Assert.Contains("Top level epic", cut.Markup);
        Assert.DoesNotContain("Parent ID", cut.Markup, 
            "Should not show parent section for top-level items");
    }
}
