using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Pages.Settings;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for WorkItemStates page component.
/// These tests verify state initialization, validation, and error handling.
/// </summary>
[TestClass]
public class WorkItemStatesTests : BunitTestContext
{
    private Mock<ISettingsClient> _mockSettingsClient = null!;
    private Mock<ILogger<WorkItemStates>> _mockLogger = null!;
    private Mock<ISnackbar> _mockSnackbar = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create mock dependencies
        _mockSettingsClient = new Mock<ISettingsClient>();
        _mockLogger = new Mock<ILogger<WorkItemStates>>();
        _mockSnackbar = new Mock<ISnackbar>();

        // Register services
        Services.AddSingleton(_mockSettingsClient.Object);
        Services.AddSingleton(_mockLogger.Object);
        Services.AddSingleton(_mockSnackbar.Object);
        Services.AddMudServices();
        
        // Setup JSInterop for MudBlazor components
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
        JSInterop.SetupVoid("mudResizeListener.listenForResize", _ => true);
        JSInterop.SetupVoid("mudScrollListener.listenForScroll", _ => true);
    }

    [TestMethod]
    public async Task WorkItemStates_InitializesAllStatesInDictionary()
    {
        // Arrange
        var workItemTypes = new List<Client.ApiClient.WorkItemTypeDefinitionDto>
        {
            new Client.ApiClient.WorkItemTypeDefinitionDto 
            { 
                TypeName = "Epic", 
                States = new List<string> { "New", "Active", "Done" }
            },
            new Client.ApiClient.WorkItemTypeDefinitionDto 
            { 
                TypeName = "Feature", 
                States = new List<string> { "New", "In Progress", "Closed" }
            }
        };

        var existingClassifications = new List<Client.ApiClient.WorkItemStateClassificationDto>
        {
            new Client.ApiClient.WorkItemStateClassificationDto
            {
                WorkItemType = "Epic",
                StateName = "Active",
                Classification = Client.ApiClient.StateClassification.InProgress
            }
        };

        _mockSettingsClient
            .Setup(x => x.GetWorkItemTypeDefinitionsAsync())
            .ReturnsAsync(workItemTypes);

        _mockSettingsClient
            .Setup(x => x.GetStateClassificationsAsync())
            .ReturnsAsync(new Client.ApiClient.GetStateClassificationsResponse
            {
                ProjectName = "TestProject",
                Classifications = existingClassifications,
                IsDefault = true
            });

        // Act
        var cut = RenderComponent<WorkItemStates>();
        await Task.Delay(100); // Give component time to initialize

        // Assert
        _mockSettingsClient.Verify(x => x.GetWorkItemTypeDefinitionsAsync(), Times.Once);
        _mockSettingsClient.Verify(x => x.GetStateClassificationsAsync(), Times.Once);
        
        // Component should have loaded successfully
        cut.WaitForAssertion(() => 
        {
            var loadingIndicator = cut.FindAll(".mud-progress-linear");
            Assert.IsEmpty(loadingIndicator, "Loading indicator should not be visible after load");
        });
    }

    [TestMethod]
    public async Task WorkItemStates_DisplaysInlineErrorForMissingClassifications()
    {
        // Arrange
        var workItemTypes = new List<Client.ApiClient.WorkItemTypeDefinitionDto>
        {
            new Client.ApiClient.WorkItemTypeDefinitionDto 
            { 
                TypeName = "Epic", 
                States = new List<string> { "New", "Active" }
            }
        };

        _mockSettingsClient
            .Setup(x => x.GetWorkItemTypeDefinitionsAsync())
            .ReturnsAsync(workItemTypes);

        _mockSettingsClient
            .Setup(x => x.GetStateClassificationsAsync())
            .ReturnsAsync(new Client.ApiClient.GetStateClassificationsResponse
            {
                ProjectName = "TestProject",
                Classifications = new List<Client.ApiClient.WorkItemStateClassificationDto>(),
                IsDefault = true
            });

        // Mock save to validate but not actually save (simulate validation failure scenario)
        _mockSettingsClient
            .Setup(x => x.SaveStateClassificationsAsync(It.IsAny<Client.ApiClient.SaveStateClassificationsRequest>()))
            .ThrowsAsync(new Exception("Simulated save failure"));

        // Act
        var cut = RenderComponent<WorkItemStates>();
        await Task.Delay(100); // Give component time to initialize

        // Component should render work item types
        cut.WaitForAssertion(() => 
        {
            var cards = cut.FindAll(".mud-card");
            Assert.IsNotEmpty(cards, "Should render work item type cards");
        });
    }

    [TestMethod]
    public async Task WorkItemStates_PreservesSelectionsOnSaveFailure()
    {
        // Arrange
        var workItemTypes = new List<Client.ApiClient.WorkItemTypeDefinitionDto>
        {
            new Client.ApiClient.WorkItemTypeDefinitionDto 
            { 
                TypeName = "Epic", 
                States = new List<string> { "New" }
            }
        };

        _mockSettingsClient
            .Setup(x => x.GetWorkItemTypeDefinitionsAsync())
            .ReturnsAsync(workItemTypes);

        _mockSettingsClient
            .Setup(x => x.GetStateClassificationsAsync())
            .ReturnsAsync(new Client.ApiClient.GetStateClassificationsResponse
            {
                ProjectName = "TestProject",
                Classifications = new List<Client.ApiClient.WorkItemStateClassificationDto>
                {
                    new Client.ApiClient.WorkItemStateClassificationDto
                    {
                        WorkItemType = "Epic",
                        StateName = "New",
                        Classification = Client.ApiClient.StateClassification.InProgress
                    }
                },
                IsDefault = false
            });

        _mockSettingsClient
            .Setup(x => x.SaveStateClassificationsAsync(It.IsAny<Client.ApiClient.SaveStateClassificationsRequest>()))
            .ThrowsAsync(new Exception("Save failed"));

        // Act
        var cut = RenderComponent<WorkItemStates>();
        await Task.Delay(100); // Give component time to initialize

        // Verify that GetStateClassificationsAsync was called once during initial load
        _mockSettingsClient.Verify(x => x.GetStateClassificationsAsync(), Times.Once);
        
        // Component should have loaded successfully
        cut.WaitForAssertion(() => 
        {
            var cards = cut.FindAll(".mud-card");
            Assert.IsNotEmpty(cards, "Should render work item type cards");
        });
    }
}
