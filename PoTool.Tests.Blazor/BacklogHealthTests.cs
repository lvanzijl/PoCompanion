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
/// bUnit tests for BacklogHealth page component
/// </summary>
[TestClass]
public class BacklogHealthTests : BunitTestContext
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
        // Setup mock to return a proper response object (not null) - match any call signature
        mockHealthCalculationClient.Setup(x => x.CalculateHealthScoreAsync(
                It.IsAny<CalculateHealthScoreRequest>()))
            .ReturnsAsync(new CalculateHealthScoreResponse { HealthScore = 80 });
        mockHealthCalculationClient.Setup(x => x.CalculateHealthScoreAsync(
                It.IsAny<CalculateHealthScoreRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalculateHealthScoreResponse { HealthScore = 80 });
        
        // Mock IWorkItemsClient for WorkItemService (needed by BacklogHealthFilters child component)
        var mockWorkItemsClient = new Mock<IWorkItemsClient>();
        var mockHttpClient = new HttpClient { BaseAddress = new Uri("http://localhost/") };
        
        // Register mock services
        Services.AddSingleton(_mockMetricsClient.Object);
        Services.AddSingleton(mockHealthCalculationClient.Object);
        Services.AddSingleton<BacklogHealthCalculationService>();
        Services.AddSingleton(mockWorkItemsClient.Object);
        Services.AddSingleton(mockHttpClient);
        Services.AddSingleton<WorkItemService>();
        Services.AddSingleton<ErrorMessageService>();
        Services.AddSingleton(_mockSnackbar.Object);
    }

    private IRenderedFragment RenderBacklogHealthWithMudProvider()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<BacklogHealth>(1);
            builder.CloseComponent();
        });
    }

    [TestMethod]
    public void BacklogHealth_RendersCorrectly_WithEmptyData()
    {
        // Arrange
        var emptyData = new MultiIterationBacklogHealthDto
        {
            IterationHealth = new List<BacklogHealthDto>(),
            TotalWorkItems = 0,
            TotalIssues = 0,
            Trend = new BacklogHealthTrend
            {
                Summary = "No data available",
                EffortTrend = TrendDirection.Stable,
                ValidationTrend = TrendDirection.Stable,
                BlockerTrend = TrendDirection.Stable
            }
        };

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(emptyData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup,
                "Loading indicator should be gone after data loads");
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("No Backlog Health Data Available", cut.Markup);
        Assert.Contains("Backlog Health Dashboard", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealth_RendersCorrectly_WithHealthData()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup,
                "Loading indicator should be gone after data loads");
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Backlog Health Dashboard", cut.Markup);
        Assert.Contains("Sprint 1", cut.Markup);
        Assert.DoesNotContain("No Backlog Health Data Available", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealth_DisplaysIterationCards()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Sprint 1", cut.Markup);
        Assert.Contains("Sprint 2", cut.Markup);
        Assert.Contains("Total Work Items", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealth_DisplaysTrendSummary()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Health improving", cut.Markup);
        Assert.Contains("Effort: Improving", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealth_HasAreaPathFilter()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Area Path Filter", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealth_HasMaxIterationsFilter()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Max Iterations", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealth_HasRefreshButton()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Refresh", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealth_DisplaysComparisonChart()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Issue Comparison Across Iterations", cut.Markup);
        var charts = cut.FindComponents<MudChart>();
        Assert.AreNotEqual(0, charts.Count, "Should render comparison chart");
    }

    [TestMethod]
    public void BacklogHealth_DisplaysValidationIssues()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Validation Issues", cut.Markup);
    }

    [TestMethod]
    public void BacklogHealth_LoadsData_OnInitialization()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            _mockMetricsClient.Verify(
                x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()),
                Times.Once);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void BacklogHealth_DisplaysContextualHelp()
    {
        // Arrange
        var healthData = CreateMultiIterationHealthData();

        _mockMetricsClient.Setup(x => x.GetMultiIterationBacklogHealthAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(healthData);

        // Act
        var cut = RenderBacklogHealthWithMudProvider();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("mud-progress-linear", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Backlog Health Dashboard Guide", cut.Markup);
    }

    // Helper method to create test health data
    private MultiIterationBacklogHealthDto CreateMultiIterationHealthData()
    {
        return new MultiIterationBacklogHealthDto
        {
            IterationHealth = new List<BacklogHealthDto>
            {
                new BacklogHealthDto
                {
                    SprintName = "Sprint 1",
                    IterationPath = "Project\\2025\\Sprint 1",
                    TotalWorkItems = 20,
                    WorkItemsWithoutEffort = 3,
                    WorkItemsInProgressWithoutEffort = 1,
                    ParentProgressIssues = 2,
                    BlockedItems = 1,
                    InProgressAtIterationEnd = 4,
                    ValidationIssues = new List<ValidationIssueSummary>
                    {
                        new ValidationIssueSummary { ValidationType = "Error", Count = 3, AffectedWorkItemIds = new List<int>() }
                    }
                },
                new BacklogHealthDto
                {
                    SprintName = "Sprint 2",
                    IterationPath = "Project\\2025\\Sprint 2",
                    TotalWorkItems = 25,
                    WorkItemsWithoutEffort = 2,
                    WorkItemsInProgressWithoutEffort = 0,
                    ParentProgressIssues = 1,
                    BlockedItems = 0,
                    InProgressAtIterationEnd = 3,
                    ValidationIssues = new List<ValidationIssueSummary>
                    {
                        new ValidationIssueSummary { ValidationType = "Warning", Count = 2, AffectedWorkItemIds = new List<int>() }
                    }
                }
            },
            TotalWorkItems = 45,
            TotalIssues = 6,
            Trend = new BacklogHealthTrend
            {
                Summary = "Health improving",
                EffortTrend = TrendDirection.Improving,
                ValidationTrend = TrendDirection.Improving,
                BlockerTrend = TrendDirection.Stable
            }
        };
    }
}
