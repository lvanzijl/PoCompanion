using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Pages.Metrics;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for VelocityDashboard page component
/// </summary>
[TestClass]
public class VelocityDashboardTests : BunitTestContext
{
    private Mock<IMetricsClient> _mockMetricsClient = null!;
    private Mock<ISnackbar> _mockSnackbar = null!;

    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();

        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Setup mocks
        _mockMetricsClient = new Mock<IMetricsClient>();
        _mockSnackbar = new Mock<ISnackbar>();

        // Register mock services
        Services.AddSingleton(_mockMetricsClient.Object);
        Services.AddSingleton(_mockSnackbar.Object);
    }

    private IRenderedFragment RenderVelocityDashboardWithMudProvider()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<VelocityDashboard>(1);
            builder.CloseComponent();
        });
    }

    [TestMethod]
    public void VelocityDashboard_RendersCorrectly_WithEmptyData()
    {
        // Arrange
        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .Returns(Task.FromResult<VelocityTrendDto>(null!));

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for empty state to display
        cut.WaitForState(() => cut.Markup.Contains("No Velocity Data Available"),
            timeout: TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("No Velocity Data Available", cut.Markup);
        Assert.Contains("Velocity Dashboard", cut.Markup);
    }

    [TestMethod]
    public void VelocityDashboard_RendersCorrectly_WithVelocityData()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("Velocity Dashboard", cut.Markup);
        Assert.DoesNotContain("No Velocity Data Available", cut.Markup);
    }

    [TestMethod]
    public void VelocityDashboard_DisplaysMetricSummaryCards()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("Average Velocity", cut.Markup);
        Assert.Contains("Last 3 Sprints", cut.Markup);
        Assert.Contains("Total Sprints", cut.Markup);
        Assert.Contains("Total Completed", cut.Markup);
    }

    [TestMethod]
    public void VelocityDashboard_DisplaysVelocityTrendChart()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("Velocity Trend", cut.Markup);
        var charts = cut.FindComponents<MudChart>();
        Assert.AreNotEqual(0, charts.Count, "Should render velocity trend chart");
    }

    [TestMethod]
    public void VelocityDashboard_DisplaysSprintDetailsTable()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("Sprint Details", cut.Markup);
        Assert.Contains("Sprint 1", cut.Markup);
        Assert.Contains("Sprint 2", cut.Markup);
        Assert.Contains("Completed Points", cut.Markup);
        Assert.Contains("Planned Points", cut.Markup);
    }

    [TestMethod]
    public void VelocityDashboard_HasRefreshButton()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("Refresh Metrics", cut.Markup);
    }

    [TestMethod]
    public void VelocityDashboard_LoadsData_OnInitialization()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            _mockMetricsClient.Verify(
                x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()),
                Times.Once);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void VelocityDashboard_DisplaysAverageVelocityCorrectly()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert - Average velocity should be 27.5
        Assert.Contains("27.5", cut.Markup);
    }

    [TestMethod]
    public void VelocityDashboard_DisplaysThreeSprintAverage()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert - 3-sprint average should be 26.7
        Assert.Contains("26.7", cut.Markup);
    }

    [TestMethod]
    public void VelocityDashboard_DisplaysCompletionPercentage()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("Completion %", cut.Markup);
    }

    [TestMethod]
    public void VelocityDashboard_DisplaysContextualHelp()
    {
        // Arrange
        var velocityData = CreateVelocityTrendData();

        _mockMetricsClient.Setup(x => x.GetVelocityTrendAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(velocityData);

        // Act
        var cut = RenderVelocityDashboardWithMudProvider();

        // Wait for async initialization
        cut.WaitForState(() => !cut.Markup.Contains("No Velocity Data Available"), timeout: TimeSpan.FromSeconds(10));

        // Assert
        Assert.Contains("Velocity Dashboard Guide", cut.Markup);
    }

    // Helper method to create test velocity data
    private VelocityTrendDto CreateVelocityTrendData()
    {
        return new VelocityTrendDto
        {
            AverageVelocity = 27.5,
            ThreeSprintAverage = 26.7,
            TotalSprints = 3,
            TotalCompletedStoryPoints = 82,
            Sprints = new List<SprintMetricsDto>
            {
                new SprintMetricsDto
                {
                    SprintName = "Sprint 1",
                    IterationPath = "Project\\2025\\Sprint 1",
                    CompletedStoryPoints = 25,
                    PlannedStoryPoints = 30,
                    CompletedWorkItemCount = 12,
                    TotalWorkItemCount = 15,
                    CompletedPBIs = 8,
                    CompletedBugs = 2,
                    CompletedTasks = 2
                },
                new SprintMetricsDto
                {
                    SprintName = "Sprint 2",
                    IterationPath = "Project\\2025\\Sprint 2",
                    CompletedStoryPoints = 30,
                    PlannedStoryPoints = 32,
                    CompletedWorkItemCount = 14,
                    TotalWorkItemCount = 16,
                    CompletedPBIs = 9,
                    CompletedBugs = 3,
                    CompletedTasks = 2
                },
                new SprintMetricsDto
                {
                    SprintName = "Sprint 3",
                    IterationPath = "Project\\2025\\Sprint 3",
                    CompletedStoryPoints = 27,
                    PlannedStoryPoints = 28,
                    CompletedWorkItemCount = 13,
                    TotalWorkItemCount = 14,
                    CompletedPBIs = 8,
                    CompletedBugs = 2,
                    CompletedTasks = 3
                }
            }
        };
    }
}
