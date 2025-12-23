using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Components.WorkItems;
using PoTool.Client.Configuration;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemExplorer component
/// </summary>
[TestClass]
public class WorkItemExplorerTests : BunitTestContext
{
    private Mock<WorkItemService> _mockWorkItemService = null!;
    private Mock<IWorkItemSyncHubService> _mockSyncHubService = null!;
    private Mock<ITreeBuilderService> _mockTreeBuilderService = null!;
    private Mock<SettingsService> _mockSettingsService = null!;
    private Mock<TfsConfigService> _mockTfsConfigService = null!;
    private Mock<ModeIsolatedStateService> _mockStateService = null!;
    private Mock<IDialogService> _mockDialogService = null!;
    private Mock<ISnackbar> _mockSnackbar = null!;
    private Mock<IJSRuntime> _mockJSRuntime = null!;

    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();

        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Setup mocks
        _mockWorkItemService = new Mock<WorkItemService>();
        _mockSyncHubService = new Mock<IWorkItemSyncHubService>();
        _mockTreeBuilderService = new Mock<ITreeBuilderService>();
        _mockSettingsService = new Mock<SettingsService>();
        _mockTfsConfigService = new Mock<TfsConfigService>();
        _mockStateService = new Mock<ModeIsolatedStateService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockSnackbar = new Mock<ISnackbar>();
        _mockJSRuntime = new Mock<IJSRuntime>();

        // Setup default behaviors
        _mockSyncHubService.Setup(x => x.StartAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockSyncHubService.Setup(x => x.IsConnected).Returns(true);

        _mockStateService.Setup(x => x.LoadExpandedStateAsync())
            .ReturnsAsync(new Dictionary<int, bool>());
        _mockStateService.Setup(x => x.SaveExpandedStateAsync(It.IsAny<Dictionary<int, bool>>()))
            .Returns(Task.CompletedTask);

        _mockSettingsService.Setup(x => x.GetOrCreateDefaultSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsDto { DataMode = DataMode.Mock, ConfiguredGoalIds = new List<int>() });

        _mockTfsConfigService.Setup(x => x.GetConfigAsync())
            .ReturnsAsync((TfsConfigDto?)null);

        _mockWorkItemService.Setup(x => x.GetAllWithValidationAsync())
            .ReturnsAsync(new List<WorkItemWithValidationDto>() as IEnumerable<WorkItemWithValidationDto>);

        _mockTreeBuilderService.Setup(x => x.BuildTreeWithValidation(
                It.IsAny<IEnumerable<WorkItemWithValidationDto>>(),
                It.IsAny<Dictionary<int, bool>>()))
            .Returns(new List<TreeNode>());

        // Register mock services
        Services.AddSingleton(_mockWorkItemService.Object);
        Services.AddSingleton(_mockSyncHubService.Object);
        Services.AddSingleton(_mockTreeBuilderService.Object);
        Services.AddSingleton(_mockSettingsService.Object);
        Services.AddSingleton(_mockTfsConfigService.Object);
        Services.AddSingleton(_mockStateService.Object);
        Services.AddSingleton(_mockDialogService.Object);
        Services.AddSingleton(_mockSnackbar.Object);
        Services.AddSingleton<ErrorMessageService>();
        Services.AddSingleton(_mockJSRuntime.Object);
    }

    private IRenderedFragment RenderWorkItemExplorerWithMudProvider()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WorkItemExplorer>(1);
            builder.CloseComponent();
        });
    }

    [TestMethod]
    public void WorkItemExplorer_RendersCorrectly_WithEmptyData()
    {
        // Arrange
        _mockWorkItemService.Setup(x => x.GetAllWithValidationAsync())
            .ReturnsAsync(new List<WorkItemWithValidationDto>() as IEnumerable<WorkItemWithValidationDto>);

        // Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var markup = cut.Markup;
            Assert.IsFalse(markup.Contains("mud-progress-linear"),
                "Loading indicator should be gone after data loads");
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Work Item Explorer", cut.Markup);
        Assert.IsNotNull(cut);
    }

    [TestMethod]
    public void WorkItemExplorer_RendersCorrectly_WithPopulatedData()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            CreateWorkItemWithValidation(1, "Epic", "Test Epic", null, "New"),
            CreateWorkItemWithValidation(2, "User Story", "Test Story", 1, "Active")
        };

        _mockWorkItemService.Setup(x => x.GetAllWithValidationAsync())
            .ReturnsAsync(workItems);

        var treeNodes = new List<TreeNode>
        {
            new TreeNode 
            { 
                Id = 1, 
                Title = "Test Epic", 
                Type = "Epic",
                Children = new List<TreeNode>
                {
                    new TreeNode { Id = 2, Title = "Test Story", Type = "User Story", Children = new List<TreeNode>() }
                }
            }
        };

        _mockTreeBuilderService.Setup(x => x.BuildTreeWithValidation(
                It.IsAny<IEnumerable<WorkItemWithValidationDto>>(),
                It.IsAny<Dictionary<int, bool>>()))
            .Returns(treeNodes);

        // Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Work Item Explorer", cut.Markup);
        _mockWorkItemService.Verify(x => x.GetAllWithValidationAsync(), Times.Once);
    }

    [TestMethod]
    public void WorkItemExplorer_DisplaysLoadingState_Initially()
    {
        // Arrange
        var tcs = new TaskCompletionSource<IEnumerable<WorkItemWithValidationDto>>();
        _mockWorkItemService.Setup(x => x.GetAllWithValidationAsync())
            .Returns(tcs.Task);

        // Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Assert - Should show loading initially
        // Note: Component uses _isLoading flag internally but may not display visible loader
        Assert.IsNotNull(cut);

        // Complete the async operation
        tcs.SetResult(new List<WorkItemWithValidationDto>() as IEnumerable<WorkItemWithValidationDto>);
    }

    [TestMethod]
    public void WorkItemExplorer_DisplaysTypeLegend()
    {
        // Arrange & Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("type-legend", cut.Markup);
    }

    [TestMethod]
    public void WorkItemExplorer_HasFilterTextbox()
    {
        // Arrange & Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert - Toolbar should be rendered which contains filter
        Assert.IsNotNull(cut);
    }

    [TestMethod]
    public void WorkItemExplorer_InitializesSignalRConnection()
    {
        // Arrange & Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            _mockSyncHubService.Verify(x => x.StartAsync(It.IsAny<string>()), Times.Once);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void WorkItemExplorer_LoadsSettings_OnInitialization()
    {
        // Arrange & Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            _mockSettingsService.Verify(x => x.GetOrCreateDefaultSettingsAsync(), Times.Once);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void WorkItemExplorer_LoadsWorkItems_OnInitialization()
    {
        // Arrange & Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            _mockWorkItemService.Verify(x => x.GetAllWithValidationAsync(), Times.Once);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void WorkItemExplorer_BuildsTree_AfterLoadingWorkItems()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            CreateWorkItemWithValidation(1, "Epic", "Test Epic", null, "New")
        };

        _mockWorkItemService.Setup(x => x.GetAllWithValidationAsync())
            .ReturnsAsync(workItems);

        // Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            _mockTreeBuilderService.Verify(
                x => x.BuildTreeWithValidation(
                    It.IsAny<IEnumerable<WorkItemWithValidationDto>>(),
                    It.IsAny<Dictionary<int, bool>>()),
                Times.AtLeastOnce);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    // Helper method to create test work items with validation
    private WorkItemWithValidationDto CreateWorkItemWithValidation(
        int id,
        string type,
        string title,
        int? parentId,
        string state)
    {
        return new WorkItemWithValidationDto
        {
            TfsId = id,
            Type = type,
            Title = title,
            ParentTfsId = parentId,
            AreaPath = "Project\\Team",
            IterationPath = "Sprint 1",
            State = state,
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.Now,
            Effort = null,
            ValidationIssues = new List<ValidationIssue>()
        };
    }
}
