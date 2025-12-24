using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class BacklogHealthCalculationServiceTests
{
    private BacklogHealthCalculationService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _service = new BacklogHealthCalculationService();
    }

    private BacklogHealthDto CreateHealthDto(
        int totalWorkItems,
        int workItemsWithoutEffort = 0,
        int workItemsInProgressWithoutEffort = 0,
        int parentProgressIssues = 0,
        int blockedItems = 0)
    {
        return new BacklogHealthDto
        {
            SprintName = "Sprint 1",
            IterationPath = "Project\\Sprint 1",
            TotalWorkItems = totalWorkItems,
            WorkItemsWithoutEffort = workItemsWithoutEffort,
            WorkItemsInProgressWithoutEffort = workItemsInProgressWithoutEffort,
            ParentProgressIssues = parentProgressIssues,
            BlockedItems = blockedItems,
            InProgressAtIterationEnd = 0,
            ValidationIssues = new List<ValidationIssueSummary>()
        };
    }

    [TestMethod]
    public void CalculateHealthScore_NoWorkItems_Returns100()
    {
        // Arrange
        var iteration = CreateHealthDto(totalWorkItems: 0);

        // Act
        var score = _service.CalculateHealthScore(iteration);

        // Assert
        Assert.AreEqual(100, score);
    }

    [TestMethod]
    public void CalculateHealthScore_NoIssues_Returns100()
    {
        // Arrange
        var iteration = CreateHealthDto(totalWorkItems: 10);

        // Act
        var score = _service.CalculateHealthScore(iteration);

        // Assert
        Assert.AreEqual(100, score);
    }

    [TestMethod]
    public void CalculateHealthScore_SomeIssues_ReturnsReducedScore()
    {
        // Arrange
        var iteration = CreateHealthDto(
            totalWorkItems: 10,
            workItemsWithoutEffort: 2, // 20% have issues
            parentProgressIssues: 1    // 10% have issues
        );
        // Total issues: 3 out of 10 = 30%
        // Score: 100 - 30 = 70

        // Act
        var score = _service.CalculateHealthScore(iteration);

        // Assert
        Assert.AreEqual(70, score);
    }

    [TestMethod]
    public void CalculateHealthScore_AllItemsHaveIssues_Returns0()
    {
        // Arrange
        var iteration = CreateHealthDto(
            totalWorkItems: 10,
            workItemsWithoutEffort: 5,
            workItemsInProgressWithoutEffort: 3,
            parentProgressIssues: 1,
            blockedItems: 1
        );
        // Total issues: 10 out of 10 = 100%
        // Score: 100 - 100 = 0

        // Act
        var score = _service.CalculateHealthScore(iteration);

        // Assert
        Assert.AreEqual(0, score);
    }

    [TestMethod]
    public void CalculateHealthScore_MoreIssuesThanItems_Returns0()
    {
        // Arrange - edge case where issue count calculation exceeds total
        var iteration = CreateHealthDto(
            totalWorkItems: 5,
            workItemsWithoutEffort: 3,
            workItemsInProgressWithoutEffort: 2,
            parentProgressIssues: 1,
            blockedItems: 1
        );
        // Total issues: 7, but only 5 items (140%)
        // Score should never go below 0

        // Act
        var score = _service.CalculateHealthScore(iteration);

        // Assert
        Assert.IsGreaterThanOrEqualTo(score, 0);
    }

    [TestMethod]
    public void GetTrendColor_Improving_ReturnsSuccess()
    {
        // Act
        var color = _service.GetTrendColor(TrendDirection.Improving);

        // Assert
        Assert.AreEqual(Color.Success, color);
    }

    [TestMethod]
    public void GetTrendColor_Stable_ReturnsInfo()
    {
        // Act
        var color = _service.GetTrendColor(TrendDirection.Stable);

        // Assert
        Assert.AreEqual(Color.Info, color);
    }

    [TestMethod]
    public void GetTrendColor_Degrading_ReturnsError()
    {
        // Act
        var color = _service.GetTrendColor(TrendDirection.Degrading);

        // Assert
        Assert.AreEqual(Color.Error, color);
    }

    [TestMethod]
    public void GetTrendIcon_Improving_ReturnsTrendingUp()
    {
        // Act
        var icon = _service.GetTrendIcon(TrendDirection.Improving);

        // Assert
        Assert.AreEqual(Icons.Material.Filled.TrendingUp, icon);
    }

    [TestMethod]
    public void GetTrendIcon_Stable_ReturnsTrendingFlat()
    {
        // Act
        var icon = _service.GetTrendIcon(TrendDirection.Stable);

        // Assert
        Assert.AreEqual(Icons.Material.Filled.TrendingFlat, icon);
    }

    [TestMethod]
    public void GetTrendIcon_Degrading_ReturnsTrendingDown()
    {
        // Act
        var icon = _service.GetTrendIcon(TrendDirection.Degrading);

        // Assert
        Assert.AreEqual(Icons.Material.Filled.TrendingDown, icon);
    }

    [TestMethod]
    public void GetSeverityColor_Error_ReturnsError()
    {
        // Act
        var color = _service.GetSeverityColor("Error");

        // Assert
        Assert.AreEqual(Color.Error, color);
    }

    [TestMethod]
    public void GetSeverityColor_Warning_ReturnsWarning()
    {
        // Act
        var color = _service.GetSeverityColor("Warning");

        // Assert
        Assert.AreEqual(Color.Warning, color);
    }

    [TestMethod]
    public void GetSeverityColor_CaseInsensitive_ReturnsCorrectColor()
    {
        // Act
        var color = _service.GetSeverityColor("ERROR");

        // Assert
        Assert.AreEqual(Color.Error, color);
    }

    [TestMethod]
    public void GetSeverityColor_UnknownSeverity_ReturnsInfo()
    {
        // Act
        var color = _service.GetSeverityColor("Unknown");

        // Assert
        Assert.AreEqual(Color.Info, color);
    }

    [TestMethod]
    public void GenerateComparisonChartData_CreatesCorrectSeries()
    {
        // Arrange
        var healthData = new MultiIterationBacklogHealthDto
        {
            IterationHealth = new List<BacklogHealthDto>
            {
                CreateHealthDto(10, workItemsWithoutEffort: 2, parentProgressIssues: 1, blockedItems: 0),
                CreateHealthDto(15, workItemsWithoutEffort: 3, parentProgressIssues: 2, blockedItems: 1)
            },
            Trend = new BacklogHealthTrend
            {
                EffortTrend = TrendDirection.Stable,
                ValidationTrend = TrendDirection.Stable,
                BlockerTrend = TrendDirection.Stable,
                Summary = "Test"
            },
            TotalWorkItems = 25,
            TotalIssues = 9
        };

        // Act
        var series = _service.GenerateComparisonChartData(healthData);

        // Assert
        Assert.HasCount(3, series);
        Assert.AreEqual("Without Effort", series[0].Name);
        Assert.AreEqual("Parent Issues", series[1].Name);
        Assert.AreEqual("Blocked", series[2].Name);

        // Check data values for first iteration
        Assert.AreEqual(2.0, series[0].Data[0]);
        Assert.AreEqual(1.0, series[1].Data[0]);
        Assert.AreEqual(0.0, series[2].Data[0]);

        // Check data values for second iteration
        Assert.AreEqual(3.0, series[0].Data[1]);
        Assert.AreEqual(2.0, series[1].Data[1]);
        Assert.AreEqual(1.0, series[2].Data[1]);
    }

    [TestMethod]
    public void GetIterationLabels_ExtractsSprintNames()
    {
        // Arrange
        var healthData = new MultiIterationBacklogHealthDto
        {
            IterationHealth = new List<BacklogHealthDto>
            {
                new() { SprintName = "Sprint 1", IterationPath = "Path1", TotalWorkItems = 0, ValidationIssues = new List<ValidationIssueSummary>() },
                new() { SprintName = "Sprint 2", IterationPath = "Path2", TotalWorkItems = 0, ValidationIssues = new List<ValidationIssueSummary>() },
                new() { SprintName = "Sprint 3", IterationPath = "Path3", TotalWorkItems = 0, ValidationIssues = new List<ValidationIssueSummary>() }
            },
            Trend = new BacklogHealthTrend
            {
                EffortTrend = TrendDirection.Stable,
                ValidationTrend = TrendDirection.Stable,
                BlockerTrend = TrendDirection.Stable,
                Summary = "Test"
            },
            TotalWorkItems = 0,
            TotalIssues = 0
        };

        // Act
        var labels = _service.GetIterationLabels(healthData);

        // Assert
        Assert.HasCount(3, labels);
        Assert.AreEqual("Sprint 1", labels[0]);
        Assert.AreEqual("Sprint 2", labels[1]);
        Assert.AreEqual("Sprint 3", labels[2]);
    }
}
