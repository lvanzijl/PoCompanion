using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Core.Health;
using PoTool.Shared.Health;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public class HealthCalculationControllerTests
{
    [TestMethod]
    public void CalculateHealthScore_ReturnsHealthScoreAndTotalIssues()
    {
        // Arrange
        var controller = new HealthCalculationController(
            new BacklogHealthCalculator(),
            new Mock<ILogger<HealthCalculationController>>().Object);

        var request = new CalculateHealthScoreRequest
        {
            TotalWorkItems = 10,
            WorkItemsWithoutEffort = 2,
            WorkItemsInProgressWithoutEffort = 1,
            ParentProgressIssues = 1,
            BlockedItems = 1
        };

        // Act
        var actionResult = controller.CalculateHealthScore(request);

        // Assert
        var okResult = actionResult.Result as OkObjectResult;
        Assert.IsNotNull(okResult);

        var response = okResult.Value as CalculateHealthScoreResponse;
        Assert.IsNotNull(response);
        Assert.AreEqual(50, response.HealthScore);
        Assert.AreEqual(5, response.TotalIssues);
    }
}
