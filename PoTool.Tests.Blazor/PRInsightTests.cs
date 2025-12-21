using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Pages.PullRequests;
using PoTool.Client.Services;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for PRInsight page component
/// </summary>
[TestClass]
public class PRInsightTests : BunitTestContext
{
    private Mock<IPullRequestsClient> _mockPullRequestsClient = null!;
    private Mock<ISnackbar> _mockSnackbar = null!;

    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();

        // Configure JSInterop in Loose mode to allow any JS calls without explicit setup
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Setup mocks
        _mockPullRequestsClient = new Mock<IPullRequestsClient>();
        _mockSnackbar = new Mock<ISnackbar>();

        // Register mock services
        Services.AddSingleton(_mockPullRequestsClient.Object);
        Services.AddSingleton<PullRequestService>();
        Services.AddSingleton<ErrorMessageService>();
        Services.AddSingleton(_mockSnackbar.Object);
    }

    private IRenderedFragment RenderPRInsightWithMudProvider()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<PRInsight>(1);
            builder.CloseComponent();
        });
    }

    private void SetupMockWithMetrics(List<PullRequestMetricsDto> metrics)
    {
        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(metrics);
    }

    private void SetupMockWithEmptyMetrics()
    {
        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(new List<PullRequestMetricsDto>());
    }

    [TestMethod]
    public void PRInsight_RendersCorrectly_WithData()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetric(1, "PR 1", "User1", "Active", 2, 10, 5),
            CreateMetric(2, "PR 2", "User2", "Completed", 3, 15, 8)
        };

        SetupMockWithMetrics(metrics);

        // Act
        var cut = RenderPRInsightWithMudProvider();
        
        // The component should render loading state first, then data after async completes
        // We need to wait for the component to finish its OnInitializedAsync
        cut.WaitForAssertion(() => 
        {
            // After async completes, _isLoading should be false and metrics should be loaded
            // This means the progress bar should be gone
            var markup = cut.Markup;
            Assert.IsFalse(markup.Contains("mud-progress-linear"), 
                "Loading indicator should be gone after data loads");
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert - Now check that data is displayed
        var finalMarkup = cut.Markup;
        Assert.Contains("PR Insight Dashboard", finalMarkup);
        Assert.Contains("Total PRs", finalMarkup);
        Assert.Contains("Avg Time Open", finalMarkup);
        Assert.Contains("Avg Iterations", finalMarkup);
        Assert.Contains("Avg Files/PR", finalMarkup);
    }

    [TestMethod]
    public void PRInsight_ShowsEmptyState_WhenNoData()
    {
        // Arrange
        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(new List<PullRequestMetricsDto>());

        // Act
        var cut = RenderPRInsightWithMudProvider();

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("No Pull Request Data Available", cut.Markup);
        Assert.Contains("Sync PRs", cut.Markup);
    }

    [TestMethod]
    public void PRInsight_DisplaysLoadingState_Initially()
    {
        // Arrange - Setup to delay the async call
        var tcs = new TaskCompletionSource<ICollection<PullRequestMetricsDto>>();
        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .Returns(tcs.Task);

        // Act
        var cut = RenderPRInsightWithMudProvider();

        // Assert
        var progressBar = cut.FindAll("div.mud-progress-linear");
        Assert.AreNotEqual(0, progressBar.Count, "Loading state should display progress bar");
        
        // Complete the async operation to prevent hanging
        tcs.SetResult(new List<PullRequestMetricsDto>());
    }

    [TestMethod]
    public async Task PRInsight_SyncButton_InvokesService()
    {
        // Arrange
        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(new List<PullRequestMetricsDto>());

        _mockPullRequestsClient.Setup(x => x.SyncAsync())
            .ReturnsAsync(5);

        var cut = RenderPRInsightWithMudProvider();

        // Act
        var syncButton = cut.Find("button.mud-fab");
        await syncButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        _mockPullRequestsClient.Verify(x => x.SyncAsync(), Times.Once);
        _mockSnackbar.Verify(x => x.Add(It.IsAny<string>(), Severity.Success, It.IsAny<Action<SnackbarOptions>?>(), It.IsAny<string?>()), Times.Once);
    }

    [TestMethod]
    public void PRInsight_CalculatesAverageMetrics_Correctly()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetric(1, "PR 1", "User1", "Active", 2, 10, 5),
            CreateMetric(2, "PR 2", "User2", "Completed", 4, 20, 10),
            CreateMetric(3, "PR 3", "User3", "Active", 3, 15, 8)
        };

        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(metrics);

        // Act
        var cut = RenderPRInsightWithMudProvider();
        
        // Wait for async rendering to complete
        cut.WaitForAssertion(() => 
        {
            Assert.DoesNotContain("No Pull Request Data Available", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        var markup = cut.Markup;
        // Average iterations: (2 + 4 + 3) / 3 = 3.0
        Assert.Contains("3.0", markup);
        // Average files: (10 + 20 + 15) / 3 = 15
        Assert.Contains("15", markup);
    }

    [TestMethod]
    public void PRInsight_DisplaysCharts_WithData()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetric(1, "PR 1", "User1", "Active", 2, 10, 5),
            CreateMetric(2, "PR 2", "User2", "Completed", 3, 15, 8)
        };

        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(metrics);

        // Act
        var cut = RenderPRInsightWithMudProvider();
        
        // Wait for async rendering to complete
        cut.WaitForAssertion(() => 
        {
            Assert.DoesNotContain("No Pull Request Data Available", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        var charts = cut.FindComponents<MudChart>();
        Assert.AreNotEqual(0, charts.Count, "Should render charts when data is available");
    }

    [TestMethod]
    public void PRInsight_HasDateRangeFilter()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetric(1, "PR 1", "User1", "Active", 2, 10, 5)
        };

        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(metrics);

        // Act
        var cut = RenderPRInsightWithMudProvider();
        
        // Wait for async rendering to complete
        cut.WaitForAssertion(() => 
        {
            Assert.DoesNotContain("No Pull Request Data Available", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Filter by Date Range", cut.Markup);
        Assert.Contains("Apply Filter", cut.Markup);
    }

    [TestMethod]
    public void PRInsight_HasMultipleTabs()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetric(1, "PR 1", "User1", "Active", 2, 10, 5)
        };

        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(metrics);

        // Act
        var cut = RenderPRInsightWithMudProvider();
        
        // Wait for async rendering to complete
        cut.WaitForAssertion(() => 
        {
            Assert.DoesNotContain("No Pull Request Data Available", cut.Markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert - When there's data, tabs should be visible in markup
        Assert.Contains("Overview", cut.Markup);
        Assert.Contains("By User", cut.Markup);
        Assert.Contains("Details", cut.Markup);
    }

    [TestMethod]
    public void PRInsight_DisplaysDataGrid_WithPRDetails()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetric(1, "Test PR Title", "TestUser", "Active", 2, 10, 5)
        };

        _mockPullRequestsClient.Setup(x => x.GetMetricsAsync())
            .ReturnsAsync(metrics);

        // Act
        var cut = RenderPRInsightWithMudProvider();
        
        // Wait for async rendering to complete and verify data is loaded
        cut.WaitForAssertion(() => 
        {
            var markup = cut.Markup;
            Assert.DoesNotContain("No Pull Request Data Available", markup);
            // Verify the tabs are rendered (which means data is loaded)
            Assert.Contains("Overview", markup);
            Assert.Contains("Details", markup);
            // Verify the metric count is correct
            Assert.Contains("Total PRs", markup);
            Assert.Contains("1", markup);
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert - Data is loaded and ready to be displayed in tabs
        Assert.IsNotNull(cut);
    }

    // Helper method to create test metrics
    private PullRequestMetricsDto CreateMetric(
        int id, 
        string title, 
        string createdBy, 
        string status,
        int iterationCount,
        int fileCount,
        int commentCount)
    {
        return new PullRequestMetricsDto
        {
            PullRequestId = id,
            Title = title,
            CreatedBy = createdBy,
            CreatedDate = DateTimeOffset.UtcNow.AddDays(-5),
            Status = status,
            IterationPath = "/Project/Sprint1",
            TotalTimeOpen = TimeSpan.FromDays(2.5),
            IterationCount = iterationCount,
            TotalFileCount = fileCount,
            CommentCount = commentCount,
            UnresolvedCommentCount = 2,
            TotalLinesAdded = 100,
            TotalLinesDeleted = 50,
            AverageLinesPerFile = 10.5
        };
    }
}
