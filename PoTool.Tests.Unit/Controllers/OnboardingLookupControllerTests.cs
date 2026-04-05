using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Api.Handlers.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class OnboardingLookupControllerTests
{
    [TestMethod]
    public async Task GetWorkItem_ErrorCodes_MapToExpectedHttpStatus()
    {
        var expectations = new[]
        {
            (OnboardingErrorCode.ValidationFailed, StatusCodes.Status400BadRequest),
            (OnboardingErrorCode.NotFound, StatusCodes.Status404NotFound),
            (OnboardingErrorCode.PermissionDenied, StatusCodes.Status403Forbidden),
            (OnboardingErrorCode.TfsUnavailable, StatusCodes.Status503ServiceUnavailable),
            (OnboardingErrorCode.Conflict, StatusCodes.Status409Conflict),
            (OnboardingErrorCode.DependencyViolation, StatusCodes.Status409Conflict)
        };

        foreach (var (errorCode, expectedStatusCode) in expectations)
        {
            var handler = new Mock<IOnboardingLookupHandler>(MockBehavior.Strict);
            handler
                .Setup(service => service.GetWorkItemAsync("100", It.IsAny<CancellationToken>()))
                .ReturnsAsync(OnboardingOperationResult<WorkItemLookupResultDto>.Failure(
                    new OnboardingErrorDto(errorCode, "Failure", null, errorCode == OnboardingErrorCode.TfsUnavailable)));

            var controller = new OnboardingLookupController(handler.Object);

            var actionResult = await controller.GetWorkItem("100", CancellationToken.None);
            var statusCode = ExtractStatusCode(actionResult.Result!);

            Assert.AreEqual(expectedStatusCode, statusCode, $"Incorrect status code for {errorCode}.");
        }
    }

    private static int ExtractStatusCode(IActionResult result)
    {
        if (result is NotFoundObjectResult)
        {
            return StatusCodes.Status404NotFound;
        }

        if (result is BadRequestObjectResult)
        {
            return StatusCodes.Status400BadRequest;
        }

        if (result is ConflictObjectResult)
        {
            return StatusCodes.Status409Conflict;
        }

        if (result is OkObjectResult)
        {
            return StatusCodes.Status200OK;
        }

        if (result is ObjectResult objectResult)
        {
            return objectResult.StatusCode ?? StatusCodes.Status200OK;
        }

        if (result is StatusCodeResult statusCodeResult)
        {
            return statusCodeResult.StatusCode;
        }

        throw new AssertFailedException($"Unsupported IActionResult type: {result.GetType().Name}");
    }
}
