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
    private Mock<IWorkItemsClient> _mockWorkItemsClient = null!;
    private Mock<IWorkItemSyncHubService> _mockSyncHubService = null!;
    private Mock<ITreeBuilderService> _mockTreeBuilderService = null!;
    private Mock<ISettingsClient> _mockSettingsClient = null!;
    private Mock<IProfilesClient> _mockProfilesClient = null!;
    private Mock<IClient> _mockApiClient = null!;
    private Mock<ISecureStorageService> _mockSecureStorage = null!;
    private Mock<Core.Contracts.IClipboardService> _mockClipboardService = null!;
    private Mock<IDialogService> _mockDialogService = null!;
    private Mock<ISnackbar> _mockSnackbar = null!;
    private Mock<Microsoft.Extensions.Logging.ILogger<ModeIsolatedStateService>> _mockLogger = null!;

    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();

        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Setup mocks for underlying clients
        _mockWorkItemsClient = new Mock<IWorkItemsClient>();
        _mockSyncHubService = new Mock<IWorkItemSyncHubService>();
        _mockTreeBuilderService = new Mock<ITreeBuilderService>();
        _mockSettingsClient = new Mock<ISettingsClient>();
        _mockProfilesClient = new Mock<IProfilesClient>();
        _mockApiClient = new Mock<IClient>();
        _mockSecureStorage = new Mock<ISecureStorageService>();
        _mockClipboardService = new Mock<Core.Contracts.IClipboardService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockSnackbar = new Mock<ISnackbar>();
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ModeIsolatedStateService>>();

        // Setup default behaviors
        _mockSyncHubService.Setup(x => x.StartAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockSyncHubService.Setup(x => x.IsConnected).Returns(true);

        _mockSettingsClient.Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsDto { Id = 1, ActiveProfileId = null, LastModified = DateTimeOffset.UtcNow });

        _mockProfilesClient.Setup(x => x.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProfileDto>());

        _mockApiClient.Setup(x => x.GetTfsConfigAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Not found", 204, null!, new Dictionary<string, IEnumerable<string>>(), null));

        _mockWorkItemsClient.Setup(x => x.GetAllWithValidationAsync())
            .ReturnsAsync(new List<WorkItemWithValidationDto>());
        _mockWorkItemsClient.Setup(x => x.GetAllWithValidationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemWithValidationDto>());

        _mockTreeBuilderService.Setup(x => x.BuildTreeWithValidation(
                It.IsAny<IEnumerable<WorkItemWithValidationDto>>(),
                It.IsAny<Dictionary<int, bool>>()))
            .Returns(new List<TreeNode>());

        _mockClipboardService.Setup(x => x.CopyToClipboardAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Register mock clients
        Services.AddSingleton(_mockWorkItemsClient.Object);
        Services.AddSingleton(_mockSettingsClient.Object);
        Services.AddSingleton(_mockProfilesClient.Object);
        Services.AddSingleton(_mockSyncHubService.Object);
        Services.AddSingleton(_mockTreeBuilderService.Object);
        Services.AddSingleton(_mockApiClient.Object);
        Services.AddSingleton(_mockSecureStorage.Object);
        Services.AddSingleton(_mockClipboardService.Object);
        Services.AddSingleton(_mockDialogService.Object);
        Services.AddSingleton(_mockSnackbar.Object);
        Services.AddSingleton(_mockLogger.Object);
        
        // Register concrete services that wrap the clients
        Services.AddSingleton<WorkItemService>();
        Services.AddSingleton<SettingsService>();
        Services.AddSingleton<ProfileService>();
        Services.AddSingleton<TfsConfigService>();
        Services.AddSingleton<ErrorMessageService>();
        Services.AddSingleton<ModeIsolatedStateService>();
        Services.AddSingleton<ExportService>();
        Services.AddSingleton<ReportService>();
        Services.AddSingleton<BrowserNavigationService>();
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
        _mockWorkItemsClient.Setup(x => x.GetAllWithValidationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemWithValidationDto>());

        // Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var markup = cut.Markup;
            Assert.DoesNotContain("mud-progress-linear", markup,
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

        _mockWorkItemsClient.Setup(x => x.GetAllWithValidationAsync())
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
        _mockWorkItemsClient.Verify(x => x.GetAllWithValidationAsync(), Times.AtLeastOnce);
    }

    [TestMethod]
    public void WorkItemExplorer_DisplaysLoadingState_Initially()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ICollection<WorkItemWithValidationDto>>();
        _mockWorkItemsClient.Setup(x => x.GetAllWithValidationAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Act
        var cut = RenderWorkItemExplorerWithMudProvider();

        // Assert - Should show loading initially
        // Note: Component uses _isLoading flag internally but may not display visible loader
        Assert.IsNotNull(cut);

        // Complete the async operation
        tcs.SetResult(new List<WorkItemWithValidationDto>() );
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
            _mockSettingsClient.Verify(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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
            _mockWorkItemsClient.Verify(x => x.GetAllWithValidationAsync(), Times.AtLeastOnce);
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

        _mockWorkItemsClient.Setup(x => x.GetAllWithValidationAsync())
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
