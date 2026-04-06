using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Api.Handlers.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class OnboardingStatusControllerTests
{
    [TestMethod]
    public async Task GetStatus_Success_ReturnsEnvelope()
    {
        var handler = new Mock<IOnboardingStatusHandler>(MockBehavior.Strict);
        handler
            .Setup(service => service.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<OnboardingStatusDto>.Success(
                new OnboardingStatusDto(
                    OnboardingConfigurationStatus.Complete,
                    OnboardingConfigurationStatus.Complete,
                    OnboardingConfigurationStatus.Complete,
                    OnboardingConfigurationStatus.Complete,
                    Array.Empty<OnboardingStatusIssueDto>(),
                    Array.Empty<OnboardingStatusIssueDto>(),
                    new OnboardingStatusCountsDto(1, 1, 0, 0, 0, 0, 1, 1, 1, 1))));

        var controller = new OnboardingStatusController(handler.Object);

        var actionResult = await controller.GetStatus(CancellationToken.None);

        Assert.IsNotNull(actionResult.Result);
        Assert.IsInstanceOfType<OkObjectResult>(actionResult.Result);
        var okResult = (OkObjectResult)actionResult.Result;
        Assert.IsInstanceOfType<OnboardingSuccessEnvelope<OnboardingStatusDto>>(okResult.Value);
    }

    [TestMethod]
    public async Task GetStatus_ErrorCodes_MapToExpectedHttpStatus()
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
            var handler = new Mock<IOnboardingStatusHandler>(MockBehavior.Strict);
            handler
                .Setup(service => service.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OnboardingOperationResult<OnboardingStatusDto>.Failure(
                    new OnboardingErrorDto(errorCode, "Failure", null, errorCode == OnboardingErrorCode.TfsUnavailable)));

            var controller = new OnboardingStatusController(handler.Object);

            var actionResult = await controller.GetStatus(CancellationToken.None);
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
