using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Api.Filters;
using PoTool.Core.Health;
using PoTool.Shared.Health;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public class HealthCalculationControllerTests
{
    [TestMethod]
    public void DeclaredActionResultType_DoesNotPreventAnonymousPayloadSerialization()
    {
        // Arrange
        var controller = new DeclaredResponseContractProbeController();

        // Act
        var actionResult = controller.ReturnAnonymousPayload();

        // Assert
        var okResult = actionResult.Result as OkObjectResult;
        Assert.IsNotNull(okResult);
        Assert.IsNotNull(okResult.Value);
        Assert.AreNotEqual(typeof(CalculateHealthScoreResponse), okResult.Value.GetType());

        var runtimeJson = JsonSerializer.Serialize(okResult.Value);
        var sharedDto = JsonSerializer.Deserialize<CalculateHealthScoreResponse>(runtimeJson);

        Assert.IsNotNull(sharedDto);
        Assert.AreEqual(50, sharedDto.HealthScore);
        Assert.AreEqual(5, sharedDto.TotalIssues);
        StringAssert.Contains(runtimeJson, "DriftSentinel");

        var sharedJson = JsonSerializer.Serialize(sharedDto);
        Assert.AreNotEqual(sharedJson, runtimeJson);
    }

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

    [TestMethod]
    public void CalculateHealthScore_HasRuntimeContractEnforcementAttribute()
    {
        // Arrange
        var method = typeof(HealthCalculationController).GetMethod(nameof(HealthCalculationController.CalculateHealthScore));

        // Act
        var attribute = method?.GetCustomAttributes(typeof(EnforceObjectResultTypeAttribute), inherit: true)
            .Cast<EnforceObjectResultTypeAttribute>()
            .SingleOrDefault();

        // Assert
        Assert.IsNotNull(attribute);
        Assert.AreEqual(typeof(CalculateHealthScoreResponse), attribute.ExpectedType);
    }

    [TestMethod]
    public void RuntimeContractEnforcement_AllowsExactSharedDto()
    {
        // Arrange
        var attribute = new EnforceObjectResultTypeAttribute(typeof(CalculateHealthScoreResponse));
        var result = new OkObjectResult(new CalculateHealthScoreResponse
        {
            HealthScore = 50,
            TotalIssues = 5
        });
        var context = CreateResultExecutingContext(result);

        // Act
        attribute.OnResultExecuting(context);

        // Assert
        Assert.AreSame(result, context.Result);
    }

    [TestMethod]
    public void RuntimeContractEnforcement_ThrowsForAnonymousSuccessPayload()
    {
        // Arrange
        var attribute = new EnforceObjectResultTypeAttribute(typeof(CalculateHealthScoreResponse));
        var context = CreateResultExecutingContext(new OkObjectResult(new
        {
            HealthScore = 50,
            TotalIssues = 5,
            DriftSentinel = "api-only-field"
        }));

        // Act
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => attribute.OnResultExecuting(context));

        // Assert
        StringAssert.Contains(exception.Message, typeof(CalculateHealthScoreResponse).FullName);
    }

    [TestMethod]
    public void RuntimeContractEnforcement_IgnoresNonSuccessResponses()
    {
        // Arrange
        var attribute = new EnforceObjectResultTypeAttribute(typeof(CalculateHealthScoreResponse));
        var result = new ObjectResult("Error calculating health score")
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        var context = CreateResultExecutingContext(result);

        // Act
        attribute.OnResultExecuting(context);

        // Assert
        Assert.AreSame(result, context.Result);
    }

    private static ResultExecutingContext CreateResultExecutingContext(IActionResult result)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new Microsoft.AspNetCore.Routing.RouteData(),
            new ActionDescriptor
            {
                DisplayName = "HealthCalculationController.CalculateHealthScore"
            });

        return new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            result,
            controller: new object());
    }

    private sealed class DeclaredResponseContractProbeController : ControllerBase
    {
        public ActionResult<CalculateHealthScoreResponse> ReturnAnonymousPayload()
        {
            return Ok(new
            {
                HealthScore = 50,
                TotalIssues = 5,
                DriftSentinel = "api-only-field"
            });
        }
    }
}
