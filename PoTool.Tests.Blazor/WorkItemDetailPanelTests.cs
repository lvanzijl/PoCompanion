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

        // Mock IClient for TfsConfigService
        var mockApiClient = new Mock<Client.ApiClient.IClient>();
        var mockTfsHttpClient = new HttpClient { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(mockApiClient.Object);
        Services.AddSingleton<TfsConfigService>(sp => new TfsConfigService(mockApiClient.Object, mockTfsHttpClient));

        // Mock IClipboardService for WorkItemDetailPanel
        var mockClipboardService = new Mock<Shared.Contracts.IClipboardService>();
        mockClipboardService.Setup(x => x.CopyToClipboardAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        Services.AddSingleton(mockClipboardService.Object);

        // Add BrowserNavigationService (required by WorkItemDetailPanel)
        Services.AddSingleton<BrowserNavigationService>();

        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsNothing_WhenNoSelection()
    {
        // Arrange & Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItem), (WorkItemDto?)null);
            builder.CloseComponent();
        });

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
            Effort = null,
            Description = null
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItem), workItem);
            builder.CloseComponent();
        });

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
            Effort = null,
            Description = null
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItem), workItem);
            builder.CloseComponent();
        });

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
            Effort = null,
            Description = null
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItem), workItem);
            builder.CloseComponent();
        });

        // Assert
        // Should show node details but not parent section
        Assert.Contains("Top level epic", cut.Markup);
        Assert.DoesNotContain("Parent ID", cut.Markup,
            "Should not show parent section for top-level items");
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsDescription_WhenPresent()
    {
        // Arrange
        var workItem = new WorkItemDto
        {
            TfsId = 123,
            Type = "User Story",
            Title = "Test story",
            ParentTfsId = null,
            AreaPath = "Project\\Team",
            IterationPath = "Sprint 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.Now,
            Effort = null,
            Description = "This is a test description"
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItem), workItem);
            builder.CloseComponent();
        });

        // Assert
        Assert.Contains("Description:", cut.Markup, "Should show description label");
        Assert.Contains("This is a test description", cut.Markup, "Should display description content");
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsNoDescriptionPlaceholder_WhenDescriptionNull()
    {
        // Arrange
        var workItem = new WorkItemDto
        {
            TfsId = 123,
            Type = "User Story",
            Title = "Test story",
            ParentTfsId = null,
            AreaPath = "Project\\Team",
            IterationPath = "Sprint 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.Now,
            Effort = null,
            Description = null
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItem), workItem);
            builder.CloseComponent();
        });

        // Assert
        Assert.Contains("Description:", cut.Markup, "Should show description label");
        Assert.Contains("(No description)", cut.Markup, "Should show placeholder for empty description");
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsNoDescriptionPlaceholder_WhenDescriptionEmpty()
    {
        // Arrange
        var workItem = new WorkItemDto
        {
            TfsId = 123,
            Type = "User Story",
            Title = "Test story",
            ParentTfsId = null,
            AreaPath = "Project\\Team",
            IterationPath = "Sprint 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.Now,
            Effort = null,
            Description = ""
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItem), workItem);
            builder.CloseComponent();
        });

        // Assert
        Assert.Contains("Description:", cut.Markup, "Should show description label");
        Assert.Contains("(No description)", cut.Markup, "Should show placeholder for empty description");
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsCommonDescription_WhenMultipleItemsWithSameDescription()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            new WorkItemDto
            {
                TfsId = 1,
                Type = "User Story",
                Title = "Story 1",
                ParentTfsId = null,
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                State = "Active",
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.Now,
                Effort = null,
                Description = "Same description"
            },
            new WorkItemDto
            {
                TfsId = 2,
                Type = "User Story",
                Title = "Story 2",
                ParentTfsId = null,
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                State = "Active",
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.Now,
                Effort = null,
                Description = "Same description"
            }
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItems), workItems);
            builder.CloseComponent();
        });

        // Assert
        Assert.Contains("Description:", cut.Markup, "Should show description label");
        Assert.Contains("Same description", cut.Markup, "Should display common description");
        Assert.DoesNotContain("(Multiple descriptions selected)", cut.Markup, "Should not show multi-select message when descriptions are the same");
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsMultiSelectMessage_WhenDifferentDescriptions()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            new WorkItemDto
            {
                TfsId = 1,
                Type = "User Story",
                Title = "Story 1",
                ParentTfsId = null,
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                State = "Active",
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.Now,
                Effort = null,
                Description = "Description 1"
            },
            new WorkItemDto
            {
                TfsId = 2,
                Type = "User Story",
                Title = "Story 2",
                ParentTfsId = null,
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                State = "Active",
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.Now,
                Effort = null,
                Description = "Description 2"
            }
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItems), workItems);
            builder.CloseComponent();
        });

        // Assert
        Assert.Contains("Description:", cut.Markup, "Should show description label");
        Assert.Contains("(Multiple descriptions selected)", cut.Markup, "Should show multi-select message when descriptions differ");
        Assert.DoesNotContain("Description 1", cut.Markup, "Should not show first description");
        Assert.DoesNotContain("Description 2", cut.Markup, "Should not show second description");
    }

    [TestMethod]
    public void WorkItemDetailPanel_ShowsNoDescriptionPlaceholder_WhenMultipleItemsAllWithEmptyDescription()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            new WorkItemDto
            {
                TfsId = 1,
                Type = "User Story",
                Title = "Story 1",
                ParentTfsId = null,
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                State = "Active",
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.Now,
                Effort = null,
                Description = null
            },
            new WorkItemDto
            {
                TfsId = 2,
                Type = "User Story",
                Title = "Story 2",
                ParentTfsId = null,
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                State = "Active",
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.Now,
                Effort = null,
                Description = ""
            }
        };

        // Act
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemDetailPanel>(1);
            builder.AddAttribute(2, nameof(WorkItemDetailPanel.SelectedWorkItems), workItems);
            builder.CloseComponent();
        });

        // Assert
        Assert.Contains("Description:", cut.Markup, "Should show description label");
        Assert.Contains("(No description)", cut.Markup, "Should show placeholder when all descriptions are empty");
        Assert.DoesNotContain("(Multiple descriptions selected)", cut.Markup, "Should not show multi-select message when all descriptions are empty");
    }
}
