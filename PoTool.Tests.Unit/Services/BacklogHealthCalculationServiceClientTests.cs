using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Shared.Metrics;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class BacklogHealthCalculationServiceClientTests
{
    private Mock<IHealthCalculationClient> _mockHealthCalculationClient = null!;
    private BacklogHealthCalculationService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockHealthCalculationClient = new Mock<IHealthCalculationClient>();
        _service = new BacklogHealthCalculationService(_mockHealthCalculationClient.Object);
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
    public async Task CalculateHealthScoreAsync_NoWorkItems_Returns100()
    {
        // Arrange
        var iteration = CreateHealthDto(totalWorkItems: 0);

        _mockHealthCalculationClient
            .Setup(x => x.CalculateHealthScoreAsync(It.IsAny<CalculateHealthScoreRequest>()))
            .ReturnsAsync(new CalculateHealthScoreResponse
            {
                HealthScore = 100,
                TotalIssues = 0
            });

        // Act
        var score = await _service.CalculateHealthScoreAsync(iteration);

        // Assert
        Assert.AreEqual(100, score);

        _mockHealthCalculationClient.Verify(x => x.CalculateHealthScoreAsync(
            It.Is<CalculateHealthScoreRequest>(r =>
                r.TotalWorkItems == 0 &&
                r.WorkItemsWithoutEffort == 0 &&
                r.WorkItemsInProgressWithoutEffort == 0 &&
                r.ParentProgressIssues == 0 &&
                r.BlockedItems == 0)), Times.Once);
    }

    [TestMethod]
    public async Task CalculateHealthScoreAsync_NoIssues_Returns100()
    {
        // Arrange
        var iteration = CreateHealthDto(totalWorkItems: 10);

        _mockHealthCalculationClient
            .Setup(x => x.CalculateHealthScoreAsync(It.IsAny<CalculateHealthScoreRequest>()))
            .ReturnsAsync(new CalculateHealthScoreResponse
            {
                HealthScore = 100,
                TotalIssues = 0
            });

        // Act
        var score = await _service.CalculateHealthScoreAsync(iteration);

        // Assert
        Assert.AreEqual(100, score);
    }

    [TestMethod]
    public async Task CalculateHealthScoreAsync_SomeIssues_ReturnsReducedScore()
    {
        // Arrange
        var iteration = CreateHealthDto(
            totalWorkItems: 10,
            workItemsWithoutEffort: 2,
            parentProgressIssues: 1
        );

        _mockHealthCalculationClient
            .Setup(x => x.CalculateHealthScoreAsync(It.IsAny<CalculateHealthScoreRequest>()))
            .ReturnsAsync(new CalculateHealthScoreResponse
            {
                HealthScore = 70,
                TotalIssues = 3
            });

        // Act
        var score = await _service.CalculateHealthScoreAsync(iteration);

        // Assert
        Assert.AreEqual(70, score);

        _mockHealthCalculationClient.Verify(x => x.CalculateHealthScoreAsync(
            It.Is<CalculateHealthScoreRequest>(r =>
                r.TotalWorkItems == 10 &&
                r.WorkItemsWithoutEffort == 2 &&
                r.ParentProgressIssues == 1)), Times.Once);
    }

    [TestMethod]
    public async Task CalculateHealthScoreAsync_AllItemsHaveIssues_Returns0()
    {
        // Arrange
        var iteration = CreateHealthDto(
            totalWorkItems: 10,
            workItemsWithoutEffort: 5,
            workItemsInProgressWithoutEffort: 3,
            parentProgressIssues: 1,
            blockedItems: 1
        );

        _mockHealthCalculationClient
            .Setup(x => x.CalculateHealthScoreAsync(It.IsAny<CalculateHealthScoreRequest>()))
            .ReturnsAsync(new CalculateHealthScoreResponse
            {
                HealthScore = 0,
                TotalIssues = 10
            });

        // Act
        var score = await _service.CalculateHealthScoreAsync(iteration);

        // Assert
        Assert.AreEqual(0, score);
    }

    [TestMethod]
    public async Task CalculateHealthScoreAsync_CallsApiWithCorrectParameters()
    {
        // Arrange
        var iteration = CreateHealthDto(
            totalWorkItems: 15,
            workItemsWithoutEffort: 3,
            workItemsInProgressWithoutEffort: 2,
            parentProgressIssues: 1,
            blockedItems: 1
        );

        _mockHealthCalculationClient
            .Setup(x => x.CalculateHealthScoreAsync(It.IsAny<CalculateHealthScoreRequest>()))
            .ReturnsAsync(new CalculateHealthScoreResponse { HealthScore = 53, TotalIssues = 7 });

        // Act
        await _service.CalculateHealthScoreAsync(iteration);

        // Assert
        _mockHealthCalculationClient.Verify(x => x.CalculateHealthScoreAsync(
            It.Is<CalculateHealthScoreRequest>(r =>
                r.TotalWorkItems == 15 &&
                r.WorkItemsWithoutEffort == 3 &&
                r.WorkItemsInProgressWithoutEffort == 2 &&
                r.ParentProgressIssues == 1 &&
                r.BlockedItems == 1)), Times.Once);
    }

    [TestMethod]
    public async Task CalculateHealthScoreDetailsAsync_ReturnsApiResponseIncludingTotalIssues()
    {
        // Arrange
        var iteration = CreateHealthDto(
            totalWorkItems: 12,
            workItemsWithoutEffort: 2,
            workItemsInProgressWithoutEffort: 1,
            parentProgressIssues: 1,
            blockedItems: 1
        );

        _mockHealthCalculationClient
            .Setup(x => x.CalculateHealthScoreAsync(It.IsAny<CalculateHealthScoreRequest>()))
            .ReturnsAsync(new CalculateHealthScoreResponse
            {
                HealthScore = 58,
                TotalIssues = 5
            });

        // Act
        var response = await _service.CalculateHealthScoreDetailsAsync(iteration);

        // Assert
        Assert.AreEqual(58, response.HealthScore);
        Assert.AreEqual(5, response.TotalIssues);
    }
}
