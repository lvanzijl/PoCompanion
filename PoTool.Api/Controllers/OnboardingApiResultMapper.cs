using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Controllers;

internal static class OnboardingApiResultMapper
{
    public static ActionResult<OnboardingSuccessEnvelope<T>> ToActionResult<T>(ControllerBase controller, OnboardingOperationResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Succeeded)
        {
            return controller.Ok(new OnboardingSuccessEnvelope<T>(result.Data!, DateTime.UtcNow));
        }

        return result.Error!.Code switch
        {
            OnboardingErrorCode.ValidationFailed => controller.BadRequest(result.Error),
            OnboardingErrorCode.NotFound => controller.NotFound(result.Error),
            OnboardingErrorCode.PermissionDenied => controller.StatusCode(StatusCodes.Status403Forbidden, result.Error),
            OnboardingErrorCode.TfsUnavailable => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, result.Error),
            OnboardingErrorCode.Conflict => controller.Conflict(result.Error),
            OnboardingErrorCode.DependencyViolation => controller.StatusCode(StatusCodes.Status409Conflict, result.Error),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, result.Error)
        };
    }
}
