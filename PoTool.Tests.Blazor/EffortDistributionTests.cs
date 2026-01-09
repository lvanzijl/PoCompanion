using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Pages.Metrics;
using PoTool.Client.Services;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for EffortDistribution page component
/// </summary>
[TestClass]
public class EffortDistributionTests : BunitTestContext
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

        // Mock IHealthCalculationClient for BacklogHealthCalculationService
        var mockHealthCalculationClient = new Mock<IHealthCalculationClient>();

        // Register mock services
        Services.AddSingleton(_mockMetricsClient.Object);
        Services.AddSingleton(mockHealthCalculationClient.Object);
        Services.AddSingleton<BacklogHealthCalculationService>();
        Services.AddSingleton<ErrorMessageService>();
        Services.AddSingleton(_mockSnackbar.Object);
    }

    private IRenderedFragment RenderEffortDistributionWithMudProvider()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<EffortDistribution>(1);
            builder.CloseComponent();
        });
    }

    [TestMethod]
    public void EffortDistribution_RendersCorrectly_WithEmptyData()
    {
        // Arrange
        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Returns(Task.FromResult<EffortDistributionDto>(null!));

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup,
                "Loading indicator should be gone after data loads");
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("No Effort Distribution Data Available", cut.Markup);
        Assert.Contains("Effort Distribution Heat Map", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_RendersCorrectly_WithDistributionData()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup,
                "Loading indicator should be gone after data loads");
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Effort Distribution Heat Map", cut.Markup);
        Assert.DoesNotContain("No Effort Distribution Data Available", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_DisplaysSummaryCards()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Total Effort", cut.Markup);
        Assert.Contains("Area Paths", cut.Markup);
        Assert.Contains("Iterations", cut.Markup);
        Assert.Contains("Avg Utilization", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_DisplaysHeatMap()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Effort Heat Map (Area Path × Iteration)", cut.Markup);
        Assert.Contains("Team A", cut.Markup);
        Assert.Contains("Team B", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_HasAreaPathFilter()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Area Path Filter", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_HasMaxIterationsFilter()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Max Iterations", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_HasDefaultCapacityFilter()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Default Capacity", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_HasRefreshButton()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Refresh", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_DisplaysIterationChart()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Effort by Iteration", cut.Markup);
        var charts = cut.FindComponents<MudChart>();
        Assert.AreNotEqual(0, charts.Count, "Should render charts");
    }

    [TestMethod]
    public void EffortDistribution_DisplaysAreaPathChart()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Effort by Area Path", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_DisplaysUtilizationTable()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Iteration Utilization", cut.Markup);
        Assert.Contains("Utilization", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_LoadsData_OnInitialization()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            _mockMetricsClient.Verify(
                x => x.GetEffortDistributionAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()),
                Times.Once);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void EffortDistribution_DisplaysContextualHelp()
    {
        // Arrange
        var distributionData = CreateEffortDistributionData();

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Effort Distribution Heat Map Guide", cut.Markup);
    }

    [TestMethod]
    public void EffortDistribution_HandlesEmptyIterations_WithoutException()
    {
        // Arrange - Create distribution data with empty iterations collection
        var distributionData = new EffortDistributionDto
        {
            TotalEffort = 0,
            EffortByArea = new List<EffortByAreaPath>(),
            EffortByIteration = new List<EffortByIteration>(),
            HeatMapData = new List<EffortHeatMapCell>()
        };

        _mockMetricsClient.Setup(x => x.GetEffortDistributionAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(distributionData);

        // Act - Should not throw InvalidOperationException
        var cut = RenderEffortDistributionWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert - Page renders without exception
        Assert.Contains("Effort Distribution Heat Map", cut.Markup);
        Assert.Contains("Total Effort", cut.Markup);
        Assert.Contains("Avg Utilization", cut.Markup);
    }

    // Helper method to create test effort distribution data
    private EffortDistributionDto CreateEffortDistributionData()
    {
        return new EffortDistributionDto
        {
            TotalEffort = 150,
            EffortByArea = new List<EffortByAreaPath>
            {
                new EffortByAreaPath
                {
                    AreaPath = "Project\\Team A",
                    TotalEffort = 80,
                    WorkItemCount = 20,
                    AverageEffortPerItem = 4.0
                },
                new EffortByAreaPath
                {
                    AreaPath = "Project\\Team B",
                    TotalEffort = 70,
                    WorkItemCount = 18,
                    AverageEffortPerItem = 3.89
                }
            },
            EffortByIteration = new List<EffortByIteration>
            {
                new EffortByIteration
                {
                    SprintName = "Sprint 1",
                    IterationPath = "Project\\2025\\Sprint 1",
                    TotalEffort = 75,
                    WorkItemCount = 19,
                    Capacity = 80,
                    UtilizationPercentage = 93.75
                },
                new EffortByIteration
                {
                    SprintName = "Sprint 2",
                    IterationPath = "Project\\2025\\Sprint 2",
                    TotalEffort = 75,
                    WorkItemCount = 19,
                    Capacity = 80,
                    UtilizationPercentage = 93.75
                }
            },
            HeatMapData = new List<EffortHeatMapCell>
            {
                new EffortHeatMapCell
                {
                    AreaPath = "Project\\Team A",
                    IterationPath = "Project\\2025\\Sprint 1",
                    Effort = 40,
                    WorkItemCount = 10,
                    Status = CapacityStatus.Normal
                },
                new EffortHeatMapCell
                {
                    AreaPath = "Project\\Team A",
                    IterationPath = "Project\\2025\\Sprint 2",
                    Effort = 40,
                    WorkItemCount = 10,
                    Status = CapacityStatus.Normal
                },
                new EffortHeatMapCell
                {
                    AreaPath = "Project\\Team B",
                    IterationPath = "Project\\2025\\Sprint 1",
                    Effort = 35,
                    WorkItemCount = 9,
                    Status = CapacityStatus.Normal
                },
                new EffortHeatMapCell
                {
                    AreaPath = "Project\\Team B",
                    IterationPath = "Project\\2025\\Sprint 2",
                    Effort = 35,
                    WorkItemCount = 9,
                    Status = CapacityStatus.Normal
                }
            }
        };
    }
}
